import SwiftUI

@MainActor @Observable
final class SportsSession {
    static let shared = SportsSession()
    var players = PlayerRosterStore.load()
    var playerIndex = 0
    var sport = "golf"
    var touch = false
    var travel = 0.35
    var sound = (UserDefaults.standard.object(forKey:"range.soundEnabled") as? Bool) ?? true {
        didSet { UserDefaults.standard.set(sound,forKey:"range.soundEnabled") }
    }
    var haptics = (UserDefaults.standard.object(forKey:"arcade.hapticsEnabled") as? Bool) ?? true {
        didSet { UserDefaults.standard.set(haptics,forKey:"arcade.hapticsEnabled") }
    }
    var status = "Connect an external display, or use on-phone preview."
    var feedback = ""
    var stamina = 1.0
    var phase = "calibrating"
    var active = false
    var paused = true
    var ready = false
    var displayConnected = false
    private var sessionID = ""
    private var pending: [String:Any]?
    private var timer: Timer?
    private var swingSequence = 0
    private var target = 0.0
    private var power = 0.0
    private var aim = 0.0
    private var rawMotion: [String:Double] = [:]
    private let motion = SportsMotion()
    init() {
        if players.isEmpty { players=[Player(name:"Player 1",colorIndex:0)] }
        motion.onProblem = { [weak self] message in self?.pause(); self?.status=message }
        motion.onSample = { [weak self] target,swing,phase,valid,raw in
            guard let self, self.active, !self.touch else { return }
            self.phase=phase; self.target=target
            self.rawMotion=raw
            if let swing, !self.paused { self.power=swing; self.swingSequence+=1 }
            if !valid && self.ready && !self.paused { self.pause(); self.status="Tracking unavailable. Recalibrate or select touch controls." }
            if !self.paused { self.sendInput(valid:valid) }
        }
    }
    func savePlayers() { PlayerRosterStore.save(players) }
    func start(preview: Bool = false) {
        guard let window=SportsDisplays.shared.gameWindow(preview:preview) else {
            status="No independent external display is available. Connect AirPlay/wired display or choose preview."; return
        }
        savePlayers(); sessionID=UUID().uuidString; ready=false; paused=true; active=true
        swingSequence=0; target=0; power=0; aim=0; rawMotion=[:]
        phase="calibrating"; feedback=""; stamina=1
        let p=players[min(playerIndex,players.count-1)]
        pending=["version":1,"session":sessionID,"action":"start","sport":sport,"playerID":p.id.uuidString,"playerName":p.name,"female":p.standardFemale,"skin":p.standardSkin,"left":p.handedness == .left,"sound":sound,"haptics":haptics,"touch":touch || preview]
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
        Task { @MainActor [weak self] in
            try? await Task.sleep(for:.seconds(20))
                guard let self, self.active, !self.ready, self.pending != nil else { return }
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
        sendJSON(["version":1,"session":sessionID,"action":action,"value":value])
    }
    func pause() { guard active else { return }; command("pause"); paused=true }
    func resume() {
        guard ready, touch || phase == "steering" else { status="Calibrate before resuming."; return }
        command("resume"); paused=false; status="Playing"
    }
    func calibrate() { motion.calibrate(); command("recalibrate") }
    func useTouch() { pause(); touch=true; motion.stop(); command("touch"); status="Touch controls selected. Resume when ready." }
    func steer(_ value:Double) { target=value; if !paused { sendInput(valid:true) } }
    func setAim(_ value:Double) { aim=value; command("aim",value:value) }
    func swing(_ value:Double) { guard !paused else { return }; power=value; swingSequence+=1; sendInput(valid:true) }
    private func sendInput(valid:Bool) {
        var object:[String:Any]=["version":1,"session":sessionID,"time":SportsRuntime.shared().clock(),"target":target,"power":power,"swing":swingSequence,"valid":valid,"aim":aim]
        if !touch { for (key,value) in rawMotion { object[key]=value } }
        if let data=try? JSONSerialization.data(withJSONObject:object), let json=String(data:data,encoding:.utf8) { SportsRuntime.shared().push(json) }
    }
    func end() {
        if active && ready {
            var history=UserDefaults.standard.array(forKey:"sports.sessions.v1") as? [[String:Any]] ?? []
            history.append(["session":sessionID,"sport":sport,"playerID":players[playerIndex].id.uuidString,"endedAt":Date().timeIntervalSince1970,"summary":feedback])
            UserDefaults.standard.set(Array(history.suffix(100)),forKey:"sports.sessions.v1")
        }
        command("end"); motion.stop(); timer?.invalidate(); timer=nil; pending=nil
        SportsRuntime.shared().pause(true); active=false; ready=false; paused=true
        SportsDisplays.shared.endPreview(); status="Choose a sport."
    }
    private func poll() {
        if touch && active && !paused { sendInput(valid:true) }
        for _ in 0..<64 {
            guard let json=SportsRuntime.shared().pollEvent(),let data=json.data(using:.utf8),
                  let event=(try? JSONSerialization.jsonObject(with:data)) as? [String:Any] else { break }
            if event["type"] as? String == "boot" { sendPending(); continue }
            guard event["session"] as? String == sessionID else { continue }
            switch event["type"] as? String {
            case "ready":
                pending=nil; ready=true; status=touch ? "Ready. Tap Resume." : "Face the display, hold neutral, then Calibrate and Resume."
                if displayConnected { SportsDisplays.shared.external?.isHidden=true }
                if !touch { motion.start(tennis:sport == "tennis",travel:travel) }
            case "feedback": feedback=event["message"] as? String ?? ""; stamina=event["stamina"] as? Double ?? 1
            case "error": pending=nil; status=event["message"] as? String ?? "Unity error"; pause()
            default: break
            }
        }
    }
}
