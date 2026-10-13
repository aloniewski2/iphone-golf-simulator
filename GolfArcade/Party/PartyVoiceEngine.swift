#if os(iOS)
import AVFoundation
import Observation
import UIKit
import os

// Party voice: the hardware half. Only depends on VoiceCodec.swift and the SDK.
//
// Voice is only ever between the members of the player's own party (a Game Center lobby of up to 4 phones), is OFF until the player turns it on,
// and nothing here touches the microphone or the shared AVAudioSession before `start()` or after `stop()`.
//
// UNVERIFIED WITHOUT HARDWARE (see PARTY_VOICE_NOTES.md): echo cancellation with TV audio, AirPlay routing, Bluetooth, and the interplay with Unity's own
// audio session. Everything else (codecs, packets, jitter buffer, VAD, state machine, mixing) is covered by tests against a fake backend.
// MANUAL TEST STEPS (two or more real phones in one party, one Apple TV / AirPlay TV with the game's sound on it):
//  1. Before turning voice on: start a match; game sound plays on the TV. Turn voice ON for phone A. The game sound MUST stay on the TV
//     (if it jumps to the phone speaker, `.allowAirPlay` was ignored: check `outputRoute`). Turn voice OFF: sound still on the TV, mic dot gone.
//  2. Push-to-talk on A, speak: B and C hear A through THEIR phones (or earbuds). `isTransmitting` is true only while the button is down.
//  3. Open mic on A: silence sends nothing (watch the packet counter in the debug HUD), speaking opens within one syllable, ~0.3 s hangover.
//  4. TV echo: with game sound loud on the TV and open mic on A, B must not hear the game sound back. If it does, switch A to push-to-talk
//     (default) and record the case in the notes: voice-processing AEC only cancels what the phone itself plays, not the TV's AirPlay stream.
//  5. Bluetooth earbuds on A: voice still works, the game sound on the TV stays on the TV. Unplug/replug wired headphones mid-call: no crash, `inputRoute` updates.
//  6. Phone call / Siri / alarm during a live call: voice pauses (`.paused(.interruption)`), game audio recovers, voice resumes after.
//  7. Home button / app switch while live: mic indicator disappears (no background audio mode), state `.paused(.background)`, resumes on return.
//  8. `canTransmit = false` while talking (parental gate): `isTransmitting` drops immediately and the other phones stop hearing within ~100 ms.
//  9. 30 minute open-mic soak: battery drain and thermal state (ProcessInfo.thermalState) acceptable; no growth in memory.
// 10. `stop()`: AVAudioSession category/mode/options equal what they were before `start()` (log them), and Unity's audio is still alive.

// MARK: - Public model

enum VoiceMode: String, Codable, CaseIterable, Sendable {
    case off
    case pushToTalk
    /// Voice-activity detected: silence sends nothing.
    case openMic
}

/// Persisted by the CALLER (the engine never writes defaults). Decoding tolerates missing keys so adding a field never loses a saved selection.
struct VoiceSettings: Codable, Equatable, Sendable {
    var mode: VoiceMode = .off
    /// Mic gain 0...2 applied before the gate and the encoder.
    var inputGain: Float = 1
    /// Master volume of everyone you hear, 0...1.
    var outputVolume: Float = 1
    /// Apple's voice processing (echo cancellation, noise suppression, AGC). Off means a raw mic: the TV will leak into it.
    var noiseSuppression = true
    var perPeerVolume: [String: Float] = [:]
    var mutedPeers: Set<String> = []

    init(mode: VoiceMode = .off, inputGain: Float = 1, outputVolume: Float = 1, noiseSuppression: Bool = true, perPeerVolume: [String: Float] = [:], mutedPeers: Set<String> = []) {
        self.mode = mode; self.inputGain = inputGain; self.outputVolume = outputVolume; self.noiseSuppression = noiseSuppression
        self.perPeerVolume = perPeerVolume; self.mutedPeers = mutedPeers
    }
    init(from decoder: Decoder) throws {
        let c = try decoder.container(keyedBy: Keys.self)
        mode = (try? c.decodeIfPresent(VoiceMode.self, forKey: .mode)) ?? .off
        inputGain = try c.decodeIfPresent(Float.self, forKey: .inputGain) ?? 1
        outputVolume = try c.decodeIfPresent(Float.self, forKey: .outputVolume) ?? 1
        noiseSuppression = try c.decodeIfPresent(Bool.self, forKey: .noiseSuppression) ?? true
        perPeerVolume = try c.decodeIfPresent([String: Float].self, forKey: .perPeerVolume) ?? [:]
        mutedPeers = try c.decodeIfPresent(Set<String>.self, forKey: .mutedPeers) ?? []
    }
    private enum Keys: String, CodingKey { case mode, inputGain, outputVolume, noiseSuppression, perPeerVolume, mutedPeers }
    var clamped: VoiceSettings {
        var s = self
        s.inputGain = s.inputGain.isFinite ? max(0, min(2, s.inputGain)) : 1
        s.outputVolume = s.outputVolume.isFinite ? max(0, min(1, s.outputVolume)) : 1
        s.perPeerVolume = s.perPeerVolume.mapValues { $0.isFinite ? max(0, min(2, $0)) : 1 }
        return s
    }
}

enum VoicePermission: Equatable, Sendable { case undetermined, granted, denied }
enum VoicePauseReason: Equatable, Sendable { case interruption, background }

/// `.paused` is an addition to off/starting/live/failed: the mic is NOT running (the UI must not show the live indicator) and the engine resumes by itself.
enum VoiceEngineState: Equatable, Sendable {
    case off, starting, live
    case paused(VoicePauseReason)
    case failed(String)
}

// MARK: - Hardware seam

enum VoiceBackendEvent: Sendable, Equatable {
    case interruptionBegan
    case interruptionEnded(shouldResume: Bool)
    case routeChanged
    /// AVAudioEngine stopped itself (route/format change): the graph must be rebuilt.
    case configurationChanged
    case mediaServicesReset
    case appBackgrounded
    case appForegrounded
}

/// Everything that touches hardware or the shared AVAudioSession, so the engine's logic runs on a simulator or in unit tests without a microphone.
/// Audio is exchanged as 16 kHz mono Float: the backend owns resampling and channel selection. Implementations must be thread-safe; the engine
/// calls them from the main actor (and `stopIO`/`endSession` once from `deinit`).
protocol VoiceAudioBackend: AnyObject, Sendable {
    var permission: VoicePermission { get }
    func requestPermission() async -> Bool
    @MainActor var appIsActive: Bool { get }
    func setEventHandler(_ handler: (@Sendable (VoiceBackendEvent) -> Void)?)
    /// Saves the current category/mode/options (once) and activates the voice configuration. Idempotent.
    func beginSession() throws
    /// Restores what `beginSession` saved. Never deactivates the session (Unity's audio is using it). Idempotent.
    func endSession()
    /// `capture` receives 16 kHz mono chunks from the tap thread; `render` must fill `frames` 16 kHz mono samples on the audio render thread (no blocking).
    func startIO(noiseSuppression: Bool, capture: @escaping @Sendable (UnsafeBufferPointer<Float>) -> Void, render: @escaping @Sendable (UnsafeMutablePointer<Float>, Int) -> Void) throws
    func stopIO()
    var inputRouteName: String { get }
    var outputRouteName: String { get }
}

// MARK: - Locking and the real-time-safe pieces

/// os_unfair_lock in a stable heap cell. `tryLock` is what the render thread uses: if a writer holds it, the render callback skips one buffer instead of blocking.
final class VoiceLock: @unchecked Sendable {
    private let cell = UnsafeMutablePointer<os_unfair_lock>.allocate(capacity: 1)
    init() { cell.initialize(to: os_unfair_lock()) }
    deinit { cell.deallocate() }
    func lock() { os_unfair_lock_lock(cell) }
    func unlock() { os_unfair_lock_unlock(cell) }
    func tryLock() -> Bool { os_unfair_lock_trylock(cell) }
}

/// Everyone you hear, mixed to 16 kHz mono. Pull-based: the audio render thread calls `render`; the rx queue calls `push`.
final class VoiceMixer: @unchecked Sendable {
    static let maxPeers = 8
    private static let frame = VoiceFormat.frameSamples
    private static let ringCap = 16_000
    private final class Channel {
        let id: String
        let jitter = VoiceJitterBuffer()
        let frame = UnsafeMutablePointer<Float>.allocate(capacity: VoiceMixer.frame)
        var framePos = VoiceMixer.frame
        var framePlaysVoice = false
        var volume: Float = 1, muted = false
        var lastVoiceSample: Int64 = -1
        init(_ id: String) { self.id = id; frame.initialize(repeating: 0, count: VoiceMixer.frame) }
        deinit { frame.deallocate() }
    }
    private let lock = VoiceLock()
    private var channels = [Channel?](repeating: nil, count: VoiceMixer.maxPeers)
    private var outputVolume: Float = 1
    private var rendered: Int64 = 0
    private var sinkEnabled = false
    private let ring = UnsafeMutablePointer<Float>.allocate(capacity: VoiceMixer.ringCap)
    private var ringHead = 0, ringCount = 0

    init() { ring.initialize(repeating: 0, count: VoiceMixer.ringCap); _ = VoiceMixer.frame + VoiceMixer.maxPeers }   // touch the lazy statics off the render thread
    deinit { ring.deallocate() }

    private func channel(_ id: String, create: Bool) -> Channel? {   // lock held
        if let c = channels.first(where: { $0?.id == id }) ?? nil { return c }
        guard create, let slot = channels.firstIndex(where: { $0 == nil }) else { return nil }
        let c = Channel(id); channels[slot] = c; return c
    }

    func configure(peer: String, volume: Float, muted: Bool) {
        lock.lock(); defer { lock.unlock() }
        guard let c = channel(peer, create: true) else { return }
        c.volume = volume
        if muted && !c.muted { c.jitter.reset(); c.framePos = VoiceMixer.frame }
        c.muted = muted
    }
    func setOutputVolume(_ v: Float) { lock.lock(); outputVolume = v; lock.unlock() }
    func setSinkEnabled(_ on: Bool) { lock.lock(); sinkEnabled = on; if !on { ringHead = 0; ringCount = 0 }; lock.unlock() }
    func remove(peer: String) {
        lock.lock(); defer { lock.unlock() }
        if let i = channels.firstIndex(where: { $0?.id == peer }) { channels[i] = nil }
    }
    /// Drops all audio in flight but keeps per-peer volume/mute.
    func resetStreams() {
        lock.lock(); defer { lock.unlock() }
        for c in channels { c?.jitter.reset(); c?.framePos = VoiceMixer.frame; c?.lastVoiceSample = -1 }
        ringHead = 0; ringCount = 0
    }
    func removeAll() { lock.lock(); channels = [Channel?](repeating: nil, count: VoiceMixer.maxPeers); ringHead = 0; ringCount = 0; lock.unlock() }

    func push(peer: String, packet: VoicePacket, samples: [Float], nowMs: Double) {
        lock.lock(); defer { lock.unlock() }
        guard let c = channel(peer, create: true), !c.muted else { return }
        c.jitter.push(sequence: packet.sequence, timestampMs: packet.timestampMs, samples: samples, speechEnd: packet.isSpeechEnd, nowMs: nowMs)
    }

    /// Peers whose audio actually played in the last `windowMs`.
    func speaking(windowMs: Int = 400) -> Set<String> {
        lock.lock(); defer { lock.unlock() }
        let window = Int64(windowMs * VoiceFormat.sampleRate / 1000)
        return Set(channels.compactMap { c in c.flatMap { $0.lastVoiceSample >= 0 && rendered - $0.lastVoiceSample < window ? $0.id : nil } })
    }

    /// Audio render thread. Never blocks, never allocates: a contended lock yields one buffer of silence.
    func render(_ out: UnsafeMutablePointer<Float>, frames: Int) {
        out.update(repeating: 0, count: frames)
        guard lock.tryLock() else { return }
        defer { lock.unlock() }
        let F = VoiceMixer.frame
        for slot in 0..<VoiceMixer.maxPeers {
            guard let c = channels[slot], !c.muted else { continue }
            var produced = 0
            while produced < frames {
                if c.framePos >= F {
                    if c.jitter.isIdle { break }
                    c.framePlaysVoice = c.jitter.pull(into: c.frame) == .voice
                    c.framePos = 0
                }
                let take = min(frames - produced, F - c.framePos)
                let g = c.volume
                for i in 0..<take { out[produced + i] += c.frame[c.framePos + i] * g }
                if c.framePlaysVoice { c.lastVoiceSample = rendered + Int64(produced + take) }
                c.framePos += take; produced += take
            }
        }
        let v = outputVolume
        for i in 0..<frames { out[i] = max(-1, min(1, out[i] * v)) }
        if sinkEnabled {
            let cap = VoiceMixer.ringCap
            for i in 0..<frames {
                if ringCount == cap { ringHead = (ringHead + 1) % cap; ringCount -= 1 }
                ring[(ringHead + ringCount) % cap] = out[i]; ringCount += 1
            }
        }
        rendered += Int64(frames)
    }

    /// Moves what the render thread produced to the recorder's thread. Returns the sample count copied.
    func drainSink(into dst: UnsafeMutablePointer<Float>, max maxCount: Int) -> Int {
        lock.lock(); defer { lock.unlock() }
        let n = min(ringCount, maxCount), cap = VoiceMixer.ringCap
        for i in 0..<n { dst[i] = ring[(ringHead + i) % cap] }
        ringHead = (ringHead + n) % cap; ringCount -= n
        return n
    }
}

enum VoiceTxEvent: Sendable {
    case packet(Data)
    case gate(Bool)
}

/// Mic -> gain -> VAD / push-to-talk gate -> encoder -> packets. Everything runs on one serial queue; `ingest` is the only entry from the tap thread.
final class VoiceTxPipeline: @unchecked Sendable {
    struct Controls: Equatable, Sendable {
        var mode = VoiceMode.off
        var gain: Float = 1
        var pttDown = false
        var selfMuted = false
        var canTransmit = true
    }
    let queue = DispatchQueue(label: "party.voice.tx", qos: .userInteractive)
    private let lock = VoiceLock()
    private var controls = Controls()
    private var emit: (@Sendable (VoiceTxEvent) -> Void)?
    private var sink: (@Sendable (UnsafePointer<Float>, Int, Int, Int) -> Void)?
    private var meter: Float = 0
    // queue-confined:
    private var acc = [Float]()
    private var encoder: VoiceStreamEncoder?
    /// Carried across restarts (resume, route rebuild): a new stream that restarted at 0 would look like late stragglers to a peer still holding the old numbers.
    private var nextSequence = UInt16.random(in: 0...UInt16.max)
    private var vad = VoiceActivityDetector()
    private var open = false
    private var elapsedFrames = 0
    private var preroll = [[Float]]()
    private let silence = [Float](repeating: 0, count: VoiceFormat.frameSamples)

    func setControls(_ c: Controls) { lock.lock(); controls = c; lock.unlock() }
    func setEmit(_ e: (@Sendable (VoiceTxEvent) -> Void)?) { lock.lock(); emit = e; lock.unlock() }
    func setSink(_ s: (@Sendable (UnsafePointer<Float>, Int, Int, Int) -> Void)?) { lock.lock(); sink = s; lock.unlock() }
    func level() -> Float { lock.lock(); defer { lock.unlock() }; return meter }
    private func snapshot() -> (Controls, (@Sendable (VoiceTxEvent) -> Void)?, (@Sendable (UnsafePointer<Float>, Int, Int, Int) -> Void)?) {
        lock.lock(); defer { lock.unlock() }; return (controls, emit, sink)
    }

    /// Fresh stream: new encoder (sequence/timestamps restart), VAD floor re-learned.
    func begin(codec: VoiceCodec) {
        queue.sync {
            encoder = VoiceStreamEncoder(codec: codec, startSequence: nextSequence); acc.removeAll(); preroll.removeAll(); open = false; elapsedFrames = 0; vad.reset()
            lock.lock(); meter = 0; lock.unlock()
        }
    }
    /// Stops the stream (waits for queued frames). If the gate was open the last packets (speech-end marker) are RETURNED for the caller to send,
    /// because the event stream is being torn down; with the kill switch or self-mute on, no half-framed audio is included.
    func end() -> [Data] {
        queue.sync {
            let (c, _, _) = snapshot()
            var tail = [Data]()
            if open, let encoder {
                if !(c.canTransmit && !c.selfMuted) { encoder.reset() }
                tail = encoder.finish().map { $0.serialized() }
            }
            if let encoder { nextSequence = encoder.sequence }
            open = false; encoder = nil; acc.removeAll(); preroll.removeAll()
            lock.lock(); meter = 0; lock.unlock()
            return tail
        }
    }

    /// Tap thread: copy and hop. (The tap thread is not the render thread; an allocation here is fine, and the copy keeps the tap buffer free.)
    func ingest(_ samples: UnsafeBufferPointer<Float>) {
        guard samples.count > 0 else { return }
        let copy = Array(samples)
        queue.async { [self] in
            guard encoder != nil else { return }
            acc += copy
            var start = 0
            while acc.count - start >= VoiceFormat.frameSamples {
                processFrame(Array(acc[start..<start + VoiceFormat.frameSamples])); start += VoiceFormat.frameSamples
            }
            if start > 0 { acc.removeFirst(start) }
        }
    }

    private func processFrame(_ raw: [Float]) {
        guard let encoder else { return }
        let (c, emit, sink) = snapshot()
        let g = c.gain
        let frame = raw.map { max(-1, min(1, $0 * g)) }
        let rms = frame.withUnsafeBufferPointer { VoiceActivityDetector.rms($0) }
        // Meter: instant attack, slow release, perceptually stretched. Local only; shown even while the gate is closed so the player sees the mic works.
        lock.lock(); let m = min(1, rms.squareRoot() * 1.8); meter = m > meter ? m : meter * 0.85 + m * 0.15; lock.unlock()

        let vadOpen = c.mode == .openMic ? vad.process(frame) : false
        let allowed = c.canTransmit && !c.selfMuted
        let wantOpen = allowed && (c.mode == .pushToTalk ? c.pttDown : c.mode == .openMic ? vadOpen : false)
        if wantOpen && !open {
            open = true
            encoder.beginSpurt(atMs: UInt32(truncatingIfNeeded: elapsedFrames * VoiceFormat.frameMs))
            emit?(.gate(true))
            // Open mic: the VAD fires a frame late, so the first syllable's onset comes from the 2-frame pre-roll. Push-to-talk sends nothing from before the press.
            if c.mode == .openMic { for p in preroll { for pkt in encoder.encode(p) { emit?(.packet(pkt.serialized())) } } }
            preroll.removeAll(keepingCapacity: true)
        }
        if open {
            if wantOpen {
                for pkt in encoder.encode(frame) { emit?(.packet(pkt.serialized())) }
                frame.withUnsafeBufferPointer { b in sink?(b.baseAddress!, b.count, VoiceFormat.sampleRate, 1) }
            } else {
                // A natural release keeps the tail; a mute / kill switch drops whatever is half-framed so nothing leaks after the player said stop.
                if !allowed { encoder.reset() }
                for pkt in encoder.finish() { emit?(.packet(pkt.serialized())) }
                emit?(.gate(false)); open = false
                silence.withUnsafeBufferPointer { b in sink?(b.baseAddress!, b.count, VoiceFormat.sampleRate, 1) }
            }
        } else {
            preroll.append(frame); if preroll.count > 2 { preroll.removeFirst() }
            silence.withUnsafeBufferPointer { b in sink?(b.baseAddress!, b.count, VoiceFormat.sampleRate, 1) }
        }
        elapsedFrames += 1
    }
}

/// All the non-main-actor machinery in one Sendable object, so `deinit` (nonisolated) can shut it down and no closure ever captures the engine.
final class VoiceRuntime: @unchecked Sendable {
    let mixer = VoiceMixer()
    let tx = VoiceTxPipeline()
    let rxQueue = DispatchQueue(label: "party.voice.rx", qos: .userInitiated)
    private var decoders = [String: VoiceStreamDecoder]()   // rxQueue-confined
    private let lock = VoiceLock()
    private var eventContinuation: AsyncStream<VoiceTxEvent>.Continuation?
    private var sinkTimer: DispatchSourceTimer?
    private var sinkScratch = UnsafeMutablePointer<Float>.allocate(capacity: 4096)   // tx.queue-confined

    deinit { sinkTimer?.cancel(); sinkScratch.deallocate() }

    /// New event stream for one live session; the previous one (if any) is finished.
    func beginEvents() -> AsyncStream<VoiceTxEvent> {
        let (stream, cont) = AsyncStream<VoiceTxEvent>.makeStream(bufferingPolicy: .unbounded)
        lock.lock(); let old = eventContinuation; eventContinuation = cont; lock.unlock()
        old?.finish()
        tx.setEmit { [weak self] e in self?.yield(e) }
        return stream
    }
    private func yield(_ e: VoiceTxEvent) { lock.lock(); let c = eventContinuation; lock.unlock(); c?.yield(e) }
    func endEvents() {
        tx.setEmit(nil)
        lock.lock(); let c = eventContinuation; eventContinuation = nil; lock.unlock()
        c?.finish()
    }

    func receive(peer: String, packet: VoicePacket, nowMs: Double) {
        rxQueue.async { [self] in
            if decoders[peer] == nil, decoders.count >= VoiceMixer.maxPeers { return }
            let dec = decoders[peer] ?? { let d = VoiceStreamDecoder(); decoders[peer] = d; return d }()
            guard let pcm = dec.decode(packet) else { return }
            mixer.push(peer: peer, packet: packet, samples: pcm, nowMs: nowMs)
        }
    }
    func forget(peer: String) { rxQueue.async { [self] in decoders[peer] = nil }; mixer.remove(peer: peer) }
    func clearDecoders() { rxQueue.async { [self] in decoders.removeAll() } }

    /// Recorder feed. Source 1 is called from `tx.queue` per frame; source 0 comes from the render thread via a ring that this timer drains.
    func configureSink(_ sink: (@Sendable (UnsafePointer<Float>, Int, Int, Int) -> Void)?, running: Bool) {
        tx.setSink(sink); mixer.setSinkEnabled(sink != nil && running)
        tx.queue.async { [self] in
            sinkTimer?.cancel(); sinkTimer = nil
            guard let sink, running else { return }
            let t = DispatchSource.makeTimerSource(queue: tx.queue)
            t.schedule(deadline: .now() + .milliseconds(20), repeating: .milliseconds(20), leeway: .milliseconds(5))
            t.setEventHandler { [mixer, sinkScratch] in
                let n = mixer.drainSink(into: sinkScratch, max: 4096)
                if n > 0 { sink(sinkScratch, n, VoiceFormat.sampleRate, 0) }
            }
            t.resume(); sinkTimer = t
        }
    }

    func drainQueues() async {
        await withCheckedContinuation { (k: CheckedContinuation<Void, Never>) in tx.queue.async { self.rxQueue.async { k.resume() } } }
    }
    func shutdown() { endEvents(); tx.queue.async { [self] in sinkTimer?.cancel(); sinkTimer = nil }; mixer.setSinkEnabled(false) }
}

// MARK: - Engine

@MainActor @Observable
final class PartyVoiceEngine {
    private(set) var state: VoiceEngineState = .off
    /// True from the moment the gate opens until it closes: this is the "your voice is going out" indicator. Always false unless `state == .live`.
    private(set) var isTransmitting = false
    private(set) var micLevel: Float = 0
    /// Peer ids heard in the last 400 ms.
    private(set) var speaking: Set<String> = []
    private(set) var inputRoute = ""
    private(set) var outputRoute = ""
    private(set) var permission: VoicePermission
    private(set) var settings = VoiceSettings()
    private(set) var codec: VoiceCodecID?
    /// Kill switch for parental / age gates: flipping it false stops transmission within one 20 ms frame and the UI flag drops immediately.
    var canTransmit = true {
        didSet { if !canTransmit { isTransmitting = false }; applyControls() }
    }
    /// Each outgoing encoded packet (raw bytes). The caller base64s it and broadcasts to the party.
    @ObservationIgnored var onPacket: (@MainActor (Data) -> Void)?
    /// (samples, frameCount, sampleRate, source). Source 0: mixed remote voice going to the speaker. 1: my own mic while transmitting (silence otherwise).
    /// Called from audio-adjacent queues, never the main actor; the callee must be real-time friendly (copy and return).
    @ObservationIgnored var captureSink: (@Sendable (UnsafePointer<Float>, Int, Int, Int) -> Void)? {
        didSet { runtime.configureSink(captureSink, running: state == .live) }
    }

    @ObservationIgnored private let backend: any VoiceAudioBackend
    @ObservationIgnored private let runtime = VoiceRuntime()
    @ObservationIgnored private let codecOverride: VoiceCodecID?
    @ObservationIgnored private var pttDown = false
    @ObservationIgnored private var selfMuted = false
    @ObservationIgnored private var generation = 0
    @ObservationIgnored private var wantsLive = false
    @ObservationIgnored private var consumeTask: Task<Void, Never>?
    @ObservationIgnored private var pollTask: Task<Void, Never>?
    @ObservationIgnored private var limiters = [String: VoiceRateLimiter]()

    init(backend: (any VoiceAudioBackend)? = nil, codec: VoiceCodecID? = nil) {
        let b = backend ?? AVVoiceAudioBackend()
        self.backend = b; codecOverride = codec; permission = b.permission
    }
    deinit {
        // Nonisolated: only Sendable `let`s are touched. Closing the IO here is what makes an engine that is dropped while live safe.
        runtime.shutdown(); backend.setEventHandler(nil); backend.stopIO(); backend.endSession()
    }

    // MARK: lifecycle

    /// Turns voice on. Asks for mic permission if undetermined. Does nothing for `mode == .off`. Safe to call again (then it just applies the settings).
    func start(_ newSettings: VoiceSettings) async {
        switch state {
        case .live, .starting, .paused: update(newSettings); return
        case .off, .failed: break
        }
        settings = newSettings.clamped
        guard settings.mode != .off else { state = .off; return }
        permission = backend.permission   // the player may have changed it in Settings since we last looked
        generation += 1; let mine = generation
        state = .starting; wantsLive = true
        // Registered now (not at bringUp) so a foreground / interruption-end event can resume a `.paused` start.
        backend.setEventHandler { [weak self] event in Task { @MainActor in self?.handle(event) } }
        if permission != .granted {
            let ok = await requestPermission()
            guard mine == generation else { return }   // stop() ran while the system prompt was up
            guard ok else { fail("Microphone access is off. Turn it on in Settings to talk to your party."); return }
        }
        // Probing builds real converters (tens of ms): off the main actor, and cached for the process.
        let chosen: VoiceCodecID
        if let codecOverride { chosen = codecOverride } else { chosen = await Task.detached(priority: .userInitiated) { VoiceCodecFactory.best }.value }
        guard mine == generation else { return }
        guard backend.appIsActive else { state = .paused(.background); return }
        bringUp(chosen)
    }

    /// Idempotent. Releases the mic and puts the AVAudioSession back as it was.
    func stop() {
        generation += 1
        guard wantsLive || state != .off else { return }   // never started (or already stopped): touch nothing
        wantsLive = false
        tearDown()
        state = .off
        clearLive()
    }

    func update(_ newSettings: VoiceSettings) {
        let old = settings, new = newSettings.clamped
        settings = new
        guard state != .off else { return }
        if new.mode == .off { stop(); return }
        applyControls(); applyMixer()
        if state == .live, old.noiseSuppression != new.noiseSuppression { restartIO() }
    }

    /// After an interruption that did not say "resume" (or any `.paused` state) the app can retry from a user action.
    func resumeIfPaused() { resume() }

    func requestPermission() async -> Bool {
        let ok = permission == .granted ? true : await backend.requestPermission()
        permission = backend.permission
        return ok
    }

    // MARK: controls

    func setPushToTalk(_ down: Bool) { pttDown = down; applyControls() }
    func setMuted(_ muted: Bool) { selfMuted = muted; if muted { isTransmitting = false }; applyControls() }
    func setPeer(_ id: String, muted: Bool) {
        if muted { settings.mutedPeers.insert(id) } else { settings.mutedPeers.remove(id) }
        applyMixer()
    }
    func setPeer(_ id: String, volume: Float) {
        settings.perPeerVolume[id] = volume.isFinite ? max(0, min(2, volume)) : 1
        applyMixer()
    }
    func removePeer(_ id: String) {
        settings.perPeerVolume[id] = nil; settings.mutedPeers.remove(id); limiters[id] = nil; speaking.remove(id)
        runtime.forget(peer: id)
    }

    /// An incoming packet from a party member. Anything malformed, flooding or from a muted peer is dropped before decode.
    func receive(_ packet: Data, from peer: String) {
        guard state == .live, !settings.mutedPeers.contains(peer) else { return }
        let now = ProcessInfo.processInfo.systemUptime * 1000
        if limiters[peer] == nil, limiters.count >= VoiceMixer.maxPeers * 2 { return }   // a stream of made-up peer ids cannot grow state
        var limiter = limiters[peer] ?? VoiceRateLimiter()
        let allowed = limiter.allow(nowMs: now); limiters[peer] = limiter
        guard allowed, let parsed = VoicePacket.parse(packet) else { return }
        runtime.receive(peer: peer, packet: parsed, nowMs: now)
    }

    // MARK: internals

    private func applyControls() {
        runtime.tx.setControls(.init(mode: settings.mode, gain: settings.inputGain, pttDown: pttDown, selfMuted: selfMuted, canTransmit: canTransmit))
    }
    private func applyMixer() {
        runtime.mixer.setOutputVolume(settings.outputVolume)
        for (id, v) in settings.perPeerVolume { runtime.mixer.configure(peer: id, volume: v, muted: settings.mutedPeers.contains(id)) }
        for id in settings.mutedPeers where settings.perPeerVolume[id] == nil { runtime.mixer.configure(peer: id, volume: 1, muted: true) }
    }

    private func fail(_ message: String) {
        generation += 1; wantsLive = false
        tearDown(); clearLive()
        state = .failed(message)
    }

    /// Starts the session + IO + pipelines. State becomes .live or .failed.
    private func bringUp(_ chosen: VoiceCodecID) {
        guard let c = VoiceCodecFactory.make(chosen) ?? VoiceCodecFactory.make(.pcm16) else { fail("No voice codec available."); return }
        codec = c.id
        runtime.tx.begin(codec: c)
        applyControls(); applyMixer()
        let stream = runtime.beginEvents()
        do { try startHardware() } catch {
            fail("Voice chat could not start: \(error.localizedDescription)"); return
        }
        state = .live
        runtime.configureSink(captureSink, running: true)
        consumeTask = Task { @MainActor [weak self] in
            for await e in stream {
                guard let self else { return }
                switch e {
                case .packet(let d): if self.state == .live { self.onPacket?(d) }
                case .gate(let open): self.isTransmitting = open && self.state == .live && self.canTransmit
                }
            }
        }
        pollTask = Task { @MainActor [weak self] in
            while !Task.isCancelled {
                try? await Task.sleep(for: .milliseconds(100))
                guard let self, !Task.isCancelled else { return }
                self.poll()
            }
        }
        updateRoutes()
    }

    private func startHardware() throws {
        try backend.beginSession()
        let rt = runtime
        try backend.startIO(noiseSuppression: settings.noiseSuppression,
                            capture: { rt.tx.ingest($0) },
                            render: { rt.mixer.render($0, frames: $1) })
    }

    /// Mic off, session restored, tasks cancelled. Does not change `state`.
    private func tearDown() {
        consumeTask?.cancel(); consumeTask = nil; pollTask?.cancel(); pollTask = nil
        runtime.configureSink(nil, running: false)
        backend.stopIO()
        sendTail(runtime.tx.end())
        runtime.endEvents()
        runtime.mixer.resetStreams(); runtime.clearDecoders()
        backend.setEventHandler(nil)
        backend.endSession()
    }

    /// The speech-end marker of a stream being closed goes out directly so the other phones stop waiting at once.
    private func sendTail(_ packets: [Data]) { if state == .live || state == .starting { for p in packets { onPacket?(p) } } }

    private func clearLive() {
        isTransmitting = false; micLevel = 0; speaking = []; inputRoute = ""; outputRoute = ""; limiters = [:]
    }

    /// Rebuild the audio graph (route/format change, media services reset, voice-processing toggle) without touching the stream or state.
    private func restartIO() {
        backend.stopIO()
        runtime.mixer.resetStreams()
        do { try startHardware(); updateRoutes() }
        catch { fail("Voice chat stopped: \(error.localizedDescription)") }
    }

    private func pause(_ reason: VoicePauseReason, endSession: Bool) {
        guard state == .live else { return }
        backend.stopIO()
        sendTail(runtime.tx.end())
        consumeTask?.cancel(); consumeTask = nil; pollTask?.cancel(); pollTask = nil
        runtime.configureSink(nil, running: false)
        runtime.mixer.resetStreams()
        if endSession { backend.endSession() }
        clearLive()
        state = .paused(reason)
    }

    private func resume() {
        guard case .paused = state, wantsLive else { return }
        guard backend.appIsActive else { return }
        let reuse = codec ?? VoiceCodecFactory.best
        state = .starting
        bringUp(reuse)
    }

    func handle(_ event: VoiceBackendEvent) {
        switch event {
        case .interruptionBegan: pause(.interruption, endSession: false)
        case .interruptionEnded(let shouldResume): if shouldResume { resume() }
        case .appBackgrounded:
            // No background audio mode: iOS would suspend us with the mic open and show the orange dot, so release everything now.
            if state == .live { pause(.background, endSession: true) }
        case .appForegrounded: resume()
        case .routeChanged: if state == .live { updateRoutes() }
        case .configurationChanged, .mediaServicesReset: if state == .live { restartIO() }
        }
    }

    private func updateRoutes() { inputRoute = backend.inputRouteName; outputRoute = backend.outputRouteName }

    private func poll() {
        guard state == .live else { return }
        let level = runtime.tx.level(); if abs(level - micLevel) > 0.01 { micLevel = level }
        let now = runtime.mixer.speaking(); if now != speaking { speaking = now }
    }

    /// Test hook: waits until work queued so far on the audio queues has run and its results reached the main actor.
    func settle() async {
        await runtime.drainQueues()
        for _ in 0..<6 { await Task.yield() }
    }
}

// MARK: - Real backend

enum VoiceBackendError: LocalizedError {
    case noInput
    var errorDescription: String? { "No microphone input is available." }
}

/// AVAudioEngine + AVAudioSession implementation. UNVERIFIED on hardware (see the header and PARTY_VOICE_NOTES.md).
final class AVVoiceAudioBackend: VoiceAudioBackend, @unchecked Sendable {
    private let lock = NSLock()
    private var engine: AVAudioEngine?
    private var saved: (category: AVAudioSession.Category, mode: AVAudioSession.Mode, options: AVAudioSession.CategoryOptions)?
    private var handler: (@Sendable (VoiceBackendEvent) -> Void)?
    private var tokens = [NSObjectProtocol]()
    private var configToken: NSObjectProtocol?

    var permission: VoicePermission {
        switch AVAudioApplication.shared.recordPermission {
        case .granted: .granted
        case .denied: .denied
        default: .undetermined
        }
    }
    func requestPermission() async -> Bool { await AVAudioApplication.requestRecordPermission() }
    @MainActor var appIsActive: Bool { UIApplication.shared.applicationState == .active }

    init() {
        let nc = NotificationCenter.default
        let session = AVAudioSession.sharedInstance()
        func add(_ name: Notification.Name, object: Any? = nil, _ map: @escaping @Sendable (Notification) -> VoiceBackendEvent?) {
            tokens.append(nc.addObserver(forName: name, object: object, queue: nil) { [weak self] n in
                guard let self, let e = map(n) else { return }
                self.lock.lock(); let h = self.handler; self.lock.unlock()
                h?(e)
            })
        }
        add(AVAudioSession.interruptionNotification, object: session) { n in
            guard let raw = n.userInfo?[AVAudioSessionInterruptionTypeKey] as? UInt, let type = AVAudioSession.InterruptionType(rawValue: raw) else { return nil }
            if type == .began { return .interruptionBegan }
            let opts = AVAudioSession.InterruptionOptions(rawValue: (n.userInfo?[AVAudioSessionInterruptionOptionKey] as? UInt) ?? 0)
            return .interruptionEnded(shouldResume: opts.contains(.shouldResume))
        }
        add(AVAudioSession.routeChangeNotification, object: session) { _ in .routeChanged }
        add(AVAudioSession.mediaServicesWereResetNotification, object: session) { _ in .mediaServicesReset }
        add(UIApplication.didEnterBackgroundNotification) { _ in .appBackgrounded }
        add(UIApplication.willEnterForegroundNotification) { _ in .appForegrounded }
    }
    deinit {
        tokens.forEach { NotificationCenter.default.removeObserver($0) }
        if let configToken { NotificationCenter.default.removeObserver(configToken) }
    }

    func setEventHandler(_ h: (@Sendable (VoiceBackendEvent) -> Void)?) { lock.lock(); handler = h; lock.unlock() }

    func beginSession() throws {
        let s = AVAudioSession.sharedInstance()
        lock.lock(); if saved == nil { saved = (s.category, s.mode, s.categoryOptions) }; lock.unlock()
        // Why each option (the session is shared with Unity, so every flag is a decision):
        //  .allowAirPlay      playAndRecord drops AirPlay output unless this is set: without it the game's TV sound would move to the phone speaker.
        //  .allowBluetooth    lets a Bluetooth headset's microphone (HFP) be the input.
        //  .allowBluetoothA2DP keeps stereo A2DP output to earbuds when the input is the phone mic.
        //  .defaultToSpeaker  playAndRecord otherwise plays through the earpiece receiver.
        //  .mixWithOthers     do not interrupt Unity's audio or the player's music when we activate.
        // Mode .default (not .voiceChat): voiceChat forces the receiver/AGC route policy and is reported to disable AirPlay. Apple's voice
        // processing comes from the engine's input node instead (startIO). All of this must be checked on a real phone with an Apple TV.
        #if compiler(>=6.2)
        let hfp = AVAudioSession.CategoryOptions.allowBluetoothHFP   // the iOS 26 SDK name of .allowBluetooth
        #else
        let hfp = AVAudioSession.CategoryOptions.allowBluetooth
        #endif
        try s.setCategory(.playAndRecord, mode: .default, options: [.allowAirPlay, hfp, .allowBluetoothA2DP, .defaultToSpeaker, .mixWithOthers])
        // iOS mutes haptics and system sounds while the mic records, and the mic runs for the whole party: without this
        // every racket and club strike (and the phone-speaker knock) goes silent the moment voice is switched on.
        try? s.setAllowHapticsAndSystemSoundsDuringRecording(true)
        try s.setActive(true)
    }

    func endSession() {
        lock.lock(); let prev = saved; saved = nil; lock.unlock()
        guard let prev else { return }
        // Restore the category only. setActive(false) here would kill Unity's audio, which is running on this same session.
        try? AVAudioSession.sharedInstance().setCategory(prev.category, mode: prev.mode, options: prev.options)
    }

    func startIO(noiseSuppression: Bool, capture: @escaping @Sendable (UnsafeBufferPointer<Float>) -> Void, render: @escaping @Sendable (UnsafeMutablePointer<Float>, Int) -> Void) throws {
        stopIO()
        let engine = AVAudioEngine()
        let input = engine.inputNode
        if noiseSuppression {
            // Echo cancellation + noise suppression + AGC. The TV's sound reaches the mic: this is the whole reason for the feature.
            try input.setVoiceProcessingEnabled(true)
            // VPIO ducks other audio by default, which would make the game's sound dip whenever voice is on: ask for the minimum (iOS 17+).
            input.voiceProcessingOtherAudioDuckingConfiguration = AVAudioVoiceProcessingOtherAudioDuckingConfiguration(enableAdvancedDucking: false, duckingLevel: .min)
        }
        // Read the formats only after voice processing changed them.
        let hw = input.outputFormat(forBus: 0)
        guard hw.sampleRate > 0, hw.channelCount > 0 else { throw VoiceBackendError.noInput }
        let grabber = MonoResampler(deliver: capture)
        // format nil = whatever the node delivers (a mismatching explicit format raises an uncatchable ObjC exception); the grabber reads the buffer's own rate.
        input.installTap(onBus: 0, bufferSize: 1024, format: nil) { buffer, _ in grabber.process(buffer) }

        guard let fmt16 = AVAudioFormat(commonFormat: .pcmFormatFloat32, sampleRate: Double(VoiceFormat.sampleRate), channels: 1, interleaved: false) else { input.removeTap(onBus: 0); throw VoiceBackendError.noInput }
        let source = AVAudioSourceNode(format: fmt16) { isSilence, _, frames, abl in
            let list = UnsafeMutableAudioBufferListPointer(abl)
            if let p = list.first?.mData?.assumingMemoryBound(to: Float.self) { render(p, Int(frames)); isSilence.pointee = false }
            return noErr
        }
        engine.attach(source)
        // The engine's mixer resamples 16 kHz to whatever the output runs at (never touch mainMixerNode before voice processing is decided).
        engine.connect(source, to: engine.mainMixerNode, format: fmt16)
        engine.prepare()
        do { try engine.start() } catch { input.removeTap(onBus: 0); throw error }
        let token = NotificationCenter.default.addObserver(forName: .AVAudioEngineConfigurationChange, object: engine, queue: nil) { [weak self] _ in
            guard let self else { return }
            self.lock.lock(); let h = self.handler; self.lock.unlock()
            h?(.configurationChanged)
        }
        lock.lock(); self.engine = engine; configToken = token; lock.unlock()
    }

    func stopIO() {
        lock.lock(); let e = engine, t = configToken; engine = nil; configToken = nil; lock.unlock()
        if let t { NotificationCenter.default.removeObserver(t) }
        guard let e else { return }
        e.inputNode.removeTap(onBus: 0)
        e.stop()
    }

    var inputRouteName: String { AVAudioSession.sharedInstance().currentRoute.inputs.first?.portName ?? "" }
    var outputRouteName: String { AVAudioSession.sharedInstance().currentRoute.outputs.first?.portName ?? "" }
}

/// Tap thread helper: channel 0 of the hardware buffer -> 16 kHz mono. (With voice processing some devices report several channels; the
/// first one is the processed voice. AVAudioConverter cannot downmix an arbitrary layout to mono, so this picks the channel itself.)
private final class MonoResampler: @unchecked Sendable {
    private let deliver: @Sendable (UnsafeBufferPointer<Float>) -> Void
    private let outFormat = AVAudioFormat(commonFormat: .pcmFormatFloat32, sampleRate: Double(VoiceFormat.sampleRate), channels: 1, interleaved: false)!
    private var rate = 0.0
    private var monoFormat: AVAudioFormat?
    private var converter: AVAudioConverter?
    private var monoIn: AVAudioPCMBuffer?
    private var monoOut: AVAudioPCMBuffer?
    private final class Feed: @unchecked Sendable { var buffer: AVAudioPCMBuffer?; var fed = false }

    init(deliver: @escaping @Sendable (UnsafeBufferPointer<Float>) -> Void) { self.deliver = deliver }

    func process(_ buffer: AVAudioPCMBuffer) {
        let n = Int(buffer.frameLength)
        guard n > 0, let src = buffer.floatChannelData?[0] else { return }
        let hwRate = buffer.format.sampleRate
        if hwRate == Double(VoiceFormat.sampleRate) { deliver(UnsafeBufferPointer(start: src, count: n)); return }
        if hwRate != rate {   // first buffer, or the route changed the hardware rate under us
            rate = hwRate; monoIn = nil; monoOut = nil
            monoFormat = AVAudioFormat(commonFormat: .pcmFormatFloat32, sampleRate: hwRate, channels: 1, interleaved: false)
            converter = monoFormat.flatMap { AVAudioConverter(from: $0, to: outFormat) }
        }
        guard let converter, let monoFormat else { return }
        if monoIn == nil || monoIn!.frameCapacity < AVAudioFrameCount(n) { monoIn = AVAudioPCMBuffer(pcmFormat: monoFormat, frameCapacity: AVAudioFrameCount(n * 2)) }
        let outCap = AVAudioFrameCount(Double(n) * Double(VoiceFormat.sampleRate) / hwRate) + 64
        if monoOut == nil || monoOut!.frameCapacity < outCap { monoOut = AVAudioPCMBuffer(pcmFormat: outFormat, frameCapacity: outCap * 2) }
        guard let mi = monoIn, let mo = monoOut, let dst = mi.floatChannelData?[0] else { return }
        dst.update(from: src, count: n); mi.frameLength = AVAudioFrameCount(n); mo.frameLength = 0
        let feed = Feed(); feed.buffer = mi
        var error: NSError?
        let status = converter.convert(to: mo, error: &error) { _, s in
            if feed.fed { s.pointee = .noDataNow; return nil }
            feed.fed = true; s.pointee = .haveData; return feed.buffer
        }
        guard status != .error, mo.frameLength > 0, let out = mo.floatChannelData?[0] else { return }
        deliver(UnsafeBufferPointer(start: out, count: Int(mo.frameLength)))
    }
}
#endif
