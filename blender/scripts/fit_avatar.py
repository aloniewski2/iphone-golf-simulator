"""Fit the customisable base avatar (Generated/models/avatar.glb, see prepare_avatar.py) onto
the male tennis rig and export its body for Unity, the way fit_opponents.py does for the
campaign opponents.

What differs from those characters:
  * Proportions. A big round head (29% of the height) on a short body: its own landmarks,
    measured from orthographic renders with a height grid, and a scale that puts its
    shoulders on the rig's shoulders.
  * The head can be scaled down about the neck (--head) so the racket clears it in serves
    and follow-throughs while it still reads as big-headed.
  * The face. The model's face is blank on purpose (the features are the expression atlas,
    so they animate and can be customised); on a head that is skin all over, the generic
    "find the bare skin" search would size the decal to the whole head. Its decal is placed
    from the head's own measurements instead, with the eye line on the nose bridge, and its
    material names its own atlas (FaceAtlas_Avatar, from build_avatar_face_atlas.py).

Run:  Blender --background --python fit_avatar.py -- [--name Avatar|AvatarF] [--head 0.8] [--render DIR] [--no-export]
"""
import sys
from pathlib import Path

import bmesh
import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import fit_higgs_characters as fit  # noqa: E402
import fit_opponents as opponents  # noqa: E402  (export + render helpers; main() guarded below)

NAME = "Avatar"
GLB = fit.MODELS / "avatar.glb"
GENDER = "Male"
# Mesh-height fractions (y lateral, f forward), read off orthographic front and side renders.
LANDMARKS = dict(hips=.37, spine=.45, chest=.55, neck=.69, head=.715, crown=.995,
                 leg=.06, knee=.18, knee_y=.07, ankle=.075, ankle_y=.085, ball_f=.07, toe_f=.13,
                 clav=.66, shoulder=.625, shoulder_y=.135, elbow=.52, elbow_y=.19,
                 wrist=.425, wrist_y=.235, hand=.37, hand_y=.255, arm_f=0)
RIG_SHOULDER = 1.196          # the male rig's shoulder height (m)
# The female base avatar, on the female rig. Landmarks measured the same way.
PROFILES = {
    "Avatar": dict(gender="Male", glb="avatar.glb"),
    "AvatarF": dict(gender="Female", glb="avatarf.glb"),
}
LANDMARKS_BY_NAME = {"Avatar": LANDMARKS,
                     "AvatarF": dict(hips=.38, spine=.46, chest=.55, neck=.69, head=.715, crown=.99,
                                     leg=.065, knee=.19, knee_y=.07, ankle=.08, ankle_y=.085, ball_f=.08, toe_f=.15,
                                     clav=.66, shoulder=.615, shoulder_y=.13, elbow=.50, elbow_y=.185,
                                     wrist=.41, wrist_y=.235, hand=.36, hand_y=.255, arm_f=0)}


def cut_hands(body, joints, rig):
    """Remove the model's own open hands; the V4 grip fists hold the racket in their place.
    The generic cut (a cylinder along the hand's axis) missed these short, splayed A-pose
    hands, leaving a flat hand flapping beside each fist. Here the hand is everything below
    the wrist line and out beyond the hips -- in A-pose nothing else is there -- and the
    opening is capped so the forearm ends cleanly inside the fist."""
    import bmesh as _bm
    k = LANDMARKS
    wrist_z = .007 + (k["wrist"] - .012) * fit.HEIGHT
    outside = (k["wrist_y"] - .045) * fit.HEIGHT
    bm = _bm.new(); bm.from_mesh(body.data)
    doomed = {v for v in bm.verts if v.co.z < wrist_z and abs(v.co.x - rig.location.x) > outside}
    rim = {n for v in doomed for e in v.link_edges for n in e.verts if n not in doomed}
    _bm.ops.delete(bm, geom=list(doomed), context="VERTS")
    edges = [e for e in bm.edges if e.is_boundary and e.verts[0] in rim and e.verts[1] in rim]
    if edges: _bm.ops.holes_fill(bm, edges=edges, sides=0)
    bm.to_mesh(body.data); bm.free()
    return len(doomed)


def small_fragments(body, joints, rig):
    """Loose slivers of fist left at the wrists after the cut. The avatars' forearm skin is its
    own shell (the sleeve is separate cloth), so once the hand is cut away the whole forearm is
    a small island near the wrist: the generic cleanup (under 400 vertices within 16 cm)
    deleted it and the arms ended at the sleeve. Only true slivers go."""
    import bmesh as _bm
    wrists = [fit.to_world(joints[f"Hand.{side}"][0], rig.location) for side in ("L", "R")]
    bm = _bm.new(); bm.from_mesh(body.data); bm.verts.ensure_lookup_table()
    seen = set(); doomed = []
    for v in bm.verts:
        if v in seen: continue
        island = []; stack = [v]; seen.add(v)
        while stack:
            x = stack.pop(); island.append(x)
            for e in x.link_edges:
                n = e.other_vert(x)
                if n not in seen: seen.add(n); stack.append(n)
        if len(island) < 40:
            c = sum((x.co for x in island), Vector()) / len(island)
            if min((c - w).length for w in wrists) < .08: doomed += island
    _bm.ops.delete(bm, geom=doomed, context="VERTS")
    bm.to_mesh(body.data); bm.free()
    return len(doomed)


def grey_skirt(body, px):
    """The pleated skort moves with the hips (see fit_higgs_characters.soften_skirt, which
    finds a navy skirt by colour): here it is the grey cloth between the waist and the hem.
    Skin and the white shoes are excluded by colour."""
    colours = fit.vertex_colours(body, px)
    groups = {vg.name: vg for vg in body.vertex_groups}
    lo, hi = .007 + .31 * fit.HEIGHT, .007 + .45 * fit.HEIGHT
    centre_x = sum(v.co.x for v in body.data.vertices) / len(body.data.vertices)
    moved = 0
    for v in body.data.vertices:
        if not lo < v.co.z < hi: continue
        r, g, b = colours[v.index]
        grey = max(r, g, b) - min(r, g, b) < .08 and max(r, g, b) < .75
        if not grey: continue
        t = 1 - (v.co.z - lo) / (hi - lo)
        leg = .3 * t * t
        for vg in body.vertex_groups: vg.remove([v.index])
        side = "L" if v.co.x > centre_x else "R"
        groups["Hips"].add([v.index], 1 - leg, "REPLACE")
        if leg > .001: groups[f"UpperLeg.{side}"].add([v.index], leg, "REPLACE")
        moved += 1
    return moved


def use(name):
    """Select which avatar the module-level NAME/GLB/GENDER/LANDMARKS describe."""
    global NAME, GLB, GENDER, LANDMARKS
    NAME = name; GLB = fit.MODELS / PROFILES[name]["glb"]; GENDER = PROFILES[name]["gender"]
    LANDMARKS = LANDMARKS_BY_NAME[name]
# Where the face sits on the head, as fractions from jaw (0) to crown (1): the eye line
# across the nose bridge, the mouth below the nose.
EYE_LINE, MOUTH_LINE = .39, .15


def avatar_face(gender, rig, body, collection, px, report):
    """The face decal, sized and placed from the head itself (see module notes)."""
    for o in list(collection.objects):
        if o.name.startswith(("V4 face decal", "V4 hair")): bpy.data.objects.remove(o, do_unlink=True)
    bm = bmesh.new(); bm.from_mesh(body.data); bm.transform(body.matrix_world)
    tree = BVHTree.FromBMesh(bm); bm.free()
    fwd = Vector((0, -1, 0)); right = Vector((-1, 0, 0))
    hg = body.vertex_groups["Head"].index
    head = [body.matrix_world @ v.co for v in body.data.vertices if any(g.group == hg and g.weight > .9 for g in v.groups)]
    jaw, crown = min(p.z for p in head), max(p.z for p in head)
    # Width across the cheeks at the eye line (ears excluded: they stick out past the face).
    eye_z = jaw + (crown - jaw) * EYE_LINE
    cheeks = [p.x for p in head if abs(p.z - eye_z) < .02 and p.y < rig.location.y - .05]
    cx = rig.location.x
    width = (max(cheeks) - min(cheeks)) if cheeks else (crown - jaw) * .9
    # Cell rows: eyes at .52 of the cell, mouth at .24 (build_avatar_face_atlas.py).
    mouth_z = jaw + (crown - jaw) * MOUTH_LINE
    h = (eye_z - mouth_z) / (.52 - .24)
    w = h * 512 / 440
    bottom = eye_z - .52 * h
    grid = 32
    bm = bmesh.new(); uvl = bm.loops.layers.uv.new("UVMap"); rows = []
    for j in range(grid + 1):
        row = []
        for i in range(grid + 1):
            u, v = i / grid, j / grid
            origin = Vector((cx + right.x * (.5 - u) * w, rig.location.y - 1, bottom + v * h))
            hit = tree.ray_cast(origin, -fwd, 3)
            surface = hit[0] if hit[0] is not None else tree.find_nearest(Vector((origin.x, rig.location.y, origin.z)))[0]
            row.append((bm.verts.new(surface + fwd * .003), u, v))
        rows.append(row)
    for j in range(grid):
        for i in range(grid):
            quad = [rows[j][i], rows[j][i + 1], rows[j + 1][i + 1], rows[j + 1][i]]
            face = bm.faces.new([q[0] for q in quad])
            for loop, (_, u, v) in zip(face.loops, quad): loop[uvl].uv = (u, v)
    bm.normal_update()
    mesh = bpy.data.meshes.new(f"V4 {gender} face decal"); bm.to_mesh(mesh); bm.free()
    decal = bpy.data.objects.new(f"V4 face decal {gender}", mesh); collection.objects.link(decal)
    if mesh.polygons and mesh.polygons[0].normal.dot(fwd) < 0:
        for p in mesh.polygons: p.flip()
    mat = bpy.data.materials.get(f"V4 face decal {NAME}") or bpy.data.materials.new(f"V4 face decal {NAME}")
    decal.data.materials.append(mat)
    vg = decal.vertex_groups.new(name="Head"); vg.add(range(len(mesh.vertices)), 1.0, "REPLACE")
    decal.parent = rig; decal.matrix_parent_inverse = rig.matrix_world.inverted()
    decal.modifiers.new("Armature", "ARMATURE").object = rig
    # Skin tone for the separate grip hands, straight into the middle of the face.
    hit = tree.ray_cast(Vector((cx, rig.location.y - 1, jaw + (crown - jaw) * .6)), -fwd, 3)
    skin = fit.sample(body, px, hit[2], body.matrix_world.inverted() @ hit[0]) if hit[0] is not None else None
    report.append(f"{NAME}: head {crown - jaw:.3f} m, cheeks {width:.3f} m, face decal {w:.3f}x{h:.3f} "
                  f"eyes z={eye_z:.3f} mouth z={mouth_z:.3f}; skin {tuple(round(float(c), 3) for c in skin) if skin is not None else '?'}")


def scale_head(body, rig, factor, widen=1.0):
    """Scale the head about the top of the neck (`widen` scales its width and depth on top,
    for the round, wide Mii head of the concept video). Vertices partly on the head (the
    blend into the neck) move in proportion, so the join stays smooth."""
    if abs(factor - 1) < 1e-3 and abs(widen - 1) < 1e-3: return
    hg = body.vertex_groups["Head"].index
    pivot = rig.matrix_world @ rig.data.bones["Head"].head_local
    inv = body.matrix_world.inverted()
    for v in body.data.vertices:
        w = next((g.weight for g in v.groups if g.group == hg), 0)
        if w <= 0: continue
        p = body.matrix_world @ v.co
        s = 1 + (factor - 1) * w
        sw = 1 + (factor * widen - 1) * w
        d = p - pivot
        v.co = inv @ (pivot + Vector((d.x * sw, d.y * sw, d.z * s)))
    body.data.update()


def video_look(body, rig, px, image, report, smooth=0, leg_thicken=.024, shoe_scale=1.15, shorts_grey=.62):
    """Push the fitted scan toward the concept video's character: a smooth, toy-like surface
    (the scan's cloth wrinkles and lumps smoothed away, the head left alone so the face
    stays), thicker, simpler legs, big rounded shoes, and mid-grey shorts instead of charcoal."""
    import numpy as np
    names = {vg.index: vg.name for vg in body.vertex_groups}
    def w(v, prefix): return sum(g.weight for g in v.groups if names.get(g.group, "").startswith(prefix))
    me = body.data
    # 1. Smooth everything but the head.
    # (Geometric smoothing tears this mesh: the scan is split along its UV seams, so each side
    # of a seam smooths away from the other. Left at 0; the wrinkles are mostly the normal
    # map, which the runtime tones down instead.)
    if not smooth: mask = None
    else: mask = body.vertex_groups.get("VideoSmooth") or body.vertex_groups.new(name="VideoSmooth")
    if mask:
        for v in me.vertices:
            mask.add([v.index], max(0.0, 1 - w(v, "Head") - w(v, "Neck") * .5), "REPLACE")
        mod = body.modifiers.new("Video smooth", "SMOOTH"); mod.factor = .6; mod.iterations = smooth; mod.vertex_group = "VideoSmooth"
        body.modifiers.move(len(body.modifiers) - 1, 0)
        bpy.context.view_layer.objects.active = body
        bpy.ops.object.modifier_apply(modifier=mod.name)
        body.vertex_groups.remove(body.vertex_groups["VideoSmooth"])
    names = {vg.index: vg.name for vg in body.vertex_groups}
    # 2. Thicker legs: push leg skin out along its normal.
    me.update()
    for v in me.vertices:
        k = w(v, "UpperLeg") + w(v, "LowerLeg")
        if k > 0: v.co += v.normal * leg_thicken * min(1.0, k)
    # 3. Bigger shoes, scaled about each ankle with the sole kept on the court.
    for s in "LR":
        ankle = body.matrix_world.inverted() @ (rig.matrix_world @ rig.data.bones[f"Foot.{s}"].head_local)
        sole = min(v.co.z for v in me.vertices if w(v, f"Foot.{s}") + w(v, f"Toes.{s}") > .5)
        pivot = Vector((ankle.x, ankle.y, sole))
        for v in me.vertices:
            k = w(v, f"Foot.{s}") + w(v, f"Toes.{s}")
            if k > 0: v.co = pivot + (v.co - pivot) * (1 + (shoe_scale - 1) * min(1.0, k))
    me.update()
    # 4. Shorts: lift the dark neutral cloth to the video's mid grey (shirt, skin, and white
    # shoes are untouched: only dark, unsaturated texels move).
    rgb = px[..., :3]; mx = rgb.max(-1); mn = rgb.min(-1)
    dark = (mx < .5) & ((mx - mn) < .06)
    lift = np.clip(shorts_grey / np.maximum(mx, 1e-3), 1, 4)
    px[..., :3] = np.where(dark[..., None], np.clip(rgb * lift[..., None], 0, 1), rgb)
    image.pixels.foreach_set(px.ravel()); image.update()
    report.append(f"video look: smoothed x{smooth}, legs +{leg_thicken * 100:.1f} cm, shoes x{shoe_scale}, {int(dark.sum())} shorts texels lifted")


GRIP_SCALE = 1.25


def big_fists(gender):
    """The V4 grip fists, sized for these chunkier arms: scaled about each fist's own centre
    (they are rigid on the hand bone, so this is a pure mesh edit) and exported with the body,
    which the runtime then swaps in for the standard rig's own (TennisActor.WearBody)."""
    collection = bpy.data.collections[f"V4 {gender} Tennis | BODY"]
    fists = [o for o in collection.objects if o.name.startswith("V4 grip hand")]
    for o in fists:
        me = o.data = o.data.copy()
        centre = sum((v.co for v in me.vertices), Vector()) / len(me.vertices)
        for v in me.vertices: v.co = centre + (v.co - centre) * GRIP_SCALE
        me.update()
    return fists


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    head = float(argv[argv.index("--head") + 1]) if "--head" in argv else 1.0
    widen = float(argv[argv.index("--widen") + 1]) if "--widen" in argv else 1.0
    directory = argv[argv.index("--render") + 1] if "--render" in argv else None
    use(argv[argv.index("--name") + 1] if "--name" in argv else "Avatar")
    bpy.ops.wm.open_mainfile(filepath=str(opponents.STUDIO))
    bpy.context.window.scene = bpy.data.scenes["03 TENNIS"]
    rig = bpy.data.objects[f"{GENDER}_Tennis_Rig"]
    fit.HEIGHT = rig.data.bones["UpperArm.R"].head_local.z / LANDMARKS["shoulder"]
    fit.build_face = avatar_face
    fit.soften_skirt = grey_skirt
    fit.remove_fragments = small_fragments
    fit.remove_fists = cut_hands
    fit.REAL_ARMS = False
    report = []
    rig, body = fit.fit_character(GENDER, report, GLB, NAME, LANDMARKS)
    from coherent_avatar_body import rebuild
    rebuild(GENDER, rig, body, fit.texture_pixels(body), report)
    scale_head(body, rig, head, widen)
    if "--video-look" in argv:
        img = next(n.image for n in body.data.materials[0].node_tree.nodes if n.type == "TEX_IMAGE" and n.image and n.image.name.startswith("Color"))
        video_look(body, rig, fit.texture_pixels(body), img, report)
        fit.save_textures(body, NAME)          # ship the recoloured map
    if head != 1 or widen != 1:
        # The decal was built on the unscaled head: rebuild it on the final one.
        collection = bpy.data.collections[f"V4 {GENDER} Tennis | BODY"]
        avatar_face(GENDER, rig, body, collection, fit.texture_pixels(body), report)
    print("\n".join("FIT " + r for r in report), flush=True)
    if directory: opponents.render(NAME, GENDER, rig, directory, (1, 560, 1250, 1280))
    if "--sheets" in argv:
        # Contact-sheet frames of the (already authored) clips on this body.
        import author_tennis_motion as author
        names = argv[argv.index("--clips") + 1].split(",") if "--clips" in argv else ["Ready", "Forehand", "Backhand", "Serve"]
        views = tuple(argv[argv.index("--views") + 1].split(",")) if "--views" in argv else ("back", "three")
        author.render(argv[argv.index("--sheets") + 1], names, GENDER, views=views)
    bpy.context.scene.frame_set(1)
    big_fists(GENDER)
    if "--no-export" not in argv:
        if "--output" in argv: opponents.OUT = Path(argv[argv.index("--output") + 1])
        parts = opponents.export(NAME, GENDER, rig)
        print(f"AVATAR {NAME} exported {[p.name for p in parts]} head x{head}", flush=True)


if __name__ == "__main__":
    main()
