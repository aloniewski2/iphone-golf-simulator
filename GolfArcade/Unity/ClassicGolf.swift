import SwiftUI

/// Golf the way it played before the TV controller, opened from the Golf menu (Play Golf): Unity's own
/// clubhouse, course screen (and its flyover), the round on this phone's screen, and the controller
/// sheet (the map, the club cards, the aim pad) when a TV takes the course. The native session stays
/// idle while it runs; Unity's phone window is in front until its "Main menu" button hands back.
/// Tennis and the rest keep the native controller.
@MainActor @Observable
final class ClassicGolf {
    static let shared = ClassicGolf()
    /// Unity is loading or showing golf.
    private(set) var active = false
    /// Unity's window is in front (loading has finished).
    private(set) var shown = false
    private var sessionID = ""
    private var message: [String: Any]?
    private var timer: Timer?
    private var startedAt = Date.distantPast
    private var lastExternal = false

    /// From the menu: show the loading screen, start Unity on the phone and open its golf clubhouse.
    func start(menu: TennisMenu = .shared) {
        let session = SportsSession.shared
        guard !active, !session.active, !session.players.isEmpty else { return }
        SportsDisplays.shared.refresh()
        do { try SportsRuntime.shared().loadOnPhone() }
        catch { menu.classicGolfFailed(error.localizedDescription); return }
        let p = session.players[min(session.playerIndex, session.players.count - 1)]
        sessionID = UUID().uuidString
        message = ["version": 1, "session": sessionID, "action": "classic", "sport": "golf",
                   "playerID": p.id.uuidString, "playerName": p.name, "female": p.standardFemale, "skin": p.standardSkin,
                   "left": p.handedness == .left, "sound": session.sound, "haptics": session.haptics,
                   "token": Int(Int32.random(in: 1...Int32.max)), "fps": session.highFrameRate ? 120 : 60,
                   "external": SportsDisplays.shared.external != nil,
                   "shirt": p.outfitHex("shirt") ?? "", "shorts": p.outfitHex("shorts") ?? "", "accent": p.outfitHex("accent") ?? "",
                   "hairStyle": p.hairStyle, "hairColor": p.hairColor]
        active = true; shown = false; startedAt = Date(); lastExternal = SportsDisplays.shared.external != nil
        session.loading.begin(now: Date())
        session.loading.onFinish = { [weak self] in self?.reveal() }
        menu.showClassicGolfLoading()
        SportsRuntime.shared().pause(false)
        timer?.invalidate()
        let timer = Timer(timeInterval: 0.05, repeats: true) { [weak self] _ in MainActor.assumeIsolated { self?.poll() } }
        RunLoop.main.add(timer, forMode: .common); self.timer = timer
        // An already-running Unity takes it now; a fresh one asks again when it boots.
        send()
        NSLog("[ClassicGolf] start session=%@ external=%d", sessionID, SportsDisplays.shared.external != nil ? 1 : 0)
    }

    /// Back on the loading screen, or a failed load: Unity goes quiet and the menus come back.
    func cancel() { finish(notice: nil) }

    /// The TV came or went while golf is up: Unity moves the course to it (or back to the phone).
    /// The app's own TV picture stays up until Unity says the course is really there ("classicTV").
    private func externalChanged(connected: Bool) {
        guard active else { return }
        NSLog("[ClassicGolf] TV %@", connected ? "connected" : "gone")
        SportsRuntime.shared().send(json(["version": 1, "session": sessionID, "action": "classicDisplay", "value": connected ? 1 : 0]))
    }

    private func send() {
        guard let message else { return }
        SportsRuntime.shared().send(json(message))
    }

    private func json(_ object: [String: Any]) -> String {
        guard let data = try? JSONSerialization.data(withJSONObject: object), let text = String(data: data, encoding: .utf8) else { return "{}" }
        return text
    }

    private func poll() {
        let loading = SportsSession.shared.loading
        loading.tick(now: Date())
        // A TV mirrored (or dropped) mid-round: the external scene sets SportsDisplays.external.
        let connected = SportsDisplays.shared.external != nil
        if connected != lastExternal { lastExternal = connected; externalChanged(connected: connected) }
        if !shown, Date().timeIntervalSince(startedAt) > 45 { finish(notice: "Golf did not finish loading. Try the gate again."); return }
        for _ in 0..<64 {
            guard let text = SportsRuntime.shared().pollEvent(), let data = text.data(using: .utf8),
                  let event = (try? JSONSerialization.jsonObject(with: data)) as? [String: Any] else { break }
            let type = event["type"] as? String ?? ""
            if type == "boot" { loading.reach(0.2); send(); continue }
            guard event["session"] as? String == sessionID else { continue }
            switch type {
            case "loadProgress":
                if let p = Double(event["message"] as? String ?? "") { loading.reach(0.2 + 0.6 * p) }
            case "classicReady":
                message = nil
                loading.markReady()
            case "classicExit":
                finish(notice: nil); return
            case "classicTV":
                // "1": the course is drawing on the TV, so the app's own TV picture steps aside
                let live = event["message"] as? String == "1"
                NSLog("[ClassicGolf] course on the TV: %@", live ? "yes" : "no")
                SportsDisplays.shared.external?.isHidden = live
            case "error":
                NSLog("[ClassicGolf] %@", text)
                if !shown { finish(notice: event["message"] as? String ?? "Golf could not start."); return }
            default: break
            }
        }
    }

    /// Loading has run its course: Unity's golf is in front on the phone (the TV follows on "classicTV").
    private func reveal() {
        guard active, !shown else { return }
        shown = true
        SportsRuntime.shared().showUnity(onPhone: true)
        NSLog("[ClassicGolf] showing Unity golf")
    }

    private func finish(notice: String?) {
        guard active else { return }
        timer?.invalidate(); timer = nil
        let session = SportsSession.shared
        session.loading.cancel()
        SportsRuntime.shared().showUnity(onPhone: false)
        SportsRuntime.shared().pause(true)
        active = false; shown = false; message = nil
        SportsDisplays.shared.phone?.makeKeyAndVisible()
        SportsDisplays.shared.showMenu()
        TennisMenu.shared.classicGolfEnded(notice: notice)
        NSLog("[ClassicGolf] back to the menus%@", notice.map { ": \($0)" } ?? "")
    }
}
