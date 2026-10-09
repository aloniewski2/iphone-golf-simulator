@preconcurrency import GameKit
import UIKit

private struct GameKitDelivery<T>: @unchecked Sendable { let value: T }

/// No UI is installed in the app. Future UI may present returned Apple controllers.
@MainActor
final class GameCenterTransport: NSObject, MultiplayerTransport, GKMatchDelegate, GKLocalPlayerListener, GKMatchmakerViewControllerDelegate {
    var localID: String { GKLocalPlayer.local.gamePlayerID }
    var peers: [String] { match?.players.map(\.gamePlayerID).filter { !disconnected.contains($0) } ?? [] }
    var onData: ((Data, String) -> Void)?
    var onPeersChanged: (() -> Void)?
    var onError: ((Error) -> Void)?
    var onConnected: (() -> Void)?
    var onInvitation: ((GKInvite) -> Void)?
    private(set) var match: GKMatch?
    private var generation = 0
    private var registered = false
    private var disconnected: Set<String> = []
    private var activeController: GKMatchmakerViewController?
    override init() { super.init(); register() }
    var capacity: Int { min(4, GKMatchRequest.maxPlayersAllowedForMatch(of: .peerToPeer)) }

    func authenticate(present: @escaping (UIViewController) -> Void) async throws {
        if GKLocalPlayer.local.isAuthenticated { register(); return }
        try await withCheckedThrowingContinuation { (continuation: CheckedContinuation<Void, Error>) in
            var completed = false
            GKLocalPlayer.local.authenticateHandler = { [weak self] controller, error in
                Task { @MainActor in
                    if let controller { present(controller); return }
                    guard !completed else { return }; completed = true
                    if let error { continuation.resume(throwing: error) }
                    else if GKLocalPlayer.local.isAuthenticated { self?.register(); continuation.resume() }
                    else { continuation.resume(throwing: MultiplayerError.unavailable("Sign in to Game Center to play online.")) }
                }
            }
        }
    }
    private func register() { if !registered { GKLocalPlayer.local.register(self); registered = true } }
    private func request(sport: MultiplayerSport?, count: Int) throws -> GKMatchRequest {
        guard GKLocalPlayer.local.isAuthenticated else { throw MultiplayerError.unavailable("Game Center is not signed in.") }
        guard capacity >= count, count >= 2 else { throw MultiplayerError.unavailable("Game Center cannot connect that many players on this OS.") }
        let request = GKMatchRequest(); request.minPlayers = 2; request.maxPlayers = count; request.defaultNumberOfPlayers = count
        // Stable, versioned queues; private parties are sport-neutral.
        request.playerGroup = sport == .tennis ? 0x4d500201 : sport == .golf ? 0x4d500202 : 0x4d500200
        return request
    }
    func invitationController() throws -> GKMatchmakerViewController {
        guard activeController == nil else { throw MultiplayerError.invalidOperation("An invitation request is already open.") }
        guard let controller = GKMatchmakerViewController(matchRequest: try request(sport: nil, count: 4)) else { throw MultiplayerError.unavailable("Game Center matchmaking is unavailable.") }
        controller.matchmakerDelegate = self; controller.matchmakingMode = .inviteOnly
        if let match { controller.addPlayers(to: match) }
        activeController = controller
        return controller
    }
    func accept(_ invite: GKInvite) throws -> GKMatchmakerViewController {
        guard match == nil else { throw MultiplayerError.invalidOperation("Leave the current lobby before accepting another invitation.") }
        guard activeController == nil else { throw MultiplayerError.invalidOperation("An invitation request is already open.") }
        guard let controller = GKMatchmakerViewController(invite: invite) else { throw MultiplayerError.unavailable("The invitation is no longer available.") }
        controller.matchmakerDelegate = self; activeController = controller; return controller
    }
    func quickMatch(sport: MultiplayerSport, count: Int = 2) async throws {
        guard match == nil else { throw MultiplayerError.invalidOperation("Already connected to a lobby.") }
        let request = try request(sport: sport, count: sport == .tennis ? 2 : count)
        // Programmatic matchmaking works on iOS 17 too. Use a fixed requested group size;
        // partial-fill UI is deliberately deferred rather than pretending a timeout can start it.
        request.minPlayers = request.maxPlayers
        generation += 1; let token = generation
        let found = try await GKMatchmaker.shared().findMatch(for: request)
        guard token == generation else { found.disconnect(); throw CancellationError() }
        attach(found)
    }
    func findMore(sport: MultiplayerSport) async throws {
        guard let match, peers.count + 1 < capacity else { throw MultiplayerError.full }
        let request = try request(sport: sport, count: 4)
        try await GKMatchmaker.shared().addPlayers(to: match, matchRequest: request)
        GKMatchmaker.shared().finishMatchmaking(for: match)
    }
    func cancelSearch() { generation += 1; GKMatchmaker.shared().cancel(); activeController?.dismiss(animated: true); activeController = nil }
    private func attach(_ found: GKMatch) {
        if let previous = match, previous !== found { previous.disconnect() }
        match = found; disconnected.removeAll(); found.delegate = self
        GKMatchmaker.shared().finishMatchmaking(for: found)
        onConnected?(); onPeersChanged?()
    }
    func send(_ data: Data, to ids: [String]?, reliable: Bool) throws {
        guard data.count <= MultiplayerPacket.maximumBytes, let match else { throw MultiplayerError.unavailable("No Game Center connection.") }
        let mode: GKMatch.SendDataMode = reliable ? .reliable : .unreliable
        if let ids { try match.send(data, to: match.players.filter { ids.contains($0.gamePlayerID) }, dataMode: mode) }
        else { try match.sendData(toAllPlayers: data, with: mode) }
    }
    func disconnect() { cancelSearch(); match?.delegate = nil; match?.disconnect(); match = nil; onPeersChanged?() }
    nonisolated func match(_ match: GKMatch, didReceive data: Data, fromRemotePlayer player: GKPlayer) {
        let id = player.gamePlayerID, delivery = GameKitDelivery(value: match)
        Task { @MainActor [weak self] in guard self?.match === delivery.value else { return }; self?.onData?(data, id) }
    }
    nonisolated func match(_ match: GKMatch, player: GKPlayer, didChange state: GKPlayerConnectionState) {
        let delivery = GameKitDelivery(value: match), id = player.gamePlayerID, connected = state == .connected
        Task { @MainActor [weak self] in
            guard let self, self.match === delivery.value else { return }
            if connected { self.disconnected.remove(id) } else { self.disconnected.insert(id) }
            self.onPeersChanged?()
        }
    }
    nonisolated func match(_ match: GKMatch, didFailWithError error: Error?) {
        Task { @MainActor [weak self] in if let error { self?.onError?(error) } }
    }
    nonisolated func match(_ match: GKMatch, shouldReinviteDisconnectedPlayer player: GKPlayer) -> Bool { true }
    nonisolated func player(_ player: GKPlayer, didAccept invite: GKInvite) {
        let delivery = GameKitDelivery(value: invite)
        Task { @MainActor [weak self] in self?.onInvitation?(delivery.value) }
    }
    nonisolated func matchmakerViewControllerWasCancelled(_ viewController: GKMatchmakerViewController) {
        Task { @MainActor [weak self] in guard self?.activeController === viewController else { return }; self?.cancelSearch() }
    }
    nonisolated func matchmakerViewController(_ viewController: GKMatchmakerViewController, didFailWithError error: Error) {
        Task { @MainActor [weak self] in guard self?.activeController === viewController else { return }; viewController.dismiss(animated: true); self?.activeController = nil; self?.onError?(error) }
    }
    nonisolated func matchmakerViewController(_ viewController: GKMatchmakerViewController, didFind match: GKMatch) {
        let delivery = GameKitDelivery(value: match)
        Task { @MainActor [weak self] in
            guard self?.activeController === viewController else { delivery.value.disconnect(); return }
            viewController.dismiss(animated: true); self?.activeController = nil; self?.attach(delivery.value)
        }
    }
}
