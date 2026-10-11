import Foundation
import Observation

/// The party's voice, wired up: the engine (microphone, codec, playback), the party channel it travels over, the safety gates that can
/// switch it off at any moment, and the recording. The engine itself touches no microphone or audio session until `turnOn()`.
@MainActor @Observable
final class PartyVoiceCoordinator {
    static let shared = PartyVoiceCoordinator()

    private(set) var prefs: VoiceSettings
    private(set) var notice = ""
    private(set) var selfMuted = false
    @ObservationIgnored let engine: PartyVoiceEngine
    @ObservationIgnored let chat: PartyChat
    @ObservationIgnored private let defaults: UserDefaults
    @ObservationIgnored private var gate: Task<Void, Never>?

    init(engine: PartyVoiceEngine? = nil, chat: PartyChat = .shared, defaults: UserDefaults = .standard) {
        self.engine = engine ?? PartyVoiceEngine()
        self.chat = chat; self.defaults = defaults
        prefs = (defaults.data(forKey: "voice.prefs").flatMap { try? JSONDecoder().decode(VoiceSettings.self, from: $0) }) ?? VoiceSettings()
        // (a fresh launch never has a live microphone: the engine only starts from `turnOn()`, whatever mode was saved)
        self.engine.onPacket = { [weak self] data in
            self?.chat.channel.send("partyVoice", payload: data.base64EncodedString(), reliable: false)
        }
    }

    // MARK: what the screens read

    var isLive: Bool { engine.state == .live }
    var isTransmitting: Bool { engine.isTransmitting }
    var micLevel: Float { engine.micLevel }
    var speakingIDs: Set<String> { engine.speaking }
    var speakingNames: [String] { engine.speaking.map { chat.name(of: $0) }.sorted() }
    var routeName: String { engine.outputRoute }
    var statusTitle: String {
        switch engine.state {
        case .off: "Voice is off"
        case .starting: "Starting voice…"
        case .live: selfMuted ? "You are muted" : prefs.mode == .pushToTalk ? (engine.isTransmitting ? "You are talking" : "Hold the mic to talk") : (engine.isTransmitting ? "You are talking" : "Mic is open")
        case .paused: "Voice paused"
        case .failed: "Voice could not start"
        }
    }
    var statusDetail: String {
        switch engine.state {
        case .off: chat.safety.access.canTalk ? "Tap the mic to talk to your party. Only they can hear you." : (chat.safety.access.reason ?? "")
        case .starting: "Setting up your microphone"
        case .live: "Only people in your party can hear you."
        case .paused(let why): why == .background ? "Comes back when you return to the app." : "Comes back after the interruption."
        case .failed(let why): why
        }
    }

    // MARK: on / off

    /// Turn voice on (the first tap on the mic). False when it cannot be: not in a party, not allowed yet, or the mic was refused.
    @discardableResult func turnOn() async -> Bool {
        guard chat.channel.inParty else { notice = "Voice chat is for your party. Invite a friend first."; return false }
        chat.safety.refresh()
        guard chat.safety.access.canTalk else { notice = chat.safety.access.reason ?? "Voice is not available."; return false }
        if prefs.mode == .off { prefs.mode = .pushToTalk; save() }
        engine.canTransmit = true; selfMuted = false
        hook()
        await engine.start(prefs.clamped)
        syncPeers(); startGate()
        if case .failed(let why) = engine.state { notice = why; return false }
        if engine.permission == .denied { notice = "Allow the microphone in Settings › Motion Club to talk."; turnOff(); return false }
        return isLive || engine.state == .starting
    }

    func turnOff() {
        gate?.cancel(); gate = nil
        engine.stop(); engine.captureSink = nil; chat.voiceHandler = nil
        selfMuted = false
    }

    /// The mic follows what the player is allowed to do: leaving the party, a parent changing Game Center limits, or a new block ends it.
    private func startGate() {
        gate?.cancel()
        gate = Task { @MainActor [weak self] in
            while !Task.isCancelled {
                try? await Task.sleep(for: .seconds(1))
                guard let self else { return }
                self.chat.safety.refresh()
                if !self.chat.channel.inParty || !self.chat.safety.access.canTalk {
                    if !self.chat.channel.inParty { self.notice = "Voice ended with your party." } else { self.notice = self.chat.safety.access.reason ?? "" }
                    self.turnOff(); return
                }
                self.syncPeers()
            }
        }
    }

    private func hook() {
        chat.voiceHandler = { [weak self] data, sender in
            guard let self, self.engine.state == .live, self.chat.channel.members.contains(where: { $0.id == sender }) else { return }
            self.engine.receive(data, from: sender)
        }
    }

    /// Voice goes into a recording only while one is running (the recording service says when).
    func recordingChanged(_ on: Bool) {
        guard on else { engine.captureSink = nil; return }
        let sink: @Sendable (UnsafePointer<Float>, Int, Int, Int) -> Void = { samples, frames, rate, source in
            LiveCaptureRuntime.pushVoice(samples, frames: frames, rate: rate, source: source)
        }
        engine.captureSink = sink
    }

    // MARK: controls

    func setMode(_ mode: VoiceMode) {
        prefs.mode = mode; save()
        if mode == .off { turnOff(); return }
        if isLive { engine.update(prefs.clamped) } else { Task { await turnOn() } }
    }
    func pushToTalk(_ down: Bool) { if !selfMuted { engine.setPushToTalk(down) } }
    func toggleMute() { selfMuted.toggle(); engine.setMuted(selfMuted) }
    func setInputGain(_ v: Float) { prefs.inputGain = v; save(); if isLive { engine.update(prefs.clamped) } }
    func setOutputVolume(_ v: Float) { prefs.outputVolume = v; save(); if isLive { engine.update(prefs.clamped) } }
    func setNoiseSuppression(_ on: Bool) { prefs.noiseSuppression = on; save(); if isLive { engine.update(prefs.clamped) } }
    func setVolume(_ v: Float, for id: String) { prefs.perPeerVolume[id] = v; save(); engine.setPeer(id, volume: v) }

    /// Mute / volume of each person in the party, from the chat's mute and block lists.
    func syncPeers() {
        let ids = Set(chat.channel.members.map(\.id))
        for id in ids { engine.setPeer(id, muted: chat.isMuted(id)); engine.setPeer(id, volume: prefs.perPeerVolume[id] ?? 1) }
        for gone in chat.speakersKnown.subtracting(ids) { engine.removePeer(gone) }
        chat.speakersKnown = ids
    }

    private func save() { if let d = try? JSONEncoder().encode(prefs) { defaults.set(d, forKey: "voice.prefs") } }
    func clearNotice() { notice = "" }
}
