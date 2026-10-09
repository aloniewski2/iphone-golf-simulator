import Foundation
import SceneKit
import simd

/// LOCKER_MIRROR: the match hero on the locker stage MOVES by playing its clips on its own skeleton, not by turning the character root.
/// `HeroRig` swaps the hero's skinned parts (Body and the six kit pieces) to their bind-pose meshes with SceneKit skinners, hangs a flat skeleton of bone nodes under the hero root, and
/// poses it from bone tracks sampled from Unity's own clips (<Sex>_ReadyIdle, <Sex>_Serve; see RigData). The rigid parts (the face, the racket) follow their track. At frame 0 of ReadyIdle the
/// skinned hero is the static Ready-stance hero, so switching between the two does not move a vertex.
@MainActor final class HeroRig {
    let data: RigData
    let root: SCNNode
    private let asset: MatchHeroData.Asset
    private(set) var isAttached = false
    private let skeleton = SCNNode()
    /// The skeleton's bone nodes (flat, one per bone of the export, hero-space transforms) while attached.
    private(set) var boneNodes: [SCNNode] = []
    private var rigid: [(node: SCNNode, track: Int)] = []
    private var staticGeometry: [String: SCNGeometry] = [:]

    init?(root: SCNNode, asset: MatchHeroData.Asset) {
        guard let data = asset.rig() else { return nil }
        self.data = data; self.root = root; self.asset = asset
        skeleton.name = "skeleton"
    }

    /// The skinned parts and the bone nodes go in; the hero is posed at frame 0 of ReadyIdle (identical to the static stance).
    func attach() {
        guard !isAttached else { return }
        root.addChildNode(skeleton)
        boneNodes = data.info.bones.map { name in let b = SCNNode(); b.name = name; skeleton.addChildNode(b); return b }
        for part in data.skinned {
            guard let node = root.childNode(withName: part.name, recursively: false), let old = node.geometry, let g = part.geometry.copy() as? SCNGeometry else { continue }
            staticGeometry[part.name] = old
            g.materials = old.materials   // the hero's own materials (skin tone, kit tints) ride along
            node.geometry = g
            let skinner = SCNSkinner(baseGeometry: g, bones: part.bones.map { boneNodes[$0] }, boneInverseBindTransforms: part.inverseBinds, boneWeights: part.weights, boneIndices: part.indices)
            skinner.skeleton = skeleton
            node.skinner = skinner
        }
        data.attachMorphers(to: root)
        rigid = data.info.parts.compactMap { rp in
            guard rp.kind == "rigid", let node = root.childNode(withName: rp.part, recursively: true) else { return nil }
            return (node, rp.track)
        }
        isAttached = true
        if let ready = data.clips["ready"] {
            apply(ready.pose(at: 0, loop: true))
            data.applyMorphWeights(ready.morphWeights(at: 0, loop: true), to: root)
        }
    }

    /// Back to the static Ready-stance hero (what the morph practice swing and the thumbnails build on).
    func detach() {
        guard isAttached else { return }
        for part in data.skinned {
            guard let node = root.childNode(withName: part.name, recursively: false) else { continue }
            node.skinner = nil; node.morpher = nil
            if let old = staticGeometry[part.name] { old.materials = node.geometry?.materials ?? old.materials; node.geometry = old }
        }
        SCNTransaction.begin(); SCNTransaction.disableActions = true
        for r in rigid { r.node.simdTransform = matrix_identity_float4x4 }
        SCNTransaction.commit()
        skeleton.removeFromParentNode(); skeleton.childNodes.forEach { $0.removeFromParentNode() }
        boneNodes = []; rigid = []; staticGeometry = [:]
        isAttached = false
    }

    /// Put the skeleton and the rigid parts in `pose` (one entry per track).
    func apply(_ pose: [HeroTrackPose]) {
        guard isAttached, pose.count == data.info.trackCount else { return }
        SCNTransaction.begin(); SCNTransaction.disableActions = true
        for (i, node) in boneNodes.enumerated() { node.simdTransform = pose[i].matrix }
        for r in rigid { r.node.simdTransform = pose[r.track].matrix }
        SCNTransaction.commit()
    }

    func apply(_ motion: MenuMotion, at time: Double) {
        apply(motion.pose(data, at: time))
        data.applyMorphWeights(motion.morphWeights(data, at: time), to: root, includeFaceBlink:false)
        var equipment = "iron"
        if case .clipOnce(let id) = motion.kind, let clip = data.clips[id], time >= motion.lead, time < motion.settledAfter(data) {
            equipment = clip.info.equipment ?? equipment
        } else if motion.kind == .serveOnce, motion.serveWeight(data, at: time) > 0 {
            equipment = data.clips["serve"]?.info.equipment ?? equipment
        }
        let hideEquipment = root.value(forKey: "heroEquipmentHidden") as? Bool ?? false
        for node in root.childNodes where node.name?.hasPrefix("Club_") == true {
            node.isHidden = hideEquipment || node.name?.lowercased() != "club_" + equipment
        }
        let happy: Bool
        if case .clipOnce(let id) = motion.kind { happy = ["celebratePoint","matchWin","hitPerfect","introWave"].contains(id) } else { happy = false }
        MatchHeroSurfaces.performFace(on:root,at:time,happy:happy)
    }
}

/// What a menu tile plays on the hero, as a function of time since the tile came up. Never the character root.
struct MenuMotion: Equatable {
    enum Kind: Equatable { case ready, serveOnce, clipOnce(String) }
    var kind: Kind
    /// Serve once: a beat of Ready first, then the Serve is blended in over `fadeIn`, plays through, and blends back out into the Ready loop over `fadeOut`. After that it is the Ready loop for good.
    var lead = 0.7, fadeIn = 0.2, fadeOut = 0.4
    var idleOffset = 0.0
    var freezeIdle = false
    /// Set when something has to take the hero off the skeleton (the loading screen's morph practice swing builds on the static Ready stance, which is frame 0 of ReadyIdle): from this time the
    /// pose eases from where it was to ReadyIdle frame 0 over `settleDuration`, so nothing pops.
    var settleFrom: Double?
    var settleDuration = 0.12

    static let ready = MenuMotion(kind: .ready)
    static let serveOnce = MenuMotion(kind: .serveOnce)

    private static func smooth(_ x: Double) -> Float { let c = min(1, max(0, x)); return Float(c * c * (3 - 2 * c)) }

    /// Seconds until only the Ready loop is left playing (0 for the Ready loop itself).
    @MainActor func settledAfter(_ rig: RigData) -> Double {
        switch kind {
        case .ready: return 0
        case .serveOnce: return lead + (rig.clips["serve"]?.length ?? 0)
        case .clipOnce(let id): return lead + (rig.clips[id]?.length ?? 0) + fadeOut
        }
    }

    /// 0...1: how much of the Serve is in the pose at `t` (0 before it and after it, eased in over `fadeIn` and out over `fadeOut`).
    @MainActor func serveWeight(_ rig: RigData, at t: Double) -> Float {
        guard kind == .serveOnce, let serve = rig.clips["serve"] else { return 0 }
        let ts = t - lead
        guard ts > 0, ts < serve.length else { return 0 }
        return min(Self.smooth(ts / fadeIn), Self.smooth((serve.length - ts) / fadeOut))
    }

    /// The pose at time `t`: the Ready loop, with the Serve laid over it for one pass in `serveOnce`; easing to the still Ready stance once `settleFrom` is set.
    @MainActor func pose(_ rig: RigData, at t: Double) -> [HeroTrackPose] {
        guard let from = settleFrom, let ready = rig.clips["ready"] else { return playing(rig, at: t) }
        return RigData.blend(playing(rig, at: from), ready.pose(at: 0, loop: true), Self.smooth((t - from) / settleDuration))
    }
    @MainActor private func playing(_ rig: RigData, at t: Double) -> [HeroTrackPose] {
        guard let ready = rig.clips["ready"] else { return [] }
        let base = ready.pose(at: freezeIdle ? 0 : t + idleOffset, loop: true)
        if case .clipOnce(let id) = kind, let clip = rig.clips[id] {
            let elapsed = t - lead
            if elapsed < 0 { return base }
            // Play through the last frame, then ease into the idle. Never cut the end of an emote off.
            let weight = elapsed <= clip.length ? Self.smooth(elapsed / max(0.001, fadeIn)) : 1 - Self.smooth((elapsed - clip.length) / max(0.001, fadeOut))
            return RigData.blend(base, clip.pose(at: elapsed, loop: false), weight)
        }
        let w = serveWeight(rig, at: t)
        guard w > 0, let serve = rig.clips["serve"] else { return base }
        return RigData.blend(base, serve.pose(at: t - lead, loop: false), w)
    }
}

/// A lobby hero owns one animation clock and at most one pending emote. Clock values use the lobby owner's timeline.
@MainActor final class LobbyHeroMotion {
    let rig: HeroRig
    let idleOffset: Double
    private(set) var clip: String?
    private(set) var queued: String?
    private(set) var startedAt = 0.0
    init(rig: HeroRig, idleOffset: Double) { self.rig = rig; self.idleOffset = idleOffset; rig.attach() }
    func play(_ id: String, at time: Double, now: Double) {
        guard rig.data.clips[id] != nil else { return }
        if clip != nil, now < endTime { queued = id; return }
        clip = id; startedAt = min(time, now)
    }
    var endTime: Double { startedAt + (clip.flatMap { rig.data.clips[$0]?.length } ?? 0) + 0.2 }
    func pose(at time: Double) {
        if clip != nil, time >= endTime {
            if let next = queued { startedAt = endTime; clip = next; queued = nil }
            else { clip = nil }
        }
        var motion = MenuMotion.ready; var t = time
        if let clip { motion = MenuMotion(kind: .clipOnce(clip), lead: 0, fadeIn: 0.12, fadeOut: 0.2); t = time - startedAt }
        motion.idleOffset = idleOffset + (clip == nil ? 0 : startedAt)
        motion.freezeIdle = true
        rig.apply(motion, at: t)
    }
}
