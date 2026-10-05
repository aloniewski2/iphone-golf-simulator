import XCTest
@testable import GolfArcade

@MainActor
final class MultiplayerTests: XCTestCase {
    @MainActor final class Bus {
        var links: [String: Link] = [:]
        func link(_ id: String) -> Link { let result = Link(id: id, bus: self); links[id] = result; return result }
    }
    final class Link: MultiplayerTransport {
        let localID: String
        unowned let bus: Bus
        var onData: ((Data, String) -> Void)?
        var onPeersChanged: (() -> Void)?
        var onError: ((Error) -> Void)?
        var connected = true
        var peers: [String] { connected ? bus.links.keys.filter { $0 != localID }.sorted() : [] }
        var sent: [MultiplayerPacket] = []
        init(id: String, bus: Bus) { localID = id; self.bus = bus }
        func send(_ data: Data, to ids: [String]?, reliable: Bool) throws {
            if let packet = try? JSONDecoder().decode(MultiplayerPacket.self, from: data) { sent.append(packet) }
            for id in ids ?? peers { bus.links[id]?.onData?(data, localID) }
        }
        func disconnect() { bus.links.removeValue(forKey: localID); for link in bus.links.values { link.onPeersChanged?() } }
    }
    @MainActor final class Runtime {
        var received: [MultiplayerPacket] = []
        var outgoing: [String] = []
        var configurations: [MultiplayerMatchConfiguration] = []
        var time: Double = 100
        func service() -> MultiplayerService {
            let s = MultiplayerService(sendToRuntime: { [self] text in
                if let p = try? JSONDecoder().decode(MultiplayerPacket.self, from: Data(text.utf8)) { received.append(p) }; return true
            }, pollRuntime: { [self] in outgoing.isEmpty ? nil : outgoing.removeFirst() }, clock: { [self] in time })
            s.onMatchRequested = { [self] in configurations.append($0) }; s.onReturnToLobby = {}
            return s
        }
    }
    @MainActor struct Party {
        let bus: Bus; let services: [MultiplayerService]; let runtimes: [Runtime]
        var host: MultiplayerService { services[0] }
        func close() { for s in services.reversed() { s.leave() } }
    }
    func party(_ count: Int = 4) throws -> Party {
        let bus = Bus(), runtime = Runtime(), host = runtime.service()
        try host.host(using: bus.link("a"))
        var services = [host], runtimes = [runtime]
        for n in 1..<count { let r = Runtime(), s = r.service(); try s.connect(using: bus.link(String(UnicodeScalar(97+n)!))); services.append(s); runtimes.append(r) }
        return Party(bus: bus, services: services, runtimes: runtimes)
    }
    func start(_ p: Party, _ sport: MultiplayerSport = .tennis) throws {
        try p.host.configure(sport)
        for s in p.services where s.localSeat >= 0 { try s.setReady(true) }
        try p.host.startMatch()
        for s in p.services where s.localSeat >= 0 { s.runtimeLoaded() }
    }
    func packet(_ service: MultiplayerService, kind: String, sender: String, sequence: Int64 = 10000, payload: String = "") throws -> Data {
        try JSONEncoder().encode(MultiplayerPacket(lobbyID: service.lobby!.id, matchID: service.lobby!.matchID, sender: sender, sequence: sequence, kind: kind, payload: payload))
    }
    func testFourPersonPartyAgreesOnOwnerSeatsAndWatchers() throws {
        let p = try party(); defer { p.close() }
        XCTAssertEqual(p.host.localSeat, 0)
        for s in p.services { XCTAssertEqual(s.lobby, p.host.lobby); XCTAssertEqual(s.lobby?.participants.count, 4); XCTAssertEqual(s.lobby?.competitors.count, 2) }
        XCTAssertEqual(p.services[2].localSeat, -1); XCTAssertEqual(p.services[3].localSeat, -1)
    }
    func testGolfConfigurationSeatsAllFourAndClearsReady() throws {
        let p = try party(); defer { p.close() }; try p.host.setReady(true); try p.host.configure(.golf)
        XCTAssertFalse(p.host.lobby!.canStart); XCTAssertEqual(p.services.map(\.localSeat), [0,1,2,3]); XCTAssertTrue(p.host.lobby!.participants.allSatisfy { !$0.ready })
    }
    func testStartRequiresReadyCompetitorsAndOwner() throws {
        let p = try party(); defer { p.close() }; XCTAssertThrowsError(try p.host.startMatch()); XCTAssertThrowsError(try p.services[1].configure(.golf))
        try p.host.setReady(true); XCTAssertThrowsError(try p.host.startMatch())
    }
    func testObserversDoNotBlockLoadingAndEveryPhoneGetsOwnConfiguration() throws {
        let p = try party(); defer { p.close() }; try start(p)
        for (i,s) in p.services.enumerated() { XCTAssertEqual(s.lobby?.phase, .playing); XCTAssertEqual(p.runtimes[i].configurations.last?.localID,s.localID); XCTAssertEqual(p.runtimes[i].configurations.last?.matchID,p.host.lobby?.matchID) }
        XCTAssertTrue(p.runtimes[0].received.contains { $0.kind == "run" })
    }
    func testObserverInputAndForgedSenderCannotControlHost() throws {
        let p = try party(); defer { p.close() }; try start(p)
        let before = p.runtimes[0].received.count
        try p.bus.links["c"]!.send(packet(p.host,kind:"input",sender:"b"),to:["a"],reliable:true)
        XCTAssertEqual(before,p.runtimes[0].received.count)
        try p.bus.links["b"]!.send(packet(p.host,kind:"input",sender:"c"),to:["a"],reliable:true)
        XCTAssertEqual(p.runtimes[0].received.last?.sender,"b")
    }
    func testStaleMatchAndDuplicateSequenceAreRejected() throws {
        let p = try party(2); defer { p.close() }; try start(p)
        var stale = MultiplayerPacket(lobbyID:p.host.lobby!.id,matchID:"old",sender:"b",sequence:10000,kind:"input")
        let before=p.runtimes[0].received.count
        try p.bus.links["b"]!.send(JSONEncoder().encode(stale),to:["a"],reliable:true); XCTAssertEqual(before,p.runtimes[0].received.count)
        stale.matchID=p.host.lobby!.matchID; stale.sequence+=1
        let data=try JSONEncoder().encode(stale); try p.bus.links["b"]!.send(data,to:["a"],reliable:true); try p.bus.links["b"]!.send(data,to:["a"],reliable:true)
        XCTAssertEqual(before+1,p.runtimes[0].received.count)
    }
    func testReturnAndSportSwitchPreservePartyButCreateFreshContest() throws {
        let p=try party(); defer { p.close() }; try start(p)
        let partyID=p.host.lobby!.id, match=p.host.lobby!.matchID
        try p.host.returnToLobby(); XCTAssertEqual(p.host.lobby?.id,partyID); XCTAssertEqual(p.host.lobby?.phase,.lobby)
        try start(p,.golf); XCTAssertNotEqual(p.host.lobby?.matchID,match); XCTAssertEqual(p.host.lobby?.participants.count,4); XCTAssertEqual(p.host.lobby?.competitors.count,4)
    }
    func testFifthPersonIsNotAdmitted() throws {
        let p=try party(); defer { p.close() }; let r=Runtime(), fifth=r.service(); defer { fifth.leave() }
        try fifth.connect(using:p.bus.link("e")); XCTAssertEqual(p.host.lobby?.participants.count,4); XCTAssertNotNil(p.host.lastError); XCTAssertNil(fifth.lobby)
    }
    func testLateViewerGetsLaunchAndLoadedRequestsCheckpoint() throws {
        let p=try party(2); defer { p.close() }; try start(p)
        let r=Runtime(), viewer=r.service(); defer { viewer.leave() }; try viewer.connect(using:p.bus.link("c"))
        XCTAssertEqual(viewer.localSeat,-1); XCTAssertEqual(r.configurations.count,1)
        viewer.runtimeLoaded(); XCTAssertTrue(r.received.contains { $0.kind=="run" }); XCTAssertTrue(p.runtimes[0].received.contains { $0.kind=="snapshotRequest" })
    }
    func testSpectatorLeavingDoesNotSuspendPlay() throws {
        let p=try party(); defer { p.close() }; try start(p); p.services[2].leave()
        XCTAssertEqual(p.host.lobby?.phase,.playing); XCTAssertFalse(p.runtimes[0].received.contains { $0.kind=="suspend"||$0.kind=="drop" })
    }
    func testCompetitorRecoveryAndExpiry() throws {
        let p=try party(2); defer { p.close() }; try start(p)
        try p.bus.links["b"]!.send(packet(p.host,kind:"availability",sender:"b",payload:"false"),to:["a"],reliable:true)
        XCTAssertEqual(p.runtimes[0].received.last?.kind,"suspend")
        try p.bus.links["b"]!.send(packet(p.host,kind:"availability",sender:"b",sequence:10001,payload:"true"),to:["a"],reliable:true)
        XCTAssertEqual(p.runtimes[0].received.last?.kind,"resumePeer")
        try p.bus.links["b"]!.send(packet(p.host,kind:"availability",sender:"b",sequence:10002,payload:"false"),to:["a"],reliable:true)
        p.runtimes[0].time+=16;p.host.update();XCTAssertEqual(p.runtimes[0].received.last?.kind,"drop");XCTAssertEqual(p.host.lobby?.participants.first { $0.id=="b" }?.seat,-1)
    }
    func testLoadingTimeoutLeavesPartyRecoverable() throws {
        let p=try party(2);defer {p.close()};try p.host.setReady(true);try p.services[1].setReady(true);try p.host.startMatch()
        p.runtimes[0].time+=20;p.host.update();XCTAssertEqual(p.host.lobby?.phase,.lobby);XCTAssertEqual(p.services[1].lobby?.phase,.lobby)
        XCTAssertEqual(p.services[1].lastError, "Loading took too long. Everyone is back in the lobby.")
    }
    func testHostBackgroundInterruptsAllPeers() throws {
        let p=try party(2);defer {p.close()};try start(p);p.host.setForeground(false)
        XCTAssertEqual(p.host.lobby?.phase,.interrupted);XCTAssertEqual(p.services[1].lobby?.phase,.interrupted)
    }
    func testWinnerStaysAndQueuedViewerTakesLoserSeat() throws {
        let p=try party();defer {p.close()};try start(p);try p.services[2].queueForNextMatch()
        let result=MultiplayerPacket(lobbyID:p.host.lobby!.id,matchID:p.host.lobby!.matchID,sender:"a",kind:"result",payload:"{\"winner\":1,\"reason\":\"complete\"}")
        p.runtimes[0].outgoing.append(String(decoding:try JSONEncoder().encode(result),as:UTF8.self));p.host.update();try p.host.returnToLobby()
        XCTAssertEqual(p.services[1].localSeat,1);XCTAssertEqual(p.services[2].localSeat,0);XCTAssertEqual(p.host.localSeat,-1)
    }
    func testBridgeOverflowInterruptsInsteadOfStallingContest() throws {
        let p=try party(2);defer {p.close()};try start(p)
        let error=MultiplayerPacket(kind:"bridgeError",payload:"Queue overflow")
        p.runtimes[0].outgoing.append(String(decoding:try JSONEncoder().encode(error),as:UTF8.self));p.host.update()
        XCTAssertEqual(p.host.lobby?.phase,.interrupted);XCTAssertEqual(p.services[1].lobby?.phase,.interrupted);XCTAssertEqual(p.host.lastError,"Queue overflow")
    }
    func testReconnectingCompetitorReloadsBeforeResume() throws {
        let p=try party(2);defer {p.close()};try start(p)
        let link=p.bus.links.removeValue(forKey:"b")!;link.connected=false;p.bus.links["a"]!.onPeersChanged?();link.onPeersChanged?()
        XCTAssertEqual(p.runtimes[0].received.last?.kind,"suspend");XCTAssertEqual(p.services[1].lobby?.phase,.interrupted)
        let prior=p.runtimes[0].received.filter {$0.kind=="resumePeer"}.count
        link.connected=true;p.bus.links["b"]=link;p.bus.links["a"]!.onPeersChanged?();link.onPeersChanged?()
        XCTAssertEqual(p.runtimes[1].configurations.count,2);XCTAssertEqual(p.runtimes[0].received.filter {$0.kind=="resumePeer"}.count,prior)
        p.services[1].runtimeLoaded();XCTAssertEqual(p.runtimes[0].received.filter {$0.kind=="resumePeer"}.count,prior+1);XCTAssertEqual(p.host.lobby?.phase,.playing)
    }
    func testLocalPartyNameFitsBonjourByteLimitWithEmoji() {
        let token=UUID().uuidString
        let name=LocalMultiplayerTransport.advertisedName(String(repeating:"🏌️~",count:30),token:token)
        XCTAssertLessThanOrEqual(name.utf8.count,63);XCTAssertEqual(name.components(separatedBy:"~").count,2);XCTAssertTrue(name.hasSuffix(token))
    }

    func testHostSeatSwapAndGuestSettingsStayProtected() throws {
        let p=try party();defer { p.close() }
        XCTAssertEqual(p.host.lobby?.startReason,"Waiting for Player to be ready")
        try p.host.swapSeat("c",with:"b")
        XCTAssertEqual(p.services.map(\.localSeat),[0,-1,1,-1])
        XCTAssertThrowsError(try p.services[1].swapSeat("d",with:"a"))
        XCTAssertThrowsError(try p.services[1].assignSeat("d",seat:0))
        try p.host.setReady(true);try p.services[2].setReady(true)
        XCTAssertTrue(p.host.lobby!.canStart)
    }

    func testGolfCourseIDsAreAcceptedOnlyForGolf() throws {
        let p = try party(); defer { p.close() }
        for venue in MultiplayerLobby.golfVenues {
            try p.host.configure(.golf, venue: venue)
            XCTAssertTrue(p.host.lobby!.valid()); XCTAssertEqual(p.services[1].lobby?.venue, venue)
        }
        XCTAssertThrowsError(try p.host.configure(.tennis, venue: "postcards"))
        XCTAssertThrowsError(try p.host.configure(.golf, venue: "../../invalid"))
    }
    func testIdentityAndLookReachEveryPeerWithoutChangingSeatsOrReady() throws {
        let p = try party(); defer { p.close() }
        var player = Player(name: "Sam", colorIndex: 1); player.setSkin(0.8); player.setOutfitHex("shirt", "D3F34B")
        p.services[1].setIdentity(name: player.name, female: true, left: true, loadout: player.multiplayerLoadout)
        for s in p.services {
            let remote = s.lobby!.participants.first { $0.id == "b" }!
            XCTAssertEqual(remote.loadout, player.multiplayerLoadout); XCTAssertTrue(remote.female); XCTAssertTrue(remote.left)
            XCTAssertEqual(remote.seat, 1); XCTAssertEqual(remote.name, "Sam")
        }
        try p.services[1].setReady(true); p.runtimes[0].time += 0.1
        player.setOutfitHex("shorts", "FF6B4A"); try p.services[1].updateLook(player.multiplayerLoadout)
        XCTAssertEqual(p.host.lobby!.participants[1].loadout, player.multiplayerLoadout)
        XCTAssertTrue(p.host.lobby!.participants[1].ready)
    }
    func testForgedInvalidAndRapidLookPacketsAreRejected() throws {
        let p = try party(2); defer { p.close() }
        let look = MultiplayerLoadout(skinHex: "E6AE7E", colours: ["shirt":"D3F34B"])
        func send(_ id: String, _ look: MultiplayerLoadout, _ seq: Int64) throws {
            let payload = String(decoding: try JSONEncoder().encode(MultiplayerLookUpdate(participantID: id, loadout: look)), as: UTF8.self)
            try p.bus.links["b"]!.send(packet(p.host, kind: "look", sender: "a", sequence: seq, payload: payload), to: ["a"], reliable: true)
        }
        try send("a", look, 1000); XCTAssertNil(p.host.lobby!.participants[0].loadout)
        try send("b", MultiplayerLoadout(skinHex:"BAD"), 1001); XCTAssertNil(p.host.lobby!.participants[1].loadout)
        try send("b", look, 1002); XCTAssertEqual(p.host.lobby!.participants[1].loadout, look)
        try send("b", MultiplayerLoadout(skinHex:"FFFFFF"), 1003); XCTAssertEqual(p.host.lobby!.participants[1].loadout, look)
    }
    func testEmoteEchoHasSameEventAndCooldownAndPhaseGate() throws {
        let p = try party(); defer { p.close() }
        try p.services[1].playEmote("scuba")
        let event = p.services[1].emotes["b"]!
        XCTAssertTrue(p.services.allSatisfy { $0.emotes["b"]?.id == event.id })
        XCTAssertThrowsError(try p.services[1].playEmote("wave"))
        p.runtimes[0].time += 1.5; p.runtimes[1].time += 1.5
        try p.services[1].playEmote("wave"); XCTAssertEqual(p.host.emotes["b"]?.emoteID, "wave")
        try start(p); XCTAssertThrowsError(try p.services[1].playEmote("pushups"))
    }
    func testHostEnforcesEmoteLimitEvenWhenClientBypassesIt() throws {
        let p = try party(2); defer { p.close() }
        func send(_ id: String, _ seq: Int64, _ startedAt: Double = 100) throws {
            let payload = String(decoding: try JSONEncoder().encode(MultiplayerEmote(participantID:id, emoteID:"spike", startedAt:startedAt, eventID:String(seq))), as:UTF8.self)
            try p.bus.links["b"]!.send(packet(p.host,kind:"emote",sender:"a",sequence:seq,payload:payload),to:["a"],reliable:true)
        }
        try send("a", 1000); XCTAssertTrue(p.host.emotes.isEmpty)
        try send("b", 1001); XCTAssertEqual(p.host.emotes["b"]?.id, "1001")
        try send("b", 1002); XCTAssertEqual(p.host.emotes["b"]?.id, "1001")
        p.runtimes[0].time += 1.49; try send("b",1003); XCTAssertEqual(p.host.emotes["b"]?.id,"1001")
        p.runtimes[0].time += 0.02; try send("b",1004); XCTAssertEqual(p.host.emotes["b"]?.id,"1004")
    }
    func testV1PeerShowsIncompatibleAndV2OptionalLookDecodes() throws {
        let p = try party(2); defer { p.close() }
        var old = MultiplayerPacket(kind:"hello"); old.version = 1
        try p.bus.links["b"]!.send(JSONEncoder().encode(old),to:["a"],reliable:true)
        XCTAssertEqual(p.host.lastError,MultiplayerError.incompatible.localizedDescription)
        XCTAssertEqual(MultiplayerPacket.version,2)
        let data = try JSONEncoder().encode(MultiplayerParticipant(id:"old",name:"Old"))
        XCTAssertNil(try JSONDecoder().decode(MultiplayerParticipant.self,from:data).loadout)
    }
    func testFourFullLoadoutsStayWithinPacketBudget() throws {
        let look = MultiplayerLoadout(gear:["tennis":["skin":"standard","racket":"standard","shoes":"standard"],"golf":["skin":"standard","club":"standard","shoes":"standard"]],skinHex:"E6AE7E",colours:["shirt":"FFFFFF","shorts":"D3F34B","accent":"FF6B4A","racket":"101D35"])
        let participants = (0..<4).map { MultiplayerParticipant(id:UUID().uuidString,name:String(repeating:"A",count:40),seat:$0,loadout:look) }
        let lobby = MultiplayerLobby(ownerID:participants[0].id,sport:.golf,venue:"postcards",participants:participants)
        let json = try JSONEncoder().encode(lobby)
        XCTAssertLessThan(json.count,MultiplayerPacket.maximumBytes)
        let packetBytes = try JSONEncoder().encode(MultiplayerPacket(kind:"lobby",payload:String(decoding:json,as:UTF8.self))).count
        XCTAssertLessThan(packetBytes,MultiplayerPacket.maximumBytes)
        print("LOBBY_JSON_BYTES lobby=\(json.count) packet=\(packetBytes) maximum=\(MultiplayerPacket.maximumBytes)")
    }
}
