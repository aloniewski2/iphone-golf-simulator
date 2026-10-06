import GameKit
import SwiftUI

enum GameCenterAccountResult: Equatable {
    case signedIn(id: String, name: String), declined, unavailable, error
    var analyticsValue: String {
        switch self { case .signedIn: "signedIn"; case .declined: "declined"; case .unavailable: "unavailable"; case .error: "error" }
    }
}
@MainActor protocol GameCenterAccount {
    func authenticate(present: @escaping (UIViewController) -> Void) async -> GameCenterAccountResult
}
@MainActor struct LiveGameCenterAccount: GameCenterAccount {
    func authenticate(present: @escaping (UIViewController) -> Void) async -> GameCenterAccountResult {
        if GKLocalPlayer.local.isAuthenticated { return .signedIn(id: GKLocalPlayer.local.teamPlayerID, name: GKLocalPlayer.local.displayName) }
        do {
            try await MultiplayerService.shared.authenticate(present: present)
            guard GKLocalPlayer.local.isAuthenticated else { return .declined }
            return .signedIn(id: GKLocalPlayer.local.teamPlayerID, name: GKLocalPlayer.local.displayName)
        } catch {
            return (error as NSError).code == GKError.Code.cancelled.rawValue ? .declined : .error
        }
    }
}
#if DEBUG
@MainActor struct FakeGameCenterAccount: GameCenterAccount {
    let result: GameCenterAccountResult
    var delay = 0.0
    func authenticate(present: @escaping (UIViewController) -> Void) async -> GameCenterAccountResult {
        if delay > 0 { try? await Task.sleep(for: .seconds(delay)) }
        return result
    }
}
#endif

@MainActor @Observable final class OnboardingAccountModel {
    struct Sheet: Identifiable { let id = UUID(); let controller: UIViewController }
    var sheet: Sheet?
    private(set) var message = ""
    private(set) var waiting = false
    private(set) var guestPrimary = false
    private var attempted = false
    private var receivedResult = false
    private var ended = false
    private var authentication: Task<Void, Never>?
    private var timeout: Task<Void, Never>?
    private let account: any GameCenterAccount
    init(account: (any GameCenterAccount)? = nil) {
        #if DEBUG
        let args = ProcessInfo.processInfo.arguments
        if let i = args.firstIndex(of: "-fakeGameCenter"), args.indices.contains(i + 1) {
            let result: GameCenterAccountResult = args[i + 1] == "signedIn" ? .signedIn(id: "debug-player", name: "Island Player") : args[i + 1] == "decline" ? .declined : .unavailable
            let delay = min(7, max(0, Double(ProcessInfo.processInfo.environment["ONBOARDING_FAKE_GC_DELAY"] ?? "0") ?? 0))
            self.account = account ?? FakeGameCenterAccount(result: result, delay: delay); return
        }
        #endif
        self.account = account ?? LiveGameCenterAccount()
    }
    func start(_ flow: OnboardingFlow) {
        guard !attempted else { return }; attempted = true; waiting = true
        authentication = Task { [weak self] in
            guard let self else { return }
            let result = await account.authenticate { [weak self] controller in
                guard let self, !ended, flow.step == .account else { return }
                sheet = Sheet(controller: controller)
            }
            guard !Task.isCancelled, !ended, flow.step == .account else { return }
            receivedResult = true; timeout?.cancel(); waiting = false; sheet = nil
            Analytics.track("gc_signin", ["result": result.analyticsValue])
            switch result {
            case .signedIn(let id, let name):
                flow.store.account = "gc:\(id)"; message = "Signed in as \(name) ✓"
                try? await Task.sleep(for: .milliseconds(800))
                guard !ended, !Task.isCancelled, flow.step == .account else { return }
                ended = true; flow.next()
            case .declined, .unavailable, .error:
                message = "You can sign in later from Play Online."
                try? await Task.sleep(for: .milliseconds(800))
                guard !ended, !Task.isCancelled, flow.step == .account else { return }
                guest(flow)
            }
        }
        timeout = Task { [weak self] in
            try? await Task.sleep(for: .seconds(8))
            guard let self, !Task.isCancelled, !ended, flow.step == .account, sheet == nil else { return }
            guestPrimary = true; waiting = false
            message = "You can sign in later from Play Online."
            Analytics.track("gc_signin", ["result": "unavailable"])
        }
    }
    func guest(_ flow: OnboardingFlow) {
        guard !ended, flow.step == .account else { return }
        ended = true; authentication?.cancel(); timeout?.cancel(); sheet = nil; flow.guest()
    }
    func dismissed(_ flow: OnboardingFlow) {
        guard !ended, !receivedResult, flow.step == .account, !GKLocalPlayer.local.isAuthenticated else { return }
        Analytics.track("gc_signin", ["result": "declined"]); guest(flow)
    }
}
