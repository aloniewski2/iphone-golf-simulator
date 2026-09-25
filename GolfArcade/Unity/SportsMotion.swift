import ARKit
import CoreMotion

// Bounded, local-only diagnostics: no camera images or player identity.
enum SportsDiagnostics {
    private static let queue=DispatchQueue(label:"sports.diagnostics")
    static func write(_ message:String) {
        NSLog("[SportsDiagnostic] %@",message)
        let line="\(Date().timeIntervalSince1970) \(message)\n"
        queue.async {
            let url=FileManager.default.urls(for:.documentDirectory,in:.userDomainMask)[0].appendingPathComponent("SportsDiagnostics.log")
            if !FileManager.default.fileExists(atPath:url.path) { FileManager.default.createFile(atPath:url.path,contents:nil) }
            guard let handle=try? FileHandle(forWritingTo:url) else { return }
            defer { try? handle.close() }
            if let end=try? handle.seekToEnd(), end>262144 {
                // Keep the most recent half rather than wiping the file: a full reset throws
                // away the Ready and axis-lock lines that explain everything after them.
                try? handle.seek(toOffset:end/2)
                let kept=(try? handle.readToEnd()) ?? nil
                try? handle.truncate(atOffset:0); try? handle.seek(toOffset:0)
                if let kept { try? handle.write(contentsOf:kept) }
            }
            if let data=line.data(using:.utf8) { try? handle.write(contentsOf:data) }
        }
    }
}

/// Tracking is graded, not a boolean. A hard swing blurs the camera and ARKit drops to
/// `.limited` for a fraction of a second; that must warn, not end the rally.
enum SportsTrackingQuality: String, Sendable { case good, degraded, lost }

/// Live state of the aim-at-the-TV gate that locks the court axis before play.
struct SportsAxisGate: Sendable {
    var locked = false
    var progress = 0.0          // 0...1 once every condition holds
    var message = "Stand about 2.5 m back and point the back of your phone at the TV."
    /// Live sensor readout, so a gate that refuses to settle can be diagnosed on the spot.
    var detail = ""
    /// Seconds the gate has been failing, used to offer a manual override.
    var stalledFor = 0.0
}

/// What the screens need from the sensors: posted to the main thread when it changes, and at
/// most ten times a second otherwise. Unity never waits on this; it gets samples directly.
struct SportsMotionStatus: Equatable, Sendable {
    var target = 0.0
    var phase = "calibrating"
    var quality = SportsTrackingQuality.lost
    var valid = false
}

/// Phone motion for the sports: the motion sensors (swings, grip, racket-face aim) and ARKit
/// world tracking (court position, and the court axis captured once by aiming at the TV).
///
/// Threading. Everything time-critical runs on `queue`, a serial high-priority queue of its
/// own: the sensor callback, ARKit frames, swing detection and the sample handed to Unity.
/// It used to run on the main thread, which Unity renders on, so a swing could sit behind a
/// frame being drawn before Unity heard of it. Samples now go to the bridge (a mutex-guarded
/// ring) the moment they are read, and Unity takes the newest at the start of its next frame.
/// The screens get their updates on the main thread. State below is only touched on `queue`;
/// the public methods hop onto it.
final class SportsMotion: NSObject, ARSessionDelegate, @unchecked Sendable {
    /// The court axis only survives while ARKit keeps the same world origin. Everything here
    /// is built so `resetTracking` happens exactly once, at axis capture.
    private static let signKey="sports.tennis.steerSign"
    private static let holdRequired=1.0
    private static let maxLensPitch=35.0*Double.pi/180
    private static let maxGateRotation=0.5
    private static let degradedGrace=2.0

    private let queue = DispatchQueue(label: "sports.motion", qos: .userInteractive)
    private let operations = OperationQueue()
    private let ar = ARSession()
    private let motion = CMMotionManager()
    private var filter = SteeringFilter()
    private var position = SIMD3<Float>.zero, right = SIMD3<Float>(1,0,0)
    private var interrupted=false
    private var frameAt = -Double.infinity, reliableAt = -Double.infinity
    private var tennis = false, calibrated = false
    private var neutralX=0.0, neutralY:Float=0
    /// Horizontal direction toward the TV, fixed when the axis locks.
    private var courtForward = SIMD3<Float>(0,0,-1)
    /// Direction out of the screen, updated every frame; compared against `courtForward`.
    private var screenNormal = SIMD3<Float>(0,0,1)
    private var strokeFacing = -1.0
    private var activeStrokeFacing = -1.0
    /// Latest motion-sensor attitude, and the heading of the TV in that frame (captured when
    /// the axis locks). Grip and racket-face aim are read from these, never the camera.
    private var attitude: simd_quatd?
    private var tvHeading: Double?
    /// Each player's habitual face angle per wing (forehand, backhand), learnt as they play.
    private var faceNeutral = [0.0, 0.0]
    private var faceAim = 0.0
    /// Distance to the TV from the depth sensor while the axis is being captured.
    private var tvDistance: Double?
    private var nextDiagnostic=0.0, nextPreview=0.0, trackingReason="not started"
    private var worldRunning=false
    private var capturingAxis=false, gateHeldSince = -Double.infinity, lastRotation=0.0, captureStartedAt=0.0
    private var gate = SportsAxisGate()
    private var sign: Double = UserDefaults.standard.object(forKey:SportsMotion.signKey) as? Double ?? 1
    /// Recent rate/force history, written out around each stroke candidate so the detection
    /// thresholds can be tuned from real swings instead of reasoning. Fixed size, no growth.
    private var trace = [(Double,Double,Double)](repeating:(0,0,0),count:80)
    private var traceHead = 0, traceOnsets = 0, traceDue = Double.infinity
    private var traceOutcome = ""
    // Output to Unity.
    private var token: Int32 = 0, live = false, swings = 0, lastPower = 0.0
    private var status = SportsMotionStatus(), nextStatusAt = 0.0
    // Delay probe: centre-of-frame brightness while the camera is aimed at the TV.
    private var probing = false
    private var probe = SportsDelayProbe.Samples()

    /// The screens' callbacks, always called on the main thread.
    @MainActor var onProblem: ((String) -> Void)?
    @MainActor var onGate: ((SportsAxisGate) -> Void)?
    @MainActor var onStatus: ((SportsMotionStatus) -> Void)?
    @MainActor weak var preview: SportsPreviewView?

    override init() {
        super.init()
        operations.underlyingQueue = queue
        operations.maxConcurrentOperationCount = 1
    }

    /// Flips left/right without recapturing the axis, for when the mapping comes out mirrored.
    var courtSign: Double {
        get { queue.sync { sign } }
        set { UserDefaults.standard.set(newValue,forKey:Self.signKey); queue.async { self.sign=newValue } }
    }
    var axisLocked: Bool { queue.sync { gate.locked } }
    /// Exposed so the aiming screen can show a viewfinder from this same session.
    var previewSession: ARSession { ar }

    // MARK: - Main-thread callbacks

    private func report(_ message: String) {
        DispatchQueue.main.async { MainActor.assumeIsolated { self.onProblem?(message) } }
    }
    private func publish(_ gate: SportsAxisGate) {
        DispatchQueue.main.async { MainActor.assumeIsolated { self.onGate?(gate) } }
    }
    private func publish(_ status: SportsMotionStatus) {
        DispatchQueue.main.async { MainActor.assumeIsolated { self.onStatus?(status) } }
    }

    // MARK: - Session lifecycle

    func start(tennis: Bool, travel: Double) {
        if tennis && AVCaptureDevice.authorizationStatus(for: .video) != .authorized {
            AVCaptureDevice.requestAccess(for: .video) { [weak self] allowed in
                if allowed { self?.start(tennis:true,travel:travel) }
                else { self?.report("Camera permission denied. Physical steering needs the rear camera; touch remains available.") }
            }
            return
        }
        queue.async { self.startOnQueue(tennis: tennis, travel: travel) }
    }

    private func startOnQueue(tennis: Bool, travel: Double) {
        calibrated=false; self.tennis=tennis; filter=SteeringFilter(); filter.travel=travel
        filter.tennisStroke = tennis
        SportsDiagnostics.write("motion start tennis=\(tennis) axisLocked=\(gate.locked)")
        guard motion.isDeviceMotionAvailable else { report("Motion sensors unavailable. Select touch controls."); return }
        if tennis {
            guard ARWorldTrackingConfiguration.isSupported else { report("Physical steering is unsupported. Select touch controls."); return }
            runWorldTracking(reset: !worldRunning)
        }
        startDeviceMotion()
    }

    /// Reset only when there is no locked axis to protect. Relocalizing into the existing
    /// map is what keeps "right" pointing the same way for the whole session.
    private func runWorldTracking(reset: Bool, depth: Bool = false) {
        let configuration=ARWorldTrackingConfiguration()
        configuration.worldAlignment = .gravity
        // No plane detection: nothing uses planes, and finding them costs every frame.
        configuration.planeDetection = []
        if depth && ARWorldTrackingConfiguration.supportsFrameSemantics(.sceneDepth) {
            // LiDAR depth only while aiming at the TV, to measure how far away the player is.
            configuration.frameSemantics.insert(.sceneDepth)
        } else if let lean = Self.leanestFormat() {
            // During play only the phone's position is needed: the smallest image at the full
            // frame rate leaves the most room for the game and the AirPlay encoder.
            configuration.videoFormat = lean
        }
        ar.delegate=self; ar.delegateQueue = queue
        ar.run(configuration,options: reset ? [.resetTracking,.removeExistingAnchors] : [])
        worldRunning=true
        if reset { gate.locked=false }
        SportsDiagnostics.write("world tracking run reset=\(reset) depth=\(depth) format=\(configuration.videoFormat.imageResolution)@\(configuration.videoFormat.framesPerSecond)")
    }

    /// Lowest-resolution world-tracking format among those at the highest frame rate.
    private static func leanestFormat() -> ARConfiguration.VideoFormat? {
        let formats = ARWorldTrackingConfiguration.supportedVideoFormats
        guard let fastest = formats.map(\.framesPerSecond).max() else { return nil }
        return formats.filter { $0.framesPerSecond == fastest }
            .min { $0.imageResolution.width * $0.imageResolution.height < $1.imageResolution.width * $1.imageResolution.height }
    }

    private func startDeviceMotion() {
        guard !motion.isDeviceMotionActive else { return }
        motion.deviceMotionUpdateInterval=0.01
        // Z vertical with long-term yaw correction: the racket-face heading has to stay
        // anchored to the TV direction for a whole match.
        let frame: CMAttitudeReferenceFrame = CMMotionManager.availableAttitudeReferenceFrames().contains(.xArbitraryCorrectedZVertical)
            ? .xArbitraryCorrectedZVertical : .xArbitraryZVertical
        // Delivered straight onto `queue`, not the main thread.
        motion.startDeviceMotionUpdates(using:frame,to:operations) { [weak self] sample,error in
            guard let self else { return }
            if let error { self.report("Motion sensor error: \(error.localizedDescription)"); return }
            guard let sample else { return }
            self.consume(sample)
        }
    }

    // MARK: - Output to Unity

    /// Where samples go and whether they flow: `live` while a motion-controlled session is
    /// playing (not paused). `swingBase` continues the swing count after touch play.
    func setOutput(token: Int32, live: Bool, swingBase: Int) {
        queue.async { self.token=token; self.live=live; self.swings=max(self.swings,swingBase) }
    }
    /// Swings counted so far, so touch play can carry on from here.
    var swingCount: Int { queue.sync { swings } }

    // MARK: - Aim-at-the-TV gate

    /// Forget the court direction, so the next match captures it again (Settings → Controls).
    func clearAxis() {
        queue.async { self.gate=SportsAxisGate(); self.publish(self.gate) }
    }

    /// Begin capturing the court axis. Tennis cannot start until this locks, because a phone
    /// that flips between forehand and backhand can never reveal where the TV is mid-rally.
    func beginAxisCapture(travel: Double) {
        guard ARWorldTrackingConfiguration.isSupported else { report("Physical steering is unsupported. Select touch controls."); return }
        guard AVCaptureDevice.authorizationStatus(for: .video) == .authorized else {
            AVCaptureDevice.requestAccess(for: .video) { [weak self] allowed in
                if allowed { self?.beginAxisCapture(travel:travel) }
                else { self?.report("Camera permission denied. Physical steering needs the rear camera; touch remains available.") }
            }
            return
        }
        queue.async {
            self.tennis=true; self.filter=SteeringFilter(); self.filter.travel=travel; self.filter.tennisStroke=true
            self.capturingAxis=true; self.gateHeldSince = -Double.infinity; self.captureStartedAt=SportsRuntime.shared().clock()
            self.gate=SportsAxisGate(); self.publish(self.gate)
            self.runWorldTracking(reset: true, depth: true)
            self.startDeviceMotion()
            SportsDiagnostics.write("axis capture begin")
        }
    }

    func cancelAxisCapture() { queue.async { self.capturingAxis=false; self.gateHeldSince = -Double.infinity } }

    /// Last resort when the gate will not settle (dark room, awkward pose). Takes the axis
    /// from the current pose regardless of how steady or level it is. Left/right may come
    /// out mirrored, which is what the flip control is for.
    @discardableResult func forceAxisFromCurrentPose() -> Bool {
        let problem: String? = queue.sync {
            guard let frame=ar.currentFrame else { return "No camera frame yet. Keep the lens uncovered." }
            let back=frame.camera.transform.columns.2
            guard let axis=SportsMotionGeometry.horizontalRight(cameraBack:SIMD3<Float>(back.x,back.y,back.z)) else {
                return "Aim the back of the phone roughly at the TV, not straight up or down."
            }
            right=axis
            if let forward=SportsMotionGeometry.horizontal(SIMD3<Float>(back.x,back.y,back.z)) { courtForward = -forward }
            strokeFacing = -1
            captureTVHeading()
            gate.locked=true; gate.progress=1; gate.message="Court direction set manually."
            capturingAxis=false
            SportsDiagnostics.write("axis forced right=\(right) sign=\(sign)")
            publish(gate)
            return nil
        }
        if let problem { report(problem); return false }
        return true
    }

    private func evaluateGate(frame: ARFrame, time: Double) {
        let back=frame.camera.transform.columns.2
        let cameraBack=SIMD3<Float>(back.x,back.y,back.z)
        let pitch=SportsMotionGeometry.lensPitch(cameraBack:cameraBack)
        var failure: String?
        if case .notAvailable = frame.camera.trackingState {
            failure="Camera is not delivering frames yet."
        }
        if failure == nil && pitch > Self.maxLensPitch { failure="Tilt the phone level — aim the back of it straight at the TV (currently \(Int(pitch*180/Double.pi))° off level)." }
        if failure == nil && lastRotation > Self.maxGateRotation { failure="Hold still for a moment." }
        tvDistance = Self.centreDepth(frame)
        if failure == nil, let distance = tvDistance, distance < SportsMotionGeometry.minTVDistance {
            failure = String(format:"Step back — you're about %.1f m from the TV. Stand about %.1f m away so you have room to swing.", distance, SportsMotionGeometry.idealTVDistance)
        }
        if failure == nil && SportsMotionGeometry.horizontalRight(cameraBack:cameraBack) == nil { failure="Aim the back of the phone at the TV, not at the floor or ceiling." }
        guard failure == nil, let axis=SportsMotionGeometry.horizontalRight(cameraBack:cameraBack) else {
            gateHeldSince = -Double.infinity
            gate.progress=0; gate.message=failure ?? gate.message
            gate.detail="Tracking \(trackingReason) · \(Int(pitch*180/Double.pi))° off level" + (tvDistance.map { String(format:" · %.1f m from TV",$0) } ?? "")
            gate.stalledFor = capturingAxis ? time-captureStartedAt : 0
            publish(gate); return
        }
        if gateHeldSince == -Double.infinity { gateHeldSince=time }
        let held=time-gateHeldSince
        gate.progress=min(1,held/Self.holdRequired)
        gate.message = gate.progress<1 ? "Hold it there…" : "Court direction locked."
        gate.detail="Tracking \(trackingReason) · \(Int(pitch*180/Double.pi))° off level" + (tvDistance.map { String(format:" · %.1f m from TV",$0) } ?? "")
        if held >= Self.holdRequired {
            right=axis
            // The lens points at the TV right now, and the screen points away from it.
            if let forward=SportsMotionGeometry.horizontal(cameraBack) { courtForward = -forward }
            strokeFacing = -1
            captureTVHeading()
            gate.locked=true; capturingAxis=false
            // Depth was only for the distance check; drop it for the rest of the session.
            runWorldTracking(reset: false)
            SportsDiagnostics.write("axis locked right=\(right) forward=\(courtForward) sign=\(sign) pitch=\(pitch) tvHeading=\(tvHeading ?? .nan) distance=\(tvDistance ?? -1)")
        }
        publish(gate)
    }

    // MARK: - TV delay probe

    /// While on, record how bright the middle of the camera image is (the TV, right after the
    /// axis is aimed at it) for SportsDelayProbe.
    func setDelayProbe(_ on: Bool) { queue.async { self.probing=on; if on { self.probe=SportsDelayProbe.Samples() } } }

    /// The TV's delay behind a white frame the game set at `rendered` (shared clock), from
    /// when the camera saw the TV turn bright. nil if it was not seen clearly.
    func delayAfterFlash(rendered: Double) -> Double? { queue.sync { SportsDelayProbe.delay(probe.series, rendered: rendered) } }

    /// Mean luma over the middle of the image: plane 0 of ARKit's YCbCr buffer, sparsely.
    private static func centreLuma(_ buffer: CVPixelBuffer) -> Double? {
        guard CVPixelBufferGetPlaneCount(buffer) > 0 else { return nil }
        CVPixelBufferLockBaseAddress(buffer,.readOnly); defer { CVPixelBufferUnlockBaseAddress(buffer,.readOnly) }
        guard let base=CVPixelBufferGetBaseAddressOfPlane(buffer,0) else { return nil }
        let w=CVPixelBufferGetWidthOfPlane(buffer,0), h=CVPixelBufferGetHeightOfPlane(buffer,0), row=CVPixelBufferGetBytesPerRowOfPlane(buffer,0)
        let pixels=base.assumingMemoryBound(to:UInt8.self)
        var total=0, count=0
        for y in stride(from:h*35/100,to:h*65/100,by:6) {
            for x in stride(from:w*35/100,to:w*65/100,by:6) { total+=Int(pixels[y*row+x]); count+=1 }
        }
        return count>0 ? Double(total)/Double(count) : nil
    }

    // MARK: - Per-sample steering

    private func consume(_ sample: CMDeviceMotion) {
        let time=SportsRuntime.shared().clock()
        let rate=sample.rotationRate
        let speed=sqrt(rate.x*rate.x+rate.y*rate.y+rate.z*rate.z)
        lastRotation=speed
        let aq=sample.attitude.quaternion
        attitude=simd_quatd(ix:aq.x,iy:aq.y,iz:aq.z,r:aq.w)
        guard !capturingAxis else { return }
        let current=currentQuality(at:time)
        // Only `good` advances the fixed real-world→court mapping. `degraded` holds the last
        // court position and keeps the rally alive; `lost` is what finally pauses play.
        let tracked = !tennis || current == .good
        let x=Double(simd_dot(position,right))*sign
        // Swings never depend on the camera: a sideways or flat swing blurs or blinds it, but
        // the gyro reads it perfectly. Camera loss only holds the lean/steering hint.
        let grace = tennis
        let previousPhase=self.filter.phase
        let a=sample.userAcceleration
        let force=sqrt(a.x*a.x+a.y*a.y+a.z*a.z)
        let swing=filter.step(position:x,rate:speed,time:time,valid:tracked,allowSwingWhileUntracked:grace,acceleration:force)
        if time>=nextDiagnostic {
            nextDiagnostic=time+1
            SportsDiagnostics.write("sensor quality=\(current.rawValue) ar=\(trackingReason) frameAge=\(time-frameAt) reliableAge=\(time-reliableAt) ready=\(calibrated) axisLocked=\(gate.locked) phase=\(filter.phase.rawValue) delta=\(x-neutralX) target=\(filter.target) reachL=\(filter.reachLeft) reachR=\(filter.reachRight) seenL=\(filter.seenLeft) seenR=\(filter.seenRight) rate=\(speed) force=\(force) sign=\(sign) facing=\(strokeFacing)")
        }
        let q=sample.attitude.quaternion, g=sample.gravity
        let screenHeading = attitude.flatMap { SportsMotionGeometry.heading(SportsMotionGeometry.rotate(SIMD3<Double>(0,0,1),by:$0)) }
        if tennis, gate.locked {
            if let tv=tvHeading { strokeFacing=SportsMotionGeometry.strokeFacing(screenHeading:screenHeading,tvHeading:tv,previous:strokeFacing) }
            else { strokeFacing=SportsMotionGeometry.strokeFacing(screenNormal:screenNormal,courtForward:courtForward,previous:strokeFacing) }
        }
        if previousPhase != .swinging && filter.phase == .swinging { activeStrokeFacing=strokeFacing }
        // Racket-face aim, read live through the whole swing so the game samples the face at
        // the moment of contact (it used to be frozen when the stroke was confirmed, mid-swing).
        // A face pointing straight up or down (phone held flat) has no direction: aim straight.
        let swinging = filter.phase == .swinging || swing != nil
        if swinging, tennis, tvHeading != nil, screenHeading == nil { faceAim=0 }
        if swinging, tennis, let tv=tvHeading, let screen=screenHeading {
            let wing = activeStrokeFacing >= 0 ? 0 : 1
            let angle=SportsMotionGeometry.faceAngle(screenHeading:screen,tvHeading:tv,facing:activeStrokeFacing)
            faceAim=SportsMotionGeometry.aim(faceAngle:angle,neutral:faceNeutral[wing])
            if swing != nil {
                // "Straight" is learned once per stroke, and only from nearly-straight ones.
                faceNeutral[wing]=SportsMotionGeometry.learnNeutral(faceNeutral[wing],faceAngle:angle)
                SportsDiagnostics.write(String(format:"face aim=%.2f angle=%.1f neutral=%.1f wing=%d",faceAim,angle,faceNeutral[wing],wing))
            }
        }
        if let swing { NSLog("[SportsMotion] tennis stroke power=%.2f",swing) }
        recordTrace(time:time,rate:speed,force:force,swing:swing)
        let valid = tennis ? calibrated : current != .lost && calibrated
        // Straight to Unity, from this queue: no hop through the main thread.
        if live {
            if let swing { lastPower=swing; swings+=1 }
            var flags:Int32 = valid ? Int32(SportsSampleValid) : 0
            if current != .good { flags |= Int32(SportsSampleDegraded) }
            let handSide = tracked ? Float(x-neutralX-filter.target*filter.travel) : 0
            let lift = tracked ? Float(position.y-neutralY) : 0
            SportsRuntime.shared().push(SportsSample(version:Int32(SportsSampleVersion),session:token,time:time,
                target:Float(filter.target),power:Float(lastPower),aim:tennis ? Float(faceAim) : 0,
                swing:Int32(swings),swingStart:Int32(filter.onsets),swingAbort:Int32(filter.aborts),flags:flags,
                handSide:handSide,lift:lift,strokeFacing:tennis ? Float(activeStrokeFacing) : 0,
                qx:Float(q.x),qy:Float(q.y),qz:Float(q.z),qw:Float(q.w),rx:Float(rate.x),ry:Float(rate.y),rz:Float(rate.z),
                gx:Float(g.x),gy:Float(g.y),gz:Float(g.z)))
        }
        // The screens only need the gist, and not every hundredth of a second.
        let next = SportsMotionStatus(target:filter.target,phase:filter.phase.rawValue,quality:current,valid:valid)
        if next.phase != status.phase || next.quality != status.quality || next.valid != status.valid || time>=nextStatusAt {
            status=next; nextStatusAt=time+0.1
            publish(next)
        }
    }

    /// Log the 0.8s of rate and force around each stroke candidate, with how it ended.
    private func recordTrace(time:Double,rate:Double,force:Double,swing:Double?) {
        guard tennis else { return }
        trace[traceHead]=(time,rate,force); traceHead=(traceHead+1)%trace.count
        if filter.onsets>traceOnsets { traceOnsets=filter.onsets; traceDue=time+0.4; traceOutcome="aborted" }
        if let swing { traceOutcome=String(format:"confirmed power=%.2f",swing) }
        guard time>=traceDue else { return }
        traceDue = .infinity
        var line="swingtrace outcome=\(traceOutcome) onsets=\(filter.onsets) aborts=\(filter.aborts) t,rate,force:"
        for i in 0..<trace.count {
            let (t,r,f)=trace[(traceHead+i)%trace.count]
            if t>0 { line+=String(format:" %.3f,%.1f,%.2f",t,r,f) }
        }
        SportsDiagnostics.write(line)
    }

    /// Heading of the phone's back (the lens) right now: at axis lock it points at the TV.
    private func captureTVHeading() {
        guard let attitude else { tvHeading=nil; return }
        tvHeading=SportsMotionGeometry.heading(SportsMotionGeometry.rotate(SIMD3<Double>(0,0,-1),by:attitude))
        faceNeutral=[0,0]; faceAim=0
    }

    /// Median LiDAR depth over the middle of the frame: the distance to whatever the lens is
    /// aimed at, i.e. the TV during axis capture. nil without a depth sensor or a reading.
    private static func centreDepth(_ frame: ARFrame) -> Double? {
        guard let map=frame.sceneDepth?.depthMap else { return nil }
        CVPixelBufferLockBaseAddress(map,.readOnly); defer { CVPixelBufferUnlockBaseAddress(map,.readOnly) }
        guard let base=CVPixelBufferGetBaseAddress(map) else { return nil }
        let w=CVPixelBufferGetWidth(map), h=CVPixelBufferGetHeight(map), row=CVPixelBufferGetBytesPerRow(map)
        var values=[Float]()
        for y in (h/2-4)...(h/2+4) {
            let line=base.advanced(by:y*row).assumingMemoryBound(to:Float32.self)
            for x in (w/2-4)...(w/2+4) { let d=line[x]; if d.isFinite && d>0.2 { values.append(d) } }
        }
        guard values.count>10 else { return nil }
        values.sort()
        return Double(values[values.count/2])
    }

    private func currentQuality(at time: Double) -> SportsTrackingQuality {
        guard tennis else { return .good }
        if interrupted { return .lost }
        let frameAge=time-frameAt
        if frameAge>Self.degradedGrace || time-reliableAt>Self.degradedGrace { return .lost }
        if trackingReason.contains("notAvailable") { return .lost }
        if frameAge<0.25 && time-reliableAt<0.25 { return .good }
        return .degraded
    }

    @discardableResult func calibrate() -> Bool {
        let problem: String? = queue.sync {
            guard motion.isDeviceMotionActive else { return "Motion is starting. Tap Ready again in a moment, or choose touch controls." }
            guard !tennis || gate.locked else { return "Aim the back of the phone at the TV to set the court direction first." }
            guard !tennis || currentQuality(at:SportsRuntime.shared().clock()) == .good else { return "Phone position is not available yet. Keep the rear camera uncovered in a well-lit room, then tap Ready." }
            let x=Double(simd_dot(position,right))*sign
            filter.calibrate(position:x,time:SportsRuntime.shared().clock())
            neutralX=x; neutralY=position.y
            calibrated=true
            SportsDiagnostics.write("Ready captured tennis=\(tennis) axis=\(right) sign=\(sign) target=\(filter.target)")
            return nil
        }
        if let problem { report(problem); return false }
        return true
    }

    /// Stop steering but keep the world map, so the locked axis survives menus and pauses.
    func suspend() { queue.async { self.motion.stopDeviceMotionUpdates(); self.calibrated=false } }

    func stop() {
        queue.async {
            self.ar.pause(); self.motion.stopDeviceMotionUpdates()
            self.interrupted=false; self.calibrated=false; self.live=false; self.probing=false
            self.worldRunning=false; self.capturingAxis=false; self.gate=SportsAxisGate()
            self.frameAt = -Double.infinity; self.reliableAt = -Double.infinity
        }
    }

    // MARK: - ARSessionDelegate (on `queue`)

    func session(_ session: ARSession, didUpdate frame: ARFrame) {
        position=SIMD3(frame.camera.transform.columns.3.x,frame.camera.transform.columns.3.y,frame.camera.transform.columns.3.z)
        let out=frame.camera.transform.columns.2
        screenNormal=SIMD3<Float>(out.x,out.y,out.z)
        trackingReason=String(describing:frame.camera.trackingState)
        interrupted=false
        frameAt=SportsRuntime.shared().clock()
        if case .normal = frame.camera.trackingState { reliableAt=frameAt }
        if probing, let luma=Self.centreLuma(frame.capturedImage) { probe.add(time:frame.timestamp,luma:luma) }
        if capturingAxis {
            evaluateGate(frame:frame,time:frameAt)
            // The viewfinder only matters while aiming at the TV.
            if frameAt>=nextPreview {
                nextPreview=frameAt+0.08
                let image=SportsPixelBuffer(buffer:frame.capturedImage)
                DispatchQueue.main.async { MainActor.assumeIsolated { self.preview?.show(image.buffer) } }
            }
        }
    }
    func session(_ session: ARSession, didFailWithError error: Error) { interrupted=true; report(error.localizedDescription) }
    func sessionWasInterrupted(_ session: ARSession) {
        interrupted=true
        SportsDiagnostics.write("ar interrupted")
    }
    /// Resume into the *existing* map. A reset here would silently invalidate the locked
    /// court axis and send the player sideways for the rest of the session.
    func sessionInterruptionEnded(_ session: ARSession) {
        SportsDiagnostics.write("ar interruption ended; relocalizing into existing map")
        runWorldTracking(reset: false)
    }
}

/// A camera image handed to the main thread for the viewfinder (read-only there).
private struct SportsPixelBuffer: @unchecked Sendable { let buffer: CVPixelBuffer }

/// Measuring how far the TV's picture lags the game, with the phone's own camera.
///
/// During setup the phone is aimed at the TV. The game blanks the TV black, then white, and
/// reports (on the shared clock) when it set each white frame. ARKit timestamps every camera
/// frame on that same clock, so the delay is simply when the camera saw the TV turn bright,
/// minus when the game drew it: AirPlay, the Apple TV and the TV's own processing, all in.
enum SportsDelayProbe {
    struct Samples {
        private(set) var times = [Double]()
        private(set) var values = [Double]()
        mutating func add(time: Double, luma: Double) {
            times.append(time); values.append(luma)
            if times.count > 600 { times.removeFirst(100); values.removeFirst(100) }
        }
        var series: [(Double, Double)] { Array(zip(times, values)) }
    }

    /// Plausible delays: anything outside this is a misread, not a TV.
    static let shortest = 0.01, longest = 0.55
    /// The flash must move the camera's reading by at least this much (0-255 luma).
    static let minimumContrast = 22.0

    /// Delay for one flash: in the window around it, find the darkest reading (the black
    /// frame on screen), the brightest after it (the white), and when the brightness first
    /// crossed halfway between them, interpolated between camera frames.
    static func delay(_ series: [(Double, Double)], rendered: Double) -> Double? {
        let window = series.filter { $0.0 >= rendered - 0.25 && $0.0 <= rendered + longest + 0.05 }
        guard window.count >= 6, let darkIndex = window.indices.min(by: { window[$0].1 < window[$1].1 }) else { return nil }
        let after = window[darkIndex...]
        guard let bright = after.map(\.1).max() else { return nil }
        let dark = window[darkIndex].1
        guard bright - dark >= minimumContrast else { return nil }
        let half = (dark + bright) / 2
        guard let cross = after.indices.first(where: { window[$0].1 >= half }), cross > darkIndex else { return nil }
        let (t1, v1) = window[cross - 1], (t2, v2) = window[cross]
        let t = v2 > v1 ? t1 + (t2 - t1) * (half - v1) / (v2 - v1) : t2
        let delay = t - rendered
        return delay >= shortest && delay <= longest ? delay : nil
    }

    /// Median of the flashes that were read, once at least two agree.
    static func combine(_ delays: [Double]) -> Double? {
        guard delays.count >= 2 else { return nil }
        let sorted = delays.sorted()
        return sorted[sorted.count / 2]
    }
}
