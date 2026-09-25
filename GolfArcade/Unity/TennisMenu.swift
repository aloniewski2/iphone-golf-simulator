import SwiftUI
import UIKit

/// Where the front end is. The same state drives the menu on the TV (steered from the phone's
/// remote) and on the phone itself when no TV is connected.
enum MenuScreen: Hashable {
    case title, main, gameSelect, hub(Sport), locked(Sport)
    case campaign, training, exhibition, character, settings, howTo, golfLesson
    case connect, loading, results, story
}
enum MenuMove { case up, down, left, right }

/// What the player chose to play.
struct MenuLaunch: Equatable {
    enum Mode: String { case campaign, training, exhibition, tutorial, round }
    var sport: Sport = .tennis
    var mode: Mode
    /// Campaign round, or the rival faced in an exhibition.
    var round: Int?
    init(sport: Sport = .tennis, mode: Mode, round: Int? = nil) { self.sport = sport; self.mode = mode; self.round = round }
    /// A campaign round (non-nil) or tennis training.
    init(round: Int?) { self.init(mode: round == nil ? .training : .campaign, round: round) }
}

struct MatchResult: Equatable { var won: Bool; var score: String; var round: Int }

/// Settings tabs.
enum SettingsTab: String, CaseIterable { case gameplay, controls, display, audio, access, developer
    var title: String {
        switch self {
        case .gameplay: "Gameplay"; case .controls: "Controls"; case .display: "Display"
        case .audio: "Audio"; case .access: "Accessibility"; case .developer: "Developer"
        }
    }
}

typealias ArcadeMenu = TennisMenu

/// The menu's navigation model. Every screen is a small grid of focusable items (rows of ids);
/// the D-pad moves between them, A selects, B goes back. Touch on the phone selects directly.
@MainActor @Observable
final class TennisMenu {
    static let shared = TennisMenu()

    private(set) var screen: MenuScreen = .title
    private(set) var row = 0
    private(set) var column = 0
    private var previous: MenuScreen = .main
    var trainingLevel = 1
    private(set) var launch: MenuLaunch?
    private(set) var result: MatchResult?
    /// Bumped when a selection is refused (a locked item), to shake the focused item.
    private(set) var refusals = 0
    private(set) var notice = ""
    /// The classic multi-sport form (every raw option), from Settings → Developer.
    var classic = false
    private(set) var settingsTab: SettingsTab = .gameplay
    /// Settings → Gameplay → Reset progress asks once more before wiping anything.
    private(set) var confirmingReset = false
    /// The golf lesson's card, and the how-to guide's page.
    private(set) var lessonCard = 0
    private(set) var howToPage = 0
    /// The story scene playing (on the TV and mirrored on the phone), and what follows it.
    private(set) var story: [StoryLine] = []
    private(set) var storyIndex = 0
    private var afterStory: AfterStory = .results
    private enum AfterStory { case play(MenuLaunch), results, hub(Sport) }
    /// Tests: show story lines whole instead of typing them out.
    private(set) var storyInstant = false
    var storyLine: StoryLine? { story.indices.contains(storyIndex) ? story[storyIndex] : nil }

    let campaign = TennisCampaign.shared
    let progress = SportProgress.shared
    private var session: SportsSession { .shared }
    private let tick = UISelectionFeedbackGenerator()

    static let trainingLevels: [(name: String, difficulty: Double)] = [("Relaxed", 0.15), ("Standard", 0.45), ("Tough", 0.8)]
    static let skinTones = ["Fair", "Light", "Tan", "Olive", "Brown", "Deep"]
    /// Items in a sport's hub, in order. The tutorial comes first and unlocks the rest.
    static func hubItems(_ sport: Sport) -> [String] {
        sport == .tennis ? ["tutorial", "campaign", "training", "exhibition"] : ["tutorial", "round", "golfCampaign", "golfTraining"]
    }
    static let characterRows = ["name", "body", "skin", "hand", "shirt", "shorts", "accent", "racket"]
    static func settingsRows(_ tab: SettingsTab) -> [String] {
        switch tab {
        case .gameplay: ["level", "coaching", "resetTips", "resetProgress"]
        case .controls: ["controls", "range", "hand", "relock", "timing"]
        case .display: ["howto", "fps", "overscan"]
        case .audio: ["sound", "haptics"]
        case .access: ["bigText", "reduceMotion"]
        case .developer: ["classic", "bench"]
        }
    }

    // MARK: Layout

    func rows(_ screen: MenuScreen) -> [[String]] {
        switch screen {
        case .title: return [["start"]]
        case .main: return [["play", "character", "settings", "howto"]]
        case .gameSelect: return [Sport.allCases.map { "sport-\($0.rawValue)" }, ["back"]]
        case .hub(let sport): return [Self.hubItems(sport), ["replayTutorial", "back"]]
        case .locked: return [["back"]]
        case .campaign: return [(0..<5).map { "round\($0)" }, (5..<10).map { "round\($0)" }, ["back", "restart"]]
        case .exhibition: return [(0..<5).map { "rival\($0)" }, (5..<10).map { "rival\($0)" }, ["back"]]
        case .story: return [["next", "skip"]]
        case .training: return [["level"], ["start"], ["back"]]
        case .character: return Self.characterRows.map { [$0] } + [["randomize", "reset", "back"]]
        case .settings:
            return [SettingsTab.allCases.map { "tab-\($0.rawValue)" }] + Self.settingsRows(settingsTab).map { [$0] } + [["back"]]
        case .howTo: return [["prev", "nextPage", "back"]]
        case .golfLesson: return [["prev", "nextCard", "back"]]
        case .connect: return [["airplay"], ["phone"], ["back"]]
        case .loading: return []
        case .results:
            guard let result else { return [["menu"]] }
            if result.won && campaign.champion && result.round == TennisCampaign.draw.count - 1 { return [["menu"], ["restart"]] }
            return result.won ? [["continue"], ["menu"]] : [["retry"], ["menu"]]
        }
    }

    var focused: String {
        let grid = rows(screen)
        guard row < grid.count, column < grid[row].count else { return "" }
        return grid[row][column]
    }

    func isFocused(_ id: String) -> Bool { focused == id }

    /// Whether a hub item can be played yet (the tutorial gates the rest).
    func hubUnlocked(_ sport: Sport, _ id: String) -> Bool {
        switch id {
        case "tutorial": return true
        case "golfCampaign", "golfTraining": return false
        default: return progress.finishedTutorial(sport)
        }
    }

    private func show(_ next: MenuScreen) {
        if next != screen, screen != .loading, screen != .connect, screen != .story { previous = screen }
        screen = next; notice = ""; confirmingReset = false
        // Land on the thing you most likely want.
        switch next {
        case .campaign: row = campaign.nextRound / 5; column = campaign.nextRound % 5
        case .hub(let sport): row = 0; column = progress.finishedTutorial(sport) ? 1 : 0
        case .settings: row = 1; column = 0
        case .gameSelect: row = 0; column = 1
        default: row = 0; column = 0
        }
    }

    private func refuse(_ message: String) {
        refusals += 1; notice = message; UINotificationFeedbackGenerator().notificationOccurred(.error)
    }

    // MARK: Input

    func move(_ direction: MenuMove) {
        let grid = rows(screen)
        guard !grid.isEmpty else { return }
        // Choice rows change their value sideways; on the settings tab row, sideways changes tab.
        if direction == .left || direction == .right, grid[row].count == 1, adjust(focused, by: direction == .left ? -1 : 1) {
            tick.selectionChanged(); return
        }
        var r = row, c = column
        switch direction {
        case .up: r = max(0, r - 1)
        case .down: r = min(grid.count - 1, r + 1)
        case .left: c = max(0, c - 1)
        case .right: c = min(grid[r].count - 1, c + 1)
        }
        c = min(c, grid[r].count - 1)
        if r != row || c != column {
            row = r; column = c; tick.selectionChanged()
            // Moving along the tab row switches tabs, like a controller's shoulder buttons.
            if screen == .settings, r == 0, let tab = SettingsTab.allCases[safe: c] { settingsTab = tab }
        }
    }

    /// Touch: focus an item and act on it.
    func tap(_ id: String) {
        for (r, items) in rows(screen).enumerated() {
            if let c = items.firstIndex(of: id) { row = r; column = c }
        }
        select()
    }

    func select() {
        let id = focused
        UIImpactFeedbackGenerator(style: .medium).impactOccurred()
        switch id {
        case "start" where screen == .title: show(.main)
        case "play": show(.gameSelect)
        case "character": show(.character)
        case "settings": show(.settings)
        case "howto": howToPage = 0; show(.howTo)
        case let s where s.hasPrefix("sport-"):
            guard let sport = Sport(rawValue: String(s.dropFirst(6))) else { return }
            show(sport.playable ? .hub(sport) : .locked(sport))
        case let t where t.hasPrefix("tab-"):
            settingsTab = SettingsTab(rawValue: String(t.dropFirst(4))) ?? .gameplay
        // Hubs.
        case "tutorial", "replayTutorial":
            guard case .hub(let sport) = screen else { return }
            startTutorial(sport)
        case "campaign", "training", "exhibition", "round", "golfCampaign", "golfTraining":
            guard case .hub(let sport) = screen else {
                if id == "campaign" { show(.campaign) }; return
            }
            guard hubUnlocked(sport, id) else {
                refuse(id.hasPrefix("golf") ? "Coming soon" : "Finish the tutorial first — it only takes a few minutes")
                return
            }
            switch id {
            case "campaign": show(.campaign)
            case "training": show(.training)
            case "exhibition": show(.exhibition)
            default: begin(MenuLaunch(sport: .golf, mode: .round))
            }
        case let r where r.hasPrefix("round"):
            let round = Int(r.dropFirst(5)) ?? 0
            if campaign.unlocked(round) { play(round: round) }
            else { refuse("Beat \(TennisCampaign.draw[round - 1].name) to unlock") }
        case let r where r.hasPrefix("rival"):
            let round = Int(r.dropFirst(5)) ?? 0
            if campaign.unlocked(round) { begin(MenuLaunch(mode: .exhibition, round: round)) }
            else { refuse("Reach \(TennisCampaign.draw[round].name) in the campaign to unlock") }
        case "restart":
            campaign.restart(); result = nil
            show(.campaign)
        case "start" where screen == .training: begin(MenuLaunch(mode: .training))
        case "randomize": randomize()
        case "reset":
            session.players[session.playerIndex].shirt = nil; session.players[session.playerIndex].shorts = nil
            session.players[session.playerIndex].accent = nil; session.players[session.playerIndex].racket = nil
            session.savePlayers()
        case "resetProgress":
            if confirmingReset {
                confirmingReset = false; progress.resetTutorials(); campaign.restart(); notice = "Progress reset"
            } else { confirmingReset = true; notice = "Press again to erase tutorials and campaign progress" }
        case "resetTips": UserDefaults.standard.set(true, forKey: "sports.resetCoaching"); notice = "Coaching tips will show again"
        case "relock": notice = "The court direction is set again at the start of your next match"; session.motion.clearAxis()
        case "timing": session.forceTimingCheckNextMatch(); notice = "The timing check runs at the start of your next match"
        case "classic": classic = true
        case "prev": if screen == .howTo { howToPage = max(0, howToPage - 1) } else { lessonCard = max(0, lessonCard - 1) }
        case "nextPage": howToPage = min(HowTo.pages.count - 1, howToPage + 1)
        case "nextCard":
            if lessonCard + 1 < GolfLesson.cards.count { lessonCard += 1 }
            else { begin(MenuLaunch(sport: .golf, mode: .tutorial)) }
        case "phone": if let launch { begin(launch, onPhone: true) }
        case "continue":
            if let result, !campaign.champion, result.won { play(round: campaign.nextRound) } else { show(.campaign) }
        case "retry": if let launch { begin(launch) }
        case "next": advanceStory()
        case "skip": finishStory()
        case "menu": show(launch.map(hubAfter) ?? .main)
        case "back": back()
        default: _ = adjust(id, by: 1)
        }
    }

    func back() {
        switch screen {
        case .title, .loading: return
        case .main: show(.title)
        case .gameSelect, .character, .settings, .howTo: show(.main)
        case .hub, .locked: show(.gameSelect)
        case .campaign, .training, .exhibition: show(.hub(.tennis))
        case .golfLesson: show(.hub(.golf))
        case .connect: show(launch.map(hubAfter) ?? .main)
        case .results: show(.campaign)
        case .story: finishStory()
        }
    }

    /// Where the menu goes back to after a launch.
    private func hubAfter(_ launch: MenuLaunch) -> MenuScreen {
        switch launch.mode {
        case .campaign: .campaign
        case .training: .training
        case .exhibition: .exhibition
        case .tutorial, .round: .hub(launch.sport)
        }
    }

    /// Sideways on a choice; returns false for items that are not choices.
    private func adjust(_ id: String, by step: Int) -> Bool {
        let s = session
        let playerItems = ["body", "skin", "hand", "shirt", "shorts", "accent", "racket"]
        guard s.players.indices.contains(s.playerIndex) || !playerItems.contains(id) else { return false }
        func cycle(_ value: Int, _ count: Int) -> Int { (value + step + count) % count }
        /// Colours cycle through the palette and "kit colour" (nil).
        func cycleColor(_ value: Int?) -> Int? {
            let n = Outfit.palette.count + 1
            let next = cycle((value ?? -1) + 1, n) - 1
            return next < 0 ? nil : next
        }
        let p = s.playerIndex
        switch id {
        case "level": trainingLevel = cycle(trainingLevel, Self.trainingLevels.count); s.tennisDifficulty = Self.trainingLevels[trainingLevel].difficulty
        case "body": s.players[p].standardFemale.toggle(); s.savePlayers()
        case "skin": s.players[p].standardSkin = cycle(s.players[p].standardSkin, Self.skinTones.count); s.savePlayers()
        case "hand": s.players[p].handedness = s.players[p].handedness == .left ? .right : .left; s.savePlayers()
        case "shirt": s.players[p].shirt = cycleColor(s.players[p].shirt); s.savePlayers()
        case "shorts": s.players[p].shorts = cycleColor(s.players[p].shorts); s.savePlayers()
        case "accent": s.players[p].accent = cycleColor(s.players[p].accent); s.savePlayers()
        case "racket": s.players[p].racket = cycleColor(s.players[p].racket); s.savePlayers()
        case "controls": s.touch.toggle()
        case "sound": s.sound.toggle()
        case "haptics": s.haptics.toggle()
        case "coaching": s.coachingTips.toggle()
        case "fps": s.highFrameRate.toggle()
        case "range": s.travel = min(1.2, max(0.3, s.travel + Double(step) * 0.1))
        case "overscan": s.overscan = min(0.1, max(0, s.overscan + Double(step) * 0.02))
        case "bigText": s.bigText.toggle()
        case "reduceMotion": s.reduceMotion.toggle()
        case "bench": notice = "Launch with -benchTennis to run the benchmark"
        default: return false
        }
        return true
    }

    private func randomize() {
        let s = session, p = s.playerIndex
        guard s.players.indices.contains(p) else { return }
        s.players[p].standardFemale = Bool.random()
        s.players[p].standardSkin = Int.random(in: 0..<Self.skinTones.count)
        s.players[p].shirt = Int.random(in: 0..<Outfit.palette.count)
        s.players[p].shorts = Int.random(in: 0..<Outfit.palette.count)
        s.players[p].accent = Int.random(in: 0..<Outfit.palette.count)
        s.players[p].racket = Int.random(in: 0..<Outfit.palette.count)
        s.savePlayers()
    }

    // MARK: Tutorials

    /// Tennis: Ray's on-court lesson. Golf: the lesson cards, then one practice shot.
    func startTutorial(_ sport: Sport) {
        switch sport {
        case .tennis:
            let beat = "tutorialIntro"
            if campaign.seen(beat) { begin(MenuLaunch(mode: .tutorial)); return }
            campaign.markSeen(beat)
            tell(TennisStory.tutorialIntro, then: .play(MenuLaunch(mode: .tutorial)))
        case .golf: lessonCard = 0; show(.golfLesson)
        default: break
        }
    }

    /// Unity finished the tutorial (tennis: every step; golf: the practice shot).
    func tutorialFinished() {
        guard let launch, launch.mode == .tutorial else { return }
        let first = !progress.finishedTutorial(launch.sport)
        progress.completeTutorial(launch.sport)
        Task { @MainActor in
            try? await Task.sleep(for: .seconds(4))
            if session.active { session.end() }
            if launch.sport == .tennis && first { tell(TennisStory.tutorialDone, then: .hub(.tennis)) }
        }
    }

    // MARK: Story

    /// A campaign round: Ray's briefing and the rival's taunt first (once per round, so a
    /// rematch goes straight back on court), then the match.
    func play(round: Int) {
        let beat = "pre\(round)"
        let lines = TennisStory.before(round)
        if campaign.seen(beat) || lines.isEmpty { begin(MenuLaunch(round: round)); return }
        campaign.markSeen(beat)
        tell(lines, then: .play(MenuLaunch(round: round)))
    }

    private func tell(_ lines: [StoryLine], then next: AfterStory) {
        story = lines; storyIndex = 0; afterStory = next
        show(.story); column = 0
    }

    func advanceStory() {
        if storyIndex + 1 < story.count { storyIndex += 1; tick.selectionChanged() } else { finishStory() }
    }

    func finishStory() {
        guard screen == .story else { return }
        story = []; storyIndex = 0
        switch afterStory {
        case .play(let launch): begin(launch)
        case .results: show(.results)
        case .hub(let sport): show(.hub(sport))
        }
    }

    // MARK: Matches

    /// Play: straight to the TV if one is connected, otherwise ask to connect one (or play on
    /// the phone itself).
    func begin(_ launch: MenuLaunch, onPhone: Bool = false) {
        self.launch = launch; result = nil
        guard onPhone || session.displayConnected else { show(.connect); return }
        show(.loading)
        if launch.sport == .golf {
            session.startGolf(tutorial: launch.mode == .tutorial, preview: onPhone)
        } else {
            let opponent = launch.round.map { TennisCampaign.draw[$0] }
            let difficulty = launch.mode == .training ? Self.trainingLevels[trainingLevel].difficulty
                : launch.mode == .tutorial ? 0.1 : opponent?.difficulty ?? session.tennisDifficulty
            session.startTennis(opponent: launch.mode == .tutorial || launch.mode == .training ? nil : opponent,
                                round: launch.mode == .campaign ? launch.round : nil,
                                mode: launch.mode.rawValue, difficulty: difficulty,
                                coach: launch.mode == .campaign ? launch.round.map { TennisStory.changeovers($0) } ?? [] : [],
                                preview: onPhone)
        }
        if !session.active { show(previous); notice = session.status }
    }

    /// A TV appeared while waiting on the connect screen: carry on to it.
    func displayChanged(connected: Bool) {
        if connected, screen == .connect, let launch { begin(launch) }
    }

    /// Unity reported the end of a match. Its own results card plays first; then the session
    /// closes and the menu shows where that leaves the draw.
    func matchFinished(won: Bool, score: String) {
        guard let launch else { return }
        progress.recordMatch(won: won)
        if launch.mode == .campaign, let round = launch.round {
            campaign.record(round: round, won: won)
            result = MatchResult(won: won, score: score, round: round)
        }
        Task { @MainActor in
            try? await Task.sleep(for: .seconds(5))
            if session.active { session.end() }
        }
    }

    /// The game session closed (finished, quit, or failed to load).
    func sessionEnded() {
        if let result {
            // The story carries on after the match: the first win of each round once, a
            // loss every time (it is short, and it repeats the tip).
            let beat = "win\(result.round)"
            if result.won, !campaign.seen(beat) {
                campaign.markSeen(beat); tell(TennisStory.afterWin(result.round), then: .results)
            } else if !result.won {
                tell(TennisStory.afterLoss(result.round), then: .results)
            } else { show(.results) }
        }
        else if screen == .story { return }
        else if screen == .loading || screen == .connect || launch != nil { show(launch.map(hubAfter) ?? .main) }
    }

    var loadingOpponent: TennisOpponent? {
        guard let launch, launch.sport == .tennis, launch.mode == .campaign || launch.mode == .exhibition else { return nil }
        return launch.round.map { TennisCampaign.draw[$0] }
    }

    #if DEBUG
    /// Tests: put the menu in a given state without playing through to it.
    func debugShow(_ screen: MenuScreen, launch: MenuLaunch? = nil, result: MatchResult? = nil, row: Int = 0, column: Int = 0,
                   tab: SettingsTab = .gameplay, page: Int = 0) {
        self.launch = launch; self.result = result; settingsTab = tab; howToPage = page; lessonCard = page
        self.screen = screen; self.row = row; self.column = column; notice = ""
    }
    func debugStory(_ lines: [StoryLine], index: Int = 0) {
        story = lines; storyIndex = index; afterStory = .results; storyInstant = true
        screen = .story; row = 0; column = 0; notice = ""
    }
    func debugLaunch(_ launch: MenuLaunch?) { self.launch = launch }
    #endif
}

extension Array {
    subscript(safe index: Int) -> Element? { indices.contains(index) ? self[index] : nil }
}
