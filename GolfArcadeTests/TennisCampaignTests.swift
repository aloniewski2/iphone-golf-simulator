import XCTest
@testable import GolfArcade

@MainActor
final class TennisCampaignTests: XCTestCase {
    private var saved = 0

    override func setUp() async throws {
        saved = UserDefaults.standard.integer(forKey: "tennis.campaign.won.v2")
        TennisCampaign.shared.restart()
    }

    override func tearDown() async throws {
        UserDefaults.standard.set(saved, forKey: "tennis.campaign.won.v2")
        TennisMenu.shared.debugShow(.title)
    }

    func testLobbyRemoteReachesEveryActionAndRoutesSports() {
        let menu = TennisMenu.shared
        let session = SportsSession.shared
        let connected = session.displayConnected
        session.displayConnected = false
        defer { session.displayConnected = connected }
        menu.debugShow(.title); menu.tap("start")
        XCTAssertEqual(menu.focused, "play", "home lands on PLAY")
        menu.select(); XCTAssertEqual(menu.screen, .gameSelect)
        menu.back(); XCTAssertEqual(menu.screen, .main)
        menu.move(.down); XCTAssertEqual(menu.focused, "homeCampaign")
        menu.move(.down); XCTAssertEqual(menu.focused, "character")
        menu.move(.down); XCTAssertEqual(menu.focused, "settings")
        menu.debugShow(.exhibition)
        menu.quickOpponent = 0; menu.quickDifficulty = 2; menu.quickLength = 2
        menu.tap("quickStart"); XCTAssertEqual(menu.screen, .map)
        XCTAssertEqual(menu.launch?.mode, .exhibition)
        XCTAssertEqual(menu.launch?.difficulty, 0.8)
        XCTAssertEqual(menu.launch?.sets, 2)
        XCTAssertEqual(menu.launch?.games, 6)
        menu.tap("map-resort"); XCTAssertEqual(menu.screen, .connect)
        XCTAssertEqual(SportsSession.shared.tennisVenue, "resort")
        menu.debugShow(.quickPlay); menu.tap("quickGolf")
        XCTAssertEqual(menu.launch?.sport, .golf)
    }

    func testContinuousBodySizeMigrationAndSaving() throws {
        var p = Player(name: "Size", colorIndex: 0)
        p.buildChoice = 4
        XCTAssertEqual(p.bodySize, 1)
        p.bodySize = 0.327
        let saved = try JSONDecoder().decode(Player.self, from: JSONEncoder().encode(p))
        XCTAssertEqual(saved.bodySize, 0.327, accuracy: 0.0001)
        p.bodySize = -1; XCTAssertEqual(p.bodySize, 0)
        p.bodySize = 2; XCTAssertEqual(p.bodySize, 1)
        XCTAssertNotNil(CharacterMeshData.load("PlayerMaleSkin"))
        XCTAssertNotNil(CharacterMeshData.load("PlayerFemaleSkin"))
    }

    func testCharacterAppearanceSurvivesSavingAndOldProfilesStillLoad() throws {
        var player=Player(name: "Custom",colorIndex: 0)
        player.hairStyle=3;player.hairColor=5;player.faceShape=2;player.heightChoice=4;player.buildChoice=0;player.standardSkin=5
        let data=try JSONEncoder().encode(player)
        XCTAssertEqual(try JSONDecoder().decode(Player.self,from:data),player)
        var json=try XCTUnwrap(JSONSerialization.jsonObject(with:data) as? [String:Any])
        for key in ["hairStyleValue","hairColorValue","faceShapeValue","heightValue","buildValue"] { json.removeValue(forKey:key) }
        let old=try JSONDecoder().decode(Player.self,from:JSONSerialization.data(withJSONObject:json))
        XCTAssertEqual(old.hairStyle,1);XCTAssertEqual(old.heightChoice,2);XCTAssertEqual(old.buildChoice,2)
    }

    func testPhoneReplayKeepsTheSameRoundAfterLoss() {
        let session = SportsSession.shared, menu = TennisMenu.shared
        let connected = session.displayConnected
        session.displayConnected = false
        defer { session.active = false; session.finishedMatch = nil; session.displayConnected = connected }
        menu.debugShow(.loading, launch: MenuLaunch(round: 0))
        session.active = true; session.finishedMatch = nil
        session.receiveMatchSnapshot(["matchComplete": true, "matchWon": false, "finalScore": "0–3"])
        XCTAssertEqual(menu.afterMatchChoices, [.replay, .menu])
        session.advanceAfterMatch(.replay)
        XCTAssertEqual(menu.launch?.round, 0)
        XCTAssertNil(session.finishedMatch)
        XCTAssertNotEqual(menu.screen, .postMatch)
        XCTAssertNotEqual(menu.screen, .story)
    }

    func testHeartbeatCompletesControllerWithoutResultEventAndActionsPersist() async throws {
        let session = SportsSession.shared, menu = TennisMenu.shared
        menu.debugShow(.loading, launch: MenuLaunch(round: 0))
        session.active = true; session.finishedMatch = nil
        defer { session.active = false; session.finishedMatch = nil }
        session.receiveMatchSnapshot(["matchComplete": false, "matchWon": true])
        XCTAssertNil(session.finishedMatch, "no post-game actions during a live match")
        session.receiveMatchSnapshot(["matchComplete": true, "matchWon": true, "finalScore": "3–0"])
        XCTAssertEqual(session.finishedMatch?.score, "3–0")
        XCTAssertEqual(menu.afterMatchChoices, [.next, .replay, .menu])
        try await Task.sleep(for: .seconds(3.6))
        XCTAssertNotNil(session.finishedMatch, "actions must stay on phone, not disappear into a TV-only rewards screen")
        session.advanceAfterMatch(.menu)
        XCTAssertFalse(session.active)
        XCTAssertNil(session.finishedMatch)
        XCTAssertNotEqual(menu.screen, .story)
        XCTAssertNotEqual(menu.screen, .postMatch)
    }

    func testFinishWaitsForContinueAndDuplicateEventsDoNotAdvanceAgain() async throws {
        let session = SportsSession.shared
        let menu = TennisMenu.shared
        menu.debugShow(.loading, launch: MenuLaunch(round: 0))
        session.active = true
        session.finishedMatch = nil
        session.receiveMatchFinish(won: true, score: "3–0")
        XCTAssertTrue(session.active, "Keep the finish screen until Continue is tapped (or the TV moves on)")
        XCTAssertEqual(session.finishedMatch?.score, "3–0")
        XCTAssertEqual(menu.campaign.won, 1)
        session.receiveMatchFinish(won: true, score: "3–0")
        XCTAssertEqual(menu.campaign.won, 1)
        session.showMatchRewards()
        XCTAssertFalse(session.active, "Continue exits racket mode")
        XCTAssertNil(session.finishedMatch)
        XCTAssertEqual(menu.screen, .postMatch, "the rep screen follows the match")
        let summary = try XCTUnwrap(menu.postMatch)
        XCTAssertEqual(menu.focused, "pm-skip", "A skips the show first")
        menu.finishPostMatchReveal()
        XCTAssertEqual(menu.postMatchChoices, ["pm-next", "pm-replay", "pm-menu"], "a campaign win offers the next round")
        menu.tap("pm-menu")
        XCTAssertEqual(menu.screen, .postMatch, "a press right as the show ends is ignored")
        try await Task.sleep(for: .milliseconds(800))
        menu.tap("pm-menu")
        XCTAssertEqual(menu.screen, .main, "Main Menu must go home without a second results screen")
        XCTAssertEqual(menu.focused, "play")
        menu.debugPostMatch(summary, launch: MenuLaunch(round: 0))
        menu.finishPostMatchReveal(immediate: true)
        menu.tap("pm-menu")
        XCTAssertEqual(menu.screen, .main, "The new static results screen accepts the first tap immediately")
    }

    func testDrawAscendsInDifficultyAndEndsWithTheBoss() {
        let draw = TennisCampaign.draw
        XCTAssertEqual(draw.count, 10)
        XCTAssertEqual(draw.map(\.difficulty), draw.map(\.difficulty).sorted(), "each round must be harder than the last")
        XCTAssertEqual(draw.filter(\.boss).map(\.key), ["Viktor"])
        XCTAssertTrue(draw.last!.boss)
        // Keys must match TennisRoster in Unity, which dresses each rival in their own body.
        XCTAssertEqual(draw.map(\.key), ["Milo", "Tama", "Suki", "Dex", "Lina", "Bruno", "Rosa", "Jax", "Nadia", "Viktor"])
        // One short set for the first five, best of three for the next four, best of five for the final.
        XCTAssertEqual(draw.map(\.sets), [1, 1, 1, 1, 1, 2, 2, 2, 2, 3])
        XCTAssertEqual(draw.map(\.games), [3, 3, 3, 3, 3, 6, 6, 6, 6, 6])
        XCTAssertEqual(draw.last!.formatTitle, "Best of 5 sets")
    }

    func testEveryRoundHasAStoryAndCoachingLines() {
        for i in TennisCampaign.draw.indices {
            XCTAssertFalse(TennisStory.before(i).isEmpty, "round \(i) needs a briefing")
            XCTAssertTrue(TennisStory.before(i).contains { $0.text.contains("Warning") } || i == 0, "round \(i) needs a tactic warning")
            XCTAssertGreaterThanOrEqual(TennisStory.changeovers(i).count, 2)
            XCTAssertFalse(TennisStory.afterWin(i).isEmpty); XCTAssertFalse(TennisStory.afterLoss(i).isEmpty)
            XCTAssertFalse(TennisStory.afterLoss(i).contains { $0.text.isEmpty })
        }
        XCTAssertTrue(TennisStory.afterWin(0).contains { $0.text.contains("coach") }, "Ray offers to coach after the first match")
    }

    func testTheBriefingPlaysOnceBeforeARound() {
        let menu = TennisMenu.shared
        menu.debugShow(.campaign)
        menu.play(round: 0)
        XCTAssertEqual(menu.screen, .story)
        XCTAssertEqual(menu.storyLine, TennisStory.before(0).first)
        menu.advanceStory(); XCTAssertEqual(menu.storyIndex, 1)
        menu.back()
        XCTAssertNotEqual(menu.screen, .story, "B skips the scene and carries on to the match")
        menu.debugShow(.campaign)
        menu.play(round: 0)
        XCTAssertNotEqual(menu.screen, .story, "a rematch goes straight back on court")
    }

    func testWinningAdvancesAndLosingReplaysTheRound() {
        let c = TennisCampaign.shared
        XCTAssertTrue(c.unlocked(0)); XCTAssertFalse(c.unlocked(1))
        c.record(round: 0, won: false)
        XCTAssertEqual(c.won, 0, "a loss replays the round")
        c.record(round: 0, won: true)
        XCTAssertEqual(c.won, 1); XCTAssertTrue(c.unlocked(1)); XCTAssertTrue(c.beaten(0))
        c.record(round: 0, won: true)
        XCTAssertEqual(c.won, 1, "replaying a beaten round does not skip ahead")
        for round in 1..<10 { c.record(round: round, won: true) }
        XCTAssertTrue(c.champion)
        XCTAssertEqual(c.nextRound, 9, "the champion can defend in the final")
    }

    func testDPadMovesAcrossTheDrawAndRefusesLockedRounds() {
        let menu = TennisMenu.shared
        menu.debugShow(.campaign)
        XCTAssertEqual(menu.focused, "round0")
        menu.move(.right); XCTAssertEqual(menu.focused, "round1")
        menu.move(.down); XCTAssertEqual(menu.focused, "round6", "the second row of the ladder")
        menu.move(.down); XCTAssertEqual(menu.focused, "back")
        menu.move(.up); menu.move(.up); menu.move(.left); XCTAssertEqual(menu.focused, "round0")
        menu.move(.right); menu.move(.right)
        let refusals = menu.refusals
        menu.select()
        XCTAssertEqual(menu.refusals, refusals + 1, "a locked round must not start")
        XCTAssertEqual(menu.screen, .campaign)
        menu.back(); XCTAssertEqual(menu.screen, .hub(.tennis), "back from the draw goes to the tennis hub")
    }

    func testTheTutorialUnlocksEverythingElseInASport() {
        let menu = TennisMenu.shared, progress = SportProgress.shared
        progress.resetTutorials()
        defer { progress.completeTutorial(.tennis); progress.completeTutorial(.golf) }
        XCTAssertTrue(menu.hubUnlocked(.tennis, "tutorial"))
        for id in ["campaign", "training", "exhibition"] { XCTAssertFalse(menu.hubUnlocked(.tennis, id), id) }
        XCTAssertFalse(menu.hubUnlocked(.golf, "round"))
        menu.debugShow(.hub(.tennis))
        XCTAssertEqual(menu.focused, "tutorial", "a first-timer lands on the tutorial")
        menu.move(.down); XCTAssertEqual(menu.focused, "campaign")
        let refusals = menu.refusals
        menu.select()
        XCTAssertEqual(menu.refusals, refusals + 1, "the campaign stays locked until the tutorial is done")
        XCTAssertEqual(menu.screen, .hub(.tennis))
        progress.completeTutorial(.tennis)
        XCTAssertTrue(menu.hubUnlocked(.tennis, "campaign"))
        XCTAssertFalse(menu.hubUnlocked(.golf, "round"), "each sport has its own tutorial")
        menu.select(); XCTAssertEqual(menu.screen, .campaign)
        XCTAssertFalse(menu.hubUnlocked(.golf, "golfCampaign"), "golf's campaign is coming soon")
    }

    func testGameSelectOffersTheTwoPlayableSports() {
        let menu = TennisMenu.shared
        menu.debugShow(.gameSelect)
        XCTAssertEqual(menu.rows(.gameSelect).first?.count, 2)
        XCTAssertEqual(Sport.allCases.filter(\.playable), [.golf, .tennis])
        menu.tap("sport-tennis"); XCTAssertEqual(menu.screen, .hub(.tennis))
        menu.back(); menu.back(); XCTAssertEqual(menu.screen, .main)
        for sport in Sport.allCases {
            for clip in sport.clips { XCTAssertNotNil(Bundle.main.url(forResource: clip, withExtension: "mp4"), "\(clip).mp4 is bundled") }
        }
    }

    func testMainMenuReachesEverySection() {
        let menu = TennisMenu.shared
        SportProgress.shared.completeTutorial(.tennis)
        for (id, screen) in [("play", MenuScreen.gameSelect), ("character", .character), ("settings", .settings), ("homeCampaign", .campaign)] {
            menu.debugShow(.main); menu.tap(id); XCTAssertEqual(menu.screen, screen, id)
        }
    }

    func testSettingsTabsAndCharacterColours() {
        let menu = TennisMenu.shared, s = SportsSession.shared
        menu.debugShow(.settings, row: 0, column: 0)
        menu.move(.right); XCTAssertEqual(menu.settingsTab, .controls, "moving along the tab row switches tabs")
        XCTAssertEqual(menu.rows(.settings)[1], ["controls"])
        menu.debugShow(.character, row: TennisMenu.characterRows.firstIndex(of: "shirt") ?? 0, column: 0)
        XCTAssertEqual(menu.focused, "shirt")
        let original = s.players[s.playerIndex]
        let before = original.outfitHex("shirt")
        menu.move(.right)
        XCTAssertNotEqual(s.players[s.playerIndex].outfitHex("shirt"), before, "sideways changes the shirt colour")
        s.players[s.playerIndex] = original; s.savePlayers()
    }

    func testLoadingCompletesPromptlyWhenReadyAndExplainsStalls() {
        let loading = LoadingModel()
        let start = Date(timeIntervalSince1970: 1000)
        var finished = false
        loading.onFinish = { finished = true }
        loading.begin(now: start)
        loading.reach(0.2); loading.markReady()        // the game is ready almost at once
        var last = 0.0
        for tenth in 0...200 {
            let now = start.addingTimeInterval(Double(tenth) / 10)
            loading.tick(now: now)
            XCTAssertGreaterThanOrEqual(loading.progress, last, "the bar never goes backwards")
            last = loading.progress
            if Double(tenth) / 10 < LoadingModel.minimum { XCTAssertFalse(finished, "never complete before the short presentation beat"); XCTAssertLessThan(loading.progress, 1) }
        }
        XCTAssertTrue(finished); XCTAssertEqual(loading.progress, 1)
        let fast = LoadingModel(); fast.begin(now: start); fast.markReady()
        fast.tick(now: start.addingTimeInterval(2))
        fast.tick(now: start.addingTimeInterval(2.4))
        XCTAssertTrue(fast.finished, "A ready game should finish inside the 2–4 second target")
        // A slow game holds the bar short of 100% until it is ready.
        let slow = LoadingModel(); slow.begin(now: start)
        slow.tick(now: start.addingTimeInterval(30))
        XCTAssertFalse(slow.finished); XCTAssertLessThanOrEqual(slow.progress, LoadingModel.hold)
        XCTAssertTrue(slow.isStalled)
        XCTAssertTrue(slow.statusText.contains("Retry"))
        slow.markReady(); slow.tick(now: start.addingTimeInterval(30.1)); slow.tick(now: start.addingTimeInterval(30.8))
        XCTAssertTrue(slow.finished)
    }

    func testOldPlayersLoadWithKitColours() throws {
        let old = #"[{"id":"6F9619FF-8B86-D011-B42D-00C04FC964FF","name":"Sam","colorIndex":0,"handedness":"right"}]"#
        let players = try JSONDecoder().decode([Player].self, from: Data(old.utf8))
        XCTAssertNil(players[0].shirt, "no colour chosen keeps the kit's own")
        var p = players[0]; p.shirt = 3; p.racket = 7
        let round = try JSONDecoder().decode(Player.self, from: JSONEncoder().encode(p))
        XCTAssertEqual(round.shirt, 3); XCTAssertEqual(round.racket, 7)
        XCTAssertEqual(Outfit.hex(3), "9EE63A"); XCTAssertEqual(Outfit.hex(nil), "")
    }

    func testChoicesChangeSideways() {
        let menu = TennisMenu.shared
        menu.debugShow(.training)
        let level = menu.trainingLevel
        menu.move(.right)
        XCTAssertNotEqual(menu.trainingLevel, level)
        XCTAssertEqual(menu.focused, "level", "left/right on a choice changes it rather than moving focus")
    }

    func testTimingCheckIsKeptPerTV() {
        let defaults = UserDefaults(suiteName: "timing-test-\(UUID().uuidString)")!
        XCTAssertNil(SportsTiming.stored(for: "Living Room", defaults: defaults))
        SportsTiming.store(0.182, for: "Living Room", defaults: defaults)
        SportsTiming.store(0.9, for: "Bedroom", defaults: defaults)
        XCTAssertEqual(SportsTiming.stored(for: "Living Room", defaults: defaults) ?? 0, 0.182, accuracy: 1e-9)
        XCTAssertEqual(SportsTiming.stored(for: "Bedroom", defaults: defaults), 0.35, "clamped to what the game compensates")
    }

    func testUnityEventsParse() {
        let score = TennisScore(line: "2,1,GAMES 2–1 · 30–15 · YOUR SERVE")
        XCTAssertEqual(score.player, 2); XCTAssertEqual(score.opponent, 1)
        let contact = TennisContact(line: "0.250,-0.500,5,1")
        XCTAssertEqual(contact?.x ?? 0, 0.25, accuracy: 1e-6)
        XCTAssertEqual(contact?.gradeName, "SUPER SHOT")
        XCTAssertNil(TennisContact(line: "garbage"))
        XCTAssertEqual(TennisContact(line: "0,0,3,0,80")?.timingWord, "LATE")
        XCTAssertEqual(TennisContact(line: "0,0,3,0,-60")?.timingWord, "EARLY")
        XCTAssertEqual(TennisContact(line: "0,0,5,0,10")?.timingWord, "ON TIME")
    }

    /// Every menu screen can be reached from the title, by D-pad and select alone.
    func testEveryScreenIsReachableFromTheTitle() {
        let menu = TennisMenu.shared, progress = SportProgress.shared
        let session = SportsSession.shared
        let connected = session.displayConnected
        session.displayConnected = false
        defer { session.displayConnected = connected }
        progress.completeTutorial(.tennis); progress.completeTutorial(.golf)
        var reached: Set<String> = []
        func key(_ s: MenuScreen) -> String { "\(s)" }
        func visit(_ path: [String]) {
            menu.debugShow(.title)
            for id in path { menu.tap(id) }
            reached.insert(key(menu.screen))
        }
        visit([]); visit(["start"])
        visit(["start", "play"])
        visit(["start", "play", "sport-tennis"])
        visit(["start", "play", "sport-golf"])
        visit(["start", "play", "sport-tennis", "campaign"])
        visit(["start", "play", "sport-tennis", "exhibition"])
        visit(["start", "play", "sport-tennis", "training"])
        visit(["start", "play", "sport-golf", "tutorial"])
        visit(["start", "character"])
        visit(["start", "settings"])
        visit(["start", "settings", "tab-display", "howto"])
        visit(["start", "homeCampaign", "campaignPlay"])
        for screen: MenuScreen in [.title, .main, .gameSelect, .hub(.tennis), .hub(.golf), .campaign, .exhibition,
                                   .training, .golfLesson, .character, .settings, .howTo] {
            XCTAssertTrue(reached.contains(key(screen)), "\(screen) is reachable")
        }
        XCTAssertTrue(reached.contains(key(.story)) || reached.contains(key(.connect)), "Continue starts the next thing")
    }

    func testNextMatchStartsNextRoundWithoutUnseenStoryOrDoubleAdvance() async throws {
        let menu = TennisMenu.shared, session = SportsSession.shared, campaign = TennisCampaign.shared
        let connected = session.displayConnected
        session.displayConnected = false
        defer { session.displayConnected = connected; session.active = false; session.finishedMatch = nil }
        campaign.restart()
        menu.debugShow(.loading, launch: MenuLaunch(round: 0))
        session.active = true
        session.finishedMatch = nil
        session.receiveMatchFinish(won: true, score: "3–0")
        XCTAssertFalse(campaign.seen("win0"))
        session.advanceAfterMatch(.next)
        XCTAssertEqual(menu.launch?.round, 1)
        XCTAssertNotEqual(menu.screen, .story, "Next must not stop at an unseen story or another Next screen")
        XCTAssertNil(session.finishedMatch)
        session.advanceAfterMatch()
        XCTAssertEqual(menu.launch?.round, 1, "A duplicate tap must not skip a rival")
    }

    func testAfterAMatchThePhoneOffersNextAndReplay() {
        let menu = TennisMenu.shared, c = TennisCampaign.shared, session = SportsSession.shared
        let connected = session.displayConnected
        session.displayConnected = false
        defer { session.displayConnected = connected }
        SportProgress.shared.completeTutorial(.tennis)
        c.restart(); c.record(round: 0, won: true); c.markSeen("win0"); c.markSeen("pre1")
        menu.debugShow(.loading, launch: MenuLaunch(round: 0), result: MatchResult(won: true, score: "3–1", round: 0))
        XCTAssertEqual(menu.afterMatchChoices, [.next, .replay, .menu])
        menu.finishMatch(.next)
        XCTAssertEqual(menu.launch?.round, 1, "Next match goes on to the next rival")
        menu.debugShow(.loading, launch: MenuLaunch(round: 1), result: MatchResult(won: false, score: "1–3", round: 1))
        XCTAssertEqual(menu.afterMatchChoices, [.replay, .menu], "after a loss: rematch or back")
        menu.finishMatch(.replay)
        XCTAssertEqual(menu.launch?.round, 1, "Rematch replays the same rival")
        menu.debugShow(.loading, launch: MenuLaunch(mode: .exhibition, round: 2))
        XCTAssertEqual(menu.afterMatchChoices, [.replay, .menu])
    }
}
