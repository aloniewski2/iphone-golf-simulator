import Foundation
import GameKit
import Observation

/// Who may type and talk in the party, and what is hidden from whom: the app's answer to Apple's rules for apps with communication
/// features (App Review 1.2 report / block / filter / contact, 5.1.4 children's data, and Game Center's own parental flags).
/// Everything here stays on the phone: the age answer, the accepted rules and the block list are never uploaded.

enum AgeBand: String, Codable, Sendable { case unknown, under13, teen, adult }

/// What Game Center says about this account (parents set these in Screen Time / Family Sharing).
struct GameCenterRestrictions: Equatable, Sendable {
    var underage = false
    var communicationRestricted = false
    var multiplayerRestricted = false
}

/// What the player may do in the party right now, and why not (shown to them in plain words).
enum PartyAccess: Equatable, Sendable {
    /// No party features at all (a parent turned multiplayer off).
    case blocked(String)
    /// Quick phrases only: no typing, no voice.
    case quickOnly(String)
    case full
    var canType: Bool { self == .full }
    var canTalk: Bool { self == .full }
    var reason: String? { switch self { case .blocked(let s), .quickOnly(let s): s; case .full: nil } }
}

@MainActor @Observable
final class PartySafety {
    static let shared = PartySafety()
    /// Bumped when the rules text changes materially: everyone accepts again.
    static let rulesVersion = 1

    private(set) var birthYear: Int?
    private(set) var ageLockedUntil: Date?
    private(set) var rulesAcceptedVersion: Int
    private(set) var restrictions = GameCenterRestrictions()
    /// Game Center ids this player blocked (chat, voice and invites), on top of the hub's own block list.
    private(set) var blocked: Set<String>
    var filterProfanity: Bool { didSet { defaults.set(filterProfanity, forKey: "party.filter") } }

    @ObservationIgnored private let defaults: UserDefaults
    @ObservationIgnored var restrictionsSource: @MainActor () -> GameCenterRestrictions
    @ObservationIgnored var now: @MainActor () -> Date = { Date() }
    @ObservationIgnored var year: @MainActor () -> Int = { Calendar.current.component(.year, from: Date()) }

    init(defaults: UserDefaults = .standard, restrictions: (@MainActor () -> GameCenterRestrictions)? = nil) {
        self.defaults = defaults
        birthYear = defaults.object(forKey: "party.birthYear") as? Int
        ageLockedUntil = defaults.object(forKey: "party.ageLock") as? Date
        rulesAcceptedVersion = defaults.integer(forKey: "party.rules")
        blocked = Set(defaults.stringArray(forKey: "party.blocked") ?? [])
        filterProfanity = defaults.object(forKey: "party.filter") as? Bool ?? true
        restrictionsSource = restrictions ?? { PartySafety.gameCenterRestrictions() }
    }

    /// Re-read Game Center's flags (they change while the app is open: a parent edits Screen Time).
    func refresh() { restrictions = restrictionsSource() }

    static func gameCenterRestrictions() -> GameCenterRestrictions {
        let p = GKLocalPlayer.local
        guard p.isAuthenticated else { return GameCenterRestrictions() }
        return GameCenterRestrictions(underage: p.isUnderage, communicationRestricted: p.isPersonalizedCommunicationRestricted,
                                      multiplayerRestricted: p.isMultiplayerGamingRestricted)
    }

    // MARK: age (a neutral screen: birth year only, asked once, never uploaded)

    var ageBand: AgeBand {
        guard let birthYear else { return .unknown }
        let age = year() - birthYear
        return age < 13 ? .under13 : age < 18 ? .teen : .adult
    }
    /// Someone who said they are under 13 cannot just type another year: the answer is locked for a week.
    var ageLocked: Bool { ageLockedUntil.map { $0 > now() } ?? false }
    /// Returns false when the answer is locked or not a plausible year.
    @discardableResult func setBirthYear(_ value: Int) -> Bool {
        guard !ageLocked, (1900...year()).contains(value) else { return false }
        birthYear = value; defaults.set(value, forKey: "party.birthYear")
        if ageBand == .under13 { let until = now().addingTimeInterval(7 * 86_400); ageLockedUntil = until; defaults.set(until, forKey: "party.ageLock") }
        return true
    }

    // MARK: rules

    var rulesAccepted: Bool { rulesAcceptedVersion >= Self.rulesVersion }
    func acceptRules() { rulesAcceptedVersion = Self.rulesVersion; defaults.set(Self.rulesVersion, forKey: "party.rules") }

    // MARK: the decision

    var access: PartyAccess {
        if restrictions.multiplayerRestricted { return .blocked("Multiplayer is turned off for this Game Center account.") }
        if restrictions.communicationRestricted || restrictions.underage { return .quickOnly("Your Game Center settings limit chat to quick phrases.") }
        switch ageBand {
        case .under13: return .quickOnly("Typing and voice chat are for ages 13 and up. Quick phrases are always on.")
        case .unknown: return .quickOnly("Tell us your age to unlock typing and voice. Quick phrases are always on.")
        case .teen, .adult: return rulesAccepted ? .full : .quickOnly("Read and accept the chat rules to unlock typing and voice.")
        }
    }
    /// Teens always have the language filter on; adults choose.
    var filterOn: Bool { ageBand == .teen || restrictions.underage || filterProfanity }
    var filterLocked: Bool { ageBand == .teen || restrictions.underage }

    // MARK: blocking

    func isBlocked(_ id: String) -> Bool { blocked.contains(id) }
    func block(_ id: String) { blocked.insert(id); defaults.set(Array(blocked), forKey: "party.blocked") }
    func unblock(_ id: String) { blocked.remove(id); defaults.set(Array(blocked), forKey: "party.blocked") }

    #if DEBUG
    func debugReset() {
        for k in ["party.birthYear", "party.ageLock", "party.rules", "party.blocked", "party.filter"] { defaults.removeObject(forKey: k) }
        birthYear = nil; ageLockedUntil = nil; rulesAcceptedVersion = 0; blocked = []; filterProfanity = true; restrictions = GameCenterRestrictions()
    }
    #endif
}

// MARK: - quick phrases (always allowed, nothing typed, nothing to filter)

enum QuickChat {
    static let phrases: [(id: String, text: String)] = [
        ("gg", "Good game!"), ("nice", "Nice shot!"), ("ready", "Ready!"), ("wait", "One sec"), ("go", "Let's go!"),
        ("oops", "Oops!"), ("thanks", "Thanks!"), ("rematch", "Rematch?"), ("wow", "Wow!"), ("sorry", "Sorry!"), ("again", "Again?"), ("brb", "Be right back")]
    static func text(_ id: String) -> String? { phrases.first { $0.id == id }?.text }
}

// MARK: - text filter

/// A small, honest filter: it masks common profanity and slurs (also when disguised with numbers, symbols or spacing) and hides contact
/// details and links, which no one needs in a party chat (and which are how strangers move kids to other apps). It is NOT a promise that
/// nothing offensive gets through: that is why Report, Mute and Block exist next to every message.
enum ChatFilter {
    static let maxLength = 140
    // rot13, only so the list is not spelled out in the source
    private static let exact: Set<String> = Set(["nff", "qvpx", "pbpx", "snt", "encr", "encvfg", "xlf", "gvg", "gvgf", "phz", "snc"].map(rot13))
    private static let stems: [String] = ["shpx", "fuvg", "ovgpu", "nffubyr", "phag", "juber", "fyhg", "chffl", "onfgneq", "avttre", "avttn", "snttbg", "ergneq",
                                          "zbgureshpxre", "qvpxurnq", "pbpxfhpxre", "jnaxre", "gjng", "cbea", "oybjwbo", "unaqwbo", "xvyylbhefrys"].map(rot13)
    private static func rot13(_ s: String) -> String {
        String(s.unicodeScalars.map { c -> Character in
            switch c.value {
            case 65...90: return Character(UnicodeScalar((c.value - 65 + 13) % 26 + 65)!)
            case 97...122: return Character(UnicodeScalar((c.value - 97 + 13) % 26 + 97)!)
            default: return Character(c)
            }
        })
    }
    /// Real words that merely contain a stem (the "Scunthorpe problem").
    private static let innocent: Set<String> = ["scunthorpe", "shitake", "shitakemushroom", "shitake", "analyze", "pussycat"]
    private static let leet: [Character: Character] = ["0": "o", "1": "i", "3": "e", "4": "a", "5": "s", "7": "t", "@": "a", "$": "s", "!": "i", "+": "t", "€": "e"]

    /// Letters only, look-alikes folded: "f.u.c.k", "ƒuck", "$h!t" end up as "fuck", "shit". `collapse` squeezes every run of one letter to a
    /// single letter ("fuuuck" -> "fuck"); the word lists are folded the same way, so doubles inside a word ("asshole") still match.
    static func fold(_ word: String, collapse: Bool = true) -> String {
        var out = ""; var last: Character?; var run = 0
        for ch in word.lowercased().folding(options: [.diacriticInsensitive, .widthInsensitive], locale: nil) {
            let c = leet[ch] ?? ch
            guard c.isLetter else { continue }
            if c == last { run += 1; if collapse || run >= 2 { continue } } else { run = 0 }
            out.append(c); last = c
        }
        return out
    }
    private static let foldedStems: [String] = stems.map { fold($0) }
    /// Short words match whole-token only, once with runs squeezed to two letters ("assss") and once fully squeezed when that does not
    /// turn them into a real word ("ass" would become "as").
    private static let foldedExact: Set<String> = Set(exact.map { fold($0) }.filter { $0.count >= 3 })
    private static func exactHit(_ w: String) -> Bool { exact.contains(fold(w, collapse: false)) || foldedExact.contains(fold(w)) }

    /// Strips control and bidirectional characters (they can hide or reorder text), trims, collapses spaces, caps the length.
    static func sanitize(_ text: String, limit: Int = maxLength) -> String {
        let cleaned = text.unicodeScalars.filter { s in
            if s.properties.isBidiControl { return false }
            let cat = s.properties.generalCategory
            if cat == .control || cat == .format || cat == .privateUse || cat == .unassigned { return s == "\n" || s == "\t" }
            return true
        }
        let single = String(String.UnicodeScalarView(cleaned)).replacingOccurrences(of: "\n", with: " ").replacingOccurrences(of: "\t", with: " ")
        let squeezed = single.split(separator: " ", omittingEmptySubsequences: true).joined(separator: " ")
        return String(squeezed.prefix(limit))
    }

    private static let contact = try! NSRegularExpression(pattern: #"(?i)(https?://\S+|www\.\S+|\b[\w.+-]+@[\w-]+\.[\w.-]+\b|\b\S+\.(com|net|org|io|gg|ly|me|tv|co|app|xyz)\b\S*|(?:\d[\s().-]*){7,})"#)

    /// `mask`: replace language with asterisks; contact details are always hidden. `flagged` says something was changed.
    static func clean(_ text: String, mask: Bool) -> (text: String, flagged: Bool) {
        var s = sanitize(text); var flagged = false
        let whole = NSRange(s.startIndex..., in: s)
        if contact.firstMatch(in: s, range: whole) != nil { s = contact.stringByReplacingMatches(in: s, range: whole, withTemplate: "[hidden]"); flagged = true }
        guard mask else { return (s, flagged) }
        let words = s.split(separator: " ", omittingEmptySubsequences: false).map(String.init)
        // a phrase can be spread over words ("kill yourself"): check the folded whole line as well
        let line = fold(s)
        let phraseHit = foldedStems.contains { $0.count >= 8 && line.contains($0) }
        let masked = words.map { w -> String in
            let f = fold(w)
            guard !f.isEmpty else { return w }
            let hit = !innocent.contains(f) && (exactHit(w) || foldedStems.contains { stem in f.contains(stem) })
            guard hit else { return w }
            flagged = true
            return String(repeating: "*", count: max(3, min(w.count, 12)))
        }
        if phraseHit && !flagged { flagged = true; return (String(repeating: "*", count: min(12, max(3, s.count))), true) }
        return (masked.joined(separator: " "), flagged)
    }
}

// MARK: - reports

/// What a report contains: who, why, and the last few things that person typed. Voice is never recorded or stored by the app, so a voice
/// report is the player's own description. Sent by the player themselves (a text message to support), never silently.
struct PartyReport: Equatable, Sendable {
    enum Reason: String, CaseIterable, Sendable {
        case harassment = "Harassment or bullying", hate = "Hate speech", sexual = "Sexual or inappropriate content",
             threats = "Threats or danger", language = "Offensive language", spam = "Spam or advertising", other = "Something else"
    }
    var reportedID: String
    var reportedName: String
    var reason: Reason
    var lines: [String]
    var voice: Bool
    var reporterName: String
    var date: Date

    var body: String {
        var parts = ["Motion Club report", "Player: \(reportedName)", "Reason: \(reason.rawValue)", "From: \(reporterName)",
                     "Time: \(ISO8601DateFormatter().string(from: date))"]
        if voice { parts.append("About: voice chat (not recorded by the app; I describe it below)") }
        if !lines.isEmpty { parts.append("Their last messages:"); parts += lines.map { "- \($0)" } }
        parts.append("Details: ")
        return parts.joined(separator: "\n")
    }
}

/// Where players find help and the rules, set per build in Info.plist (ClubSupportEmail, ClubSupportURL, ClubPrivacyPolicyURL). Apple
/// asks for published contact details and, for communication features, a privacy policy; the beta uses the text-message line the app
/// already has for feedback.
enum PartyContact {
    static func plist(_ key: String) -> String { (Bundle.main.object(forInfoDictionaryKey: key) as? String) ?? "" }
    static var email: String { plist("ClubSupportEmail") }
    static var supportURL: URL? { URL(string: plist("ClubSupportURL")).flatMap { $0.scheme == nil ? nil : $0 } }
    static var privacyURL: URL? { URL(string: plist("ClubPrivacyPolicyURL")).flatMap { $0.scheme == nil ? nil : $0 } }
    static let smsNumber = "+19085909023"
    static func reportURL(_ report: PartyReport) -> URL? {
        let body = report.body.addingPercentEncoding(withAllowedCharacters: .urlQueryAllowed) ?? ""
        if !email.isEmpty { return URL(string: "mailto:\(email)?subject=Motion%20Club%20report&body=\(body)") }
        return URL(string: "sms:\(smsNumber)&body=\(body)")
    }
}
