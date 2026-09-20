import RealityKit
import UIKit

/// Cosmetic multiplayer presentation. Contact detection retains the original
/// rig-unit capsule rules and cannot submit a shot or change a score.
@MainActor
final class NativeBystanderPresentation {
    static let metresPerRigUnit: Float = 0.292608
    let root = Entity()
    private var members: [Member] = []
    private var roster: [Player] = []
    private var contact = ClubContact()
    private var mainClipName: String?
    private var definition: NativeAssetManifest.GolferAsset?

    @MainActor private final class Member {
        let container = Entity()
        let golfer: Entity
        let correction: NativeGolferConstraints
        var playback: AnimationPlaybackController?
        var clip: String?
        var recovery: (started: Date, push: SIMD3<Float>)?
        init(template: Entity, definition: NativeAssetManifest.GolferAsset) throws {
            golfer = template.clone(recursive: true)
            guard let model = golfer.findEntity(named: definition.skeletonEntity) as? ModelEntity else {
                throw NativeAssetError.invalid("Waiting golfer has no bound skeleton")
            }
            correction = try NativeGolferConstraints(model: model)
            container.addChild(golfer)
        }
        func select(_ name: String) {
            guard clip != name, let animation = golfer.availableAnimations.first(where: { $0.name == name }) else { return }
            playback?.stop()
            playback = golfer.playAnimation(animation, transitionDuration: 0)
            playback?.speed = 0
            clip = name
        }
        func stop() {
            playback?.stop(); playback = nil
            golfer.stopAllAnimations(recursive: true)
            correction.model.components.remove(IKComponent.self)
            container.removeFromParent()
        }
    }

    init() { root.name = "waiting-players" }
    var playerIDs: [UUID] { roster.map(\.id) }
    var recoveringPlayerIDs: [UUID] {
        zip(roster, members).compactMap { $0.1.recovery == nil ? nil : $0.0.id }
    }

    func prepare(template: Entity, definition: NativeAssetManifest.GolferAsset, playerCount: Int) throws {
        clear()
        self.definition = definition
        for index in 0..<max(0, min(3, playerCount - 1)) {
            let member = try Member(template: template, definition: definition)
            member.container.name = "waiting-player-\(index)"
            root.addChild(member.container)
            members.append(member)
        }
    }

    func clear() {
        members.forEach { $0.stop() }
        members.removeAll(); roster.removeAll(); definition = nil
        contact = ClubContact(); mainClipName = nil
    }

    func setPlayers(_ players: [Player], active: Player) {
        let waiting = Array(players.filter { $0.id != active.id }.prefix(members.count))
        guard waiting != roster else { return }
        roster = waiting
        contact = ClubContact(); mainClipName = nil
        for (player, member) in zip(roster, members) {
            member.recovery = nil
            member.clip = nil
            NativeGolferStyle.apply(player.golferAppearance, to: member.golfer, showClub: false)
        }
    }

    static func spot(index: Int, handedness: Handedness) -> SIMD3<Float> {
        let mirror: Float = handedness == .left ? -1 : 1
        let offsets = [SIMD3<Float>(-0.6 * mirror, 0, -4.8),
                       SIMD3<Float>(-4.5 * mirror, 0, -2.2), SIMD3<Float>(-4.5 * mirror, 0, 2.2)]
        return SIMD3(-3.2 * mirror, 0, 0) + offsets[min(max(0, index), 2)]
    }

    /// Returns only cosmetic hits, for synchronized thump feedback.
    func update(session: GameSession, world: Entity, activeClip: String?, activeTime: Double,
                origin: SIMD3<Float>, orientation: simd_quatf) -> Int {
        guard let definition, let idle = definition.clips?["idle"] else { return 0 }
        setPlayers(session.players, active: session.player)
        root.position = origin; root.orientation = orientation
        root.isEnabled = session.assetsReady && !roster.isEmpty
        guard root.isEnabled else { contact.reset(); return 0 }
        let now = session.date.timeIntervalSinceReferenceDate
        let reduceMotion = UIAccessibility.isReduceMotionEnabled
        var targets: [ClubContact.Target] = []
        for (index, pair) in zip(roster, members).enumerated() {
            let (player, member) = pair
            let spot = Self.spot(index: index, handedness: session.player.handedness)
            let mainPosition = SIMD3<Float>(session.player.handedness == .left ? 3.2 : -3.2, 0, 0)
            let facing = atan2(-(mainPosition - spot).z, (mainPosition - spot).x)
            var rotation = simd_quatf(angle: facing, axis: SIMD3(0, 1, 0))
            var name = "idle"
            var time = reduceMotion ? 0 : (now + Double(index) * 1.3).truncatingRemainder(dividingBy: idle.duration)
            if let recovery = member.recovery, !reduceMotion,
               let clip = definition.clips?["recovery"], session.date.timeIntervalSince(recovery.started) < clip.duration {
                name = "recovery"; time = max(0, session.date.timeIntervalSince(recovery.started))
                rotation = simd_quatf(from: SIMD3(-1, 0, 0), to: simd_normalize(recovery.push))
            } else {
                member.recovery = nil
                targets.append(.init(id: player.id, base: spot + SIMD3(0, 0.8, 0),
                                     top: spot + SIMD3(0, 5.2, 0), radius: 1))
            }
            let body = spot * Self.metresPerRigUnit
            let worldBody = root.convert(position: body, to: world)
            let point = CoursePoint(x: Double(worldBody.x) / GolfUnits.metresPerYard,
                                    d: -Double(worldBody.z) / GolfUnits.metresPerYard)
            let ground = Float(session.round.hole.surface(at: point).heightYards * GolfUnits.metresPerYard)
            // The USD golfer is ball-relative; cancel that offset so its body is
            // at the exact waiting spot before applying the facing rotation.
            member.container.position = body + rotation.act(SIMD3(3.2, 0, 0) * Self.metresPerRigUnit)
            member.container.position.y = ground - origin.y
            member.container.orientation = rotation
            member.select(name); member.playback?.time = time
            if let clip = definition.clips?[name] {
                member.correction.update(clip: clip, time: time, hole: session.round.hole, world: world)
            }
        }
        if mainClipName != activeClip { contact.reset(); mainClipName = activeClip }
        guard session.isActive, !reduceMotion, let activeClip, let clip = definition.clips?[activeClip],
              !clip.clubIsDropped(at: activeTime),
              let grip = clip.contact("clubGrip", at: activeTime), let head = clip.contact("clubHead", at: activeTime) else {
            contact.reset(); return 0
        }
        let mirror: Float = session.player.handedness == .left ? -1 : 1
        let transform = SIMD3(mirror, 1, 1) / Self.metresPerRigUnit
        let hits = contact.update(hands: grip * transform, head: head * transform, at: now, targets: targets)
        for hit in hits {
            if let index = roster.firstIndex(where: { $0.id == hit.id }) {
                members[index].recovery = (session.date, hit.push)
            }
        }
        return hits.count
    }
}
