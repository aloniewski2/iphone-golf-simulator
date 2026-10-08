import XCTest
import SceneKit
import simd
@testable import GolfArcade

/// The walkable lobby: the rules (what is walkable, how long a walk takes), the scene, the stations opening the menus the app already has, and the position messages.
@MainActor final class LobbyWorldTests: XCTestCase {
    typealias P = LobbyLayout.P
    let layout = LobbyLayout.standard

    // MARK: the layout
    func testSpawnAndEveryStationRingAreWalkable() {
        XCTAssertTrue(layout.isWalkable(layout.spawn))
        for s in layout.stations { XCTAssertTrue(layout.isWalkable(layout.arrival(at: s)), "\(s.id) cannot be stood in") }
        for p in layout.partySpots { XCTAssertTrue(layout.isWalkable(p), "a party spot is off the ground: \(p)") }
        XCTAssertEqual(layout.partySpots.count, 4)
    }

    func testEveryStationIsAboutFourSecondsFromTheSpawn() throws {
        for s in layout.stations {
            let metres = try XCTUnwrap(layout.walkingDistance(from: layout.spawn, to: layout.arrival(at: s)), "no way to \(s.id)")
            XCTAssertLessThanOrEqual(metres / LobbyLayout.jogSpeed, 4.6, "\(s.id) is \(metres) m away: \(metres / LobbyLayout.jogSpeed) s")
        }
    }

    func testThePathsDoNotRunThroughEachOtherAndEveryRingIsReachable() throws {
        // from the locker to the terrace you cross the plaza: the way is no shorter than the walk to the plaza's edge and across it
        let locker = layout.arrival(at: try XCTUnwrap(layout.station("locker-colors"))), terrace = layout.arrival(at: try XCTUnwrap(layout.station("party-board")))
        let direct = simd_length(locker - terrace)
        let walk = try XCTUnwrap(layout.walkingDistance(from: locker, to: terrace))
        XCTAssertGreaterThanOrEqual(walk, direct - 0.5)
        // the ground between the paths is not walkable
        for p in [P(-9, 9), P(9, 9), P(9, -9), P(-6, -14), P(0, 25), P(-26, 8)] { XCTAssertFalse(layout.isWalkable(p), "\(p) should be lawn or sea") }
        // every station can be walked to from every other
        for a in layout.stations { for b in layout.stations where a.id != b.id {
            XCTAssertNotNil(layout.walkingDistance(from: layout.arrival(at: a), to: layout.arrival(at: b)), "\(a.id) -> \(b.id)")
        } }
    }

    func testStationRingsAreSeparate() {
        for a in layout.stations { for b in layout.stations where a.id < b.id {
            XCTAssertGreaterThan(simd_length(a.ring - b.ring), a.radius + b.radius, "\(a.id) and \(b.id) overlap")
        } }
    }

    func testWalkingIntoThingsIsStopped() {
        // into the tower: pushed out to its edge
        let atTower = layout.resolve(from: P(5, 0), to: P(0, 0))
        XCTAssertGreaterThanOrEqual(simd_length(atTower), 3.8)
        // off the plaza onto the lawn between two paths: stays on the ground
        let off = layout.resolve(from: P(5, 5), to: P(12, 12))
        XCTAssertTrue(layout.isWalkable(off))
        // along the edge of a path it slides instead of stopping dead
        let slid = layout.resolve(from: P(-14, 2.0), to: P(-15, 3.2))
        XCTAssertGreaterThan(simd_length(slid - P(-14, 2.0)), 0.5)
        XCTAssertTrue(layout.isWalkable(slid))
        // a long walk in a straight line across the whole world never ends up off the ground
        var p = layout.spawn
        for step in 0..<400 {
            let a = Float(step) * 0.05
            let next = p + P(cos(a), sin(a)) * 0.4
            p = layout.resolve(from: p, to: next)
            XCTAssertTrue(layout.isWalkable(p) || layout.walkable.contains { $0.contains(p) })
        }
    }

    func testTheTerraceIsRaisedAndReachedByARamp() {
        XCTAssertEqual(layout.height(at: P(0, 0)), 0)
        XCTAssertEqual(layout.height(at: P(18, 0)), LobbyLayout.deckHeight, accuracy: 0.001)
        let mid = layout.height(at: P(11.5, 0))
        XCTAssertGreaterThan(mid, 0.05); XCTAssertLessThan(mid, LobbyLayout.deckHeight)
        var last: Float = 0
        for x in stride(from: Float(9), through: 14, by: 0.25) { let h = layout.height(at: P(x, 0)); XCTAssertGreaterThanOrEqual(h, last - 0.0001); last = h }
    }

    func testComingBackFromAMatchLandsNextToTheGate() throws {
        for id in ["gate-tennis", "gate-golf", "gate-howto"] {
            let s = try XCTUnwrap(layout.station(id)), back = layout.returnPoint(from: s)
            XCTAssertTrue(layout.isWalkable(back.position))
            XCTAssertLessThan(simd_length(back.position - s.ring), 3.5, "\(id): too far from its gate")
        }
        let party = layout.returnPoint(from: try XCTUnwrap(layout.station("party-board")))
        XCTAssertEqual(party.position, layout.partySpots[0])
    }

    func testTheQuickMenuReachesEveryPlace() {
        let world = LobbyWorld.shared
        let ids = Set(world.quickEntries.map(\.id))
        for id in ["locker-colors", "gate-tennis", "gate-golf", "gate-howto", "party-board", "shop", "settings"] { XCTAssertTrue(ids.contains(id), id) }
        for e in world.quickEntries where e.id != "settings" { XCTAssertNotNil(layout.station(e.id), e.id) }
        XCTAssertTrue(world.quickEntries.first { $0.id == "shop" }?.locked == true)
    }

    // MARK: the scene
    func testTheSceneHasARingAndAFloatingNameForEveryStation() {
        let scene = LobbyScene(layout: layout)
        for s in layout.stations {
            XCTAssertNotNil(scene.rings[s.id], "ring \(s.id)")
            XCTAssertNotNil(scene.labels[s.id], "label \(s.id)")
        }
        XCTAssertNotNil(scene.cameraNode.camera)
        XCTAssertNotNil(scene.scene.rootNode.childNode(withName: "tapPlane", recursively: false))
    }

    func testThePlaceIsBuiltFromTheLayoutAlone() {
        // moving a station moves its ring: nothing else in the scene hard-codes it
        var moved = layout.stations
        moved[0].ring = P(-19.4, 6.0)
        let altered = LobbyLayout(spawn: layout.spawn, spawnHeading: layout.spawnHeading, stations: moved, walkable: layout.walkable, obstacles: layout.obstacles,
                                  partySpots: layout.partySpots, partyHeading: layout.partyHeading, cameraBehind: layout.cameraBehind, cameraHeight: layout.cameraHeight, cameraLookHeight: layout.cameraLookHeight)
        let scene = LobbyScene(layout: altered)
        XCTAssertEqual(scene.rings[moved[0].id]?.position.z ?? 0, -6.0, accuracy: 0.001)
    }

    // MARK: stations open the menus the app already has
    func testEachStationOpensItsMenuAndBackComesHome() throws {
        LobbyWorld.enabled = true
        let world = LobbyWorld.shared
        let menu = TennisMenu()
        func use(_ id: String) { menu.showWorld(); world.perform(try! XCTUnwrap(layout.station(id)), menu: menu) }
        use("locker-gear"); XCTAssertEqual(menu.screen, .character); XCTAssertEqual(menu.lockerTab, .gear)
        menu.back(); XCTAssertEqual(menu.screen, .world)
        use("locker-colors"); XCTAssertEqual(menu.lockerTab, .customize)
        menu.back(); XCTAssertEqual(menu.screen, .world)
        use("locker-emotes"); XCTAssertEqual(menu.lockerTab, .emotes)
        menu.back(); XCTAssertEqual(menu.screen, .world)
        use("gate-tennis"); XCTAssertEqual(menu.screen, .hub(.tennis))
        menu.back(); XCTAssertEqual(menu.screen, .world, "the gate's menu goes back to the club, not up the old menu tree")
        use("gate-golf"); XCTAssertEqual(menu.screen, .hub(.golf))
        menu.back(); XCTAssertEqual(menu.screen, .world)
        use("gate-howto"); XCTAssertEqual(menu.screen, .howTo)
        menu.back(); XCTAssertEqual(menu.screen, .world)
        use("party-board"); XCTAssertEqual(menu.screen, .party)
        menu.back(); XCTAssertEqual(menu.screen, .world)
        use("shop"); XCTAssertEqual(menu.screen, .world, "the Pro Shop stays closed")
        XCTAssertFalse(menu.notice.isEmpty)
    }

    func testAMatchSentFromTheGateComesBackToTheGate() throws {
        let world = LobbyWorld.shared, menu = TennisMenu()
        menu.showWorld()
        world.perform(try XCTUnwrap(layout.station("gate-golf")), menu: menu)
        XCTAssertNil(world.returnStation, "closing the gate's menu leaves you where you are")
        world.matchStarted()
        XCTAssertEqual(world.returnStation, "gate-golf")
    }

    func testClassicMenusTakeOverWhenTheClubIsOff() {
        LobbyWorld.enabled = false
        defer { LobbyWorld.enabled = true }
        let menu = TennisMenu()
        XCTAssertEqual(menu.homeScreen, .main)
        XCTAssertFalse(LobbyWorld.available)
        LobbyWorld.enabled = true
        XCTAssertEqual(menu.homeScreen, .world)
    }

    // MARK: people
    func testAPoseTravelsAsTinyJSON() throws {
        let m = LobbyPoseMessage(id: "00000000-0000-0000-0000-000000000002", x: 12.5, y: -3.25, h: 1.57, s: 5.4)
        let data = try JSONEncoder().encode(m)
        XCTAssertLessThan(data.count, 120)
        XCTAssertEqual(try JSONDecoder().decode(LobbyPoseMessage.self, from: data), m)
    }

    func testAPersonIsBuiltFromTheirLookAndWalksOnTheGround() throws {
        var p = Player(name: "Sam", colorIndex: 1, handedness: .left)
        p.standardFemale = true; p.setOutfitHex("shirt", "D3F34B")
        let a = LobbyAvatar(id: "x", player: p, name: "Sam", showsName: true, index: 1)
        XCTAssertTrue(a.hasRig, "the hero needs its exported rig")
        a.place(P(3, -4), height: 0.8, heading: 1.0)
        XCTAssertEqual(a.node.position.y, 0.8, accuracy: 0.0001)
        XCTAssertEqual(a.node.position.z, 4, accuracy: 0.0001)
        for speed: Float in [0, 0.5, 2, 5.5] { a.animate(dt: 1.0 / 60, now: 1, speed: speed) }
        // a new shirt rebuilds the person; the same look does not
        let before = a.node.childNodes.count
        a.update(player: p, name: "Sam"); XCTAssertEqual(a.node.childNodes.count, before)
        p.setOutfitHex("shirt", "FF6B4A"); a.update(player: p, name: "Sam"); XCTAssertTrue(a.hasRig)
    }

    func testEveryoneInTheLobbyIsInTheWorld() throws {
        let world = LobbyWorld.shared
        XCTAssertTrue(world.prepare())
        let service = MultiplayerService.shared
        service.leave()
        try service.enableMock(count: 3, player: Player(name: "Me", colorIndex: 0))
        world.syncParty(force: true)
        XCTAssertEqual(world.remoteCount, 2)
        // a position from a phone in the lobby moves that person; one from a stranger is ignored
        let friend = try XCTUnwrap(service.lobby?.participants.first { $0.id != service.localID })
        world.receivePose(LobbyPoseMessage(id: friend.id, x: 5, y: 6, h: 0.5, s: 4))
        world.receivePose(LobbyPoseMessage(id: "stranger", x: 1, y: 1, h: 0, s: 0))
        XCTAssertEqual(world.remoteCount, 2)
        service.leave()
        world.syncParty(force: true)
        XCTAssertEqual(world.remoteCount, 0)
    }
}
