import Foundation

enum OnboardingStep: Equatable {
    case account, character, connect, motionPrimer, choose, tutorial(Sport), reward(Sport), done
    var key: String {
        switch self {
        case .account: "account"; case .character: "character"; case .connect: "connect"
        case .motionPrimer: "motionPrimer"; case .choose: "choose"; case .done: "done"
        case .tutorial(let sport): "tutorial:\(sport.rawValue)"
        case .reward(let sport): "reward:\(sport.rawValue)"
        }
    }
    init?(key: String) {
        switch key {
        case "account": self = .account; case "character": self = .character
        case "connect": self = .connect; case "motionPrimer": self = .motionPrimer
        case "choose": self = .choose; case "done": self = .done
        default:
            let pair = key.split(separator: ":").map(String.init)
            guard pair.count == 2, let sport = Sport(rawValue: pair[1]), sport.playable else { return nil }
            if pair[0] == "tutorial" { self = .tutorial(sport) }
            else if pair[0] == "reward" { self = .reward(sport) }
            else { return nil }
        }
    }
}

@MainActor
final class OnboardingStore {
    let defaults: UserDefaults
    init(defaults: UserDefaults = .standard) { self.defaults = defaults }
    var step: OnboardingStep {
        get { defaults.string(forKey: "ftue.step.v1").flatMap(OnboardingStep.init(key:)) ?? .account }
        set { defaults.set(newValue.key, forKey: "ftue.step.v1") }
    }
    var tutorialStep: Int {
        get { min(4, max(0, defaults.integer(forKey: "ftue.tutorialStep.v1"))) }
        set { defaults.set(min(4, max(0, newValue)), forKey: "ftue.tutorialStep.v1") }
    }
    var account: String? {
        get { defaults.string(forKey: "ftue.account.v1") }
        set { defaults.set(newValue, forKey: "ftue.account.v1") }
    }
    var explicitRun: Bool {
        get { defaults.bool(forKey: "ftue.explicitRun.v1") }
        set { defaults.set(newValue, forKey: "ftue.explicitRun.v1") }
    }
    var characterInitialized: Bool {
        get { defaults.bool(forKey: "ftue.characterInitialized.v1") }
        set { defaults.set(newValue, forKey: "ftue.characterInitialized.v1") }
    }
    var touch: Bool {
        get { defaults.object(forKey: "ftue.touch.v1") as? Bool ?? true }
        set { defaults.set(newValue, forKey: "ftue.touch.v1") }
    }
    var installedAt: TimeInterval {
        if let time = defaults.object(forKey: "ftue.installedAt.v1") as? Double { return time }
        let time = Date().timeIntervalSince1970
        defaults.set(time, forKey: "ftue.installedAt.v1")
        return time
    }
    func guest() -> String {
        let id = defaults.string(forKey: "ftue.guestID.v1") ?? UUID().uuidString
        defaults.set(id, forKey: "ftue.guestID.v1")
        let key = "guest:\(id)"; account = key; return key
    }
    func rewardClaimed(_ sport: Sport) -> Bool { defaults.bool(forKey: "ftue.reward.\(sport.rawValue).v1") }
    var anyRewardClaimed: Bool { [.tennis, .golf].contains { rewardClaimed($0) } }
    func markReward(_ sport: Sport, xp: Int, item: String?) {
        defaults.set(true, forKey: "ftue.reward.\(sport.rawValue).v1")
        defaults.set(xp, forKey: "ftue.rewardXP.\(sport.rawValue).v1")
        defaults.set(item, forKey: "ftue.rewardItem.\(sport.rawValue).v1")
        if let item { requestEquip(item) }
    }
    func rewardXP(_ sport: Sport) -> Int { defaults.integer(forKey: "ftue.rewardXP.\(sport.rawValue).v1") }
    func rewardItem(_ sport: Sport) -> String? { defaults.string(forKey: "ftue.rewardItem.\(sport.rawValue).v1") }
    var requestedHeadband: Bool { defaults.string(forKey: "ftue.equipRequested.v1") == "white-headband" }
    func requestEquip(_ item: String) { defaults.set(item, forKey: "ftue.equipRequested.v1") }
    func reset() {
        for key in defaults.dictionaryRepresentation().keys where key.hasPrefix("ftue.") { defaults.removeObject(forKey: key) }
    }
    static func name(_ value: String) -> String {
        let trimmed = value.trimmingCharacters(in: .whitespacesAndNewlines)
        return trimmed.isEmpty ? "Player 1" : String(trimmed.prefix(40))
    }
}
