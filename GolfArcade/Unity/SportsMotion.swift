import ARKit
import CoreMotion

@MainActor
final class SportsMotion: NSObject, @preconcurrency ARSessionDelegate {
    private let ar = ARSession()
    private let motion = CMMotionManager()
    private var filter = SteeringFilter()
    private var position = SIMD3<Float>.zero, right = SIMD3<Float>(1,0,0)
    private var valid = false, frameAt = -Double.infinity
    private var tennis = false, calibrated = false
    var onSample: ((Double, Double?, String, Bool, [String:Double]) -> Void)?
    var onProblem: ((String) -> Void)?
    func start(tennis: Bool, travel: Double) {
        stop(); self.tennis=tennis; filter=SteeringFilter(); filter.travel=travel
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
            ar.delegate=self; ar.delegateQueue = .main
            ar.run(ARWorldTrackingConfiguration(),options:[.resetTracking,.removeExistingAnchors])
        }
        motion.deviceMotionUpdateInterval=0.01
        motion.startDeviceMotionUpdates(to:.main) { [weak self] sample,error in
            MainActor.assumeIsolated {
                guard let self, let sample else { return }
                let time=SportsRuntime.shared().clock()
                let rate=sample.rotationRate
                let speed=sqrt(rate.x*rate.x+rate.y*rate.y+rate.z*rate.z)
                let tracked = !self.tennis || (self.valid && time-self.frameAt<0.25)
                let x=Double(simd_dot(self.position,self.right))
                let swing=self.filter.step(position:x,rate:speed,time:time,valid:tracked)
                let q=sample.attitude.quaternion, g=sample.gravity
                self.onSample?(self.filter.target,swing,self.filter.phase.rawValue,tracked && self.calibrated,
                    ["qx":q.x,"qy":q.y,"qz":q.z,"qw":q.w,"rx":rate.x,"ry":rate.y,"rz":rate.z,"gx":g.x,"gy":g.y,"gz":g.z])
            }
        }
    }
    func calibrate() {
        guard !tennis || valid else { onProblem?("Move the phone slowly in a well-lit room until tracking is ready."); return }
        if let frame=ar.currentFrame {
            let axis=SIMD3<Float>(frame.camera.transform.columns.0.x,0,frame.camera.transform.columns.0.z)
            if simd_length(axis)>0.1 { right=simd_normalize(axis) }
        }
        filter.calibrate(position:Double(simd_dot(position,right)),time:SportsRuntime.shared().clock())
        calibrated=true
    }
    func stop() { ar.pause(); motion.stopDeviceMotionUpdates(); valid=false; calibrated=false }
    func session(_ session: ARSession, didUpdate frame: ARFrame) {
        position=SIMD3(frame.camera.transform.columns.3.x,frame.camera.transform.columns.3.y,frame.camera.transform.columns.3.z)
        if case .normal = frame.camera.trackingState { valid=true } else { valid=false }
        frameAt=SportsRuntime.shared().clock()
    }
    func session(_ session: ARSession, didFailWithError error: Error) { valid=false; onProblem?(error.localizedDescription) }
    func sessionWasInterrupted(_ session: ARSession) { valid=false; onProblem?("Tracking interrupted. Recalibrate before resuming.") }
}
