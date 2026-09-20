import SwiftUI

struct ContentView: View {
    @StateObject private var flow: GameFlow

    init() {
        // Old camera preferences must never reopen tracking after an upgrade.
        let defaults = UserDefaults.standard
        ControllerPreferences.migrate(defaults: defaults)
        #if DEBUG
        let arguments = ProcessInfo.processInfo.arguments
        if arguments.contains("-skipPlayerCalibration") || arguments.contains("-fixturePlayers") {
            let count = arguments.firstIndex(of: "-fixturePlayers")
                .flatMap { arguments.indices.contains($0 + 1) ? Int(arguments[$0 + 1]) : nil } ?? 1
            let players = (0..<max(1, min(count, GameFlow.maxPlayers))).map {
                Player(name: "Player \($0 + 1)", colorIndex: $0)
            }
            defaults.set(arguments.contains("-phoneController") ? "phone" : "touch", forKey: "range.swingInput")
            let flow = GameFlow(fixturePlayers: players)
            if let index = arguments.firstIndex(of: "-startCourse"), arguments.indices.contains(index + 1),
               let course = Course.all.first(where: { $0.id == arguments[index + 1] }) ??
                    Course.all.first(where: { $0.difficulty.rawValue == arguments[index + 1] }) {
                if count > 1 { flow.choose(.multiplayer) }
                flow.play(course)
            }
            _flow = StateObject(wrappedValue: flow)
            return
        }
        #endif
        _flow = StateObject(wrappedValue: GameFlow())
    }

    var body: some View {
        ZStack {
            switch flow.screen {
            case .menu: MainMenuView(flow: flow)
            case .players: PlayerSetupView(flow: flow)
            case .courses: CourseSelectView(flow: flow)
            case .playing:
                #if NATIVE_ONLY
                NativeCourseScreen(flow: flow)
                #else
                if DisplayCoordinator.usesNativeRenderer { NativeCourseScreen(flow: flow) }
                else { CourseScreen(flow: flow) }
                #endif
            case .practice:
                #if NATIVE_ONLY
                NativeCourseScreen(flow: flow, practice: true)
                #else
                if DisplayCoordinator.usesNativeRenderer { NativeCourseScreen(flow: flow, practice: true) }
                else { CourseScreen(flow: flow, practice: true) }
                #endif
            }
        }
        .foregroundStyle(Palette.cream)
        .preferredColorScheme(.dark)
    }
}

#Preview { ContentView() }

enum ControllerPreferences {
    static func migrate(defaults: UserDefaults) {
        if defaults.integer(forKey: "controller.version") < 1 ||
            defaults.string(forKey: "range.swingInput").flatMap(SwingInput.init(rawValue:)) == nil {
            defaults.set(SwingInput.phone.rawValue, forKey: "range.swingInput")
        }
        defaults.set(1, forKey: "controller.version")
        defaults.set(false, forKey: "gestures.enabled")
    }
}
