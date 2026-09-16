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
    @Published private(set) var latestCaptureTimestamp: TimeInterval = 0
    @Published private(set) var detectedBodyCount = 0
    @Published private(set) var playerMatchConfidence: Double?
    /// Rotation physically applied to both the delivered buffer and its preview.
    @Published private(set) var videoRotationAngle: CGFloat = 0
    @Published private(set) var isVideoMirrored = true
    /// Horizontal field of view of the active format, in degrees, for on-screen guidance.
    @Published private(set) var fieldOfView: Float = 0
    /// Width ÷ height of the delivered (portrait) frames, so previews can show the whole frame.
    @Published private(set) var frameAspect: CGFloat = 3.0 / 4.0

    private let captureQueue = DispatchQueue(label: "golf.camera.capture", qos: .userInitiated)
    private let visionQueue = DispatchQueue(label: "golf.camera.vision", qos: .userInitiated)
    private let request = VNDetectHumanBodyPoseRequest()
    private let calibrationLock = NSLock()
    private var calibration: PlayerCalibration?
    private var isConfigured = false
    private var rotationCoordinator: AVCaptureDevice.RotationCoordinator?
    private var rotationObservation: NSKeyValueObservation?

    func setCalibration(_ calibration: PlayerCalibration?) {
        calibrationLock.lock()
        self.calibration = calibration
        calibrationLock.unlock()
        DispatchQueue.main.async { [weak self] in self?.playerMatchConfidence = nil }
    }

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
        configureRotation(for: camera, output: output)
        isConfigured = true
        return true
    }

    /// AVCaptureDevice cameras do not all share the same native sensor orientation. The rotation
    /// coordinator is the camera-specific source of truth and also updates when the phone turns.
    /// Rotation and mirroring are physically applied here, so Vision receives an upright `.up`
    /// image and the preview can use the exact same transform.
    private func configureRotation(for camera: AVCaptureDevice, output: AVCaptureVideoDataOutput) {
        let coordinator = AVCaptureDevice.RotationCoordinator(device: camera, previewLayer: nil)
        rotationCoordinator = coordinator
        applyRotation(coordinator.videoRotationAngleForHorizonLevelCapture, to: output)
        rotationObservation = coordinator.observe(\.videoRotationAngleForHorizonLevelCapture, options: [.new]) { [weak self, weak output] coordinator, _ in
            guard let self, let output else { return }
            let angle = coordinator.videoRotationAngleForHorizonLevelCapture
            self.captureQueue.async { [weak self, weak output] in
                guard let self, let output else { return }
                self.applyRotation(angle, to: output)
            }
        }
    }

    private func applyRotation(_ angle: CGFloat, to output: AVCaptureVideoDataOutput) {
        guard let connection = output.connection(with: .video) else { return }
        if connection.isVideoRotationAngleSupported(angle) {
            connection.videoRotationAngle = angle
        }
        if connection.isVideoMirroringSupported {
            connection.automaticallyAdjustsVideoMirroring = false
            connection.isVideoMirrored = true
        }
        let appliedAngle = connection.videoRotationAngle
        let appliedMirror = connection.isVideoMirrored
        DispatchQueue.main.async { [weak self] in
            self?.videoRotationAngle = appliedAngle
            self?.isVideoMirrored = appliedMirror
        }
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
        let fov = widest.videoFieldOfView / Float(camera.videoZoomFactor)
        DispatchQueue.main.async { [weak self] in
            self?.fieldOfView = fov
        }
    }

    func captureOutput(_ output: AVCaptureOutput, didOutput sampleBuffer: CMSampleBuffer, from connection: AVCaptureConnection) {
        guard let pixelBuffer = CMSampleBufferGetImageBuffer(sampleBuffer) else { return }
        let timestamp = CMSampleBufferGetPresentationTimeStamp(sampleBuffer).seconds
        do {
            // The output connection has already made the pixels upright and mirrored.
            let handler = VNImageRequestHandler(cvPixelBuffer: pixelBuffer, orientation: .up)
            try handler.perform([request])
            let observations = request.results ?? []
            let frames = observations.compactMap { try? poseFrame(from: $0, timestamp: timestamp) }
            let selection = selectPlayer(from: frames)
            let width = CGFloat(CVPixelBufferGetWidth(pixelBuffer))
            let height = CGFloat(CVPixelBufferGetHeight(pixelBuffer))
            DispatchQueue.main.async { [weak self] in
                self?.latestCaptureTimestamp = timestamp
                self?.detectedBodyCount = frames.count
                self?.latestFrame = selection?.frame
                self?.playerMatchConfidence = selection?.confidence
                if height > 0, let self {
                    let aspect = width / height
                    if abs(self.frameAspect - aspect) > 0.001 { self.frameAspect = aspect }
                }
            }
        } catch {
            DispatchQueue.main.async { [weak self] in
                self?.latestCaptureTimestamp = timestamp
                self?.detectedBodyCount = 0
                self?.latestFrame = nil
                self?.playerMatchConfidence = nil
            }
            #if DEBUG
            print("Vision body-pose request failed: \(error.localizedDescription)")
            #endif
        }
    }

    private func selectPlayer(from frames: [PoseFrame]) -> (frame: PoseFrame, confidence: Double?)? {
        guard !frames.isEmpty else { return nil }
        calibrationLock.lock()
        let savedCalibration = calibration
        calibrationLock.unlock()

        if let savedCalibration {
            let ranked = frames.compactMap { frame -> (PoseFrame, Double)? in
                guard let score = savedCalibration.matchScore(for: frame) else { return nil }
                return (frame, score)
            }.min { $0.1 < $1.1 }
            guard let ranked, ranked.1 <= 0.24 else { return nil }
            return (ranked.0, max(0, 1 - ranked.1 / 0.24))
        }

        // Before calibration, favor the largest complete body nearest the middle of the frame.
        guard let best = frames.max(by: { acquisitionScore($0) < acquisitionScore($1) }) else { return nil }
        return (best, nil)
    }

    private func acquisitionScore(_ frame: PoseFrame) -> Double {
        guard let bounds = frame.bodyBounds else { return 0 }
        let completeness = Double(frame.points.values.filter { $0.confidence >= 0.45 }.count) / Double(BodyJoint.allCases.count)
        let centerPenalty = hypot(Double(bounds.midX - 0.5), Double(bounds.midY - 0.5))
        return completeness + Double(bounds.height) * 1.4 - centerPenalty * 0.45
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
