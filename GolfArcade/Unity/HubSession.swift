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
        case .door: detail == "Coming soon" ? "\(label) · coming soon" : detail == "TV needed" ? "\(label) · connect a TV (AirPlay) to play" : "\(label) · walk in"
        case .station: "\(label) · A to use"
        case .bay: "\(label) · A to sit"
        }
    }
}

/// The walkable menu (PLAN_MenuHub_WalkableWorld): the Plaza in Unity is the game's only menu and the phone is its controller, an
/// analog stick. This owns that session: when to open the plaza, the runtime start, the stick stream (the binary sample channel:
/// target = x, aim = y, power = deflection), the buttons, and Unity's reports of where the player is.
///
/// With a TV the plaza is on the TV. With none it is on the phone itself, in the top part of the screen above the same controls,
/// and the PLAY hall is shut (a match needs a TV; connecting one moves the plaza onto it). It never runs during a match
/// (SportsSession owns the runtime then) or first-run onboarding. A station's own screen (the locker, settings, a bay's match
/// options) opens as a panel on the phone; nothing else ever replaces the controller: no lobby screens, no Game Center sheets.
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
    /// Unity's measured age of the stick samples it applied (p50, p95), milliseconds.
    private(set) var inputAge = (p50: 0.0, p95: 0.0)
    private(set) var speed = 0.0
    private(set) var sessionID = ""
    private var token: Int32 = 0
    /// The plaza scene is what Unity has loaded right now (false once a match replaced it).
    private(set) var loaded = false
    /// The TV's loading bar while the plaza boots, 0...1: the runtime starting, Unity's scene load, the plaza's build (Unity's
    /// loadProgress / buildProgress); 1 = ready, then the loading screen fades into the arrival fly-in.
    private(set) var loadProgress = 0.0
    /// What the loading bar is doing, from its progress.
    var loadStage: String {
        loadProgress >= 1 ? "Ready! Into the Plaza" : loadProgress >= 0.95 ? "Opening the gates"
            : loadProgress >= 0.72 ? "Getting your player ready" : loadProgress >= 0.12 ? "Building the Plaza" : "Starting up"
    }
    /// Seconds the full bar holds before the loading screen fades, and the fade (the fly-in starts with it).
    static let readyHold = 0.45, revealFade = 0.35
    private func progress(_ value: Double) { if value.isFinite { loadProgress = max(loadProgress, min(1, value)) } }
    @ObservationIgnored private var timer: Timer?
    @ObservationIgnored private var stick = (x: 0.0, y: 0.0, mag: 0.0)
    @ObservationIgnored private var lastSampleSent = 0.0
    @ObservationIgnored private var bootStarted = Date.distantPast
    @ObservationIgnored private var windowRetries = 0

    private var session: SportsSession { .shared }
    private var menu: TennisMenu { .shared }

    /// Should the plaza be up right now? Whenever no match has the runtime: on the TV with one connected, on the phone without.
    var wanted: Bool {
        !session.active && !OnboardingFlow.shared.holdsMenu && !SportsSession.benchmark && phase != .failed && !menu.classic && !runtimeMissing
    }
    /// The plaza is on this phone's own screen (no TV): it draws above the controls (HubControllerView's phone layout).
    private(set) var onPhone = false
    /// The share of the phone's height the plaza takes on a phone with no TV (Unity's HubWorld.PhoneViewShare is the same).
    static let phoneViewShare = 0.56
    /// No Unity in this build (the simulator): the plaza cannot open at all.
    private(set) var runtimeMissing = false
    /// A station's own screen is open on the phone (the locker, settings, a bay's match options). Never a lobby or home screen.
    var panelOpen: Bool { station != nil && Self.panelScreens(menu.screen) }
    /// The phone is the plaza's controller (the plaza is opening, on, or could not open and offers Try again).
    var drivesPhone: Bool { wanted && (phase == .booting || phase == .live) }
    /// Screens a station's panel may show. Anything else (home, the old play / online / lobby screens) ends the station.
    static func panelScreens(_ screen: MenuScreen) -> Bool {
        switch screen {
        case .character, .settings, .howTo, .campaign, .training, .exhibition, .localPlayers, .map, .loading, .story, .results, .postMatch: true
        default: false
        }
    }

    /// Called whenever something the decision depends on changes (display, match, menu screen, settings).
    func evaluate() {
        // the panel's own Back / Done went past the station's screens (home, or an old menu screen): the station is finished
        if let s = station, !Self.partyBays.contains(s), !Self.panelScreens(menu.screen) {
            station = nil; returnBay = nil; command("hubB")
            if menu.screen != .main && menu.screen != .title { menu.openFromPlaza(.main) }
        }
        // the plaza never shows a lobby screen: a party lives in the plaza itself
        if drivesPhone, menu.screen.isOnline { menu.openFromPlaza(.main) }
        if wanted { enter() } else if phase == .live || phase == .booting { suspend() }
    }

    // MARK: lifecycle

    private func enter() {
        let wantPhone = !session.displayConnected
        // a TV was connected or went away: the plaza moves to the screen there is now (it boots again there)
        if phase != .off, phase != .failed, onPhone != wantPhone {
            SportsDiagnostics.write("hub moves to the \(wantPhone ? "phone" : "TV")")
            if loaded && !session.active { sendJSON(["version": 1, "session": sessionID, "action": "end"]) }
            timer?.invalidate(); timer = nil; phase = .off; loaded = false; zone = nil; station = nil; arriving = false
            if onPhone { SportsDisplays.shared.endPhonePlaza() }
        }
        switch phase {
        case .live: reveal(); return
        case .booting: return
        case .suspended where loaded:
            SportsRuntime.shared().pause(false); phase = .live; reveal(); startTimer(); sendLook(); return
        default: break
        }
        onPhone = wantPhone
        guard let window = onPhone ? SportsDisplays.shared.phonePlazaWindow() : SportsDisplays.shared.external else {
            // (at launch the app's own window may not be registered yet: look again shortly)
            if onPhone, windowRetries < 20 { windowRetries += 1; DispatchQueue.main.asyncAfter(deadline: .now() + 0.25) { [weak self] in self?.evaluate() } }
            return
        }
        windowRetries = 0
        phase = .booting; bootStarted = Date(); notice = ""; loadProgress = 0; progress(0.03); revealedOnPhone = false
        sessionID = UUID().uuidString; token = Int32.random(in: 1...Int32.max); loaded = false
        place = "plaza"; zone = nil; lastAction = nil
        if onPhone { returnBay = nil }   // (no benches without a TV)
        SportsDiagnostics.write("hub boot session=\(sessionID) on \(onPhone ? "phone" : "TV")")
        // A display cycle first: the first Unity start blocks the main thread while it boots.
        let launch = sessionID, phone = onPhone
        DispatchQueue.main.asyncAfter(deadline: .now() + 0.05) { [weak self] in
            guard let self, self.sessionID == launch, self.phase == .booting, self.wanted else { return }
            do { try SportsRuntime.shared().load(in: window, controllerReplica: phone); RacketFeel.apply() }
            catch {
                // no Unity in this build (the simulator): there is no plaza to show at all
                if (error as NSError).domain == "SportsRuntime" && (error as NSError).code == 1 { self.runtimeMissing = true }
                if phone { SportsDisplays.shared.endPhonePlaza() }
                self.fail(error.localizedDescription); return
            }
            if phone { SportsDisplays.shared.showPhonePlaza() }
            self.progress(0.12)
            SportsRuntime.shared().pause(false)
            self.startTimer()
            self.sendStart()
        }
    }

    /// A match takes the runtime: stop walking and idle Unity (the match's own screens take over).
    private func suspend() {
        setStick(x: 0, y: 0, magnitude: 0)
        if phase == .booting { phase = .off; timer?.invalidate(); timer = nil; if onPhone { SportsDisplays.shared.endPhonePlaza() }; return }
        phase = .suspended; timer?.invalidate(); timer = nil
        if onPhone { SportsDisplays.shared.endPhonePlaza() } else { SportsDisplays.shared.showMenu() }
        if !session.active { SportsRuntime.shared().pause(true) }
    }

    /// The bay you launched from: back from the match, you sit on its bench again (RETURN).
    private(set) var returnBay: String?

    // MARK: after an online match (a party's, or a quick match from an online bench)

    /// The match's result is on the TV: the phone offers Back to the Plaza (the line says who won). Nil otherwise.
    private(set) var matchOver: String?
    func onlineMatchFinished(_ line: String) { matchOver = line; searching = false }
    /// Back to the Plaza from an online match, at its end or mid-match (Exit). A party goes back to its bench together (staying
    /// seated and ready again is the rematch); a quick match with other players is left.
    func leaveOnlineMatch(_ lobby: MultiplayerService? = nil) {
        matchOver = nil
        let service = lobby ?? partyService
        guard service.lobby != nil else { if session.multiplayerMatchID != nil { session.end() }; return }
        if service.isParty, service.lobby?.phase != .interrupted {
            service.endPartyMatch()
            // a guest whose host has gone cannot be brought back by the host: leave after a moment
            DispatchQueue.main.asyncAfter(deadline: .now() + 3) { [weak self] in
                guard let self, self.session.multiplayerMatchID != nil else { return }
                service.leave(); self.session.end()
            }
        } else { service.leave(); if session.multiplayerMatchID != nil { session.end() } }
    }
    /// The online match stopped on this phone (its lobby is back in the plaza, or it was left).
    func onlineMatchEnded() { matchOver = nil; if menu.screen != .main { menu.openFromPlaza(.main) } }
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

    /// The TV went away: the plaza on it is gone (it opens again on the phone).
    func displayLost() {
        guard !onPhone else { return }
        timer?.invalidate(); timer = nil
        if loaded && !session.active { sendJSON(["version": 1, "session": sessionID, "action": "end"]) }
        phase = .off; loaded = false; zone = nil; station = nil; returnBay = nil; arriving = false
    }

    private func fail(_ message: String) {
        SportsDiagnostics.write("hub failed: \(message)")
        timer?.invalidate(); timer = nil; loaded = false
        phase = .failed; notice = message
        if !onPhone { SportsDisplays.shared.showMenu() }   // (the TV says so; the phone offers Try again)
    }
    /// Try the plaza again after a failure.
    func retry() { if phase == .failed { phase = .off; notice = ""; evaluate() } }

    /// On a phone with no TV: the loading screen has faded and the plaza shows above the controls.
    private(set) var revealedOnPhone = false
    private func reveal() {
        guard phase == .live, !arriving else { return }
        if onPhone { revealedOnPhone = true } else { SportsDisplays.shared.external?.isHidden = true }
    }
    /// The loading screen fading into the arrival (between the full bar and the plaza on the TV).
    private(set) var arriving = false
    /// A fresh boot reached 100 %: the full bar holds a moment, then the loading screen fades off the TV as Unity's camera flies in
    /// from high over the plaza to behind the hero (hubArrive).
    /// The friends-list prompt (once ever), once the player is in the plaza: Apple only lists friends who both allowed it.
    private func askForFriendsOnce() { ClubSignIn.askForFriendsOnce() }
    private func arrive() {
        arriving = true
        let launch = sessionID
        DispatchQueue.main.asyncAfter(deadline: .now() + Self.readyHold) { [weak self] in
            guard let self, self.sessionID == launch else { return }
            self.command("hubArrive")
            if self.onPhone {
                withAnimation(.easeOut(duration: Self.revealFade)) { self.revealedOnPhone = true }
                DispatchQueue.main.asyncAfter(deadline: .now() + Self.revealFade) { self.arriving = false; self.askForFriendsOnce() }
                return
            }
            guard self.phase == .live, self.wanted, let tv = SportsDisplays.shared.external else { self.arriving = false; return }
            UIView.animate(withDuration: Self.revealFade, animations: { tv.alpha = 0 }) { _ in
                tv.isHidden = true; tv.alpha = 1; self.arriving = false; self.askForFriendsOnce()
            }
        }
    }

    /// A menu screen on the phone over the plaza (☰ shortcuts, the Party button), with `taps` made on it.
    func openPanel(_ screen: MenuScreen, taps: [String] = []) {
        menu.openFromPlaza(screen)
        for tap in taps { menu.tap(tap) }
        evaluate()
    }

    // MARK: stations and bays (phase 2: each opens the existing menu screen for it on the phone)

    /// What a station or bay opens: a menu screen (as a panel on the phone, with `taps` made on it), or an action.
    struct Route { var screen: MenuScreen?; var taps: [String] = []; var action: (@MainActor () -> Void)? = nil }
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
        case "board-invite": Route(action: { HubSession.shared.inviteFriends() })
        // every bench is one sport and one mode, straight to its own screen
        case "bay-tennis-exhibition": Route(screen: .exhibition)
        case "bay-tennis-training": Route(screen: .training)
        case "bay-golf-round": Route(action: { TennisMenu.shared.openFromPlaza(.main); TennisMenu.shared.begin(MenuLaunch(sport: .golf, mode: .round)) })   // the course picker
        case "bay-golf-pass": Route(screen: .localPlayers)   // how many players, then straight into the round
        // the online benches: with your party, its bench (that sport); alone, a quick match with other players of that sport
        case "bay-tennis-online": Route(action: { HubSession.shared.useOnlineBench(.tennis) })
        case "bay-golf-online": Route(action: { HubSession.shared.useOnlineBench(.golf) })
        default: nil
        }
    }

    /// Unity confirmed A at a spot (hubAction): open its panel.
    func useStation(_ id: String) {
        if Self.partyBays.contains(id), let party {
            // a party sits together on an online bench: the bench's sport becomes the party's (host), the phone shows who is here.
            // Sitting is being ready: when everyone playing has sat down the match starts.
            station = id
            let sport: MultiplayerSport = id.contains("golf") ? .golf : .tennis
            if partyService.isOwner, party.sport != sport { try? partyService.configure(sport, venue: sport == .golf ? session.golfCourse : session.tennisVenue) }
            haptic(); evaluate(); return
        }
        guard let route = Self.route(id) else { command("hubB"); return }
        if let action = route.action {
            // a bench's action keeps you seated on it (its panel is the course picker / the search); a station's just happens
            if id.hasPrefix("bay-") { station = id; action(); evaluate() } else { action(); command("hubB") }
            return
        }
        if let screen = route.screen { menu.openFromPlaza(screen) }
        for tap in route.taps { menu.tap(tap) }
        station = id
        evaluate()
    }

    // MARK: quick match from an online bench (no party): other players of that sport, found by Game Center with no screen

    /// Sitting on an online bench with no party: that bench's sport (its panel finds other players of it). Nil otherwise.
    private(set) var onlineBench: MultiplayerSport?
    /// Game Center is looking for players for the online bench (it shows no screen of its own).
    private(set) var searching = false
    @ObservationIgnored private var searchTask: Task<Void, Never>?
    @ObservationIgnored private var returning = false
    /// An online bench with no party: look for players of its sport at once (not when you are put back on it after a match).
    func useOnlineBench(_ sport: MultiplayerSport) { onlineBench = sport; if !returning { findPlayers() } }
    func findPlayers() {
        guard party == nil, let sport = onlineBench else { return }
        guard partyService.authenticated else { notice = "Sign in to Game Center to play online (Friends › Sign in)."; return }
        searching = true; notice = ""
        searchTask?.cancel()
        searchTask = Task { @MainActor [weak self] in
            guard let self else { return }
            TennisMenu.shared.online.identity(TennisMenu.shared)
            self.partyService.setScreen(self.session.displayConnected)
            // (a party of one, waiting for an invite to be answered, is given up for the quick match)
            if self.partyService.isParty { ClubHub.shared.leaveParty() } else if self.partyService.lobby != nil { self.partyService.leave() }
            do { try await self.partyService.quickMatch(sport, golfPlayers: 2) }
            catch is CancellationError {}
            catch {
                guard self.searching, self.onlineBench == sport else { return }
                self.searching = false; self.notice = error.localizedDescription
            }
        }
    }
    func stopSearching() {
        searchTask?.cancel(); searchTask = nil
        if searching { searching = false; partyService.cancelSearch() }
        if !partyService.isParty, partyService.lobby?.phase == .lobby { partyService.leave() }
    }
    /// B, Done or Leave seat: back to walking.
    func leaveStation() {
        guard station != nil || panelOpen else { return }
        if onlineBench != nil { stopSearching(); onlineBench = nil }
        if station != nil { station = nil; returnBay = nil; command("hubB") }
        if menu.screen != .main && menu.screen != .title { menu.openFromPlaza(.main) }
        evaluate()
    }
    /// Friends: the Friends list over the controller (who is online, one tap to invite anyone, invites you got). It never
    /// opens Game Center or a lobby screen. `afterSheet`: called as one of the controller's own sheets (☰) closes.
    func inviteFriends(afterSheet: Bool = false) {
        haptic()
        if afterSheet { DispatchQueue.main.asyncAfter(deadline: .now() + 0.45) { [weak self] in self?.presentInvite() } }
        else { presentInvite() }
    }
    /// What Friends does: opens the Friends list. Tests count the calls instead.
    @ObservationIgnored var presentInvite: @MainActor () -> Void = { ClubHub.shared.hubOpen = true; ClubHub.shared.start() }
    /// Joined a friend's party: your hero goes to stand beside theirs (as soon as their position arrives).
    @ObservationIgnored private var meetHost: String?
    func joinedParty(host: String) { meetHost = host; station = nil; returnBay = nil; if menu.screen != .main { menu.openFromPlaza(.main) } }
    /// A short message on the TV in the plaza (an invite arrived, a friend is joining): Unity draws it over the plaza.
    func toast(_ text: String) { command("hubToast", mode: text) }
    /// ☰ shortcut: walk there (a quick fade) and use it.
    func openStation(_ id: String) { command("hubUse", mode: id); haptic() }
    func backToPlaza() {
        if phase == .failed { retry(); return }
        // a party's lobby stays: the plaza is where it lives now
        if menu.screen != .main && menu.screen != .title { menu.openFromPlaza(.main) }
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
        // friends in your plaza see the change too (your party look)
        if party != nil { menu.online.identity(menu) }
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
                       "sound": session.sound, "haptics": session.haptics, "touch": true, "external": !onPhone, "phoneOnly": onPhone,
                       "fps": 60, "left": p.handedness == .left, "emotes": p.equippedEmotes]) { _, new in new }
        if let returnBay, !onPhone { message["bay"] = returnBay }
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
    func debugLive(place: String = "plaza", zone: String = "", session: String = "debug-hub", onPhone: Bool = false) {
        sessionID = session; phase = .live; loaded = true; self.onPhone = onPhone; revealedOnPhone = onPhone; runtimeMissing = false
        self.place = place; self.zone = HubZone(line: zone)
    }
    /// Tests: the boot message the plaza would send (no Unity in the simulator).
    func debugBoot(session: String, onPhone: Bool = false) { sessionID = session; token = 7; phase = .booting; loadProgress = 0; self.onPhone = onPhone; sendStart() }
    func debugPartyTick() { partyTick() }
    /// Snapshots: sitting on an online bench (searching or not), no Game Center.
    func debugOnlineBench(_ sport: MultiplayerSport, searching: Bool) { station = sport == .golf ? "bay-golf-online" : "bay-tennis-online"; onlineBench = sport; self.searching = searching }
    func debugReset() { timer?.invalidate(); timer = nil; phase = .off; loaded = false; loadProgress = 0; arriving = false; zone = nil; station = nil; returnBay = nil; place = "plaza"; notice = ""; lookSent = ""; debugSent = nil
        onPhone = false; revealedOnPhone = false; runtimeMissing = false; searching = false; onlineBench = nil; searchTask?.cancel(); meetHost = nil; matchOver = nil; partyStartAt = nil
        presentInvite = { ClubHub.shared.hubOpen = true; ClubHub.shared.start() } }
    #endif

    // MARK: party (PLAN §4: friends in your plaza, sitting together, Call party)

    static let partyBays: Set<String> = ["bay-tennis-online", "bay-golf-online"]
    /// The lobby the party lives in (tests inject their own service).
    @ObservationIgnored var partyService: MultiplayerService = .shared { didSet { installParty() } }
    /// The friends' party while it is idle (in the plaza) and someone has joined you; nil once a match is on, alone (an invite
    /// still on its way), or in a quick match's lobby.
    var party: MultiplayerLobby? {
        guard partyService.isParty, let lobby = partyService.lobby, lobby.phase == .lobby, lobby.participants.filter({ !$0.isGuest }).count >= 2 else { return nil }
        return lobby
    }
    /// Where each friend is sitting (bay id), and the place they are in, from their presence.
    private(set) var friendBays: [String: String] = [:]
    private(set) var friendPlaces: [String: String] = [:]
    private(set) var lastCall: (from: String, bay: String)?
    @ObservationIgnored private var lastPose = ""
    @ObservationIgnored private var presenceSent = 0.0
    @ObservationIgnored private var presenceKey = (place: "", bay: "")
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
        let f = lastPose.split(separator: "|", omittingEmptySubsequences: false).map(String.init)
        guard f.count >= 8, let x = Double(f[1]), let y = Double(f[2]), let z = Double(f[3]), let yaw = Double(f[4]), let speed = Double(f[5]) else { return }
        let p = Presence(place: f[0], x: x, y: y, z: z, yaw: yaw, speed: speed, bay: f[6], emote: f[7])
        // ~10 a second, but sitting down, standing up or going through a door goes out at once (and reliably): the bench and the
        // friends' plazas must never miss it
        let now = ProcessInfo.processInfo.systemUptime, changed = p.bay != presenceKey.bay || p.place != presenceKey.place
        guard changed || now - presenceSent >= 0.09 else { return }
        guard let data = try? JSONEncoder().encode(p), let text = String(data: data, encoding: .utf8) else { return }
        presenceSent = now; presenceKey = (p.place, p.bay); partyService.sendHub("hubPresence", payload: text, reliable: changed)
    }

    /// A friend's presence or event, from the lobby.
    func partyMessage(_ kind: String, payload: String, from sender: String) {
        guard let party, sender != partyService.localID else { return }
        switch kind {
        case "hubPresence":
            guard let p = try? JSONDecoder().decode(Presence.self, from: Data(payload.utf8)),
                  let index = party.participants.firstIndex(where: { $0.id == sender }) else { return }
            friendBays[sender] = p.bay.isEmpty ? nil : p.bay
            friendPlaces[sender] = p.place
            let who = party.participants[index], look = who.loadout
            var remote: [String: Any] = ["id": sender, "name": who.name, "colour": index, "female": who.female, "place": p.place,
                                         "x": p.x, "y": p.y, "z": p.z, "yaw": p.yaw, "speed": p.speed, "bay": p.bay, "emote": p.emote]
            remote["skinHex"] = look?.skinHex ?? ""
            remote["shirt"] = look?.shirtHex ?? look?.colours["shirt"] ?? ""
            remote["shorts"] = look?.shortsHex ?? look?.colours["shorts"] ?? ""
            remote["accent"] = look?.colours["accent"] ?? ""
            guard let data = try? JSONSerialization.data(withJSONObject: remote), let text = String(data: data, encoding: .utf8) else { return }
            command("hubRemote", remote: text)
            // just joined their party: go and stand beside them
            if meetHost == sender { meetHost = nil; command("hubMeet", mode: sender) }
        case "hubEvent":
            let f = payload.split(separator: "|", omittingEmptySubsequences: false).map(String.init)
            if f.first == "starting", f.count > 1 {
                notice = "Everyone's on the bench · starting \(f[1].contains("golf") ? "golf" : "tennis")…"
                if session.haptics { UINotificationFeedbackGenerator().notificationOccurred(.success) }
            }
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
    /// Everyone playing is here: the bench holds at least two of the party, and every friend who is in the play rooms (tennis or
    /// golf, so on their way) has sat down too. Friends elsewhere in the plaza (the locker, phone only) do not hold the match up.
    var partyAllSeated: Bool {
        guard let party, let bay = station else { return false }
        let others = party.participants.filter { $0.connected && !$0.isGuest && $0.id != partyService.localID }
        let seated = others.filter { friendBays[$0.id] == bay }
        return !seated.isEmpty && partyWaitingFor.isEmpty
    }
    /// Friends in the PLAY hall or a sport room who have not sat on this bench yet.
    var partyWaitingFor: [String] {
        guard let party, let bay = station else { return [] }
        return party.participants.filter { $0.connected && !$0.isGuest && $0.id != partyService.localID && friendBays[$0.id] != bay
                                           && ["play", "tennis", "golf"].contains(friendPlaces[$0.id] ?? "") }.map(\.name)
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
        if roster != rosterSent { rosterSent = roster; command("hubRoster", mode: roster); friendBays = friendBays.filter { ids.contains($0.key) }; friendPlaces = friendPlaces.filter { ids.contains($0.key) } }
        // host: the seats are the people on this bench (whoever is elsewhere in the plaza stays there); sitting is being ready.
        // When everyone playing has sat down, a short count and the match starts on every phone.
        if partyService.isOwner, let bay = station, Self.partyBays.contains(bay), let lobby = party {
            let me = partyService.localID
            partyService.seatParty(lobby.participants.filter { !$0.isGuest && ($0.id == me || friendBays[$0.id] == bay) }.map(\.id))
            if partyAllSeated, partyService.lobby?.canStart == true {
                let now = ProcessInfo.processInfo.systemUptime
                if partyStartAt == nil { partyStartAt = now + Self.partyCountdown; partyService.sendHub("hubEvent", payload: "starting|" + bay, reliable: true) }
                if let at = partyStartAt, now >= at { partyStartAt = nil; try? partyService.startMatch() }
            } else { partyStartAt = nil }
        } else { partyStartAt = nil }
    }
    /// Seconds between everyone sitting down and the match starting (the panels count down).
    static var partyCountdown = 2.5
    /// When the host's phone starts the party's match (everyone seated); nil while waiting.
    private(set) var partyStartAt: Double?

    /// One Unity event (also called directly by tests).
    func receive(_ event: [String: Any]) {
        let type = event["type"] as? String ?? ""
        if type == "boot" { if phase == .booting { sendStart() }; return }
        guard event["session"] as? String == sessionID else { return }
        let message = event["message"] as? String ?? ""
        switch type {
        case "loadProgress": progress(0.12 + 0.18 * (Double(message) ?? 0))
        case "buildProgress": progress(0.30 + 0.65 * (Double(message) ?? 0))
        case "ready":
            loaded = true; progress(1)
            let fresh = phase == .booting && returnBay == nil
            if phase == .booting { phase = .live; SportsDiagnostics.write("hub ready after \(String(format: "%.2f", Date().timeIntervalSince(bootStarted))) s") }
            // back from a match on the same bench: its panel again (staying seated = rematch, B = stand up)
            if let bay = returnBay, station == nil { returning = true; useStation(bay); returning = false }
            if !wanted { suspend() } else if fresh { arrive() } else { reveal() }
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
