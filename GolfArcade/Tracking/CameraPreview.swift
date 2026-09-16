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
        uiView.previewLayer.session = session
        updateConnection(on: uiView)
    }

    private func updateConnection(on view: PreviewView) {
        view.previewLayer.videoGravity = videoGravity
        guard let connection = view.previewLayer.connection else { return }
        if connection.isVideoRotationAngleSupported(videoRotationAngle) {
            connection.videoRotationAngle = videoRotationAngle
        }
        if connection.isVideoMirroringSupported {
            connection.automaticallyAdjustsVideoMirroring = false
            connection.isVideoMirrored = isVideoMirrored
        }
    }
}

final class PreviewView: UIView {
    override class var layerClass: AnyClass { AVCaptureVideoPreviewLayer.self }
    var previewLayer: AVCaptureVideoPreviewLayer { layer as! AVCaptureVideoPreviewLayer }
}
