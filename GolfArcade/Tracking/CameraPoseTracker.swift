@preconcurrency import AVFoundation
import Combine
import ImageIO
import QuartzCore
@preconcurrency import Vision

/// Front-camera body tracking tuned for one golfer.
///
/// Every frame goes through Vision's body-pose detector on the whole image (no cropping, so the
/// skeleton always lines up with the video), with two things layered on top so the read is about
/// the player and nothing else:
/// - **Person lock.** Of everything Vision finds, the largest, most confident body is the player;
///   others are ignored.
/// - **Temporal filtering.** Each joint runs through a One-Euro filter — steady when still,
///   responsive when moving — and a wrist that blinks out for a frame or two is held from its last
///   good position so a swing is never dropped over a single bad frame.
/// - **Skeleton constraints.** Bone lengths learned at address reject joints that stretch or
///   wrists that fly apart (see `BodyModel`).
///
/// It also reports what the readiness checklist needs: how many people are in view and how far
/// the exposure is from target. `benchmark3D` times Apple's 3D pose request on the same frames so
/// the switch to metric joints can be decided from a real device.
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
    /// Bodies Vision found in the last frame.
    @Published private(set) var peopleInView = 0
    /// Exposure offset from the camera's target, in EV.
    @Published private(set) var exposureOffsetEV: Double = 0
    /// Frames delivered per second, measured.
    @Published private(set) var measuredFrameRate: Double = 0
    /// Average milliseconds per frame for the 3D pose request while `benchmark3D` is on.
    @Published private(set) var pose3DMilliseconds: Double = 0
    /// Bone-length constraints on or off (on by default).
    var enforceSkeleton = true
    /// Also run `VNDetectHumanBodyPose3DRequest` on every frame and time it.
    var benchmark3D = false {
        didSet { if !benchmark3D { pose3DSamples = [] } }
    }

    private let captureQueue = DispatchQueue(label: "golf.camera.capture", qos: .userInitiated)
    private let visionQueue = DispatchQueue(label: "golf.camera.vision", qos: .userInteractive)
    private let request = VNDetectHumanBodyPoseRequest()
    private let request3D = VNDetectHumanBodyPose3DRequest()
    private var camera: AVCaptureDevice?
    private var isConfigured = false

    // Vision-queue state.
    private var filters: [BodyJoint: (x: OneEuroFilter, y: OneEuroFilter)] = [:]
    private var held: [BodyJoint: (point: PosePoint, until: TimeInterval)] = [:]
    private var body = BodyModel()
    private var previousHands: (point: CGPoint, time: TimeInterval)?
    private var frameTimes: [TimeInterval] = []
    private var pose3DSamples: [Double] = []

    /// Joints below this confidence are treated as missing.
    static let minimumConfidence: Float = 0.2
    /// How long a briefly lost joint keeps its last good position.
    static let holdDuration: TimeInterval = 0.12

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
        visionQueue.async { [weak self] in
            self?.filters.removeAll()
            self?.held.removeAll()
            self?.body.reset()
            self?.previousHands = nil
            self?.frameTimes.removeAll()
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
        self.camera = camera
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
    /// full swing in frame from much closer; at equal width, 60 fps beats 30 so a downswing is
    /// ~14 frames instead of ~7. Late frames are dropped rather than queued if Vision falls behind.
    private func selectWidestFormat(on camera: AVCaptureDevice) {
        let candidates = camera.formats.filter { format in
            let size = CMVideoFormatDescriptionGetDimensions(format.formatDescription)
            let fps = format.videoSupportedFrameRateRanges.contains { $0.maxFrameRate >= 30 }
            return fps && size.height >= 720 && size.height <= 1600 // enough for Vision, cheap enough for 60 fps
        }
        func maxFrameRate(_ format: AVCaptureDevice.Format) -> Double {
            min(60, format.videoSupportedFrameRateRanges.map(\.maxFrameRate).max() ?? 30)
        }
        guard let widest = candidates.max(by: { a, b in
            if abs(a.videoFieldOfView - b.videoFieldOfView) > 0.5 { return a.videoFieldOfView < b.videoFieldOfView }
            if maxFrameRate(a) != maxFrameRate(b) { return maxFrameRate(a) < maxFrameRate(b) }
            let sizeA = CMVideoFormatDescriptionGetDimensions(a.formatDescription)
            let sizeB = CMVideoFormatDescriptionGetDimensions(b.formatDescription)
            return sizeA.height > sizeB.height // prefer the cheaper frame otherwise
        }) else { return }
        let frameRate = Int32(maxFrameRate(widest))
        do {
            try camera.lockForConfiguration()
            camera.activeFormat = widest
            camera.activeVideoMinFrameDuration = CMTime(value: 1, timescale: frameRate)
            camera.activeVideoMaxFrameDuration = CMTime(value: 1, timescale: frameRate)
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
        let timestamp = CMSampleBufferGetPresentationTimeStamp(sampleBuffer).seconds
        // Frames arrive rotated to portrait and, when supported, already mirrored to match the
        // selfie preview. Tell Vision exactly what it is looking at so the joints it returns land
        // on the video: a mirrored buffer is `.up` as-is; an unmirrored one needs `.upMirrored`
        // so the results match the preview layer, which mirrors the front camera on its own.
        let orientation: CGImagePropertyOrientation = connection.isVideoMirrored ? .up : .upMirrored
        recordFrameTime(timestamp)
        do {
            let handler = VNImageRequestHandler(cvPixelBuffer: pixelBuffer, orientation: orientation)
            try handler.perform([request])
            let observations = request.results ?? []
            let exposure = Double(camera?.exposureTargetOffset ?? 0)
            let people = observations.count
            DispatchQueue.main.async { [weak self] in
                self?.peopleInView = people
                self?.exposureOffsetEV = exposure
            }
            if benchmark3D { time3DRequest(handler) }
            guard let observation = Self.player(among: observations) else { return }
            var frame = try poseFrame(from: observation, timestamp: timestamp)
            if enforceSkeleton { frame = body.apply(to: frame, isStill: handsAreStill(in: frame)) }
            DispatchQueue.main.async { [weak self] in self?.latestFrame = frame }
        } catch {
            // Unrecognized frames are expected and safely ignored.
        }
    }

    private func recordFrameTime(_ timestamp: TimeInterval) {
        frameTimes.append(timestamp)
        frameTimes.removeAll { timestamp - $0 > 1 }
        let rate = Double(frameTimes.count)
        DispatchQueue.main.async { [weak self] in self?.measuredFrameRate = rate }
    }

    private func time3DRequest(_ handler: VNImageRequestHandler) {
        let start = CACurrentMediaTime()
        try? handler.perform([request3D])
        pose3DSamples.append((CACurrentMediaTime() - start) * 1000)
        if pose3DSamples.count > 30 { pose3DSamples.removeFirst() }
        let average = pose3DSamples.reduce(0, +) / Double(pose3DSamples.count)
        DispatchQueue.main.async { [weak self] in self?.pose3DMilliseconds = average }
    }

    /// Hands moving slower than ~5 % of the frame per second count as still for calibration.
    private func handsAreStill(in frame: PoseFrame) -> Bool {
        guard let hands = frame.handCenter else { previousHands = nil; return false }
        defer { previousHands = (hands, frame.timestamp) }
        guard let previous = previousHands, frame.timestamp > previous.time else { return false }
        let speed = hypot(hands.x - previous.point.x, hands.y - previous.point.y) / (frame.timestamp - previous.time)
        return speed < 0.05
    }

    /// The largest, most confident body is the player; bystanders and reflections are ignored.
    private static func player(among observations: [VNHumanBodyPoseObservation]) -> VNHumanBodyPoseObservation? {
        observations.max { size(of: $0) < size(of: $1) }
    }

    private static func size(of observation: VNHumanBodyPoseObservation) -> CGFloat {
        guard let points = try? observation.recognizedPoints(.all) else { return 0 }
        let good = points.values.filter { $0.confidence >= minimumConfidence }
        guard good.count >= 4 else { return 0 }
        let xs = good.map(\.location.x), ys = good.map(\.location.y)
        let box = CGRect(x: xs.min()!, y: ys.min()!, width: xs.max()! - xs.min()!, height: ys.max()! - ys.min()!)
        let confidence = CGFloat(good.map(\.confidence).reduce(0, +)) / CGFloat(good.count)
        return box.width * box.height * (0.5 + confidence)
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
            if point.confidence >= Self.minimumConfidence {
                let smoothed = smooth(joint, point.location, at: timestamp)
                let smoothedPoint = PosePoint(location: smoothed, confidence: point.confidence)
                points[joint] = smoothedPoint
                held[joint] = (smoothedPoint, timestamp + Self.holdDuration)
            } else if let hold = held[joint], timestamp <= hold.until {
                // Brief dropout: keep the last good position, flagged as lower confidence.
                points[joint] = PosePoint(location: hold.point.location, confidence: max(Self.minimumConfidence, hold.point.confidence * 0.6))
            } else {
                filters[joint] = nil
            }
        }
        return PoseFrame(timestamp: timestamp, points: points)
    }

    private func smooth(_ joint: BodyJoint, _ point: CGPoint, at time: TimeInterval) -> CGPoint {
        var pair = filters[joint] ?? (OneEuroFilter(), OneEuroFilter())
        let x = pair.x.filter(Double(point.x), at: time)
        let y = pair.y.filter(Double(point.y), at: time)
        filters[joint] = pair
        return CGPoint(x: x, y: y)
    }

    private func setStatus(_ newStatus: Status) {
        DispatchQueue.main.async { [weak self] in self?.status = newStatus }
    }
}

/// One-Euro filter (Casiez et al.): a low-pass filter whose cutoff rises with speed, so tracked
/// joints hold still at rest and still follow a fast swing without lag.
struct OneEuroFilter {
    var minCutoff = 1.0
    var beta = 1.5
    var derivativeCutoff = 1.0
    private var previous: Double?
    private var previousDerivative = 0.0
    private var previousTime: Double?

    mutating func filter(_ value: Double, at time: Double) -> Double {
        guard let last = previous, let lastTime = previousTime, time > lastTime else {
            previous = value
            previousTime = time
            return value
        }
        let dt = time - lastTime
        let derivative = (value - last) / dt
        let dAlpha = Self.alpha(cutoff: derivativeCutoff, dt: dt)
        let smoothedDerivative = dAlpha * derivative + (1 - dAlpha) * previousDerivative
        let cutoff = minCutoff + beta * abs(smoothedDerivative)
        let alpha = Self.alpha(cutoff: cutoff, dt: dt)
        let smoothed = alpha * value + (1 - alpha) * last
        previous = smoothed
        previousDerivative = smoothedDerivative
        previousTime = time
        return smoothed
    }

    private static func alpha(cutoff: Double, dt: Double) -> Double {
        let tau = 1 / (2 * .pi * cutoff)
        return 1 / (1 + tau / dt)
    }
}
