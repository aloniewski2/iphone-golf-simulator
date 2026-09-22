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
enum SportsTrackingQuality: String { case good, degraded, lost }

/// Live state of the aim-at-the-TV gate that locks the court axis before play.
struct SportsAxisGate {
    var locked = false
    var progress = 0.0          // 0...1 once every condition holds
    var message = "Point the back of your phone at the TV."
    /// Live sensor readout, so a gate that refuses to settle can be diagnosed on the spot.
    var detail = ""
    /// Seconds the gate has been failing, used to offer a manual override.
    var stalledFor = 0.0
}

@MainActor
final class SportsMotion: NSObject, @preconcurrency ARSessionDelegate {
    /// The court axis only survives while ARKit keeps the same world origin. Everything here
    /// is built so `resetTracking` happens exactly once, at axis capture.
    private static let signKey="sports.tennis.steerSign"
    private static let holdRequired=1.0
    private static let maxLensPitch=35.0*Double.pi/180
    private static let maxGateRotation=0.5
    private static let degradedGrace=2.0

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
    private var nextDiagnostic=0.0, nextPreview=0.0, trackingReason="not started"
    private var worldRunning=false
    private var capturingAxis=false, gateHeldSince = -Double.infinity, lastRotation=0.0, captureStartedAt=0.0
    private(set) var gate = SportsAxisGate()
    /// Recent rate/force history, written out around each stroke candidate so the detection
    /// thresholds can be tuned from real swings instead of reasoning. Fixed size, no growth.
    private var trace = [(Double,Double,Double)](repeating:(0,0,0),count:80)
    private var traceHead = 0, traceOnsets = 0, traceDue = Double.infinity
    private var traceOutcome = ""
    /// Flips left/right without recapturing the axis, for when the mapping comes out mirrored.
    var courtSign: Double {
        get { UserDefaults.standard.object(forKey:Self.signKey) as? Double ?? 1 }
        set { UserDefaults.standard.set(newValue,forKey:Self.signKey) }
    }
    var axisLocked: Bool { gate.locked }
    /// Exposed so the aiming screen can show a viewfinder from this same session.
    var previewSession: ARSession { ar }
    weak var preview: SportsPreviewView?
    var onSample: ((Double, Double?, String, Bool, SportsTrackingQuality, [String:Double]) -> Void)?
    var onProblem: ((String) -> Void)?
    var onGate: ((SportsAxisGate) -> Void)?

    // MARK: - Session lifecycle

    func start(tennis: Bool, travel: Double) {
        suspendFilter(); self.tennis=tennis; filter=SteeringFilter(); filter.travel=travel
        filter.tennisStroke = tennis
        SportsDiagnostics.write("motion start tennis=\(tennis) axisLocked=\(gate.locked) cameraPermission=\(AVCaptureDevice.authorizationStatus(for:.video).rawValue)")
        guard motion.isDeviceMotionAvailable else { onProblem?("Motion sensors unavailable. Select touch controls."); return }
        if tennis {
            guard ARWorldTrackingConfiguration.isSupported else { onProblem?("Physical steering is unsupported. Select touch controls."); return }
            guard AVCaptureDevice.authorizationStatus(for: .video) == .authorized else {
                AVCaptureDevice.requestAccess(for: .video) { [weak self] allowed in
                    Task { @MainActor in
                        if allowed { self?.start(tennis:true,travel:travel) }
                        else { self?.onProblem?("Camera permission denied. Physical steering needs the rear camera; touch remains available.") }
                    }
                }; return
            }
            runWorldTracking(reset: !worldRunning)
        }
        startDeviceMotion()
    }

    /// Reset only when there is no locked axis to protect. Relocalizing into the existing
    /// map is what keeps "right" pointing the same way for the whole session.
    private func runWorldTracking(reset: Bool) {
        let configuration=ARWorldTrackingConfiguration()
        configuration.worldAlignment = .gravity
        configuration.planeDetection = [.horizontal]
        ar.delegate=self; ar.delegateQueue = .main
        ar.run(configuration,options: reset ? [.resetTracking,.removeExistingAnchors] : [])
        worldRunning=true
        if reset { gate.locked=false }
        SportsDiagnostics.write("world tracking run reset=\(reset)")
    }

    private func startDeviceMotion() {
        guard !motion.isDeviceMotionActive else { return }
        motion.deviceMotionUpdateInterval=0.01
        motion.startDeviceMotionUpdates(to:.main) { [weak self] sample,error in
            MainActor.assumeIsolated {
                guard let self else { return }
                if let error { self.onProblem?("Motion sensor error: \(error.localizedDescription)"); return }
                guard let sample else { return }
                self.consume(sample)
            }
        }
    }

    // MARK: - Aim-at-the-TV gate

    /// Begin capturing the court axis. Tennis cannot start until this locks, because a phone
    /// that flips between forehand and backhand can never reveal where the TV is mid-rally.
    func beginAxisCapture(travel: Double) {
        tennis=true; filter=SteeringFilter(); filter.travel=travel; filter.tennisStroke=true
        guard ARWorldTrackingConfiguration.isSupported else { onProblem?("Physical steering is unsupported. Select touch controls."); return }
        guard AVCaptureDevice.authorizationStatus(for: .video) == .authorized else {
            AVCaptureDevice.requestAccess(for: .video) { [weak self] allowed in
                Task { @MainActor in
                    if allowed { self?.beginAxisCapture(travel:travel) }
                    else { self?.onProblem?("Camera permission denied. Physical steering needs the rear camera; touch remains available.") }
                }
            }; return
        }
        capturingAxis=true; gateHeldSince = -Double.infinity; captureStartedAt=SportsRuntime.shared().clock()
        gate=SportsAxisGate(); onGate?(gate)
        runWorldTracking(reset: true)
        startDeviceMotion()
        SportsDiagnostics.write("axis capture begin")
    }

    func cancelAxisCapture() { capturingAxis=false; gateHeldSince = -Double.infinity }

    /// Last resort when the gate will not settle (dark room, awkward pose). Takes the axis
    /// from the current pose regardless of how steady or level it is. Left/right may come
    /// out mirrored, which is what the flip control is for.
    @discardableResult func forceAxisFromCurrentPose() -> Bool {
        guard let frame=ar.currentFrame else { onProblem?("No camera frame yet. Keep the lens uncovered."); return false }
        let back=frame.camera.transform.columns.2
        guard let axis=SportsMotionGeometry.horizontalRight(cameraBack:SIMD3<Float>(back.x,back.y,back.z)) else {
            onProblem?("Aim the back of the phone roughly at the TV, not straight up or down."); return false
        }
        right=axis
        if let forward=SportsMotionGeometry.horizontal(SIMD3<Float>(back.x,back.y,back.z)) { courtForward = -forward }
        strokeFacing = -1
        gate.locked=true; gate.progress=1; gate.message="Court direction set manually."
        capturingAxis=false
        SportsDiagnostics.write("axis forced right=\(right) sign=\(courtSign)")
        onGate?(gate)
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
        if failure == nil && SportsMotionGeometry.horizontalRight(cameraBack:cameraBack) == nil { failure="Aim the back of the phone at the TV, not at the floor or ceiling." }
        guard failure == nil, let axis=SportsMotionGeometry.horizontalRight(cameraBack:cameraBack) else {
            gateHeldSince = -Double.infinity
            gate.progress=0; gate.message=failure ?? gate.message
            gate.detail="Tracking \(trackingReason) · \(Int(pitch*180/Double.pi))° off level"
            gate.stalledFor = capturingAxis ? time-captureStartedAt : 0
            onGate?(gate); return
        }
        if gateHeldSince == -Double.infinity { gateHeldSince=time }
        let held=time-gateHeldSince
        gate.progress=min(1,held/Self.holdRequired)
        gate.message = gate.progress<1 ? "Hold it there…" : "Court direction locked."
        gate.detail="Tracking \(trackingReason) · \(Int(pitch*180/Double.pi))° off level"
        if held >= Self.holdRequired {
            right=axis
            // The lens points at the TV right now, and the screen points away from it.
            if let forward=SportsMotionGeometry.horizontal(cameraBack) { courtForward = -forward }
            strokeFacing = -1
            gate.locked=true; capturingAxis=false
            SportsDiagnostics.write("axis locked right=\(right) forward=\(courtForward) sign=\(courtSign) pitch=\(pitch)")
        }
        onGate?(gate)
    }

    // MARK: - Per-sample steering

    private func consume(_ sample: CMDeviceMotion) {
        let time=SportsRuntime.shared().clock()
        let rate=sample.rotationRate
        let speed=sqrt(rate.x*rate.x+rate.y*rate.y+rate.z*rate.z)
        lastRotation=speed
        guard !capturingAxis else { return }
        let current=currentQuality(at:time)
        // Only `good` advances the fixed real-world→court mapping. `degraded` holds the last
        // court position and keeps the rally alive; `lost` is what finally pauses play.
        let tracked = !tennis || current == .good
        let x=Double(simd_dot(position,right))*courtSign
        let grace = tennis && current != .lost
        let previousPhase=self.filter.phase
        let a=sample.userAcceleration
        let force=sqrt(a.x*a.x+a.y*a.y+a.z*a.z)
        let swing=filter.step(position:x,rate:speed,time:time,valid:tracked,allowSwingWhileUntracked:grace,acceleration:force)
        if time>=nextDiagnostic {
            nextDiagnostic=time+1
            SportsDiagnostics.write("sensor quality=\(current.rawValue) ar=\(trackingReason) frameAge=\(time-frameAt) reliableAge=\(time-reliableAt) ready=\(calibrated) axisLocked=\(gate.locked) phase=\(filter.phase.rawValue) delta=\(x-neutralX) target=\(filter.target) reachL=\(filter.reachLeft) reachR=\(filter.reachRight) seenL=\(filter.seenLeft) seenR=\(filter.seenRight) rate=\(speed) force=\(force) sign=\(courtSign) facing=\(strokeFacing)")
        }
        let q=sample.attitude.quaternion, g=sample.gravity
        if tennis, gate.locked {
            strokeFacing=SportsMotionGeometry.strokeFacing(screenNormal:screenNormal,courtForward:courtForward,previous:strokeFacing)
        }
        if previousPhase != .swinging && filter.phase == .swinging { activeStrokeFacing=strokeFacing }
        if let swing { NSLog("[SportsMotion] tennis stroke power=%.2f",swing) }
        recordTrace(time:time,rate:speed,force:force,swing:swing)
        onSample?(filter.target,swing,filter.phase.rawValue,current != .lost && calibrated,current,
            ["qx":q.x,"qy":q.y,"qz":q.z,"qw":q.w,"rx":rate.x,"ry":rate.y,"rz":rate.z,"gx":g.x,"gy":g.y,"gz":g.z,
             "handSide":tracked ? x-neutralX-filter.target*filter.travel : 0,"lift":tracked ? Double(position.y-neutralY) : 0,"strokeFacing":tennis ? activeStrokeFacing : 0,
             "swingStart":Double(filter.onsets),"swingAbort":Double(filter.aborts)])
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
        guard motion.isDeviceMotionActive else { onProblem?("Motion is starting. Tap Ready again in a moment, or choose touch controls."); return false }
        guard !tennis || gate.locked else { onProblem?("Aim the back of the phone at the TV to set the court direction first."); return false }
        guard !tennis || currentQuality(at:SportsRuntime.shared().clock()) == .good else { onProblem?("Phone position is not available yet. Keep the rear camera uncovered in a well-lit room, then tap Ready."); return false }
        let x=Double(simd_dot(position,right))*courtSign
        filter.calibrate(position:x,time:SportsRuntime.shared().clock())
        neutralX=x; neutralY=position.y
        calibrated=true
        SportsDiagnostics.write("Ready captured tennis=\(tennis) axis=\(right) sign=\(courtSign) target=\(filter.target)")
        return true
    }

    /// Stop steering but keep the world map, so the locked axis survives menus and pauses.
    func suspend() { motion.stopDeviceMotionUpdates(); calibrated=false }
    private func suspendFilter() { calibrated=false }

    func stop() {
        ar.pause(); motion.stopDeviceMotionUpdates()
        interrupted=false; calibrated=false
        worldRunning=false; capturingAxis=false; gate=SportsAxisGate()
        frameAt = -Double.infinity; reliableAt = -Double.infinity
    }

    // MARK: - ARSessionDelegate

    func session(_ session: ARSession, didUpdate frame: ARFrame) {
        position=SIMD3(frame.camera.transform.columns.3.x,frame.camera.transform.columns.3.y,frame.camera.transform.columns.3.z)
        let out=frame.camera.transform.columns.2
        screenNormal=SIMD3<Float>(out.x,out.y,out.z)
        trackingReason=String(describing:frame.camera.trackingState)
        interrupted=false
        frameAt=SportsRuntime.shared().clock()
        if case .normal = frame.camera.trackingState { reliableAt=frameAt }
        if capturingAxis { evaluateGate(frame:frame,time:frameAt) }
        if let preview, frameAt>=nextPreview { nextPreview=frameAt+0.08; preview.show(frame.capturedImage) }
    }
    func session(_ session: ARSession, didFailWithError error: Error) { interrupted=true; onProblem?(error.localizedDescription) }
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
