import SceneKit
import UIKit
import simd

/// One person in the lobby: their match hero (built from their look, so new clothes need no changes here), standing on the ground and playing clips on its own
/// skeleton: the Ready stance when still, Walk and Run by ground speed, an emote on top when one is played.
@MainActor final class LobbyAvatar {
    typealias P = LobbyLayout.P
    let id: String
    /// Moves and turns with the person; the hero hangs under it.
    let node = SCNNode()
    private(set) var player: Player
    private(set) var name: String
    private let holder = SCNNode()
    private var rig: HeroRig?
    private let tag = SCNNode()
    private var walkPhase = 0.0, runPhase = 0.0, idleClock = 0.0
    private let idleOffset: Double
    private var emote: (id: String, started: Double)?
    private var look = ""

    /// A metre of the lobby: the hero is exported 1.387 units tall for the locker stage, and a person is 1.7 m.
    static let worldScale: Float = 1.7 / MatchHeroData.stageHeight

    /// The Walk clip's planted-foot speed at 1x (Unity's HeroTennisDriver.walkClipSpeed) and the Run clip's (runClipSpeed).
    static let walkClipSpeed: Float = 0.96, runClipSpeed: Float = 5.8

    init(id: String, player: Player, name: String, showsName: Bool, index: Int) {
        self.id = id; self.player = player; self.name = name; idleOffset = Double(index) * 0.73
        node.name = "avatar-" + id
        holder.scale = SCNVector3(Self.worldScale, Self.worldScale, Self.worldScale)
        node.addChildNode(holder)
        // a soft contact shadow, so a person always reads as standing on the ground
        let blob = SCNPlane(width: 1.1, height: 1.1)
        let m = SCNMaterial(); m.lightingModel = .constant; m.diffuse.contents = Self.blobImage; m.blendMode = .alpha; m.writesToDepthBuffer = false
        blob.materials = [m]
        let b = SCNNode(geometry: blob); b.eulerAngles.x = -.pi / 2; b.position.y = 0.015; b.renderingOrder = -1; node.addChildNode(b)
        if showsName { buildTag() }
        rebuild()
    }

    private static let blobImage: UIImage = UIGraphicsImageRenderer(size: CGSize(width: 64, height: 64)).image { ctx in
        let colors = [UIColor(white: 0, alpha: 0.38).cgColor, UIColor(white: 0, alpha: 0).cgColor] as CFArray
        let g = CGGradient(colorsSpace: CGColorSpaceCreateDeviceRGB(), colors: colors, locations: [0, 1])!
        ctx.cgContext.drawRadialGradient(g, startCenter: CGPoint(x: 32, y: 32), startRadius: 0, endCenter: CGPoint(x: 32, y: 32), endRadius: 32, options: [])
    }

    private func buildTag() {
        tag.name = "nameTag"
        let image = LobbyKit.signImage(name, fill: LobbyKit.navy, ink: LobbyKit.white, size: CGSize(width: 320, height: 96), corner: 40)
        let plane = SCNPlane(width: 0.9, height: 0.27); plane.materials = [LobbyKit.signMaterial(image)]
        tag.geometry = plane
        tag.position = SCNVector3(0, 2.05, 0)
        let bb = SCNBillboardConstraint(); bb.freeAxes = [.Y]; tag.constraints = [bb]
        node.addChildNode(tag)
    }

    /// The look this person is wearing right now (the hero is rebuilt only when it changes).
    private static func signature(_ p: Player) -> String {
        "\(p.standardFemale)|\(p.handedness == .left)|\(p.skinHex)|" + Player.outfitSlots.map { p.outfitHex($0) ?? "kit" }.joined(separator: "|")
    }

    func update(player p: Player, name n: String) {
        name = n
        guard Self.signature(p) != look else { player = p; return }
        player = p
        rebuild()
    }

    private func rebuild() {
        look = Self.signature(player)
        holder.childNodes.forEach { $0.removeFromParentNode() }
        rig = nil
        guard let hero = MatchHero.build(player), let asset = MatchHero.asset(female: player.standardFemale) else { return }
        hero.eulerAngles.y = 0   // the export faces -z (north): the node's heading turns the whole person
        holder.addChildNode(hero)
        if let r = HeroRig(root: hero, asset: asset) { r.attach(); rig = r }
        hero.childNodes.forEach { $0.castsShadow = true }
        hero.enumerateChildNodes { n, _ in n.castsShadow = true }
        hero.setValue(true, forKey: "lobbyAvatar")
        // the racket is for the court
        hero.childNode(withName: MatchHeroData.racketName, recursively: false)?.isHidden = true
    }

    /// Put the person at `position` (metres, north up) at ground `height`, facing `heading` (0 = north).
    func place(_ position: P, height: Float, heading: Float) {
        node.position = LobbyScene.scn(position, height)
        node.eulerAngles.y = -heading
    }

    var hasRig: Bool { rig != nil }
    var emoting: Bool { emote != nil }
    func startEmote(_ id: String, at time: Double) {
        guard rig?.data.clips[id] != nil else { return }
        emote = (id, time)
    }
    func stopEmote() { emote = nil }

    /// Advance the animation clocks and pose the skeleton. `speed` is the ground speed in m/s.
    func animate(dt: Double, now: Double, speed: Float) {
        guard let rig, let ready = rig.data.clips["ready"] else { return }
        idleClock += dt
        let base: [HeroTrackPose]
        let walk = rig.data.clips["walk"], run = rig.data.clips["run"]
        if speed > 0.12, let walk, let run {
            let walkRate = Double(min(1.8, max(0.35, speed / Self.walkClipSpeed))), runRate = Double(min(1.25, max(0.55, speed / Self.runClipSpeed)))
            walkPhase = (walkPhase + dt * walkRate).truncatingRemainder(dividingBy: walk.length)
            runPhase = (runPhase + dt * runRate).truncatingRemainder(dividingBy: run.length)
            let idle = ready.pose(at: idleClock + idleOffset, loop: true)
            // still -> walking -> running, each blended over a band of speeds
            let toWalk = smooth((speed - 0.12) / 0.5), toRun = smooth((speed - 1.5) / 1.4)
            let w = walk.pose(at: walkPhase, loop: true), r = run.pose(at: runPhase, loop: true)
            base = RigData.blend(RigData.blend(idle, w, toWalk), r, toRun)
        } else if speed > 0.12, let walk {
            walkPhase = (walkPhase + dt * Double(min(1.8, max(0.35, speed / Self.walkClipSpeed)))).truncatingRemainder(dividingBy: walk.length)
            base = RigData.blend(ready.pose(at: idleClock + idleOffset, loop: true), walk.pose(at: walkPhase, loop: true), smooth((speed - 0.12) / 0.5))
        } else {
            base = ready.pose(at: idleClock + idleOffset, loop: true)
        }
        var pose = base
        if let e = emote, let clip = rig.data.clips[e.id] {
            let t = now - e.started
            if speed > 0.6 || t > clip.length + 0.3 { emote = nil }
            else {
                let w = Float(min(smooth(t / 0.15), smooth((clip.length + 0.25 - t) / 0.3)))
                pose = RigData.blend(base, clip.pose(at: min(t, clip.length), loop: false), w)
            }
        }
        rig.apply(pose)
    }
    private func smooth(_ x: Double) -> Double { let c = min(1, max(0, x)); return c * c * (3 - 2 * c) }
    private func smooth(_ x: Float) -> Float { let c = min(1, max(0, x)); return c * c * (3 - 2 * c) }

    func appear(animated: Bool) {
        guard animated else { return }
        node.opacity = 0; node.scale = SCNVector3(0.92, 0.92, 0.92)
        node.runAction(.group([.fadeIn(duration: 0.25), .scale(to: 1, duration: 0.25)]))
    }
    func disappear() { node.runAction(.sequence([.fadeOut(duration: 0.2), .removeFromParentNode()])) }
}
