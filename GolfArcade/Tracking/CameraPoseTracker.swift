@preconcurrency import AVFoundation
import Combine
import ImageIO
@preconcurrency import Vision

/// Front-camera body tracking tuned for one golfer.
///
/// Every frame goes through Vision's body-pose detector, but with three things layered on top so
/// the read is about the player and nothing else:
/// - **Person lock.** Of everything Vision finds, the largest, most confident body is the player;
///   others are ignored.
/// - **Region of interest.** Once locked, Vision only looks inside a generous box around the
///   player (extra headroom for the hands at the top of the swing), which is both faster and more
///   precise than scanning the whole frame. The box follows the player and resets if they are lost.
/// - **Temporal filtering.** Each joint runs through a One-Euro filter — steady when still,
///   responsive when moving — and a wrist that blinks out for a frame or two is held from its last
///   good position so a swing is never dropped over a single bad frame.
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
    /// The box Vision is currently scanning, normalised to the frame (nil = whole frame).
    @Published private(set) var focusRegion: CGRect?

    private let captureQueue = DispatchQueue(label: "golf.camera.capture", qos: .userInitiated)
    private let visionQueue = DispatchQueue(label: "golf.camera.vision", qos: .userInteractive)
    private let request = VNDetectHumanBodyPoseRequest()
    private var isConfigured = false

    // Vision-queue state.
    private var filters: [BodyJoint: (x: OneEuroFilter, y: OneEuroFilter)] = [:]
    private var held: [BodyJoint: (point: PosePoint, until: TimeInterval)] = [:]
    private var region: CGRect?
    private var lastSeen: TimeInterval?
    /// Whether Vision reports points relative to the region of interest (Apple's documented
    /// behaviour) or to the whole frame. Decided from evidence on the first cropped frame, so a
    /// change in Vision's behaviour can never scatter the skeleton.
    private var pointsAreRelativeToRegion: Bool?
    private var lastShoulders: CGPoint?

    /// Joints below this confidence are treated as missing.
    static let minimumConfidence: Float = 0.2
    /// How long a briefly lost joint keeps its last good position.
    static let holdDuration: TimeInterval = 0.12
    /// Without a body for this long the region of interest resets to the whole frame.
    static let regionTimeout: TimeInterval = 0.5

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
            self?.region = nil
            self?.lastSeen = nil
            self?.request.regionOfInterest = CGRect(x: 0, y: 0, width: 1, height: 1)
            DispatchQueue.main.async { self?.focusRegion = nil }
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
    /// full swing in frame from much closer; at equal width the faster frame rate wins.
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
        let roi = region ?? CGRect(x: 0, y: 0, width: 1, height: 1)
        request.regionOfInterest = roi
        do {
            let handler = VNImageRequestHandler(cvPixelBuffer: pixelBuffer, orientation: .upMirrored)
            try handler.perform([request])
            guard let observation = Self.player(among: request.results ?? [], in: roi) else {
                releaseLock(at: timestamp)
                return
            }
            let frame = try poseFrame(from: observation, roi: roi, timestamp: timestamp)
            lastSeen = timestamp
            if let left = frame.point(.leftShoulder), let right = frame.point(.rightShoulder) {
                lastShoulders = CGPoint(x: (left.x + right.x) / 2, y: (left.y + right.y) / 2)
            }
            updateRegion(for: frame)
            DispatchQueue.main.async { [weak self] in self?.latestFrame = frame }
        } catch {
            releaseLock(at: timestamp)
        }
    }

    /// The largest, most confident body is the player; bystanders and reflections are ignored.
    private static func player(among observations: [VNHumanBodyPoseObservation], in roi: CGRect) -> VNHumanBodyPoseObservation? {
        observations.max { size(of: $0, in: roi) < size(of: $1, in: roi) }
    }

    private static func size(of observation: VNHumanBodyPoseObservation, in roi: CGRect) -> CGFloat {
        guard let points = try? observation.recognizedPoints(.all) else { return 0 }
        let good = points.values.filter { $0.confidence >= minimumConfidence }
        guard good.count >= 4 else { return 0 }
        let xs = good.map(\.location.x), ys = good.map(\.location.y)
        let box = CGRect(x: xs.min()!, y: ys.min()!, width: xs.max()! - xs.min()!, height: ys.max()! - ys.min()!)
        let confidence = CGFloat(good.map(\.confidence).reduce(0, +)) / CGFloat(good.count)
        return box.width * roi.width * box.height * roi.height * (0.5 + confidence)
    }

    private func poseFrame(from observation: VNHumanBodyPoseObservation, roi: CGRect, timestamp: TimeInterval) throws -> PoseFrame {
        let mapping: [(BodyJoint, VNHumanBodyPoseObservation.JointName)] = [
            (.nose, .nose), (.neck, .neck), (.leftShoulder, .leftShoulder), (.rightShoulder, .rightShoulder),
            (.leftElbow, .leftElbow), (.rightElbow, .rightElbow), (.leftWrist, .leftWrist), (.rightWrist, .rightWrist),
            (.root, .root), (.leftHip, .leftHip), (.rightHip, .rightHip), (.leftKnee, .leftKnee),
            (.rightKnee, .rightKnee), (.leftAnkle, .leftAnkle), (.rightAnkle, .rightAnkle)
        ]
        var points: [BodyJoint: PosePoint] = [:]
        let relative = resolveCoordinateSpace(of: observation, roi: roi)
        for (joint, visionName) in mapping {
            let point = try observation.recognizedPoint(visionName)
            if point.confidence >= Self.minimumConfidence {
                let full = relative ? Self.framePoint(point.location, in: roi) : point.location
                let smoothed = smooth(joint, full, at: timestamp)
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

    /// On the first frame scanned with a crop, the shoulders cannot have jumped: whichever reading
    /// of Vision's coordinates lands them nearest their last full-frame position is the right one.
    private func resolveCoordinateSpace(of observation: VNHumanBodyPoseObservation, roi: CGRect) -> Bool {
        let isCropped = roi != CGRect(x: 0, y: 0, width: 1, height: 1)
        guard isCropped else { return true }
        if let decided = pointsAreRelativeToRegion { return decided }
        guard let previous = lastShoulders,
              let left = try? observation.recognizedPoint(.leftShoulder), left.confidence >= Self.minimumConfidence,
              let right = try? observation.recognizedPoint(.rightShoulder), right.confidence >= Self.minimumConfidence else { return true }
        let raw = CGPoint(x: (left.location.x + right.location.x) / 2, y: (left.location.y + right.location.y) / 2)
        let mapped = Self.framePoint(raw, in: roi)
        let relative = hypot(mapped.x - previous.x, mapped.y - previous.y) <= hypot(raw.x - previous.x, raw.y - previous.y)
        pointsAreRelativeToRegion = relative
        return relative
    }

    private func smooth(_ joint: BodyJoint, _ point: CGPoint, at time: TimeInterval) -> CGPoint {
        var pair = filters[joint] ?? (OneEuroFilter(), OneEuroFilter())
        let x = pair.x.filter(Double(point.x), at: time)
        let y = pair.y.filter(Double(point.y), at: time)
        filters[joint] = pair
        return CGPoint(x: x, y: y)
    }

    private func updateRegion(for frame: PoseFrame) {
        guard let box = Self.focusBox(around: frame) else { return }
        region = box
        DispatchQueue.main.async { [weak self] in self?.focusRegion = box }
    }

    /// Vision reports points relative to the region of interest; map back to the whole frame.
    static func framePoint(_ point: CGPoint, in roi: CGRect) -> CGPoint {
        CGPoint(x: roi.minX + point.x * roi.width, y: roi.minY + point.y * roi.height)
    }

    /// A generous box around the player: wide margins, and extra headroom because the hands rise
    /// well above the head at the top of the backswing. Never tighter than 40 % × 50 % of the frame.
    static func focusBox(around frame: PoseFrame) -> CGRect? {
        let good = frame.points.values.filter { $0.confidence >= 0.3 }.map(\.location)
        guard good.count >= 4 else { return nil }
        let frameRect = CGRect(x: 0, y: 0, width: 1, height: 1)
        let minX = good.map(\.x).min()!, maxX = good.map(\.x).max()!
        let minY = good.map(\.y).min()!, maxY = good.map(\.y).max()!
        let width = max(maxX - minX, 0.2), height = max(maxY - minY, 0.3)
        var box = CGRect(x: minX - width * 0.45, y: minY - height * 0.2, width: width * 1.9, height: height * 1.85)
        if box.width < 0.4 { box = box.insetBy(dx: -(0.4 - box.width) / 2, dy: 0) }
        if box.height < 0.5 { box = box.insetBy(dx: 0, dy: -(0.5 - box.height) / 2) }
        return box.intersection(frameRect)
    }

    private func releaseLock(at timestamp: TimeInterval) {
        if let lastSeen, timestamp - lastSeen > Self.regionTimeout, region != nil {
            region = nil
            filters.removeAll()
            held.removeAll()
            DispatchQueue.main.async { [weak self] in self?.focusRegion = nil }
        }
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
