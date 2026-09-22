import SwiftUI

@MainActor @Observable
final class SportsSession {
    static let shared = SportsSession()
    var players = PlayerRosterStore.load()
    var playerIndex = 0
    var sport = "golf"
    var touch = false
    var travel = 0.85
    var sound = (UserDefaults.standard.object(forKey:"range.soundEnabled") as? Bool) ?? true {
        didSet { UserDefaults.standard.set(sound,forKey:"range.soundEnabled") }
    }
    /// Render at 120fps on ProMotion phones. Off by default: 60 is the guaranteed target,
    /// 120 is smoother where the phone can hold it.
    var highFrameRate = (UserDefaults.standard.object(forKey:"sports.highFrameRate") as? Bool) ?? false {
        didSet { UserDefaults.standard.set(highFrameRate,forKey:"sports.highFrameRate") }
    }
    /// Opponent strength for tennis: 0 relaxed, 0.45 standard, 0.8 tough.
    var tennisDifficulty = (UserDefaults.standard.object(forKey:"sports.tennisDifficulty") as? Double) ?? 0.45 {
        didSet { UserDefaults.standard.set(tennisDifficulty,forKey:"sports.tennisDifficulty") }
    }
    /// Set by the `-benchTennis` launch argument: Unity plays itself and logs frame times.
    static let benchmark = ProcessInfo.processInfo.arguments.contains("-benchTennis")
    var haptics = (UserDefaults.standard.object(forKey:"arcade.hapticsEnabled") as? Bool) ?? true {
        didSet { UserDefaults.standard.set(haptics,forKey:"arcade.hapticsEnabled") }
    }
    var status = "Connect an external display, or use on-phone preview."
    var feedback = ""
    var stamina = 1.0
    var phase = "calibrating"
    var tracking: SportsTrackingQuality = .lost
    var trackingWarning = ""
    var axisGate = SportsAxisGate()
    var active = false
    var tennisControllerActive = false
    var paused = true
    var ready = false
    var displayConnected = false
    private var sessionID = ""
    /// Numeric stand-in for the session id on the binary sample channel.
    private var sessionToken: Int32 = 0
    private var pending: [String:Any]?
    private var timer: Timer?
    private var swingSequence = 0
    private var target = 0.0
    private var power = 0.0
    private var aim = 0.0
    private var rawMotion: [String:Double] = [:]
    private var nextDiagnostic=0.0
    let motion = SportsMotion()
    init() {
        if players.isEmpty { players=[Player(name:"Player 1",colorIndex:0)] }
        motion.onProblem = { [weak self] message in self?.pause(reason:message); self?.status=message }
        motion.onGate = { [weak self] gate in
            guard let self else { return }
            self.axisGate=gate
            if gate.locked {
                self.motion.start(tennis:true,travel:self.travel)
                self.status="Court direction locked. Stand at your center, then tap Ready."
            }
        }
        motion.onSample = { [weak self] target,swing,phase,valid,quality,raw in
            guard let self, self.active, !self.touch else { return }
            self.phase=phase; self.target=target
            self.rawMotion=raw
            self.tracking=quality
            if let swing, !self.paused { self.power=swing; self.swingSequence+=1 }
            // A blurred frame mid-swing warns and holds position; only a real outage pauses.
            switch quality {
            case .good: self.trackingWarning=""
            case .degraded: self.trackingWarning="Tracking degraded — keep the lens clear"
            case .lost:
                self.trackingWarning="Tracking lost"
                if self.ready && !self.paused {
                    self.status="Tracking lost — play paused. Keep the camera uncovered, then tap Ready or select touch controls."
                    self.pause(reason:self.status)
                }
            }
            // Keep feeding Unity through a degraded patch, otherwise its 0.5s input
            // watchdog pauses the game for exactly the blip we are trying to ride out.
            if !self.paused { self.sendInput(valid:quality != .lost && valid) }
        }
    }
    func savePlayers() { PlayerRosterStore.save(players) }
    func start(preview: Bool = false) {
        guard !active else { return }
        SportsDisplays.shared.refresh()
        guard !preview || !displayConnected else {
            status="TV connected. Choose Play on external display to keep this phone as your controller."; return
        }
        NSLog("[SportsSession] launch sport=%@ mode=%@ touch=%d",sport,preview ? "phone-preview" : "external-controller",touch ? 1 : 0)
        guard let window=SportsDisplays.shared.gameWindow(preview:preview) else {
            status="No independent external display is available. Connect AirPlay/wired display or choose preview."; return
        }
        savePlayers(); sessionID=UUID().uuidString; sessionToken=Int32.random(in:1...Int32.max); ready=false; paused=true; active=true; tennisControllerActive=false
        swingSequence=0; target=0; power=0; aim=0; rawMotion=[:]
        phase="calibrating"; feedback=""; stamina=1
        let p=players[min(playerIndex,players.count-1)]
        pending=["version":1,"session":sessionID,"action":"start","sport":sport,"playerID":p.id.uuidString,"playerName":p.name,"female":p.standardFemale,"skin":p.standardSkin,"left":p.handedness == .left,"sound":sound,"haptics":haptics,"touch":touch || preview,"token":Int(sessionToken),"fps":highFrameRate ? 120 : 60,"bench":SportsSession.benchmark,"difficulty":tennisDifficulty]
        if preview { touch=true }
        pending?["external"] = !preview
        do { try SportsRuntime.shared().load(in:window) }
        catch { status=error.localizedDescription; active=false; pending=nil; SportsDisplays.shared.endPreview(); return }
        SportsDisplays.shared.restorePhoneControls()
        SportsRuntime.shared().pause(false)
        status="Loading Unity…"
        timer?.invalidate()
        timer=Timer.scheduledTimer(withTimeInterval:0.05,repeats:true) { [weak self] _ in
            MainActor.assumeIsolated { self?.poll() }
        }
        // A persistent runtime is already ready after the first launch.
        sendPending()
        let launchingSession=sessionID
        Task { @MainActor [weak self] in
            try? await Task.sleep(for:.seconds(20))
                guard let self, self.sessionID == launchingSession, self.active, !self.ready, self.pending != nil else { return }
            self.status="Unity did not finish loading. Return to menu and retry."; self.pause()
        }
    }
    private func sendPending() {
        guard let pending else { return }
        sendJSON(pending)
    }
    private func sendJSON(_ object:[String:Any]) {
        guard let data=try? JSONSerialization.data(withJSONObject:object),let text=String(data:data,encoding:.utf8) else { return }
        SportsRuntime.shared().send(text)
    }
    func command(_ action:String, value:Double=0) {
        guard active else { return }
        NSLog("[SportsSession] command=%@ value=%.2f",action,value)
        sendJSON(["version":1,"session":sessionID,"action":action,"value":value])
    }
    func pause(reason:String="Paused on phone — tap Ready to continue") {
        guard active else { return }
        SportsDiagnostics.write("pause reason=\(reason) touch=\(touch) phase=\(phase)")
        sendJSON(["version":1,"session":sessionID,"action":"pause","reason":reason]); paused=true; status=reason
    }
    func resume() {
        guard ready, touch || phase == "steering" else { status="Tap Ready to set your center and play."; return }
        command("resume"); paused=false; status="Playing"
        if sport == "tennis" { tennisControllerActive=true }
        SportsDiagnostics.write("resume touch=\(touch) phase=\(phase) target=\(target)")
    }
    func readyToPlay() {
        guard ready else { return }
        if !touch {
            guard sport != "tennis" || motion.axisLocked else { status="Aim the back of the phone at the TV to set the court direction first."; return }
            guard motion.calibrate() else { return }
            phase="steering"
            command("recalibrate")
        }
        resume()
    }
    func useTouch() { pause(); touch=true; trackingWarning=""; motion.stop(); command("touch"); status="Touch controls selected. Tap Ready to play." }
    func useMotion() {
        pause(); touch=false; phase="calibrating"; rawMotion=[:]; trackingWarning=""; command("motion")
        if sport == "tennis" && !motion.axisLocked { beginAxisCapture(); return }
        motion.start(tennis:sport == "tennis",travel:travel)
        status="Motion controls selected. Stand at your center, then tap Ready." 
    }
    /// Locking the court direction is a separate, gated step. Once it is done the phone can
    /// be held at any angle — which is the whole point, since forehands and backhands flip it.
    func beginAxisCapture() {
        axisGate=SportsAxisGate()
        status="Point the back of your phone at the TV and hold still."
        motion.beginAxisCapture(travel:travel)
    }
    /// Escape hatch when the gate will not settle, so motion tennis is never unreachable.
    func useCurrentDirection() {
        guard motion.forceAxisFromCurrentPose() else { return }
        motion.start(tennis:true,travel:travel)
        status="Court direction set. Stand at your center, then tap Ready. Use flip if left/right are swapped."
    }
    /// One tap to mirror steering, for when left and right come out swapped.
    func flipSteering() {
        motion.courtSign = -motion.courtSign
        status=motion.courtSign<0 ? "Steering flipped. Tap Ready to recenter." : "Steering restored. Tap Ready to recenter."
    }
    func steer(_ value:Double) { target=value; if !paused { sendInput(valid:true) } }
    func setAim(_ value:Double) { aim=value; command("aim",value:value) }
    func swing(_ value:Double) { guard !paused else { return }; power=value; swingSequence+=1; NSLog("[SportsSession] touch swing=%d power=%.2f",swingSequence,value); sendInput(valid:true) }
    private func sendInput(valid:Bool) {
        func raw(_ key:String) -> Float { touch ? 0 : Float(rawMotion[key] ?? 0) }
        var flags:Int32 = valid ? Int32(SportsSampleValid) : 0
        if !touch && tracking == .degraded { flags |= Int32(SportsSampleDegraded) }
        let sample=SportsSample(version:Int32(SportsSampleVersion),session:sessionToken,time:SportsRuntime.shared().clock(),
            target:Float(target),power:Float(power),aim:Float(aim),
            swing:Int32(swingSequence),swingStart:Int32(raw("swingStart")),swingAbort:Int32(raw("swingAbort")),flags:flags,
            handSide:raw("handSide"),lift:raw("lift"),strokeFacing:raw("strokeFacing"),
            qx:raw("qx"),qy:raw("qy"),qz:raw("qz"),qw:raw("qw"),rx:raw("rx"),ry:raw("ry"),rz:raw("rz"),gx:raw("gx"),gy:raw("gy"),gz:raw("gz"))
        SportsRuntime.shared().push(sample)
    }
    func end() {
        if active && ready {
            var history=UserDefaults.standard.array(forKey:"sports.sessions.v1") as? [[String:Any]] ?? []
            history.append(["session":sessionID,"sport":sport,"playerID":players[playerIndex].id.uuidString,"endedAt":Date().timeIntervalSince1970,"summary":feedback])
            UserDefaults.standard.set(Array(history.suffix(100)),forKey:"sports.sessions.v1")
        }
        command("end"); motion.stop(); timer?.invalidate(); timer=nil; pending=nil
        SportsRuntime.shared().pause(true); active=false; ready=false; paused=true; tennisControllerActive=false
        SportsDisplays.shared.endPreview(); status="Choose a sport."
        SportsDisplays.shared.showWaiting("Choose a sport on your iPhone")
    }
    private func poll() {
        if touch && active && !paused { sendInput(valid:true) }
        for _ in 0..<64 {
            guard let json=SportsRuntime.shared().pollEvent(),let data=json.data(using:.utf8),
                  let event=(try? JSONSerialization.jsonObject(with:data)) as? [String:Any] else { break }
            if event["type"] as? String != "feedback" { NSLog("[SportsSession] %@",json) }
            if event["type"] as? String == "boot" { sendPending(); continue }
            guard event["session"] as? String == sessionID else { continue }
            switch event["type"] as? String {
            case "ready":
                pending=nil; ready=true
                if UserDefaults.standard.bool(forKey:"sports.resetCoaching") {
                    UserDefaults.standard.set(false,forKey:"sports.resetCoaching"); command("coaching")
                }
                if displayConnected { SportsDisplays.shared.external?.isHidden=true }
                if touch { status="Tap Ready to play."; if SportsSession.benchmark { readyToPlay() } }
                else if sport == "tennis" && !motion.axisLocked { beginAxisCapture() }
                else { motion.start(tennis:sport == "tennis",travel:travel); status="Stand at your center, then tap Ready." }
            case "feedback":
                feedback=event["message"] as? String ?? ""; stamina=event["stamina"] as? Double ?? 1
                if SportsRuntime.shared().clock()>=nextDiagnostic {
                    nextDiagnostic=SportsRuntime.shared().clock()+1
                    SportsDiagnostics.write("bridge touch=\(touch) nativePaused=\(paused) phase=\(phase) target=\(target) unityFrame=\(event["frame"] ?? "unknown") unityPaused=\(event["paused"] ?? "unknown") playerX=\(event["playerX"] ?? "unknown") inputAge=\(event["inputAge"] ?? "unknown")")
                }
            case "displayReady":
                if displayConnected { SportsDisplays.shared.external?.isHidden=true }
                status="Display reconnected. Tap Ready to set your center and play."
            case "perf": SportsDiagnostics.write("perf \(event["message"] as? String ?? "")")
            case "error": pending=nil; status=event["message"] as? String ?? "Unity error"; pause(reason:status)
            default: break
            }
        }
    }
}
