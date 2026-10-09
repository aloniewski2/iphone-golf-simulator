import SwiftUI
import UIKit

/// Where the front end is. The same state drives the menu on the TV (steered from the phone's
/// remote) and on the phone itself when no TV is connected.
enum MenuScreen: Hashable {
    case title, main, party, multiplayer, localChoice, onlineChoice, homeEmotes, quickPlay, gameSelect, hub(Sport), locked(Sport)
    case campaign, training, exhibition, character, settings, howTo, golfLesson
    case connect, loading, results, story
    /// Choose the court or course before the game loads.
    case map
    /// After a match: the result and the XP it earned.
    case postMatch
    case online(OnlineLobbyScreen)
}
enum OnlineLobbyScreen: String, Hashable { case entry, nearby, searching, lobby, emotes, clothes, settings, loading, match, results, leave }
extension MenuScreen { var isOnline: Bool { if case .online = self { return true }; return false } }
enum MenuMove { case up, down, left, right }

/// What the player chose to play.
struct MenuLaunch: Equatable {
    enum Mode: String { case campaign, training, exhibition, tutorial, round }
    var sport: Sport = .tennis
    var mode: Mode
    /// Campaign round, or the rival faced in an exhibition.
    var round: Int?
    var difficulty: Double?
    var sets: Int?
    var games: Int?
    init(sport: Sport = .tennis, mode: Mode, round: Int? = nil) { self.sport = sport; self.mode = mode; self.round = round }
    /// A campaign round (non-nil) or tennis training.
    init(round: Int?) { self.init(mode: round == nil ? .training : .campaign, round: round) }
}

struct MatchResult: Equatable { var won: Bool; var score: String; var round: Int }

/// Settings tabs.
enum SettingsTab: String, CaseIterable { case gameplay, controls, display, audio, access, developer
    /// The tabs a player sees. Developer (classic menu, benchmark) is for debug builds only.
    static var visible: [SettingsTab] {
        #if DEBUG
        return allCases
        #else
        return allCases.filter { $0 != .developer }
        #endif
    }
    var title: String {
        switch self {
        case .gameplay: "Gameplay"; case .controls: "Controls"; case .display: "Display"
        case .audio: "Audio"; case .access: "Accessibility"; case .developer: "Developer"
        }
    }
}


/// The menu's navigation model. Every screen is a small grid of focusable items (rows of ids);
/// the D-pad moves between them, A selects, B goes back. Touch on the phone selects directly.
@MainActor @Observable
final class TennisMenu {
    static let shared = TennisMenu()
    var online = OnlineLobbyMenu()
    private(set) var homeEmote: String?
    private(set) var homeEmoteSequence = 0
    var homeSport: Sport { session.lastPlayedSport }
    var homeEmotes: [String] { homeSport == .golf ? ["wave", "cheer", "hitPerfect"] : player?.equippedEmotes ?? EmoteCatalog.defaults }
    static func homeEmoteName(_ id: String) -> String {
        switch id { case "cheer": "Cheer"; case "hitPerfect": "Fist Pump"; default: EmoteCatalog.name(id) }
    }
    private(set) var previewSport: Sport = .tennis
    private func updateSportPreview() {
        if screen == .gameSelect, focused.hasPrefix("sport-"), let sport = Sport(rawValue: String(focused.dropFirst(6))) { previewSport = sport }
    }

    private(set) var screen: MenuScreen = .title
    private(set) var row = 0
    private(set) var column = 0
    private var previous: MenuScreen = .main
    private var launchOrigin: MenuScreen?
    var trainingLevel = 1
    var quickOpponent = 0
    var quickDifficulty = 1
    var quickLength = 0
    var selectedRound = 0
    var pendingCampaignRound: Int?
    /// Campaign → More → New tournament asks once before it erases the draw.
    private(set) var confirmingRestart = false

    func confirmCampaignRound() {
        guard let round = pendingCampaignRound else { return }
        pendingCampaignRound = nil
        guard campaign.unlocked(round) else { refuse("Win the previous round to unlock"); return }
        play(round: round)
    }
    private(set) var launch: MenuLaunch?
    /// A match waiting on the court or course picker.
    private var pendingPick: (launch: MenuLaunch, onPhone: Bool)?
    /// The XP screen after a match. Its buttons only work once the show is over (or skipped).
    private(set) var postMatch: PostMatchSummary?
    private(set) var postMatchDone = false
    private(set) var postMatchSkips = 0
    private var postMatchDoneAt = Date.distantPast
    private var showPostMatchAfterEnd = false
    private(set) var result: MatchResult?
    /// Bumped when a selection is refused (a locked item), to shake the focused item.
    private(set) var refusals = 0
    private(set) var notice = ""
    /// The classic multi-sport form (every raw option), from Settings → Developer.
    var classic = false
    private(set) var settingsTab: SettingsTab = .gameplay
    /// The locker: Gear (equip per sport and slot) or Customize (colours). See `lockerRows()` and the extension at the bottom.
    enum LockerTab: String { case gear, customize, emotes }
    private(set) var lockerTab: LockerTab = .gear
    private(set) var lockerEmoteSlot = 0
    private(set) var lockerSport: Sport = .tennis
    private(set) var lockerSlot: LockerSlot = .skin
    /// The colour whose full gradient panel is open (skin / shirt / shorts / accent / racket); nil = closed.
    private(set) var lockerRange: String?
    /// The player as the locker was opened, so Revert can put it back.
    fileprivate var lockerOpening: Player?
    fileprivate var lockerRangeFrom: String?
    /// Settings → Gameplay → Reset progress asks once more before wiping anything.
    private(set) var confirmingReset = false
    /// The golf lesson's card, and the how-to guide's page.
    private(set) var lessonCard = 0
    private(set) var howToPage = 0
    /// The story scene playing (on the TV and mirrored on the phone), and what follows it.
    private(set) var story: [StoryLine] = []
    private(set) var storyIndex = 0
    private var afterStory: AfterStory = .results
    private enum AfterStory { case play(MenuLaunch), round(Int), results, hub(Sport) }
    /// Tests: show story lines whole instead of typing them out.
    private(set) var storyInstant = false
    var storyLine: StoryLine? { story.indices.contains(storyIndex) ? story[storyIndex] : nil }

    let campaign = TennisCampaign.shared
    let progress = SportProgress.shared
    private var session: SportsSession { .shared }
    private let tick = UISelectionFeedbackGenerator()

    static let trainingLevels: [(name: String, difficulty: Double)] = [("Relaxed", 0.15), ("Standard", 0.45), ("Tough", 0.8)]
    static let skinTones = ["Fair", "Light", "Tan", "Olive", "Brown", "Deep"]
    /// Available modes for each sport; every mode asks for a map before loading.
    static func hubItems(_ sport: Sport) -> [String] {
        sport == .tennis ? ["exhibition", "campaign", "training"] : ["round"]
    }
    /// Hero V4 identity only (HeroKit.cs / HeroV4 in CharacterModelPreview.swift): the rows the locked hero supports.
    static let characterRows = ["body", "haircut", "skin", "hair", "hairColor", "hand", "shirt", "shorts", "accent", "racket"]
    static func settingsRows(_ tab: SettingsTab) -> [String] {
        switch tab {
        case .gameplay: ["level", "presentationIntros", "holeFlyover", "presentationBigMoments", "resetProgress"]
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
        case .main: return [["play", "homeInvite"], ["character"], ["homeEmotes"], ["settings"], ["betaFeedback"]]
        // One list: play alone, pass one phone round, friends nearby, or online.
        case .party: return [["partySolo"], ["partyLocalGolf"], ["partyNearby"], ["partyOnline"], ["back"]]
        case .multiplayer: return [["partyOnline"], ["partyLocal"], ["back"]]
        case .localChoice: return [["partyLocalGolf"], ["partyNearby"], ["back"]]
        case .onlineChoice: return [["onlineQuick"], ["homeInvite"], ["back"]]
        case .homeEmotes: return homeEmotes.map { ["home-emote-\($0)"] } + [["back"]]
        case .online(let route): return online.rows(route, menu: self)
        case .quickPlay: return [["quickTennis", "quickGolf"], ["back"]]
        case .gameSelect: return [Sport.allCases.filter(\.playable).map { "sport-\($0.rawValue)" }, ["back"]]
        case .hub(let sport): return Self.hubItems(sport).map { [$0] } + [["back"]]
        case .locked: return [["back"]]
        case .campaign:
            if confirmingRestart { return [["restartNo", "restartYes"]] }
            return [(0..<5).map { "round\($0)" }, (5..<10).map { "round\($0)" }, ["campaignPlay", "campaignMore"], ["back"]]
        case .exhibition: return [["quickOpponent"], ["quickDifficulty"], ["quickLength"], ["quickStart"], ["back"]]
        case .story: return [["next", "skip"]]
        case .map: return [mapChoices.map { "map-\($0.id)" }, ["back"]]
        case .postMatch: return postMatchDone ? postMatchChoices.map { [$0] } : [["pm-skip"]]
        case .training: return [["level"], ["start"], ["back"]]
        case .character: return lockerRows()
        case .settings:
            return [SettingsTab.visible.map { "tab-\($0.rawValue)" }] + Self.settingsRows(settingsTab).map { [$0] } + [["back"]]
        case .howTo: return [["prev", "nextPage", "back"]]
        case .golfLesson: return [["round", "back"]]
        case .connect: return [["back"]]
        case .loading: return (SportsSession.shared.loading.isStalled || SportsSession.shared.loading.phase == .failed) ? [["loadingBack", "loadingRetry"]] : [["loadingBack"]]
        case .results:
            guard let result else { return [["menu"]] }
            if result.won && campaign.champion && result.round == TennisCampaign.draw.count - 1 { return [["menu"], ["restart"]] }
            return result.won ? [["continue"], ["court"], ["menu"]] : [["retry"], ["court"], ["menu"]]
        }
    }

    var focused: String {
        let grid = rows(screen)
        guard row < grid.count, column < grid[row].count else { return "" }
        return grid[row][column]
    }

    func isFocused(_ id: String) -> Bool { focused == id }

    /// Both sports are available without completing a lesson.
    func hubUnlocked(_ sport: Sport, _ id: String) -> Bool {
        Self.hubItems(sport).contains(id)
    }
    var mapSport: Sport { launch?.sport ?? .tennis }
    var mapChoices: [SportMapChoice] { SportMapChoice.choices(for: mapSport) }
    var selectedMap: String { mapSport == .golf ? session.golfCourse : session.tennisVenue }

    /// Bumped on every screen change: drives the stripe wipe and its whoosh.
    private(set) var transitions = 0

    private func show(_ next: MenuScreen) {
        if next != screen, screen != .loading, screen != .connect, screen != .story, screen != .postMatch { previous = screen }
        if next == .postMatch { postMatchDone = false }
        if next != screen { transitions += 1; ClubSound.play("whoosh", volume: 0.35) }
        screen = next; notice = ""; confirmingReset = false; pendingCampaignRound = nil; confirmingRestart = false
        // Land on the thing you most likely want.
        switch next {
        case .main: row = 0; column = 0
        case .campaign: selectedRound = min(campaign.nextRound, TennisCampaign.draw.count - 1); row = 2; column = 0   // on Play Round
        case .hub: row = 0; column = 0
        case .settings: row = 1; column = 0
        case .character, .online(.clothes): lockerOpen()
        case .gameSelect: row = 0; column = Sport.allCases.filter(\.playable).firstIndex(of: homeSport) ?? 0; updateSportPreview()
        case .map: row = 0; column = mapChoices.firstIndex { $0.id == selectedMap } ?? 0   // start on the last court
        default: row = 0; column = 0
        }
    }

    /// Home → Continue: whatever is next for this player. The tennis tutorial first, then the
    /// next Island Circuit round (with its story), then the draw to defend the title.
    var continueLabel: (title: String, subtitle: String) {
        if campaign.champion { return ("Defend your title", "The Island Circuit awaits") }
        let o = campaign.next
        return ("Continue", "\(o.roundTitle) · vs \(o.name)")
    }

    func continueJourney() {
        if campaign.champion { show(.campaign); return }
        play(round: campaign.nextRound)
    }

    /// Notices fade on their own (IslandMenuNotice): clear it if it is still the one that was shown.
    func clearNotice(_ shown: String) { if notice == shown { notice = "" } }

    private func refuse(_ message: String) {
        refusals += 1; notice = message
        if session.haptics { UINotificationFeedbackGenerator().notificationOccurred(.error) }
    }

    // MARK: Input

    func move(_ direction: MenuMove) {
        let grid = rows(screen)
        guard !grid.isEmpty else { return }
        row = min(row, grid.count - 1); column = min(column, max(0, grid[row].count - 1))   // a row can disappear (e.g. Revert)
        // Choice rows change their value sideways; on the settings tab row, sideways changes tab.
        if direction == .left || direction == .right, grid[row].count == 1, adjust(focused, by: direction == .left ? -1 : 1) {
            if session.haptics { tick.selectionChanged() }; return
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
            row = r; column = c; updateSportPreview(); if screen == .campaign, focused.hasPrefix("round"), let index = Int(focused.dropFirst(5)) { selectedRound = index }; if session.haptics { tick.selectionChanged() }; ClubSound.play("tick", volume: 0.4)
            // Moving along the tab row switches tabs, like a controller's shoulder buttons.
            if screen == .settings, r == 0, let tab = SettingsTab.visible[safe: c] { settingsTab = tab }
        }
    }

    /// Pointer and remote focus change the preview without navigating.
    @discardableResult func focus(_ id: String) -> Bool {
        for (r, items) in rows(screen).enumerated() {
            if let c = items.firstIndex(of: id) { row = r; column = c; updateSportPreview(); return true }
        }
        return false
    }

    /// Touch: focus an item and act on it.
    func tap(_ id: String) {
        guard focus(id) else { return }
        select()
    }

    func select() {
        let id = focused
        if screen.isOnline || id == "partyNearby" {
            online.select(id, menu: self); return
        }
        if session.haptics { UIImpactFeedbackGenerator(style: .medium).impactOccurred() }
        ClubSound.play("pop", volume: 0.5)
        switch id {
        case "start" where screen == .title: show(.main)
        case "store": refuse("Store · Coming soon")
        case "homeContinue": continueJourney()
        case "campaignMore": confirmingRestart = true; row = 0; column = 0
        case "restartNo": confirmingRestart = false; _ = focus("campaignPlay")
        case "restartYes": confirmingRestart = false; campaign.restart(); result = nil; show(.campaign)
        case "court":
            // Change court after a match: the same match again, but the court picker first.
            if let launch { result = nil; postMatch = nil; launchOrigin = hubAfter(launch); begin(launch, onPhone: !session.displayConnected) }
        case "homePlay": show(.party)
        case "campaignPlay":
            if campaign.unlocked(selectedRound) { play(round: selectedRound) } else { refuse("Win the previous round to unlock") }
        case "quickStart":
            var match = MenuLaunch(mode: .exhibition, round: quickOpponent)
            match.difficulty = Self.trainingLevels[quickDifficulty].difficulty
            match.sets = quickLength == 2 ? 2 : 1; match.games = quickLength == 0 ? 3 : 6
            begin(match)
        case "quickPlay", "partySolo": show(.gameSelect)
        case "partyLocalGolf": online.select(id, menu: self)
        case "partyMultiplayer": show(.multiplayer)
        case "partyLocal": show(.localChoice)
        case "partyOnline": show(.onlineChoice)
        case "onlineQuick": online.select("partyOnline", menu: self)
        case "homeInvite": online.select("net-invite", menu: self)
        case "homeEmotes": homeEmote = nil; show(.homeEmotes)
        case let emote where emote.hasPrefix("home-emote-"):
            homeEmote = String(emote.dropFirst(11)); homeEmoteSequence += 1
        case "betaFeedback": BetaFeedback.open()
        case "homeCampaign": show(.campaign)
        case "quickTennis": begin(MenuLaunch(mode: .exhibition, round: 0))
        case "quickGolf": begin(MenuLaunch(sport: .golf, mode: .round))
        case "loadingBack": back()
        case "loadingRetry":
            guard let retry = launch else { return }
            let onPhone = !session.displayConnected
            session.end(); begin(retry, onPhone: onPhone, skipMap: true)
        case "play": show(.party)
        case "character": show(.character)
        case "settings": show(.settings)
        case "howto": howToPage = 0; show(.howTo)
        case let s where s.hasPrefix("sport-"):
            guard let sport = Sport(rawValue: String(s.dropFirst(6))) else { return }
            // Golf has one way to play: straight on to its courses.
            if sport == .golf { begin(MenuLaunch(sport: .golf, mode: .round)); return }
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
                refuse("Coming soon")
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
            // Picking a round shows it and puts focus on Play Round; playing is one more click (no dialog a remote can't answer).
            if campaign.unlocked(round) { selectedRound = round; _ = focus("campaignPlay") }
            else { refuse("Beat \(TennisCampaign.draw[max(0, round - 1)].name) to unlock") }
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
            for slot in Player.outfitSlots { session.players[session.playerIndex].clearOutfit(slot) }
            session.savePlayers()
        case "resetProgress":
            if confirmingReset {
                confirmingReset = false; progress.resetTutorials(); campaign.restart(); notice = "Progress reset"
            } else { confirmingReset = true; notice = "Press again to erase campaign progress" }
        case "resetTips": notice = "Coaching is disabled"
        case "replayOnboarding": OnboardingFlow.shared.replay()
        case "relock": notice = "The court direction is set again at the start of your next match"; session.motion.clearAxis()
        case "timing": session.forceTimingCheckNextMatch(); notice = "The timing check runs at the start of your next match"
        case "classic": classic = true
        case let l where l.hasPrefix("lk-"): lockerSelect(l)
        case "prev": if screen == .howTo { howToPage = max(0, howToPage - 1) } else { lessonCard = max(0, lessonCard - 1) }
        case "nextPage": howToPage = min(HowTo.pages.count - 1, howToPage + 1)
        case "nextCard":
            if lessonCard + 1 < GolfLesson.cards.count { lessonCard += 1 }
            else { begin(MenuLaunch(sport: .golf, mode: .tutorial), onPhone: !session.displayConnected || session.touch) }
        case "pm-skip": postMatchSkips += 1
        // A second press of the button that skipped the show must not also pick a choice.
        case "pm-next", "pm-replay", "pm-court", "pm-menu":
            guard Date().timeIntervalSince(postMatchDoneAt) > 0.7 else { return }
            leavePostMatch(id == "pm-next" ? .next : id == "pm-replay" ? .replay : id == "pm-court" ? .court : .menu)
        case let m where m.hasPrefix("map-") && screen == .map:
            // The court is chosen: remember it and go on to the match that was waiting.
            if mapSport == .golf { session.golfCourse = String(m.dropFirst(4)) }
            else { session.tennisVenue = String(m.dropFirst(4)) }
            if let pick = pendingPick { pendingPick = nil; begin(pick.launch, onPhone: pick.onPhone, skipMap: true) }
        case "phone": show(.connect)
        case "continue":
            if let result, !campaign.champion, result.won { play(round: campaign.nextRound) } else { show(.campaign) }
        case "retry": if let launch { begin(launch, skipMap: true) }
        case "next": advanceStory()
        case "skip": finishStory()
        case "menu": show(.main)
        case "back": back()
        default: _ = adjust(id, by: 1)
        }
    }

    func back() {
        switch screen {
        case .online(let route): online.back(route, menu: self)
        case .title: return
        case .loading:
            if OnboardingFlow.shared.active { OnboardingFlow.shared.back(); return }
            let destination = launchOrigin ?? launch.map(hubAfter) ?? .main
            pendingPick = nil; result = nil
            if session.active { session.end() }
            launch = nil; show(destination)
        case .main: show(.title)
        case .character: if lockerRange != nil { lockerCloseRange() } else { show(.main) }
        case .howTo: show(.settings)
        case .party, .settings, .homeEmotes: show(.main)
        case .multiplayer, .quickPlay, .gameSelect, .onlineChoice, .localChoice: show(.party)
        case .hub, .locked: show(.gameSelect)
        case .campaign: if confirmingRestart { confirmingRestart = false; _ = focus("campaignPlay") } else { show(.hub(.tennis)) }
        case .training, .exhibition: show(.hub(.tennis))
        case .golfLesson: show(.hub(.golf))
        case .connect: show(launch.map(hubAfter) ?? .main)
        case .results: show(.campaign)
        case .story:
            story = []
            switch afterStory {
            case .play(let next): show(hubAfter(next))
            case .round: show(.campaign)
            case .results: show(.results)
            case .hub(let sport): show(.hub(sport))
            }
        case .map: pendingPick = nil; show(launchOrigin ?? .main)
        case .postMatch: if postMatchDone { if Date().timeIntervalSince(postMatchDoneAt) > 0.7 { leavePostMatch(.menu) } } else { postMatchSkips += 1 }
        }
    }

    /// Where the menu goes back to after a launch.
    private func hubAfter(_ launch: MenuLaunch) -> MenuScreen {
        if launchOrigin == .quickPlay { return .quickPlay }
        return switch launch.mode {
        case .campaign: .campaign
        case .training: .training
        case .exhibition: .exhibition
        case .tutorial, .round: .hub(launch.sport)
        }
    }

    /// Sideways on a choice; returns false for items that are not choices.
    func adjust(_ id: String, by step: Int) -> Bool {
        let s = session
        let playerItems = ["body", "haircut", "skin", "hair", "hairColor", "face", "height", "build", "hand", "shirt", "shorts", "accent", "racket"]
        guard s.players.indices.contains(s.playerIndex) || !playerItems.contains(id) else { return false }
        func cycle(_ value: Int, _ count: Int) -> Int { (value + step + count) % count }
        /// Colours cycle through the palette and "kit colour" (nil).
        func cycleColor(_ value: Int?) -> Int? {
            let n = Outfit.palette.count + 1
            let next = cycle((value ?? -1) + 1, n) - 1
            return next < 0 ? nil : next
        }
        let p = s.playerIndex
        if id.hasPrefix("lk-") { return lockerAdjust(id, by: step) }
        switch id {
        case "quickOpponent": quickOpponent = cycle(quickOpponent, min(TennisCampaign.draw.count, campaign.nextRound + 1))
        case "quickDifficulty": quickDifficulty = cycle(quickDifficulty, Self.trainingLevels.count)
        case "quickLength": quickLength = cycle(quickLength, 3)
        case "level": trainingLevel = cycle(trainingLevel, Self.trainingLevels.count); s.tennisDifficulty = Self.trainingLevels[trainingLevel].difficulty
        case "body": s.players[p].standardFemale.toggle(); s.savePlayers()
        // colour ranges: the remote walks along the range in small steps (the phone locker has sliders)
        case "skin": s.players[p].setSkin(min(1, max(0, s.players[p].skinT + Double(step) * 0.05))); s.savePlayers()
        case "hair": s.players[p].hairStyle = cycle(s.players[p].hairStyle, 4); s.savePlayers()
        case "haircut": s.players[p].haircut = cycle(min(s.players[p].haircut, HeroV4.offered - 1), HeroV4.offered); s.savePlayers()
        case "hairColor": s.players[p].setHair(natural: min(1, max(0, s.players[p].hairT + Double(step) * 0.05))); s.savePlayers()
        case "face": s.players[p].faceShape = cycle(s.players[p].faceShape, 4); s.savePlayers()
        case "height": s.players[p].heightChoice = cycle(s.players[p].heightChoice, 5); s.savePlayers()
        case "build": setBodySize(s.players[p].bodySize + Double(step) * 0.05)
        case "hand": s.players[p].handedness = s.players[p].handedness == .left ? .right : .left; s.savePlayers()
        case "shirt", "shorts", "accent", "racket":
            let (h, sh) = s.players[p].hueShade(id)
            s.players[p].setOutfit(id, hue: h + Double(step) / 24, shade: sh); s.savePlayers()
        case "controls": s.touch.toggle()
        case "presentationIntros":
            let values = ["full", "short", "off"]
            s.presentationIntros = values[cycle(values.firstIndex(of: s.presentationIntros) ?? 0, values.count)]
        case "holeFlyover": s.holeFlyover.toggle()
        case "presentationBigMoments": s.presentationBigMoments.toggle()
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

    func setBodySize(_ value: Double) {
        let s = session
        guard s.players.indices.contains(s.playerIndex) else { return }
        s.players[s.playerIndex].bodySize = value
        s.savePlayers()
    }

    private func randomize() {
        let s = session, p = s.playerIndex
        guard s.players.indices.contains(p) else { return }
        s.players[p].hairStyle = Int.random(in: 0..<4)
        s.players[p].faceShape = Int.random(in: 0..<4)
        s.players[p].heightChoice = Int.random(in: 0..<5)
        s.players[p].bodySize = Double.random(in: 0...1)
        s.players[p].standardFemale = Bool.random()
        s.players[p].haircut = s.players[p].standardFemale ? [1, 2, 3, 4, 6, 7].filter { $0 < HeroV4.offered }.randomElement()! : [0, 4, 5, 6, 7].filter { $0 < HeroV4.offered }.randomElement()!
        s.players[p].setSkin(Double.random(in: 0...1))
        if Int.random(in: 0..<6) == 0 { s.players[p].setHair(dyeHue: Double.random(in: 0...1), shade: Double.random(in: -0.2...0.4)) }
        else { s.players[p].setHair(natural: Double.random(in: 0...1)) }
        for slot in Player.outfitSlots { s.players[p].setOutfit(slot, hue: Double.random(in: 0...1), shade: Double.random(in: -0.6...0.7)) }
        s.savePlayers()
    }

    func randomizeLook() { randomize() }

    // MARK: Tutorials

    func finishOnboarding(_ sport: Sport) { show(.hub(sport)) }

    /// Compatibility for saved routes and older UI: choose a map for a normal game.
    func startTutorial(_ sport: Sport) {
        begin(MenuLaunch(sport: sport, mode: sport == .golf ? .round : .exhibition, round: sport == .tennis ? 0 : nil))
    }
    func tutorialFinished() {}

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
        if storyIndex + 1 < story.count { storyIndex += 1; if session.haptics { tick.selectionChanged() } } else { finishStory() }
    }

    func finishStory() {
        guard screen == .story else { return }
        story = []; storyIndex = 0
        switch afterStory {
        case .play(let launch): begin(launch)
        case .round(let round): play(round: round)
        case .results: show(.results)
        case .hub(let sport): show(.hub(sport))
        }
    }

    func openCharacterEditor() { classic = false; show(.character) }
    /// Leave the current local screen or game and return to the main menu.
    func goHome() {
        if screen.isOnline {
            if online.service.lobby != nil { showOnline(.leave); return }
            online.cancel(); online.service.stopBrowsing()
        }
        pendingPick = nil; result = nil; postMatch = nil; showPostMatchAfterEnd = false; afterMatch = .menu
        if session.active { session.end() }
        launch = nil; launchOrigin = nil; story = []
        show(.main)
    }

    // MARK: Matches

    /// Play on the connected TV with phone controls, otherwise play on the phone.
    func begin(_ launch: MenuLaunch, onPhone requestedPhone: Bool? = nil, skipMap: Bool = false) {
        let onPhone = false
        var launch = launch
        if launch.mode == .tutorial { launch.mode = launch.sport == .golf ? .round : .exhibition }
        if screen != .connect && screen != .loading && screen != .map && screen != .postMatch { launchOrigin = screen }
        self.launch = launch; result = nil
        // Choose the map first; retries and replays keep the selected map.
        if !skipMap, launch.sport.playable {
            pendingPick = (launch, onPhone); show(.map); return
        }
        SportsDisplays.shared.refresh()
        guard session.displayConnected else { show(.connect); return }
        show(.loading)
        if launch.sport == .golf {
            session.startGolf(tutorial: false, preview: onPhone)
        } else {
            let opponent = launch.round.map { TennisCampaign.draw[$0] }
            let difficulty = launch.mode == .training ? Self.trainingLevels[trainingLevel].difficulty
                : launch.mode == .tutorial ? 0.1 : launch.difficulty ?? opponent?.difficulty ?? session.tennisDifficulty
            session.startTennis(opponent: launch.mode == .tutorial || launch.mode == .training ? nil : opponent,
                                round: launch.mode == .campaign ? launch.round : nil,
                                mode: launch.mode.rawValue, difficulty: difficulty,
                                coach: launch.mode == .campaign ? launch.round.map { TennisStory.changeovers($0) } ?? [] : [],
                                preview: onPhone, sets: launch.sets, games: launch.games)
        }
        if !session.active, screen == .loading { launchFailed(session.status) }
    }

    /// Leave a failed startup with the chosen match available to retry.
    func launchFailed(_ message: String) {
        if let launch {
            pendingPick = (launch, !session.displayConnected)
            show(.map)
        } else { show(.main) }
        notice = message
    }

    /// A TV appeared while waiting on the connect screen: carry on to it.
    func displayChanged(connected: Bool) {
        if connected, screen == .connect, let launch { begin(launch, skipMap: true) }
    }

    /// Record the result once, then keep it visible until the phone's Next button is tapped.
    func matchFinished(won: Bool, score: String) {
        guard session.active, let launch else { return }
        progress.recordMatch(won: won)
        if launch.mode == .campaign, let round = launch.round {
            campaign.record(round: round, won: won)
            result = MatchResult(won: won, score: score, round: round)
        }
        // Bank the XP now; the rep screen shows it once Unity has been closed.
        if launch.mode != .tutorial, session.players.indices.contains(session.playerIndex) {
            let opponent = loadingOpponent
            let difficulty = launch.mode == .training ? Self.trainingLevels[trainingLevel].difficulty : launch.difficulty ?? opponent?.difficulty ?? session.tennisDifficulty
            postMatch = Progression.shared.award(player: session.players[session.playerIndex].id, stats: session.lastMatchStats ?? MatchStats(won: won),
                                                 won: won, score: score, opponent: opponent?.name ?? (launch.mode == .training ? "Coach" : ""),
                                                 practice: launch.mode == .training, difficulty: difficulty)
        }
        // Keep the TV result visible until the player taps Next on the phone.
    }

    /// Explicit device-launch recovery, used only when requested by the player.
    func resumeCampaign(round: Int) {
        progress.completeTutorial(.tennis)
        for completed in 0..<min(max(0, round), TennisCampaign.draw.count) {
            campaign.record(round: completed, won: true)
            campaign.markSeen("win\(completed)")
        }
        show(.campaign)
    }

    /// The game session closed (finished, quit, or failed to load).
    /// What the phone's post-match panel chose.
    enum AfterMatch { case next, replay, court, menu }
    private var afterMatch: AfterMatch = .menu

    /// The post-match panel on the phone: next match, replay, or back to the menus.
    func finishMatch(_ choice: AfterMatch) {
        if choice == .replay { session.prepareRematchPresentation() }
        afterMatch = choice
        showPostMatchAfterEnd = false
        if choice == .menu { result = nil; postMatch = nil }
        session.end()
        if choice == .menu { show(.main) }
    }

    /// Which post-match choices make sense for the match just played.
    var afterMatchChoices: [AfterMatch] {
        guard let launch else { return [.replay, .menu] }
        // Change court is offered wherever a tennis match asks for a court (everything but the tutorial).
        let court: [AfterMatch] = launch.sport.playable ? [.court] : []
        if launch.mode == .campaign, let result {
            return result.won && !campaign.champion ? [.next, .replay] + court + [.menu] : [.replay] + court + [.menu]
        }
        return [.replay] + court + [.menu]
    }

    /// The match is over: close Unity and show the XP screen. Called by the phone's Continue button,
    /// or by itself a few seconds after the last point.
    func beginPostMatch() {
        showPostMatchAfterEnd = postMatch != nil
        session.end()
    }

    /// The rep show finished (or was skipped): the buttons come alive.
    func finishPostMatchReveal(immediate: Bool = false) { postMatchDone = true; postMatchDoneAt = immediate ? .distantPast : Date(); row = 0; column = 0 }

    /// What can be done after the rep screen. Every match offers a next one.
    var postMatchChoices: [String] {
        guard let launch else { return ["pm-menu"] }
        if launch.mode == .campaign, let result {
            return result.won && !campaign.champion ? ["pm-next", "pm-replay", "pm-menu"] : ["pm-replay", "pm-court", "pm-menu"]
        }
        return ["pm-next", "pm-court", "pm-menu"]
    }

    private enum PostChoice { case next, replay, court, menu }
    private func leavePostMatch(_ choice: PostChoice) {
        let onPhone = !session.displayConnected
        switch choice {
        case .next:
            if let launch, launch.mode == .campaign, let r = result, r.won, !campaign.champion {
                // The next round of the draw; the same court.
                campaign.markSeen("win\(r.round)")
                let next = campaign.nextRound
                result = nil; postMatch = nil
                begin(MenuLaunch(round: next), onPhone: onPhone, skipMap: true)
            } else if let launch { result = nil; postMatch = nil; begin(launch, onPhone: onPhone, skipMap: true) }
        case .replay:
            session.prepareRematchPresentation()
            if let launch { result = nil; postMatch = nil; begin(launch, onPhone: onPhone, skipMap: true) }
        case .court:
            if let launch { result = nil; postMatch = nil; launchOrigin = hubAfter(launch); begin(launch, onPhone: onPhone) }
        case .menu:
            postMatch = nil
            if let result, result.won { campaign.markSeen("win\(result.round)") }
            show(.main)
        }
    }

    func sessionEnded() {
        if showPostMatchAfterEnd, postMatch != nil { showPostMatchAfterEnd = false; show(.postMatch); return }
        let choice = afterMatch; afterMatch = .menu
        if choice == .court, let launch {
            // Same match, new court: the court picker comes first.
            result = nil; postMatch = nil; launchOrigin = hubAfter(launch)
            begin(launch, onPhone: !session.displayConnected); return
        }
        if choice == .replay, launch == nil {
            begin(MenuLaunch(mode: .exhibition), onPhone: !session.displayConnected, skipMap: true); return
        }
        if choice == .replay, let launch { result = nil; begin(launch, onPhone: !session.displayConnected, skipMap: true); return }
        if choice == .next, let r = result, r.won, !campaign.champion {
            // A phone Next is an explicit request to play, not a second results/story menu.
            let next = campaign.nextRound
            campaign.markSeen("win\(r.round)")
            result = nil
            SportsDiagnostics.write("postMatch next: launching round \(next)")
            begin(MenuLaunch(round: next), onPhone: !session.displayConnected, skipMap: true)
            return
        }
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

    func returnFromNetworkEntry(_ route: OnlineLobbyScreen) {
        show(route == .entry ? .onlineChoice : .party)
    }

    var loadingOpponent: TennisOpponent? {
        guard let launch, launch.sport == .tennis, launch.mode == .campaign || launch.mode == .exhibition else { return nil }
        return launch.round.map { TennisCampaign.draw[$0] }
    }

    #if DEBUG
    /// Tests: put the menu in a given state without playing through to it.
    func debugShow(_ screen: MenuScreen, launch: MenuLaunch? = nil, result: MatchResult? = nil, row: Int = 0, column: Int = 0,
                   tab: SettingsTab = .gameplay, page: Int = 0) {
        self.launch = launch; self.result = result; launchOrigin = nil; settingsTab = tab; howToPage = page; lessonCard = page
        self.screen = screen; self.row = row; self.column = column; notice = ""; confirmingRestart = false
        if screen == .character, row == 0, column == 0 { lockerOpen() }
    }
    func debugStory(_ lines: [StoryLine], index: Int = 0) {
        story = lines; storyIndex = index; afterStory = .results; storyInstant = true
        screen = .story; row = 0; column = 0; notice = ""
    }
    func debugLaunch(_ launch: MenuLaunch?) { self.launch = launch }
    /// Tests: show the rep screen for a made-up match.
    func debugPostMatch(_ summary: PostMatchSummary, launch: MenuLaunch?) {
        postMatch = summary; postMatchDone = false; self.launch = launch
        screen = .postMatch; row = 0; column = 0; notice = ""
    }
    #endif
}


// MARK: - Locker (Gear + Customize)

extension TennisMenu {
    /// Colours offered as swatches for clothes, shoes and rackets: the palette's eight friendliest tones.
    static let lockerSwatches: [(name: String, hex: String)] = [
        ("White", "F2F2F0"), ("Sky", "3FA9F5"), ("Navy", "1E2A6E"), ("Lime", "9EE63A"),
        ("Sun", "FFC233"), ("Coral", "FF6B4A"), ("Violet", "8A4FFF"), ("Charcoal", "2E3138"),
    ]

    var lockerShelf: [LockerItem] { LockerCatalog.items(sport: lockerSport, slot: lockerSlot) }
    var lockerEquipped: LockerItem? { player.map { $0.equipped(lockerSlot, sport: lockerSport) } }
    /// Changed since the locker opened (Revert shows only then).
    var lockerDirty: Bool { player != nil && lockerOpening != nil && player != lockerOpening }
    /// The colour the Gear tab's colour row edits for the equipped item; nil when that item has none.
    var lockerItemColourSlot: String? { lockerEquipped?.tintable == true ? lockerSlot.colourSlot : nil }

    /// The focus grid, top to bottom as drawn. Gear: sport, tabs, slots, the shelf, the item's colour, actions. Customize: tabs, then one row per choice.
    func lockerRows() -> [[String]] {
        if let range = lockerRange {
            return range == "skin" ? [["lk-range-skin"], ["lk-range-close"]] : [["lk-range-hue"], ["lk-range-shade"], ["lk-range-close"]]
        }
        var actions = ["lk-shuffle"]
        if lockerDirty { actions.append("lk-revert") }
        actions.append("lk-done")
        let tabs = ["lk-tab-gear", "lk-tab-customize", "lk-tab-emotes"]
        switch lockerTab {
        case .gear:
            var rows = [LockerCatalog.sports.map { "lk-sport-\($0.rawValue)" }, tabs,
                        LockerCatalog.slots(for: lockerSport).map { "lk-slot-\($0.rawValue)" },
                        lockerShelf.map { "lk-item-\($0.id)" }]
            if lockerItemColourSlot != nil { rows.append(["lk-colour"]) }
            rows.append(actions)
            return rows
        case .customize:
            return [tabs, ["lk-body"], ["lk-hand"], ["lk-skin"], ["lk-shirt"], ["lk-shorts"], actions]
        case .emotes:
            return [tabs, (0..<3).map { "lk-emote-slot-\($0)" },
                    Array(EmoteCatalog.ids.prefix(3)).map { "lk-emote-\($0)" },
                    Array(EmoteCatalog.ids.suffix(3)).map { "lk-emote-\($0)" }, actions]
        }
    }

    /// Opening the locker: Gear, on the shelf, on the item you wear.
    fileprivate func lockerOpen() {
        lockerOpening = player; lockerTab = .gear; lockerEmoteSlot = 0; lockerRange = nil; lockerRangeFrom = nil
        if !LockerCatalog.sports.contains(lockerSport) { lockerSport = .tennis }
        if !LockerCatalog.slots(for: lockerSport).contains(lockerSlot) { lockerSlot = LockerCatalog.slots(for: lockerSport)[0] }
        row = 3
        column = max(0, lockerShelf.firstIndex { $0.id == lockerEquipped?.id } ?? 0)
    }

    fileprivate func lockerClampFocus() {
        let grid = lockerRows()
        row = min(row, grid.count - 1); column = min(column, max(0, grid[row].count - 1))
    }

    /// Change the current player; saved to disk straight away, or later while a slider drags.
    func lockerEdit(save: Bool = true, _ change: (inout Player) -> Void) {
        let s = session
        guard s.players.indices.contains(s.playerIndex) else { return }
        change(&s.players[s.playerIndex])
        if save && screen != .online(.clothes) { s.savePlayers() }
    }

    fileprivate func lockerSelect(_ id: String) {
        switch id {
        case "lk-tab-gear": lockerTab = .gear; _ = focus(id)
        case "lk-tab-customize": lockerTab = .customize; _ = focus(id)
        case "lk-tab-emotes": lockerTab = .emotes; _ = focus(id)
        case let s where s.hasPrefix("lk-emote-slot-"):
            if let slot = Int(s.dropFirst(14)), (0..<3).contains(slot) { lockerEmoteSlot = slot }
        case let s where s.hasPrefix("lk-emote-"):
            let emote = String(s.dropFirst(9))
            lockerEdit { $0.equipEmote(emote, slot: lockerEmoteSlot) }
        case "lk-sport-tennis", "lk-sport-golf":
            lockerSport = id == "lk-sport-golf" ? .golf : .tennis
            if !LockerCatalog.slots(for: lockerSport).contains(lockerSlot) { lockerSlot = lockerSlot == .racket ? .club : lockerSlot == .club ? .racket : .skin }
        case let s where s.hasPrefix("lk-slot-"):
            if let slot = LockerSlot(rawValue: String(s.dropFirst(8))) { lockerSlot = slot }
        case let s where s.hasPrefix("lk-item-"):
            let itemID = String(s.dropFirst(8))
            if let item = lockerShelf.first(where: { $0.id == itemID }) { lockerEdit { $0.equip(item, sport: lockerSport) } }
        case "lk-colour": if let c = lockerItemColourSlot { lockerOpenRange(c, from: id) }
        case "lk-skin": lockerOpenRange("skin", from: id)
        case "lk-shirt": lockerOpenRange("shirt", from: id)
        case "lk-shorts": lockerOpenRange("shorts", from: id)
        case "lk-body": lockerEdit { $0.standardFemale.toggle() }
        case "lk-hand": lockerEdit { $0.handedness = $0.handedness == .left ? .right : .left }
        case "lk-shuffle": lockerShuffle()
        case "lk-revert": lockerRevert(); if screen == .online(.clothes) { show(.online(.lobby)) }
        case "lk-done":
            if screen == .online(.clothes) { online.finishClothes(menu: self) }
            else { show(.main) }
        case "lk-range-close": lockerCloseRange()
        default: break
        }
        if screen == .character || screen == .online(.clothes) { lockerClampFocus() }
    }

    /// Sideways on a locker row: step along its swatches, or flip a two-way choice. False = not a stepping row (move focus instead).
    fileprivate func lockerAdjust(_ id: String, by step: Int) -> Bool {
        switch id {
        case "lk-colour": guard let c = lockerItemColourSlot else { return false }; lockerStepSwatch(c, by: step); return true
        case "lk-shirt": lockerStepSwatch("shirt", by: step); return true
        case "lk-shorts": lockerStepSwatch("shorts", by: step); return true
        case "lk-skin":
            let presets = LockerColor.skinPresets, i = lockerSkinIndex()
            let next = i < 0 ? (step > 0 ? 0 : presets.count - 1) : (i + step + presets.count) % presets.count
            lockerEdit { $0.setSkin(presets[next]) }
            return true
        case "lk-body", "lk-hand": lockerSelect(id); return true
        case "lk-range-hue", "lk-range-shade":
            guard let slot = lockerRange else { return false }
            lockerEdit { p in
                let (h, sh) = p.hueShade(slot)
                p.setOutfit(slot, hue: id == "lk-range-hue" ? h + Double(step) / 24 : h, shade: id == "lk-range-shade" ? sh + Double(step) * 0.1 : sh)
            }
            return true
        case "lk-range-skin": lockerEdit { $0.setSkin($0.skinT + Double(step) * 0.05) }; return true
        default: return false
        }
    }

    // MARK: swatches

    /// -1 = the kit's own colour, -2 = a custom colour, otherwise the swatch index.
    func lockerSwatchIndex(_ slot: String) -> Int {
        guard let hex = player?.outfitHex(slot) else { return -1 }
        if let exact = Self.lockerSwatches.firstIndex(where: { $0.hex.caseInsensitiveCompare(hex) == .orderedSame }) { return exact }
        // A colour from the sliders (or an older save) that is close to a swatch still lights it.
        func norm(_ h: String) -> String { let (a, b) = LockerColor.hueShadeOf(h); return LockerColor.hueShade(a, b) }
        let mine = norm(hex)
        return Self.lockerSwatches.firstIndex { norm($0.hex) == mine } ?? -2
    }
    func lockerSkinIndex() -> Int {
        guard let t = player?.skinT else { return -2 }
        return LockerColor.skinPresets.enumerated().min { abs($0.element - t) < abs($1.element - t) }.flatMap { abs($0.element - t) < 0.035 ? $0.offset : nil } ?? -2
    }
    /// Touch: pick swatch `index` (-1 = kit colour) for a colour slot.
    func lockerPick(_ slot: String, index: Int) {
        lockerEdit { p in
            if index < 0 { p.clearOutfit(slot) }
            else { p.setOutfitHex(slot, Self.lockerSwatches[index].hex) }
        }
        if session.haptics { tick.selectionChanged() }
    }
    func lockerPickSkin(_ index: Int) { lockerEdit { $0.setSkin(LockerColor.skinPresets[index]) }; if session.haptics { tick.selectionChanged() } }

    fileprivate func lockerStepSwatch(_ slot: String, by step: Int) {
        let cycle = [-1] + Array(Self.lockerSwatches.indices)
        let i = lockerSwatchIndex(slot)
        let at = cycle.firstIndex(of: i) ?? 0
        lockerPick(slot, index: cycle[(at + step + cycle.count) % cycle.count])
    }

    // MARK: ranges, shuffle, revert

    fileprivate func lockerOpenRange(_ slot: String, from id: String) {
        lockerRange = slot; lockerRangeFrom = id; row = 0; column = 0
    }
    func lockerCloseRange() {
        lockerRange = nil
        if let from = lockerRangeFrom { _ = focus(from) } else { row = 0; column = 0 }
        lockerRangeFrom = nil
        lockerClampFocus()
    }
    func lockerOpenRangeForTouch(_ slot: String) { lockerOpenRange(slot, from: slot == "skin" ? "lk-skin" : slot == "shirt" ? "lk-shirt" : slot == "shorts" ? "lk-shorts" : "lk-colour") }

    fileprivate func lockerShuffle() {
        lockerEdit { p in
            p.setSkin(Double.random(in: 0...1))
            var last = -1
            for slot in ["shirt", "shorts", "accent", "racket"] {
                var pick = Int.random(in: 0..<Self.lockerSwatches.count)
                if pick == last { pick = (pick + 1) % Self.lockerSwatches.count }
                last = pick
                p.setOutfitHex(slot, Self.lockerSwatches[pick].hex)
            }
        }
    }
    fileprivate func lockerRevert() {
        guard let original = lockerOpening else { return }
        lockerEdit { $0 = original }
    }

    /// Phone remote text for a locker item.
    static func lockerLabel(_ id: String) -> String {
        switch id {
        case "lk-tab-gear": return "Locker · Gear"
        case "lk-tab-customize": return "Locker · Customize"
        case "lk-tab-emotes": return "Locker · Emotes"
        case "lk-sport-tennis": return "Gear for Tennis"
        case "lk-sport-golf": return "Gear for Golf"
        case "lk-colour": return "Colour"
        case "lk-skin": return "Skin tone"
        case "lk-shirt": return "Shirt colour"
        case "lk-shorts": return "Shorts colour"
        case "lk-body": return "Player"
        case "lk-hand": return "Plays"
        case "lk-shuffle": return "Shuffle"
        case "lk-revert": return "Revert changes"
        case "lk-done": return "Done"
        case "lk-range-hue": return "Hue"
        case "lk-range-shade": return "Shade"
        case "lk-range-skin": return "Skin tone range"
        case "lk-range-close": return "Close range"
        default:
            if id.hasPrefix("lk-emote-slot-"), let slot = Int(id.dropFirst(14)) { return "Emote slot \(slot + 1)" }
            if id.hasPrefix("lk-emote-") { return "Equip · \(EmoteCatalog.name(String(id.dropFirst(9))))" }
            if id.hasPrefix("lk-slot-"), let slot = LockerSlot(rawValue: String(id.dropFirst(8))) { return "Slot · \(slot.title(for: .tennis))" }
            if id.hasPrefix("lk-item-") { return "Equip · \(id.dropFirst(8).prefix(1).uppercased() + id.dropFirst(9))" }
            return id
        }
    }
}

extension Array {
    subscript(safe index: Int) -> Element? { indices.contains(index) ? self[index] : nil }
}

// MARK: - Online play (the same focus grid as every Island screen)

extension TennisMenu {
    func showOnline(_ route: OnlineLobbyScreen) { show(.online(route)) }
    func onlineNotice(_ text: String) { notice = text }
    func openOnlineParty() { show(.party) }
    func onlineLockerTap(_ id: String) { lockerSelect(id) }
    func cancelOnlineClothes() { lockerRevert(); show(.online(.lobby)) }
}

struct OnlineAppleSheet: Identifiable {
    let id = UUID()
    let controller: UIViewController
}

@MainActor @Observable final class OnlineLobbyMenu {
    let service: MultiplayerService
    var sheet: OnlineAppleSheet?
    var searchSport: MultiplayerSport = .tennis
    var searchStarted = Date()
    var settingsSeats = false
    var nearbyPage = 0
    var joinCode = ""
    var winnerID: String?
    @ObservationIgnored private var operation: Task<Void, Never>?
    @ObservationIgnored private var operationID = UUID()
    init(service: MultiplayerService = .shared) { self.service = service }
    var nearbyItems: [LocalMultiplayerTransport.DiscoveredLobby] { Array(service.discoveredLobbies.dropFirst(nearbyPage * 4).prefix(4)) }
    var participants: [MultiplayerParticipant] {
        (service.lobby?.participants ?? []).sorted { a, b in
            if a.id == b.id { return false }; if a.id == service.localID { return true }; if b.id == service.localID { return false }
            return a.seat == b.seat ? a.id < b.id : a.seat >= 0 && (b.seat < 0 || a.seat < b.seat)
        }
    }
    func identity(_ menu: TennisMenu) {
        guard let p = menu.player else { return }
        service.setIdentity(name:p.name, female:p.standardFemale, left:p.handedness == .left, loadout:p.multiplayerLoadout)
    }
    func rows(_ route: OnlineLobbyScreen, menu: TennisMenu) -> [[String]] {
        switch route {
        case .entry: return (service.authenticated ? [] : [["net-signin"]]) + [["net-tennis"],["net-golf"],["back"]]
        case .nearby: return nearbyItems.map { ["net-join-\($0.id)"] } + (service.discoveredLobbies.count > 4 ? [["net-page-prev","net-page-next"]] : []) + [["net-join-code"],["net-host"],["back"]]
        case .searching: return [["net-cancel"]]
        case .lobby: return (service.isNearby && service.isOwner && service.lobby?.sport == .golf ? [["net-add-guest","net-remove-guest"]] : []) + [["net-ready"],["net-emotes","net-clothes"],["net-settings"],["net-invite","net-find"]] + (service.isOwner ? [["net-start"]] : []) + [["net-leave"]]
        case .emotes: return [Array(MultiplayerEmote.ids.prefix(3)).map { "net-emote-\($0)" },Array(MultiplayerEmote.ids.suffix(3)).map { "net-emote-\($0)" },["back"]]
        case .clothes: return menu.lockerRows()
        case .settings:
            if settingsSeats { return (service.isOwner ? participants.map { ["net-seat-\($0.id)"] } : []) + [["net-settings-match"],["back"]] }
            guard service.isOwner else { return [["net-settings-seats"],["back"]] }
            let sport = service.lobby?.sport ?? .tennis
            let venues = sport == .tennis ? ["resort","skyscraper","volcano"] : GolfCourseChoice.allCases.map(\.rawValue)
            return [["net-sport-tennis","net-sport-golf"],venues.map { "net-venue-\($0)" }]
                + (sport == .tennis ? [["net-sets-1","net-sets-2","net-sets-3"],["net-games-1","net-games-3","net-games-6"]] : [])
                + [["net-settings-seats"],["back"]]
        case .loading: return service.loadingNeedsDecision ? [["net-wait", "net-leave"]] : [["net-leave"]]
        case .match: return [["net-leave"]]
        case .results: return (service.isOwner ? [["net-rematch"]] : []) + [["net-leave"]]
        case .leave: return [["net-stay","net-confirm-leave"]]
        }
    }
    func select(_ id: String, menu: TennisMenu) {
        do {
            switch id {
            case "partyOnline": identity(menu); menu.showOnline(.entry)
            case "partyLocalGolf":
                // Pass the phone: the course last picked, a guest already in, and this phone ready, so
                // START is the only press left.
                identity(menu); try service.hostLocal(name: menu.player?.name ?? "Friends")
                let course = SportsSession.shared.golfCourse
                try service.configure(.golf, venue: MultiplayerLobby.validVenue(course, sport: .golf) ? course : "postcards")
                try service.addLocalGuest(); try? service.setReady(true); menu.showOnline(.lobby)
            case "net-add-guest": try service.addLocalGuest()
            case "net-remove-guest": try service.removeLocalGuest()
            case "net-join-code":
                identity(menu); try service.joinLocal(code:joinCode); searchStarted = Date(); menu.showOnline(.searching)
            case "partyNearby": identity(menu); nearbyPage = 0; service.browseLocal(); menu.showOnline(.nearby)
            case "net-signin":
                run(menu) { [self] in try await service.authenticate { [weak self] vc in self?.sheet = OnlineAppleSheet(controller:vc) } }
            case "net-tennis","net-golf":
                identity(menu); searchSport = id == "net-golf" ? .golf : .tennis; searchStarted = Date(); menu.showOnline(.searching)
                run(menu) { [self] in try await service.quickMatch(searchSport) }
            case "net-cancel": cancel(); service.leave(); menu.showOnline(.entry)
            case "net-host": identity(menu); try service.hostLocal(name: menu.player?.name ?? "Friends"); menu.showOnline(.lobby)
            case "net-page-prev": nearbyPage = max(0,nearbyPage - 1)
            case "net-page-next": nearbyPage = min(max(0,(service.discoveredLobbies.count - 1) / 4),nearbyPage + 1)
            case let id where id.hasPrefix("net-join-"):
                guard let found = service.discoveredLobbies.first(where: { $0.id == String(id.dropFirst(9)) }) else { return }
                identity(menu); try service.joinLocal(found); searchStarted = Date(); menu.showOnline(.searching)
            case "net-invite":
                identity(menu)
                if service.isNearby { menu.onlineNotice("Ask your friend to open Nearby on the same Wi-Fi."); return }
                run(menu) { [self] in
                    if !service.authenticated { try await service.authenticate { [weak self] vc in self?.sheet = OnlineAppleSheet(controller: vc) } }
                    sheet = OnlineAppleSheet(controller: try service.inviteFriends())
                    menu.showOnline(.lobby)
                }
            case "net-find":
                if service.isNearby { menu.onlineNotice("Your lobby is visible in Nearby. Ask a friend to join."); return }
                run(menu) { [self] in try await service.findMorePlayers() }
            case "net-wait": try service.keepWaitingForLoad(); SportsSession.shared.loading.keepWaiting()
            case "net-ready": try service.setReady(!(service.lobby?.participants.first { $0.id == service.localID }?.ready ?? false))
            case "net-start": try service.startMatch(); sync(menu)
            case "net-emotes": menu.showOnline(.emotes)
            case let id where id.hasPrefix("net-emote-"): try service.playEmote(String(id.dropFirst(10)))
            case "net-clothes": menu.showOnline(.clothes)
            case let id where id.hasPrefix("lk-"): menu.onlineLockerTap(id)
            case "net-settings": settingsSeats = false; menu.showOnline(.settings)
            case "net-settings-seats": settingsSeats = true; menu.showOnline(.settings)
            case "net-settings-match": settingsSeats = false; menu.showOnline(.settings)
            case "net-sport-tennis","net-sport-golf":
                let sport: MultiplayerSport = id == "net-sport-golf" ? .golf : .tennis
                try service.configure(sport,venue:sport == .golf ? "postcards" : "resort")
                _ = menu.focus(id)
            case let id where id.hasPrefix("net-venue-"):
                guard let l = service.lobby else { return }; try service.configure(l.sport,venue:String(id.dropFirst(10)),sets:l.sets,games:l.games)
            case let id where id.hasPrefix("net-sets-") || id.hasPrefix("net-games-"):
                guard let l = service.lobby else { return }
                try service.configure(l.sport,venue:l.venue,sets:id.hasPrefix("net-sets-") ? Int(id.dropFirst(9)) ?? 1 : l.sets,games:id.hasPrefix("net-games-") ? Int(id.dropFirst(10)) ?? 3 : l.games)
            case let id where id.hasPrefix("net-seat-"):
                guard let l = service.lobby, let p = l.participants.first(where: { $0.id == String(id.dropFirst(9)) }) else { return }
                if p.seat >= 0 { try service.assignSeat(p.id,seat:-1) }
                else if let seat = (0..<l.capacity).first(where: { n in !l.participants.contains { $0.seat == n } }) { try service.assignSeat(p.id,seat:seat) }
                else if let outgoing = l.participants.last(where: { $0.seat >= 0 && $0.id != l.ownerID }) { try service.swapSeat(p.id,with:outgoing.id) }
            case "net-leave": SportsDisplays.shared.showMatchControls(); menu.showOnline(.leave)
            case "net-stay": sync(menu,force:true)
            case "net-confirm-leave": cancel(); service.leave(); menu.openOnlineParty()
            case "net-return": try service.requestReturnToLobby(); sync(menu,force:true)
            case "net-rematch": try service.rematch(); sync(menu,force:true)
            case "net-queue": try service.queueForNextMatch(!(service.lobby?.queue.contains(service.localID) ?? false))
            case "back": if case .online(let route) = menu.screen { back(route,menu:menu) }
            default: break
            }
        } catch { menu.onlineNotice(error.localizedDescription) }
    }
    private func run(_ menu: TennisMenu, _ work: @escaping @MainActor () async throws -> Void) {
        operation?.cancel(); let token = UUID(); operationID = token
        operation = Task { @MainActor [weak self, weak menu] in
            do { try await work(); guard let self, let menu, self.operationID == token else { return }; self.sync(menu) }
            catch is CancellationError { }
            catch { guard let self, let menu, self.operationID == token else { return }; if menu.screen == .online(.searching) { menu.showOnline(.entry) }; menu.onlineNotice(error.localizedDescription) }
        }
    }
    fileprivate func cancel() { operationID = UUID(); operation?.cancel(); operation = nil; service.cancelSearch() }
    func finishClothes(menu: TennisMenu) {
        guard let p = menu.player else { return }
        do { try service.updateLook(p.multiplayerLoadout,name:p.name,female:p.standardFemale,left:p.handedness == .left); SportsSession.shared.savePlayers(); menu.showOnline(.lobby) }
        catch { menu.onlineNotice(error.localizedDescription) }
    }
    func back(_ route: OnlineLobbyScreen, menu: TennisMenu) {
        switch route {
        case .entry,.nearby: cancel(); service.stopBrowsing(); menu.returnFromNetworkEntry(route)
        case .searching: cancel(); service.leave(); menu.showOnline(.entry)
        case .emotes,.settings: menu.showOnline(.lobby)
        case .clothes: if menu.lockerRange != nil { menu.onlineLockerTap("lk-range-close") } else { menu.cancelOnlineClothes() }
        case .leave: sync(menu,force:true)
        case .lobby,.loading,.match,.results: menu.showOnline(.leave)
        }
    }
    func sync(_ menu: TennisMenu, force: Bool = false) {
        guard let l = service.lobby else { return }
        switch l.phase {
        case .lobby:
            if force || !menu.screen.isOnline || [.online(.entry),.online(.nearby),.online(.searching),.online(.loading),.online(.results)].contains(menu.screen) { menu.showOnline(.lobby) }
        case .loading: if menu.screen != .online(.leave) { menu.showOnline(.loading) }
        case .results: if menu.screen != .online(.leave) { SportsDisplays.shared.showMatchControls(); menu.showOnline(.results) }
        case .playing: if menu.screen != .online(.leave) { menu.showOnline(.match); SportsDisplays.shared.restorePhoneControls() }
        case .interrupted: if menu.screen != .online(.leave) { menu.showOnline(.lobby) }
        }
    }
    func acceptInvite(_ menu: TennisMenu) {
        guard service.pendingInvite != nil else { return }
        do {
            if service.lobby != nil || SportsSession.shared.active { service.leave(); SportsSession.shared.end() }
            identity(menu); sheet = OnlineAppleSheet(controller:try service.acceptInvitation()); menu.showOnline(.searching)
        } catch { menu.onlineNotice(error.localizedDescription) }
    }
}

extension OnlineLobbyMenu {
    func installCallbacks(_ menu: TennisMenu) {
        service.onResult = { [weak self, weak menu] payload in
            guard let self, let menu else { return }
            self.winnerID = nil
            if let data = payload.data(using:.utf8), let result = try? JSONSerialization.jsonObject(with:data) as? [String:Any], result["reason"] as? String == "complete", let seat = result["winner"] as? Int, seat >= 0 {
                self.winnerID = self.service.lobby?.participants.first { $0.seat == seat }?.id
            }
            SportsDisplays.shared.showMatchControls(); SportsDisplays.shared.showMenu(); menu.showOnline(.results)
        }
        service.onReturnToLobby = { [weak menu] in
            guard let menu else { return }
            if SportsSession.shared.multiplayerMatchID != nil { SportsSession.shared.end() }
            SportsDisplays.shared.showMatchControls(); SportsDisplays.shared.showMenu()
            if menu.online.service.lobby != nil { menu.showOnline(.lobby) }
        }
    }
}

#if DEBUG
/// An opt-in two-simulator proof driver. It uses the production focus ids and Nearby transport.
@MainActor enum OnlineLobbyProofDriver {
    private static var task: Task<Void,Never>?
    private static var logURL: URL?
    static func event(_ name: String, _ detail: String = "") {
        guard let logURL else { return }
        let line = "\(ProcessInfo.processInfo.systemUptime)\t\(name)\t\(detail)\n"
        if let data=line.data(using:.utf8), let handle=try? FileHandle(forWritingTo:logURL) { _ = try? handle.seekToEnd();try? handle.write(contentsOf:data);try? handle.close() }
    }
    static func start(_ menu: TennisMenu, args:[String]) {
        guard task == nil,let i=args.firstIndex(of:"--lobby-proof"),let role=args[safe:i+1] else { return }
        let host=role == "host"
        let output=args.firstIndex(of:"--lobby-proof-output").flatMap { args[safe:$0+1] } ?? NSTemporaryDirectory()
        try? FileManager.default.createDirectory(atPath:output,withIntermediateDirectories:true)
        logURL=URL(fileURLWithPath:output).appendingPathComponent("\(role).tsv");try? Data().write(to:logURL!)
        let service=menu.online.service;service.proofRecording=true
        task=Task { @MainActor in
            do {
                var p=Player(name:host ? "Adnan" : "Sam",colorIndex:host ? 0 : 1,handedness:host ? .right : .left)
                p.standardFemale = !host;p.setSkin(host ? 0.45 : 0.2);p.setOutfitHex("shirt",host ? "FF6B4A" : "D3F34B")
                p.setOutfitHex("shorts",host ? "101D35" : "FAF8F3")
                SportsSession.shared.players=[p];SportsSession.shared.playerIndex=0
                let session=SportsSession.shared
                session.startTennis(opponent:nil,round:nil,difficulty:0.1,preview:true,sets:1,games:1)
                for _ in 0..<1200 { if session.ready { break };try await Task.sleep(for:.milliseconds(100)) }
                guard session.ready else { throw MultiplayerError.unavailable("Unity proof warmup did not become ready") }
                event("warmup-ready");session.end();try await Task.sleep(for:.seconds(1))
                menu.openOnlineParty();menu.tap("partyLocal");menu.tap("partyNearby")
                if host { menu.tap("net-host");try service.configure(.tennis,venue:"resort",sets:1,games:1);menu.tap("net-invite");event("nearby-invitation") }
                else {
                    var found:LocalMultiplayerTransport.DiscoveredLobby?
                    for _ in 0..<1200 { found=service.discoveredLobbies.first { $0.name == "Adnan" };if found != nil {break};try await Task.sleep(for:.milliseconds(100)) }
                    menu.tap("net-join-\(try requireValue(found).id)")
                }
                for _ in 0..<1200 { if (service.lobby?.participants.count ?? 0) >= 2 { break };try await Task.sleep(for:.milliseconds(100)) }
                guard (service.lobby?.participants.count ?? 0) >= 2 else { throw MultiplayerError.unavailable("Nearby proof did not connect") }
                event("joined",service.localID)
                if host { try service.addProofSpectators() }
                try await Task.sleep(for:.seconds(5))
                menu.tap("net-emotes");menu.tap(host ? "net-emote-scuba" : "net-emote-pushups");event("emote-selected",service.localID)
                try await Task.sleep(for:.seconds(2));menu.back();menu.tap("net-clothes");menu.tap("lk-tab-customize")
                menu.lockerEdit { $0.setOutfitHex("shirt",host ? "34435A" : "FF6B4A") }
                event("look-preview",service.localID);try await Task.sleep(for:.seconds(1));menu.tap("lk-done");event("look-done",service.localID)
                try await Task.sleep(for:.seconds(5))
                if host { service.removeProofSpectator();event("mock-leave");try await Task.sleep(for:.seconds(1));try service.addProofSpectators();event("mock-rejoin") }
                try await Task.sleep(for:.seconds(3));menu.tap("net-ready");event("ready",service.localID)
                if args.contains("--lobby-proof-lobby-only") { event("finished"); return }
                if host {
                    for _ in 0..<100 { if service.lobby?.canStart == true { break };try await Task.sleep(for:.milliseconds(100)) }
                    menu.tap("net-start");event("start")
                }
                var completed=0,action:Int64=1_000_000,lastTossPoint = "",lastSwingPoint = "",lastSwingAt=0.0
                for step in 0..<1800 {
                    if step % 100 == 0 { event("phase","\(String(describing:service.lobby?.phase)) active=\(session.active) ready=\(session.ready) \(session.status) snapshot=\(service.proofSnapshot?.prefix(500) ?? "none")") }
                    if service.lobby?.phase == .playing,let text=service.proofSnapshot,let data=text.data(using:.utf8),let s=try JSONSerialization.jsonObject(with:data) as? [String:Any],let phase=s["phase"] as? String,let time=s["time"] as? Double,let point=s["point"] as? Int64,let contact=s["contact"] as? Int64 {
                        let server=s["server"] as? Int ?? 0,seat=service.localSeat,phaseAt=s["phaseAt"] as? Double ?? 0
                        var input:[String:Any]? = nil
                        if phase == "serve",server == seat,"\(point):\(phaseAt)" != lastTossPoint,time-phaseAt > 0.4 { input=["action":"toss"];lastTossPoint="\(point):\(phaseAt)" }
                        if phase == "toss",server == seat,"\(point):\(phaseAt)" != lastSwingPoint,time-phaseAt > 0.65 { input=["action":"swing","power":0.65,"aim":0.0];lastSwingPoint="\(point):\(phaseAt)" }
                        if phase == "rally",contact < 6,s["receiver"] as? Int == seat,time-lastSwingAt > 0.3,let ball=s["ball"] as? [String:Double],abs((ball["z"] ?? 0)-(seat == 0 ? -12.2 : 12.2)) < 1.8 {
                            input=["action":"swing","power":0.55,"aim":0.0];lastSwingAt=time
                        }
                        if var input { action+=1;input["eventID"]=action;input["point"]=point;input["contact"]=contact;input["time"]=service.networkTime;input["age"]=0;try service.sendProofInput(input);event("input",input["action"] as? String ?? "") }
                    }
                    if service.lobby?.phase == .results {
                        completed+=1;event("result",String(completed));try await Task.sleep(for:.seconds(3))
                        if completed == 1 {
                            menu.tap("net-return");try await Task.sleep(for:.seconds(2));menu.tap("net-ready");lastTossPoint = "";lastSwingPoint = ""
                            if host { for _ in 0..<100 { if service.lobby?.canStart == true {break};try await Task.sleep(for:.milliseconds(100)) };menu.tap("net-start");event("rematch-start") }
                        } else { break }
                    }
                    if completed >= 1,service.lobby?.phase == .playing {
                        if !host { try await Task.sleep(for:.seconds(3));service.breakProofConnection();menu.openOnlineParty();event("forced-disconnect");break }
                    }
                    try await Task.sleep(for:.milliseconds(100))
                }
                event("finished")
            }catch { event("error",error.localizedDescription);menu.onlineNotice(error.localizedDescription) }
        }
    }
    private static func requireValue<T>(_ value:T?) throws -> T { guard let value else { throw MultiplayerError.unavailable("Nearby proof host not found") };return value }
}
#endif
