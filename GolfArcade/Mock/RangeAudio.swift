import AVFoundation

/// Original procedural sounds. No downloads, microphone, or third-party assets.
///
/// Every CoreAudio call runs on a private serial queue. Starting an audio queue can block for
/// seconds when the output device stalls (observed on the iOS simulator), and that must never
/// freeze a swing gesture on the main thread. Tension updates are coalesced so a slow device
/// only ever has one pending update, not a backlog of every drag sample.
@MainActor
final class RangeAudio {
    var enabled = true { didSet { if !enabled { stop() } } }
    private let queue = DispatchQueue(label: "com.aloniewski.GolfArcade.rangeAudio", qos: .userInteractive)
    private let engine = Engine()

    init() {
        queue.async { [engine] in engine.prepare() }
    }

    func tension(_ amount: Double) {
        guard enabled, engine.queueTension(amount) else { return }
        queue.async { [engine] in engine.applyTension() }
    }

    func impact(club: GolfClub) {
        let frequency = club == .driver ? 180.0 : club == .putter ? 700 : 380
        let enabled = enabled
        queue.async { [engine] in
            engine.stop()
            if enabled { engine.impact(frequency: frequency) }
        }
    }

    func celebrate() {
        guard enabled else { return }
        queue.async { [engine] in engine.celebrate() }
    }

    /// A club connecting with a friend.
    func thump() {
        guard enabled else { return }
        queue.async { [engine] in engine.thump() }
    }

    /// A falling tone for a bad result.
    func groan() {
        guard enabled else { return }
        queue.async { [engine] in engine.groan() }
    }

    func stop() {
        queue.async { [engine] in engine.stop() }
    }

    /// Owns the players. Everything except `latestTension` is touched only on `RangeAudio.queue`.
    private final class Engine: @unchecked Sendable {
        private var load: AVAudioPlayer?
        private var hit: AVAudioPlayer?
        private var reward: AVAudioPlayer?
        private var bump: AVAudioPlayer?
        private var sigh: AVAudioPlayer?
        private let lock = NSLock()
        private var latestTension: Double?

        func prepare() {
            try? AVAudioSession.sharedInstance().setCategory(.ambient, mode: .default)
            load = try? AVAudioPlayer(data: RangeAudio.wave(duration: 0.5, frequency: 150, percussive: false))
            load?.numberOfLoops = -1
            load?.enableRate = true
            reward = try? AVAudioPlayer(data: RangeAudio.wave(duration: 0.35, frequency: 880, percussive: true))
            bump = try? AVAudioPlayer(data: RangeAudio.wave(duration: 0.25, frequency: 85, percussive: true, noise: 0.6))
            sigh = try? AVAudioPlayer(data: RangeAudio.wave(duration: 0.45, frequency: 140, percussive: false, noise: 0.1))
            sigh?.enableRate = true
            sigh?.rate = 0.7
            bump?.prepareToPlay()
            sigh?.prepareToPlay()
            load?.prepareToPlay()
            reward?.prepareToPlay()
        }

        /// Records the newest value. Returns `false` when an update is already waiting, so the
        /// caller does not enqueue a second block for it.
        func queueTension(_ amount: Double) -> Bool {
            lock.lock()
            defer { lock.unlock() }
            let wasPending = latestTension != nil
            latestTension = amount
            return !wasPending
        }

        func applyTension() {
            lock.lock()
            let amount = latestTension
            latestTension = nil
            lock.unlock()
            guard let amount else { return }
            load?.rate = Float(0.7 + amount * 1.2)
            load?.volume = Float(0.06 + amount * 0.16)
            if load?.isPlaying == false { load?.play() }
        }

        func impact(frequency: Double) {
            hit = try? AVAudioPlayer(data: RangeAudio.wave(duration: 0.18, frequency: frequency, percussive: true, noise: 0.45))
            hit?.volume = 0.5
            hit?.play()
        }

        func celebrate() {
            reward?.currentTime = 0
            reward?.volume = 0.3
            reward?.play()
        }

        func thump() {
            bump?.currentTime = 0
            bump?.volume = 0.6
            bump?.play()
        }

        func groan() {
            sigh?.currentTime = 0
            sigh?.volume = 0.25
            sigh?.play()
        }

        func stop() {
            lock.lock()
            latestTension = nil
            lock.unlock()
            load?.stop()
            hit?.stop()
            reward?.stop()
        }
    }

    private nonisolated static func wave(duration: Double, frequency: Double, percussive: Bool, noise: Double = 0) -> Data {
        let rate = 22_050.0
        let count = Int(duration * rate)
        var data = Data()
        func string(_ value: String) { data.append(contentsOf: value.utf8) }
        func u16(_ value: UInt16) { var v = value.littleEndian; withUnsafeBytes(of: &v) { data.append(contentsOf: $0) } }
        func u32(_ value: UInt32) { var v = value.littleEndian; withUnsafeBytes(of: &v) { data.append(contentsOf: $0) } }
        string("RIFF"); u32(UInt32(36 + count * 2)); string("WAVEfmt "); u32(16)
        u16(1); u16(1); u32(UInt32(rate)); u32(UInt32(rate * 2)); u16(2); u16(16)
        string("data"); u32(UInt32(count * 2))
        var seed: UInt32 = 42
        for index in 0..<count {
            let t = Double(index) / rate
            seed = 1664525 &* seed &+ 1013904223
            let random = Double(seed) / Double(UInt32.max) * 2 - 1
            let envelope = percussive ? exp(-t * 24) * min(1, t * 1000) : min(1, t * 100) * min(1, (duration - t) * 100)
            let signal = (sin(2 * .pi * frequency * t) * (1 - noise) + random * noise) * envelope * 0.65
            u16(UInt16(bitPattern: Int16(signal * 32767)))
        }
        return data
    }
}
