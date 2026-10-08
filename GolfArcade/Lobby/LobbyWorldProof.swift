#if DEBUG
import Foundation
import simd

/// A two-simulator proof of the walkable lobby, run from the command line (nothing in the shipped app calls it):
///   host:  --world-proof host  --world-proof-output <dir>
///   guest: --world-proof guest --world-proof-output <dir>
/// The host opens a nearby lobby from the party board, the guest joins it, both end up in the world, both walk a scripted route and one plays an emote.
/// Each writes `<role>.tsv`: one line a second with where it is and where it sees the other, so the two files can be compared.
@MainActor enum LobbyWorldProof {
    private static var started = false
    private static var logURL: URL?
    static func event(_ name: String, _ detail: String = "") {
        guard let logURL else { return }
        let line = "\(String(format: "%.2f", ProcessInfo.processInfo.systemUptime))\t\(name)\t\(detail)\n"
        if let data = line.data(using: .utf8), let handle = try? FileHandle(forWritingTo: logURL) { _ = try? handle.seekToEnd(); try? handle.write(contentsOf: data); try? handle.close() }
    }

    static func start(_ menu: TennisMenu, args: [String]) {
        guard !started, let i = args.firstIndex(of: "--world-proof"), let role = args[safe: i + 1] else { return }
        started = true
        let host = role == "host"
        let output = args.firstIndex(of: "--world-proof-output").flatMap { args[safe: $0 + 1] } ?? NSTemporaryDirectory()
        try? FileManager.default.createDirectory(atPath: output, withIntermediateDirectories: true)
        logURL = URL(fileURLWithPath: output).appendingPathComponent("\(role).tsv"); try? Data().write(to: logURL!)
        Task { @MainActor in
            let service = menu.online.service, world = LobbyWorld.shared
            // a simulator that is not on screen never fires a display link: drive the world's clock from a timer
            world.externalClock = true
            let clock = Timer(timeInterval: 1.0 / 30, repeats: true) { _ in MainActor.assumeIsolated { world.advance(now: ProcessInfo.processInfo.systemUptime) } }
            RunLoop.main.add(clock, forMode: .common)
            do {
                var p = Player(name: host ? "Adnan" : "Sam", colorIndex: host ? 0 : 1, handedness: .right)
                p.standardFemale = !host; p.setOutfitHex("shirt", host ? "FF6B4A" : "D3F34B"); p.setOutfitHex("shorts", host ? "101D35" : "FAF8F3")
                SportsSession.shared.players = [p]; SportsSession.shared.playerIndex = 0
                if menu.screen == .title { menu.tap("start") }
                try await Task.sleep(for: .seconds(1))
                event("world", "\(menu.screen)")
                // the party board: nearby
                world.perform(try require(LobbyLayout.standard.station("party-board")), menu: menu)
                menu.tap("partyNearby")
                if host { menu.tap("net-host"); event("hosting") }
                else {
                    var found: LocalMultiplayerTransport.DiscoveredLobby?
                    for _ in 0..<600 { found = service.discoveredLobbies.first { $0.name == "Adnan" }; if found != nil { break }; try await Task.sleep(for: .milliseconds(100)) }
                    menu.tap("net-join-\(try require(found).id)"); event("joining")
                }
                for n in 0..<600 {
                    if (service.lobby?.participants.count ?? 0) >= 2 && menu.screen == .world { break }
                    if n % 20 == 0 { event("waiting", "screen=\(menu.screen) people=\(service.lobby?.participants.count ?? 0) found=\(service.discoveredLobbies.map(\.name)) error=\(service.lastError ?? "-") notice=\(menu.notice)") }
                    try await Task.sleep(for: .milliseconds(100))
                }
                guard (service.lobby?.participants.count ?? 0) >= 2 else { throw MultiplayerError.unavailable("the party never formed") }
                event("in-world", "screen=\(menu.screen) people=\(service.lobby?.participants.count ?? 0)")
                try await Task.sleep(for: .seconds(2))
                // a route: north, then east, then back, then stand and emote
                let route: [(SIMD2<Float>, Double)] = [(SIMD2(1, 0), 1.6), (SIMD2(0, 1), 2.0), (SIMD2(0, 0), 1.5), (SIMD2(-1, 0), 1.6), (SIMD2(0, 0), 2.5)]
                var t = 0.0, step = 0, stepUntil = 0.0
                if !host { try await Task.sleep(for: .seconds(1)) }
                while t < 16 {
                    if t >= stepUntil, step < route.count { world.setStick(route[step].0); stepUntil = t + route[step].1; step += 1 }
                    if abs(t - 12.0) < 0.26 { world.playEmote(slot: 0); event("emote") }
                    let me = world.localPosition
                    let others = service.lobby?.participants.filter { $0.id != service.localID }.compactMap { o in world.remotePosition(o.id).map { "\(o.name)=(\(String(format: "%.1f,%.1f", $0.x, $0.y))) heard=(\(String(format: "%.1f,%.1f", world.remoteTarget(o.id)?.x ?? 0, world.remoteTarget(o.id)?.y ?? 0)))" } } ?? []
                    event("pos", "me=(\(String(format: "%.1f,%.1f", me.x, me.y))) sees \(others.joined(separator: " ")) remotes=\(world.remoteCount) screen=\(menu.screen)")
                    try await Task.sleep(for: .milliseconds(500)); t += 0.5
                }
                world.setStick(SIMD2(0, 0))
                event("finished")
            } catch { event("error", error.localizedDescription) }
        }
    }
    private static func require<T>(_ value: T?) throws -> T { guard let value else { throw MultiplayerError.unavailable("not found") }; return value }
}
#endif
