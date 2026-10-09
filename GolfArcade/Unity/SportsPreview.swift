import ARKit
import SwiftUI

/// Rear-camera viewfinder for the aim-at-the-TV step.
///
/// It draws frames from the steering `ARSession` rather than opening a second capture
/// session — two sessions cannot own the camera at once, and the aiming step has to show
/// exactly what world tracking is seeing. Without it there is no way to tell a working
/// camera from a dead one, or to know what you are actually pointing at.
final class SportsPreviewView: UIView {
    private let context = CIContext(options: [.useSoftwareRenderer: false])
    private var lastOrientation: CGImagePropertyOrientation = .right

    override init(frame: CGRect) {
        super.init(frame: frame)
        backgroundColor = .black
        layer.contentsGravity = .resizeAspectFill
        layer.masksToBounds = true
    }
    required init?(coder: NSCoder) { fatalError("init(coder:) is unused") }

    /// ARKit hands over the sensor's native landscape buffer; rotate it to match how the
    /// phone is actually being held so aiming is not guesswork.
    func show(_ pixelBuffer: CVPixelBuffer) {
        let orientation = Self.imageOrientation(for: window?.windowScene?.interfaceOrientation ?? .portrait)
        lastOrientation = orientation
        let image = CIImage(cvPixelBuffer: pixelBuffer).oriented(orientation)
        guard let rendered = context.createCGImage(image, from: image.extent) else { return }
        layer.contents = rendered
    }

    static func imageOrientation(for interface: UIInterfaceOrientation) -> CGImagePropertyOrientation {
        switch interface {
        case .landscapeLeft: return .down
        case .landscapeRight: return .up
        case .portraitUpsideDown: return .left
        default: return .right
        }
    }
}

struct SportsCameraPreview: UIViewRepresentable {
    let motion: SportsMotion
    func makeUIView(context: Context) -> SportsPreviewView {
        let view = SportsPreviewView(frame: .zero)
        view.isUserInteractionEnabled = false
        motion.preview = view
        return view
    }
    func updateUIView(_ view: SportsPreviewView, context: Context) { motion.preview = view }
}
