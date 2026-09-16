import SwiftUI

struct ContentView: View {
    @StateObject private var camera = CameraSwingController()
    @State private var calibration: PlayerCalibration?
    @State private var startCameraGameplay = false

    init() {
        #if DEBUG
        let arguments = ProcessInfo.processInfo.arguments
        let initial = arguments.contains("-skipPlayerCalibration")
            ? PlayerCalibration.uiTestingFixture
            : PlayerCalibrationStore.load()
        let startsInCameraMode = arguments.contains("-startInCameraMode")
        #else
        let initial = PlayerCalibrationStore.load()
        let startsInCameraMode = false
        #endif
        _calibration = State(initialValue: initial)
        _startCameraGameplay = State(initialValue: startsInCameraMode)
    }

    var body: some View {
        Group {
            if let calibration {
                RangeMockView(
                    camera: camera,
                    calibration: calibration,
                    startsInCameraMode: startCameraGameplay
                ) {
                    camera.stop()
                    PlayerCalibrationStore.clear()
                    camera.tracker.setCalibration(nil)
                    startCameraGameplay = false
                    withAnimation(.easeInOut) { self.calibration = nil }
                }
                .transition(.opacity)
                .onAppear { camera.tracker.setCalibration(calibration) }
            } else {
                CalibrationView(tracker: camera.tracker) { completed in
                    startCameraGameplay = true
                    withAnimation(.easeInOut) { calibration = completed }
                }
                .transition(.opacity)
            }
        }
        .preferredColorScheme(.dark)
    }
}

#Preview {
    ContentView()
}
