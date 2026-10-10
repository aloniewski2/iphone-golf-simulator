import XCTest
@testable import GolfArcade

/// PLAN_MenuHub_WalkableWorld phase 4 (party), phone half, over an in-memory network: a friend's presence becomes a plaza friend
/// in their lobby look; everyone seated in the same online bay and ready starts the match (ALL_SEATED); Call party reaches the
/// others; the host leaving an idle lobby hands it to the next phone.
@MainActor
final class HubPartyTests: XCTestCase {
    @MainActor final class Bus {
        var links: [String: Link] = [:]
        func link(_ id: String) -> Link { let l = Link(id: id, bus: self); links[id] = l; return l }
    }
    final class Link: MultiplayerTransport {
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
    override func setUp() async throws { savedDisplay = SportsSession.shared.displayConnected; SportsSession.shared.displayConnected = true; TennisMenu.shared.debugShow(.main); OnboardingFlow.shared.exitToMenu() }
    override func tearDown() async throws { HubTestState.restore(display: savedDisplay) }

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

    func testEveryoneSeatedAndReadyStartsTheMatchFromTheHostsPhone() throws {
        let bus = Bus(), a = service("Ann", shirt: "E8505B"), b = service("Bea", shirt: "3F62CC")
        try a.host(using: bus.link("a")); try b.connect(using: bus.link("b"))
        defer { b.leave(); a.leave() }
        let ha = hub(a, "ha"), hb = hub(b, "hb")
        ha.receive(["type": "hubAction", "session": "ha", "message": "bay-tennis-online"])
        XCTAssertEqual(ha.station, "bay-tennis-online"); XCTAssertEqual(a.lobby?.sport, .tennis)
        ha.setPartyReady(true)
        ha.debugPartyTick()
        XCTAssertEqual(a.lobby?.phase, .lobby, "Bea is still on her way: no start")
        XCTAssertEqual(ha.partyWaitingFor, ["Bea"])
        hb.receive(["type": "hubAction", "session": "hb", "message": "bay-tennis-online"])
        hb.receive(["type": "hubPose", "session": "hb", "message": "tennis|204.600|-1000.000|3.700|0.0|0.00|bay-tennis-online|"])
        XCTAssertEqual(ha.friendBays["b"], "bay-tennis-online"); XCTAssertTrue(ha.partyWaitingFor.isEmpty)
        ha.debugPartyTick()
        XCTAssertEqual(a.lobby?.phase, .lobby, "seated, but Bea is not ready yet")
        hb.setPartyReady(true)
        ha.debugPartyTick()
        XCTAssertEqual(a.lobby?.phase, .loading, "everyone seated and ready: the host's phone starts the match")
        XCTAssertEqual(b.lobby?.phase, .loading)
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
        ha.setPartyReady(true); hb.setPartyReady(true); ha.debugPartyTick()
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
        for i in 0..<3 { if i > 0 { sit(i) }; hubs[i].setPartyReady(true) }
        hubs[0].debugPartyTick()
        XCTAssertEqual(services[0].lobby?.phase, .lobby, "Dee is still on her way: no start")
        XCTAssertEqual(hubs[0].partyWaitingFor, ["Dee"])
        sit(3); hubs[0].debugPartyTick()
        XCTAssertTrue(hubs[0].partyWaitingFor.isEmpty)
        XCTAssertEqual(services[0].lobby?.phase, .lobby, "all seated, but Dee is not ready yet")
        hubs[3].setPartyReady(true); hubs[0].debugPartyTick()
        XCTAssertEqual(services[0].lobby?.phase, .loading, "everyone seated and ready: the host's phone starts the match")
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
