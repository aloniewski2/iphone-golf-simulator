@preconcurrency import AVFoundation
import Combine
import ImageIO
@preconcurrency import Vision

final class CameraPoseTracker: NSObject, ObservableObject, AVCaptureVideoDataOutputSampleBufferDelegate, @unchecked Sendable {
    enum Status: Equatable {
        case idle, requestingPermission, running, denied
        case unavailable(String)
    }

    let session = AVCaptureSession()
    @Published private(set) var status: Status = .idle
    @Published private(set) var latestFrame: PoseFrame?
    /// Horizontal field of view of the active format, in degrees, for on-screen guidance.
    @Published private(set) var fieldOfView: Float = 0
    /// Width ÷ height of the delivered (portrait) frames, so previews can show the whole frame.
    @Published private(set) var frameAspect: CGFloat = 3.0 / 4.0

    private let captureQueue = DispatchQueue(label: "golf.camera.capture", qos: .userInitiated)
    private let visionQueue = DispatchQueue(label: "golf.camera.vision", qos: .userInitiated)
    private let request = VNDetectHumanBodyPoseRequest()
    private var isConfigured = false

    func start() {
        switch AVCaptureDevice.authorizationStatus(for: .video) {
        case .authorized: configureAndStart()
        case .notDetermined:
            status = .requestingPermission
            AVCaptureDevice.requestAccess(for: .video) { [weak self] granted in
                guard let self else { return }
                granted ? self.configureAndStart() : self.setStatus(.denied)
            }
        case .denied, .restricted: status = .denied
        @unknown default: status = .unavailable("Camera authorization is unavailable.")
        }
    }

    func stop() {
        captureQueue.async { [session] in
            if session.isRunning { session.stopRunning() }
        }
    }

    private func configureAndStart() {
        captureQueue.async { [weak self] in
            guard let self else { return }
            if !self.isConfigured, !self.configureSession() { return }
            if !self.session.isRunning { self.session.startRunning() }
            self.setStatus(.running)
        }
    }

    private func configureSession() -> Bool {
        session.beginConfiguration()
        defer { session.commitConfiguration() }
        session.sessionPreset = .inputPriority
        guard let camera = AVCaptureDevice.default(.builtInWideAngleCamera, for: .video, position: .front),
              let input = try? AVCaptureDeviceInput(device: camera), session.canAddInput(input) else {
            setStatus(.unavailable("The front camera is unavailable."))
            return false
        }
        selectWidestFormat(on: camera)
        let output = AVCaptureVideoDataOutput()
        output.alwaysDiscardsLateVideoFrames = true
        output.videoSettings = [kCVPixelBufferPixelFormatTypeKey as String: kCVPixelFormatType_32BGRA]
        output.setSampleBufferDelegate(self, queue: visionQueue)
        guard session.canAddOutput(output) else {
            setStatus(.unavailable("Camera frames could not be read."))
            return false
        }
        session.addInput(input)
        session.addOutput(output)
        if let connection = output.connection(with: .video) {
            if connection.isVideoRotationAngleSupported(90) { connection.videoRotationAngle = 90 }
            if connection.isVideoMirroringSupported { connection.isVideoMirrored = true }
        }
        isConfigured = true
        return true
    }

    /// The default preset crops the sensor. The widest format plus the minimum zoom factor keeps a
    /// full swing in frame from much closer, which matters when the phone is propped on the floor.
    private func selectWidestFormat(on camera: AVCaptureDevice) {
        let candidates = camera.formats.filter { format in
            let size = CMVideoFormatDescriptionGetDimensions(format.formatDescription)
            let fps = format.videoSupportedFrameRateRanges.contains { $0.maxFrameRate >= 30 }
            return fps && size.height >= 720 && size.height <= 1600 // enough for Vision, cheap enough for 30 fps
        }
        guard let widest = candidates.max(by: { a, b in
            if a.videoFieldOfView != b.videoFieldOfView { return a.videoFieldOfView < b.videoFieldOfView }
            let sizeA = CMVideoFormatDescriptionGetDimensions(a.formatDescription)
            let sizeB = CMVideoFormatDescriptionGetDimensions(b.formatDescription)
            return sizeA.height > sizeB.height // prefer the cheaper frame at equal FOV
        }) else { return }
        do {
            try camera.lockForConfiguration()
            camera.activeFormat = widest
            camera.activeVideoMinFrameDuration = CMTime(value: 1, timescale: 30)
            camera.activeVideoMaxFrameDuration = CMTime(value: 1, timescale: 30)
            camera.videoZoomFactor = camera.minAvailableVideoZoomFactor
            camera.unlockForConfiguration()
        } catch {
            return
        }
        let size = CMVideoFormatDescriptionGetDimensions(widest.formatDescription)
        let fov = widest.videoFieldOfView / Float(camera.videoZoomFactor)
        let aspect = CGFloat(size.height) / CGFloat(size.width) // frames are rotated to portrait
        DispatchQueue.main.async { [weak self] in
            self?.fieldOfView = fov
            self?.frameAspect = aspect
        }
    }

    func captureOutput(_ output: AVCaptureOutput, didOutput sampleBuffer: CMSampleBuffer, from connection: AVCaptureConnection) {
        guard let pixelBuffer = CMSampleBufferGetImageBuffer(sampleBuffer) else { return }
        do {
            let handler = VNImageRequestHandler(cvPixelBuffer: pixelBuffer, orientation: .upMirrored)
            try handler.perform([request])
            guard let observation = request.results?.first else { return }
            let frame = try poseFrame(from: observation, timestamp: CMSampleBufferGetPresentationTimeStamp(sampleBuffer).seconds)
            DispatchQueue.main.async { [weak self] in self?.latestFrame = frame }
        } catch {
            // Unrecognized frames are expected and safely ignored.
        }
    }

    private func poseFrame(from observation: VNHumanBodyPoseObservation, timestamp: TimeInterval) throws -> PoseFrame {
        let mapping: [(BodyJoint, VNHumanBodyPoseObservation.JointName)] = [
            (.nose, .nose), (.neck, .neck), (.leftShoulder, .leftShoulder), (.rightShoulder, .rightShoulder),
            (.leftElbow, .leftElbow), (.rightElbow, .rightElbow), (.leftWrist, .leftWrist), (.rightWrist, .rightWrist),
            (.root, .root), (.leftHip, .leftHip), (.rightHip, .rightHip), (.leftKnee, .leftKnee),
            (.rightKnee, .rightKnee), (.leftAnkle, .leftAnkle), (.rightAnkle, .rightAnkle)
        ]
        var points: [BodyJoint: PosePoint] = [:]
        for (joint, visionName) in mapping {
            let point = try observation.recognizedPoint(visionName)
            points[joint] = PosePoint(location: point.location, confidence: point.confidence)
        }
        return PoseFrame(timestamp: timestamp, points: points)
    }

    private func setStatus(_ newStatus: Status) {
        DispatchQueue.main.async { [weak self] in self?.status = newStatus }
    }
}

