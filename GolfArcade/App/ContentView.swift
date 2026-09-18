import AVFoundation
import SwiftUI

struct ContentView: View {
    // Own the controller without subscribing the entire navigation tree to every
    // pose. Camera screens observe it locally; menus still share the same instance.
    @StateObject private var cameraOwner = CameraControllerOwner()
    private var camera: CameraSwingController { cameraOwner.controller }
    @StateObject private var flow: GameFlow
    @AppStorage("range.swingInput") private var swingInput: SwingInput = .camera
    @AppStorage("gestures.enabled") private var gesturesEnabled = true
    @Environment(\.scenePhase) private var appPhase

    init() {
        #if DEBUG
        // UI tests: `-skipPlayerCalibration` seeds scanned players (`-fixturePlayers N`, default 1)
        // and plays with the touch pad unless `-startInCameraMode` is also passed.
        let arguments = ProcessInfo.processInfo.arguments
        if arguments.contains("-skipPlayerCalibration") {
            let count = arguments.firstIndex(of: "-fixturePlayers")
                .flatMap { arguments.indices.contains($0 + 1) ? Int(arguments[$0 + 1]) : nil } ?? 1
            let players = (0..<max(1, min(count, GameFlow.maxPlayers))).map {
                Player(name: "Player \($0 + 1)", colorIndex: $0, calibration: .uiTestingFixture)
            }
            UserDefaults.standard.set(
                (arguments.contains("-startInCameraMode") ? SwingInput.camera : .touch).rawValue,
                forKey: "range.swingInput"
            )
            let flow = GameFlow(fixturePlayers: players)
            // `-startCourse easy|medium|hard` skips the menus and tees off straight away.
            if let index = arguments.firstIndex(of: "-startCourse"), arguments.indices.contains(index + 1),
               let course = Course.all.first(where: { $0.difficulty.rawValue == arguments[index + 1] }) {
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
            case .menu:
                MainMenuView(flow: flow, camera: camera)
            case .players:
                PlayerSetupView(flow: flow, camera: camera)
            case .scan(let id):
                CalibrationView(
                    tracker: camera.tracker,
                    playerName: flow.player(id)?.name,
                    onComplete: { flow.finishScan(id, calibration: $0) },
                    onCancel: { flow.cancelScan() }
                )
            case .courses:
                CourseSelectView(flow: flow, camera: camera)
            case .playing:
                CourseScreen(flow: flow, camera: camera)
            case .practice:
                PracticeRangeScreen(camera: camera, onExit: { flow.quitToMenu() })
            }
        }
        .foregroundStyle(Palette.cream)
        .animation(.easeInOut(duration: 0.25), value: flow.screen)
        .onAppear(perform: updateMenuCamera)
        .onChange(of: flow.screen) { _, _ in updateMenuCamera() }
        .onChange(of: gesturesEnabled) { _, _ in updateMenuCamera() }
        .onChange(of: swingInput) { _, _ in updateMenuCamera() }
        .onChange(of: appPhase) { _, _ in updateMenuCamera() }
        .preferredColorScheme(.dark)
    }

    /// Menus listen for gestures from whoever is in front of the camera. Only runs once camera
    /// access has been granted, so the menu never triggers the permission prompt.
    private func updateMenuCamera() {
        switch flow.screen {
        case .playing, .practice:
            return // the course screen manages the camera
        case .scan:
            // The scan drives the shared tracker itself.
            camera.gesturesEnabled = false
        case .menu, .players, .courses:
            let authorized = AVCaptureDevice.authorizationStatus(for: .video) == .authorized
            let wanted = appPhase == .active && gesturesEnabled && swingInput == .camera && authorized
            camera.gesturesEnabled = wanted
            if wanted {
                camera.tracker.setCalibration(nil)
                camera.start()
            } else {
                camera.stop()
            }
        }
    }
}

@MainActor
private final class CameraControllerOwner: ObservableObject {
    let controller = CameraSwingController()
}

#Preview {
    ContentView()
}
