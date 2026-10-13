import Foundation
import GameKit
import UIKit
import UserNotifications

/// The hub's party, backed by the real MultiplayerService over the hub's relay room.
@MainActor final class MultiplayerRoom: ClubHubRoom {
    private var service: MultiplayerService { .shared }
    var inLobby: Bool { service.lobby != nil && service.isParty }
    var partySize: Int { service.lobby?.participants.filter { !$0.isGuest }.count ?? 1 }
    var sport: String { service.lobby?.sport.rawValue ?? "tennis" }
    var capacity: Int { service.lobby?.capacity ?? 2 }

    func hostParty(over transport: MultiplayerTransport) throws {
        TennisMenu.shared.online.identity(TennisMenu.shared)
        service.setScreen(SportsSession.shared.displayConnected)
        try service.hostParty(over: transport)
    }
    func joinParty(over transport: MultiplayerTransport, host: String) async throws {
        TennisMenu.shared.online.identity(TennisMenu.shared)
        service.setScreen(SportsSession.shared.displayConnected)
        try await service.joinParty(over: transport, host: host)
    }
    func leaveLobby() { if service.isParty { service.leave() } }
}

extension ClubHub {
    /// Wires the shared hub to the real game: what the player is doing, their name, the TV toast, haptics, push.
    func configureLive() {
        context = {
            let hub = HubSession.shared
            return ClubHubContext(background: UIApplication.shared.applicationState != .active,
                                  inMatch: SportsSession.shared.active,
                                  busyLoading: hub.phase == .booting || hub.arriving)
        }
        playerName = { TennisMenu.shared.player?.name ?? "Player" }
        tvToast = { HubSession.shared.toast($0) }
        onJoined = { HubSession.shared.joinedParty(host: $0) }
        haptic = { if SportsSession.shared.haptics { UINotificationFeedbackGenerator().notificationOccurred(.warning) } }
        requestPushPermission = { ClubPush.requestPermission() }
        ClubPush.registerIfAuthorized()
        ClubSignIn.start()
    }
}

/// Game Center sign-in on every launch, silently (Apple: set the handler as early as possible). Without it a returning player was
/// never signed in after onboarding, so the Club Hub saw them as signed out and nobody could see them online or invite them.
/// The sign-in screen itself is never pushed over the controller: it is offered from the Friends screen (Sign in).
@MainActor enum ClubSignIn {
    private(set) static var started = false
    /// Apple's sign-in screen, when Game Center has one to show (shown only if the player taps Sign in).
    private(set) static var pendingController: UIViewController?
    static func start() {
        guard !started, !ProcessInfo.processInfo.arguments.contains("--lobby-mock") else { return }
        started = true
        Task { @MainActor in
            do { try await MultiplayerService.shared.authenticate { pendingController = $0 } } catch {}
            pendingController = nil
            await ClubHub.shared.refresh()
            askForFriendsOnce()
        }
    }
    /// The Friends screen's Sign in: Apple's screen if Game Center offered one, else the normal flow.
    static func signIn() {
        if let vc = pendingController, let top = GameKitFriendsSource.topViewController() { top.present(vc, animated: true); return }
        Task { @MainActor in
            try? await MultiplayerService.shared.authenticate { vc in GameKitFriendsSource.topViewController()?.present(vc, animated: true) }
            await ClubHub.shared.refresh()
        }
    }
    /// Apple only lists friends who BOTH allowed this game to read their friends list, so ask once (a system prompt), the first
    /// time the player is signed in, instead of waiting for them to find the Friends screen.
    static func askForFriendsOnce() {
        let key = "clubhub.askedFriends"
        guard GKLocalPlayer.local.isAuthenticated, !UserDefaults.standard.bool(forKey: key) else { return }
        Task { @MainActor in
            guard await ClubHub.shared.source.access() == .notDetermined else { return }
            UserDefaults.standard.set(true, forKey: key)
            await ClubHub.shared.allowFriends()
        }
    }
}

/// Invites as iOS notifications (the player is out of the app): a banner with Join / Not now, opening the app into the join.
/// Needs the Push Notifications capability and an APNs key on the service; until the app has the `aps-environment` entitlement
/// registering for remote notifications simply fails and the in-game banner and the hub carry the invites.
enum ClubPush {
    static let category = "CLUB_INVITE", joinAction = "CLUB_JOIN", declineAction = "CLUB_DECLINE"
    #if DEBUG
    static let environment = "sandbox"
    #else
    static let environment = "production"
    #endif

    static func registerCategories() {
        let join = UNNotificationAction(identifier: joinAction, title: "Join", options: [.foreground])
        let decline = UNNotificationAction(identifier: declineAction, title: "Not now", options: [])
        UNUserNotificationCenter.current().setNotificationCategories([
            UNNotificationCategory(identifier: category, actions: [join, decline], intentIdentifiers: [], options: [])])
    }

    /// Asked after the player's first invite, never at launch.
    static func requestPermission() {
        UNUserNotificationCenter.current().requestAuthorization(options: [.alert, .sound, .timeSensitive]) { granted, _ in
            if granted { DispatchQueue.main.async { UIApplication.shared.registerForRemoteNotifications() } }
        }
    }

    /// Already allowed in an earlier run: get this launch's device token.
    static func registerIfAuthorized() {
        UNUserNotificationCenter.current().getNotificationSettings { settings in
            guard [.authorized, .provisional, .ephemeral].contains(settings.authorizationStatus) else { return }
            DispatchQueue.main.async { UIApplication.shared.registerForRemoteNotifications() }
        }
    }

    /// The invite a notification carries (the service's payload: club-hub/PROTOCOL.md, Push).
    static func invite(from userInfo: [AnyHashable: Any], now: Double = Date().timeIntervalSince1970 * 1000) -> HubInvite? {
        guard let inviteID = userInfo["inviteID"] as? String, let code = userInfo["lobbyCode"] as? String else { return nil }
        func number(_ any: Any?) -> Double? { (any as? NSNumber)?.doubleValue ?? (any as? String).flatMap(Double.init) }
        guard let group = number(userInfo["group"]) else { return nil }
        let from: HubPlayerRef
        if let dict = userInfo["from"] as? [String: Any], let id = dict["id"] as? String { from = HubPlayerRef(id: id, name: dict["name"] as? String ?? "A friend") }
        else if let id = userInfo["from"] as? String { from = HubPlayerRef(id: id, name: userInfo["fromName"] as? String ?? "A friend") }
        else { return nil }
        return HubInvite(inviteID: inviteID, from: from, sport: userInfo["sport"] as? String ?? "tennis", lobbyCode: code, group: UInt32(group),
                         capacity: Int(number(userInfo["capacity"]) ?? 2), expiresAt: number(userInfo["expiresAt"]) ?? now + 100_000)
    }
}

/// Foreground: the in-game banner shows instead of the system one. Tapped, or Join / Not now chosen: the hub answers.
final class ClubNotificationDelegate: NSObject, UNUserNotificationCenterDelegate, @unchecked Sendable {
    static let shared = ClubNotificationDelegate()
    func userNotificationCenter(_ center: UNUserNotificationCenter, willPresent notification: UNNotification) async -> UNNotificationPresentationOptions { [] }
    func userNotificationCenter(_ center: UNUserNotificationCenter, didReceive response: UNNotificationResponse) async {
        guard let invite = ClubPush.invite(from: response.notification.request.content.userInfo) else { return }
        let accept: Bool? = switch response.actionIdentifier {
        case ClubPush.joinAction: true
        case ClubPush.declineAction: false
        case UNNotificationDefaultActionIdentifier: nil
        default: nil
        }
        await MainActor.run { ClubHub.shared.notification(invite: invite, accept: accept) }
    }
}
