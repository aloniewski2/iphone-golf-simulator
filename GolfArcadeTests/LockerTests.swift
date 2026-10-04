import XCTest
@testable import GolfArcade

/// The locker's data model (Standard-only catalog, loadout saves), its focus grid and its Revert, and the roster safety net.
@MainActor
final class LockerTests: XCTestCase {
    private var savedPlayers: [Player] = []
    private var savedIndex = 0

    override func setUp() {
        let s = SportsSession.shared
        savedPlayers = s.players; savedIndex = s.playerIndex
        s.players = [Player(name: "Tester", colorIndex: 0)]; s.playerIndex = 0
    }
    override func tearDown() {
        let s = SportsSession.shared
        s.players = savedPlayers; s.playerIndex = savedIndex
        TennisMenu.shared.debugShow(.title)
    }

    // MARK: catalog and saves

    func testNoGearShipsYetOnlyStandardItems() {
        for sport in LockerCatalog.sports {
            for slot in LockerCatalog.slots(for: sport) {
                let items = LockerCatalog.items(sport: sport, slot: slot)
                XCTAssertEqual(items.count, 1, "\(sport) \(slot): the shelf holds exactly the Standard item")
                XCTAssertTrue(items[0].isStandard)
            }
        }
        XCTAssertEqual(LockerCatalog.slots(for: .tennis), [.skin, .racket, .shoes])
        XCTAssertEqual(LockerCatalog.slots(for: .golf), [.skin, .club, .shoes])
    }

    func testLoadoutSurvivesSavingAndOldProfilesStillDecode() throws {
        var p = Player(name: "Old", colorIndex: 0)
        XCTAssertNil(p.loadout)
        let old = try JSONEncoder().encode(p)
        XCTAssertFalse(String(decoding: old, as: UTF8.self).contains("loadout"), "an untouched player writes no loadout key")
        // A profile saved before the field existed decodes with nil.
        let decoded = try JSONDecoder().decode(Player.self, from: old)
        XCTAssertNil(decoded.loadout)
        // Equipping Standard keeps the save clean; a real id would be stored per sport and slot.
        p.equip(LockerCatalog.item(id: nil, sport: .tennis, slot: .racket), sport: .tennis)
        XCTAssertNil(p.loadout)
        let ghost = LockerItem(id: "future-racket", slot: .racket, name: "Future", sports: [.tennis], tintable: true)
        p.equip(ghost, sport: .tennis)
        XCTAssertEqual(p.loadout?["tennis"]?["racket"], "future-racket")
        let again = try JSONDecoder().decode(Player.self, from: JSONEncoder().encode(p))
        XCTAssertEqual(again.loadout, p.loadout)
        // The game never sees an id the catalog does not have: it falls back to Standard.
        XCTAssertTrue(again.equipped(.racket, sport: .tennis).isStandard)
        XCTAssertEqual(again.loadoutPayload(sport: .tennis), ["skin": "standard", "racket": "standard", "shoes": "standard"])
        XCTAssertEqual(again.loadoutPayload(sport: .golf), ["skin": "standard", "club": "standard", "shoes": "standard"])
    }

    func testAnUnreadableRosterIsKeptNotOverwritten() throws {
        let suite = UserDefaults(suiteName: "LockerTests.roster")!
        suite.removePersistentDomain(forName: "LockerTests.roster")
        defer { suite.removePersistentDomain(forName: "LockerTests.roster") }
        let garbage = Data("{ not a roster".utf8)
        suite.set(garbage, forKey: "players.v1")
        let loaded = PlayerRosterStore.load(defaults: suite)
        XCTAssertEqual(loaded.count, 1, "a fresh default player is returned")
        let backups = suite.dictionaryRepresentation().filter { $0.key.hasPrefix(PlayerRosterStore.unreadablePrefix) }
        XCTAssertEqual(backups.count, 1)
        XCTAssertEqual(backups.first?.value as? Data, garbage, "the old bytes are preserved")
        PlayerRosterStore.save(loaded, defaults: suite)   // the next save overwrites players.v1 but not the backup
        XCTAssertEqual(suite.dictionaryRepresentation().filter { $0.key.hasPrefix(PlayerRosterStore.unreadablePrefix) }.count, 1)
    }

    // MARK: focus grid

    func testLockerOpensOnTheShelfAndTheGridFollowsTheTab() {
        let menu = TennisMenu.shared
        menu.debugShow(.character)
        XCTAssertEqual(menu.lockerTab, .gear)
        XCTAssertEqual(menu.focused, "lk-item-standard", "opens on the shelf, on the item you wear")
        let gear = menu.rows(.character)
        XCTAssertEqual(gear[0], ["lk-sport-tennis", "lk-sport-golf"])
        XCTAssertEqual(gear[1], ["lk-tab-gear", "lk-tab-customize"])
        XCTAssertEqual(gear[2], ["lk-slot-skin", "lk-slot-racket", "lk-slot-shoes"])
        XCTAssertEqual(gear[3], ["lk-item-standard"])
        XCTAssertFalse(gear.contains(["lk-colour"]), "the Standard skin has no colour row: colours live in Customize")
        menu.tap("lk-slot-racket")
        XCTAssertTrue(menu.rows(.character).contains(["lk-colour"]), "a racket has a colour row")
        menu.tap("lk-tab-customize")
        XCTAssertEqual(menu.rows(.character).dropFirst().prefix(5).map { $0 }, [["lk-body"], ["lk-hand"], ["lk-skin"], ["lk-shirt"], ["lk-shorts"]])
    }

    func testSportToggleSwapsRacketForClubAndBack() {
        let menu = TennisMenu.shared
        menu.debugShow(.character)
        menu.tap("lk-slot-racket"); XCTAssertEqual(menu.lockerSlot, .racket)
        menu.tap("lk-sport-golf"); XCTAssertEqual(menu.lockerSport, .golf)
        XCTAssertEqual(menu.lockerSlot, .club, "the racket slot becomes the club slot for golf")
        XCTAssertEqual(menu.rows(.character)[2], ["lk-slot-skin", "lk-slot-club", "lk-slot-shoes"])
        menu.tap("lk-sport-tennis"); XCTAssertEqual(menu.lockerSlot, .racket)
    }

    func testClickingAlongSwatchesChangesColourAndRevertRestores() throws {
        let menu = TennisMenu.shared, session = SportsSession.shared
        menu.debugShow(.character)
        let opening = session.players[0]
        XCTAssertFalse(menu.lockerDirty)
        menu.tap("lk-tab-customize"); XCTAssertTrue(menu.focus("lk-shirt"))
        XCTAssertEqual(menu.lockerSwatchIndex("shirt"), -1, "starts on the kit colour")
        menu.move(.right)
        XCTAssertEqual(menu.lockerSwatchIndex("shirt"), 0, "one click right = the first swatch")
        XCTAssertEqual(session.players[0].outfitHex("shirt"), "F2F2F0", "a swatch is stored as its exact colour, so White is really white")
        menu.move(.right)
        XCTAssertEqual(menu.lockerSwatchIndex("shirt"), 1)
        menu.move(.left); menu.move(.left)
        XCTAssertEqual(menu.lockerSwatchIndex("shirt"), -1, "wraps back to the kit colour")
        menu.move(.right)
        XCTAssertTrue(menu.lockerDirty)
        XCTAssertTrue(menu.rows(.character).last?.contains("lk-revert") == true, "Revert appears once something changed")
        menu.tap("lk-revert")
        XCTAssertEqual(session.players[0], opening)
        XCTAssertFalse(menu.lockerDirty)
    }

    func testCustomRangeOpensClosesAndBackLeavesItBeforeTheLocker() {
        let menu = TennisMenu.shared
        menu.debugShow(.character)
        menu.tap("lk-tab-customize"); menu.tap("lk-shirt")
        XCTAssertEqual(menu.lockerRange, "shirt")
        XCTAssertEqual(menu.rows(.character), [["lk-range-hue"], ["lk-range-shade"], ["lk-range-close"]])
        let before = SportsSession.shared.players[0].hueShade("shirt").0
        menu.move(.right)
        XCTAssertNotEqual(SportsSession.shared.players[0].hueShade("shirt").0, before, "right on Hue moves the hue")
        menu.back()
        XCTAssertNil(menu.lockerRange)
        XCTAssertEqual(menu.screen, .character, "Back closes the range first, then the locker")
        XCTAssertEqual(menu.focused, "lk-shirt", "focus returns to the row that opened it")
        menu.back(); XCTAssertEqual(menu.screen, .main)
    }

    func testShuffleChangesColoursButNeverGear() {
        let menu = TennisMenu.shared, session = SportsSession.shared
        menu.debugShow(.character)
        menu.tap("lk-shuffle")
        XCTAssertTrue(menu.lockerDirty)
        XCTAssertNil(session.players[0].loadout, "shuffle never touches equipped gear")
    }

    func testHomeContinuesAndHasNoStore() {
        let menu = TennisMenu.shared
        menu.debugShow(.main)
        XCTAssertEqual(menu.rows(.main).first, ["homeContinue"])
        XCTAssertEqual(menu.rows(.main).flatMap { $0 }, ["homeContinue", "play", "character", "settings"])
    }
}
