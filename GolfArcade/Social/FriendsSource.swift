import Foundation
import GameKit
import UIKit

struct FriendProfile: Equatable, Identifiable, Sendable {
    var id: String
    var name: String
}

enum FriendsAccess: Equatable, Sendable {
    /// Game Center has not signed this player in (or it is off).
    case signedOut
    /// The player has not been asked yet: the next friends request shows Apple's prompt.
    case notDetermined
    case authorized
    case denied
    case restricted
}

/// Game Center's friend graph, behind a protocol so tests and previews use their own. Game Center keeps the friend relationship
/// (add, accept, remove, block all happen in Apple's UI); we only read it.
@MainActor protocol FriendsSource: AnyObject {
    var localID: String { get }
    var localName: String { get }
    func access() async -> FriendsAccess
    /// Asks Apple's prompt if the player has not been asked, then reads the friends list.
    func friends() async throws -> [FriendProfile]
    func recent() async throws -> [FriendProfile]
    /// Apple's friend-request screen (we cannot build our own).
    func presentAddFriend() throws
    /// Apple's identity verification of the local player, for the service to check.
    func identity() async throws -> HubClientMessage.Auth
}

/// Ids are Game Center **teamPlayerID**s: that is the id Apple's identity signature is made over (Apple's verification steps for
/// fetchItems(forIdentityVerificationSignature:)), so the Club Hub can check it. (A gamePlayerID would fail every real sign-in.)
@MainActor final class GameKitFriendsSource: FriendsSource {
    var localID: String { GKLocalPlayer.local.teamPlayerID }
    var localName: String { GKLocalPlayer.local.displayName }

    func access() async -> FriendsAccess {
        guard GKLocalPlayer.local.isAuthenticated else { return .signedOut }
        switch (try? await GKLocalPlayer.local.loadFriendsAuthorizationStatus()) {
        case .authorized?: return .authorized
        case .denied?: return .denied
        case .restricted?: return .restricted
        default: return .notDetermined
        }
    }

    func friends() async throws -> [FriendProfile] {
        try await GKLocalPlayer.local.loadFriends().map { FriendProfile(id: $0.teamPlayerID, name: $0.displayName) }
    }

    func recent() async throws -> [FriendProfile] {
        try await GKLocalPlayer.local.loadRecentPlayers().map { FriendProfile(id: $0.teamPlayerID, name: $0.displayName) }
    }

    func presentAddFriend() throws {
        guard let top = Self.topViewController() else { throw MultiplayerError.unavailable("There is nothing to show Game Center on right now.") }
        try GKLocalPlayer.local.presentFriendRequestCreator(from: top)
    }

    func identity() async throws -> HubClientMessage.Auth {
        guard GKLocalPlayer.local.isAuthenticated else { throw MultiplayerError.unavailable("Sign in to Game Center first.") }
        let id = GKLocalPlayer.local.teamPlayerID
        return try await withCheckedThrowingContinuation { continuation in
            GKLocalPlayer.local.fetchItems { url, signature, salt, timestamp, error in
                if let url, let signature, let salt {
                    continuation.resume(returning: .apple(playerID: id, bundleID: Bundle.main.bundleIdentifier ?? "", publicKeyURL: url.absoluteString,
                                                          signature: signature, salt: salt, timestamp: timestamp))
                } else {
                    continuation.resume(throwing: error ?? MultiplayerError.unavailable("Game Center could not verify you."))
                }
            }
        }
    }

    /// The view controller on top: Apple's sheets present from it.
    static func topViewController() -> UIViewController? {
        let scene = UIApplication.shared.connectedScenes.compactMap { $0 as? UIWindowScene }.first { $0.activationState == .foregroundActive }
        var top = scene?.windows.first(where: \.isKeyWindow)?.rootViewController
        while let next = top?.presentedViewController { top = next }
        return top
    }
}

/// Where the Club Hub service is and how invite links look. Everything has a launch-argument / defaults override so a build can
/// point at a dev service without a rebuild: `-clubHubURL ws://host:8090/v1/hub`, `-clubHubDevAuth 1`.
enum ClubHubConfig {
    static func override(_ key: String) -> String? {
        let args = ProcessInfo.processInfo.arguments
        if let i = args.firstIndex(of: "-" + key), args.indices.contains(i + 1) { return args[i + 1] }
        return UserDefaults.standard.string(forKey: key)
    }
    /// The WebSocket URL, or nil when no service is configured (the hub then shows Game Center friends without presence).
    static var serverURL: URL? {
        let text = override("clubHubURL") ?? (Bundle.main.object(forInfoDictionaryKey: "ClubHubServerURL") as? String) ?? ""
        return text.isEmpty ? nil : URL(string: text)
    }
    /// A dev service accepts a bare id instead of Apple's signature (HUB_DEV_AUTH=1 on the service).
    static var devAuth: Bool { override("clubHubDevAuth") == "1" }
    /// https page for people without the app, `<base>/j/<CODE>`; the same host as the service unless set.
    static var inviteBaseURL: URL? {
        if let text = override("clubHubInviteBase") ?? (Bundle.main.object(forInfoDictionaryKey: "ClubHubInviteBaseURL") as? String), !text.isEmpty { return URL(string: text) }
        guard let server = serverURL, var c = URLComponents(url: server, resolvingAgainstBaseURL: false) else { return nil }
        c.scheme = c.scheme == "wss" ? "https" : "http"; c.path = ""; c.query = nil
        return c.url
    }
    static let codeAlphabet = Array("ABCDEFGHJKLMNPQRSTUVWXYZ23456789")
    static func newCode() -> String { String((0..<6).map { _ in codeAlphabet.randomElement()! }) }
    /// The private Game Center room's group number: random, away from the fixed groups quick match uses (0x4d500200…0x4d5002ff).
    static func newGroup() -> UInt32 {
        while true { let g = UInt32.random(in: 1...UInt32.max); if !(0x4d500200...0x4d5002ff).contains(g) { return g } }
    }
    static func normalizedCode(_ text: String) -> String? {
        // accepts "ABC234", "abc 234", a motionclub://j/ABC234 or https://host/j/ABC234 link
        var t = text.trimmingCharacters(in: .whitespacesAndNewlines).uppercased()
        if let r = t.range(of: "/J/", options: .backwards) { t = String(t[r.upperBound...].prefix { $0.isLetter || $0.isNumber }) }   // (stops at ? or #)
        t = t.filter { codeAlphabet.contains($0) }
        return t.count == 6 ? t : nil
    }
    /// The words and link to send to someone who may not have the app.
    static func shareMessage(code: String, from name: String, sport: String) -> String {
        let game = sport == "golf" ? "golf" : "tennis"
        let link = inviteBaseURL.map { $0.appendingPathComponent("j").appendingPathComponent(code).absoluteString } ?? "motionclub://j/\(code)"
        return "\(name) invited you to play \(game) in Motion Club. Join here: \(link)\nInvite code: \(code)"
    }
}
