@preconcurrency import AVFoundation
import Combine
import ImageIO
import simd
@preconcurrency import Vision

final class CameraPoseTracker: NSObject, ObservableObject, AVCaptureVideoDataOutputSampleBufferDelegate, @unchecked Sendable {
    enum Status: Equatable {
        case idle, requestingPermission, running, denied
        case unavailable(String)
    }

    let session = AVCaptureSession()
    @Published private(set) var status: Status = .idle
    @Published private(set) var latestFrame: PoseFrame?
    @Published private(set) var latestDelivery: PoseDelivery?
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
    @Published private(set) var captureFPS = 30
    @Published private(set) var captureInventory: [CaptureDeviceCapability] = []
    @Published private(set) var captureConfiguration: CaptureConfiguration?
    @Published private(set) var configurationError: String?
    @Published private(set) var sourceRecordingActive = false
    @Published private(set) var sourceRecordingError: String?
    @Published private(set) var sourceClips: [SourceVideoClip] = []
    private let sourceRecorder = SourceVideoRecorder() // visionQueue only

    func loadSourceClips() {
        visionQueue.async { [weak self] in
            do {
                let clips = try SourceVideoRecorder.savedClips()
                DispatchQueue.main.async { self?.sourceClips = clips }
            } catch { DispatchQueue.main.async { self?.sourceRecordingError = error.localizedDescription } }
        }
    }

    func beginSourceRecording(trialID: UUID, consented: Bool) {
        guard !sourceRecordingActive else { return }
        sourceRecordingActive = true
        sourceRecordingError = nil
        visionQueue.async { [weak self] in
            guard let self else { return }
            self.sourceRecorder.completed = { [weak self] result in
                DispatchQueue.main.async {
                    self?.sourceRecordingActive = false
                    switch result {
                    case .success: self?.loadSourceClips()
                    case .failure(let error): self?.sourceRecordingError = error.localizedDescription
                    }
                }
            }
            do { try self.sourceRecorder.begin(trialID: trialID, consented: consented) }
            catch {
                DispatchQueue.main.async {
                    self.sourceRecordingActive = false
                    self.sourceRecordingError = error.localizedDescription
                }
            }
        }
    }

    func finishSourceRecording(reason: String) {
        visionQueue.async { [weak self] in self?.sourceRecorder.finish(reason: reason) }
    }

    func deleteSourceClip(_ id: UUID) {
        guard !sourceRecordingActive else { return }
        visionQueue.async { [weak self] in
            do {
                try SourceVideoRecorder.delete(id)
                self?.loadSourceClips()
            } catch { DispatchQueue.main.async { self?.sourceRecordingError = error.localizedDescription } }
        }
    }

    func saveSourceTrial(_ trial: BenchmarkTrial) {
        guard trial.sourceVideoConsented == true else { return }
        // Encode on the owning thread before crossing to the capture queue.
        guard let data = try? JSONEncoder().encode(trial) else { return }
        let id = trial.id
        visionQueue.async { [weak self] in
            do {
                let urls = try SourceVideoRecorder.urls(for: id)
                guard FileManager.default.fileExists(atPath: urls[0].path) else { return }
                try data.write(to: urls[2], options: .atomic)
            }
            catch { DispatchQueue.main.async { self?.sourceRecordingError = error.localizedDescription } }
        }
    }

    private let captureQueue = DispatchQueue(label: "golf.camera.capture", qos: .userInitiated)
    private let visionQueue = DispatchQueue(label: "golf.camera.vision", qos: .userInitiated)
    private let request = VNDetectHumanBodyPoseRequest()
    private let depthRequest = VNDetectHumanBodyPose3DRequest()
    private var poseMode: CameraPoseMode = .body2D
    private var requestedFPS = 60
    private var captureProfile: CameraCaptureProfile = .baseline
    private var appliedProfile: CameraCaptureProfile = .baseline // captureQueue only
    private var evidenceConfiguration: CaptureConfiguration? // calibrationLock
    private var configurationGeneration = 0 // calibrationLock
    private var droppedFrames = 0 // visionQueue only
    private var lastDepth: BodyDepthEstimate? // visionQueue only
    private var lastOrientation: BodyOrientation? // visionQueue only
    /// The 3D body pose is expensive; it runs on every third frame only while a caller
    /// (stance aiming, the avatar's torso turn) wants it. Guarded by `calibrationLock`.
    private var bodyOrientationEnabled = false
    private let handRequest: VNDetectHumanHandPoseRequest = {
        let request = VNDetectHumanHandPoseRequest()
        request.maximumHandCount = 2
        return request
    }()
    /// Hand pose only runs while gestures can be used, on every other frame; readings are reused
    /// briefly in between. Guarded by `calibrationLock`.
    private var handPoseEnabled = false
    private var frameCounter = 0
    private var lastHands: (time: TimeInterval, hands: [(wrist: CGPoint, shape: HandShape)]) = (0, [])
    private let calibrationLock = NSLock()
    private var calibration: PlayerCalibration?
    private var selectionGeneration = 0
    private var appliedSelectionGeneration = -1
    private var playerSelector = PlayerPoseSelector()
    private var isConfigured = false
    private var rotationCoordinator: AVCaptureDevice.RotationCoordinator?
    private var rotationObservation: NSKeyValueObservation?

    func setCalibration(_ calibration: PlayerCalibration?) {
        calibrationLock.lock()
        self.calibration = calibration
        selectionGeneration += 1
        calibrationLock.unlock()
        DispatchQueue.main.async { [weak self] in self?.playerMatchConfidence = nil }
    }

    func setHandPoseEnabled(_ enabled: Bool) {
        calibrationLock.lock()
        handPoseEnabled = enabled
        calibrationLock.unlock()
    }

    func setBodyOrientationEnabled(_ enabled: Bool) {
        calibrationLock.lock()
        bodyOrientationEnabled = enabled
        calibrationLock.unlock()
    }

    /// Configure only between attempts. The caller clears the previous setup.
    func setTracking(mode: CameraPoseMode, framesPerSecond: Int, profile: CameraCaptureProfile = .baseline) {
        calibrationLock.lock()
        poseMode = mode
        requestedFPS = [30, 60, 120].contains(framesPerSecond) ? framesPerSecond : 30
        captureProfile = profile
        calibrationLock.unlock()
        captureQueue.async { [weak self] in
            guard let self, let input = self.session.inputs.first as? AVCaptureDeviceInput else { return }
            if self.appliedProfile != profile {
                let wasRunning = self.session.isRunning
                if wasRunning { self.session.stopRunning() }
                self.visionQueue.sync {} // Drain old-sensor deliveries before changing coordinates.
                self.session.beginConfiguration()
                self.session.inputs.forEach { self.session.removeInput($0) }
                self.session.outputs.forEach { self.session.removeOutput($0) }
                self.session.commitConfiguration()
                self.isConfigured = false
                if self.configureSession(), wasRunning { self.session.startRunning() }
                return
            }
            self.session.beginConfiguration()
            let selected = self.selectWidestFormat(on: input.device)
            if !selected {
                self.session.inputs.forEach { self.session.removeInput($0) }
                self.session.outputs.forEach { self.session.removeOutput($0) }
                self.isConfigured = false
            }
            self.session.commitConfiguration()
            if !selected {
                if self.session.isRunning { self.session.stopRunning() }
                self.setStatus(.unavailable("The requested camera configuration could not be applied."))
            }
        }
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
        finishSourceRecording(reason: "Camera stopped or app interrupted")
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
        calibrationLock.lock()
        let profile = captureProfile
        calibrationLock.unlock()
        appliedProfile = profile
        let devices = AVCaptureDevice.DiscoverySession(deviceTypes: [.builtInWideAngleCamera, .builtInUltraWideCamera, .builtInTrueDepthCamera],
            mediaType: .video, position: .unspecified).devices
        let inventory = devices.map { device in
            CaptureDeviceCapability(name: device.localizedName, deviceType: device.deviceType.rawValue,
                position: device.position == .front ? "front" : "back", formats: Self.formatInventory(device))
        }
        DispatchQueue.main.async { [weak self] in self?.captureInventory = inventory }
        let wideFront = devices.first { $0.position == .front && $0.deviceType == .builtInUltraWideCamera }
        let baseline = AVCaptureDevice.default(.builtInWideAngleCamera, for: .video, position: .front)
        guard let camera = (profile == .wideFront ? wideFront ?? baseline : baseline),
              let input = try? AVCaptureDeviceInput(device: camera), session.canAddInput(input) else {
            setStatus(.unavailable("The front camera is unavailable."))
            return false
        }
        // A frozen image-space address must never follow automatic Center Stage pans/zooms.
        AVCaptureDevice.centerStageControlMode = .app
        AVCaptureDevice.isCenterStageEnabled = false
        guard selectWidestFormat(on: camera) else {
            setStatus(.unavailable("The requested camera configuration could not be applied."))
            return false
        }
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
        let supportedAngle = connection.isVideoRotationAngleSupported(angle)
            ? angle
            : CameraRotation.nearestRightAngle(to: angle)
        if connection.isVideoRotationAngleSupported(supportedAngle) {
            connection.videoRotationAngle = supportedAngle
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
    private static func formatInventory(_ camera: AVCaptureDevice) -> [CaptureFormatCapability] {
        camera.formats.enumerated().map { index, format in
            let size = CMVideoFormatDescriptionGetDimensions(format.formatDescription)
            var entry = CaptureFormatCapability(index: index, width: Int(size.width), height: Int(size.height),
                fieldOfView: Double(format.videoFieldOfView), rates: format.videoSupportedFrameRateRanges.map {
                    .init(minimum: $0.minFrameRate, maximum: $0.maxFrameRate)
                }, depthFormatCount: format.supportedDepthDataFormats.count, centerStageSupported: format.isCenterStageSupported)
            if #available(iOS 26.0, *) { entry.dynamicAspectRatios = format.supportedDynamicAspectRatios.map(\.rawValue) }
            return entry
        }
    }

    private func selectWidestFormat(on camera: AVCaptureDevice) -> Bool {
        calibrationLock.lock()
        let preferredFPS = requestedFPS
        let profile = captureProfile
        calibrationLock.unlock()
        let formats = Self.formatInventory(camera)
        let choice: CaptureFormatSelection.Choice?
        if profile == .baseline {
            // Preserve the existing baseline's format ordering for honest A/B comparisons.
            let selected = formats.filter { $0.height >= 720 && $0.height <= 1600 && $0.supports(30) }.max {
                $0.fieldOfView == $1.fieldOfView ? $0.height > $1.height : $0.fieldOfView < $1.fieldOfView
            }
            choice = selected.map { .init(index: $0.index, fps: $0.supports(preferredFPS) ? preferredFPS : 30) }
        } else { choice = CaptureFormatSelection.choose(formats, requestedFPS: preferredFPS) }
        guard let choice else {
            DispatchQueue.main.async { [weak self] in self?.configurationError = "No suitable 720p-or-better tracking format." }
            return false
        }
        let widest = camera.formats[choice.index]
        do {
            try camera.lockForConfiguration()
            defer { camera.unlockForConfiguration() }
            camera.activeFormat = widest
            if #available(iOS 26.0, *), profile == .wideFront,
               widest.supportedDynamicAspectRatios.contains(.ratio1x1) {
                camera.setDynamicAspectRatio(.ratio1x1, completionHandler: nil)
            }
            let fps = choice.fps
            camera.activeVideoMinFrameDuration = CMTime(value: 1, timescale: Int32(fps))
            camera.activeVideoMaxFrameDuration = CMTime(value: 1, timescale: Int32(fps))
            camera.videoZoomFactor = camera.minAvailableVideoZoomFactor
            let format = formats[choice.index]
            var ratio: String?
            if #available(iOS 26.0, *) { ratio = camera.dynamicAspectRatio?.rawValue }
            var reasons: [String] = []
            if profile == .wideFront && camera.deviceType != .builtInUltraWideCamera { reasons.append("Ultra-wide front camera unavailable; using wide camera.") }
            if fps != preferredFPS { reasons.append("Requested \(preferredFPS) fps unavailable at selected coverage; configured \(fps) fps.") }
            let configuration = CaptureConfiguration(profile: profile, cameraName: camera.localizedName,
                deviceType: camera.deviceType.rawValue, formatIndex: choice.index, requestedFPS: preferredFPS,
                configuredFPS: fps, formatWidth: format.width, formatHeight: format.height,
                nominalFieldOfView: format.fieldOfView, dynamicAspectRatio: ratio,
                centerStageActive: camera.isCenterStageActive, fallbackReason: reasons.isEmpty ? nil : reasons.joined(separator: " "))
            calibrationLock.lock()
            evidenceConfiguration = configuration
            configurationGeneration += 1
            calibrationLock.unlock()
            DispatchQueue.main.async { [weak self] in
                self?.captureFPS = fps
                self?.captureConfiguration = configuration
                self?.configurationError = nil
            }
        } catch {
            DispatchQueue.main.async { [weak self] in self?.configurationError = error.localizedDescription }
            return false
        }
        let fov = widest.videoFieldOfView / Float(camera.videoZoomFactor)
        DispatchQueue.main.async { [weak self] in
            self?.fieldOfView = fov
        }
        return true
    }

    func captureOutput(_ output: AVCaptureOutput, didOutput sampleBuffer: CMSampleBuffer, from connection: AVCaptureConnection) {
        let callbackStarted = ProcessInfo.processInfo.systemUptime
        guard let pixelBuffer = CMSampleBufferGetImageBuffer(sampleBuffer) else { return }
        let timestamp = CMSampleBufferGetPresentationTimeStamp(sampleBuffer).seconds
        calibrationLock.lock()
        let configuration = evidenceConfiguration
        let generation = configurationGeneration
        calibrationLock.unlock()
        let evidence = configuration.map { CaptureEvidence(configuration: $0,
            geometry: CaptureGeometry(width: CVPixelBufferGetWidth(pixelBuffer), height: CVPixelBufferGetHeight(pixelBuffer),
                rotationDegrees: Double(connection.videoRotationAngle), mirrored: connection.isVideoMirrored,
                configurationGeneration: generation), thermalState: String(describing: ProcessInfo.processInfo.thermalState)) }
        sourceRecorder.append(sampleBuffer, evidence: evidence)
        do {
            // The output connection has already made the pixels upright and mirrored.
            let handler = VNImageRequestHandler(cvPixelBuffer: pixelBuffer, orientation: .up)
            calibrationLock.lock()
            frameCounter += 1
            let runHands = handPoseEnabled && frameCounter.isMultiple(of: 2)
            let handsEnabled = handPoseEnabled
            let mode = poseMode
            let runDepth = mode == .depthPreview && frameCounter.isMultiple(of: 3)
            let runOrientation = bodyOrientationEnabled && frameCounter.isMultiple(of: 3)
            calibrationLock.unlock()
            try handler.perform(runHands ? [request, handRequest] : [request])
            if runHands {
                lastHands = (timestamp, (handRequest.results ?? []).compactMap(Self.handShape))
            }
            let observations = request.results ?? []
            let rawPoses = observations.compactMap { try? poseFrame(from: $0, timestamp: timestamp) }
            // The capture connection already rotates the pixels. Never infer camera rotation
            // from a leaning golfer: a pose-dependent quarter turn invents a swing.
            let frames = rawPoses
            var selection = selectPlayer(from: frames, at: timestamp)
            if mode == .body2D || frames.count != 1 || selection == nil { lastDepth = nil }
            if frames.count != 1 || selection == nil { lastOrientation = nil }
            if runDepth || runOrientation, frames.count == 1, let selected = selection?.frame {
                // Optional work. A failed 3D request must not erase the 2D pose.
                do {
                    try handler.perform([depthRequest])
                    let observation = depthRequest.results?.first
                    if runDepth { lastDepth = observation.flatMap { Self.depthEstimate($0, matching: selected, at: timestamp) } }
                    if runOrientation { lastOrientation = observation.flatMap { Self.bodyOrientation($0, matching: selected, at: timestamp) } }
                } catch { lastDepth = nil; lastOrientation = nil }
            }
            let selectedIndex = selection.flatMap { selection in frames.firstIndex(of: selection.frame) }
            if handsEnabled, timestamp - lastHands.time < 0.2, let selectedIndex {
                selection?.frame.hands = Self.readings(lastHands.hands, for: rawPoses[selectedIndex])
            }
            if let depth = lastDepth, timestamp - depth.timestamp <= 0.15 { selection?.frame.depth = depth }
            if let orientation = lastOrientation, orientation.isFresh(at: timestamp) { selection?.frame.orientation = orientation }
            let width = CGFloat(CVPixelBufferGetWidth(pixelBuffer))
            let height = CGFloat(CVPixelBufferGetHeight(pixelBuffer))
            let aspect = Self.deliveredAspect(width: width, height: height)
            let delivery = PoseDelivery(frame: selection?.frame, captureTime: timestamp, aspect: aspect,
                bodyCount: frames.count, callbackStarted: callbackStarted,
                inferenceFinished: ProcessInfo.processInfo.systemUptime, candidateFrames: frames,
                droppedFrames: droppedFrames, poseMode: mode, captureEvidence: evidence)
            DispatchQueue.main.async { [weak self] in
                self?.latestCaptureTimestamp = timestamp
                self?.detectedBodyCount = frames.count
                self?.latestFrame = selection?.frame
                self?.playerMatchConfidence = selection?.confidence
                if height > 0, let self {
                    if abs(self.frameAspect - aspect) > 0.001 { self.frameAspect = aspect }
                }
                self?.latestDelivery = delivery
            }
        } catch {
            let finished = ProcessInfo.processInfo.systemUptime
            let dropped = droppedFrames
            calibrationLock.lock()
            let mode = poseMode
            calibrationLock.unlock()
            lastDepth = nil
            DispatchQueue.main.async { [weak self] in
                self?.latestCaptureTimestamp = timestamp
                self?.detectedBodyCount = 0
                self?.latestFrame = nil
                self?.playerMatchConfidence = nil
                self?.latestDelivery = PoseDelivery(frame: nil, captureTime: timestamp,
                    aspect: self?.frameAspect ?? 1, bodyCount: 0,
                    callbackStarted: callbackStarted, inferenceFinished: finished,
                    droppedFrames: dropped, poseMode: mode, captureEvidence: evidence)
            }
            #if DEBUG
            print("Vision body-pose request failed: \(error.localizedDescription)")
            #endif
        }
    }

    func captureOutput(_ output: AVCaptureOutput, didDrop sampleBuffer: CMSampleBuffer, from connection: AVCaptureConnection) {
        droppedFrames += 1
    }

    private static let joints3D: [(BodyJoint, VNHumanBodyPose3DObservation.JointName)] = [
        (.root, .root), (.neck, .centerShoulder), (.nose, .centerHead),
        (.leftShoulder, .leftShoulder), (.rightShoulder, .rightShoulder),
        (.leftElbow, .leftElbow), (.rightElbow, .rightElbow),
        (.leftWrist, .leftWrist), (.rightWrist, .rightWrist),
        (.leftHip, .leftHip), (.rightHip, .rightHip), (.leftKnee, .leftKnee),
        (.rightKnee, .rightKnee), (.leftAnkle, .leftAnkle), (.rightAnkle, .rightAnkle)
    ]

    /// Vision 3D selects the most prominent person. Require correspondence with the selected
    /// 2D torso rather than attaching another person's body to the player.
    private static func matchesPlayer(_ observation: VNHumanBodyPose3DObservation, frame: PoseFrame) -> Bool {
        for (body, key) in joints3D where [.leftShoulder, .rightShoulder, .leftHip, .rightHip].contains(body) {
            guard let observed = frame.point(body, minimumConfidence: 0.45),
                  let projected = try? observation.pointInImage(key),
                  hypot(observed.x - projected.location.x, observed.y - projected.location.y) < 0.08 else { return false }
        }
        return true
    }

    /// Shoulder and hip lines in camera space. Vision's camera looks down its -z axis, so the
    /// joint nearer the phone has the larger z; x follows the (mirrored) image, so image right
    /// is the player's right.
    private static func bodyOrientation(_ observation: VNHumanBodyPose3DObservation, matching frame: PoseFrame,
                                        at time: Double) -> BodyOrientation? {
        guard matchesPlayer(observation, frame: frame) else { return nil }
        let cameraFromModel = simd_inverse(observation.cameraOriginMatrix)
        func camera(_ key: VNHumanBodyPose3DObservation.JointName) -> simd_float3? {
            guard let point = try? observation.recognizedPoint(key) else { return nil }
            let position = cameraFromModel * point.position.columns.3
            return simd_float3(position.x, position.y, position.z)
        }
        guard let leftShoulder = camera(.leftShoulder), let rightShoulder = camera(.rightShoulder),
              let leftHip = camera(.leftHip), let rightHip = camera(.rightHip) else { return nil }
        let shoulders = Self.lineYaw(from: leftShoulder, to: rightShoulder)
        let hips = Self.lineYaw(from: leftHip, to: rightHip)
        guard let shoulders, let hips else { return nil }
        return BodyOrientation(timestamp: time, shoulderYaw: shoulders, hipYaw: hips)
    }

    /// Degrees a left-to-right body line is turned from the image plane; nil for a line seen
    /// end-on or folded over, which no upright golfer produces.
    static func lineYaw(from left: simd_float3, to right: simd_float3) -> Double? {
        let across = right.x - left.x
        let nearer = right.z - left.z
        guard across.isFinite, nearer.isFinite, across > 0.05 else { return nil }
        let degrees = Double(atan2(nearer, across)) * 180 / .pi
        return abs(degrees) < 75 ? degrees : nil
    }

    private static func depthEstimate(_ observation: VNHumanBodyPose3DObservation, matching frame: PoseFrame,
                                      at time: Double) -> BodyDepthEstimate? {
        let keys = joints3D
        guard matchesPlayer(observation, frame: frame) else { return nil }
        let cameraFromModel = simd_inverse(observation.cameraOriginMatrix)
        let rootZ = (cameraFromModel * simd_float4(0, 0, 0, 1)).z
        let height = observation.bodyHeight
        guard height.isFinite, height > 0.5 else { return nil }
        var values: [String: Float] = [:]
        for (body, key) in keys {
            guard let point = try? observation.recognizedPoint(key) else { continue }
            let z = (cameraFromModel * point.position.columns.3).z
            let normalized = (z - rootZ) / height
            if normalized.isFinite, abs(normalized) < 0.75 { values[body.rawValue] = normalized }
        }
        return BodyDepthEstimate(timestamp: time, normalizedDepth: values)
    }

    private func selectPlayer(from frames: [PoseFrame], at time: Double) -> (frame: PoseFrame, confidence: Double?)? {
        calibrationLock.lock()
        let savedCalibration = calibration
        let generation = selectionGeneration
        calibrationLock.unlock()
        if appliedSelectionGeneration != generation {
            playerSelector = PlayerPoseSelector()
            lastDepth = nil
            appliedSelectionGeneration = generation
        }
        return playerSelector.select(from: frames, calibration: savedCalibration, at: time)
    }

    static func deliveredAspect(width: CGFloat, height: CGFloat) -> CGFloat {
        height > 0 ? width / height : 1
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

    /// Fingertips folded in past their knuckles on most fingers is a fist; stretched out is open.
    static func handShape(_ observation: VNHumanHandPoseObservation) -> (wrist: CGPoint, shape: HandShape)? {
        guard let wrist = try? observation.recognizedPoint(.wrist), wrist.confidence >= 0.3 else { return nil }
        let fingers: [(VNHumanHandPoseObservation.JointName, VNHumanHandPoseObservation.JointName)] = [
            (.indexTip, .indexMCP), (.middleTip, .middleMCP), (.ringTip, .ringMCP), (.littleTip, .littleMCP)
        ]
        var curled = 0, extended = 0, measured = 0
        for (tipName, knuckleName) in fingers {
            guard let tip = try? observation.recognizedPoint(tipName), tip.confidence >= 0.3,
                  let knuckle = try? observation.recognizedPoint(knuckleName), knuckle.confidence >= 0.3 else { continue }
            let knuckleReach = hypot(knuckle.location.x - wrist.location.x, knuckle.location.y - wrist.location.y)
            guard knuckleReach > 0.001 else { continue }
            let ratio = hypot(tip.location.x - wrist.location.x, tip.location.y - wrist.location.y) / knuckleReach
            measured += 1
            if ratio < 1.15 { curled += 1 } else if ratio > 1.5 { extended += 1 }
        }
        let shape: HandShape = measured < 3 ? .unknown : curled >= 3 ? .fist : extended >= 3 ? .open : .unknown
        return (wrist.location, shape)
    }

    /// Pins each hand reading to the nearer body wrist. Matching uses the uncorrected body pose,
    /// which shares the hand request's image coordinates.
    static func readings(_ hands: [(wrist: CGPoint, shape: HandShape)], for frame: PoseFrame) -> [HandReading] {
        hands.compactMap { hand in
            let candidates: [(BodyJoint, CGFloat)] = [BodyJoint.leftWrist, .rightWrist].compactMap { joint in
                frame.point(joint).map { (joint, hypot($0.x - hand.wrist.x, $0.y - hand.wrist.y)) }
            }
            guard let nearest = candidates.min(by: { $0.1 < $1.1 }), nearest.1 < 0.12 else { return nil }
            return HandReading(wrist: nearest.0, shape: hand.shape)
        }
    }

    private func setStatus(_ newStatus: Status) {
        DispatchQueue.main.async { [weak self] in self?.status = newStatus }
    }
}

/// Use the saved scan to acquire a person, then follow torso continuity. Requiring the person's
/// standing limb proportions on EVERY frame rejects bent arms, foreshortening and occluded legs
/// during a golf swing. This is spatial association, not biometric identity recognition.
struct PlayerPoseSelector {
    private var lastFrame: PoseFrame?
    private var lastTime: Double?
    private var associationScale: CGFloat = 0.1

    mutating func select(from frames: [PoseFrame], calibration: PlayerCalibration?, at time: Double)
        -> (frame: PoseFrame, confidence: Double?)? {
        if let previous = lastFrame, let lastTime, time - lastTime <= 1.2 {
            let candidates = frames.compactMap { frame -> (PoseFrame, Double)? in
                // A backswing turns/occludes the shoulders. Associate using whichever torso
                // landmarks are still measured, not a mandatory pair of shoulders and width.
                let core: [BodyJoint] = [.root, .leftHip, .rightHip, .neck, .leftShoulder, .rightShoulder]
                let travel = core.compactMap { joint -> CGFloat? in
                    guard let a = previous.point(joint, minimumConfidence: 0.35),
                          let b = frame.point(joint, minimumConfidence: 0.35) else { return nil }
                    return hypot(b.x - a.x, b.y - a.y) / associationScale
                }.sorted()
                guard travel.count >= 2 else { return nil }
                let median = travel[travel.count / 2]
                guard median <= 1.25 else { return nil }
                return (frame, Double(median))
            }.sorted { $0.1 < $1.1 }
            if let best = candidates.first {
                // Do not switch between two overlapping people when association is ambiguous.
                if candidates.count > 1, candidates[1].1 - best.1 < 0.15 { return nil }
                remember(best.0, at: time)
                return (best.0, max(0, 1 - best.1 / 1.5))
            }
            // Keep the target through short occlusions, rather than adopting a bystander.
            return nil
        }
        let selected: (frame: PoseFrame, confidence: Double?)?
        if let calibration {
            let ranked = frames.compactMap { frame -> (PoseFrame, Double)? in
                calibration.matchScore(for: frame).map { (frame, $0) }
            }.min { $0.1 < $1.1 }
            if let ranked, ranked.1 <= 0.24 {
                selected = (ranked.0, max(0, 1 - ranked.1 / 0.24))
            } else { selected = nil }
        } else {
            selected = frames.max(by: { acquisitionScore($0) < acquisitionScore($1) }).map { ($0, nil) }
        }
        if let selected {
            associationScale = max(0.08, selected.frame.torsoLength ?? selected.frame.shoulderWidth ?? 0.1)
            remember(selected.frame, at: time)
        }
        return selected
    }

    private mutating func remember(_ frame: PoseFrame, at time: Double) {
        if lastFrame == nil {
            associationScale = max(0.08, frame.torsoLength ?? frame.shoulderWidth ?? 0.1)
        }
        lastFrame = frame
        lastTime = time
    }

    private func acquisitionScore(_ frame: PoseFrame) -> Double {
        guard let bounds = frame.bodyBounds else { return 0 }
        let completeness = Double(frame.points.values.filter { $0.confidence >= 0.45 }.count) / Double(BodyJoint.allCases.count)
        let centerPenalty = hypot(Double(bounds.midX - 0.5), Double(bounds.midY - 0.5))
        return completeness + Double(bounds.height) * 1.4 - centerPenalty * 0.45
    }
}

enum CameraRotation {
    static func nearestRightAngle(to angle: CGFloat) -> CGFloat {
        var normalized = angle.truncatingRemainder(dividingBy: 360)
        if normalized < 0 { normalized += 360 }
        let snapped = (normalized / 90).rounded() * 90
        return snapped == 360 ? 0 : snapped
    }
}
