import XCTest
@testable import GolfArcade

// Party voice (PARTY_VOICE_NOTES.md). Codec, packet, jitter-buffer and VAD tests are pure and run anywhere; the engine tests drive the real
// PartyVoiceEngine through a fake VoiceAudioBackend, so state transitions, gating, mixing and recovery are exercised without a microphone.
// What these tests cannot prove (AEC against TV audio, AirPlay routing, Bluetooth, Unity's session) is on the device checklist in the notes.

private func speech(_ seconds: Double, amplitude: Float = 0.8) -> [Float] { VoiceTestSignal.speechLike(seconds: seconds, amplitude: amplitude) }

final class VoiceCodecTests: XCTestCase {
    func testPacketLayoutAndRoundTrip() {
        let p = VoicePacket(codec: .iLBC, sequence: 0xBEEF, timestampMs: 0x01020304, flags: 1, payload: Data([1, 2, 3]))
        let d = p.serialized()
        XCTAssertEqual(Array(d.prefix(9)), [1, 2, 0xEF, 0xBE, 4, 3, 2, 1, 1])
        XCTAssertEqual(VoicePacket.parse(d), p)
        var shifted = Data([9, 9, 9]); shifted.append(d)
        XCTAssertEqual(VoicePacket.parse(shifted.dropFirst(3)), p, "a Data slice that does not start at index 0")
    }

    func testMalformedPacketsAreRejected() {
        let good = VoicePacket(codec: .aacELD, sequence: 1, timestampMs: 2, payload: Data([1, 2, 3])).serialized()
        var badVersion = good; badVersion[0] = 2
        var badCodec = good; badCodec[1] = 99
        var badFlags = good; badFlags[8] = 0x80
        var emptyNoEnd = Data(good.prefix(9)); emptyNoEnd[8] = 0
        XCTAssertNil(VoicePacket.parse(badVersion)); XCTAssertNil(VoicePacket.parse(badCodec)); XCTAssertNil(VoicePacket.parse(badFlags))
        XCTAssertNil(VoicePacket.parse(Data())); XCTAssertNil(VoicePacket.parse(good.prefix(8)))
        XCTAssertNil(VoicePacket.parse(emptyNoEnd), "an empty payload is only legal on the speech-end marker")
        var endOnly = Data(good.prefix(9)); endOnly[8] = 1
        XCTAssertNotNil(VoicePacket.parse(endOnly))
        var max = good; max.append(Data(repeating: 7, count: 200 - 3))
        XCTAssertEqual(max.count, VoiceFormat.maxPacketBytes); XCTAssertNotNil(VoicePacket.parse(max))
        max.append(0); XCTAssertNil(VoicePacket.parse(max), "one byte over the cap")
    }

    func testPacketFuzzNeverTraps() {
        var s: UInt64 = 42
        func next() -> UInt64 { s = s &* 6364136223846793005 &+ 1442695040888963407; return s >> 11 }
        for _ in 0..<100_000 {
            let n = Int(next() % 260)
            var d = Data((0..<n).map { _ in UInt8(truncatingIfNeeded: next()) })
            if n > 9, next() % 2 == 0 { d[0] = 1; d[1] = UInt8(1 + next() % 3); d[8] &= 1 }
            if let p = VoicePacket.parse(d) { XCTAssertEqual(VoicePacket.parse(p.serialized()), p) }
        }
    }

    func testRateLimiterBurstAndRefill() {
        var rl = VoiceRateLimiter(ratePerSecond: 100, burst: 10)
        XCTAssertEqual((0..<100).filter { _ in rl.allow(nowMs: 0) }.count, 10)
        XCTAssertEqual((0..<100).filter { _ in rl.allow(nowMs: 1000) }.count, 10)
    }

    func testProbeAlwaysFindsAWorkingCodecAndPrefersTheBest() {
        let ids = VoiceCodecFactory.available
        XCTAssertTrue(ids.contains(.pcm16), "the built-in fallback must always pass")
        XCTAssertEqual(ids, VoiceCodecFactory.preference.filter(ids.contains), "available keeps the preference order")
        XCTAssertEqual(VoiceCodecFactory.best, ids.first)
        for p in VoiceCodecFactory.probes where p.ok { XCTAssertLessThanOrEqual(p.maxBytes, VoiceFormat.maxPayloadBytes, "\(p.id)") }
    }

    func testEveryAvailableCodecRoundTripsSpeechThroughTheWire() {
        let sig = VoiceTestSignal.speechLike(seconds: 3)
        for id in VoiceCodecFactory.available {
            let enc = VoiceStreamEncoder(codec: VoiceCodecFactory.make(id)!, startSequence: 65530), dec = VoiceStreamDecoder()
            var out = [Float](), packets = [VoicePacket]()
            var i = 0
            while i < sig.count {   // awkward chunk sizes exercise the framing buffer
                let n = min(sig.count - i, 37 + (i % 650))
                packets += enc.encode(Array(sig[i..<i + n])); i += n
            }
            for p in packets {
                guard let (q, pcm) = dec.decode(data: p.serialized()) else { return XCTFail("\(id) packet \(p.sequence) did not survive the wire") }
                XCTAssertEqual(q.codec, id); XCTAssertLessThanOrEqual(p.payload.count, VoiceFormat.maxPayloadBytes)
                out += pcm
            }
            let snr = VoiceMetrics.bestLagSNR(reference: sig, test: out, maxLag: 1200)
            XCTAssertGreaterThan(snr, VoiceCodecFactory.snrFloor(id) + 1, "\(id) SNR \(snr) dB")
            XCTAssertEqual(enc.oversizeOrFailedFrames, 0, "\(id)")
            XCTAssertTrue(zip(packets, packets.dropFirst()).allSatisfy { $1.sequence == $0.sequence &+ 1 }, "\(id) sequence wraps 65535 -> 0")
            let tail = enc.finish()
            XCTAssertEqual(tail.count, 1); XCTAssertTrue(tail[0].isSpeechEnd)
        }
    }

    func testFullScaleNoiseStaysUnderThePayloadCap() {
        for id in VoiceCodecFactory.available {
            let enc = VoiceStreamEncoder(codec: VoiceCodecFactory.make(id)!)
            let ps = enc.encode(VoiceTestSignal.noise(seconds: 2, rms: 0.5))
            XCTAssertTrue(ps.allSatisfy { $0.payload.count <= VoiceFormat.maxPayloadBytes }, "\(id)")
            XCTAssertEqual(enc.oversizeOrFailedFrames, 0, "\(id) dropped frames")
        }
    }

    func testDecodersSurviveGarbagePayloads() {
        var s: UInt64 = 99
        func next() -> UInt64 { s = s &* 6364136223846793005 &+ 1442695040888963407; return s >> 11 }
        for id in VoiceCodecID.allCases {
            guard let codec = VoiceCodecFactory.make(id) else { continue }
            for _ in 0..<2000 {
                let d = Data((0..<(1 + Int(next() % 200))).map { _ in UInt8(truncatingIfNeeded: next()) })
                if let pcm = codec.decode(d) { XCTAssertTrue(pcm.allSatisfy { $0.isFinite && abs($0) <= 1.0001 }, "\(id)") }
            }
            XCTAssertNil(codec.decode(Data(repeating: 1, count: 201)), "\(id) oversize payload")
            let ps = VoiceStreamEncoder(codec: VoiceCodecFactory.make(id)!).encode(VoiceTestSignal.speechLike(seconds: 0.5))
            XCTAssertNotNil(ps.first.flatMap { codec.decode($0.payload) }, "\(id) still decodes after garbage")
        }
        let none = VoiceStreamDecoder(factory: { _ in nil })
        XCTAssertNil(none.decode(VoicePacket(codec: .aacELD, sequence: 1, timestampMs: 0, payload: Data([1]))), "codec this device lacks")
        XCTAssertEqual(none.decode(VoicePacket(codec: .iLBC, sequence: 1, timestampMs: 0, flags: 1, payload: Data())), [])
    }
}

final class VoiceJitterBufferTests: XCTestCase {
    /// Real-time simulation: each 20 ms tick delivers the packets scheduled for it and pulls one frame. Packet k is constant-valued (k+1)/100.
    private func simulate(_ jb: VoiceJitterBuffer, packets: Int, startSeq: Int = 0, ticks: Int, arrive: (Int) -> Int?, endAt: Int? = nil) -> (voice: [Float], kinds: [VoiceJitterBuffer.Output]) {
        var voice = [Float](), kinds = [VoiceJitterBuffer.Output]()
        for t in 0..<ticks {
            for k in 0..<packets where arrive(k) == t {
                jb.push(sequence: UInt16(truncatingIfNeeded: startSeq + k), timestampMs: UInt32(k * 20), samples: [Float](repeating: Float(k + 1) / 100, count: 320), speechEnd: endAt == k, nowMs: Double(t * 20))
            }
            let (f, o) = jb.pullFrame(); kinds.append(o); if o == .voice { voice.append(f[0]) }
        }
        return (voice, kinds)
    }
    private func expected(_ n: Int) -> [Float] { (0..<n).map { Float($0 + 1) / 100 } }

    func testInOrderPlaysEverythingOnce() {
        let jb = VoiceJitterBuffer()
        let r = simulate(jb, packets: 40, ticks: 60, arrive: { $0 }, endAt: 39)
        XCTAssertEqual(r.voice, expected(40)); XCTAssertEqual(jb.stats.concealedFrames, 0); XCTAssertEqual(jb.stats.discarded, 0)
        XCTAssertTrue(jb.isIdle, "the speech-end marker ends the spurt without concealment")
    }
    func testReorderedPacketsPlayInSequence() {
        let jb = VoiceJitterBuffer()
        let r = simulate(jb, packets: 40, ticks: 70, arrive: { k in k + (k % 4 == 0 ? 2 : k % 4 == 2 ? 1 : 0) }, endAt: 39)
        XCTAssertEqual(r.voice, expected(40))
    }
    func testSequenceWrapAround() {
        let jb = VoiceJitterBuffer()
        let r = simulate(jb, packets: 40, startSeq: 65520, ticks: 60, arrive: { $0 }, endAt: 39)
        XCTAssertEqual(r.voice, expected(40)); XCTAssertEqual(jb.stats.late, 0); XCTAssertEqual(jb.stats.resets, 0)
    }
    func testOneLostPacketIsConcealedInPlace() {
        let jb = VoiceJitterBuffer()
        let r = simulate(jb, packets: 30, ticks: 50, arrive: { $0 == 10 ? nil : $0 }, endAt: 29)
        XCTAssertEqual(r.voice.count, 30)
        XCTAssertEqual(r.voice[10], 0.7 * 0.10, accuracy: 1e-5, "last good frame, faded to 70 %")
        XCTAssertEqual(r.voice[11], 0.12, accuracy: 1e-6); XCTAssertEqual(r.voice[29], 0.30, accuracy: 1e-6, "time did not slide")
        XCTAssertEqual(jb.stats.concealedPackets, 1)
    }
    func testTwoLostPacketsFadeFurtherWithALargerFloor() {
        let jb = VoiceJitterBuffer(minDelayMs: 80, maxDelayMs: 120)
        let r = simulate(jb, packets: 30, ticks: 50, arrive: { ($0 == 10 || $0 == 11) ? nil : $0 }, endAt: 29)
        XCTAssertEqual(r.voice.count, 30)
        XCTAssertEqual(r.voice[10], 0.07, accuracy: 1e-5); XCTAssertEqual(r.voice[11], 0.04, accuracy: 1e-5); XCTAssertEqual(r.voice[12], 0.13, accuracy: 1e-6)
    }
    func testUnderrunRecoversByItselfInsteadOfStallingOnTheHole() {
        let jb = VoiceJitterBuffer()
        let r = simulate(jb, packets: 30, ticks: 50, arrive: { ($0 == 10 || $0 == 11) ? nil : $0 }, endAt: 29)
        let concealed = r.kinds.filter { $0 == .concealed }.count
        XCTAssertEqual(r.voice.count, 28); XCTAssertEqual(r.voice.last, 0.30); XCTAssertTrue((2...4).contains(concealed)); XCTAssertEqual(jb.stats.underruns, 1)
    }
    func testSenderGoneWithoutAMarkerFadesToSilenceThenIdle() {
        let jb = VoiceJitterBuffer()
        let r = simulate(jb, packets: 10, ticks: 90, arrive: { $0 })
        let tail = zip(r.kinds, 0...).filter { $0.0 == .concealed }.count
        XCTAssertGreaterThan(tail, 3); XCTAssertTrue(jb.isIdle); XCTAssertEqual(jb.stats.resets, 1)
        var now = 5000.0, played = 0   // a new stream (any sequence) works afterwards
        for k in 0..<6 { jb.push(sequence: UInt16(5000 + k), timestampMs: UInt32(k * 20), samples: [Float](repeating: 0.2, count: 320), nowMs: now); now += 20; if jb.pullFrame().output == .voice { played += 1 } }
        for _ in 0..<8 where jb.pullFrame().output == .voice { played += 1 }
        XCTAssertEqual(played, 6)
    }
    func testSenderRestartResetsInsteadOfDroppingEverythingAsLate() {
        let jb = VoiceJitterBuffer(); var now = 0.0
        for k in 0..<10 { jb.push(sequence: UInt16(1000 + k), timestampMs: UInt32(5000 + k * 20), samples: [Float](repeating: 0.5, count: 320), nowMs: now); now += 20 }
        for _ in 0..<3 { _ = jb.pullFrame() }
        for k in 0..<10 { jb.push(sequence: UInt16(k), timestampMs: UInt32(k * 20), samples: [Float](repeating: 0.25, count: 320), nowMs: now); now += 20 }
        XCTAssertEqual(jb.stats.resets, 1)
        var vals = [Float](); for _ in 0..<10 { let (f, o) = jb.pullFrame(); if o == .voice { vals.append(f[0]) } }
        XCTAssertFalse(vals.isEmpty); XCTAssertTrue(vals.allSatisfy { $0 == 0.25 }, "only the new stream's audio")
    }
    func testDuplicateLateAndOversizeAreDropped() {
        let jb = VoiceJitterBuffer(); var now = 0.0
        for k in 0..<10 { jb.push(sequence: UInt16(k), timestampMs: UInt32(k * 20), samples: [Float](repeating: 0.5, count: 320), nowMs: now); now += 20 }
        XCTAssertFalse(jb.push(sequence: 5, timestampMs: 100, samples: [Float](repeating: 0.5, count: 320), nowMs: now)); XCTAssertEqual(jb.stats.duplicate, 1)
        for _ in 0..<6 { _ = jb.pullFrame() }
        XCTAssertFalse(jb.push(sequence: 1, timestampMs: 20, samples: [Float](repeating: 0.9, count: 320), nowMs: now)); XCTAssertEqual(jb.stats.late, 1)
        XCTAssertFalse(jb.push(sequence: 12, timestampMs: 0, samples: [Float](repeating: 0.9, count: 600), nowMs: now)); XCTAssertEqual(jb.stats.rejected, 1)
    }
    func testThirtyMillisecondCodecPacketsPaceCorrectlyAndSecondSpurtStarts() {
        let jb = VoiceJitterBuffer()
        var voiced = 0, concealed = 0, next = 0
        for t in 0..<40 {
            while next < 7, next * 30 <= t * 20 { jb.push(sequence: UInt16(next), timestampMs: UInt32(next * 30), samples: [Float](repeating: 0.5, count: 480), speechEnd: next == 6, nowMs: Double(next * 30)); next += 1 }
            let o = jb.pullFrame().output; if o == .voice { voiced += 1 }; if o == .concealed { concealed += 1 }
        }
        XCTAssertTrue([10, 11].contains(voiced), "7 x 480 / 320 = 10.5 frames, got \(voiced)"); XCTAssertEqual(concealed, 0); XCTAssertTrue(jb.isIdle)
        XCTAssertFalse(jb.push(sequence: 5, timestampMs: 150, samples: [Float](repeating: 0.5, count: 480), nowMs: 2000), "straggler of the spurt that just ended")
        var again = 0, k = 7
        for t in 0..<30 {
            while k < 12, (k - 7) * 30 <= t * 20 { jb.push(sequence: UInt16(k), timestampMs: UInt32(1000 + k * 30), samples: [Float](repeating: 0.5, count: 480), nowMs: 2000 + Double((k - 7) * 30)); k += 1 }
            if jb.pullFrame().output == .voice { again += 1 }
        }
        XCTAssertGreaterThanOrEqual(again, 6)
    }
    func testAdaptiveTargetStaysInsideItsBounds() {
        let steady = VoiceJitterBuffer(), jittery = VoiceJitterBuffer()
        var s: UInt64 = 5, now = 0.0
        for k in 0..<200 {
            steady.push(sequence: UInt16(k), timestampMs: UInt32(k * 20), samples: [Float](repeating: 0.1, count: 320), nowMs: now)
            s = s &* 6364136223846793005 &+ 1442695040888963407
            jittery.push(sequence: UInt16(k), timestampMs: UInt32(k * 20), samples: [Float](repeating: 0.1, count: 320), nowMs: now + Double((s >> 33) % 120))
            now += 20
        }
        XCTAssertEqual(steady.targetDelayMs, 40); XCTAssertGreaterThan(jittery.targetDelayMs, 70); XCTAssertLessThanOrEqual(jittery.targetDelayMs, 120)
    }
    func testBurstDoesNotLetLatencyGrowUnbounded() {
        let jb = VoiceJitterBuffer(); var now = 0.0
        for k in 0..<30 { jb.push(sequence: UInt16(k), timestampMs: UInt32(k * 20), samples: [Float](repeating: 0.1, count: 320), nowMs: now); now += 1 }
        for _ in 0..<3 { _ = jb.pullFrame() }
        XCTAssertLessThanOrEqual(jb.bufferedMs, jb.targetDelayMs + 121)
    }
    func testRandomPushPullNeverTrapsAndStaysFinite() {
        let jb = VoiceJitterBuffer(); var s: UInt64 = 123, now = 0.0
        func r(_ n: Int) -> Int { s = s &* 6364136223846793005 &+ 1442695040888963407; return Int((s >> 33) % UInt64(n)) }
        for _ in 0..<60_000 {
            if r(3) != 0 {
                jb.push(sequence: UInt16(truncatingIfNeeded: r(2) == 0 ? r(65536) : Int(jb.stats.pushed) + r(10)), timestampMs: UInt32(r(1_000_000)), samples: [Float](repeating: 0.3, count: r(520)), speechEnd: r(20) == 0, nowMs: now)
            } else { XCTAssertTrue(jb.pullFrame().frame.allSatisfy(\.isFinite)) }
            now += Double(r(30))
        }
    }
}

final class VoiceActivityDetectorTests: XCTestCase {
    private let n = VoiceFormat.frameSamples
    private func loudFrame() -> [Float] {
        let s = speech(2)
        for i in stride(from: 0, to: s.count - n, by: n) { let f = Array(s[i..<i + n]); if f.withUnsafeBufferPointer({ VoiceActivityDetector.rms($0) }) > 0.05 { return f } }
        return []
    }
    func testSilenceAndQuietRoomNoiseNeverOpen() {
        var vad = VoiceActivityDetector()
        XCTAssertFalse((0..<200).contains { _ in vad.process([Float](repeating: 0, count: n)) })
        let room = VoiceTestSignal.noise(seconds: 3, rms: 0.004)
        XCTAssertFalse(stride(from: 0, to: room.count - n, by: n).contains { vad.process(Array(room[$0..<$0 + n])) })
    }
    func testOpensAtOnceAndClosesAfterThe300msHangover() {
        var vad = VoiceActivityDetector(); let loud = loudFrame(); XCTAssertFalse(loud.isEmpty)
        let quiet = VoiceTestSignal.noise(seconds: 1, rms: 0.004)
        for i in stride(from: 0, to: quiet.count - n, by: n) { vad.process(Array(quiet[i..<i + n])) }
        XCTAssertTrue(vad.process(loud))
        var open = 0, closedAt = -1
        for k in 0..<40 { if vad.process(Array(quiet[0..<n])) { open += 1 } else if closedAt < 0 { closedAt = k } }
        XCTAssertEqual(open, 14); XCTAssertEqual(closedAt, 14, "hangover is 300 ms = 15 frames including the speech frame")
    }
    func testNewSpeechDuringHangoverExtendsIt() {
        var vad = VoiceActivityDetector(); let loud = loudFrame(), silence = [Float](repeating: 0, count: n)
        vad.process(loud); for _ in 0..<10 { vad.process(silence) }; vad.process(loud)
        XCTAssertEqual((0..<30).filter { _ in vad.process(silence) }.count, 14)
    }
    func testStationaryLoudNoiseEventuallyClosesTheMic() {
        var vad = VoiceActivityDetector(); let tv = VoiceTestSignal.noise(seconds: 120, rms: 0.05)
        var lastOpen = 0
        for (k, i) in stride(from: 0, to: tv.count - n, by: n).enumerated() where vad.process(Array(tv[i..<i + n])) { lastOpen = k }
        XCTAssertLessThan(lastOpen, 5000, "a TV left on must not keep an open mic transmitting forever")
    }
    func testConfigurableAndDeterministicAndNaNSafe() {
        var strict = VoiceActivityDetector(config: .init(minRMS: 0.5)); XCTAssertFalse(strict.process(loudFrame()))
        var a = VoiceActivityDetector(), b = VoiceActivityDetector(); let s = speech(2)
        for i in stride(from: 0, to: s.count - n, by: n) { XCTAssertEqual(a.process(Array(s[i..<i + n])), b.process(Array(s[i..<i + n]))) }
        var v = VoiceActivityDetector(); v.process([Float](repeating: .nan, count: n)); v.process([Float](repeating: 0, count: n))
        XCTAssertTrue(v.noiseFloor.isFinite)
    }
}

final class VoiceSettingsTests: XCTestCase {
    func testDecodingToleratesMissingAndUnknownKeys() throws {
        let old = try JSONDecoder().decode(VoiceSettings.self, from: Data(#"{"mode":"openMic","inputGain":1.5}"#.utf8))
        XCTAssertEqual(old.mode, .openMic); XCTAssertEqual(old.inputGain, 1.5); XCTAssertEqual(old.outputVolume, 1); XCTAssertTrue(old.noiseSuppression)
        XCTAssertEqual(try JSONDecoder().decode(VoiceSettings.self, from: Data(#"{"mode":"hologram"}"#.utf8)).mode, .off, "an unknown mode never turns the mic on")
        let s = VoiceSettings(mode: .pushToTalk, inputGain: 0.7, outputVolume: 0.4, noiseSuppression: false, perPeerVolume: ["a": 1.5], mutedPeers: ["b"])
        XCTAssertEqual(try JSONDecoder().decode(VoiceSettings.self, from: JSONEncoder().encode(s)), s)
        XCTAssertEqual(VoiceSettings(mode: .openMic, inputGain: 9, outputVolume: -1, perPeerVolume: ["a": .nan]).clamped.inputGain, 2)
    }
    func testDefaultsAreOff() { XCTAssertEqual(VoiceSettings().mode, .off) }
}

#if os(iOS)
/// No microphone, no AVAudioSession: records what the engine asks of the hardware and lets a test play the part of the tap and the speaker.
final class FakeVoiceBackend: VoiceAudioBackend, @unchecked Sendable {
    private let lock = NSLock()
    private var _capture: (@Sendable (UnsafeBufferPointer<Float>) -> Void)?
    private var _render: (@Sendable (UnsafeMutablePointer<Float>, Int) -> Void)?
    private var handler: (@Sendable (VoiceBackendEvent) -> Void)?
    private var _log = [String]()
    private var _permission = VoicePermission.granted
    private var permissionWaiter: CheckedContinuation<Bool, Never>?
    var grantOnRequest = true, holdPermissionPrompt = false, startError: Error?
    var sessionActive: Bool { lock.lock(); defer { lock.unlock() }; return _sessionActive }
    var ioRunning: Bool { lock.lock(); defer { lock.unlock() }; return _capture != nil }
    private var _sessionActive = false
    var lastNoiseSuppression: Bool?
    @MainActor var active = true

    var log: [String] { lock.lock(); defer { lock.unlock() }; return _log }
    func count(_ s: String) -> Int { log.filter { $0 == s }.count }
    private func note(_ s: String) { lock.lock(); _log.append(s); lock.unlock() }

    var permission: VoicePermission { lock.lock(); defer { lock.unlock() }; return _permission }
    func setPermission(_ p: VoicePermission) { lock.lock(); _permission = p; lock.unlock() }
    func requestPermission() async -> Bool {
        note("requestPermission")
        if holdPermissionPrompt { return await withCheckedContinuation { c in lock.lock(); permissionWaiter = c; lock.unlock() } }
        setPermission(grantOnRequest ? .granted : .denied); return grantOnRequest
    }
    func answerPrompt(_ ok: Bool) { lock.lock(); let w = permissionWaiter; permissionWaiter = nil; lock.unlock(); setPermission(ok ? .granted : .denied); w?.resume(returning: ok) }
    @MainActor var appIsActive: Bool { active }
    func setEventHandler(_ h: (@Sendable (VoiceBackendEvent) -> Void)?) { lock.lock(); handler = h; lock.unlock() }
    func send(_ e: VoiceBackendEvent) { lock.lock(); let h = handler; lock.unlock(); h?(e) }
    func beginSession() throws { note("beginSession"); lock.lock(); _sessionActive = true; lock.unlock() }
    func endSession() { lock.lock(); let was = _sessionActive; _sessionActive = false; lock.unlock(); if was { note("endSession") } }
    func startIO(noiseSuppression: Bool, capture: @escaping @Sendable (UnsafeBufferPointer<Float>) -> Void, render: @escaping @Sendable (UnsafeMutablePointer<Float>, Int) -> Void) throws {
        note("startIO")
        if let startError { throw startError }
        lock.lock(); _capture = capture; _render = render; lastNoiseSuppression = noiseSuppression; lock.unlock()
    }
    func stopIO() { lock.lock(); let was = _capture != nil; _capture = nil; _render = nil; lock.unlock(); if was { note("stopIO") } }
    var inputRouteName: String { "Fake Mic" }
    var outputRouteName: String { "Fake TV" }

    /// The mic delivers `samples` (in uneven chunks, like a real tap).
    func feed(_ samples: [Float]) {
        lock.lock(); let c = _capture; lock.unlock()
        guard let c else { return }
        var i = 0
        while i < samples.count { let n = min(samples.count - i, 441 + i % 300); samples[i..<i + n].withUnsafeBufferPointer { c($0) }; i += n }
    }
    /// The speaker pulls `frames` samples.
    func pull(_ frames: Int) -> [Float] {
        lock.lock(); let r = _render; lock.unlock()
        var out = [Float](repeating: 0, count: frames)
        guard let r else { return out }
        out.withUnsafeMutableBufferPointer { r($0.baseAddress!, frames) }
        return out
    }
}

@MainActor
final class PartyVoiceEngineTests: XCTestCase {
    private var nonSilent: (Float) -> Bool { { abs($0) > 1e-4 } }
    private final class Box: @unchecked Sendable {
        private let lock = NSLock(); private var items = [Data]()
        func add(_ d: Data) { lock.lock(); items.append(d); lock.unlock() }
        var all: [Data] { lock.lock(); defer { lock.unlock() }; return items }
        func clear() { lock.lock(); items.removeAll(); lock.unlock() }
        var parsed: [VoicePacket] { all.compactMap(VoicePacket.parse) }
    }
    private func makeEngine(codec: VoiceCodecID = .pcm16) -> (PartyVoiceEngine, FakeVoiceBackend, Box) {
        let b = FakeVoiceBackend(), e = PartyVoiceEngine(backend: b, codec: codec), box = Box()
        e.onPacket = { box.add($0) }
        return (e, b, box)
    }
    private func live(_ mode: VoiceMode, codec: VoiceCodecID = .pcm16) async -> (PartyVoiceEngine, FakeVoiceBackend, Box) {
        let (e, b, box) = makeEngine(codec: codec)
        await e.start(VoiceSettings(mode: mode))
        XCTAssertEqual(e.state, .live)
        return (e, b, box)
    }
    private func feedFrames(_ e: PartyVoiceEngine, _ b: FakeVoiceBackend, _ samples: [Float]) async { b.feed(samples); await e.settle() }
    private let silence1s = [Float](repeating: 0, count: 16_000)

    // MARK: lifecycle

    func testNothingTouchesTheHardwareUntilStartAndAfterStop() async {
        let (e, b, _) = makeEngine()
        XCTAssertEqual(e.state, .off); XCTAssertEqual(b.log, [])
        e.stop(); e.setPushToTalk(true); e.setMuted(true); e.update(VoiceSettings(mode: .openMic)); e.receive(Data([1, 2, 3]), from: "x")
        XCTAssertEqual(b.log, [], "stop/controls/update/receive while off never reach the backend")
        await e.start(VoiceSettings(mode: .pushToTalk))
        XCTAssertEqual(e.state, .live); XCTAssertTrue(b.sessionActive); XCTAssertTrue(b.ioRunning)
        e.stop()
        XCTAssertEqual(e.state, .off); XCTAssertFalse(b.sessionActive); XCTAssertFalse(b.ioRunning)
        let after = b.log
        e.stop(); e.stop()
        XCTAssertEqual(b.log, after, "stop() is idempotent and silent")
        XCTAssertEqual(after, ["beginSession", "startIO", "stopIO", "endSession"])
    }
    func testModeOffNeverStarts() async {
        let (e, b, _) = makeEngine()
        await e.start(VoiceSettings(mode: .off))
        XCTAssertEqual(e.state, .off); XCTAssertEqual(b.log, [])
    }
    func testDeniedMicrophoneFailsWithoutTouchingTheSession() async {
        let (e, b, _) = makeEngine(); b.grantOnRequest = false; b.setPermission(.undetermined)
        await e.start(VoiceSettings(mode: .pushToTalk))
        guard case .failed(let why) = e.state else { return XCTFail("\(e.state)") }
        XCTAssertTrue(why.lowercased().contains("microphone"))
        XCTAssertEqual(e.permission, .denied); XCTAssertEqual(b.count("beginSession"), 0)
        await e.start(VoiceSettings(mode: .pushToTalk))   // retry after granting in Settings
        b.setPermission(.granted); b.grantOnRequest = true
        await e.start(VoiceSettings(mode: .pushToTalk))
        XCTAssertEqual(e.state, .live)
    }
    func testStopWhileThePermissionPromptIsUpLeavesTheEngineOff() async {
        let (e, b, _) = makeEngine(); b.setPermission(.undetermined); b.holdPermissionPrompt = true
        let starting = Task { @MainActor in await e.start(VoiceSettings(mode: .openMic)) }
        for _ in 0..<20 where e.state != .starting { await Task.yield() }
        XCTAssertEqual(e.state, .starting)
        e.stop(); b.answerPrompt(true)
        await starting.value
        XCTAssertEqual(e.state, .off); XCTAssertEqual(b.count("beginSession"), 0)
    }
    func testStartFailureReportsAndReleasesTheSession() async {
        let (e, b, _) = makeEngine(); b.startError = NSError(domain: "x", code: 1, userInfo: [NSLocalizedDescriptionKey: "boom"])
        await e.start(VoiceSettings(mode: .openMic))
        guard case .failed(let why) = e.state else { return XCTFail("\(e.state)") }
        XCTAssertTrue(why.contains("boom")); XCTAssertFalse(b.sessionActive, "the category is restored even when the engine would not start"); XCTAssertFalse(e.isTransmitting)
    }
    func testStartTwiceAppliesSettingsInsteadOfRestarting() async {
        let (e, b, _) = await live(.pushToTalk)
        await e.start(VoiceSettings(mode: .openMic, inputGain: 1.5))
        XCTAssertEqual(b.count("beginSession"), 1); XCTAssertEqual(e.settings.mode, .openMic); XCTAssertEqual(e.settings.inputGain, 1.5)
        e.update(VoiceSettings(mode: .off)); XCTAssertEqual(e.state, .off, "switching the mode to off turns voice off")
    }
    func testRoutesAndCodecAreExposed() async {
        let (e, _, _) = await live(.pushToTalk, codec: .iLBC)
        XCTAssertEqual(e.inputRoute, "Fake Mic"); XCTAssertEqual(e.outputRoute, "Fake TV"); XCTAssertEqual(e.codec, .iLBC)
        e.stop(); XCTAssertEqual(e.inputRoute, ""); XCTAssertEqual(e.micLevel, 0)
    }
    func testNoiseSuppressionIsPassedAndToggleRestartsIO() async {
        let (e, b, _) = makeEngine()
        await e.start(VoiceSettings(mode: .openMic, noiseSuppression: true))
        XCTAssertEqual(b.lastNoiseSuppression, true)
        e.update(VoiceSettings(mode: .openMic, noiseSuppression: false))
        XCTAssertEqual(b.lastNoiseSuppression, false); XCTAssertEqual(b.count("startIO"), 2); XCTAssertEqual(e.state, .live)
    }

    // MARK: transmit gating

    func testPushToTalkSendsOnlyWhileTheButtonIsDown() async {
        let (e, b, box) = await live(.pushToTalk)
        await feedFrames(e, b, speech(1))
        XCTAssertTrue(box.all.isEmpty, "mic is hot but the gate is shut: nothing leaves the phone"); XCTAssertFalse(e.isTransmitting)
        e.setPushToTalk(true)
        await feedFrames(e, b, speech(1))
        XCTAssertTrue(e.isTransmitting); XCTAssertGreaterThan(box.all.count, 40)
        XCTAssertTrue(box.parsed.allSatisfy { $0.codec == .pcm16 && !$0.isSpeechEnd })
        e.setPushToTalk(false)
        await feedFrames(e, b, speech(0.2))
        XCTAssertFalse(e.isTransmitting)
        XCTAssertTrue(box.parsed.last?.isSpeechEnd ?? false, "release ends with the speech-end marker")
        let n = box.all.count
        await feedFrames(e, b, speech(1)); XCTAssertEqual(box.all.count, n, "nothing after release")
    }
    func testPushToTalkSendsNothingRecordedBeforeThePress() async {
        let (e, b, box) = await live(.pushToTalk)
        await feedFrames(e, b, speech(0.5)); e.setPushToTalk(true)
        await feedFrames(e, b, [Float](repeating: 0, count: 320))
        let first = box.parsed.first
        XCTAssertNotNil(first); XCTAssertEqual(first?.payload.count, 160, "first packet is the first frame after the press, no pre-roll")
    }
    func testOpenMicSilenceSendsNothingSpeechOpensAndHangoverCloses() async {
        let (e, b, box) = await live(.openMic)
        await feedFrames(e, b, silence1s); await feedFrames(e, b, VoiceTestSignal.noise(seconds: 1, rms: 0.003))
        XCTAssertTrue(box.all.isEmpty); XCTAssertFalse(e.isTransmitting)
        await feedFrames(e, b, speech(1.2))
        XCTAssertTrue(e.isTransmitting); XCTAssertGreaterThan(box.all.count, 20)
        await feedFrames(e, b, VoiceTestSignal.noise(seconds: 1, rms: 0.003))
        XCTAssertFalse(e.isTransmitting); XCTAssertTrue(box.parsed.contains { $0.isSpeechEnd })
        let n = box.all.count
        await feedFrames(e, b, silence1s); XCTAssertEqual(box.all.count, n)
    }
    func testKillSwitchStopsTransmissionImmediately() async {
        let (e, b, box) = await live(.pushToTalk); e.setPushToTalk(true)
        await feedFrames(e, b, speech(0.5)); XCTAssertTrue(e.isTransmitting)
        e.canTransmit = false
        XCTAssertFalse(e.isTransmitting, "the indicator drops synchronously, before any audio frame runs")
        let n = box.all.count
        await feedFrames(e, b, speech(1)); XCTAssertEqual(box.all.count - n, 1, "only the closing marker")
        XCTAssertTrue(box.parsed.last?.isSpeechEnd ?? false); XCTAssertFalse(e.isTransmitting)
        e.canTransmit = true
        await feedFrames(e, b, speech(0.5)); XCTAssertTrue(e.isTransmitting, "ptt is still down: allowed again")
    }
    func testSelfMuteGatesBothModes() async {
        let (e, b, box) = await live(.openMic); e.setMuted(true)
        await feedFrames(e, b, speech(1)); XCTAssertTrue(box.all.isEmpty); XCTAssertFalse(e.isTransmitting)
        e.setMuted(false); await feedFrames(e, b, speech(1)); XCTAssertFalse(box.all.isEmpty)
    }
    func testInputGainScalesWhatIsSent() async {
        let (e, b, box) = await live(.pushToTalk); e.setPushToTalk(true)
        var s = VoiceSettings(mode: .pushToTalk, inputGain: 0); e.update(s)
        await feedFrames(e, b, speech(0.5))
        let dec = VoiceStreamDecoder()
        XCTAssertTrue(box.parsed.compactMap { dec.decode($0) }.joined().allSatisfy { abs($0) < 0.01 }, "gain 0 sends silence")
        box.clear(); s.inputGain = 1; e.update(s)
        await feedFrames(e, b, speech(0.5))
        XCTAssertTrue(box.parsed.compactMap { dec.decode($0) }.joined().contains { abs($0) > 0.1 })
    }
    func testMicLevelMeterMovesWhileLive() async {
        let (e, b, _) = await live(.pushToTalk)
        await feedFrames(e, b, speech(1)); try? await Task.sleep(for: .milliseconds(250))
        XCTAssertGreaterThan(e.micLevel, 0, "the meter shows a working mic even with the gate shut")
    }
    func testSequenceNumbersContinueAcrossAPause() async {
        let (e, b, box) = await live(.pushToTalk); e.setPushToTalk(true)
        await feedFrames(e, b, speech(0.3)); let before = box.parsed.last!.sequence
        e.handle(.appBackgrounded); e.handle(.appForegrounded); await e.settle()
        await feedFrames(e, b, speech(0.3))
        let seqs = box.parsed.map(\.sequence)
        XCTAssertTrue(zip(seqs, seqs.dropFirst()).allSatisfy { $1 == $0 &+ 1 }, "one unbroken numbering (peers would drop a restart at 0 as late)")
        XCTAssertGreaterThan(seqs.count, Int(before &- seqs[0]) + 2)
    }

    // MARK: receive and mix

    private func loopback(codec: VoiceCodecID) async -> (heard: [Float], sent: [Float]) {
        let (a, ba, boxA) = await live(.pushToTalk, codec: codec), (c, bc, _) = makeEngine(codec: codec)
        await c.start(VoiceSettings(mode: .pushToTalk))
        a.setPushToTalk(true)
        let sig = speech(1.5), chunk = 640
        var heard = [Float](), delivered = 0
        for i in stride(from: 0, to: sig.count - chunk, by: chunk) {
            ba.feed(Array(sig[i..<i + chunk])); await a.settle()
            for d in boxA.all.dropFirst(delivered) { c.receive(d, from: "ann") }
            delivered = boxA.all.count
            await c.settle()
            heard += bc.pull(chunk)
        }
        return (heard, sig)
    }
    func testLoopbackBetweenTwoEnginesEveryCodec() async {
        for id in VoiceCodecFactory.available {
            let r = await loopback(codec: id)
            try? await Task.sleep(for: .milliseconds(150))
            let snr = VoiceMetrics.bestLagSNR(reference: r.sent, test: r.heard, maxLag: 4000)
            XCTAssertGreaterThan(snr, VoiceCodecFactory.snrFloor(id), "\(id): what C hears matches what A said (SNR \(snr) dB)")
        }
    }
    func testSpeakingSetNamesThePeerThenClears() async {
        let (e, b, _) = makeEngine(codec: .pcm16); await e.start(VoiceSettings(mode: .pushToTalk))
        let enc = VoiceStreamEncoder(codec: VoiceCodecFactory.make(.pcm16)!)
        let packets = enc.encode(speech(1.5)).map { $0.serialized() }
        var sawSpeaking = false
        for batch in stride(from: 0, to: packets.count, by: 40) {
            for p in packets[batch..<min(batch + 40, packets.count)] { e.receive(p, from: "bea") }
            await e.settle()
            for _ in 0..<8 { _ = b.pull(320) }
            try? await Task.sleep(for: .milliseconds(120))   // the 10 Hz poll publishes `speaking`
            if e.speaking.contains("bea") { sawSpeaking = true }
        }
        XCTAssertTrue(sawSpeaking)
        for _ in 0..<40 { _ = b.pull(320) }
        try? await Task.sleep(for: .milliseconds(250))
        XCTAssertFalse(e.speaking.contains("bea"), "no audio for 400 ms: not speaking")
    }
    func testMutedPeerIsDroppedAndPerPeerVolumeApplies() async {
        let (e, b, _) = makeEngine(codec: .pcm16); await e.start(VoiceSettings(mode: .pushToTalk))
        func play(_ peer: String) async -> Float {
            let enc = VoiceStreamEncoder(codec: VoiceCodecFactory.make(.pcm16)!)
            let ps = enc.encode(speech(0.8)).map { $0.serialized() }
            var peak: Float = 0
            for p in ps { e.receive(p, from: peer) }
            await e.settle()
            for _ in 0..<60 { peak = max(peak, b.pull(320).map(abs).max() ?? 0) }
            return peak
        }
        e.setPeer("ann", volume: 1); let normal = await play("ann")
        XCTAssertGreaterThan(normal, 0.05)
        e.removePeer("ann"); e.setPeer("ann", volume: 0.25); let quiet = await play("ann")
        XCTAssertLessThan(quiet, normal * 0.5); XCTAssertGreaterThan(quiet, 0)
        e.removePeer("ann"); e.setPeer("ann", muted: true); let muted = await play("ann")
        XCTAssertEqual(muted, 0, "muted peer is silent"); XCTAssertTrue(e.settings.mutedPeers.contains("ann"))
        e.setPeer("ann", muted: false); e.removePeer("ann")
        XCTAssertFalse(e.settings.mutedPeers.contains("ann"))
    }
    func testOutputVolumeZeroIsSilent() async {
        let (e, b, _) = makeEngine(codec: .pcm16); await e.start(VoiceSettings(mode: .pushToTalk, outputVolume: 0))
        for p in VoiceStreamEncoder(codec: VoiceCodecFactory.make(.pcm16)!).encode(speech(0.5)) { e.receive(p.serialized(), from: "ann") }
        await e.settle()
        XCTAssertTrue((0..<30).allSatisfy { _ in b.pull(320).allSatisfy { $0 == 0 } })
    }
    func testGarbageAndFloodingAreDroppedWithoutHarm() async {
        let (e, b, _) = makeEngine(codec: .pcm16); await e.start(VoiceSettings(mode: .pushToTalk))
        var s: UInt64 = 7
        for _ in 0..<3000 { s = s &* 6364136223846793005 &+ 1442695040888963407; e.receive(Data((0..<Int(s >> 56)).map { UInt8(truncatingIfNeeded: s >> UInt64($0 % 40)) }), from: "mallory") }
        for k in 0..<5000 { e.receive(VoicePacket(codec: .pcm16, sequence: UInt16(k & 0xffff), timestampMs: UInt32(k * 10), payload: Data(repeating: 130, count: 160)).serialized(), from: "flood") }
        for i in 0..<100 { e.receive(Data([1]), from: "made-up-\(i)") }
        await e.settle()
        _ = b.pull(320)
        XCTAssertEqual(e.state, .live)
    }
    func testPacketsAreIgnoredWhenNotLive() async {
        let (e, b, _) = makeEngine(codec: .pcm16)
        e.receive(VoicePacket(codec: .pcm16, sequence: 0, timestampMs: 0, payload: Data(repeating: 130, count: 160)).serialized(), from: "ann")
        XCTAssertEqual(b.log, [])
    }

    // MARK: system events

    func testInterruptionPausesAndResumes() async {
        let (e, b, _) = await live(.pushToTalk); e.setPushToTalk(true)
        await feedFrames(e, b, speech(0.3)); XCTAssertTrue(e.isTransmitting)
        e.handle(.interruptionBegan)
        XCTAssertEqual(e.state, .paused(.interruption)); XCTAssertFalse(e.isTransmitting); XCTAssertFalse(b.ioRunning)
        e.handle(.interruptionEnded(shouldResume: true))
        XCTAssertEqual(e.state, .live); XCTAssertTrue(b.ioRunning); XCTAssertEqual(b.count("startIO"), 2)
    }
    func testInterruptionWithoutResumeWaitsForTheApp() async {
        let (e, b, _) = await live(.openMic)
        e.handle(.interruptionBegan); e.handle(.interruptionEnded(shouldResume: false))
        XCTAssertEqual(e.state, .paused(.interruption)); XCTAssertFalse(b.ioRunning)
        e.resumeIfPaused(); XCTAssertEqual(e.state, .live)
    }
    func testBackgroundReleasesTheMicAndTheSessionThenResumes() async {
        let (e, b, _) = await live(.openMic)
        e.handle(.appBackgrounded)
        XCTAssertEqual(e.state, .paused(.background)); XCTAssertFalse(b.ioRunning); XCTAssertFalse(b.sessionActive, "no background audio mode: the session goes back to Unity")
        e.handle(.appForegrounded)
        XCTAssertEqual(e.state, .live); XCTAssertTrue(b.ioRunning); XCTAssertTrue(b.sessionActive)
    }
    func testStopWhilePausedStaysOffEvenIfTheAppReturns() async {
        let (e, b, _) = await live(.openMic)
        e.handle(.appBackgrounded); e.stop(); e.handle(.appForegrounded)
        XCTAssertEqual(e.state, .off); XCTAssertFalse(b.ioRunning)
    }
    func testStartingInTheBackgroundWaitsForForeground() async {
        let (e, b, _) = makeEngine(); b.active = false
        await e.start(VoiceSettings(mode: .openMic))
        XCTAssertEqual(e.state, .paused(.background)); XCTAssertEqual(b.count("beginSession"), 0)
        b.active = true; e.handle(.appForegrounded)
        XCTAssertEqual(e.state, .live)
    }
    func testConfigurationChangeAndMediaServicesResetRebuildTheGraph() async {
        let (e, b, _) = await live(.pushToTalk)
        e.handle(.configurationChanged); XCTAssertEqual(e.state, .live); XCTAssertEqual(b.count("startIO"), 2)
        e.handle(.mediaServicesReset); XCTAssertEqual(e.state, .live); XCTAssertEqual(b.count("startIO"), 3)
        e.handle(.routeChanged); XCTAssertEqual(b.count("startIO"), 3, "a route change alone only refreshes the names")
    }
    func testRebuildFailureEndsInFailedAndReleasesTheSession() async {
        let (e, b, _) = await live(.pushToTalk)
        b.startError = NSError(domain: "x", code: 2, userInfo: [NSLocalizedDescriptionKey: "gone"])
        e.handle(.configurationChanged)
        guard case .failed = e.state else { return XCTFail("\(e.state)") }
        XCTAssertFalse(b.sessionActive)
    }
    func testBackendEventsReachTheEngineThroughTheHandler() async {
        let (e, b, _) = await live(.openMic)
        b.send(.appBackgrounded)
        for _ in 0..<50 where e.state == .live { try? await Task.sleep(for: .milliseconds(5)) }
        XCTAssertEqual(e.state, .paused(.background))
    }

    // MARK: recording sink

    func testCaptureSinkGetsMyMicOnlyWhileTransmittingAndRemoteVoiceFromTheMixer() async {
        let (e, b, _) = await live(.pushToTalk)
        final class Tap: @unchecked Sendable {
            private let lock = NSLock(); private var _mine = [Float](), _remote = [Float](), _rates = Set<Int>()
            func add(_ p: UnsafePointer<Float>, _ n: Int, _ r: Int, _ src: Int) {
                let buf = Array(UnsafeBufferPointer(start: p, count: n))
                lock.withLock { _rates.insert(r); if src == 1 { _mine += buf } else if src == 0 { _remote += buf } }
            }
            var mine: [Float] { lock.withLock { _mine } }
            var remote: [Float] { lock.withLock { _remote } }
            var rates: Set<Int> { lock.withLock { _rates } }
        }
        let tap = Tap()
        e.captureSink = { tap.add($0, $1, $2, $3) }
        await feedFrames(e, b, speech(0.5))                       // gate shut: silence frames
        XCTAssertTrue(tap.mine.allSatisfy { $0 == 0 }); XCTAssertGreaterThan(tap.mine.count, 0, "silence is delivered while not transmitting so the recording stays aligned")
        e.setPushToTalk(true); await feedFrames(e, b, speech(0.5))
        XCTAssertTrue(tap.mine.contains { abs($0) > 0.05 })
        for p in VoiceStreamEncoder(codec: VoiceCodecFactory.make(.pcm16)!).encode(speech(0.6)) { e.receive(p.serialized(), from: "ann") }
        await e.settle()
        for _ in 0..<40 { _ = b.pull(320) }
        try? await Task.sleep(for: .milliseconds(120))
        XCTAssertTrue(tap.remote.contains { abs($0) > 0.05 }, "source 0 carries the mixed remote voice"); XCTAssertEqual(tap.rates, [16_000])
        e.captureSink = nil
        let before = tap.mine.count; await feedFrames(e, b, speech(0.3)); XCTAssertEqual(tap.mine.count, before, "removing the sink stops the feed")
    }

    func testDroppingAnEngineWhileLiveReleasesTheHardware() async {
        let b = FakeVoiceBackend()
        do {
            let e = PartyVoiceEngine(backend: b, codec: .pcm16)
            await e.start(VoiceSettings(mode: .openMic)); XCTAssertTrue(b.ioRunning)
        }
        XCTAssertFalse(b.ioRunning); XCTAssertFalse(b.sessionActive)
    }
}
#endif
