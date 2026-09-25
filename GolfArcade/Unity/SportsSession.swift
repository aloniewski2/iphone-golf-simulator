import SwiftUI
import AVFoundation

@MainActor @Observable
final class SportsSession {
    static let shared = SportsSession()
    var players = PlayerRosterStore.load()
    var playerIndex = 0
    var sport = "golf"
    var touch = false { didSet { routeSamples() } }
    var travel = 0.85
    var sound = (UserDefaults.standard.object(forKey:"range.soundEnabled") as? Bool) ?? true {
        didSet { UserDefaults.standard.set(sound,forKey:"range.soundEnabled") }
    }
    /// Render at 120fps on ProMotion phones. Off by default: 60 is the guaranteed target,
    /// 120 is smoother where the phone can hold it.
    var highFrameRate = (UserDefaults.standard.object(forKey:"sports.highFrameRate") as? Bool) ?? false {
        didSet { UserDefaults.standard.set(highFrameRate,forKey:"sports.highFrameRate") }
    }
    /// The in-match coaching cards (first serve, first backhand…).
    var coachingTips = (UserDefaults.standard.object(forKey:"sports.coachingTips") as? Bool) ?? true {
        didSet { UserDefaults.standard.set(coachingTips,forKey:"sports.coachingTips") }
    }
    /// TVs that crop the picture's edges: shrink the menus and HUD by this fraction.
    var overscan = UserDefaults.standard.double(forKey:"sports.overscan") {
        didSet { UserDefaults.standard.set(overscan,forKey:"sports.overscan") }
    }
    /// Accessibility: larger text on the phone remote, and still frames instead of moving b-roll.
    var bigText = UserDefaults.standard.bool(forKey:"sports.bigText") {
        didSet { UserDefaults.standard.set(bigText,forKey:"sports.bigText") }
    }
    var reduceMotion = UserDefaults.standard.bool(forKey:"sports.reduceMotion") {
        didSet { UserDefaults.standard.set(reduceMotion,forKey:"sports.reduceMotion") }
    }
    /// The loading screen (TV and phone): at least ten seconds, a flowing bar to 100%.
    let loading = LoadingModel()
    /// The tennis tutorial's current step, from Unity: (index, count, text).
    var tutorialStep: (index: Int, count: Int, text: String)?
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
    var paused = true { didSet { routeSamples() } }
    var ready = false
    var displayConnected = false {
        didSet { if displayConnected != oldValue { TennisMenu.shared.displayChanged(connected: displayConnected) } }
    }
    /// For the racket controller: the scoreboard and where recent hits met the strings.
    var score = TennisScore()
    var contacts: [TennisContact] = []
    var opponentName = ""
    /// What the match is doing, from Unity, for the controller: "serve", "toss", "receive",
    /// "rally" or "point"; and which court a serve goes from.
    var tennisPhase = ""
    var serveFromDeuce = true
    /// The controller serve's aim in the target box: across (T -1 .. wide +1), depth (0..1).
    var serveAim = (across: 0.0, depth: 0.8)
    /// Extra start fields for the tennis front end (mode, opponent, round).
    private var launchExtras: [String:Any] = [:]
    private var sessionID = ""
    /// Numeric stand-in for the session id on the binary sample channel.
    private var sessionToken: Int32 = 0
    private var pending: [String:Any]?
    private var timer: Timer?
    private var swingSequence = 0
    private var target = 0.0
    private var power = 0.0
    private var aim = 0.0
    /// How far the TV's picture lags the game (seconds), measured with the camera during
    /// setup and remembered for next time. 0 = never measured, so no compensation.
    var tvDelay = UserDefaults.standard.double(forKey:"sports.tv.delay.v1")
    var measuringDelay = false
    /// The timing check is running on the TV (swing with the bouncing ball).
    var checkingTiming = false
    /// The timing check is offered, not sprung: the phone explains it and waits for Start.
    var timingPrompt = false
    /// When the TV's get-ready countdown ends (the phone mirrors it).
    var timingCountdownEnds: Date?
    /// What the last timing check found, for the controller.
    var timingNote = ""
    private var checkTimingOnResume = false
    /// The timing check's result for the TV in use (seconds of lag from what the player sees
    /// to the swing registering): the whole chain, picture delay and swing detection alike.
    /// Kept per AirPlay receiver, since each TV has its own delay.
    var timingCalibration: Double? { SportsTiming.stored(for: SportsTiming.currentTV()) }
    /// The lag Unity should start from: the timing check, else the camera's measurement.
    var startingLag: Double { timingCalibration ?? tvDelay }
    /// Shown only when the measured delay is high enough that the TV is probably processing
    /// the picture: the one setting outside the app that helps most.
    var delayTip = ""
    private var flashDelays: [Double] = []
    private static let flashes = 4, gameModeHint = 0.14
    private var nextDiagnostic=0.0
    let motion = SportsMotion()
    init() {
        if players.isEmpty { players=[Player(name:"Player 1",colorIndex:0)] }
        motion.onProblem = { [weak self] message in self?.pause(reason:message); self?.status=message }
        motion.onGate = { [weak self] gate in
            guard let self else { return }
            let justLocked = gate.locked && !self.axisGate.locked
            self.axisGate=gate
            if gate.locked {
                self.motion.start(tennis:true,travel:self.travel)
                self.status="Court direction locked. Stand at your center, then tap Ready."
                // Still aimed at the TV: time its delay now, while it costs the player nothing.
                if justLocked { self.measureTVDelay() }
            }
        }
        // Unity gets every sample straight from the sensor queue; this is only for the screens.
        motion.onStatus = { [weak self] update in
            guard let self, self.active, !self.touch else { return }
            self.phase=update.phase; self.target=update.target
            let quality=update.quality
            self.tracking=quality
            // A blurred frame mid-swing warns and holds position; only a real outage pauses.
            switch quality {
            case .good: self.trackingWarning=""
            case .degraded: self.trackingWarning="Tracking degraded — keep the lens clear"
            case .lost where self.sport == "tennis":
                // Tennis needs the camera only for the lean hint: swings and aim run on the
                // motion sensors, so play goes on and steering simply holds.
                self.trackingWarning="Camera can't see the room — swings still count"
            case .lost:
                self.trackingWarning="Tracking lost"
                if self.ready && !self.paused {
                    self.status="Tracking lost — play paused. Keep the camera uncovered, then tap Ready or select touch controls."
                    self.pause(reason:self.status)
                }
            }
        }
    }
    /// Motion samples flow from the sensor queue straight to Unity while a motion-controlled
    /// session is playing; touch play sends its own from here.
    private func routeSamples() {
        motion.setOutput(token:sessionToken,live:active && !paused && !touch,swingBase:swingSequence)
    }

    /// Time the TV: flash it black-then-white a few times while the camera still looks at it,
    /// and take the median delay. Unity then judges swings against what the player saw.
    func measureTVDelay() {
        // A TV already timed with the swing check needs no camera probe.
        guard active, ready, displayConnected, !touch, sport == "tennis", !measuringDelay, timingCalibration == nil else { return }
        measuringDelay=true; flashDelays=[]
        status="Keep pointing at the TV for a moment — timing its picture…"
        motion.setDelayProbe(true)
        let launched=sessionID
        Task { @MainActor [weak self] in
            // Let ARKit settle into its play configuration first.
            try? await Task.sleep(for:.milliseconds(400))
            guard let self, self.sessionID == launched else { return }
            self.command("flash",value:Double(Self.flashes))
            try? await Task.sleep(for:.seconds(Double(Self.flashes)*0.6+1.5))
            self.finishTVDelay()
        }
    }

    private func flashShown(at rendered: Double) {
        guard measuringDelay else { return }
        // The camera needs the TV's delay (and a little) to see it: read it back after that.
        Task { @MainActor [weak self] in
            try? await Task.sleep(for:.milliseconds(700))
            guard let self, self.measuringDelay else { return }
            if let delay=self.motion.delayAfterFlash(rendered:rendered) { self.flashDelays.append(delay) }
        }
    }

    private func finishTVDelay() {
        guard measuringDelay else { return }
        measuringDelay=false; motion.setDelayProbe(false)
        if let delay=SportsDelayProbe.combine(flashDelays) {
            tvDelay=delay; UserDefaults.standard.set(delay,forKey:"sports.tv.delay.v1")
            status=String(format:"TV delay %.0f ms — your swings are timed to what you see. Stand at your center, then tap Ready.",delay*1000)
        } else {
            status=tvDelay>0 ? "Couldn't re-time the TV; using the last measurement. Stand at your center, then tap Ready."
                             : "Couldn't time the TV's picture; playing without delay compensation. Stand at your center, then tap Ready."
        }
        command("latency",value:tvDelay)
        delayTip=tvDelay>Self.gameModeHint ? "If your TV has Game Mode, turn it on for this input — it cuts the delay you feel." : ""
        SportsDiagnostics.write("tv delay flashes=\(flashDelays.map { String(format:"%.3f",$0) }) chosen=\(tvDelay)")
    }
    func savePlayers() { PlayerRosterStore.save(players) }
    /// Start tennis from the front end: a campaign round against `opponent`, or training.
    func startTennis(opponent: TennisOpponent?, round: Int?, mode: String? = nil, difficulty: Double, coach: [String] = [], preview: Bool) {
        sport = "tennis"
        opponentName = opponent?.name.components(separatedBy: " ").first ?? (mode == "tutorial" ? "Ray" : "Coach")
        launchExtras = ["mode": mode ?? (opponent == nil ? "training" : "campaign"), "opponent": opponent?.key ?? "",
                        "opponentName": opponentName, "round": opponent?.round ?? "", "difficulty": difficulty,
                        "sets": opponent?.sets ?? 1, "games": opponent?.games ?? 3,
                        "coach": coach.joined(separator: "|")]
        start(preview: preview)
        launchExtras = [:]
    }
    /// Golf's Cliffside round, or (tutorial) its practice shot.
    func startGolf(tutorial: Bool, preview: Bool) {
        sport = "golf"; opponentName = ""
        launchExtras = ["mode": tutorial ? "tutorial" : "round"]
        start(preview: preview)
        launchExtras = [:]
    }
    /// Settings → Controls: run the timing check at the start of the next match.
    func forceTimingCheckNextMatch() { checkTimingOnResume = true }
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
        swingSequence=0; target=0; power=0; aim=0; measuringDelay=false; delayTip=""; checkingTiming=false; timingPrompt=false; timingNote=""
        phase="calibrating"; feedback=""; stamina=1
        let p=players[min(playerIndex,players.count-1)]
        pending=["version":1,"session":sessionID,"action":"start","sport":sport,"playerID":p.id.uuidString,"playerName":p.name,"female":p.standardFemale,"skin":p.standardSkin,"left":p.handedness == .left,"sound":sound,"haptics":haptics,"touch":touch || preview,"token":Int(sessionToken),"fps":highFrameRate ? 120 : 60,"bench":SportsSession.benchmark,"difficulty":tennisDifficulty,
                 "shirt":Outfit.hex(p.shirt),"shorts":Outfit.hex(p.shorts),"accent":Outfit.hex(p.accent),"racket":Outfit.hex(p.racket),
                 "tips":coachingTips,"overscan":overscan]
        if preview { touch=true }
        pending?["external"] = !preview
        for (key, value) in launchExtras { pending?[key] = value }
        score = TennisScore(); contacts = []; tutorialStep = nil
        loading.begin(now: Date())
        loading.onFinish = { [weak self] in self?.loadingFinished() }
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
    // MARK: Controller serve

    /// TOSS pressed. The game judges it against the toss meter under the player's feet on the
    /// TV, as the player saw it then (value -1: "read your meter").
    func toss() {
        guard active, !paused else { return }
        sendJSON(["version":1,"session":sessionID,"action":"toss","value":-1])
    }
    func setServeAim(across: Double, depth: Double) {
        serveAim=(max(-1,min(1,across)),max(0,min(1,depth)))
        guard active else { return }
        sendJSON(["version":1,"session":sessionID,"action":"serveAim","value":serveAim.across,"value2":serveAim.depth])
    }
    /// Walk along the baseline before a serve: -1 left, 0 stop, 1 right (held buttons).
    func nudge(_ direction: Double) { command("nudge",value:direction) }

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
            guard !measuringDelay else { status="Keep pointing at the TV for a moment — timing its picture…"; return }
            guard motion.calibrate() else { return }
            phase="steering"
            command("recalibrate")
        }
        resume()
        // First time on this TV (or asked for): the timing check, before the first point.
        if !paused && sport == "tennis" && displayConnected && (checkTimingOnResume || timingCalibration == nil) {
            // Asked for explicitly (Options → re-check): straight in. First time on this TV:
            // offer it, with an explanation and a Start button.
            if checkTimingOnResume { checkTimingOnResume=false; startTimingCheck() } else { timingPrompt=true }
        }
    }
    /// Swing along with a ball bouncing on the TV for a few seconds; Unity measures how far
    /// behind the picture the swings land and times every swing to what the player sees.
    func startTimingCheck() {
        guard active, !paused, sport == "tennis" else { return }
        timingPrompt=false; checkingTiming=true; timingNote=""
        timingCountdownEnds=Date().addingTimeInterval(5)
        command("timingCheck")
    }
    /// Options → re-check: runs on resuming if the match is paused.
    func recheckTiming() {
        if paused { checkTimingOnResume=true; readyToPlay() } else { startTimingCheck() }
    }
    func useTouch() {
        pause(); swingSequence=max(swingSequence,motion.swingCount); touch=true; trackingWarning=""; measuringDelay=false
        motion.stop(); command("touch"); status="Touch controls selected. Tap Ready to play."
    }
    func useMotion() {
        pause(); touch=false; phase="calibrating"; trackingWarning=""; command("motion")
        if sport == "tennis" && !motion.axisLocked { beginAxisCapture(); return }
        motion.start(tennis:sport == "tennis",travel:travel)
        status="Motion controls selected. Stand at your center, then tap Ready." 
    }
    /// Locking the court direction is a separate, gated step. Once it is done the phone can
    /// be held at any angle — which is the whole point, since forehands and backhands flip it.
    func beginAxisCapture() {
        axisGate=SportsAxisGate()
        status="Stand about 2.5 m back, point the back of your phone at the TV and hold still."
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
        // Touch play only: motion samples go to Unity from SportsMotion's own queue.
        guard touch else { return }
        let flags:Int32 = valid ? Int32(SportsSampleValid) : 0
        let sample=SportsSample(version:Int32(SportsSampleVersion),session:sessionToken,time:SportsRuntime.shared().clock(),
            target:Float(target),power:Float(power),aim:Float(aim),
            swing:Int32(swingSequence),swingStart:0,swingAbort:0,flags:flags,
            handSide:0,lift:0,strokeFacing:0,qx:0,qy:0,qz:0,qw:0,rx:0,ry:0,rz:0,gx:0,gy:0,gz:0)
        SportsRuntime.shared().push(sample)
    }
    /// The loading screen has run its course: show the game, and start the setup that the
    /// player sees (court direction, motion) only now.
    private func loadingFinished() {
        guard active, ready else { return }
        if displayConnected { SportsDisplays.shared.external?.isHidden=true }
        // Compensate from the first point with the last measurement of this TV; the
        // setup re-times it when the camera is aimed at it.
        if displayConnected && startingLag>0 { command("latency",value:startingLag) }
        if touch { status="Tap Ready to play."; if SportsSession.benchmark { readyToPlay() } }
        else if sport == "tennis" && !motion.axisLocked { beginAxisCapture() }
        else { motion.start(tennis:sport == "tennis",travel:travel); status="Stand at your center, then tap Ready." }
    }
    func end() {
        if active && ready {
            var history=UserDefaults.standard.array(forKey:"sports.sessions.v1") as? [[String:Any]] ?? []
            history.append(["session":sessionID,"sport":sport,"playerID":players[playerIndex].id.uuidString,"endedAt":Date().timeIntervalSince1970,"summary":feedback])
            UserDefaults.standard.set(Array(history.suffix(100)),forKey:"sports.sessions.v1")
        }
        command("end"); motion.stop(); timer?.invalidate(); timer=nil; pending=nil; measuringDelay=false; checkingTiming=false
        loading.cancel(); tutorialStep=nil
        SportsRuntime.shared().pause(true); active=false; ready=false; paused=true; tennisControllerActive=false
        SportsDisplays.shared.endPreview(); status="Choose a sport."
        SportsDisplays.shared.showMenu()
        TennisMenu.shared.sessionEnded()
    }
    private func poll() {
        if touch && active && !paused { sendInput(valid:true) }
        loading.tick(now: Date())
        for _ in 0..<64 {
            guard let json=SportsRuntime.shared().pollEvent(),let data=json.data(using:.utf8),
                  let event=(try? JSONSerialization.jsonObject(with:data)) as? [String:Any] else { break }
            if event["type"] as? String != "feedback" { NSLog("[SportsSession] %@",json) }
            if event["type"] as? String == "boot" { loading.reach(0.2); sendPending(); continue }
            guard event["session"] as? String == sessionID else { continue }
            switch event["type"] as? String {
            case "ready":
                pending=nil; ready=true
                if UserDefaults.standard.bool(forKey:"sports.resetCoaching") {
                    UserDefaults.standard.set(false,forKey:"sports.resetCoaching"); command("coaching")
                }
                // The loading screen stays up until it has run its course (loadingFinished).
                loading.markReady()
                if SportsSession.benchmark { loading.skip() }
            case "loadProgress":
                // Unity's scene load, 0...1, fills 20% → 80% of the bar.
                if let p=Double(event["message"] as? String ?? "") { loading.reach(0.2+0.6*p) }
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
            case "score": score = TennisScore(line: event["message"] as? String ?? "")
            case "timing":
                checkingTiming=false
                let message=event["message"] as? String ?? "failed"
                if let ms=Double(message) {
                    SportsTiming.store(ms/1000, for: SportsTiming.currentTV())
                    timingNote=String(format:"Timing set — %.0f ms. Swings now land when you see them.",ms)
                } else {
                    timingNote="No steady rhythm found — the game will learn your timing as you play."
                }
                SportsDiagnostics.write("timing check tv=\(SportsTiming.currentTV()) result=\(message)")
            case "contact":
                if let contact = TennisContact(line: event["message"] as? String ?? "") { contacts = Array((contacts + [contact]).suffix(12)) }
            case "flash": if let rendered=Double(event["message"] as? String ?? "") { flashShown(at:rendered) }
            case "phase":
                let parts=(event["message"] as? String ?? "").split(separator:"|").map(String.init)
                let phase=parts.first ?? ""
                if phase != tennisPhase {
                    // A held move button must not carry on into the rally.
                    if phase != "serve" && phase != "receive" { nudge(0) }
                    // A new serve starts from the aim the player last chose.
                    if phase == "serve" { setServeAim(across:serveAim.across,depth:serveAim.depth) }
                }
                tennisPhase=phase
                if parts.count>1 { serveFromDeuce=parts[1] == "deuce" }
            case "tutorialStep":
                let parts=(event["message"] as? String ?? "").split(separator:"|",maxSplits:2).map(String.init)
                if parts.count == 3, let i=Int(parts[0]), let n=Int(parts[1]) { tutorialStep=(i,n,parts[2]) }
            case "tutorialDone", "shot":
                if event["type"] as? String == "tutorialDone" || TennisMenu.shared.launch?.mode == .tutorial { TennisMenu.shared.tutorialFinished() }
            case "rally": if let n=Int(event["message"] as? String ?? "") { SportProgress.shared.recordRally(n) }
            case "matchOver":
                let parts = (event["message"] as? String ?? "").split(separator: "|")
                TennisMenu.shared.matchFinished(won: parts.first == "won", score: parts.count > 1 ? String(parts[1]) : "")
            case "error": pending=nil; status=event["message"] as? String ?? "Unity error"; pause(reason:status)
            default: break
            }
        }
    }
}


/// Games and the point score, from Unity's "score" event ("playerGames,opponentGames,scoreboard").
struct TennisScore: Equatable {
    var player = 0, opponent = 0
    var detail = ""
    init() {}
    init(line: String) {
        let parts = line.split(separator: ",", maxSplits: 2).map(String.init)
        player = parts.count > 0 ? Int(parts[0]) ?? 0 : 0
        opponent = parts.count > 1 ? Int(parts[1]) ?? 0 : 0
        detail = parts.count > 2 ? parts[2] : ""
    }
}

/// Where a hit met the strings (x, y in half-widths of the string bed, so the frame is at ±1)
/// and how well it was timed, from Unity's "contact" event.
struct TennisContact: Equatable, Identifiable {
    let id = UUID()
    var x: Double, y: Double
    /// TennisRules.Timing: 0 missed ... 5 perfect.
    var grade: Int
    var supercharged: Bool
    /// How late the swing was, milliseconds (negative: early).
    var lateMs: Int
    init?(line: String) {
        let parts = line.split(separator: ",").map { Double($0) ?? 0 }
        guard parts.count >= 4 else { return nil }
        x = parts[0]; y = parts[1]; grade = Int(parts[2]); supercharged = parts[3] > 0
        lateMs = parts.count > 4 ? Int(parts[4]) : 0
    }
    /// "LATE" / "EARLY" when the swing was off by more than a perfect one's margin.
    var timingWord: String { lateMs > 35 ? "LATE" : lateMs < -35 ? "EARLY" : "ON TIME" }
    static let gradeNames = ["MISS", "OK", "GOOD", "GREAT", "EXCELLENT", "PERFECT"]
    var gradeName: String { supercharged ? "SUPER SHOT" : Self.gradeNames[max(0, min(5, grade))] }
}

/// Per-TV timing check results, keyed by the AirPlay receiver's name.
enum SportsTiming {
    private static let key = "sports.tv.timing.v1"
    static func currentTV() -> String {
        let outputs = AVAudioSession.sharedInstance().currentRoute.outputs
        return outputs.first(where: { $0.portType == .airPlay })?.portName ?? outputs.first?.portName ?? "screen"
    }
    static func stored(for tv: String, defaults: UserDefaults = .standard) -> Double? {
        (defaults.dictionary(forKey: key) as? [String: Double])?[tv]
    }
    static func store(_ lag: Double, for tv: String, defaults: UserDefaults = .standard) {
        var all = defaults.dictionary(forKey: key) as? [String: Double] ?? [:]
        all[tv] = max(0, min(0.35, lag))
        defaults.set(all, forKey: key)
    }
}
