import SwiftUI

/// What the player stands next to in the Plaza, from Unity's `hubZone` event ("kind|id|label|detail").
struct HubZone: Equatable {
    enum Kind: String { case door, station, bay }
    var kind: Kind, id: String, label: String, detail: String
    init?(line: String) {
        let parts = line.split(separator: "|", maxSplits: 3, omittingEmptySubsequences: false).map(String.init)
        guard parts.count >= 3, let kind = Kind(rawValue: parts[0]), !parts[1].isEmpty else { return nil }
        self.kind = kind; id = parts[1]; label = parts[2]; detail = parts.count > 3 ? parts[3] : ""
    }
    /// What the phone's strip says: "Locker · walk in", "Shirt · A to use", "Exhibition · A to sit".
    var prompt: String {
        switch kind {
        case .door: detail == "Coming soon" ? "\(label) · coming soon" : "\(label) · walk in"
        case .station: "\(label) · A to use"
        case .bay: "\(label) · A to sit"
        }
    }
}

/// The walkable menu (PLAN_MenuHub_WalkableWorld): with a TV connected, the menus are the Plaza in Unity on the TV and the phone is
/// an analog stick. This owns that session: when to open the plaza, the runtime start, the stick stream (the binary sample channel:
/// target = x, aim = y, power = deflection), the buttons, and Unity's reports of where the player is.
///
/// It never runs with no TV (the phone keeps TennisPhoneMenu), during a match (SportsSession owns the runtime then), during
/// onboarding, or with Plaza menus switched off; ☰ shows the classic menu over it at any time.
@MainActor @Observable
final class HubSession {
    static let shared = HubSession()
    enum Phase: Equatable { case off, booting, live, suspended, failed }

    private(set) var phase: Phase = .off
    /// Where the player is: "plaza", "locker", "clubhouse", "play", "tennis", "golf".
    private(set) var place = "plaza"
    private(set) var zone: HubZone?
    /// The last station or bay the player pressed A at.
    private(set) var lastAction: String?
    /// The station or bay in use: the TV frames the hero, the phone shows its panel (the existing menu screen for it).
    private(set) var station: String?
    var notice = ""
    /// The classic menu is showing over the plaza (☰).
    private(set) var classicOverlay = false
    /// Settings: walk the Plaza when a TV is connected (on), or keep the flat TV menu (off).
    var plazaMenus = (UserDefaults.standard.object(forKey: "sports.plazaMenus") as? Bool) ?? true {
        didSet { UserDefaults.standard.set(plazaMenus, forKey: "sports.plazaMenus"); evaluate() }
    }
    /// Unity's measured age of the stick samples it applied (p50, p95), milliseconds.
    private(set) var inputAge = (p50: 0.0, p95: 0.0)
    private(set) var speed = 0.0
    private(set) var sessionID = ""
    private var token: Int32 = 0
    /// The plaza scene is what Unity has loaded right now (false once a match replaced it).
    private(set) var loaded = false
    @ObservationIgnored private var timer: Timer?
    @ObservationIgnored private var stick = (x: 0.0, y: 0.0, mag: 0.0)
    @ObservationIgnored private var lastSampleSent = 0.0
    @ObservationIgnored private var bootStarted = Date.distantPast

    private var session: SportsSession { .shared }
    private var menu: TennisMenu { .shared }

    /// Should the plaza be on the TV right now?
    var wanted: Bool {
        plazaMenus && session.displayConnected && !session.active && !menu.classic && !OnboardingFlow.shared.holdsMenu
            && !classicOverlay && !SportsSession.benchmark && phase != .failed
            && (menu.screen == .title || menu.screen == .main || station != nil || (party != nil && menu.screen == .online(.lobby)))
    }
    /// The phone shows the stick (the plaza is opening or on).
    var drivesPhone: Bool { wanted && (phase == .booting || phase == .live) }

    /// Called whenever something the decision depends on changes (display, match, menu screen, settings).
    func evaluate() {
        // the panel's own Back / Done reached the home screen: the station is finished
        if let s = station, !(Self.partyBays.contains(s) && party != nil), menu.screen == .main || menu.screen == .title { station = nil; command("hubB") }
        if wanted { enter() } else if phase == .live || phase == .booting { suspend() }
    }

    // MARK: lifecycle

    private func enter() {
        switch phase {
        case .live: reveal(); return
        case .booting: return
        case .suspended where loaded:
            SportsRuntime.shared().pause(false); phase = .live; reveal(); startTimer(); sendLook(); return
        default: break
        }
        guard let window = SportsDisplays.shared.external else { return }
        phase = .booting; bootStarted = Date(); notice = ""
        sessionID = UUID().uuidString; token = Int32.random(in: 1...Int32.max); loaded = false
        place = "plaza"; zone = nil; lastAction = nil
        SportsDiagnostics.write("hub boot session=\(sessionID)")
        // A display cycle first: the first Unity start blocks the main thread while it boots.
        let launch = sessionID
        DispatchQueue.main.asyncAfter(deadline: .now() + 0.05) { [weak self] in
            guard let self, self.sessionID == launch, self.phase == .booting, self.wanted else { return }
            do { try SportsRuntime.shared().load(in: window, controllerReplica: false) }
            catch { self.fail(error.localizedDescription); return }
            SportsRuntime.shared().pause(false)
            self.startTimer()
            self.sendStart()
        }
    }

    /// The classic TV menu takes over (☰, a menu screen, a match starting): stop walking, show the TV menu, idle Unity.
    private func suspend() {
        setStick(x: 0, y: 0, magnitude: 0)
        if phase == .booting { phase = .off; timer?.invalidate(); timer = nil; return }
        phase = .suspended; timer?.invalidate(); timer = nil
        SportsDisplays.shared.showMenu()
        if !session.active { SportsRuntime.shared().pause(true) }
    }

    /// The bay you launched from: back from the match, you sit on its bench again (RETURN).
    private(set) var returnBay: String?
    /// A match ended. Played to the end: back on the same bench. Stopped while loading or quit early: back on your feet (CANCEL).
    func sessionEnded(finished: Bool) { if !finished { returnBay = nil } }
    /// The bay to launch from (the match loads behind the plaza), or nil for a normal launch.
    func bayForLaunch() -> String? {
        guard phase == .live, loaded, let s = station, s.hasPrefix("bay-") else { return nil }
        return s
    }
    /// A match took the runtime (SportsSession.start): its scene replaces the plaza (from a bay: once it is ready behind it).
    /// Launches made from a bay (behind the plaza), for tests and diagnostics.
    private(set) var bayLaunches = 0
    func runtimeTaken(bay: String? = nil) {
        if let bay { returnBay = bay; bayLaunches += 1 }
        timer?.invalidate(); timer = nil; station = nil
        if phase != .off { phase = .suspended }
        loaded = false
    }

    /// The TV went away: the plaza is gone with it.
    func displayLost() {
        timer?.invalidate(); timer = nil
        if loaded && !session.active { sendJSON(["version": 1, "session": sessionID, "action": "end"]) }
        phase = .off; loaded = false; zone = nil; classicOverlay = false; station = nil
    }

    private func fail(_ message: String) {
        SportsDiagnostics.write("hub failed: \(message)")
        timer?.invalidate(); timer = nil; loaded = false
        phase = .failed; notice = message
        SportsDisplays.shared.showMenu()   // never strand the player: the classic TV menu is right there
    }
    /// Try the plaza again after a failure.
    func retry() { if phase == .failed { phase = .off; evaluate() } }

    private func reveal() {
        guard phase == .live else { return }
        SportsDisplays.shared.external?.isHidden = true
    }

    // MARK: classic overlay (☰)

    func showClassic() {
        classicOverlay = true; station = nil
        evaluate()
    }

    // MARK: stations and bays (phase 2: each opens the existing menu screen for it on the phone)

    /// What a station or bay opens. `classic`: the screen is one of the multiplayer flows, which use the TV menu.
    struct Route { var screen: MenuScreen?; var taps: [String] = []; var classic = false; var action: (@MainActor () -> Void)? = nil }
    /// Every spot in the Plaza (Unity's HubLayout.Spots; HubLayoutTests checks the same list).
    static let spotIDs = ["rack-shirt", "rack-shorts", "rack-shoes", "rack-racket", "rack-club", "look-mirror", "emote-mirror",
                          "desk-settings", "screen-howto", "shelf-trophies", "mailbox-feedback", "board-invite",
                          "bay-tennis-exhibition", "bay-tennis-campaign", "bay-tennis-training", "bay-tennis-online",
                          "bay-golf-round", "bay-golf-online", "bay-golf-pass"]
    static func route(_ id: String) -> Route? {
        switch id {
        case "rack-shirt": Route(screen: .character, taps: ["lk-tab-customize", "lk-shirt"])
        case "rack-shorts": Route(screen: .character, taps: ["lk-tab-customize", "lk-shorts"])
        case "rack-shoes": Route(screen: .character, taps: ["lk-sport-tennis", "lk-slot-shoes"])
        case "rack-racket": Route(screen: .character, taps: ["lk-sport-tennis", "lk-slot-racket"])
        case "rack-club": Route(screen: .character, taps: ["lk-sport-golf", "lk-slot-club"])
        case "look-mirror": Route(screen: .character, taps: ["lk-tab-customize"])
        case "emote-mirror": Route(screen: .character, taps: ["lk-tab-emotes"])
        case "desk-settings": Route(screen: .settings)
        case "screen-howto": Route(screen: .howTo)
        case "shelf-trophies", "bay-tennis-campaign": Route(screen: .campaign)
        case "mailbox-feedback": Route(action: { BetaFeedback.open() })
        case "board-invite": Route(screen: .main, taps: ["homeInvite"], classic: true)
        case "bay-tennis-exhibition": Route(screen: .exhibition)
        case "bay-tennis-training": Route(screen: .training)
        case "bay-tennis-online", "bay-golf-online": Route(screen: .onlineChoice, taps: ["onlineQuick"], classic: true)
        case "bay-golf-round": Route(screen: .hub(.golf), taps: ["round"])
        case "bay-golf-pass": Route(screen: .localChoice, taps: ["partyLocalGolf"], classic: true)
        default: nil
        }
    }

    /// Unity confirmed A at a spot (hubAction): open its panel.
    func useStation(_ id: String) {
        if Self.partyBays.contains(id), let party {
            // a party sits together in an online bay: the bay's sport becomes the lobby's (host), the phone shows who is here
            station = id
            let sport: MultiplayerSport = id.contains("golf") ? .golf : .tennis
            if partyService.isOwner, party.sport != sport { try? partyService.configure(sport) }
            evaluate(); return
        }
        guard let route = Self.route(id) else { command("hubB"); return }
        if let action = route.action { action(); command("hubB"); return }
        if route.classic { showClassic() }
        if let screen = route.screen { menu.openFromPlaza(screen) }
        for tap in route.taps { menu.tap(tap) }
        if !route.classic { station = id }
        evaluate()
    }
    /// B or Done on the station panel: back to walking.
    func leaveStation() {
        guard station != nil else { return }
        if Self.partyBays.contains(station!), party != nil { try? partyService.setReady(false) }
        station = nil; returnBay = nil; command("hubB")
        if menu.screen != .main && menu.screen != .title { menu.openFromPlaza(.main) }
        evaluate()
    }
    /// ☰ shortcut: walk there (a quick fade) and use it.
    func openStation(_ id: String) { command("hubUse", mode: id); haptic() }
    func backToPlaza() {
        classicOverlay = false
        // a party's lobby stays: the plaza is where it lives now
        if menu.screen != .main && menu.screen != .title { if menu.screen.isOnline && partyService.lobby != nil { menu.openFromPlaza(.main) } else { menu.goHome() } }
        evaluate()
    }

    // MARK: input

    /// The stick: direction (-1...1 each, +y = away from the camera) and deflection 0...1.
    func setStick(x: Double, y: Double, magnitude: Double) {
        let mag = max(0, min(1, magnitude.isFinite ? magnitude : 0))
        stick = (x.isFinite ? max(-1, min(1, x)) : 0, y.isFinite ? max(-1, min(1, y)) : 0, mag)
        if mag == 0 || phase == .live { sendStick(force: mag == 0) }
    }
    func pressA() { command("hubA"); haptic() }
    func pressB() { command("hubB") }
    func emote(_ id: String) { command("hubEmote", mode: id); haptic() }
    func go(_ target: String) { command("hubGo", mode: target); haptic() }
    @ObservationIgnored private var lookSent = ""
    /// A locker change was saved: show it on the plaza hero now (LOCKER_LIVE).
    func lookChanged() {
        guard loaded, phase == .live || phase == .suspended, session.players.indices.contains(session.playerIndex) else { return }
        let signature = lookFields(session.players[session.playerIndex]).sorted { $0.key < $1.key }.map { "\($0.key)=\($0.value)" }.joined(separator: ";")
        guard signature != lookSent else { return }
        lookSent = signature; sendLook()
    }
    /// The locker's look reached the plaza hero (live, on every change).
    func sendLook() {
        guard loaded, session.players.indices.contains(session.playerIndex) else { return }
        var message = lookFields(session.players[session.playerIndex])
        message["version"] = 1; message["session"] = sessionID; message["action"] = "hubLook"
        sendJSON(message)
    }

    private func haptic() { if session.haptics { UIImpactFeedbackGenerator(style: .light).impactOccurred() } }

    private func lookFields(_ p: Player) -> [String: Any] {
        ["female": p.standardFemale, "skin": p.standardSkin, "skinHex": p.skinHex,
         "shirt": p.outfitHex("shirt") ?? "", "shorts": p.outfitHex("shorts") ?? "", "accent": p.outfitHex("accent") ?? "", "racket": p.outfitHex("racket") ?? ""]
    }

    private func sendStart() {
        let p = session.players[min(session.playerIndex, max(0, session.players.count - 1))]
        var message = lookFields(p)
        message.merge(["version": 1, "session": sessionID, "action": "start", "sport": "hub", "token": Int(token),
                       "sound": session.sound, "haptics": session.haptics, "touch": true, "external": true,
                       "fps": 60, "left": p.handedness == .left, "emotes": p.equippedEmotes]) { _, new in new }
        if let returnBay { message["bay"] = returnBay }
        sendJSON(message)
    }

    private func command(_ action: String, mode: String? = nil, remote: String? = nil) {
        guard phase == .live else { return }
        var message: [String: Any] = ["version": 1, "session": sessionID, "action": action]
        if let mode { message["mode"] = mode }
        if let remote { message["remote"] = remote }
        sendJSON(message)
    }
    #if DEBUG
    /// Tests: every message that would go to Unity.
    var debugSent: (([String: Any]) -> Void)?
    #endif
    private func sendJSON(_ object: [String: Any]) {
        #if DEBUG
        debugSent?(object)
        #endif
        guard let data = try? JSONSerialization.data(withJSONObject: object), let text = String(data: data, encoding: .utf8) else { return }
        SportsRuntime.shared().send(text)
    }

    /// One stick reading on the binary channel Unity polls every frame (no JSON, no allocation on the Unity side).
    private func sendStick(force: Bool = false) {
        guard loaded || force, token != 0 else { return }
        let now = SportsRuntime.shared().clock()
        let sample = SportsSample(version: Int32(SportsSampleVersion), session: token, time: now,
            target: Float(stick.x), power: Float(stick.mag), aim: Float(stick.y),
            swing: 0, swingStart: 0, swingAbort: 0, flags: Int32(SportsSampleValid),
            handSide: 0, lift: 0, strokeFacing: 0, qx: 0, qy: 0, qz: 0, qw: 0, rx: 0, ry: 0, rz: 0, gx: 0, gy: 0, gz: 0,
            onsetTime: 0, confirmationTime: 0, abortTime: 0)
        SportsRuntime.shared().push(sample)
        lastSampleSent = now
    }

    private func startTimer() {
        timer?.invalidate()
        // 60 Hz: the stick goes out on every tick while it is held (and a zero when it is let go); Unity's events come back here.
        let t = Timer(timeInterval: 1.0 / 60, repeats: true) { [weak self] _ in MainActor.assumeIsolated { self?.tick() } }
        RunLoop.main.add(t, forMode: .common); timer = t
    }

    private func tick() {
        guard !session.active else { timer?.invalidate(); timer = nil; return }
        partyTick()
        if stick.mag > 0 && phase == .live { sendStick() }
        if phase == .booting && Date().timeIntervalSince(bootStarted) > 25 { fail("The plaza did not open. Showing the classic menu."); return }
        for _ in 0..<64 {
            guard let json = SportsRuntime.shared().pollEvent(), let data = json.data(using: .utf8),
                  let event = (try? JSONSerialization.jsonObject(with: data)) as? [String: Any] else { break }
            receive(event)
        }
    }

    #if DEBUG
    /// Tests and snapshots: pretend the plaza is up (no Unity in the simulator) with this place and zone.
    func debugLive(place: String = "plaza", zone: String = "", session: String = "debug-hub") {
        sessionID = session; phase = .live; loaded = true; classicOverlay = false
        self.place = place; self.zone = HubZone(line: zone)
    }
    /// Tests: the boot message the plaza would send (no Unity in the simulator).
    func debugBoot(session: String) { sessionID = session; token = 7; phase = .booting; sendStart() }
    func debugPartyTick() { partyTick() }
    func debugReset() { timer?.invalidate(); timer = nil; phase = .off; loaded = false; classicOverlay = false; zone = nil; station = nil; returnBay = nil; place = "plaza"; notice = ""; lookSent = ""; debugSent = nil }
    #endif

    // MARK: party (PLAN §4: friends in your plaza, sitting together, Call party)

    static let partyBays: Set<String> = ["bay-tennis-online", "bay-golf-online"]
    /// The lobby the party lives in (tests inject their own service).
    @ObservationIgnored var partyService: MultiplayerService = .shared { didSet { installParty() } }
    /// The party while it is idle (in the plaza); nil once a match is on or with no lobby.
    var party: MultiplayerLobby? { partyService.lobby?.phase == .lobby ? partyService.lobby : nil }
    /// Where each friend is sitting (bay id), from their presence.
    private(set) var friendBays: [String: String] = [:]
    private(set) var lastCall: (from: String, bay: String)?
    @ObservationIgnored private var lastPose = ""
    @ObservationIgnored private var presenceSent = 0.0
    @ObservationIgnored private var rosterSent = ""
    @ObservationIgnored private var partyInstalled = false

    struct Presence: Codable { var place: String; var x, y, z, yaw, speed: Double; var bay: String; var emote: String }

    func installParty() {
        partyService.onHub = { [weak self] kind, payload, sender in self?.partyMessage(kind, payload: payload, from: sender) }
        partyInstalled = true
    }

    /// My pose (Unity's hubPose, ~10 Hz) to the party.
    private func sendPresence() {
        guard party != nil, loaded, !lastPose.isEmpty else { return }
        let now = ProcessInfo.processInfo.systemUptime; guard now - presenceSent >= 0.09 else { return }
        let f = lastPose.split(separator: "|", omittingEmptySubsequences: false).map(String.init)
        guard f.count >= 8, let x = Double(f[1]), let y = Double(f[2]), let z = Double(f[3]), let yaw = Double(f[4]), let speed = Double(f[5]) else { return }
        let p = Presence(place: f[0], x: x, y: y, z: z, yaw: yaw, speed: speed, bay: f[6], emote: f[7])
        guard let data = try? JSONEncoder().encode(p), let text = String(data: data, encoding: .utf8) else { return }
        presenceSent = now; partyService.sendHub("hubPresence", payload: text, reliable: false)
    }

    /// A friend's presence or event, from the lobby.
    func partyMessage(_ kind: String, payload: String, from sender: String) {
        guard let party, sender != partyService.localID else { return }
        switch kind {
        case "hubPresence":
            guard let p = try? JSONDecoder().decode(Presence.self, from: Data(payload.utf8)),
                  let index = party.participants.firstIndex(where: { $0.id == sender }) else { return }
            friendBays[sender] = p.bay.isEmpty ? nil : p.bay
            let who = party.participants[index], look = who.loadout
            var remote: [String: Any] = ["id": sender, "name": who.name, "colour": index, "female": who.female, "place": p.place,
                                         "x": p.x, "y": p.y, "z": p.z, "yaw": p.yaw, "speed": p.speed, "bay": p.bay, "emote": p.emote]
            remote["skinHex"] = look?.skinHex ?? ""
            remote["shirt"] = look?.shirtHex ?? look?.colours["shirt"] ?? ""
            remote["shorts"] = look?.shortsHex ?? look?.colours["shorts"] ?? ""
            remote["accent"] = look?.colours["accent"] ?? ""
            guard let data = try? JSONSerialization.data(withJSONObject: remote), let text = String(data: data, encoding: .utf8) else { return }
            command("hubRemote", remote: text)
        case "hubEvent":
            let f = payload.split(separator: "|", omittingEmptySubsequences: false).map(String.init)
            if f.first == "call", f.count > 1, Self.spotIDs.contains(f[1]) {
                lastCall = (sender, f[1]); command("hubCall", mode: f[1])
                let name = party.participants.first { $0.id == sender }?.name ?? "A friend"
                notice = "\(name) calls the party to \(f[1].contains("golf") ? "Golf" : "Tennis") · Online"
                if session.haptics { UINotificationFeedbackGenerator().notificationOccurred(.warning) }
            }
        default: break
        }
    }

    /// Host: Call party to the bay you sit in (everyone's TV shows the way, their phones buzz).
    func callParty() {
        guard party != nil, let bay = station, Self.partyBays.contains(bay) else { return }
        partyService.sendHub("hubEvent", payload: "call|" + bay, reliable: true); haptic()
    }
    /// Everyone in the party seated in this bay (you included) and ready: the host's phone starts the match.
    var partyAllSeated: Bool {
        guard let party, let bay = station else { return false }
        let others = party.participants.filter { $0.connected && !$0.isGuest && $0.id != partyService.localID }
        return others.allSatisfy { friendBays[$0.id] == bay }
    }
    var partyWaitingFor: [String] {
        guard let party, let bay = station else { return [] }
        return party.participants.filter { $0.connected && !$0.isGuest && $0.id != partyService.localID && friendBays[$0.id] != bay }.map(\.name)
    }
    func setPartyReady(_ ready: Bool) { try? partyService.setReady(ready); haptic() }
    /// Host: start now even if someone is still on their way (the plan's "or the host forces it").
    func forcePartyStart() { guard partyService.isOwner, partyService.lobby?.canStart == true else { return }; try? partyService.startMatch() }

    private func partyTick() {
        if !partyInstalled { installParty() }
        guard phase == .live else { return }
        // the member list: anyone who left walks off
        let ids = party?.participants.map(\.id).filter { $0 != partyService.localID }.sorted() ?? []
        let roster = ids.joined(separator: ",")
        if roster != rosterSent { rosterSent = roster; command("hubRoster", mode: roster); friendBays = friendBays.filter { ids.contains($0.key) } }
        // host: everyone sat down in my bay and is ready -> go
        if partyService.isOwner, let bay = station, Self.partyBays.contains(bay), partyAllSeated, partyService.lobby?.canStart == true { try? partyService.startMatch() }
    }

    /// One Unity event (also called directly by tests).
    func receive(_ event: [String: Any]) {
        let type = event["type"] as? String ?? ""
        if type == "boot" { if phase == .booting { sendStart() }; return }
        guard event["session"] as? String == sessionID else { return }
        let message = event["message"] as? String ?? ""
        switch type {
        case "ready":
            loaded = true
            if phase == .booting { phase = .live; SportsDiagnostics.write("hub ready after \(String(format: "%.2f", Date().timeIntervalSince(bootStarted))) s") }
            // back from a match on the same bench: its panel again (staying seated = rematch, B = stand up)
            if let bay = returnBay, station == nil { useStation(bay) }
            if wanted { reveal() } else { suspend() }
        case "hubPlace": place = message
        case "hubZone": zone = HubZone(line: message)
        case "hubAction": lastAction = message; useStation(message)
        case "hubPose": lastPose = message; sendPresence()
        case "hubState":
            let f = message.split(separator: "|", omittingEmptySubsequences: false).map(String.init)
            if f.count >= 5 { speed = Double(f[2]) ?? 0; inputAge = (Double(f[3]) ?? 0, Double(f[4]) ?? 0) }
        case "emoteResult": notice = message
        case "error": if phase == .booting { fail(message) } else { notice = message }
        default: break
        }
    }
}
