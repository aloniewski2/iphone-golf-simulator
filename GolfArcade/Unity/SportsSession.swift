import SwiftUI
import AVFoundation

enum ControllerSetupStage { case scan, timing, ready, playing }

enum TennisUltimate: Int, CaseIterable, Identifiable {
    case skybreaker = 0, rescueLob = 1, curveball = 2
    var id: Int { rawValue }
    var title: String { switch self { case .skybreaker: "Skybreaker"; case .rescueLob: "Rescue Lob"; case .curveball: "Curveball" } }
    var detail: String { switch self {
    case .skybreaker: "Your next hit is a rocket: 30% faster, straight where you aim."
    case .rescueLob: "Your next hit floats sky-high and deep, buying time to get back in position."
    case .curveball: "Your next hit bends sideways in the air toward your aim side."
    } }
    var icon: String { switch self { case .skybreaker: "bolt.fill"; case .rescueLob: "arrow.up.forward.circle.fill"; case .curveball: "tornado" } }
}

@MainActor @Observable
final class SportsSession {
    static let shared = SportsSession()
    var players = PlayerRosterStore.load()
    var playerIndex = 0
    var sport = "golf"
    var menuPauseVisible = false
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
    /// A short loading transition, gated by actual runtime readiness.
    let loading = LoadingModel()
    let pointClips = PointClips()
    /// The tennis tutorial's current step, from Unity: (index, count, text).
    var finishedMatch: (won: Bool, score: String)?
    /// Unity's numbers for the match just finished (aces, winners, rally, timing...), for the XP screen.
    var lastMatchStats: MatchStats?
    var tutorialStep: (index: Int, count: Int, text: String)?
    /// Opponent strength for tennis: 0 relaxed, 0.45 standard, 0.8 tough.
    var tennisDifficulty = (UserDefaults.standard.object(forKey:"sports.tennisDifficulty") as? Double) ?? 0.45 {
        didSet { UserDefaults.standard.set(tennisDifficulty,forKey:"sports.tennisDifficulty") }
    }
    /// The court for tennis matches: "resort", "skyscraper" or "volcano" (Unity's TennisVenue keys).
    /// Picked on the map screen before each match and remembered for the next.
    var tennisVenue = UserDefaults.standard.string(forKey: "sports.tennisVenue") ?? "resort" {
        didSet { UserDefaults.standard.set(tennisVenue, forKey: "sports.tennisVenue") }
    }
    var golfCourse = UserDefaults.standard.string(forKey: "sports.golfCourse") ?? "cliffside" {
        didSet { UserDefaults.standard.set(golfCourse, forKey: "sports.golfCourse") }
    }
    /// Set by the `-benchTennis` launch argument: Unity plays itself and logs frame times.
    static let benchmark = ProcessInfo.processInfo.arguments.contains("-benchTennis")
    var haptics = (UserDefaults.standard.object(forKey:"arcade.hapticsEnabled") as? Bool) ?? true {
        didSet { UserDefaults.standard.set(haptics,forKey:"arcade.hapticsEnabled") }
    }
    var status = "Connect a TV or Mac to play. This phone is your controller."
    var feedback = ""
    var ultimateChoice = TennisUltimate(rawValue: UserDefaults.standard.integer(forKey: "tennis.ultimate")) ?? .skybreaker
    var ultimateMeter = 0.0
    var ultimateArmed = false
    var diveCooldown = 0.0
    var canDive = false
    var canArmUltimate = false
    var loadoutLocked = false
    // Retained API for older callers; ultimates are no longer a gameplay action.
    func chooseUltimate(_ choice: TennisUltimate) {}
    func dive() { guard pointControlsVisible && canDive else { return }; command("dive"); canDive = false }
    func armUltimate() {}
    var stamina = 1.0
    var phase = "calibrating"
    var tracking: SportsTrackingQuality = .lost
    var trackingWarning = ""
    var setupStage: ControllerSetupStage = .scan
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
    private(set) var matchEmotes = EmoteCatalog.defaults
    var emoteWindow = ""
    var emoteNotice = ""
    var canPlayEmote: Bool {
        active && ready && loading.finished && !paused && finishedMatch == nil && sport == "tennis"
        && setupStage == .playing && emoteWindow == "point"
    }
    var pointControlsVisible: Bool {
        active && ready && loading.finished && !paused && setupStage == .playing
        && finishedMatch == nil && sport == "tennis"
        && ["serve", "toss", "receive", "rally"].contains(tennisPhase)
    }
    var tossVisible: Bool { pointControlsVisible && tennisPhase == "serve" }
    func playEmote(slot: Int) {
        guard canPlayEmote, matchEmotes.indices.contains(slot) else { return }
        command("emote", value: Double(slot))
        emoteWindow = ""
        emoteNotice = "\(EmoteCatalog.name(matchEmotes[slot])) selected"
    }
    var serveFromDeuce = true
    /// The controller serve's aim in the target box: across (T -1 .. wide +1), depth (0..1).
    var serveAim = (across: 0.0, depth: 0.8)
    var shotDepth=0.75
    var shotAim=0.0
    var aimLesson:TennisAimLesson?
    var aimLessonMessage=""
    private var aimChecked=false
    private var aimFeedSequence=0
    private var aimFeedAt=0.0
    private var aimWaiting=false
    private var aimSwing:TennisAimSwing?
    @ObservationIgnored private var aimFeedTask:Task<Void,Never>?
    @ObservationIgnored private var aimTimeoutTask:Task<Void,Never>?
    var aimingSetup:Bool { aimLesson != nil }
    /// Extra start fields for the tennis front end (mode, opponent, round).
    private var launchExtras: [String:Any] = [:]
    private(set) var multiplayerMatchID: String?
    private var multiplayerSeat = -1
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
        motion.onAimSwing = { [weak self] swing in
            guard let self, self.aimWaiting, self.aimLesson != nil, swing.time >= self.aimFeedAt else { return }
            self.aimSwing=swing
        }
        if players.isEmpty { players=[Player(name:"Player 1",colorIndex:0)] }
        motion.onProblem = { [weak self] message in self?.pause(reason:message); self?.status=message }
        motion.onGate = { [weak self] gate in
            guard let self else { return }
            self.axisGate=gate
            if gate.locked {
                self.motion.start(tennis:true,travel:self.travel)
                if self.setupStage == .scan { self.offerTimingCalibration() }
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
        motion.setOutput(token:sessionToken,live:active && !paused && !touch && (multiplayerMatchID == nil || multiplayerSeat >= 0),swingBase:swingSequence)
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
    func startTennis(opponent: TennisOpponent?, round: Int?, mode: String? = nil, difficulty: Double, coach: [String] = [], preview: Bool, sets: Int? = nil, games: Int? = nil) {
        let mode = mode == "tutorial" ? "exhibition" : mode
        sport = "tennis"
        opponentName = opponent?.name.components(separatedBy: " ").first ?? (mode == "tutorial" ? "Ray" : "Coach")
        launchExtras = ["mode": mode ?? (opponent == nil ? "training" : "campaign"), "opponent": opponent?.key ?? "",
                        "opponentName": opponentName, "round": opponent?.round ?? "", "difficulty": difficulty,
                        "sets": sets ?? opponent?.sets ?? 1, "games": games ?? opponent?.games ?? 3,
                        "coach": coach.joined(separator: "|")]
        start(preview: preview)
        launchExtras = [:]
    }
    /// Start a golf round; legacy tutorial requests use the same playable round.
    func startGolf(tutorial: Bool, preview: Bool) {
        sport = "golf"; opponentName = ""
        launchExtras = ["mode": "round", "course": GolfCourseChoice(rawValue: golfCourse)?.rawValue ?? "cliffside"]
        start(preview: preview)
        launchExtras = [:]
    }
    /// Settings → Controls: run the timing check at the start of the next match.
    func forceTimingCheckNextMatch() { checkTimingOnResume = true }
    func startMultiplayer(_ configuration: MultiplayerMatchConfiguration) {
        guard !active, let data=try? JSONEncoder().encode(configuration), let json=String(data:data,encoding:.utf8) else { return }
        multiplayerMatchID=configuration.matchID
        multiplayerSeat=configuration.participants.first { $0.id == configuration.localID }?.seat ?? -1
        sport=configuration.sport; tennisVenue=configuration.venue
        launchExtras=["mode":"multiplayer", "network":json, "sets":configuration.sets, "games":configuration.games, "tips":false]
        start(preview: !displayConnected)
        launchExtras=[:]
        if !active { multiplayerMatchID=nil }
    }
    func start(preview: Bool = false) {
        guard !active else { return }
        SportsDisplays.shared.refresh()
        let preview = false
        NSLog("[SportsSession] launch sport=%@ mode=%@ touch=%d",sport,preview ? "phone-preview" : "external-controller",touch ? 1 : 0)
        guard let window=SportsDisplays.shared.gameWindow(preview:preview) else {
            status="Connect a TV or Mac with AirPlay or a wired display to play. This phone is your controller."; return
        }
        savePlayers(); sessionID=UUID().uuidString; sessionToken=Int32.random(in:1...Int32.max); ready=false; paused=true; active=true; tennisControllerActive=false
        aimFeedTask?.cancel(); aimTimeoutTask?.cancel(); aimLesson=nil; aimChecked=false; aimWaiting=false; aimSwing=nil; shotAim=0; shotDepth=0.75
        finishedMatch=nil; lastMatchStats=nil; swingSequence=0; target=0; power=0; aim=0; measuringDelay=false; delayTip=""; checkingTiming=false; timingPrompt=false; timingNote=""
        setupStage = .scan; axisGate = SportsAxisGate(); phase="calibrating"; feedback=""; golfPhase=""; golfHasNextHole=false; stamina=1
        ultimateMeter=0; ultimateArmed=false; diveCooldown=0; canDive=false; canArmUltimate=false; loadoutLocked=false
        let p=players[min(playerIndex,players.count-1)]
        matchEmotes = p.equippedEmotes; emoteWindow = ""; emoteNotice = ""; tennisPhase = ""
        if let network = launchExtras["network"] as? String, let data = network.data(using: .utf8),
           let configuration = try? JSONDecoder().decode(MultiplayerMatchConfiguration.self, from: data),
           let local = configuration.participants.first(where: { $0.id == configuration.localID }) {
            matchEmotes = EmoteCatalog.normalized(local.loadout?.emotes)
        }
        motion.setAimProfile(storedAimProfile())
        pending=["version":1,"session":sessionID,"action":"start","sport":sport,"playerID":p.id.uuidString,"playerName":p.name,"female":p.standardFemale,"skin":p.standardSkin,"left":p.handedness == .left,"sound":sound,"haptics":haptics,"touch":touch || preview,"token":Int(sessionToken),"fps":highFrameRate ? 120 : 60,"bench":SportsSession.benchmark,"difficulty":tennisDifficulty,"venue":sport == "tennis" ? tennisVenue : "resort",
                 "shirt":p.outfitHex("shirt") ?? "","shorts":p.outfitHex("shorts") ?? "","accent":p.outfitHex("accent") ?? "","racket":p.outfitHex("racket") ?? "",
                 "skinHex":p.skinHex,"hairHex":p.hairHex,
                 "tips":false,"overscan":overscan,
                 "hairStyle":p.hairStyle,"haircut":p.shownHaircut,"hairColor":p.hairColor,"faceShape":p.faceShape,
                 "heightChoice":p.heightChoice,"buildChoice":p.buildChoice,"bodySize":p.bodySize,
                 "loadout":p.loadoutPayload(sport:Sport(rawValue:sport) ?? .tennis)]
        if preview { touch=true }
        runtimeExternalDisplay = !preview
        pending?["external"] = runtimeExternalDisplay
        pending?["emotes"] = matchEmotes
        for (key, value) in launchExtras { pending?[key] = value }
        // Explicit device verification uses a real, short match (no fabricated finish event).
        if Self.benchmark && ProcessInfo.processInfo.arguments.contains("--postgame-check") {
            pending?["sets"] = 1; pending?["games"] = 1
        }

        score = TennisScore(); contacts = []; tutorialStep = nil
        loading.begin(now: Date())
        loading.onFinish = { [weak self] in self?.loadingFinished() }
        do { try SportsRuntime.shared().load(in:window) }
        catch { failStartup(error.localizedDescription); return }
        SportsRuntime.shared().clearTennisResult()
        SportsDisplays.shared.restorePhoneControls()
        SportsRuntime.shared().pause(false)
        status="Loading Unity…"
        timer?.invalidate()
        timer=Timer(timeInterval:0.05,repeats:true) { [weak self] _ in
            MainActor.assumeIsolated { self?.poll() }
        }
        if let timer { RunLoop.main.add(timer, forMode: .common) }
        // A persistent runtime is already ready after the first launch.
        sendPending()
        let launchingSession=sessionID
        Task { @MainActor [weak self] in
            try? await Task.sleep(for:.seconds(20))
                guard let self, self.sessionID == launchingSession, self.active, !self.ready, self.pending != nil else { return }
            self.failStartup("Unity did not finish loading. Choose a map to retry.")
        }
    }
    func failStartup(_ message: String) {
        SportsDiagnostics.write("startup failed: \(message)")
        end()
        status = message
        TennisMenu.shared.launchFailed(message)
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

    private var aimProfileKey:String {
        let player=players[min(playerIndex,players.count-1)]
        return "tennis.aim.v1.\(player.id.uuidString).\(player.handedness == .left ? "left" : "right")"
    }
    private func storedAimProfile() -> TennisAimProfile? {
        guard !players.isEmpty, let data=UserDefaults.standard.data(forKey:aimProfileKey),
              let profile=try? JSONDecoder().decode(TennisAimProfile.self,from:data), profile.valid else { return nil }
        return profile
    }
    /// Court scanning finishes before timing. The match remains held until Ready.
    func offerTimingCalibration() {
        guard active, ready, loading.finished, sport == "tennis" else { return }
        setupStage = .timing; timingPrompt = true; checkingTiming = false
        menuPauseVisible = false; measuringDelay = false
        status = "TV scan complete. Calibrate your swing timing on the TV."
        command("cancelTimingCheck"); command("controllerSetup", value: 1)
        pause(reason: status)
        SportsDisplays.shared.external?.isHidden = true
    }
    func skipTimingCheck() {
        guard setupStage == .timing else { return }
        command("cancelTimingCheck")
        completeTimingCalibration(message: timingCalibration == nil ? "The game will refine your timing as you play." : "Using the saved timing for this screen.")
    }
    func completeTimingCalibration(message: String) {
        guard setupStage == .timing else { return }
        timingPrompt = false; checkingTiming = false; timingCountdownEnds = nil
        timingNote = message; setupStage = .ready
        pause(reason: "Timing calibration complete. Tap Ready to play.")
    }
    func receiveTimingResult(_ message: String) {
        guard checkingTiming, setupStage == .timing else { return }
        let note: String
        if let ms = Double(message), ms.isFinite, ms >= 0 {
            SportsTiming.store(ms / 1000, for: SportsTiming.currentTV())
            note = String(format: "Timing set — %.0f ms.", ms)
        } else {
            note = "The game will refine your timing as you play."
        }
        SportsDiagnostics.write("timing check tv=\(SportsTiming.currentTV()) result=\(message)")
        completeTimingCalibration(message: note)
    }
    /// Recenter the racket without entering the retired aiming lesson.
    func recalibrateAiming() {
        guard active, ready, !touch, motion.axisLocked, motion.calibrate() else { return }
        phase = "steering"; command("recalibrate")
        pause(reason: "Center set. Tap Ready to continue.")
    }

    // MARK: Controller serve

    /// TOSS pressed. The game judges it against the toss meter under the player's feet on the
    /// TV, as the player saw it then (value -1: "read your meter").
    func toss() {
        guard tossVisible else { return }
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
        if checkingTiming {
            command("cancelTimingCheck"); checkingTiming = false; timingPrompt = true; timingCountdownEnds = nil
        }
        SportsDiagnostics.write("pause reason=\(reason) touch=\(touch) phase=\(phase)")
        sendJSON(["version":1,"session":sessionID,"action":"pause","reason":reason]); paused=true; status=reason
        if reason == "Paused on phone — tap Ready to continue", ready, loading.finished, finishedMatch == nil {
            menuPauseVisible = true; SportsDisplays.shared.showMenu(); if !displayConnected { SportsDisplays.shared.showMatchControls() }
        }
    }
    func resume() {
        guard displayConnected else { pause(reason: "Reconnect your TV or Mac to continue."); return }
        guard sport != "tennis" || setupStage == .playing else { status = "Finish TV scan and timing calibration, then tap Ready."; return }
        guard ready, touch || phase == "steering" else { status="Tap Ready to set your center and play."; return }
        menuPauseVisible = false; SportsDisplays.shared.external?.isHidden = true
        if !displayConnected { SportsDisplays.shared.restorePhoneControls() }
        command("resume"); paused=false; status="Playing"; if sport == "tennis" { loadoutLocked=true }
        if sport == "tennis" { tennisControllerActive=true }
        SportsDiagnostics.write("resume touch=\(touch) phase=\(phase) target=\(target)")
    }
    func readyToPlay() {
        guard displayConnected, active, ready, loading.finished else { return }
        if sport == "tennis", setupStage == .scan || setupStage == .timing {
            menuPauseVisible = false; SportsDisplays.shared.external?.isHidden = true
            status = setupStage == .scan ? "Scan your TV to continue." : "Start the timing calibration to continue."
            return
        }
        if !touch {
            guard sport != "tennis" || motion.axisLocked else { return }
            guard motion.calibrate() else { return }
            phase = "steering"; command("recalibrate")
        }
        setupStage = .playing
        command("controllerSetup", value: 0)
        resume()
    }
    /// Unity renders the timing ball on the TV; motion samples keep flowing from the phone.
    func startTimingCheck() {
        guard active, ready, loading.finished, displayConnected, sport == "tennis",
              setupStage == .timing, !checkingTiming else { return }
        if !touch {
            guard motion.axisLocked, motion.calibrate() else { return }
            phase = "steering"
        }
        menuPauseVisible = false; SportsDisplays.shared.external?.isHidden = true
        command("controllerSetup", value: 1)
        command("resume"); paused = false
        timingPrompt = false; checkingTiming = true; timingNote = ""
        timingCountdownEnds = Date().addingTimeInterval(5)
        status = "Swing with the bouncing ball on your TV."
        command("timingCheck")
    }
    func recheckTiming() {
        guard touch || motion.axisLocked else { beginAxisCapture(); return }
        offerTimingCalibration()
    }
    func useTouch() {
        if sport == "tennis" {
            timingPrompt=false; checkingTiming=false; timingCountdownEnds=nil; checkTimingOnResume=false
            command("cancelTimingCheck"); aimLesson = nil; setupStage = .ready
        }
        pause(); swingSequence=max(swingSequence,motion.swingCount); touch=true; trackingWarning=""; measuringDelay=false
        motion.stop(); command("touch"); status="Touch controls selected. Tap Ready to play."
    }
    func useMotion() {
        pause(); touch=false; phase="calibrating"; trackingWarning=""; command("motion")
        if sport == "tennis" { aimChecked=false }
        if sport == "tennis" && !motion.axisLocked { beginAxisCapture(); return }
        motion.start(tennis:sport == "tennis",travel:travel)
        status="Motion controls selected. Stand at your center, then tap Ready." 
    }
    /// Locking the court direction is a separate, gated step. Once it is done the phone can
    /// be held at any angle — which is the whole point, since forehands and backhands flip it.
    func beginAxisCapture() {
        if sport == "tennis" {
            aimFeedTask?.cancel(); aimTimeoutTask?.cancel(); aimLesson=nil; aimWaiting=false; aimSwing=nil; aimChecked=false
        }
        setupStage = .scan; timingPrompt = false; checkingTiming = false
        command("cancelTimingCheck"); command("controllerSetup", value: 1)
        pause(reason: "Scan your TV to set the court direction.")
        axisGate=SportsAxisGate()
        status="Stand about 2.5 m back, point the back of your phone at the TV and hold still."
        motion.beginAxisCapture(travel:travel)
    }
    /// Escape hatch when the gate will not settle, so motion tennis is never unreachable.
    func useCurrentDirection() {
        guard motion.forceAxisFromCurrentPose() else { return }
        motion.start(tennis:true,travel:travel)
        if setupStage == .scan { offerTimingCalibration() }
    }
    /// One tap to mirror steering, for when left and right come out swapped.
    func flipSteering() {
        motion.courtSign = -motion.courtSign
        status=motion.courtSign<0 ? "Steering flipped. Tap Ready to recenter." : "Steering restored. Tap Ready to recenter."
    }
    func steer(_ value:Double) { target=value; if !paused { sendInput(valid:true) } }
    func setAim(_ value:Double) { if sport == "tennis" { setShotAim(across:value,depth:shotDepth) } else { aim=value; command("aim",value:value) } }
    func setShotAim(across:Double,depth:Double) {
        shotAim=max(-1,min(1,across)); shotDepth=max(0,min(1,depth)); aim=shotAim
        sendJSON(["version":1,"session":sessionID,"action":"rallyAim","value":shotAim,"value2":shotDepth])
    }
    func setShotDepth(_ depth:Double) { setShotAim(across:shotAim,depth:depth) }
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
    func setPointRecording(_ enabled: Bool) {
        guard active, ready, sport == "tennis", finishedMatch == nil else { return }
        pointClips.bufferingEnabled = enabled
        pointClips.state = enabled ? "checking" : "off"
        pointClips.message = enabled
            ? (paused ? "Resume play to check 1080p · 60 fps." : "Checking 1080p · 60 fps…")
            : "Point buffering off."
        command("recordPoints", value: enabled ? 1 : 0)
    }

    /// The loading screen has run its course: show the game, and start the setup that the
    /// player sees (court direction, motion) only now.
    private func loadingFinished() {
        guard active, ready else { return }
        if displayConnected { SportsDisplays.shared.external?.isHidden=true }
        else { SportsDisplays.shared.restorePhoneControls() }
        // Compensate from the first point with the last measurement of this TV; the
        // setup re-times it when the camera is aimed at it.
        if displayConnected && startingLag>0 { command("latency",value:startingLag) }
        if multiplayerMatchID != nil {
            guard SportsRuntime.shared().multiplayerAvailable() else {
                MultiplayerService.shared.runtimeUnavailable("This Unity export does not contain multiplayer. Re-export and rebuild the integrated app.")
                return
            }
            setupStage = .playing; paused=false; status=multiplayerSeat<0 ? "Watching" : "Playing"
            if !touch && multiplayerSeat>=0 { motion.start(tennis:sport == "tennis",travel:travel) }
            command("resume"); MultiplayerService.shared.runtimeLoaded(); return
        }
        if Self.benchmark { setupStage = .ready; readyToPlay(); return }
        if sport == "tennis" {
            if touch { offerTimingCalibration() } else { beginAxisCapture() }
        } else {
            setupStage = .ready
            if !touch { motion.start(tennis:false,travel:travel) }
            status = "Stand at your center, then tap Ready."
        }
    }
    /// Completion is part of every Unity heartbeat, independently of one-shot result events.
    func receiveMatchSnapshot(_ event: [String: Any]) {
        guard event["matchComplete"] as? Bool == true,
              let won = event["matchWon"] as? Bool else { return }
        receiveMatchFinish(won: won, score: event["finalScore"] as? String ?? "")
    }

    func receiveMatchFinish(won: Bool, score: String) {
        guard active, finishedMatch == nil else { return }
        finishedMatch = (won, score)
        SportsDiagnostics.write("match finished: phone Next enabled; classic=\(TennisMenu.shared.classic) external=\(displayConnected)")
        motion.stop()
        TennisMenu.shared.matchFinished(won: won, score: score)
        // Keep explicit phone actions visible until the player chooses one.
        canDive = false
        timingPrompt = false; checkingTiming = false; measuringDelay = false
        aimFeedTask?.cancel(); aimTimeoutTask?.cancel(); aimLesson=nil; aimWaiting=false
        SportsDisplays.shared.showMatchControls()
        if Self.benchmark {
            Task { @MainActor in
                try? await Task.sleep(for: .seconds(1))
                SportsDisplays.shared.captureBenchmarkFinish()
            }
        }
    }

    func advanceAfterMatch(_ choice: TennisMenu.AfterMatch = .menu) {
        guard active, finishedMatch != nil else {
            SportsDiagnostics.write("postMatch ignored: active=\(active) finished=\(finishedMatch != nil)")
            return
        }
        SportsDiagnostics.write("postMatch tapped: \(choice) mode=\(TennisMenu.shared.launch?.mode.rawValue ?? "none") round=\(TennisMenu.shared.launch?.round ?? -1)")
        TennisMenu.shared.finishMatch(choice)
    }

    func showMatchRewards() {
        guard active, finishedMatch != nil else { return }
        TennisMenu.shared.beginPostMatch()
    }

    var golfPhase = ""
    var golfHasNextHole = false
    private var runtimeExternalDisplay = false
    /// A display can arrive after a phone game has begun, including while it is loading.
    func routeGameToExternalDisplay() {
        guard active, ready, let window = SportsDisplays.shared.external else { return }
        runtimeExternalDisplay = true
        SportsRuntime.shared().attach(to: window)
        command("display")
        SportsDisplays.shared.endPreview()
        SportsDisplays.shared.restorePhoneControls()
        status = "Display connected. Waiting for a gameplay frame…"
    }
    func exitGame() {
        if multiplayerMatchID != nil { TennisMenu.shared.online.select("net-leave", menu: .shared) }
        else if OnboardingFlow.shared.active { OnboardingFlow.shared.exitToMenu() }
        else { TennisMenu.shared.goHome() }
    }
    func end() {
        menuPauseVisible = false
        if active && ready {
            var history=UserDefaults.standard.array(forKey:"sports.sessions.v1") as? [[String:Any]] ?? []
            history.append(["session":sessionID,"sport":sport,"playerID":players[playerIndex].id.uuidString,"endedAt":Date().timeIntervalSince1970,"summary":feedback])
            UserDefaults.standard.set(Array(history.suffix(100)),forKey:"sports.sessions.v1")
        }
        pointClips.reset()
        multiplayerMatchID=nil; multiplayerSeat = -1
        command("end"); motion.stop(); timer?.invalidate(); timer=nil; pending=nil; measuringDelay=false; checkingTiming=false
        loading.cancel(); tutorialStep=nil; finishedMatch=nil; timingPrompt=false; setupStage = .scan
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
                if displayConnected && !runtimeExternalDisplay { routeGameToExternalDisplay() }
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
                if sport == "golf" {
                    golfPhase = event["golfState"] as? String ?? ""
                    golfHasNextHole = event["golfHasNextHole"] as? Bool ?? false
                }
                receiveMatchSnapshot(event)
                feedback=event["message"] as? String ?? ""; stamina=event["stamina"] as? Double ?? 1
                if SportsRuntime.shared().clock()>=nextDiagnostic {
                    nextDiagnostic=SportsRuntime.shared().clock()+1
                    SportsDiagnostics.write("bridge touch=\(touch) nativePaused=\(paused) phase=\(phase) target=\(target) unityFrame=\(event["frame"] ?? "unknown") unityPaused=\(event["paused"] ?? "unknown") playerX=\(event["playerX"] ?? "unknown") inputAge=\(event["inputAge"] ?? "unknown")")
                }
            case "displayReady":
                if displayConnected { SportsDisplays.shared.external?.isHidden=true }
                status="Display reconnected. Tap Ready to set your center and play."
            case "recording": pointClips.receive(event)
            case "perf": SportsDiagnostics.write("perf \(event["message"] as? String ?? "")")
            case "score":
                score = TennisScore(line: event["message"] as? String ?? "")
                // The final scoreboard ("YOU WIN 6–4 3–6 7–5" / "OPPONENT WINS …") also ends the
                // match here, so the phone's Next never depends on one event arriving.
                for (prefix, won) in [("YOU WIN ", true), ("OPPONENT WINS ", false)] where score.detail.hasPrefix(prefix) {
                    receiveMatchFinish(won: won, score: String(score.detail.dropFirst(prefix.count)))
                }
            case "emoteState":
                let window = event["message"] as? String ?? ""
                if window != emoteWindow { emoteNotice = "" }
                emoteWindow = window
            case "emoteResult": emoteNotice = event["message"] as? String ?? ""
            case "timing": receiveTimingResult(event["message"] as? String ?? "failed")
            case "abilities":
                let fields = (event["message"] as? String ?? "").split(separator: "|")
                if fields.count == 5, let meter = Double(fields[0]), let cooldown = Double(fields[2]), meter.isFinite, cooldown.isFinite {
                    ultimateMeter = 0; ultimateArmed = false
                    diveCooldown = max(0, cooldown); canDive = fields[3] == "1"; canArmUltimate = false
                }
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
                if tennisPhase != phase { SportsDiagnostics.write("tennis phase=\(phase)") }
                tennisPhase=phase
                if phase == "finished" { receiveMatchSnapshot(event) }
                if parts.count>1 { serveFromDeuce=parts[1] == "deuce" }
            case "golfHoleDone":
                let parts = (event["message"] as? String ?? "").split(separator: "|").map(String.init)
                if parts.count == 2 { Analytics.track("golf_hole_done", ["strokes": parts[0], "capped": parts[1]]) }
            case "rally": if let n=Int(event["message"] as? String ?? "") { SportProgress.shared.recordRally(n); SportsDiagnostics.write("rally persisted \(n)") }
            case "matchStats": lastMatchStats = MatchStats(line: event["message"] as? String ?? "")
            case "matchOver":
                let parts = (event["message"] as? String ?? "").split(separator: "|")
                receiveMatchFinish(won: parts.first == "won", score: parts.count > 1 ? String(parts[1]) : "")
            case "exit": if active { exitGame() }
            case "error":
                let message = event["message"] as? String ?? "Unity error"
                if !ready { failStartup(message) }
                else { status = message; pause(reason: message) }
            default: break
            }
        }
        // The durable copy of the result, read after the events so their stats are already in.
        if active && finishedMatch == nil, let result = SportsRuntime.shared().tennisResult() {
            let parts = result.split(separator: "|", maxSplits: 1).map(String.init)
            receiveMatchFinish(won: parts.first == "won", score: parts.count > 1 ? parts[1] : "")
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
