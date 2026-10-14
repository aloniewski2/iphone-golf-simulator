import SwiftUI
import XCTest
@testable import GolfArcade

/// The party chat, the safety rules Apple expects around it (filter, report, block, mute, age and parental limits) and one-tap
/// recording with the party told. In-memory fakes stand in for the lobby, the native TV recorder and Photos; the real recorder and
/// microphone are device gates (proof/party/GATE_RESULTS.md).
@MainActor
final class PartyChatTests: XCTestCase {
    // MARK: fakes
    @MainActor final class Channel: PartyChannel {
        let localID: String, name: String
        var inParty = true
        var onParty: ((String, String, String) -> Void)?
        weak var peer: Channel?
        var peers: [Channel] = []
        var sent: [(kind: String, payload: String)] = []
        init(_ id: String, _ name: String) { localID = id; self.name = name }
        var members: [(id: String, name: String)] { peers.map { ($0.localID, $0.name) } }
        func send(_ kind: String, payload: String, reliable: Bool) {
            sent.append((kind, payload))
            for p in peers { p.onParty?(kind, payload, localID) }
        }
    }
    @MainActor final class Phone {
        let channel: Channel, safety: PartySafety, chat: PartyChat, defaults: UserDefaults
        var clock = Date(timeIntervalSince1970: 1_800_000_000)
        init(_ id: String, _ name: String, adult: Bool = true) {
            let suite = "party-test-\(UUID().uuidString)"
            defaults = UserDefaults(suiteName: suite)!
            channel = Channel(id, name)
            safety = PartySafety(defaults: defaults, restrictions: { GameCenterRestrictions() })
            chat = PartyChat(channel: channel, safety: safety, defaults: defaults)
            chat.localName = { name }
            if adult { safety.setBirthYear(1990); safety.acceptRules() }
            chat.now = { [unowned self] in clock }
        }
        func tick(_ s: Double = 2) { clock = clock.addingTimeInterval(s) }
    }
    private func party(_ n: Int = 2) -> [Phone] {
        let names = ["Ann", "Bea", "Cy", "Dee"]
        let phones = (0..<n).map { Phone("p\($0)", names[$0]) }
        for p in phones { p.channel.peers = phones.filter { $0 !== p }.map(\.channel) }
        return phones
    }

    // MARK: filter

    func testFilterMasksLanguageAndDisguises() {
        for bad in ["fuck", "F.U.C.K", "fuuuuck you", "sh1t", "$h!t", "b i t c h".replacingOccurrences(of: " ", with: ""), "asshole", "Kill yourself"] {
            let r = ChatFilter.clean(bad, mask: true)
            XCTAssertTrue(r.flagged, "\(bad) should be masked"); XCTAssertTrue(r.text.contains("*"), r.text)
        }
        let line = ChatFilter.clean("you are a total fuck up lol", mask: true)
        XCTAssertEqual(line.text, "you are a total **** up lol", "only the word is masked")
    }
    func testFilterLeavesRealWordsAlone() {
        for ok in ["Nice shot! Class act.", "I assess the cocktail", "Scunthorpe grape drape", "great match, well played", "Cumberland classic", "titanic", "Hello! 6-4 3-6 7-5"] {
            let r = ChatFilter.clean(ok, mask: true)
            XCTAssertEqual(r.text, ok, "\(ok) is fine"); XCTAssertFalse(r.flagged)
        }
    }
    func testFilterOffStillHidesContactDetails() {
        XCTAssertEqual(ChatFilter.clean("add me john@example.com", mask: false).text, "add me [hidden]")
        XCTAssertTrue(ChatFilter.clean("call 908 590 9023 now", mask: false).text.contains("[hidden]"))
        XCTAssertTrue(ChatFilter.clean("go to www.evil.com", mask: false).text.contains("[hidden]"))
        XCTAssertTrue(ChatFilter.clean("join discord.gg/abc", mask: false).text.contains("[hidden]"))
        XCTAssertEqual(ChatFilter.clean("fuck", mask: false).text, "fuck", "with the filter off language is untouched (adults only)")
    }
    func testSanitizeStripsHiddenControlsAndCapsLength() {
        XCTAssertEqual(ChatFilter.sanitize("a\u{202E}b\u{200B}c\u{0000}d"), "abcd", "bidi, zero-width and control characters")
        XCTAssertEqual(ChatFilter.sanitize("  too   many \n spaces "), "too many spaces")
        XCTAssertEqual(ChatFilter.sanitize(String(repeating: "x", count: 500)).count, ChatFilter.maxLength)
    }

    // MARK: who may type and talk

    func testAccessRules() {
        var gc = GameCenterRestrictions()
        let defaults = UserDefaults(suiteName: "party-access-\(UUID().uuidString)")!
        let safety = PartySafety(defaults: defaults, restrictions: { gc })
        safety.refresh()
        XCTAssertEqual(safety.ageBand, .unknown)
        XCTAssertFalse(safety.access.canType, "no age yet: quick phrases only"); XCTAssertNotNil(safety.access.reason)
        safety.setBirthYear(1990); XCTAssertEqual(safety.ageBand, .adult)
        XCTAssertFalse(safety.access.canType, "rules not accepted yet")
        safety.acceptRules(); XCTAssertEqual(safety.access, .full); XCTAssertTrue(safety.access.canTalk)
        gc.communicationRestricted = true; safety.refresh(); XCTAssertEqual(safety.access, .quickOnly("Your Game Center settings limit chat to quick phrases."))
        gc = GameCenterRestrictions(underage: true); safety.refresh(); XCTAssertFalse(safety.access.canType); XCTAssertTrue(safety.filterOn && safety.filterLocked)
        gc = GameCenterRestrictions(multiplayerRestricted: true); safety.refresh()
        if case .blocked = safety.access {} else { XCTFail("multiplayer off: no party features") }
    }
    func testUnder13GetsQuickPhrasesOnlyAndCannotRetypeTheYear() {
        let safety = PartySafety(defaults: UserDefaults(suiteName: "party-kid-\(UUID().uuidString)")!, restrictions: { GameCenterRestrictions() })
        var clock = Date(timeIntervalSince1970: 1_800_000_000); safety.now = { clock }
        safety.setBirthYear(safety.year() - 10)
        XCTAssertEqual(safety.ageBand, .under13); XCTAssertFalse(safety.access.canType); XCTAssertFalse(safety.access.canTalk)
        XCTAssertFalse(safety.setBirthYear(1990), "locked: no instant retry with a higher age"); XCTAssertEqual(safety.ageBand, .under13)
        clock = clock.addingTimeInterval(8 * 86_400)
        XCTAssertTrue(safety.setBirthYear(1990)); XCTAssertEqual(safety.ageBand, .adult)
    }
    func testTeensAlwaysHaveTheFilterOn() {
        let safety = PartySafety(defaults: UserDefaults(suiteName: "party-teen-\(UUID().uuidString)")!, restrictions: { GameCenterRestrictions() })
        safety.setBirthYear(safety.year() - 15); safety.acceptRules(); safety.filterProfanity = false
        XCTAssertEqual(safety.ageBand, .teen); XCTAssertTrue(safety.filterOn && safety.filterLocked); XCTAssertEqual(safety.access, .full)
        XCTAssertFalse(safety.setBirthYear(3000), "an impossible year is refused")
    }

    // MARK: chat

    func testQuickChatReachesTheWholePartyAndOnlyIt() {
        let p = party(3)
        let outsider = Phone("x", "Eve"); outsider.channel.peers = []
        XCTAssertEqual(p[0].chat.sendQuick("gg"), .sent)
        for other in p.dropFirst() { XCTAssertEqual(other.chat.messages.map(\.text), ["Good game!"]); XCTAssertEqual(other.chat.messages.first?.senderName, "Ann") }
        XCTAssertTrue(outsider.chat.messages.isEmpty, "nobody outside the party hears it")
        XCTAssertEqual(p[0].chat.messages.first?.mine, true)
    }
    func testTypedChatFiltersAndRespectsEachSidesAccess() {
        let p = party(2)
        let kid = p[1]; kid.safety.debugReset(); kid.safety.setBirthYear(kid.safety.year() - 9)
        XCTAssertEqual(p[0].chat.send(text: "hello there"), .sent)
        XCTAssertTrue(kid.chat.messages.isEmpty, "a child never receives typed text from others")
        XCTAssertEqual(kid.chat.send(text: "hi"), .notAllowed("Typing and voice chat are for ages 13 and up. Quick phrases are always on."))
        XCTAssertEqual(kid.chat.sendQuick("wow"), .sent); XCTAssertEqual(p[0].chat.messages.last?.text, "Wow!")
        // an adult with the filter on masks what arrives
        let c = party(2)
        c[0].safety.filterProfanity = false
        c[0].chat.send(text: "this is shit")
        XCTAssertEqual(c[1].chat.messages.last?.text, "this is ****"); XCTAssertEqual(c[1].chat.messages.last?.filtered, true)
        XCTAssertEqual(c[0].chat.messages.last?.text, "this is shit", "the sender with the filter off sees their own words")
        c[1].chat.incomingTextVisible = false; c[0].tick(); c[0].chat.send(text: "again")
        XCTAssertEqual(c[1].chat.messages.count, 1, "typed chat turned off for this player")
    }
    func testNotAllowedBeforeSetupAndRateLimit() {
        let a = Phone("a", "Ann", adult: false); let b = Phone("b", "Bea")
        a.channel.peers = [b.channel]; b.channel.peers = [a.channel]
        XCTAssertEqual(a.chat.send(text: "hi"), .notAllowed("Tell us your age to unlock typing and voice. Quick phrases are always on."))
        let p = party(2)
        XCTAssertEqual(p[0].chat.send(text: "1"), .sent)
        XCTAssertEqual(p[0].chat.send(text: "2"), .tooFast, "one a second")
        for i in 0..<6 { p[0].tick(1.1); _ = p[0].chat.send(text: "m\(i)") }
        XCTAssertEqual(p[0].chat.send(text: "x"), .tooFast, "six in ten seconds at most")
        XCTAssertEqual(p[0].chat.send(text: "   "), .empty)
    }
    func testAFloodingSenderIsDroppedOnTheReceiver() {
        let p = party(2)
        for i in 0..<12 { p[0].channel.send("partyChat", payload: "{\"t\":\"m\",\"text\":\"spam \(i)\",\"name\":\"Ann\"}", reliable: true) }
        XCTAssertEqual(p[1].chat.messages.count, 5, "more than 5 in 5 seconds are dropped")
    }
    func testMuteAndBlockSilenceAPlayerEverywhere() {
        let p = party(3)
        p[0].chat.send(text: "first"); p[1].chat.sendQuick("gg")
        XCTAssertEqual(p[2].chat.messages.count, 2)
        p[2].chat.block("p0")
        XCTAssertEqual(p[2].chat.messages.map(\.senderID), ["p1"], "their old messages are removed")
        XCTAssertTrue(p[2].safety.isBlocked("p0")); XCTAssertTrue(p[2].chat.isMuted("p0")); XCTAssertEqual(p[2].chat.members.map(\.id), ["p1"])
        p[0].tick(); p[0].chat.send(text: "still here?")
        XCTAssertEqual(p[2].chat.messages.map(\.senderID), ["p1"], "a blocked player cannot reach you")
        p[2].chat.mute("p1"); p[1].tick(); p[1].chat.sendQuick("wow")
        XCTAssertEqual(p[2].chat.messages.count, 1, "muted: dropped"); p[2].chat.unmute("p1"); p[1].tick(2); p[1].chat.sendQuick("ok".isEmpty ? "gg" : "gg")
        XCTAssertEqual(p[2].chat.messages.count, 2)
    }
    func testOnlyRealPartyMembersCanSpeakInThisParty() {
        let p = party(2)
        p[1].chat.receive("partyChat", "{\"t\":\"q\",\"id\":\"gg\",\"name\":\"Mallory\"}", from: "stranger")
        XCTAssertTrue(p[1].chat.messages.isEmpty, "a sender who is not in the lobby is ignored")
        p[1].chat.receive("partyChat", "not json", from: "p0"); p[1].chat.receive("partyChat", "{\"t\":\"q\",\"id\":\"nope\"}", from: "p0")
        XCTAssertTrue(p[1].chat.messages.isEmpty, "garbage and unknown phrases are ignored")
        p[1].chat.receive("partyChat", "{\"t\":\"q\",\"id\":\"gg\",\"name\":\"A\u{202E}nn\"}", from: "p0")
        XCTAssertEqual(p[1].chat.messages.last?.senderName, "Ann")
    }
    func testUnreadCountsUntilTheChatIsOpen() {
        let p = party(2)
        p[0].chat.sendQuick("go"); XCTAssertEqual(p[1].chat.unread, 1)
        p[1].chat.chatOpen = true; XCTAssertEqual(p[1].chat.unread, 0)
        p[0].tick(); p[0].chat.sendQuick("gg"); XCTAssertEqual(p[1].chat.unread, 0, "read as it arrives")
    }
    func testReportCarriesTheirLastMessagesAndMutesThem() {
        let p = party(2)
        for i in 0..<7 { p[0].tick(); p[1].tick(); p[0].chat.send(text: "line \(i)") }
        let r = p[1].chat.report("p0", reason: .harassment, voice: false)
        XCTAssertEqual(r.lines, ["line 2", "line 3", "line 4", "line 5", "line 6"]); XCTAssertEqual(r.reportedName, "Ann")
        XCTAssertTrue(r.body.contains("Harassment or bullying") && r.body.contains("line 6") && r.body.contains("Bea"))
        XCTAssertTrue(p[1].chat.isMuted("p0"), "reporting mutes right away")
        let url = PartyContact.reportURL(r)
        XCTAssertNotNil(url); XCTAssertTrue(url!.absoluteString.hasPrefix("sms:") || url!.absoluteString.hasPrefix("mailto:"))
        let v = p[1].chat.report("p0", reason: .language, voice: true, alsoMute: false)
        XCTAssertTrue(v.body.contains("voice chat")); XCTAssertTrue(v.body.contains("not recorded by the app"))
    }
    func testChatEndsWithTheParty() {
        let p = party(2)
        p[0].chat.sendQuick("gg"); XCTAssertEqual(p[1].chat.messages.count, 1)
        p[1].channel.inParty = false; p[1].channel.peers = []; p[1].chat.membersChanged()
        XCTAssertTrue(p[1].chat.messages.isEmpty, "nothing is kept after the party")
        XCTAssertEqual(p[1].chat.send(text: "hi"), .noParty)
    }

    // MARK: recording

    @MainActor final class Runtime: CaptureRuntime {
        var available = true, active = false, accept = true
        var started: [String] = [], stops = 0
        var handler: (@MainActor (String) -> Void)?
        func start(_ json: String) -> Bool { started.append(json); if accept { active = true; handler?(#"{"type":"capture","state":"started"}"#) }; return accept }
        func stop() { stops += 1; active = false }
        func setHandler(_ h: (@MainActor (String) -> Void)?) { handler = h }
        func finish(_ path: String, reason: String = "user", seconds: Double = 75) {
            handler?("{\"type\":\"capture\",\"state\":\"finished\",\"url\":\"\(path)\",\"seconds\":\(seconds),\"reason\":\"\(reason)\"}")
        }
    }
    @MainActor final class Photos: PhotoSaving {
        var access = true, requests = 0, saved: [URL] = [], fail = false
        var hasAccess: Bool { access }
        func requestAccess() async -> Bool { requests += 1; return access }
        func save(_ url: URL) async throws { if fail { throw NSError(domain: "x", code: 1) }; saved.append(url) }
    }
    private func recorder(connected: Bool = true) -> (RecordingService, Runtime, Photos, Phone) {
        let phone = party(2)[0]
        let rt = Runtime(), ph = Photos()
        let svc = RecordingService(runtime: rt, photos: ph, chat: phone.chat, defaults: UserDefaults(suiteName: "rec-\(UUID().uuidString)")!)
        svc.displayConnected = { connected }; svc.freeBytes = { 50_000_000_000 }
        return (svc, rt, ph, phone)
    }
    private func tempVideo() -> URL {
        let dir = RecordingService.tempFolder; try? FileManager.default.createDirectory(at: dir, withIntermediateDirectories: true)
        let url = dir.appendingPathComponent("MotionClub-test-\(UUID().uuidString).mp4"); FileManager.default.createFile(atPath: url.path, contents: Data([0, 0, 0, 24]))
        return url
    }
    private func settle(_ svc: RecordingService, until: RecordingService.State) async {
        for _ in 0..<200 { if svc.state == until { return }; try? await Task.sleep(for: .milliseconds(10)) }
        XCTFail("state stayed \(svc.state), wanted \(until)")
    }

    func testOneTapRecordsTheTVAndSavesToPhotosOnStop() async {
        let (svc, rt, photos, phone) = recorder(); defer { try? FileManager.default.removeItem(at: RecordingService.tempFolder) }
        XCTAssertNil(svc.unavailableReason); XCTAssertTrue(svc.canToggle)
        svc.toggle(); await settle(svc, until: .recording)
        XCTAssertEqual(photos.requests, 1, "Photos access is asked up front, not when you stop")
        let cfg = (try? JSONSerialization.jsonObject(with: Data(rt.started[0].utf8))) as? [String: Any]
        XCTAssertEqual(cfg?["display"] as? Int, 1, "the TV (first non-main display), never the phone"); XCTAssertEqual(cfg?["width"] as? Int, 1920); XCTAssertEqual(cfg?["fps"] as? Int, 30)
        XCTAssertEqual(cfg?["voice"] as? Bool, true, "party voices are in the recording"); XCTAssertEqual(cfg?["mic"] as? Bool, true); XCTAssertEqual(cfg?["gameAudio"] as? Bool, true)
        XCTAssertTrue(phone.channel.sent.contains { $0.kind == "partyRec" && $0.payload.contains("true") }, "the party is told you are recording")
        XCTAssertTrue(svc.isRecording)
        svc.toggle(); XCTAssertEqual(svc.state, .saving); XCTAssertEqual(rt.stops, 1)
        XCTAssertTrue(phone.channel.sent.last?.payload.contains("false") == true, "and told when it stops")
        let file = tempVideo(); rt.finish(file.path)
        await settle(svc, until: .saved)
        XCTAssertEqual(photos.saved, [file]); XCTAssertFalse(FileManager.default.fileExists(atPath: file.path), "the temporary file is removed once it is in Photos")
        XCTAssertTrue(svc.message.contains("Saved to Photos") && svc.message.contains("1:15"), svc.message)
    }
    func testNoTVNoRecording() async {
        let (svc, rt, _, _) = recorder(connected: false)
        XCTAssertEqual(svc.unavailableReason, "Connect a TV to record. Recordings capture the TV picture, never this phone.")
        XCTAssertFalse(svc.canToggle); await svc.start(); XCTAssertEqual(svc.state, .failed); XCTAssertTrue(rt.started.isEmpty)
        let (svc2, rt2, _, _) = recorder(); rt2.available = false
        XCTAssertNotNil(svc2.unavailableReason); await svc2.start(); XCTAssertEqual(svc2.state, .failed)
    }
    func testLowStorageRefusesToStart() async {
        let (svc, rt, _, _) = recorder(); svc.freeBytes = { 300_000_000 }
        await svc.start(); XCTAssertEqual(svc.state, .failed); XCTAssertTrue(svc.message.contains("storage")); XCTAssertTrue(rt.started.isEmpty)
    }
    func testPhotosDeniedKeepsTheFileInTheApp() async {
        let (svc, rt, photos, _) = recorder(); photos.access = false
        defer { try? FileManager.default.removeItem(at: RecordingService.folder) }
        await svc.start(); XCTAssertEqual(svc.state, .recording); XCTAssertTrue(svc.message.contains("stay in the app"))
        let file = tempVideo(); svc.stop(); rt.finish(file.path)
        await settle(svc, until: .saved)
        XCTAssertTrue(photos.saved.isEmpty); XCTAssertNotNil(svc.keptFile); XCTAssertTrue(FileManager.default.fileExists(atPath: svc.keptFile!.path))
        XCTAssertTrue(svc.message.contains("kept in Motion Club"), svc.message)
        XCTAssertEqual(RecordingService.library().first, svc.keptFile); svc.deleteKept(svc.keptFile!); XCTAssertNil(svc.keptFile)
    }
    func testAutoStopReasonsAreExplainedAndSaved() async {
        for (reason, words) in [("background", "left the screen"), ("thermal", "too warm"), ("storage", "out of storage"), ("display", "TV disconnected"), ("limit", "90 minute")] {
            let (svc, rt, photos, _) = recorder()
            await svc.start(); let file = tempVideo(); rt.finish(file.path, reason: reason)
            await settle(svc, until: .saved)
            XCTAssertTrue(svc.message.contains(words), "\(reason): \(svc.message)"); XCTAssertEqual(photos.saved.count, 1, "what was recorded is kept")
        }
    }
    func testRecorderFailureAndSecondStart() async {
        let (svc, rt, _, _) = recorder(); rt.accept = false
        await svc.start(); XCTAssertEqual(svc.state, .failed)
        rt.accept = true; await svc.start(); XCTAssertEqual(svc.state, .recording); await svc.start(); XCTAssertEqual(rt.started.count, 2, "a second tap while recording does not start another")
        svc.receive(#"{"type":"capture","state":"failed","message":"The encoder stopped."}"#)
        XCTAssertEqual(svc.state, .failed); XCTAssertEqual(svc.message, "The encoder stopped.")
        svc.receive("garbage"); svc.receive(#"{"type":"other","state":"finished"}"#); XCTAssertEqual(svc.state, .failed)
    }
    func testSettingsPickWhatGoesInTheRecording() async {
        let (svc, rt, _, _) = recorder()
        svc.settings.quality = .p720at60; svc.settings.partyVoice = false; svc.settings.myMic = false; svc.settings.gameAudio = false
        await svc.start()
        let cfg = (try? JSONSerialization.jsonObject(with: Data(rt.started[0].utf8))) as? [String: Any]
        XCTAssertEqual(cfg?["width"] as? Int, 1280); XCTAssertEqual(cfg?["fps"] as? Int, 60); XCTAssertEqual(cfg?["voice"] as? Bool, false); XCTAssertEqual(cfg?["mic"] as? Bool, false); XCTAssertEqual(cfg?["gameAudio"] as? Bool, false)
        XCTAssertEqual(CaptureSettings.Quality.allCases.map(\.title), ["720p · 30 fps", "720p · 60 fps", "1080p · 30 fps", "1080p · 60 fps"])
    }
    func testLosingTheTVStopsTheRecording() async {
        let (svc, rt, _, _) = recorder(); await svc.start(); svc.displayLost(); XCTAssertEqual(svc.state, .saving); XCTAssertEqual(rt.stops, 1)
    }
    func testALeftoverRecordingIsRecovered() async {
        let (svc, _, photos, _) = recorder(); defer { try? FileManager.default.removeItem(at: RecordingService.tempFolder) }
        let file = tempVideo(); await svc.recoverLeftovers()
        XCTAssertEqual(photos.saved, [file], "a recording left by a crash goes to Photos on the next launch")
    }

    // MARK: renders

    func testPartyScreensRender() async throws {
        guard let dir = ProcessInfo.processInfo.environment["MENU_SNAP_DIR"] else { return }
        let p = party(3)
        p[0].chat.send(text: "ready when you are"); p[1].chat.sendQuick("go"); p[2].chat.sendQuick("nice")
        p[0].tick(); p[1].tick()
        let me = p[0]
        let chat = PartyChat(channel: me.channel, safety: me.safety, defaults: me.defaults)
        chat.localName = { "Ann" }
        chat.receive("partyChat", "{\"t\":\"m\",\"text\":\"you coming to the net?\",\"name\":\"Bea\"}", from: "p1")
        chat.receive("partyChat", "{\"t\":\"q\",\"id\":\"nice\",\"name\":\"Cy\"}", from: "p2")
        chat.sendQuick("gg"); chat.receive("partyRec", "{\"on\":true}", from: "p2")
        let folder = URL(fileURLWithPath: dir, isDirectory: true)
        try FileManager.default.createDirectory(at: folder, withIntermediateDirectories: true)
        func shot<V: View>(_ v: V, _ name: String) async throws {
            let size = CGSize(width: 402, height: 874)
            let scene = try XCTUnwrap(UIApplication.shared.connectedScenes.compactMap { $0 as? UIWindowScene }.first)
            let host = UIHostingController(rootView: v); let window = UIWindow(windowScene: scene)
            window.frame = CGRect(origin: .zero, size: size); window.rootViewController = host; window.isHidden = false
            host.view.frame = window.bounds; host.view.layoutIfNeeded(); try await Task.sleep(for: .seconds(0.9))
            let image = UIGraphicsImageRenderer(size: size).image { _ in host.view.drawHierarchy(in: window.bounds, afterScreenUpdates: true) }
            try XCTUnwrap(image.pngData()).write(to: folder.appendingPathComponent("\(name).png")); window.isHidden = true
        }
        try await shot(PartyChatView(chat: chat), "party-chat")
        try await shot(PartySettingsView(), "party-settings")
        try await shot(PartySetupView(safety: PartySafety(defaults: UserDefaults(suiteName: "snap-\(UUID().uuidString)")!, restrictions: { GameCenterRestrictions() })), "party-setup-age")
        try await shot(PartyMembersView(chat: chat), "party-members")
    }
}
