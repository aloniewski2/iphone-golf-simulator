import AVFoundation

// Party voice, the pure half: codecs, wire packets, jitter buffer, voice-activity detector.
// No UIKit, no AVAudioSession, no engine: this file compiles and is tested on macOS as well as iOS. The hardware half is PartyVoiceEngine.swift.
//
// Everything here is single-threaded by contract (the engine confines each object to one queue / holds a lock around it), so nothing locks.

/// The wire and audio constants every peer must agree on.
enum VoiceFormat {
    static let sampleRate = 16_000
    static let frameMs = 20
    /// One 20 ms frame at 16 kHz. Codecs with a different native frame (AAC-ELD 480 = 30 ms, iLBC 480 = 30 ms, mu-law 160 = 10 ms) buffer or split.
    static let frameSamples = 320
    static let version: UInt8 = 1
    static let headerBytes = 9
    /// Payload cap per packet for ANY codec: keeps the base64 text inside one small GameKit data message.
    static let maxPayloadBytes = 200
    static let maxPacketBytes = headerBytes + maxPayloadBytes
    /// Upper bound of samples one packet may decode to (AAC-ELD / iLBC 480); anything bigger is rejected as garbage.
    static let maxPacketSamples = 512
}

/// Codec ids on the wire. Never renumber: a peer on an older build decodes by this byte.
enum VoiceCodecID: UInt8, CaseIterable, Sendable {
    case aacELD = 1
    case iLBC = 2
    /// Last resort, needs nothing from the OS: 16 kHz mono, 8-bit mu-law (about 14-bit dynamic range), 10 ms per packet.
    case pcm16 = 3
}

// MARK: - Packet

/// `[0] version=1 [1] codec [2..3] sequence [4..7] timestampMs since stream start [8] flags (bit0 speech-end) [9...] payload`, little endian.
struct VoicePacket: Equatable, Sendable {
    static let speechEndFlag: UInt8 = 1
    var codec: VoiceCodecID
    var sequence: UInt16
    var timestampMs: UInt32
    var flags: UInt8 = 0
    var payload: Data
    var isSpeechEnd: Bool { flags & VoicePacket.speechEndFlag != 0 }

    func serialized() -> Data {
        var d = Data(capacity: VoiceFormat.headerBytes + payload.count)
        d.append(VoiceFormat.version); d.append(codec.rawValue)
        d.append(UInt8(sequence & 0xff)); d.append(UInt8(sequence >> 8))
        for shift in stride(from: 0, to: 32, by: 8) { d.append(UInt8((timestampMs >> UInt32(shift)) & 0xff)) }
        d.append(flags); d.append(payload)
        return d
    }

    /// nil for anything malformed: wrong version, too short, unknown codec, oversize, reserved flag bits, empty payload without the end marker.
    /// Total (never traps) on any bytes, including a Data slice that does not start at index 0.
    static func parse(_ data: Data) -> VoicePacket? {
        guard data.count >= VoiceFormat.headerBytes, data.count <= VoiceFormat.maxPacketBytes else { return nil }
        let b = [UInt8](data.prefix(VoiceFormat.headerBytes))
        guard b[0] == VoiceFormat.version, let codec = VoiceCodecID(rawValue: b[1]) else { return nil }
        let flags = b[8]
        guard flags & ~speechEndFlag == 0 else { return nil }
        let payload = Data(data.dropFirst(VoiceFormat.headerBytes))
        guard !payload.isEmpty || flags & speechEndFlag != 0 else { return nil }
        let seq = UInt16(b[2]) | UInt16(b[3]) << 8
        let ts = UInt32(b[4]) | UInt32(b[5]) << 8 | UInt32(b[6]) << 16 | UInt32(b[7]) << 24
        return VoicePacket(codec: codec, sequence: seq, timestampMs: ts, flags: flags, payload: payload)
    }
}

/// Inbound flood guard (a hostile or broken peer): token bucket with the clock passed in so it is deterministic in tests.
struct VoiceRateLimiter {
    var ratePerSecond: Double
    var burst: Double
    private var tokens: Double
    private var lastMs: Double?
    init(ratePerSecond: Double = 160, burst: Double = 80) { self.ratePerSecond = ratePerSecond; self.burst = burst; tokens = burst }
    mutating func allow(nowMs: Double) -> Bool {
        if let lastMs { tokens = min(burst, tokens + max(0, nowMs - lastMs) * ratePerSecond / 1000) }
        lastMs = nowMs
        guard tokens >= 1 else { return false }
        tokens -= 1; return true
    }
}

// MARK: - Codec protocol

/// One direction-agnostic codec instance. Stateful (encoder history, decoder history): one instance per stream, never shared across threads.
protocol VoiceCodec: AnyObject, Sendable {
    var id: VoiceCodecID { get }
    /// Input samples (16 kHz) one `encode` consumes.
    var frameSamples: Int { get }
    /// nil if this frame failed (or exceeded the payload cap): the sender then simply skips it and the receiver conceals.
    func encode(_ frame: [Float]) -> Data?
    /// nil for a payload this codec cannot decode (garbage). Samples are 16 kHz, finite, clamped to -1...1.
    func decode(_ payload: Data) -> [Float]?
}

/// The two-direction half-band resampler iLBC needs (it is 8 kHz only; the party stream is 16 kHz). 31-tap Hamming sinc, cutoff 3.4 kHz.
struct VoiceHalfBand {
    private static let taps: [Float] = {
        let n = 31, fc = 3400.0 / 16000.0
        var h = (0..<n).map { i -> Double in
            let m = Double(i - n / 2)
            let sinc = m == 0 ? 2 * fc : sin(2 * Double.pi * fc * m) / (Double.pi * m)
            return sinc * (0.54 - 0.46 * cos(2 * Double.pi * Double(i) / Double(n - 1)))
        }
        let sum = h.reduce(0, +); h = h.map { $0 / sum }
        return h.map { Float($0) }
    }()
    private var history = [Float](repeating: 0, count: 30)
    /// 16 kHz in -> 8 kHz out (half the samples).
    mutating func decimate(_ x: [Float]) -> [Float] {
        let h = VoiceHalfBand.taps, buf = history + x
        var out = [Float](); out.reserveCapacity(x.count / 2)
        var n = 0
        while n < x.count {
            var acc: Float = 0
            for k in 0..<h.count { acc += h[k] * buf[n + 30 - k] }
            out.append(acc); n += 2
        }
        history = Array(buf.suffix(30)); return out
    }
    /// 8 kHz in -> 16 kHz out (double the samples, zero-stuffed then low-passed, gain 2).
    mutating func interpolate(_ x: [Float]) -> [Float] {
        let h = VoiceHalfBand.taps
        var stuffed = [Float](repeating: 0, count: x.count * 2)
        for i in 0..<x.count { stuffed[2 * i] = x[i] }
        let buf = history + stuffed
        var out = [Float](repeating: 0, count: stuffed.count)
        for n in 0..<stuffed.count {
            var acc: Float = 0
            for k in 0..<h.count { acc += h[k] * buf[n + 30 - k] }
            out[n] = acc * 2
        }
        history = Array(buf.suffix(30)); return out
    }
}

/// Block-based AVAudioConverter input needs a mutable "already fed" flag; the block may be @Sendable so it lives in a box.
private final class ConvertFeed: @unchecked Sendable {
    var buffer: AVAudioBuffer?
    var fed = false
    func next(_ status: UnsafeMutablePointer<AVAudioConverterInputStatus>) -> AVAudioBuffer? {
        if fed || buffer == nil { status.pointee = .noDataNow; return nil }
        fed = true; status.pointee = .haveData; return buffer
    }
}

/// AAC-ELD and iLBC through Core Audio's AVAudioConverter. Failable: a codec the OS lacks (or whose converter refuses) yields nil.
final class AVConverterCodec: VoiceCodec, @unchecked Sendable {
    let id: VoiceCodecID
    let frameSamples: Int
    private let codecFrames: Int
    private let encoder: AVAudioConverter
    private let decoder: AVAudioConverter
    private let pcmFormat: AVAudioFormat
    private let compFormat: AVAudioFormat
    private let pcmIn: AVAudioPCMBuffer
    private let pcmOut: AVAudioPCMBuffer
    private let compOut: AVAudioCompressedBuffer
    private let compIn: AVAudioCompressedBuffer
    private var resampler: VoiceHalfBand?
    private var resamplerUp: VoiceHalfBand?

    init?(_ id: VoiceCodecID) {
        let formatID: AudioFormatID, rate: Double, framesPerPacket: UInt32, bitRate: Int?
        switch id {
        // ELD works in 480-sample (30 ms) frames at 16 kHz. 24 kbps is the lowest rate that still sounds clean on speech.
        case .aacELD: formatID = kAudioFormatMPEG4AAC_ELD; rate = 16_000; framesPerPacket = 480; bitRate = 24_000
        // iLBC exists only at 8 kHz, 30 ms = 240 samples = 50 bytes (13.3 kbps); we resample around it.
        case .iLBC: formatID = kAudioFormatiLBC; rate = 8_000; framesPerPacket = 240; bitRate = nil
        case .pcm16: return nil
        }
        var asbd = AudioStreamBasicDescription(mSampleRate: rate, mFormatID: formatID, mFormatFlags: 0, mBytesPerPacket: 0, mFramesPerPacket: framesPerPacket,
                                               mBytesPerFrame: 0, mChannelsPerFrame: 1, mBitsPerChannel: 0, mReserved: 0)
        guard let comp = AVAudioFormat(streamDescription: &asbd),
              let pcm = AVAudioFormat(commonFormat: .pcmFormatFloat32, sampleRate: rate, channels: 1, interleaved: false),
              let enc = AVAudioConverter(from: pcm, to: comp), let dec = AVAudioConverter(from: comp, to: pcm) else { return nil }
        if let bitRate { enc.bitRate = bitRate }
        // The decoder needs the encoder's config (AudioSpecificConfig for ELD). Both ends derive it from the same fixed settings, so it is never sent.
        if let cookie = enc.magicCookie { dec.magicCookie = cookie }
        let maxPacket = max(enc.maximumOutputPacketSize, 64)
        guard let pcmIn = AVAudioPCMBuffer(pcmFormat: pcm, frameCapacity: AVAudioFrameCount(framesPerPacket)),
              let pcmOut = AVAudioPCMBuffer(pcmFormat: pcm, frameCapacity: AVAudioFrameCount(framesPerPacket * 4)) else { return nil }
        self.id = id; self.encoder = enc; self.decoder = dec; self.pcmFormat = pcm; self.compFormat = comp
        self.pcmIn = pcmIn; self.pcmOut = pcmOut
        self.compOut = AVAudioCompressedBuffer(format: comp, packetCapacity: 2, maximumPacketSize: maxPacket)
        self.compIn = AVAudioCompressedBuffer(format: comp, packetCapacity: 1, maximumPacketSize: maxPacket)
        self.codecFrames = Int(framesPerPacket)
        if rate == 8_000 { resampler = VoiceHalfBand(); resamplerUp = VoiceHalfBand(); frameSamples = codecFrames * 2 } else { frameSamples = codecFrames }
    }

    func encode(_ frame: [Float]) -> Data? {
        guard frame.count == frameSamples else { return nil }
        let x = resampler != nil ? resampler!.decimate(frame) : frame
        guard x.count == codecFrames, let dst = pcmIn.floatChannelData?[0] else { return nil }
        for i in 0..<codecFrames { dst[i] = max(-1, min(1, x[i])) }
        pcmIn.frameLength = AVAudioFrameCount(codecFrames)
        compOut.packetCount = 0; compOut.byteLength = 0
        let feed = ConvertFeed(); feed.buffer = pcmIn
        var error: NSError?
        let status = encoder.convert(to: compOut, error: &error) { _, s in feed.next(s) }
        guard status != .error, error == nil, compOut.packetCount > 0, compOut.byteLength > 0,
              Int(compOut.byteLength) <= VoiceFormat.maxPayloadBytes else { return nil }
        return Data(bytes: compOut.data, count: Int(compOut.byteLength))
    }

    func decode(_ payload: Data) -> [Float]? {
        guard !payload.isEmpty, payload.count <= VoiceFormat.maxPayloadBytes, payload.count <= compIn.byteCapacity else { return nil }
        payload.withUnsafeBytes { raw in if let base = raw.baseAddress { memcpy(compIn.data, base, payload.count) } }
        compIn.byteLength = UInt32(payload.count); compIn.packetCount = 1
        compIn.packetDescriptions?[0] = AudioStreamPacketDescription(mStartOffset: 0, mVariableFramesInPacket: 0, mDataByteSize: UInt32(payload.count))
        pcmOut.frameLength = 0
        let feed = ConvertFeed(); feed.buffer = compIn
        var error: NSError?
        let status = decoder.convert(to: pcmOut, error: &error) { _, s in feed.next(s) }
        let frames = Int(pcmOut.frameLength)
        guard status != .error, error == nil, frames > 0, frames <= codecFrames * 2, let src = pcmOut.floatChannelData?[0] else { decoder.reset(); return nil }
        var out = [Float](repeating: 0, count: frames)
        for i in 0..<frames { let v = src[i]; out[i] = v.isFinite ? max(-1, min(1, v)) : 0 }
        if resamplerUp != nil { out = resamplerUp!.interpolate(out).map { max(-1, min(1, $0)) } }
        guard out.count <= VoiceFormat.maxPacketSamples else { return nil }
        return out
    }
}

/// 8-bit mu-law (G.711 companding) at 16 kHz. Always available. 160 samples (10 ms) = 160 bytes per packet, the largest that fits the 200 byte cap.
final class PCMMuLawCodec: VoiceCodec, @unchecked Sendable {
    let id = VoiceCodecID.pcm16
    let frameSamples = 160
    private static let decodeTable: [Float] = (0..<256).map { i -> Float in
        let u = ~UInt8(i)
        let sign: Float = u & 0x80 != 0 ? -1 : 1
        let exponent = Int((u >> 4) & 7), mantissa = Int(u & 0x0f)
        let magnitude = ((mantissa << 3) + 0x84) << exponent
        return sign * Float(magnitude - 0x84) / 32768
    }
    func encode(_ frame: [Float]) -> Data? {
        guard frame.count == frameSamples else { return nil }
        var out = Data(count: frame.count)
        for i in 0..<frame.count {
            let s = Int(max(-1, min(1, frame[i])) * 32767)
            let sign: UInt8 = s < 0 ? 0x80 : 0
            let mag = min(abs(s), 32635) + 0x84
            var exp = 7, mask = 0x4000
            while exp > 0, mag & mask == 0 { exp -= 1; mask >>= 1 }
            let mant = UInt8((mag >> (exp + 3)) & 0x0f)
            out[i] = ~(sign | UInt8(exp << 4) | mant)
        }
        return out
    }
    func decode(_ payload: Data) -> [Float]? {
        guard !payload.isEmpty, payload.count <= VoiceFormat.maxPayloadBytes else { return nil }
        return payload.map { PCMMuLawCodec.decodeTable[Int($0)] }
    }
}

// MARK: - Probe and factory

enum VoiceCodecFactory {
    struct Probe: Equatable {
        var id: VoiceCodecID
        var ok: Bool
        var snrDB: Double
        var meanBytes: Double
        var maxBytes: Int
        var note: String
    }
    /// Best first. The order is the product choice: ELD (low latency, best quality at ~24 kbps) > iLBC (13 kbps, 8 kHz telephone band) > mu-law.
    static let preference: [VoiceCodecID] = [.aacELD, .iLBC, .pcm16]

    static func make(_ id: VoiceCodecID) -> VoiceCodec? { id == .pcm16 ? PCMMuLawCodec() : AVConverterCodec(id) }

    /// Real encode+decode of 1.2 s of speech-like signal. We never assume an OS has a codec: a converter can construct and still fail to round-trip.
    /// `snrFloor` is per codec because iLBC is a perceptual speech coder (low waveform SNR by design).
    static func probe(_ id: VoiceCodecID, signal: [Float]? = nil) -> Probe {
        guard let enc = make(id), let dec = make(id) else { return Probe(id: id, ok: false, snrDB: -99, meanBytes: 0, maxBytes: 0, note: "converter unavailable") }
        let sig = signal ?? VoiceTestSignal.speechLike(seconds: 1.2)
        let n = enc.frameSamples
        var decoded = [Float](), sizes = [Int]()
        var i = 0
        while i + n <= sig.count {
            guard let bytes = enc.encode(Array(sig[i..<i + n])) else { return Probe(id: id, ok: false, snrDB: -99, meanBytes: 0, maxBytes: 0, note: "encode failed at frame \(i / n)") }
            sizes.append(bytes.count)
            guard let pcm = dec.decode(bytes) else { return Probe(id: id, ok: false, snrDB: -99, meanBytes: 0, maxBytes: 0, note: "decode failed at frame \(i / n)") }
            decoded += pcm; i += n
        }
        let snr = VoiceMetrics.bestLagSNR(reference: sig, test: decoded, maxLag: 640)
        let floor = snrFloor(id)
        let lengthOK = decoded.count >= Int(Double(i) * 0.9)
        return Probe(id: id, ok: lengthOK && snr >= floor, snrDB: snr, meanBytes: Double(sizes.reduce(0, +)) / Double(max(sizes.count, 1)), maxBytes: sizes.max() ?? 0,
                     note: lengthOK ? (snr >= floor ? "ok" : "SNR below \(floor) dB") : "short output \(decoded.count)/\(i)")
    }
    static func snrFloor(_ id: VoiceCodecID) -> Double { id == .aacELD ? 8 : id == .iLBC ? 4 : 20 }

    /// Probed once per process (cheap: tens of ms); thread-safe because a static let initialises exactly once.
    static let probes: [Probe] = preference.map { probe($0) }
    static let available: [VoiceCodecID] = probes.filter(\.ok).map(\.id)
    /// pcm16 always works, so there is always an answer.
    static var best: VoiceCodecID { available.first ?? .pcm16 }
}

// MARK: - Streams (framing + packets)

/// Turns a continuous 16 kHz mono PCM stream into packets. One per outgoing stream.
final class VoiceStreamEncoder {
    let codec: VoiceCodec
    private(set) var sequence: UInt16
    private(set) var oversizeOrFailedFrames = 0
    private var pending = [Float]()
    private var clockSamples: Int64 = 0

    init(codec: VoiceCodec, startSequence: UInt16 = 0) { self.codec = codec; sequence = startSequence }

    /// Sets the stream timestamp of the next sample when nothing is buffered (start of a talk spurt after silence, so the receiver sees real elapsed time and tells a pause from a loss).
    func beginSpurt(atMs: UInt32) { if pending.isEmpty { clockSamples = Int64(atMs) * Int64(VoiceFormat.sampleRate / 1000) } }

    func encode(_ pcm: [Float]) -> [VoicePacket] {
        pending += pcm
        var out = [VoicePacket]()
        let n = codec.frameSamples
        var start = 0
        while pending.count - start >= n {
            if let p = makePacket(Array(pending[start..<start + n]), flags: 0) { out.append(p) }
            start += n
        }
        if start > 0 { pending.removeFirst(start) }
        return out
    }

    /// Ends the spurt: pads the remainder (or a whole silent frame) and tags it with the speech-end marker. Always returns exactly one packet,
    /// even if the codec fails on the tail (then a payload-less marker), so the receiver always learns the spurt ended.
    func finish() -> [VoicePacket] {
        let n = codec.frameSamples
        var frame = pending; frame += [Float](repeating: 0, count: max(0, n - frame.count))
        pending.removeAll(keepingCapacity: true)
        if let p = makePacket(frame, flags: VoicePacket.speechEndFlag) { return [p] }
        let marker = VoicePacket(codec: codec.id, sequence: sequence, timestampMs: timestamp(), flags: VoicePacket.speechEndFlag, payload: Data())
        sequence &+= 1; return [marker]
    }

    func reset() { pending.removeAll(); clockSamples = 0 }

    private func timestamp() -> UInt32 { UInt32(truncatingIfNeeded: clockSamples / Int64(VoiceFormat.sampleRate / 1000)) }

    private func makePacket(_ frame: [Float], flags: UInt8) -> VoicePacket? {
        let ts = timestamp(), seq = sequence
        clockSamples += Int64(codec.frameSamples); sequence &+= 1   // consumed even on failure, so the receiver conceals instead of sliding time
        guard let payload = codec.encode(frame), !payload.isEmpty, payload.count <= VoiceFormat.maxPayloadBytes else { oversizeOrFailedFrames += 1; return nil }
        return VoicePacket(codec: codec.id, sequence: seq, timestampMs: ts, flags: flags, payload: payload)
    }
}

/// Decodes one remote sender's packets. Decoders are stateful per sender and per codec, created lazily so a peer on a different codec just works.
final class VoiceStreamDecoder {
    private var codecs = [VoiceCodecID: VoiceCodec]()
    private var unsupported = Set<VoiceCodecID>()
    private let factory: (VoiceCodecID) -> VoiceCodec?
    init(factory: @escaping (VoiceCodecID) -> VoiceCodec? = VoiceCodecFactory.make) { self.factory = factory }

    /// nil if this device cannot decode the packet's codec or the payload is garbage; [] for a payload-less end marker.
    func decode(_ packet: VoicePacket) -> [Float]? {
        if packet.payload.isEmpty { return packet.isSpeechEnd ? [] : nil }
        if codecs[packet.codec] == nil {
            guard !unsupported.contains(packet.codec), let c = factory(packet.codec) else { unsupported.insert(packet.codec); return nil }
            codecs[packet.codec] = c
        }
        return codecs[packet.codec]?.decode(packet.payload)
    }
    func decode(data: Data) -> (VoicePacket, [Float])? {
        guard let p = VoicePacket.parse(data), let pcm = decode(p) else { return nil }
        return (p, pcm)
    }
}

// MARK: - Jitter buffer

/// Per-sender reordering + pacing. `push` stores decoded packets by sequence (UInt16 wrap-safe), `pull` yields 20 ms frames.
/// Preallocated and allocation-free on both paths, because the engine calls `pull` from the audio render thread.
final class VoiceJitterBuffer {
    enum Output: Equatable { case silence, voice, concealed }
    struct Stats: Equatable { var pushed = 0, late = 0, duplicate = 0, rejected = 0, concealedPackets = 0, concealedFrames = 0, underruns = 0, resets = 0, discarded = 0 }
    private enum Phase { case idle, buffering, playing }

    let minDelayMs: Double, maxDelayMs: Double
    private(set) var targetDelayMs: Double
    private(set) var stats = Stats()
    var isIdle: Bool { phase == .idle }

    private static let slotCount = 64
    private static let slotCap = VoiceFormat.maxPacketSamples
    private static let frame = VoiceFormat.frameSamples
    private static let fifoCap = 16_000 * 2
    private static let fade: [Float] = [0.7, 0.4, 0.15]

    private let slotData = UnsafeMutablePointer<Float>.allocate(capacity: slotCount * slotCap)
    private var slotSeq = [UInt16](repeating: 0, count: slotCount)
    private var slotLen = [Int](repeating: -1, count: slotCount)
    private var slotEnd = [Bool](repeating: false, count: slotCount)
    private let fifo = UnsafeMutablePointer<Float>.allocate(capacity: fifoCap)
    private var fifoHead = 0, fifoCount = 0
    private let lastFrame = UnsafeMutablePointer<Float>.allocate(capacity: frame)
    private let scratch = UnsafeMutablePointer<Float>.allocate(capacity: slotCap)
    private var hasLast = false
    private var phase = Phase.idle
    private var nextSeq: UInt16 = 0
    private var lastPacketLen = frame
    private var concealRun = 0, silentFrames = 0
    private var rebuffering = false, endPending = false, endInSlots = 0, hasEnded = false
    private var jitterMs = 0.0, prevTransit: Double?, lastTimestamp: UInt32?, boostMs = 0.0

    init(minDelayMs: Double = 40, maxDelayMs: Double = 120) {
        self.minDelayMs = minDelayMs; self.maxDelayMs = maxDelayMs; targetDelayMs = minDelayMs
        lastFrame.initialize(repeating: 0, count: VoiceJitterBuffer.frame)
        slotData.initialize(repeating: 0, count: VoiceJitterBuffer.slotCount * VoiceJitterBuffer.slotCap)
        fifo.initialize(repeating: 0, count: VoiceJitterBuffer.fifoCap)
        scratch.initialize(repeating: 0, count: VoiceJitterBuffer.slotCap)
        _ = VoiceJitterBuffer.fade.count   // statics are lazy: pay the one-time init here, never on the render thread
    }
    deinit { slotData.deallocate(); fifo.deallocate(); lastFrame.deallocate(); scratch.deallocate() }

    /// Drops everything: the next push starts a fresh stream. Called on a stream restart, a peer leaving, or the engine stopping.
    func reset() {
        for i in 0..<VoiceJitterBuffer.slotCount { slotLen[i] = -1; slotEnd[i] = false }
        fifoHead = 0; fifoCount = 0; hasLast = false; phase = .idle
        concealRun = 0; silentFrames = 0; rebuffering = false; endPending = false; endInSlots = 0; hasEnded = false
        prevTransit = nil; lastTimestamp = nil
    }

    @discardableResult
    func push(sequence: UInt16, timestampMs: UInt32, samples: UnsafeBufferPointer<Float>, speechEnd: Bool, nowMs: Double) -> Bool {
        guard samples.count <= VoiceJitterBuffer.slotCap, (samples.count > 0 || speechEnd) else { stats.rejected += 1; return false }
        if phase != .idle {
            let d = Int(Int16(bitPattern: sequence &- nextSeq))
            // A jump this far is not reordering: the sender restarted (new stream) or we were cut off for over half a second.
            let tsBack = lastTimestamp.map { $0 > timestampMs && $0 - timestampMs > 2_000 } ?? false
            if d >= 32 || d < -32 || tsBack { reset(); stats.resets += 1 }
            else if d < 0 {
                // Only a not-yet-started stream may move its start back (first packets arriving out of order).
                if phase == .buffering, !rebuffering, d >= -8, slotLen[Int(sequence) & 63] < 0 { nextSeq = sequence }
                else { stats.late += 1; return false }
            }
        }
        if phase == .idle {
            // In idle we still refuse stragglers of the spurt that just ended (they would restart a phantom stream).
            let d = Int(Int16(bitPattern: sequence &- nextSeq))
            if stats.pushed > 0, d < 0, d >= -32, hasEnded { stats.late += 1; return false }
            nextSeq = sequence; phase = .buffering; rebuffering = false; endPending = false; concealRun = 0; silentFrames = 0; hasEnded = false
            prevTransit = nil; boostMs = max(0, boostMs)
        }
        let i = Int(sequence) & 63
        if slotLen[i] >= 0, slotSeq[i] == sequence { stats.duplicate += 1; return false }
        if slotLen[i] >= 0 { stats.late += 1; return false }
        if samples.count > 0 { (slotData + i * VoiceJitterBuffer.slotCap).update(from: samples.baseAddress!, count: samples.count) }
        slotSeq[i] = sequence; slotLen[i] = samples.count; slotEnd[i] = speechEnd
        if speechEnd { endInSlots += 1 }
        lastTimestamp = timestampMs; stats.pushed += 1
        // RFC 3550 interarrival jitter on (arrival - sender timestamp); only across one spurt, a pause is not jitter.
        let transit = nowMs - Double(timestampMs)
        if let prev = prevTransit { jitterMs += (abs(transit - prev) - jitterMs) / 16 }
        prevTransit = transit
        targetDelayMs = min(maxDelayMs, max(minDelayMs, 1.5 * jitterMs + 30 + boostMs))
        return true
    }

    @discardableResult
    func push(sequence: UInt16, timestampMs: UInt32, samples: [Float], speechEnd: Bool = false, nowMs: Double) -> Bool {
        samples.withUnsafeBufferPointer { push(sequence: sequence, timestampMs: timestampMs, samples: $0, speechEnd: speechEnd, nowMs: nowMs) }
    }

    /// Milliseconds of audio ready or contiguous behind it.
    var bufferedMs: Double { Double(fifoCount + contiguousSlotSamples()) * 1000 / Double(VoiceFormat.sampleRate) }

    private func contiguousSlotSamples() -> Int {
        var total = 0, s = nextSeq
        for _ in 0..<VoiceJitterBuffer.slotCount {
            let i = Int(s) & 63
            guard slotLen[i] >= 0, slotSeq[i] == s else { break }
            total += slotLen[i]; s &+= 1
        }
        return total
    }
    private func hasLaterPacket() -> Bool {
        for k in 1..<VoiceJitterBuffer.slotCount {
            let s = nextSeq &+ UInt16(k), i = Int(s) & 63
            if slotLen[i] >= 0, slotSeq[i] == s { return true }
        }
        return false
    }

    /// While (re)priming, the packet we wait for may simply be lost: once enough newer audio is queued behind the hole, step over it
    /// instead of waiting for a packet that will never come (otherwise a 2-packet loss would stall until the 1 s give-up).
    private func skipLostHead() {
        let head = Int(nextSeq) & 63
        guard slotLen[head] < 0 || slotSeq[head] != nextSeq else { return }
        for k in 1..<VoiceJitterBuffer.slotCount {
            let s = nextSeq &+ UInt16(k), i = Int(s) & 63
            guard slotLen[i] >= 0, slotSeq[i] == s else { continue }
            var samples = 0, t = s
            for _ in 0..<VoiceJitterBuffer.slotCount {
                let j = Int(t) & 63
                guard slotLen[j] >= 0, slotSeq[j] == t else { break }
                samples += slotLen[j]; t &+= 1
            }
            if Double(samples) * 1000 / Double(VoiceFormat.sampleRate) >= targetDelayMs || endInSlots > 0 {
                stats.concealedPackets += k; nextSeq = s
            }
            return
        }
    }

    private func fifoAppend(_ src: UnsafePointer<Float>, _ n: Int) {
        guard n > 0, fifoCount + n <= VoiceJitterBuffer.fifoCap else { return }
        let cap = VoiceJitterBuffer.fifoCap
        var w = (fifoHead + fifoCount) % cap
        for k in 0..<n { fifo[w] = src[k]; w += 1; if w == cap { w = 0 } }
        fifoCount += n
    }
    private func fifoPop(_ out: UnsafeMutablePointer<Float>, _ n: Int) {
        let cap = VoiceJitterBuffer.fifoCap
        for k in 0..<n { out[k] = fifo[fifoHead]; fifoHead += 1; if fifoHead == cap { fifoHead = 0 } }
        fifoCount -= n
    }

    /// Real audio only feeds concealment (never our own faded copies), so consecutive losses fade 0.7 / 0.4 / 0.15 / 0 from the same source.
    private func rememberTail(_ src: UnsafePointer<Float>, _ n: Int) {
        let F = VoiceJitterBuffer.frame
        if n >= F { lastFrame.update(from: src + (n - F), count: F) }
        else { (lastFrame).update(from: lastFrame + n, count: F - n); (lastFrame + (F - n)).update(from: src, count: n) }
        hasLast = true
    }

    private func drain() {
        while !endPending, fifoCount + VoiceJitterBuffer.slotCap <= VoiceJitterBuffer.fifoCap {
            let i = Int(nextSeq) & 63
            guard slotLen[i] >= 0, slotSeq[i] == nextSeq else { break }
            let n = slotLen[i]
            if n > 0 { rememberTail(slotData + i * VoiceJitterBuffer.slotCap, n); fifoAppend(slotData + i * VoiceJitterBuffer.slotCap, n); lastPacketLen = n; concealRun = 0 }
            if slotEnd[i] { endPending = true; endInSlots -= 1 }
            slotLen[i] = -1; slotEnd[i] = false; nextSeq &+= 1
        }
    }

    /// Repeats the last good frame, faded; after three lost frames, silence.
    private func concealment(_ out: UnsafeMutablePointer<Float>, _ n: Int) {
        let g = concealRun < VoiceJitterBuffer.fade.count ? VoiceJitterBuffer.fade[concealRun] : 0
        let f = VoiceJitterBuffer.frame
        for k in 0..<n { out[k] = hasLast ? lastFrame[k % f] * g : 0 }
        concealRun += 1
    }

    /// Fills exactly one 20 ms frame (320 samples).
    @discardableResult
    func pull(into out: UnsafeMutablePointer<Float>) -> Output {
        let F = VoiceJitterBuffer.frame
        switch phase {
        case .idle:
            out.update(repeating: 0, count: F); return .silence
        case .buffering:
            skipLostHead()
            if bufferedMs >= targetDelayMs || endInSlots > 0 { phase = .playing; rebuffering = false; silentFrames = 0 }
            else { return waiting(out) }
        case .playing: break
        }
        var guardLoop = 0
        while guardLoop < 80 {  // each pass pops, discards one frame or conceals one lost packet (at most 32 of those pending)
            guardLoop += 1
            drain()
            if endPending, fifoCount < F {
                let n = fifoCount
                if n > 0 { fifoPop(out, n) }
                (out + n).update(repeating: 0, count: F - n)
                endPending = false; hasEnded = true
                if endInSlots > 0 || contiguousSlotSamples() > 0 { phase = .buffering } else { phase = .idle }
                return n > 0 ? .voice : .silence
            }
            if fifoCount >= F {
                // Latency creeping up (sender clock faster, or a burst): skip one frame rather than drift.
                if bufferedMs > targetDelayMs + 100 { fifoHead = (fifoHead + F) % VoiceJitterBuffer.fifoCap; fifoCount -= F; stats.discarded += 1; continue }
                fifoPop(out, F); silentFrames = 0
                boostMs = max(0, boostMs - 0.02)
                return .voice
            }
            if hasLaterPacket() {
                // nextSeq never came but newer packets did: it is lost, fill its slot with concealment and move on.
                let n = min(lastPacketLen, VoiceJitterBuffer.slotCap)
                concealment(scratch, n); fifoAppend(scratch, n)
                nextSeq &+= 1; stats.concealedPackets += 1
                continue
            }
            break
        }
        // Nothing (or only a stub) to play: underrun. Use what is left, conceal the rest, and re-prime before resuming.
        if concealRun == 0 { stats.underruns += 1; boostMs = min(maxDelayMs, boostMs + 20) }
        let n = min(fifoCount, F)
        if n > 0 { fifoPop(out, n) }
        concealment(out + n, F - n)
        stats.concealedFrames += 1
        phase = .buffering; rebuffering = true
        silentFrames += 1
        return .concealed
    }

    /// While re-priming after an underrun: keep the fade going, then silence; give up after a second of nothing.
    private func waiting(_ out: UnsafeMutablePointer<Float>) -> Output {
        let F = VoiceJitterBuffer.frame
        guard rebuffering else { out.update(repeating: 0, count: F); return .silence }
        silentFrames += 1
        if silentFrames > 50 { reset(); stats.resets += 1; out.update(repeating: 0, count: F); return .silence }
        concealment(out, F); stats.concealedFrames += 1
        return .concealed
    }

    /// Convenience for tests and tools (allocates).
    func pullFrame() -> (frame: [Float], output: Output) {
        var f = [Float](repeating: 0, count: VoiceFormat.frameSamples)
        let o = f.withUnsafeMutableBufferPointer { pull(into: $0.baseAddress!) }
        return (f, o)
    }
}

// MARK: - Voice activity

/// Energy VAD with a noise-floor tracker and hangover. Deterministic: only the frames you feed it matter (no clock).
struct VoiceActivityDetector {
    struct Config: Equatable {
        /// Speech starts when RMS exceeds floor*onRatio, and continues while above floor*offRatio (hysteresis).
        var onRatio: Float = 3.0
        var offRatio: Float = 2.0
        /// Absolute floor of the threshold so a digitally silent or very quiet mic never opens by a rounding blip.
        var minRMS: Float = 0.010
        var hangoverMs = 300
        var frameMs = VoiceFormat.frameMs
        var floorMin: Float = 0.0004
        var floorMax: Float = 0.06
        /// Per-frame smoothing: floor drops quickly onto quieter input and rises slowly (speech must not become "noise").
        var floorFall: Float = 0.25
        var floorRise: Float = 0.01
        /// Continuous "speech" longer than this is stationary noise (a TV): let the floor creep up so the mic closes.
        var stationaryMs = 12_000
    }
    var config: Config
    private(set) var noiseFloor: Float
    private(set) var isActive = false
    private(set) var lastRMS: Float = 0
    private var hangoverLeft = 0, activeFrames = 0

    init(config: Config = Config()) { self.config = config; noiseFloor = config.floorMin * 4 }

    static func rms(_ x: UnsafeBufferPointer<Float>) -> Float {
        guard x.count > 0 else { return 0 }
        var s: Float = 0
        for v in x { s += v * v }
        return (s / Float(x.count)).squareRoot()
    }

    /// Feed one 20 ms frame; returns whether the mic should be treated as open (includes the hangover).
    @discardableResult
    mutating func process(_ frame: UnsafeBufferPointer<Float>) -> Bool {
        let level = VoiceActivityDetector.rms(frame)
        lastRMS = level.isFinite ? level : 0
        let on = max(noiseFloor * config.onRatio, config.minRMS)
        let off = max(noiseFloor * config.offRatio, config.minRMS * 0.7)
        let loud = lastRMS > (isActive ? off : on)
        let hangFrames = max(1, config.hangoverMs / max(config.frameMs, 1))
        if loud { isActive = true; hangoverLeft = hangFrames; activeFrames += 1 }
        else if isActive { hangoverLeft -= 1; if hangoverLeft <= 0 { isActive = false; activeFrames = 0 } }
        else { activeFrames = 0 }
        let stationary = activeFrames * config.frameMs > config.stationaryMs
        if !loud || stationary {
            let rate = lastRMS < noiseFloor ? config.floorFall : (stationary ? config.floorRise * 0.2 : config.floorRise)
            noiseFloor = min(config.floorMax, max(config.floorMin, noiseFloor + (lastRMS - noiseFloor) * rate))
        }
        return isActive
    }
    @discardableResult
    mutating func process(_ frame: [Float]) -> Bool { frame.withUnsafeBufferPointer { process($0) } }
    mutating func reset() { isActive = false; hangoverLeft = 0; activeFrames = 0; noiseFloor = config.floorMin * 4; lastRMS = 0 }
}

// MARK: - Test signal and metrics (shared by the probe, the macOS harness and the XCTests)

enum VoiceTestSignal {
    /// Voiced-speech-like: 130 Hz pitch with vibrato, 14 harmonics under two formant bumps, syllable envelope (about 4/s) with short gaps. Deterministic.
    static func speechLike(seconds: Double, sampleRate: Int = VoiceFormat.sampleRate, amplitude: Float = 0.35) -> [Float] {
        let n = Int(seconds * Double(sampleRate))
        var out = [Float](repeating: 0, count: n)
        var phase = 0.0
        for i in 0..<n {
            let t = Double(i) / Double(sampleRate)
            let f0 = 130 + 18 * sin(2 * Double.pi * 0.7 * t) + 25 * sin(2 * Double.pi * 0.23 * t)
            phase += 2 * Double.pi * f0 / Double(sampleRate)
            var s = 0.0
            for h in 1...14 {
                let f = f0 * Double(h)
                let formant = exp(-pow((f - 700) / 350, 2)) + 0.6 * exp(-pow((f - 1800) / 600, 2)) + 0.05
                s += formant / Double(h).squareRoot() * sin(Double(h) * phase)
            }
            let env = max(0, sin(2 * Double.pi * 4 * t)) * (0.6 + 0.4 * sin(2 * Double.pi * 0.9 * t))
            out[i] = Float(s * env * 0.25) * amplitude
        }
        return out
    }
    static func noise(seconds: Double, rms: Float, seed: UInt64 = 1) -> [Float] {
        var state = seed &* 6364136223846793005 &+ 1442695040888963407
        return (0..<Int(seconds * Double(VoiceFormat.sampleRate))).map { _ in
            state = state &* 6364136223846793005 &+ 1442695040888963407
            return (Float(state >> 40) / Float(1 << 24) * 2 - 1) * rms * 1.732
        }
    }
}

enum VoiceMetrics {
    /// SNR in dB of `test` against `reference` after the best integer delay (codecs add lookahead / filter delay) and best gain. Skips the first 0.1 s (codec priming)
    /// and looks at the next `window` samples only (the search is O(window x maxLag); keep it cheap enough for a Debug test run).
    static func bestLagSNR(reference: [Float], test: [Float], maxLag: Int, skip: Int = 1600, window: Int = 12_000) -> Double {
        guard reference.count > skip + maxLag, test.count > skip + maxLag else { return -99 }
        let end = min(reference.count, test.count, skip + window) - maxLag
        guard end > skip else { return -99 }
        var best = -99.0
        for lag in 0...maxLag {
            var rr = 0.0, tt = 0.0, rt = 0.0
            for i in skip..<end { let r = Double(reference[i]), t = Double(test[i + lag]); rr += r * r; tt += t * t; rt += r * t }
            guard rr > 0, tt > 0 else { continue }
            let g = rt / tt   // optimal scale of test
            let err = rr - 2 * g * rt + g * g * tt
            best = max(best, 10 * log10(rr / max(err, 1e-12)))
        }
        return best
    }
}
