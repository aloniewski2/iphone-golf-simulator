import AVFoundation
import Combine

/// Original procedural sounds. No downloads, microphone, or third-party assets.
///
/// Every CoreAudio call runs on a private serial queue. Starting an audio queue can block for
/// seconds when the output device stalls (observed on the iOS simulator), and that must never
/// freeze a swing gesture on the main thread. Tension updates are coalesced so a slow device
/// only ever has one pending update, not a backlog of every drag sample.
@MainActor
final class RangeAudio: ObservableObject {
    #if DEBUG
    private(set) static var initializationCount = 0
    #endif
    var enabled = true { didSet { if !enabled { stop() } } }
    private let queue = DispatchQueue(label: "com.aloniewski.GolfArcade.rangeAudio", qos: .userInteractive)
    private let engine = Engine()

    /// What the ball met when it came down.
    enum Landing: Equatable, Sendable {
        case turf(CourseLie, speed: Double)
        case sand
        case water
        case cup
    }

    init() {
        #if DEBUG
        Self.initializationCount += 1
        #endif
        queue.async { [engine] in engine.prepare() }
    }

    func tension(_ amount: Double) {
        guard enabled, engine.queueTension(amount) else { return }
        queue.async { [engine] in engine.applyTension() }
    }

    /// The club meeting the ball. Louder with power; a mishit sounds like one.
    func impact(club: GolfClub, strike: StrikeQuality = .center, power: Double = 1) {
        let enabled = enabled
        queue.async { [engine] in
            engine.stop()
            if enabled { engine.impact(club: club, strike: strike, power: power) }
        }
    }

    /// The club head passing through the air on the way down. Scaled by how hard the swing is.
    func whoosh(power: Double) {
        guard enabled else { return }
        queue.async { [engine] in engine.whoosh(power: power) }
    }

    func landing(_ landing: Landing) {
        guard enabled else { return }
        queue.async { [engine] in engine.landing(landing) }
    }

    func celebrate() {
        guard enabled else { return }
        queue.async { [engine] in engine.celebrate() }
    }

    func ready() {
        guard enabled else { return }
        queue.async { [engine] in engine.ready() }
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
        private var hits: [String: AVAudioPlayer] = [:]
        private var whooshes: [AVAudioPlayer] = []
        private var landings: [String: AVAudioPlayer] = [:]
        private var reward: AVAudioPlayer?
        private var bump: AVAudioPlayer?
        private var sigh: AVAudioPlayer?
        private var readyTone: AVAudioPlayer?
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
            readyTone = try? AVAudioPlayer(data: RangeAudio.wave(duration: 0.18, frequency: 660, percussive: true))
            for club in GolfClub.allCases {
                for strike in [StrikeQuality.center, .thin, .fat, .heel] {
                    let player = try? AVAudioPlayer(data: ImpactSound.strike(club: club, strike: strike).render())
                    player?.prepareToPlay()
                    hits[Self.key(club, strike)] = player
                }
            }
            whooshes = [0.35, 0.7, 1.0].compactMap { try? AVAudioPlayer(data: ImpactSound.whoosh(power: $0).render()) }
            for (name, sound) in [("turf", ImpactSound.turfLanding), ("green", ImpactSound.greenLanding),
                                  ("sand", ImpactSound.sandLanding), ("water", ImpactSound.splash), ("cup", ImpactSound.cupDrop)] {
                let player = try? AVAudioPlayer(data: sound.render())
                player?.prepareToPlay()
                landings[name] = player
            }
            whooshes.forEach { $0.prepareToPlay() }
            readyTone?.prepareToPlay()
            bump?.prepareToPlay()
            sigh?.prepareToPlay()
            load?.prepareToPlay()
            reward?.prepareToPlay()
        }

        private static func key(_ club: GolfClub, _ strike: StrikeQuality) -> String {
            let family: StrikeQuality = strike == .toe ? .heel : strike
            return "\(club.rawValue)-\(family.rawValue)"
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

        func impact(club: GolfClub, strike: StrikeQuality, power: Double) {
            guard strike != .miss, let player = hits[Self.key(club, strike)] else { return }
            player.currentTime = 0
            let loudness = 0.35 + 0.65 * min(max(power, 0), 1)
            player.volume = Float((club == .putter ? 0.5 : 0.85) * loudness)
            player.play()
        }

        func whoosh(power: Double) {
            guard !whooshes.isEmpty else { return }
            let level = min(max(power, 0), 1)
            let player = whooshes[min(whooshes.count - 1, Int(level * Double(whooshes.count)))]
            player.currentTime = 0
            player.volume = Float(0.15 + 0.45 * level)
            player.play()
        }

        func landing(_ landing: Landing) {
            let name: String
            var volume = 0.6
            switch landing {
            case .turf(let lie, let speed):
                name = lie == .green ? "green" : "turf"
                volume = 0.25 + 0.5 * min(1, speed / 30)
            case .sand: name = "sand"
            case .water: name = "water"; volume = 0.7
            case .cup: name = "cup"; volume = 0.75
            }
            guard let player = landings[name] else { return }
            player.currentTime = 0
            player.volume = Float(volume)
            player.play()
        }

        func celebrate() {
            reward?.currentTime = 0
            reward?.volume = 0.3
            reward?.play()
        }

        func ready() {
            readyTone?.currentTime = 0
            readyTone?.volume = 0.4
            readyTone?.play()
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
            reward?.stop()
            readyTone?.stop()
        }
    }

    private nonisolated static func wave(duration: Double, frequency: Double, percussive: Bool, noise: Double = 0) -> Data {
        let rate = 22_050.0
        let count = Int(duration * rate)
        var samples = [Double](repeating: 0, count: count)
        var seed: UInt32 = 42
        for index in 0..<count {
            let t = Double(index) / rate
            seed = 1664525 &* seed &+ 1013904223
            let random = Double(seed) / Double(UInt32.max) * 2 - 1
            let envelope = percussive ? exp(-t * 24) * min(1, t * 1000) : min(1, t * 100) * min(1, (duration - t) * 100)
            samples[index] = (sin(2 * .pi * frequency * t) * (1 - noise) + random * noise) * envelope * 0.65
        }
        return ImpactSound.wav(samples, rate: rate)
    }
}

/// A small additive synth: decaying partials for the ring and body of a strike, shaped noise
/// for the click, the turf and the air. Rendered once at start-up into short WAV buffers.
struct ImpactSound: Sendable {
    struct Partial: Sendable {
        var frequency: Double
        var amplitude: Double
        /// Seconds for the partial to fall to 1/e.
        var decay: Double
        /// Hz the pitch falls over the first `decay` seconds: the sag of a struck body.
        var drop: Double = 0
        var delay: Double = 0
    }

    struct Noise: Sendable {
        var amplitude: Double
        var attack: Double
        var decay: Double
        /// 0 = very dull, 1 = white. A one-pole low-pass on the noise.
        var brightness: Double
        var delay: Double = 0
    }

    var partials: [Partial]
    var noises: [Noise]
    var duration: Double
    var gain = 1.0

    static let rate = 22_050.0

    // MARK: - Strikes

    static func strike(club: GolfClub, strike: StrikeQuality) -> ImpactSound {
        var sound: ImpactSound
        switch club {
        case .driver, .wood3:
            // A titanium face: a bright metallic ping over a deep, short thump.
            sound = ImpactSound(partials: [
                Partial(frequency: 2350, amplitude: 0.42, decay: 0.055),
                Partial(frequency: 3120, amplitude: 0.30, decay: 0.042),
                Partial(frequency: 4260, amplitude: 0.18, decay: 0.030),
                Partial(frequency: 165, amplitude: 0.55, decay: 0.032, drop: 70)
            ], noises: [Noise(amplitude: 0.7, attack: 0.0004, decay: 0.006, brightness: 0.95)], duration: 0.32)
        case .iron5, .iron, .iron9:
            // Forged iron: a crisp compressed thwack with a little turf.
            sound = ImpactSound(partials: [
                Partial(frequency: 1320, amplitude: 0.36, decay: 0.030),
                Partial(frequency: 1940, amplitude: 0.22, decay: 0.022),
                Partial(frequency: 230, amplitude: 0.58, decay: 0.036, drop: 90)
            ], noises: [Noise(amplitude: 0.9, attack: 0.0004, decay: 0.012, brightness: 0.65),
                        Noise(amplitude: 0.35, attack: 0.004, decay: 0.05, brightness: 0.2, delay: 0.004)], duration: 0.3)
        case .wedge:
            // Lofted wedge: softer, duller, more grass.
            sound = ImpactSound(partials: [
                Partial(frequency: 920, amplitude: 0.28, decay: 0.026),
                Partial(frequency: 270, amplitude: 0.5, decay: 0.04, drop: 110)
            ], noises: [Noise(amplitude: 0.8, attack: 0.0005, decay: 0.02, brightness: 0.4),
                        Noise(amplitude: 0.45, attack: 0.006, decay: 0.07, brightness: 0.15, delay: 0.003)], duration: 0.3)
        case .putter:
            // A milled insert: a quiet wooden tock.
            sound = ImpactSound(partials: [
                Partial(frequency: 1480, amplitude: 0.32, decay: 0.020),
                Partial(frequency: 2260, amplitude: 0.18, decay: 0.014),
                Partial(frequency: 430, amplitude: 0.34, decay: 0.022)
            ], noises: [Noise(amplitude: 0.4, attack: 0.0003, decay: 0.004, brightness: 0.8)], duration: 0.2)
        }
        switch strike {
        case .center, .miss:
            break
        case .thin:
            // Off the leading edge: all click and ring, no body.
            sound.partials = sound.partials.map { p in
                p.frequency < 600 ? Partial(frequency: p.frequency, amplitude: p.amplitude * 0.25, decay: p.decay * 0.6)
                    : Partial(frequency: p.frequency * 1.35, amplitude: p.amplitude * 0.9, decay: p.decay * 0.8)
            }
            sound.noises = sound.noises.map { n in Noise(amplitude: n.amplitude, attack: n.attack, decay: n.decay * 0.7, brightness: min(1, n.brightness + 0.2), delay: n.delay) }
        case .fat:
            // Ground first: a dull dig, the ring smothered by turf.
            sound.partials = sound.partials.map { p in
                p.frequency < 600 ? Partial(frequency: p.frequency * 0.8, amplitude: p.amplitude * 1.3, decay: p.decay * 1.4, drop: p.drop)
                    : Partial(frequency: p.frequency, amplitude: p.amplitude * 0.3, decay: p.decay * 0.5)
            }
            sound.noises = [Noise(amplitude: 0.85, attack: 0.002, decay: 0.09, brightness: 0.12),
                            Noise(amplitude: 0.3, attack: 0.0005, decay: 0.01, brightness: 0.5)]
            sound.duration = 0.36
        case .heel, .toe:
            // Off the sweet spot: the face rings less and lower.
            sound.partials = sound.partials.map { p in
                p.frequency < 600 ? p : Partial(frequency: p.frequency * 0.88, amplitude: p.amplitude * 0.55, decay: p.decay * 0.55)
            }
        }
        return sound
    }

    static func whoosh(power: Double) -> ImpactSound {
        let level = min(max(power, 0), 1)
        return ImpactSound(partials: [], noises: [
            Noise(amplitude: 0.5 + 0.4 * level, attack: 0.05 - 0.02 * level, decay: 0.09 + 0.05 * level, brightness: 0.18 + 0.3 * level)
        ], duration: 0.32)
    }

    // MARK: - Landings

    static let turfLanding = ImpactSound(partials: [Partial(frequency: 95, amplitude: 0.7, decay: 0.05, drop: 35)],
        noises: [Noise(amplitude: 0.5, attack: 0.001, decay: 0.035, brightness: 0.2)], duration: 0.25)
    static let greenLanding = ImpactSound(partials: [Partial(frequency: 120, amplitude: 0.45, decay: 0.035, drop: 30)],
        noises: [Noise(amplitude: 0.3, attack: 0.001, decay: 0.02, brightness: 0.25)], duration: 0.2)
    static let sandLanding = ImpactSound(partials: [Partial(frequency: 80, amplitude: 0.35, decay: 0.05)],
        noises: [Noise(amplitude: 0.85, attack: 0.004, decay: 0.12, brightness: 0.3)], duration: 0.35)
    static let splash = ImpactSound(partials: [Partial(frequency: 190, amplitude: 0.45, decay: 0.12, drop: 95)],
        noises: [Noise(amplitude: 0.9, attack: 0.008, decay: 0.2, brightness: 0.5),
                 Noise(amplitude: 0.4, attack: 0.05, decay: 0.3, brightness: 0.35, delay: 0.08)], duration: 0.6)
    static let cupDrop = ImpactSound(partials: [
        Partial(frequency: 2650, amplitude: 0.28, decay: 0.03), Partial(frequency: 3420, amplitude: 0.18, decay: 0.025),
        Partial(frequency: 2650, amplitude: 0.2, decay: 0.028, delay: 0.07), Partial(frequency: 3420, amplitude: 0.12, decay: 0.022, delay: 0.07),
        Partial(frequency: 2650, amplitude: 0.13, decay: 0.025, delay: 0.13),
        Partial(frequency: 410, amplitude: 0.45, decay: 0.07, drop: 160, delay: 0.16)
    ], noises: [Noise(amplitude: 0.35, attack: 0.001, decay: 0.01, brightness: 0.7),
                Noise(amplitude: 0.25, attack: 0.001, decay: 0.012, brightness: 0.6, delay: 0.07)], duration: 0.45)

    // MARK: - Rendering

    func render() -> Data {
        let rate = Self.rate
        let count = Int(duration * rate)
        var samples = [Double](repeating: 0, count: count)
        for partial in partials {
            let start = Int(partial.delay * rate)
            var phase = 0.0
            for index in max(0, start)..<count {
                let t = Double(index - start) / rate
                let envelope = exp(-t / max(partial.decay, 0.001)) * min(1, t * 2500)
                let frequency = partial.frequency - partial.drop * min(1, t / max(partial.decay, 0.001))
                phase += 2 * .pi * frequency / rate
                samples[index] += sin(phase) * partial.amplitude * envelope
            }
        }
        var seed: UInt32 = 0x9E37
        for noise in noises {
            let start = Int(noise.delay * rate)
            let alpha = 0.02 + pow(noise.brightness, 2) * 0.98
            var filtered = 0.0
            for index in max(0, start)..<count {
                let t = Double(index - start) / rate
                seed = 1664525 &* seed &+ 1013904223
                let white = Double(seed) / Double(UInt32.max) * 2 - 1
                filtered += (white - filtered) * alpha
                let envelope = min(1, t / max(noise.attack, 0.0001)) * exp(-max(0, t - noise.attack) / max(noise.decay, 0.001))
                samples[index] += filtered * noise.amplitude * envelope
            }
        }
        // Soft limiter: struck things are loud but never clipped into buzz.
        let shaped = samples.map { tanh($0 * gain * 1.4) * 0.92 }
        return Self.wav(shaped, rate: rate)
    }

    /// 16-bit mono PCM in a RIFF container.
    static func wav(_ samples: [Double], rate: Double) -> Data {
        var data = Data(capacity: 44 + samples.count * 2)
        func string(_ value: String) { data.append(contentsOf: value.utf8) }
        func u16(_ value: UInt16) { var v = value.littleEndian; withUnsafeBytes(of: &v) { data.append(contentsOf: $0) } }
        func u32(_ value: UInt32) { var v = value.littleEndian; withUnsafeBytes(of: &v) { data.append(contentsOf: $0) } }
        string("RIFF"); u32(UInt32(36 + samples.count * 2)); string("WAVEfmt "); u32(16)
        u16(1); u16(1); u32(UInt32(rate)); u32(UInt32(rate * 2)); u16(2); u16(16)
        string("data"); u32(UInt32(samples.count * 2))
        for sample in samples {
            u16(UInt16(bitPattern: Int16(max(-1, min(1, sample)) * 32767)))
        }
        return data
    }
}
