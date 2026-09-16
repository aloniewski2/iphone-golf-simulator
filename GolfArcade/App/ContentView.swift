import SwiftUI

struct ContentView: View {
    @StateObject private var camera = CameraSwingController()
    @State private var calibration: PlayerCalibration?

    init() {
        #if DEBUG
        let initial = ProcessInfo.processInfo.arguments.contains("-skipPlayerCalibration")
            ? PlayerCalibration.uiTestingFixture
            : PlayerCalibrationStore.load()
        #else
        let initial = PlayerCalibrationStore.load()
        #endif
        _calibration = State(initialValue: initial)
    }

    var body: some View {
        Group {
            if let calibration {
                RangeMockView(camera: camera, calibration: calibration) {
                    camera.stop()
                    PlayerCalibrationStore.clear()
                    camera.tracker.setCalibration(nil)
                    withAnimation(.easeInOut) { self.calibration = nil }
                }
                .transition(.opacity)
                .onAppear { camera.tracker.setCalibration(calibration) }
            } else {
                CalibrationView(tracker: camera.tracker) { completed in
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
