import XCTest
@testable import GolfArcade

/// PLAN_MenuHub_WalkableWorld phase 4 (party), phone half, over an in-memory network: a friend's presence becomes a plaza friend
/// in their lobby look; sitting down together on an online bench starts the match (ALL_SEATED: sitting is being ready; friends
/// elsewhere in the plaza do not hold it up and stay in the plaza); Call party reaches the others; the host leaving an idle lobby
/// hands it to the next phone.
@MainActor
final class HubPartyTests: XCTestCase {
    @MainActor final class Bus {
        var links: [String: Link] = [:]
        func link(_ id: String) -> Link { let l = Link(id: id, bus: self); links[id] = l; return l }
    }
    final class Link: PartyTransport {
        let localID: String; unowned let bus: Bus
        var onData: ((Data, String) -> Void)?; var onPeersChanged: (() -> Void)?; var onError: ((Error) -> Void)?
        var peers: [String] { bus.links.keys.filter { $0 != localID }.sorted() }
        init(id: String, bus: Bus) { localID = id; self.bus = bus }
        func send(_ data: Data, to ids: [String]?, reliable: Bool) throws { for id in ids ?? peers { bus.links[id]?.onData?(data, localID) } }
        func disconnect() { bus.links.removeValue(forKey: localID); for link in bus.links.values.sorted(by: { $0.localID < $1.localID }) { link.onPeersChanged?() } }
    }
    private func service(_ name: String, shirt: String) -> MultiplayerService {
        let s = MultiplayerService(sendToRuntime: { _ in true }, pollRuntime: { nil }, clock: { 100 })
        s.onMatchRequested = { _ in }; s.onReturnToLobby = {}
        // every phone in the Plaza has a TV (the plaza only runs with one); the link-steadiness gate has its own MultiplayerTests
        s.setScreen(true); s.requiresStableLink = false
        s.setIdentity(name: name, female: name == "Bea", loadout: MultiplayerLoadout(skinHex: "C47A4C", colours: ["shirt": shirt], emotes: EmoteCatalog.defaults, shirtHex: shirt, shortsHex: "24345E"))
        return s
    }
    private func hub(_ s: MultiplayerService, _ session: String) -> HubSession {
        let h = HubSession(); h.partyService = s; h.debugLive(session: session); return h
    }
    private var savedDisplay = false
    override func setUp() async throws { savedDisplay = SportsSession.shared.displayConnected; SportsSession.shared.displayConnected = true; TennisMenu.shared.debugShow(.main); OnboardingFlow.shared.exitToMenu(); HubSession.partyCountdown = 0 }
    override func tearDown() async throws { HubTestState.restore(display: savedDisplay); HubSession.partyCountdown = 2.5 }

    func testAFriendsPresenceBecomesAPlazaFriendInTheirLook() throws {
        let bus = Bus(), a = service("Ann", shirt: "E8505B"), b = service("Bea", shirt: "3F62CC")
        try a.host(using: bus.link("a")); try b.connect(using: bus.link("b"))
        defer { b.leave(); a.leave() }
        XCTAssertEqual(b.lobby?.participants.count, 2, "Bea joined Ann's lobby")
        let ha = hub(a, "ha"), hb = hub(b, "hb")
        var toUnity: [[String: Any]] = []; hb.debugSent = { toUnity.append($0) }
        ha.receive(["type": "hubPose", "session": "ha", "message": "plaza|1.000|0.000|2.000|90.0|1.20||"])
        let remote = toUnity.first { $0["action"] as? String == "hubRemote" }
        XCTAssertNotNil(remote, "Ann's pose reached Bea's plaza")
        let state = try JSONSerialization.jsonObject(with: Data((remote?["remote"] as? String ?? "").utf8)) as? [String: Any]
        XCTAssertEqual(state?["id"] as? String, "a"); XCTAssertEqual(state?["name"] as? String, "Ann")
        XCTAssertEqual(state?["shirt"] as? String, "E8505B", "in Ann's own shirt (LOOK)")
        XCTAssertEqual(state?["x"] as? Double, 1); XCTAssertEqual(state?["place"] as? String, "plaza")
        // the member list goes to Unity too; Ann leaving removes her
        hb.debugPartyTick()
        XCTAssertTrue(toUnity.contains { $0["action"] as? String == "hubRoster" && $0["mode"] as? String == "a" })
    }

    func testSittingDownTogetherStartsTheMatchFromTheHostsPhone() throws {
        let bus = Bus(), a = service("Ann", shirt: "E8505B"), b = service("Bea", shirt: "3F62CC")
        try a.host(using: bus.link("a")); try b.connect(using: bus.link("b"))
        defer { b.leave(); a.leave() }
        let ha = hub(a, "ha"), hb = hub(b, "hb")
        ha.receive(["type": "hubAction", "session": "ha", "message": "bay-tennis-online"])
        XCTAssertEqual(ha.station, "bay-tennis-online"); XCTAssertEqual(a.lobby?.sport, .tennis)
        ha.debugPartyTick()
        XCTAssertEqual(a.lobby?.phase, .lobby, "alone on the bench: no start")
        // Bea walks into the tennis room: the bench waits for her
        hb.receive(["type": "hubPose", "session": "hb", "message": "tennis|204.600|-1000.000|0.000|0.0|1.00||"])
        XCTAssertEqual(ha.partyWaitingFor, ["Bea"]); ha.debugPartyTick(); XCTAssertEqual(a.lobby?.phase, .lobby)
        // ...and sits down: that is all (no Ready button)
        hb.receive(["type": "hubAction", "session": "hb", "message": "bay-tennis-online"])
        hb.receive(["type": "hubPose", "session": "hb", "message": "tennis|204.600|-1000.000|3.700|0.0|0.00|bay-tennis-online|"])
        XCTAssertEqual(ha.friendBays["b"], "bay-tennis-online"); XCTAssertTrue(ha.partyWaitingFor.isEmpty)
        ha.debugPartyTick()   // seats + ready from the bench, then the count
        ha.debugPartyTick()
        XCTAssertEqual(a.lobby?.phase, .loading, "everyone on the bench: the host's phone starts the match")
        XCTAssertEqual(b.lobby?.phase, .loading)
        XCTAssertEqual(Set(a.lobby?.competitors.map(\.id) ?? []), ["a", "b"])
    }

    /// A friend elsewhere in the plaza (the locker, or a phone with no TV) does not hold the bench up and is not pulled into the
    /// match: their phone stays in the plaza.
    func testAFriendElsewhereInThePlazaStaysThereWhileTheBenchPlays() throws {
        let bus = Bus(), a = service("Ann", shirt: "E8505B"), b = service("Bea", shirt: "3F62CC"), c = service("Cy", shirt: "7BDA4A")
        var configs: [String: MultiplayerMatchConfiguration] = [:]
        a.onMatchRequested = { configs["a"] = $0 }; b.onMatchRequested = { configs["b"] = $0 }; c.onMatchRequested = { configs["c"] = $0 }
        try a.host(using: bus.link("a")); try b.connect(using: bus.link("b")); try c.connect(using: bus.link("c"))
        defer { c.leave(); b.leave(); a.leave() }
        let ha = hub(a, "ha"), hb = hub(b, "hb"), hc = hub(c, "hc")
        hc.receive(["type": "hubPose", "session": "hc", "message": "locker|0.000|-1000.000|0.000|0.0|0.00||"])
        ha.receive(["type": "hubAction", "session": "ha", "message": "bay-tennis-online"])
        hb.receive(["type": "hubAction", "session": "hb", "message": "bay-tennis-online"])
        hb.receive(["type": "hubPose", "session": "hb", "message": "tennis|204.600|-1000.000|3.700|0.0|0.00|bay-tennis-online|"])
        XCTAssertTrue(ha.partyWaitingFor.isEmpty, "Cy is in the locker: nobody to wait for")
        ha.debugPartyTick(); ha.debugPartyTick()
        XCTAssertEqual(a.lobby?.phase, .loading)
        XCTAssertNotNil(configs["a"]); XCTAssertNotNil(configs["b"])
        XCTAssertNil(configs["c"], "Cy was not on the bench: Cy's phone stays in the plaza")
        XCTAssertEqual(a.lobby?.participants.first { $0.id == "c" }?.seat, -1)
    }

    func testThePartyLandsInOneMatchAndComesBackToTheSameBay() throws {
        let bus = Bus(), a = service("Ann", shirt: "E8505B"), b = service("Bea", shirt: "3F62CC")
        var configs: [String: MultiplayerMatchConfiguration] = [:]
        a.onMatchRequested = { configs["a"] = $0 }; b.onMatchRequested = { configs["b"] = $0 }
        try a.host(using: bus.link("a")); try b.connect(using: bus.link("b"))
        defer { b.leave(); a.leave() }
        let ha = hub(a, "ha"), hb = hub(b, "hb")
        for (h, s) in [(ha, "ha"), (hb, "hb")] { h.receive(["type": "hubAction", "session": s, "message": "bay-tennis-online"]) }
        hb.receive(["type": "hubPose", "session": "hb", "message": "tennis|204.600|-1000.000|3.700|0.0|0.00|bay-tennis-online|"])
        ha.debugPartyTick(); ha.debugPartyTick()
        XCTAssertNotNil(configs["a"]); XCTAssertEqual(configs["a"]?.matchID, configs["b"]?.matchID, "the same match for everyone")
        // each phone launched from the bay (behind its plaza) and, back from the match, sits on the same bench
        for h in [ha, hb] {
            XCTAssertEqual(h.bayForLaunch(), "bay-tennis-online")
            h.runtimeTaken(bay: h.bayForLaunch()); h.sessionEnded(finished: true)
            XCTAssertEqual(h.returnBay, "bay-tennis-online")
        }
    }

    /// ALL_SEATED + TOGETHER with a full party of four. Online tennis is 1 v 1, so four friends play golf: the host's bay makes it golf.
    func testFourFriendsSeatedInTheGolfBayStartOneMatchAndComeBackToIt() throws {
        let bus = Bus(), bay = "bay-golf-online"
        let ids = ["a", "b", "c", "d"], names = ["Ann", "Bea", "Cal", "Dee"], shirts = ["E8505B", "3F62CC", "2FA36B", "F2B233"]
        let services = ids.indices.map { service(names[$0], shirt: shirts[$0]) }
        var configs: [String: MultiplayerMatchConfiguration] = [:]
        for (i, s) in services.enumerated() { let id = ids[i]; s.onMatchRequested = { configs[id] = $0 } }
        try services[0].host(using: bus.link("a"))
        for i in 1..<4 { try services[i].connect(using: bus.link(ids[i])) }
        defer { for s in services.reversed() { s.leave() } }
        XCTAssertEqual(services[0].lobby?.participants.count, 4)
        let hubs = ids.indices.map { hub(services[$0], "h" + ids[$0]) }
        func sit(_ i: Int) {
            hubs[i].receive(["type": "hubAction", "session": "h" + ids[i], "message": bay])
            hubs[i].receive(["type": "hubPose", "session": "h" + ids[i], "message": "golf|40\(i).600|-1000.000|3.700|0.0|0.00|\(bay)|"])
        }
        sit(0); XCTAssertEqual(services[0].lobby?.sport, .golf, "the host's bay sets the party's sport")
        for i in 1..<3 { sit(i) }
        hubs[3].receive(["type": "hubPose", "session": "hd", "message": "golf|403.000|-1000.000|0.000|0.0|1.00||"])   // Dee walking in
        hubs[0].debugPartyTick()
        XCTAssertEqual(services[0].lobby?.phase, .lobby, "Dee is still on her way: no start")
        XCTAssertEqual(hubs[0].partyWaitingFor, ["Dee"])
        sit(3); hubs[0].debugPartyTick(); hubs[0].debugPartyTick()
        XCTAssertTrue(hubs[0].partyWaitingFor.isEmpty)
        XCTAssertEqual(services[0].lobby?.phase, .loading, "everyone on the golf bench: the host's phone starts the match")
        XCTAssertEqual(configs["a"]?.sport, "golf", "the golf bench plays golf")
        XCTAssertEqual(configs.count, 4, "all four phones were asked to load")
        XCTAssertEqual(Set(configs.values.map(\.matchID)).count, 1, "the same match for everyone")
        for h in hubs {
            XCTAssertEqual(h.bayForLaunch(), bay)
            h.runtimeTaken(bay: h.bayForLaunch()); h.sessionEnded(finished: true)
            XCTAssertEqual(h.returnBay, bay, "back on the same bench")
        }
    }

    func testCallPartyReachesTheOthers() throws {
        let bus = Bus(), a = service("Ann", shirt: "E8505B"), b = service("Bea", shirt: "3F62CC")
        try a.host(using: bus.link("a")); try b.connect(using: bus.link("b"))
        defer { b.leave(); a.leave() }
        let ha = hub(a, "ha"), hb = hub(b, "hb")
        var toUnity: [[String: Any]] = []; hb.debugSent = { toUnity.append($0) }
        ha.receive(["type": "hubAction", "session": "ha", "message": "bay-golf-online"])
        XCTAssertEqual(a.lobby?.sport, .golf, "the host's bay sets the party's sport")
        ha.callParty()
        XCTAssertEqual(hb.lastCall?.bay, "bay-golf-online")
        XCTAssertTrue(toUnity.contains { $0["action"] as? String == "hubCall" && $0["mode"] as? String == "bay-golf-online" }, "Bea's TV shows the way")
        XCTAssertTrue(hb.notice.contains("Ann"))
    }

    /// INVITE: every Invite in the Plaza (the invite board, ☰'s shortcut, the Friends button) opens the Friends list (a sheet over
    /// the controller) and nothing else: no Game Center sheet, no old menu screen on the phone; the hero steps away from the board.
    func testInviteInThePlazaOpensTheHubAndNoMenuScreen() {
        let hub = HubSession.shared, menu = TennisMenu.shared
        hub.debugReset(); menu.debugShow(.main); hub.debugLive(session: "inv")
        var invites = 0; hub.presentInvite = { invites += 1 }
        var toUnity: [[String: Any]] = []; hub.debugSent = { toUnity.append($0) }
        hub.receive(["type": "hubAction", "session": "inv", "message": "board-invite"])
        XCTAssertEqual(invites, 1, "the invite board opens the Friends hub")
        XCTAssertEqual(menu.screen, .main, "no menu screen opens on the phone"); XCTAssertFalse(hub.panelOpen); XCTAssertNil(hub.station)
        XCTAssertTrue(toUnity.contains { $0["action"] as? String == "hubB" }, "the hero steps away from the board")
        hub.inviteFriends()
        XCTAssertEqual(invites, 2, "Friends button / ☰ Invite: the hub at once, no walk")
        menu.tap("homeInvite")
        XCTAssertEqual(invites, 3, "the menu's Play with Friends opens the same hub")
        XCTAssertEqual(menu.screen, .main)
    }

    /// JOIN: a friend connecting (or any lobby change) leaves both phones in the Plaza, also mid-station, with a TV or without.
    func testAFriendJoiningKeepsBothPhonesInThePlaza() throws {
        let bus = Bus(), a = service("Ann", shirt: "E8505B"), b = service("Bea", shirt: "3F62CC")
        let hub = HubSession.shared, menu = TennisMenu.shared
        hub.debugReset(); menu.debugShow(.main); hub.debugLive(session: "join")
        XCTAssertTrue(hub.drivesPhone)
        try a.host(using: bus.link("a")); try b.connect(using: bus.link("b"))
        defer { b.leave(); a.leave() }
        XCTAssertEqual(b.lobby?.participants.count, 2)
        for s in [a, b] { OnlineLobbyMenu(service: s).sync(menu) }
        XCTAssertEqual(menu.screen, .main, "in the plaza with your friend: no lobby screen")
        menu.debugShow(.character); OnlineLobbyMenu(service: b).sync(menu)
        XCTAssertEqual(menu.screen, .character, "a friend joining does not pull you out of the locker")
        SportsSession.shared.displayConnected = false; menu.debugShow(.main); OnlineLobbyMenu(service: b).sync(menu)
        XCTAssertEqual(menu.screen, .main, "no TV: the plaza is on the phone, still no lobby screen")
        menu.debugShow(.online(.lobby)); OnlineLobbyMenu(service: b).sync(menu)
        XCTAssertEqual(menu.screen, .main, "a lobby screen left over from an old path is closed")
    }

    /// Leaving a party match (Exit, or Back to the Plaza after it) brings the whole party back to the bench: the host ends it for
    /// everyone; a guest asks the host. Never a confirm or lobby screen.
    func testBackToThePlazaFromAPartyMatchBringsEveryoneBack() throws {
        let bus = Bus(), a = service("Ann", shirt: "E8505B"), b = service("Bea", shirt: "3F62CC")
        var back = 0; a.onReturnToLobby = { back += 1 }; b.onReturnToLobby = { back += 1 }
        try a.host(using: bus.link("a")); try b.connect(using: bus.link("b"))
        defer { b.leave(); a.leave() }
        try a.configure(.tennis); a.seatParty(["a", "b"]); try a.startMatch()
        XCTAssertEqual(b.lobby?.phase, .loading)
        b.endPartyMatch()   // a guest's Exit mid-load
        XCTAssertEqual(a.lobby?.phase, .lobby, "the host brought everyone back"); XCTAssertEqual(b.lobby?.phase, .lobby)
        XCTAssertGreaterThanOrEqual(back, 2, "every phone's match stopped")
        XCTAssertEqual(a.lobby?.participants.count, 2, "the party is still together")
    }

    func testTheHostLeavingAnIdleLobbyHandsItOver() throws {
        let bus = Bus(), a = service("Ann", shirt: "E8505B"), b = service("Bea", shirt: "3F62CC"), c = service("Cy", shirt: "7BDA4A")
        try a.host(using: bus.link("a")); try b.connect(using: bus.link("b")); try c.connect(using: bus.link("c"))
        defer { c.leave(); b.leave() }
        XCTAssertEqual(c.lobby?.participants.count, 3)
        a.leave()
        XCTAssertTrue(b.isOwner, "the next phone (lowest id) is the host now")
        XCTAssertEqual(c.lobby?.ownerID, "b"); XCTAssertEqual(c.lobby?.phase, .lobby, "the party carries on")
        XCTAssertFalse(c.lobby?.participants.contains { $0.id == "a" } ?? true)
    }
}
