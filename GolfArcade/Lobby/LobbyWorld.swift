import SceneKit
import SwiftUI
import UIKit
import simd

/// One person's position in the lobby, as it goes over the network (ten times a second, never held up for a late one).
struct LobbyPoseMessage: Codable, Sendable, Equatable {
    var id: String
    var x: Float, y: Float
    /// 0 = north, +π/2 = east.
    var h: Float
    /// Metres per second, so the other phone can play Walk or Run.
    var s: Float
}

/// A place the Quick Menu can send you.
struct LobbyQuickEntry: Identifiable, Equatable {
    var id: String          // the station it drops you at
    var title: String
    var detail: String
    var icon: String
    var locked: Bool
}

/// The walkable lobby: the scene, the people in it, and the rules for moving through it. One instance; the phone or the TV shows it, whichever the player is looking at.
@MainActor @Observable final class LobbyWorld {
    static let shared = LobbyWorld()
    typealias P = LobbyLayout.P

    /// The classic menus are always one switch away (Settings → Display) and take over by themselves if the world cannot load.
    static var enabled: Bool {
        get { UserDefaults.standard.object(forKey: "lobby.world") as? Bool ?? true }
        set { UserDefaults.standard.set(newValue, forKey: "lobby.world") }
    }
    /// True while the world can be shown (switched on and it loaded).
    static var available: Bool { enabled && !shared.failed }

    let layout = LobbyLayout.standard

    // MARK: what the screens read
    private(set) var failed = false
    private(set) var nearStation: LobbyLayout.Station?
    private(set) var party: [MultiplayerParticipant] = []
    var quickMenuOpen = false
    var quickIndex = 0
    var emoteMenuOpen = false
    /// Set while the player is steering (a joystick under the thumb, or the remote's arrows): the screens draw the joystick from this.
    private(set) var steering = false

    let quickEntries: [LobbyQuickEntry] = [
        LobbyQuickEntry(id: "locker-colors", title: "Locker", detail: "Gear, colors, emotes", icon: "tshirt.fill", locked: false),
        LobbyQuickEntry(id: "gate-tennis", title: "Tennis", detail: "Play Gate", icon: "tennisball.fill", locked: false),
        LobbyQuickEntry(id: "gate-golf", title: "Golf", detail: "Play Gate", icon: "flag.fill", locked: false),
        LobbyQuickEntry(id: "gate-howto", title: "How to Play", detail: "The guide", icon: "questionmark.circle.fill", locked: false),
        LobbyQuickEntry(id: "party-board", title: "Party Terrace", detail: "Play with friends", icon: "person.3.fill", locked: false),
        LobbyQuickEntry(id: "shop", title: "Pro Shop", detail: "Coming soon", icon: "lock.fill", locked: true),
        LobbyQuickEntry(id: "settings", title: "Settings", detail: "Controls, sound, the classic menus", icon: "gearshape.fill", locked: false),
    ]

    // MARK: the scene
    @ObservationIgnored private(set) var lobbyScene: LobbyScene?
    @ObservationIgnored private var local: LobbyAvatar?
    @ObservationIgnored private var remotes: [String: Remote] = [:]
    private struct Remote { var avatar: LobbyAvatar; var position: P; var heading: Float; var speed: Float; var target: P; var targetHeading: Float; var targetSpeed: Float; var lastHeard: Double }

    // MARK: the walker
    @ObservationIgnored private(set) var position: P
    @ObservationIgnored private(set) var heading: Float
    @ObservationIgnored private var velocity = P(0, 0)
    @ObservationIgnored private var walkSpeed: Float = 0
    @ObservationIgnored private var stick = P(0, 0)
    @ObservationIgnored private var tapTarget: P?
    @ObservationIgnored private var cameraYaw: Float
    @ObservationIgnored private var cameraCentre = P(0, 0)
    @ObservationIgnored private var link: CADisplayLink?
    @ObservationIgnored private var lastTick = 0.0
    @ObservationIgnored private var lastSent = 0.0
    @ObservationIgnored private var lastPartySync = 0.0
    @ObservationIgnored private var clock = 0.0
    @ObservationIgnored private var landscape = false
    /// The gate you went through to a match (or the terrace, for a friends match): where you stand when it is over.
    @ObservationIgnored var returnStation: String?
    /// The station whose menu is open: if a match starts from it, this is where you come back to.
    @ObservationIgnored private(set) var lastGate: String?
    func matchStarted() { returnStation = lastGate }
    /// True when something else drives `advance(now:)` (a proof run in a simulator that is not on screen, where a display link never fires).
    @ObservationIgnored var externalClock = false
    @ObservationIgnored private var heldArrows: Set<Int> = []
    @ObservationIgnored private var lastQuickStep = 0.0
    /// For tests and proof recordings: how many ticks have run.
    @ObservationIgnored private(set) var ticks = 0

    private init() {
        position = layout.spawn; heading = layout.spawnHeading; cameraYaw = layout.spawnHeading
        #if DEBUG
        if let y = ProcessInfo.processInfo.environment["LOBBY_HEADING"], let h = Float(y) { heading = h }
        if let id = ProcessInfo.processInfo.environment["LOBBY_STATION"], let s = layout.station(id) { position = layout.arrival(at: s); heading = s.heading; cameraYaw = s.heading }
        #endif
        cameraCentre = layout.spawn
    }

    // MARK: lifecycle
    /// Build the scene (once). False when it could not be built, and the classic menus carry on.
    @discardableResult func prepare() -> Bool {
        if lobbyScene != nil { return true }
        guard !failed else { return false }
        guard MatchHero.asset(female: false) != nil || MatchHero.asset(female: true) != nil else { failed = true; return false }
        lobbyScene = LobbyScene(layout: layout)
        #if DEBUG
        // look-at-it helpers for screenshots: a mock party (LOBBY_MOCK=<people>), an emote after two seconds (LOBBY_EMOTE=<slot>)
        if let n = ProcessInfo.processInfo.environment["LOBBY_MOCK"].flatMap(Int.init), let player = TennisMenu.shared.player { try? MultiplayerService.shared.enableMock(count: n, player: player) }
        if let slot = ProcessInfo.processInfo.environment["LOBBY_EMOTE"].flatMap(Int.init) {
            Task { @MainActor [weak self] in try? await Task.sleep(for: .seconds(3)); self?.playEmote(slot: slot) }
        }
        #endif
        MultiplayerService.shared.onLobbyPose = { [weak self] pose in self?.receivePose(pose) }
        refreshLocal()
        applyPose(instant: true)
        return true
    }

    /// The world is the screen: start moving the clock and put people where they belong.
    func appear(menu: TennisMenu = .shared) {
        guard prepare() else { menu.worldUnavailable(); return }
        quickMenuOpen = false; emoteMenuOpen = false
        if let id = returnStation, let station = layout.station(id) {
            var spot = layout.returnPoint(from: station)
            if station.action == .party { spot.position = partySpot(of: MultiplayerService.shared.localID) }
            position = spot.position; heading = spot.heading; cameraYaw = spot.heading; cameraCentre = spot.position
            returnStation = nil
        }
        refreshLocal()
        syncParty(force: true)
        applyPose(instant: true)
        guard link == nil, !externalClock else { return }
        let l = CADisplayLink(target: LinkTarget { [weak self] link in self?.tick(link) }, selector: #selector(LinkTarget.fire(_:)))
        l.preferredFrameRateRange = CAFrameRateRange(minimum: 30, maximum: 60, preferred: 60)
        l.add(to: .main, forMode: .common); link = l; lastTick = 0
    }
    func disappear() {
        link?.invalidate(); link = nil
        stick = P(0, 0); heldArrows.removeAll(); steering = false; tapTarget = nil; velocity = P(0, 0); walkSpeed = 0
    }
    var isRunning: Bool { link != nil }

    private final class LinkTarget: NSObject {
        let action: @MainActor (CADisplayLink) -> Void
        init(_ action: @escaping @MainActor (CADisplayLink) -> Void) { self.action = action }
        @MainActor @objc func fire(_ link: CADisplayLink) { action(link) }
    }

    // MARK: input
    /// The joystick, or the remote's arrows: x to the right, y forward (away from the camera), length 0...1.
    func setStick(_ v: SIMD2<Float>) {
        let l = simd_length(v)
        stick = l > 1 ? v / l : v
        steering = simd_length(stick) > 0.05
        if steering { tapTarget = nil }
    }
    /// Tap where you want to go.
    func walk(to p: P) {
        guard layout.isWalkable(p) || layout.walkable.contains(where: { $0.contains(p) }) else {
            // snap a tap outside the paths to the nearest walkable ground along the way
            if let q = nearestWalkable(to: p) { tapTarget = q }
            return
        }
        tapTarget = p
    }
    private func nearestWalkable(to p: P) -> P? {
        var best: P?, bestD = Float.infinity
        var x = -30.0 as Float
        while x <= 30 { var y = -30.0 as Float
            while y <= 34 { let q = P(x, y); if layout.isWalkable(q) { let d = simd_length(q - p); if d < bestD { bestD = d; best = q } }; y += 1 }
            x += 1 }
        return bestD < 6 ? best : nil
    }
    /// The remote's four arrows (0 up, 1 down, 2 left, 3 right): hold to walk, or move through the Quick Menu while it is open.
    func arrow(_ index: Int, down: Bool) {
        if quickMenuOpen {
            guard down else { return }
            if index == 0 { quickIndex = (quickIndex + quickEntries.count - 1) % quickEntries.count }
            if index == 1 { quickIndex = (quickIndex + 1) % quickEntries.count }
            return
        }
        if down { heldArrows.insert(index) } else { heldArrows.remove(index) }
        var v = P(0, 0)
        if heldArrows.contains(0) { v.y += 1 }; if heldArrows.contains(1) { v.y -= 1 }
        if heldArrows.contains(2) { v.x -= 1 }; if heldArrows.contains(3) { v.x += 1 }
        setStick(v)
    }
    /// A on the remote or the phone: use the station you are standing in, or the Quick Menu's choice.
    func activate(menu: TennisMenu = .shared) {
        if quickMenuOpen { quickGo(quickEntries[quickIndex].id, menu: menu); return }
        if emoteMenuOpen { emoteMenuOpen = false; return }
        guard let s = nearStation else { return }
        perform(s, menu: menu)
    }
    /// B on the remote: close whatever is open.
    func back() { if quickMenuOpen { quickMenuOpen = false } else if emoteMenuOpen { emoteMenuOpen = false } }
    func toggleQuickMenu() { quickMenuOpen.toggle(); emoteMenuOpen = false; if quickMenuOpen { quickIndex = 0 } }

    func quickGo(_ id: String, menu: TennisMenu = .shared) {
        quickMenuOpen = false
        if id == "settings" { stick = P(0, 0); steering = false; heldArrows.removeAll(); menu.openSettings(); return }
        guard let s = layout.station(id) else { return }
        if s.locked { menu.refuseFromWorld("Pro Shop · Coming soon"); return }
        jump(to: s)
    }
    /// Stand in a station's ring, looking at it.
    func jump(to station: LobbyLayout.Station) {
        position = layout.arrival(at: station); heading = station.heading; cameraYaw = station.heading; cameraCentre = position
        velocity = P(0, 0); walkSpeed = 0; tapTarget = nil
        nearStation = station
        applyPose(instant: true)
        UIImpactFeedbackGenerator(style: .light).impactOccurred()
    }

    /// What a station opens: the menus the app already has, one for each.
    func perform(_ s: LobbyLayout.Station, menu: TennisMenu = .shared) {
        stick = P(0, 0); steering = false; heldArrows.removeAll(); tapTarget = nil
        switch s.action {
        case .locker(let tab): menu.openLocker(tab: tab)
        case .play(let sport): lastGate = s.id; menu.openHub(sport == "golf" ? .golf : .tennis)
        case .howToPlay: lastGate = s.id; menu.openHowTo()
        case .party: lastGate = s.id; menu.openParty()
        case .shop: menu.refuseFromWorld("Pro Shop · Coming soon")
        }
    }

    func playEmote(slot: Int) {
        emoteMenuOpen = false
        guard let player = TennisMenu.shared.player else { return }
        let ids = player.equippedEmotes
        guard ids.indices.contains(slot) else { return }
        let service = MultiplayerService.shared
        if service.lobby != nil {
            // the lobby plays it for everyone and hands the event back
            if (try? service.playEmote(ids[slot])) != nil { local?.startEmote(ids[slot], at: service.networkTime); return }
        }
        local?.startEmote(ids[slot], at: clock)
    }

    // MARK: the tick
    private func tick(_ link: CADisplayLink) { advance(now: link.targetTimestamp) }

    /// One step of the world's clock (the display link calls this every frame; a proof with no screen calls it from a timer).
    func advance(now: Double) {
        guard let scene = lobbyScene else { return }
        let dt = lastTick == 0 ? 1.0 / 60 : min(0.05, now - lastTick)
        lastTick = now; clock = now; ticks += 1
        move(dt: Float(dt))
        // the local person
        let h = layout.height(at: position)
        local?.place(position, height: h, heading: heading)
        local?.animate(dt: dt, now: MultiplayerService.shared.lobby != nil ? MultiplayerService.shared.networkTime : now, speed: walkSpeed)
        // others
        let service = MultiplayerService.shared
        for id in Array(remotes.keys) {
            guard var r = remotes[id] else { continue }
            let k = Float(1 - exp(-dt * 9))
            let before = r.position
            r.position += (r.target - r.position) * k
            r.heading += angleDifference(r.targetHeading, r.heading) * k
            let v: Float = simd_length(r.position - before) / Float(max(dt, 0.001))
            r.speed += (min(Float(7), max(v, r.targetSpeed * 0.7)) - r.speed) * k
            if now - r.lastHeard > 1.5 { r.targetSpeed = 0 }
            r.avatar.place(r.position, height: layout.height(at: r.position), heading: r.heading)
            r.avatar.animate(dt: dt, now: service.networkTime, speed: r.targetSpeed < 0.05 && v < 0.3 ? 0 : max(r.speed, 0))
            remotes[id] = r
        }
        // emotes from the others (the service holds the newest event per person)
        for (id, event) in service.emotes where id != service.localID {
            guard let r = remotes[id], seenEmotes[id] != event.id else { continue }
            seenEmotes[id] = event.id; r.avatar.startEmote(event.emoteID, at: event.startedAt)
        }
        if let mine = service.emotes[service.localID], seenEmotes[service.localID] != mine.id { seenEmotes[service.localID] = mine.id; local?.startEmote(mine.emoteID, at: mine.startedAt) }
        camera(dt: Float(dt), scene: scene)
        stations(scene: scene, dt: dt)
        if now - lastPartySync > 0.5 { lastPartySync = now; syncParty(force: false) }
        if service.lobby?.phase == .lobby, now - lastSent > 0.1 {
            lastSent = now
            service.sendLobbyPose(LobbyPoseMessage(id: service.localID, x: position.x, y: position.y, h: heading, s: walkSpeed))
        }
    }
    @ObservationIgnored private var seenEmotes: [String: String] = [:]

    private func angleDifference(_ a: Float, _ b: Float) -> Float {
        var d = (a - b).truncatingRemainder(dividingBy: 2 * .pi)
        if d > .pi { d -= 2 * .pi }; if d < -.pi { d += 2 * .pi }
        return d
    }

    private func move(dt: Float) {
        // what the player wants: a direction relative to the camera, or toward a tapped spot
        var wish = P(0, 0)
        let forward = P(sin(cameraYaw), cos(cameraYaw)), right = P(cos(cameraYaw), -sin(cameraYaw))
        if simd_length(stick) > 0.08 {
            wish = (right * stick.x + forward * stick.y) * LobbyLayout.jogSpeed * min(1, simd_length(stick))
        } else if let t = tapTarget {
            let d = t - position, l = simd_length(d)
            if l < 0.2 { tapTarget = nil } else { wish = d / l * LobbyLayout.jogSpeed * min(1, l / 0.9 + 0.35) }
        }
        // ease toward it
        let rate: Float = simd_length(wish) > 0.01 ? 11 : 15
        velocity += (wish - velocity) * min(1, rate * dt)
        let target = position + velocity * dt
        let resolved = layout.resolve(from: position, to: target)
        let moved = simd_length(resolved - position)
        if moved < simd_length(velocity * dt) * 0.35, simd_length(wish) > 0.01, tapTarget != nil { tapTarget = nil }   // walked into something: stop trying
        position = resolved
        walkSpeed = dt > 0 ? moved / dt : 0
        if walkSpeed < 0.05 { walkSpeed = 0 }
        // face the way you go
        if walkSpeed > 0.3 {
            let want = atan2(velocity.x, velocity.y)
            heading += angleDifference(want, heading) * min(1, 14 * dt)
        }
        // the camera swings round behind you while you run ahead, and leaves you alone when you step sideways
        if walkSpeed > 0.6, stick.y > -0.2 {
            cameraYaw += angleDifference(heading, cameraYaw) * min(1, 1.5 * dt)
        } else if walkSpeed > 0.6, tapTarget != nil {
            cameraYaw += angleDifference(heading, cameraYaw) * min(1, 1.5 * dt)
        }
    }

    private func camera(dt: Float, scene: LobbyScene) {
        cameraCentre += (position - cameraCentre) * min(1, 8 * dt)
        let behind = layout.cameraBehind * (landscape ? 1.15 : 1.0)
        let eye = cameraCentre - P(sin(cameraYaw), cos(cameraYaw)) * behind
        let groundHere = layout.height(at: cameraCentre)
        let height = groundHere + layout.cameraHeight
        let node = scene.cameraNode
        #if DEBUG
        if let spec = ProcessInfo.processInfo.environment["LOBBY_CAM"] {
            let v = spec.split(separator: ",").compactMap { Float($0) }
            if v.count == 6 { node.position = SCNVector3(v[0], v[2], -v[1]); node.look(at: SCNVector3(v[3], v[5], -v[4]), up: SCNVector3(0, 1, 0), localFront: SCNVector3(0, 0, -1)); return }
        }
        if let top = ProcessInfo.processInfo.environment["LOBBY_TOP"], let h = Float(top) {
            let at = (ProcessInfo.processInfo.environment["LOBBY_TOP_AT"] ?? "0,0").split(separator: ",").compactMap { Float($0) }; let cx = at.first ?? 0, cz = -(at.count > 1 ? at[1] : 0)
            node.position = SCNVector3(cx, h, cz + 0.01); node.look(at: SCNVector3(cx, 0, cz), up: SCNVector3(0, 0, -1), localFront: SCNVector3(0, 0, -1)); node.camera?.fieldOfView = 60; return
        }
        #endif
        node.position = LobbyScene.scn(eye, height)
        node.look(at: LobbyScene.scn(cameraCentre + P(sin(cameraYaw), cos(cameraYaw)) * 3.4, groundHere + layout.cameraLookHeight), up: SCNVector3(0, 1, 0), localFront: SCNVector3(0, 0, -1))
    }

    private func stations(scene: LobbyScene, dt: Double) {
        let near = layout.station(at: position)
        if near?.id != nearStation?.id { nearStation = near }
        for s in layout.stations {
            let ringNode = scene.rings[s.id]
            let active = s.id == near?.id
            let target: Float = active ? 1.0 + 0.04 * Float(sin(clock * 5)) : 1.0
            ringNode?.scale = SCNVector3(target, 1, target)
            ringNode?.opacity = active ? 1.0 : (s.locked ? 0.7 : 0.9)
            if let label = scene.labels[s.id] {
                let d = simd_length(position - s.ring)
                let a = Float(min(1, max(0.25, (34 - d) / 14)))
                // the prompt on the screen says what a ring does; the name floating over it is for finding it from afar
                label.opacity = active ? 0 : CGFloat(a)
            }
        }
    }

    // MARK: size
    /// The view's shape: a phone upright sees more sideways, a TV more of the sky.
    func setViewport(size: CGSize) {
        guard let cam = lobbyScene?.cameraNode.camera, size.width > 0, size.height > 0 else { return }
        landscape = size.width >= size.height
        cam.projectionDirection = landscape ? .vertical : .horizontal
        cam.fieldOfView = landscape ? 46 : 56
    }

    // MARK: placing people
    private func applyPose(instant: Bool) {
        let h = layout.height(at: position)
        local?.place(position, height: h, heading: heading)
        guard let scene = lobbyScene else { return }
        if instant { cameraCentre = position; camera(dt: 1, scene: scene) }
    }

    /// Dress the local avatar in whatever the player wears now (the Locker may have changed it).
    func refreshLocal() {
        guard let scene = lobbyScene, let player = TennisMenu.shared.player else { return }
        if let local { local.update(player: player, name: player.name) }
        else {
            let a = LobbyAvatar(id: "local", player: player, name: player.name, showsName: false, index: 0)
            scene.people.addChildNode(a.node); local = a
            a.place(position, height: layout.height(at: position), heading: heading)
        }
    }

    /// Everyone in the party is in the world: one avatar for each person, dressed from their look.
    func syncParty(force: Bool) {
        guard let scene = lobbyScene else { return }
        let service = MultiplayerService.shared
        let lobby = service.lobby
        let others = (lobby?.participants ?? []).filter { $0.id != service.localID && $0.connected }
        if !force, others.map(\.id) == party.filter({ $0.id != service.localID }).map(\.id), others.map({ $0.loadout }) == party.filter({ $0.id != service.localID }).map({ $0.loadout }) { return }
        party = lobby?.participants ?? []
        let ids = Set(others.map(\.id))
        for id in Array(remotes.keys) where !ids.contains(id) {
            remotes[id]?.avatar.disappear(); remotes.removeValue(forKey: id)
        }
        for (i, p) in others.enumerated() {
            let player = p.lobbyPlayer
            if var r = remotes[p.id] {
                r.avatar.update(player: player, name: p.name); remotes[p.id] = r
            } else {
                let a = LobbyAvatar(id: p.id, player: player, name: p.name, showsName: true, index: i + 1)
                // newcomers arrive on the terrace, at their own spot, until their phone says where they are
                let spot = partySpot(of: p.id)
                scene.people.addChildNode(a.node)
                a.place(spot, height: layout.height(at: spot), heading: layout.partyHeading)
                a.appear(animated: true)
                remotes[p.id] = Remote(avatar: a, position: spot, heading: layout.partyHeading, speed: 0, target: spot, targetHeading: layout.partyHeading, targetSpeed: 0, lastHeard: clock)
            }
        }
    }

    /// The terrace spot for a person in the party: their place in the lobby's list (the owner first).
    func partySpot(of id: String) -> P {
        let order = MultiplayerService.shared.lobby?.participants.map(\.id) ?? []
        return layout.partySpots[min(layout.partySpots.count - 1, order.firstIndex(of: id) ?? 0)]
    }

    /// A pose from someone else's phone.
    func receivePose(_ m: LobbyPoseMessage) {
        guard var r = remotes[m.id], m.x.isFinite, m.y.isFinite, m.h.isFinite, m.s.isFinite else { return }
        let p = P(min(40, max(-40, m.x)), min(40, max(-40, m.y)))
        if simd_length(p - r.target) > 14 { r.position = p }   // a jump (the Quick Menu): no sliding across the plaza
        r.target = p; r.targetHeading = m.h; r.targetSpeed = min(8, max(0, m.s)); r.lastHeard = clock
        remotes[m.id] = r
    }
    func remotePosition(_ id: String) -> P? { remotes[id]?.position }
    func remoteTarget(_ id: String) -> P? { remotes[id]?.target }
    var remoteCount: Int { remotes.count }
    var localPosition: P { position }

    /// Everyone on the terrace again (after a party match): the local player at the first spot, the rest where their phones were.
    func gatherOnTerrace() {
        returnStation = "party-board"
    }

    // MARK: hit testing (taps)
    @ObservationIgnored var groundHit: ((CGPoint) -> P?)?
    func tap(at point: CGPoint) { if let p = groundHit?(point) { walk(to: p) } }
}
