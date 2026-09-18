import AVFoundation
import SwiftUI

struct CameraPreview: UIViewRepresentable {
    let session: AVCaptureSession
    var videoRotationAngle: CGFloat = 0
    var isVideoMirrored = true
    var videoGravity: AVLayerVideoGravity = .resizeAspectFill

    func makeUIView(context: Context) -> PreviewView {
        let view = PreviewView()
        view.previewLayer.session = session
        updateConnection(on: view)
        return view
    }

    func updateUIView(_ uiView: PreviewView, context: Context) {
        // Pose updates arrive many times a second; do not reconnect an unchanged live session.
        if uiView.previewLayer.session !== session { uiView.previewLayer.session = session }
        updateConnection(on: uiView)
    }

    static func dismantleUIView(_ uiView: PreviewView, coordinator: ()) {
        uiView.previewLayer.session = nil
    }

    private func updateConnection(on view: PreviewView) {
        if view.previewLayer.videoGravity != videoGravity { view.previewLayer.videoGravity = videoGravity }
        guard let connection = view.previewLayer.connection else { return }
        if connection.videoRotationAngle != videoRotationAngle, connection.isVideoRotationAngleSupported(videoRotationAngle) {
            connection.videoRotationAngle = videoRotationAngle
        }
        if connection.isVideoMirroringSupported {
            if connection.automaticallyAdjustsVideoMirroring { connection.automaticallyAdjustsVideoMirroring = false }
            if connection.isVideoMirrored != isVideoMirrored { connection.isVideoMirrored = isVideoMirrored }
        }
    }
}

final class PreviewView: UIView {
    override class var layerClass: AnyClass { AVCaptureVideoPreviewLayer.self }
    var previewLayer: AVCaptureVideoPreviewLayer { layer as! AVCaptureVideoPreviewLayer }
    private let instanceID = UUID().uuidString
    private var renderingObservation: NSKeyValueObservation?

    override init(frame: CGRect) {
        super.init(frame: frame)
        isAccessibilityElement = true
        accessibilityLabel = "Live camera preview"
        accessibilityIdentifier = "liveCameraPreview"
        renderingObservation = previewLayer.observe(\.isPreviewing, options: [.initial, .new]) { [weak self] _, _ in
            DispatchQueue.main.async { self?.updateRenderingStatus() }
        }
    }

    required init?(coder: NSCoder) { fatalError("init(coder:) has not been implemented") }

    override func layoutSubviews() {
        super.layoutSubviews()
        updateRenderingStatus()
    }

    private func updateRenderingStatus() {
        // Tests must verify actual AVFoundation rendering, not just a black SwiftUI rectangle.
        accessibilityValue = "\(instanceID);rendering=\(previewLayer.isPreviewing ? 1 : 0)"
    }
}
