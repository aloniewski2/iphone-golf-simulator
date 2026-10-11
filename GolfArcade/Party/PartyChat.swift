import Foundation
import Observation
import UIKit

struct PartyChatMessage: Identifiable, Equatable, Sendable {
    enum Kind: Equatable, Sendable { case text, quick, system }
    var id = UUID()
    var senderID: String
    var senderName: String
    var text: String
    var kind: Kind = .text
    var date: Date
    var mine = false
    /// Language was masked or contact details hidden on this phone.
    var filtered = false
}

/// What the party chat needs from the lobby, behind a protocol so tests run it without Game Center.
@MainActor protocol PartyChannel: AnyObject {
    var localID: String { get }
    var inParty: Bool { get }
    /// The other real players in this party, id and name.
    var members: [(id: String, name: String)] { get }
    var onParty: ((String, String, String) -> Void)? { get set }
    func send(_ kind: String, payload: String, reliable: Bool)
}

@MainActor final class MultiplayerPartyChannel: PartyChannel {
    private var service: MultiplayerService { .shared }
    var localID: String { service.localID }
    var inParty: Bool { service.lobby != nil && !service.partyMembers.isEmpty }
    var members: [(id: String, name: String)] { service.partyMembers.map { ($0.id, $0.name) } }
    var onParty: ((String, String, String) -> Void)? { get { service.onParty } set { service.onParty = newValue } }
    func send(_ kind: String, payload: String, reliable: Bool) { service.sendParty(kind, payload: payload, reliable: reliable) }
}

/// Typed chat, quick phrases and "who is recording" for ONE party (the phones in your lobby). Messages live only in memory and only for
/// this session: nothing is saved, uploaded or sent anywhere but to the party's own phones. PLAN_PartyChat_Recording.md.
@MainActor @Observable
final class PartyChat {
    static let shared = PartyChat()

    private(set) var messages: [PartyChatMessage] = []
    private(set) var unread = 0
    private(set) var notice = ""
    /// Party members who are recording the TV right now (their phones told us).
    private(set) var recording: [String: String] = [:]
    /// The chat sheet is open (new messages are read as they arrive).
    var chatOpen = false { didSet { if chatOpen { unread = 0 } } }
    var incomingTextVisible: Bool { didSet { defaults.set(incomingTextVisible, forKey: "party.showText") } }
    var soundOnMessage: Bool { didSet { defaults.set(soundOnMessage, forKey: "party.msgSound") } }
    /// Muted for this session and after (their messages and voice are dropped on this phone).
    private(set) var muted: Set<String>

    @ObservationIgnored let channel: PartyChannel
    @ObservationIgnored let safety: PartySafety
    @ObservationIgnored private let defaults: UserDefaults
    @ObservationIgnored var now: @MainActor () -> Date = { Date() }
    @ObservationIgnored var localName: @MainActor () -> String = { "Player" }
    @ObservationIgnored var haptic: @MainActor () -> Void = {}
    /// Where voice frames go (the voice coordinator; nil while voice is off).
    @ObservationIgnored var voiceHandler: (@MainActor (Data, String) -> Void)?
    /// Party members the voice engine has been told about (so it can forget the ones who left).
    @ObservationIgnored var speakersKnown: Set<String> = []
    @ObservationIgnored private var sentTimes: [Date] = []
    @ObservationIgnored private var heard: [String: [Date]] = [:]
    @ObservationIgnored private var names: [String: String] = [:]
    static let history = 100

    init(channel: PartyChannel? = nil, safety: PartySafety = .shared, defaults: UserDefaults = .standard) {
        self.channel = channel ?? MultiplayerPartyChannel()
        self.safety = safety; self.defaults = defaults
        incomingTextVisible = defaults.object(forKey: "party.showText") as? Bool ?? true
        soundOnMessage = defaults.object(forKey: "party.msgSound") as? Bool ?? true
        muted = Set(defaults.stringArray(forKey: "party.muted") ?? [])
        self.channel.onParty = { [weak self] kind, payload, sender in self?.receive(kind, payload, from: sender) }
    }

    /// There is a party to talk to.
    var available: Bool { channel.inParty && !isBlockedAccess }
    private var isBlockedAccess: Bool { if case .blocked = safety.access { return true }; return false }
    var members: [(id: String, name: String)] { channel.members.filter { !isBlockedID($0.id) } }
    func isBlockedID(_ id: String) -> Bool { safety.isBlocked(id) || ClubHub.shared.blocked.contains(id) }
    func isMuted(_ id: String) -> Bool { muted.contains(id) || isBlockedID(id) }
    func name(of id: String) -> String { channel.members.first { $0.id == id }?.name ?? names[id] ?? "Player" }

    // MARK: sending

    enum SendResult: Equatable { case sent, empty, tooFast, notAllowed(String), noParty }
    @discardableResult func send(text raw: String) -> SendResult {
        guard channel.inParty else { return .noParty }
        guard safety.access.canType else { return .notAllowed(safety.access.reason ?? "Chat is not available.") }
        // the sender's own filter applies first: a teen's words are masked before they leave the phone
        let clean = ChatFilter.clean(raw, mask: safety.filterOn)
        guard !clean.text.isEmpty else { return .empty }
        guard rateAllows() else { return .tooFast }
        let payload = json(["t": "m", "text": clean.text, "name": localName()])
        channel.send("partyChat", payload: payload, reliable: true)
        add(PartyChatMessage(senderID: channel.localID, senderName: "You", text: clean.text, date: now(), mine: true, filtered: clean.flagged))
        return .sent
    }

    /// Quick phrases are available to everyone (also children): nothing is typed.
    @discardableResult func sendQuick(_ id: String) -> SendResult {
        guard channel.inParty else { return .noParty }
        guard let text = QuickChat.text(id) else { return .empty }
        guard rateAllows() else { return .tooFast }
        channel.send("partyChat", payload: json(["t": "q", "id": id, "name": localName()]), reliable: true)
        add(PartyChatMessage(senderID: channel.localID, senderName: "You", text: text, kind: .quick, date: now(), mine: true))
        return .sent
    }

    /// At most 1 message a second and 6 in 10 seconds.
    private func rateAllows() -> Bool {
        let t = now(); sentTimes = sentTimes.filter { t.timeIntervalSince($0) < 10 }
        if let last = sentTimes.last, t.timeIntervalSince(last) < 1 { return false }
        if sentTimes.count >= 6 { return false }
        sentTimes.append(t); return true
    }

    // MARK: receiving

    func receive(_ kind: String, _ payload: String, from sender: String) {
        guard sender != channel.localID, channel.members.contains(where: { $0.id == sender }) else { return }
        switch kind {
        case "partyChat": receiveChat(payload, from: sender)
        case "partyVoice":
            // voice frames: only while voice is on here, only from someone not muted or blocked
            guard !isMuted(sender), let data = Data(base64Encoded: payload) else { return }
            voiceHandler?(data, sender)
        case "partyRec":
            guard let o = decode(payload) else { return }
            let on = o["on"] as? Bool ?? false
            if on { recording[sender] = name(of: sender) } else { recording[sender] = nil }
        default: break
        }
    }

    private func receiveChat(_ payload: String, from sender: String) {
        guard !isMuted(sender), let o = decode(payload), let type = o["t"] as? String else { return }
        let t = now()
        // a sender cannot flood this phone: more than 5 messages in 5 seconds are dropped
        heard[sender] = (heard[sender] ?? []).filter { t.timeIntervalSince($0) < 5 }
        guard (heard[sender]?.count ?? 0) < 5 else { return }
        heard[sender, default: []].append(t)
        let senderName = ChatFilter.sanitize((o["name"] as? String) ?? name(of: sender), limit: 24)
        if !senderName.isEmpty { names[sender] = senderName }
        let display = names[sender] ?? "Player"
        switch type {
        case "q":
            guard let id = o["id"] as? String, let text = QuickChat.text(id) else { return }
            add(PartyChatMessage(senderID: sender, senderName: display, text: text, kind: .quick, date: t))
        case "m":
            // children (and anyone whose account limits chat) never see typed text from others
            guard safety.access.canType, incomingTextVisible, let raw = o["text"] as? String else { return }
            let clean = ChatFilter.clean(raw, mask: safety.filterOn)
            guard !clean.text.isEmpty else { return }
            add(PartyChatMessage(senderID: sender, senderName: display, text: clean.text, date: t, filtered: clean.flagged))
        default: return
        }
        if !chatOpen { unread += 1; haptic() }
    }

    private func add(_ message: PartyChatMessage) {
        messages.append(message)
        if messages.count > Self.history { messages.removeFirst(messages.count - Self.history) }
    }

    func systemNote(_ text: String) { add(PartyChatMessage(senderID: "", senderName: "", text: text, kind: .system, date: now())) }

    // MARK: people

    func mute(_ id: String) { muted.insert(id); defaults.set(Array(muted), forKey: "party.muted") }
    func unmute(_ id: String) { muted.remove(id); defaults.set(Array(muted), forKey: "party.muted") }
    /// Blocked: no chat, no voice, no invites from them, here and in the Friends hub.
    func block(_ id: String) {
        safety.block(id); mute(id)
        messages.removeAll { $0.senderID == id }
        recording[id] = nil
    }

    func lastLines(from id: String, count: Int = 5) -> [String] {
        messages.filter { $0.senderID == id && $0.kind == .text }.suffix(count).map(\.text)
    }
    func report(_ id: String, reason: PartyReport.Reason, voice: Bool, alsoMute: Bool = true) -> PartyReport {
        let r = PartyReport(reportedID: id, reportedName: name(of: id), reason: reason, lines: lastLines(from: id), voice: voice, reporterName: localName(), date: now())
        if alsoMute { mute(id) }
        return r
    }

    // MARK: recording notices (the party always knows when someone records)

    func announceRecording(_ on: Bool) { channel.send("partyRec", payload: json(["on": on]), reliable: true) }
    var someoneRecording: String? { recording.values.sorted().first }

    /// The lobby changed (someone left, the party ended): forget people who are gone.
    func membersChanged() {
        let ids = Set(channel.members.map(\.id))
        recording = recording.filter { ids.contains($0.key) }
        if !channel.inParty { messages = []; unread = 0 }
    }

    func say(_ text: String) { notice = text }
    func clearNotice() { notice = "" }

    private func json(_ o: [String: Any]) -> String { (try? JSONSerialization.data(withJSONObject: o)).flatMap { String(data: $0, encoding: .utf8) } ?? "{}" }
    private func decode(_ s: String) -> [String: Any]? { (try? JSONSerialization.jsonObject(with: Data(s.utf8))) as? [String: Any] }

    #if DEBUG
    func debugReset() { messages = []; unread = 0; recording = [:]; muted = []; sentTimes = []; heard = [:]; names = [:]; chatOpen = false; notice = "" }
    #endif
}
