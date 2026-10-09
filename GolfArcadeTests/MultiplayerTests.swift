import XCTest
import SwiftUI
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
    func testSharedPhoneGuestsLoadWithHostAndKeepIndependentLooks() throws {
        let bus=Bus(); let phone=bus.link("host"); let host=MultiplayerService(sendToRuntime:{_ in true},pollRuntime:{nil})
        host.onMatchRequested={ _ in }; defer { host.leave() }
        try host.host(using:phone,sharedPhone:true);try host.configure(.golf,venue:"postcards")
        try host.addLocalGuest();try host.addLocalGuest();try host.addLocalGuest()
        XCTAssertEqual(host.lobby?.participants.count,4);XCTAssertTrue(host.lobby!.valid())
        let guests=host.lobby!.participants.filter(\.isGuest)
        XCTAssertEqual(Set(guests.compactMap{$0.loadout?.colours["shirt"]}).count,3)
        XCTAssertTrue(guests.allSatisfy{$0.controllerID==host.localID && $0.loadout?.valid()==true})
        XCTAssertThrowsError(try host.addLocalGuest());try host.setReady(true);try host.startMatch();host.runtimeLoaded()
        XCTAssertEqual(host.lobby?.phase,.playing);XCTAssertTrue(host.lobby!.competitors.allSatisfy(\.loaded))
    }
    func testSharedGuestsAndSeparatePhoneKeepTheirOwnSeatsAndLooks() throws {
        let bus=Bus(), runtime=Runtime(), remoteRuntime=Runtime()
        let host=runtime.service(), remote=remoteRuntime.service()
        defer { remote.leave();host.leave() }
        try host.host(using:bus.link("host"),sharedPhone:true);try host.configure(.golf,venue:"postcards")
        try host.addLocalGuest();try host.addLocalGuest()
        let look=MultiplayerLoadout(skinHex:"A8704E",colours:["shirt":"78C5E8"],emotes:EmoteCatalog.defaults,shirtHex:"78C5E8")
        remote.setIdentity(name:"Sam",female:true,loadout:look)
        try remote.connect(using:bus.link("remote"))
        XCTAssertEqual(remote.localSeat,3);XCTAssertEqual(host.lobby,remote.lobby)
        XCTAssertEqual(host.lobby?.participants.first{$0.id=="remote"}?.loadout,look)
        XCTAssertTrue(host.lobby!.participants.allSatisfy(\.connected))
        try host.setReady(true);XCTAssertFalse(host.lobby!.canStart)
        try remote.setReady(true);try host.startMatch();host.runtimeLoaded()
        XCTAssertEqual(host.lobby?.phase,.loading)
        remote.runtimeLoaded();XCTAssertEqual(host.lobby?.phase,.playing)
        XCTAssertTrue(remoteRuntime.configurations.last!.usesHostGolfDisplay)
        XCTAssertFalse(runtime.configurations.last!.usesHostGolfDisplay)
        XCTAssertTrue(host.lobby!.competitors.allSatisfy(\.loaded))
    }
    func testGolfPartyPhoneScreens() async throws {
        let session=SportsSession.shared
        let before=session.golfController;let oldPhase=session.golfPhase;let oldReady=session.ready;let oldPaused=session.paused
        defer { session.golfController=before;session.golfPhase=oldPhase;session.ready=oldReady;session.paused=oldPaused }
        let players=(0..<4).map { GolfControllerReading.PartyPlayer(seat:$0,name:$0==0 ? "Adnan" : "Guest \($0)",controlled:true,canEmote:true,emotes:EmoteCatalog.defaults) }
        var reading=GolfControllerReading.reference
        reading.party = .init(turn:0,shotID:1,player:"Adnan",outcome:"FAIRWAY",phase:"result",myTurn:true,shared:true,putt:false,resultSeconds:2,carry:176,roll:22,apex:28,total:198,players:players)
        session.golfController=reading;session.golfPhase="Result";session.ready=true;session.paused=false
        session.loading.begin(now:.now);session.loading.markReady();session.loading.skip()
        let scene=try XCTUnwrap(UIApplication.shared.connectedScenes.compactMap{$0 as? UIWindowScene}.first)
        let window=UIWindow(windowScene:scene);window.frame=CGRect(x:0,y:0,width:402,height:874)
        let host=UIHostingController(rootView:GolfPhoneController(session:session));host.safeAreaRegions=[]
        window.rootViewController=host;window.isHidden=false;host.view.frame=window.bounds
        defer { window.isHidden=true }
        try await Task.sleep(for:.milliseconds(500));host.view.layoutIfNeeded()
        let out=URL(fileURLWithPath:#filePath).deletingLastPathComponent().deletingLastPathComponent().appendingPathComponent("work/golf-party/native-proof")
        try FileManager.default.createDirectory(at:out,withIntermediateDirectories:true)
        let format=UIGraphicsImageRendererFormat();format.scale=2
        let image=UIGraphicsImageRenderer(size:window.bounds.size,format:format).image { _ in host.view.drawHierarchy(in:window.bounds,afterScreenUpdates:true) }
        try XCTUnwrap(image.pngData()).write(to:out.appendingPathComponent("phone-result.png"))
        XCTAssertEqual(session.golfController.party?.players.filter(\.controlled).count,4)
    }
    func testNearbyCodeAndForgedGuestOwnership() {
        XCTAssertEqual(LocalMultiplayerTransport.joinCode(for:"abc12345-6789-1234-5678-123456789012"),"ABC123")
        let host=MultiplayerParticipant(id:"host",name:"Host",seat:0)
        let guest=MultiplayerParticipant(id:"guest",name:"Guest",seat:1,controllerID:"intruder")
        XCTAssertFalse(MultiplayerLobby(ownerID:"host",sport:.golf,participants:[host,guest]).valid())
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
    func testSharedStartWaitsForTheLatestClientCoverDeadline() throws {
        let p=try party(2); defer { p.close() }
        for service in p.services { try service.setReady(true) }; try p.host.startMatch()
        p.host.runtimeLoaded(readyAfter: 0.2); p.services[1].runtimeLoaded(readyAfter: 1.5)
        let starts = p.runtimes.compactMap { runtime in runtime.received.last(where: { $0.kind == "run" }).flatMap { Double($0.payload) } }
        XCTAssertEqual(starts.count, 2); XCTAssertEqual(starts[0], starts[1])
        XCTAssertGreaterThanOrEqual(starts[0], p.runtimes[0].time + 2.0)
    }

    func testLoadingTimeoutLeavesPartyRecoverable() throws {
        let p = try party(2); defer { p.close() }
        try p.host.setReady(true); try p.services[1].setReady(true); try p.host.startMatch()
        p.runtimes[0].time += 20; p.host.update()
        XCTAssertEqual(p.host.lobby?.phase, .loading); XCTAssertEqual(p.services[1].lobby?.phase, .loading)
        XCTAssertTrue(p.host.loadingNeedsDecision); XCTAssertTrue(p.services[1].loadingNeedsDecision)
        try p.services[1].keepWaitingForLoad()
        XCTAssertFalse(p.host.loadingNeedsDecision); XCTAssertFalse(p.services[1].loadingNeedsDecision)
        p.runtimes[0].time += 21; p.host.update(); XCTAssertTrue(p.host.loadingNeedsDecision)
        p.host.runtimeLoaded(); p.services[1].runtimeLoaded()
        XCTAssertEqual(p.host.lobby?.phase, .playing); XCTAssertFalse(p.host.loadingNeedsDecision)
        XCTAssertFalse(p.services[1].loadingNeedsDecision)
    }
    func testRematchUsesAFreshReadinessBarrierAndRejectsRepeatedInput() throws {
        let p = try party(); defer { p.close() }; try start(p)
        try p.services[2].queueForNextMatch()
        let old = try XCTUnwrap(p.host.lobby?.matchID)
        let result = MultiplayerPacket(lobbyID:p.host.lobby!.id,matchID:old,sender:"a",kind:"result",payload:"{\"winner\":0,\"reason\":\"complete\"}")
        p.runtimes[0].outgoing.append(String(decoding:try JSONEncoder().encode(result),as:UTF8.self)); p.host.update()
        XCTAssertThrowsError(try p.services[1].rematch())
        try p.host.rematch()
        let next = try XCTUnwrap(p.host.lobby?.matchID)
        XCTAssertNotEqual(next, old)
        XCTAssertEqual(p.services.map(\.localSeat), [0,1,-1,-1], "Rematch keeps the same competitors")
        for s in p.services { XCTAssertEqual(s.lobby?.phase, .loading); XCTAssertFalse(s.lobby!.competitors.contains(where: \.loaded)) }
        XCTAssertThrowsError(try p.host.rematch())
        XCTAssertEqual(p.host.lobby?.matchID, next)
        p.host.runtimeLoaded(); XCTAssertEqual(p.host.lobby?.phase, .loading)
        p.services[1].runtimeLoaded(); XCTAssertEqual(p.host.lobby?.phase, .playing)
        for r in p.runtimes { XCTAssertEqual(r.configurations.count, 2) }
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
    func testOldPeerShowsIncompatibleAndOptionalLookDecodes() throws {
        let p = try party(2); defer { p.close() }
        var old = MultiplayerPacket(kind:"hello"); old.version = 1
        try p.bus.links["b"]!.send(JSONEncoder().encode(old),to:["a"],reliable:true)
        XCTAssertEqual(p.host.lastError,MultiplayerError.incompatible.localizedDescription)
        XCTAssertEqual(MultiplayerPacket.version,3)
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

    // MARK: Shared clock

    /// Same vector as ClockFilterModelTests.FixedVectorForTheSwiftPort (Tools/netsim): the Swift filter must match the
    /// model that was validated against simulated jitter spikes.
    func testClockFilterMatchesTheSharedVector() {
        var filter = ClockFilter()
        var samples: [(Double, Double)] = [(0.050, 100.000), (0.030, 100.002), (0.200, 100.090), (0.040, 100.004), (0.300, 99.900), (0.032, 100.003)]
        samples += Array(repeating: (0.031, 100.040), count: 8) + Array(repeating: (0.025, 100.500), count: 4)
        let expected = [100.000, 100.001, 100.002, 100.002, 100.002, 100.003, 100.003, 100.008, 100.013, 100.018, 100.023, 100.028, 100.033, 100.038, 100.040,
                        100.500, 100.500, 100.500]
        XCTAssertEqual(samples.count, expected.count)
        for (i, sample) in samples.enumerated() {
            XCTAssertEqual(filter.add(rtt: sample.0, offset: sample.1), expected[i], accuracy: 1e-9, "sample \(i)")
        }
        XCTAssertEqual(filter.medianRTT, 0.028, accuracy: 1e-9)
    }
    func testClockFilterIgnoresBadSamples() {
        var filter = ClockFilter(); filter.add(rtt: 0.04, offset: 10)
        XCTAssertEqual(filter.add(rtt: .nan, offset: 99), 10, accuracy: 1e-12)
        XCTAssertEqual(filter.add(rtt: 0.04, offset: .infinity), 10, accuracy: 1e-12)
        XCTAssertEqual(filter.add(rtt: -1, offset: 99), 10, accuracy: 1e-12)
    }
    /// A pong that took 200 ms (one leg slow, so its offset is 75 ms off) must not move a clock that a 40 ms pong set.
    func testASlowPongDoesNotMoveTheSharedClock() throws {
        let p = try party(2); defer { p.close() }
        let guest = p.services[1], runtime = p.runtimes[1]
        func pong(sent: Double, hostSentAt: Double, sequence: Int64) throws {
            let data = try JSONEncoder().encode(MultiplayerPacket(lobbyID: guest.lobby!.id, matchID: guest.lobby!.matchID, sender: "a",
                                                                   sequence: sequence, kind: "pong", reliable: false, sentAt: hostSentAt, payload: String(sent)))
            try p.bus.links["a"]!.send(data, to: ["b"], reliable: false)
        }
        runtime.time = 200.00
        try pong(sent: 199.96, hostSentAt: 5.0 + (199.96 + 200.00) / 2, sequence: 5001)
        XCTAssertEqual(guest.networkTime - runtime.time, 5.0, accuracy: 1e-6, "a good pong sets the clock")
        runtime.time = 200.50
        try pong(sent: 200.30, hostSentAt: 5.0 + (200.30 + 200.50) / 2 + 0.075, sequence: 5002)
        XCTAssertEqual(guest.networkTime - runtime.time, 5.0, accuracy: 0.006, "a slow pong may nudge the clock by at most one slew step")
        XCTAssertEqual(guest.roundTrip, 0.12, accuracy: 1e-6, "round trip is the median of the recent pongs, not the last one")
    }
}
