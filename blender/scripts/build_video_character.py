"""The concept video's tennis character, built clean (plan phase C).

A stylised toy body with simple, readable parts, sized from the reference spec
(SportsLibrary/ArtDirection/Tennis/Reference/character-spec.json) on the V5 rigs
(build_v5_rig.py): a big round head with small ears and no neck, a boxy short-sleeved tee,
mid-thigh shorts (a pleated skirt for the female), slim even tube arms ending in mitten
fists, short even tube legs, and big rounded shoes. Every surface is a flat colour from a
small palette texture -- no scan detail -- so it reads like the video.

Weights are explicit, part by part, the way a toy is jointed: the head, ears and hair ride
the head bone rigidly; the tee blends hips -> spine -> chest with its sleeves on the upper
arms; limbs are chains blended across the elbow, wrist and knee; shoes are rigid on the foot.

The body is one mesh named "V4 Higgs body <Name>" with material "V4 Higgs body <Name>" and
textures Higgs<Name>_Color / _Normal, so the Unity runtime (TennisLook.PrepareCharacter,
TennisActor.WearBody) takes it exactly like the fitted bodies it replaces. The face is the
existing decal system (fit_avatar.avatar_face + FaceAtlas_<Name>).

Run:  Blender --background STUDIO.blend --python build_video_character.py -- [--render DIR] [--export] [--save] [--floating-hands]
        --export writes the standard bodies into the studio (saved with --save, then exported by
        export_tennis_runtime.py) and Resources/Tennis/Opponents/<Name>.fbx for every variant.
"""
import math
import sys
from pathlib import Path

import bmesh
import bpy
import numpy as np
from mathutils import Matrix, Vector

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
ROOT = HERE.parents[1]
TEXTURES = ROOT / "Unity/Assets/Resources/Tennis/Characters"

# Palette swatches (sRGB). Variants override entries.
BASE = dict(skin="#E8955A", top="#C8C8C8", bottom="#6E6E6E", shoe="#F4F4F4", sole="#D8D8D8",
            hair="#5A3522", band="#F4F4F4", trim="#B0B0B0")
# Who gets built: name -> (rig gender, outfit, palette overrides).
VARIANTS = {
    "Male": ("Male", "shorts", {}),
    "Female": ("Female", "skirt", dict(top="#F0F0F0", bottom="#8C8C8C")),
    "Avatar": ("Male", "shorts", {}),
    "AvatarF": ("Female", "skirt", dict(top="#F0F0F0", bottom="#8C8C8C")),
    "Milo": ("Male", "shorts", dict(top="#F2C230", bottom="#2F5DA8", skin="#D98A52")),
    "Bruno": ("Male", "shorts", dict(top="#2E8B57", bottom="#F4F4F4", skin="#9C5F38")),
    "Viktor": ("Male", "shorts", dict(top="#1F2F5A", bottom="#F4F4F4", skin="#F0B48A")),
    "Suki": ("Female", "skirt", dict(top="#E85A8C", bottom="#F4F4F4", hair="#1C1410", skin="#EBB083")),
    "Tama": ("Male", "shorts", dict(top="#1FA8A0", bottom="#F4F4F4", hair="#1C1410", skin="#B8784A", band="#F08A2A")),
    "Dex": ("Male", "shorts", dict(top="#1E2A6E", bottom="#1A1A1A", hair="#120C08", skin="#5A3522", band="#E4F030")),
    "Lina": ("Female", "skirt", dict(top="#8FDCC0", bottom="#8FDCC0", hair="#E8C860", skin="#F2C4A0", band="#F4F4F4")),
    "Rosa": ("Female", "skirt", dict(top="#7A2A9A", bottom="#C0308C", hair="#1C1410", skin="#C98A5E", band="#E0308C")),
    "Jax": ("Male", "shorts", dict(top="#1A1A1A", bottom="#F4F4F4", hair="#C8401E", skin="#F0C0A0", band="#F07A1E")),
    "Nadia": ("Female", "skirt", dict(top="#4A78A8", bottom="#1F2F5A", hair="#E4DCC8", skin="#F5D2BC", band="#F4F4F4")),
}
SWATCHES = ["skin", "top", "bottom", "shoe", "sole", "hair", "band", "trim"]
# "--floating-hands": no arm tubes -- the fists float, following the hand bones (a trial look).
FLOATING_HANDS = "--floating-hands" in sys.argv


def srgb(h):
    h = h.lstrip("#"); return [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]


def palette_image(name, colours):
    """8x1 swatches (upscaled to 64x8 so filtering never bleeds): one texel column per colour."""
    w, h = 64, 8
    img = bpy.data.images.get(f"Color {name}") or bpy.data.images.new(f"Color {name}", w, h, alpha=False)
    img.colorspace_settings.name = "sRGB"
    px = np.zeros((h, w, 4), np.float32)
    for i, key in enumerate(SWATCHES):
        px[:, i * 8:(i + 1) * 8, :3] = srgb(colours[key]); px[..., 3] = 1
    img.pixels.foreach_set(px.ravel()); img.update()
    return img


def swatch_uv(key):
    return ((SWATCHES.index(key) * 8 + 4) / 64, .5)


# ---------------------------------------------------------------- geometry helpers

def ellipsoid(center, radii, segments=32, rings=16):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=segments, v_segments=rings, radius=1)
    bmesh.ops.scale(bm, vec=radii, verts=bm.verts)
    bmesh.ops.translate(bm, vec=center, verts=bm.verts)
    return bm


def rounded_box(center, half, level=2, taper_top=1.0):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=2)
    for v in bm.verts:
        s = taper_top if v.co.z > 0 else 1
        v.co = Vector((v.co.x * half[0] * s, v.co.y * half[1] * s, v.co.z * half[2])) + Vector(center)
    bmesh.ops.subdivide_edges(bm, edges=bm.edges[:], cuts=2, use_grid_fill=True)
    for _ in range(level):
        bmesh.ops.smooth_vert(bm, verts=bm.verts, factor=.5, use_axis_x=True, use_axis_y=True, use_axis_z=True)
    # Rounding pulls the box in toward a blob; scale it back out to the size asked for.
    c = Vector(center)
    lo = Vector([min(v.co[i] for v in bm.verts) for i in range(3)]); hi = Vector([max(v.co[i] for v in bm.verts) for i in range(3)])
    k = [2 * half[i] / max(1e-6, hi[i] - lo[i]) for i in range(3)]
    mid = (lo + hi) / 2
    for v in bm.verts:
        d = v.co - mid
        v.co = c + Vector((d.x * k[0], d.y * k[1], d.z * k[2]))
    return bm


def skin_chain(points, radii, subdiv=2):
    """A tube through points (Skin modifier + Subdivision), baked to a bmesh."""
    me = bpy.data.meshes.new("chain")
    me.from_pydata([tuple(p) for p in points], [(i, i + 1) for i in range(len(points) - 1)], [])
    ob = bpy.data.objects.new("chain", me); bpy.context.scene.collection.objects.link(ob)
    ob.modifiers.new("Skin", "SKIN")
    for i, sv in enumerate(me.skin_vertices[0].data):
        r = radii[i]; sv.radius = (r, r) if not isinstance(r, tuple) else r
        sv.use_root = i == 0
    s = ob.modifiers.new("Sub", "SUBSURF"); s.levels = subdiv
    dg = bpy.context.evaluated_depsgraph_get()
    baked = bpy.data.meshes.new_from_object(ob.evaluated_get(dg))
    bm = bmesh.new(); bm.from_mesh(baked)
    bpy.data.objects.remove(ob, do_unlink=True); bpy.data.meshes.remove(me); bpy.data.meshes.remove(baked)
    return bm


class Part:
    def __init__(self, bm, swatch, weights):
        self.bm, self.swatch, self.weights = bm, swatch, weights    # weights(point) -> {bone: w}


def build_body(name, gender, outfit, colours, rig):
    """One mesh of all parts, weighted to `rig`, in the rig's rest pose."""
    R = rig.matrix_world
    bones = rig.data.bones
    H = lambda n: R @ bones[n].head_local
    T = lambda n: R @ bones[n].tail_local
    o = R.translation
    parts = []
    rigid = lambda b: (lambda p: {b: 1.0})

    def along(a, b, p):
        ab = b - a; return max(0.0, min(1.0, (p - a).dot(ab) / max(ab.length_squared, 1e-9)))

    # Head: round, a touch wider than tall, bottom just above the tee; small ears.
    hc = o + Vector((0, .005, 1.46))
    parts.append(Part(ellipsoid(hc, (.29, .268, .25), 40, 20), "skin", rigid("Head")))
    for s in (1, -1):
        parts.append(Part(ellipsoid(hc + Vector((s * .286, .015, -.01)), (.03, .024, .05), 16, 8), "skin", rigid("Head")))
    # Tee: a boxy torso, slightly tapered to the shoulders, with short sleeves.
    torso = rounded_box(o + Vector((0, .005, .905)), (.23, .15, .255), 2, .9)

    def torso_w(p):
        z = p.z - o.z
        if z < .74: k = max(0.0, (z - .66) / .08); return {"Hips": 1 - k * .5, "Spine": k * .5}
        if z < .95: k = (z - .74) / .21; return {"Spine": 1 - k * .6, "Chest": k * .6 + .0}
        return {"Chest": 1.0}
    parts.append(Part(torso, "top", torso_w))
    for s in "LR":
        a, e = H(f"UpperArm.{s}"), H(f"LowerArm.{s}")
        sleeve = skin_chain([a + (e - a) * .05, a + (e - a) * .25, a + (e - a) * .42], [.066, .064, .062])
        parts.append(Part(sleeve, "top", (lambda sd: lambda p: {f"UpperArm.{sd}": 1.0})(s)))
    # Arms: slim even tubes into mitten fists (thumb on the inside).
    for s in "LR":
        a, e, w = H(f"UpperArm.{s}"), H(f"LowerArm.{s}"), H(f"Hand.{s}")
        hand_dir = (T(f"Hand.{s}") - w).normalized()
        arm = skin_chain([a + (e - a) * .1, e, w, w + hand_dir * .02], [.046, .043, .038, .038])

        def arm_w(p, a=a, e=e, w=w, s=s):
            te = (p - e).dot((w - e).normalized()); tw = (p - w).dot((w - e).normalized())
            if tw > -.03: k = min(1, (tw + .03) / .06); return {f"LowerArm.{s}": 1 - k, f"Hand.{s}": k}
            if te < .04: k = max(0, min(1, (te + .04) / .08)); return {f"UpperArm.{s}": 1 - k, f"LowerArm.{s}": k}
            return {f"LowerArm.{s}": 1.0}
        if not FLOATING_HANDS: parts.append(Part(arm, "skin", arm_w))
        fc = w + hand_dir * .06
        side = Vector((1 if s == "L" else -1, 0, 0))
        parts.append(Part(ellipsoid(fc, (.05, .058, .062), 20, 10), "skin", rigid(f"Hand.{s}")))
        parts.append(Part(ellipsoid(fc - side * .045 + Vector((0, -.03, .02)), (.022, .024, .034), 12, 6), "skin", rigid(f"Hand.{s}")))
    # Legs: short even tubes, knee blended.
    for s in "LR":
        hp, kn, an = H(f"UpperLeg.{s}"), H(f"LowerLeg.{s}"), H(f"Foot.{s}")
        leg = skin_chain([hp + (kn - hp) * .25, kn, an + Vector((0, 0, .02))], [.076, .074, .07])

        def leg_w(p, hp=hp, kn=kn, an=an, s=s):
            t = (p - kn).dot((an - kn).normalized())
            k = max(0, min(1, (t + .05) / .1))
            return {f"UpperLeg.{s}": 1 - k, f"LowerLeg.{s}": k}
        parts.append(Part(leg, "skin", leg_w))
    # Bottoms: mid-thigh shorts, or a flared pleated skirt.
    if outfit == "shorts":
        shorts = rounded_box(o + Vector((0, .005, .62)), (.215, .14, .09), 2)
        parts.append(Part(shorts, "bottom", lambda p: {"Hips": 1.0}))
        for s in "LR":
            hp, kn = H(f"UpperLeg.{s}"), H(f"LowerLeg.{s}")
            tube = skin_chain([hp + Vector((.0, 0, -.02)), hp + (kn - hp) * .88], [.108, .11])
            parts.append(Part(tube, "bottom", (lambda sd: lambda p: {f"UpperLeg.{sd}": .85, "Hips": .15})(s)))
    else:
        bm = bmesh.new()
        bmesh.ops.create_cone(bm, cap_ends=False, cap_tris=False, segments=24, radius1=.25, radius2=.195, depth=.2)
        for v in bm.verts:
            ang = math.atan2(v.co.y, v.co.x); pleat = 1 + .04 * math.cos(ang * 12) * (0.5 - v.co.z / .2)
            v.co.x *= pleat; v.co.y *= pleat * .8
        bmesh.ops.translate(bm, vec=o + Vector((0, .005, .60)), verts=bm.verts)
        parts.append(Part(bm, "bottom", lambda p: {"Hips": 1.0}))
        waist = rounded_box(o + Vector((0, .005, .69)), (.19, .128, .04), 2)
        parts.append(Part(waist, "bottom", lambda p: {"Hips": 1.0}))
    # Shoes: big rounded boxes, heel under the ankle, toe forward.
    for s in "LR":
        an = H(f"Foot.{s}")
        shoe = rounded_box(Vector((an.x, an.y - .065, o.z + .085)), (.095, .17, .085), 3)
        parts.append(Part(shoe, "shoe", rigid(f"Foot.{s}")))
        sole = rounded_box(Vector((an.x, an.y - .065, o.z + .016)), (.098, .172, .018), 2)
        parts.append(Part(sole, "sole", rigid(f"Foot.{s}")))
    # Female hair: a cap over the crown, a ponytail and a headband.
    if gender == "Female":
        cap = ellipsoid(hc + Vector((0, .02, .03)), (.282, .262, .235), 40, 20)
        doomed = [v for v in cap.verts if v.co.z < hc.z - .02 or (v.co.y < hc.y - .12 and v.co.z < hc.z + .12)]
        bmesh.ops.delete(cap, geom=doomed, context="VERTS")
        parts.append(Part(cap, "hair", rigid("Head")))
        tail = skin_chain([hc + Vector((0, .21, .17)), hc + Vector((0, .3, .1)), hc + Vector((0, .34, -.02)), hc + Vector((0, .32, -.12))],
                          [.075, .085, .07, .035])
        parts.append(Part(tail, "hair", rigid("Head")))
        band = bmesh.new()
        bmesh.ops.create_circle(band, segments=40, radius=1)
        # Hug the hair: the head's own width at the band's height, plus the hair and a little.
        for v in band.verts:
            dz = .085 + v.co.y * -.05
            k = math.sqrt(max(0.0, 1 - (dz / .235) ** 2)) + .06
            v.co = hc + Vector((v.co.x * .27 * k, v.co.y * .25 * k + .012, dz))
        before = set(band.verts)
        bmesh.ops.extrude_edge_only(band, edges=band.edges[:])
        for v in band.verts:
            if v not in before: v.co.z += .035
        parts.append(Part(band, "band", rigid("Head")))

    # Join: one mesh, palette UVs, explicit weights.
    me = bpy.data.meshes.new(f"V4 Higgs body {name}")
    out = bmesh.new()
    uvl = out.loops.layers.uv.new("UVMap")
    dvl = out.verts.layers.deform.verify()
    group_names = [b.name for b in rig.data.bones]
    for part in parts:
        offset = len(out.verts)
        vmap = {}
        for v in part.bm.verts:
            nv = out.verts.new(v.co); vmap[v] = nv
            for bone, wt in part.weights(v.co).items():
                if wt > 0: nv[dvl][group_names.index(bone)] = wt
        out.verts.ensure_lookup_table()
        u = swatch_uv(part.swatch)
        for f in part.bm.faces:
            try: nf = out.faces.new([vmap[v] for v in f.verts])
            except ValueError: continue
            nf.smooth = True
            for loop in nf.loops: loop[uvl].uv = u
        part.bm.free()
    bmesh.ops.recalc_face_normals(out, faces=out.faces[:])
    out.to_mesh(me); out.free()
    body = bpy.data.objects.new(f"V4 Higgs body {name}", me)
    for n in group_names: body.vertex_groups.new(name=n)
    mat = bpy.data.materials.get(f"V4 Higgs body {name}") or bpy.data.materials.new(f"V4 Higgs body {name}")
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    tex = next((n for n in nodes if n.type == "TEX_IMAGE"), None) or nodes.new("ShaderNodeTexImage")
    tex.image = palette_image(name, colours); tex.interpolation = "Closest"
    bsdf = next(n for n in nodes if n.type == "BSDF_PRINCIPLED")
    mat.node_tree.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    me.materials.append(mat)
    return body


def save_textures(name, image):
    TEXTURES.mkdir(parents=True, exist_ok=True)
    # Save the palette itself: a copy of a freshly generated image comes out black.
    image.filepath_raw = str(TEXTURES / f"Higgs{name}_Color.png"); image.file_format = "PNG"; image.save()
    flat = bpy.data.images.new(f"Normal {name}", 4, 4, alpha=False)
    flat.pixels.foreach_set(np.tile([.5, .5, 1, 1], 16).astype(np.float32)); flat.update()
    flat.filepath_raw = str(TEXTURES / f"Higgs{name}_Normal.png"); flat.file_format = "PNG"; flat.save()


def install(name, gender, outfit, colours, report):
    """Replace this rig's body with the clean build (and its face decal)."""
    import fit_avatar
    rig = bpy.data.objects[f"{gender}_Tennis_Rig"]
    rig.data.pose_position = "REST"; bpy.context.view_layer.update()
    collection = bpy.data.collections[f"V4 {gender} Tennis | BODY"]
    for o in list(collection.objects): bpy.data.objects.remove(o, do_unlink=True)
    body = build_body(name, gender, outfit, colours, rig)
    collection.objects.link(body)
    body.parent = rig; body.matrix_parent_inverse = rig.matrix_world.inverted()
    body.modifiers.new("Armature", "ARMATURE").object = rig
    # The existing face-decal system, placed on the new head.
    fit_avatar.use("AvatarF" if gender == "Female" else "Avatar")
    fit_avatar.NAME = name
    image = next(n.image for n in body.data.materials[0].node_tree.nodes if n.type == "TEX_IMAGE")
    px = np.empty(image.size[0] * image.size[1] * 4, np.float32); image.pixels.foreach_get(px)
    fit_avatar.avatar_face(gender, rig, body, collection, px.reshape(image.size[1], image.size[0], 4), report)
    rig.data.pose_position = "POSE"
    save_textures(name, image)
    report.append(f"{name}: {len(body.data.vertices)} verts, outfit {outfit}")
    return rig, body


def ortho(directory):
    """Orthographic front/back/side of each body standing in Ready, isolated, for silhouette
    scoring against the reference (Reference/silhouette-*.png)."""
    scene = bpy.context.scene
    out = Path(directory); out.mkdir(parents=True, exist_ok=True)
    cam = bpy.data.objects.new("Ortho", bpy.data.cameras.new("Ortho")); scene.collection.objects.link(cam)
    cam.data.type = "ORTHO"; cam.data.ortho_scale = 2.0; scene.camera = cam
    scene.render.engine = "BLENDER_WORKBENCH"; scene.display.shading.light = "FLAT"; scene.display.shading.color_type = "TEXTURE"
    scene.render.resolution_x = 700; scene.render.resolution_y = 1000; scene.render.film_transparent = True
    for gender in ("Male", "Female"):
        rig = bpy.data.objects[f"{gender}_Tennis_Rig"]
        keep = set(bpy.data.collections[f"V4 {gender} Tennis | BODY"].objects)
        for o in scene.objects:
            if o.type in {"MESH", "CURVE"}: o.hide_render = o not in keep
        track = next(t for t in rig.animation_data.nla_tracks if t.name.startswith("V4 |"))
        st = next(x for x in track.strips if x.name == "Ready RH"); scene.frame_set(int(st.frame_start) + 10)
        c = rig.matrix_world.translation + Vector((0, 0, .9))
        for view, d in (("front", Vector((0, -6, 0))), ("back", Vector((0, 6, 0))), ("side", Vector((6, 0, 0)))):
            cam.location = c + d; cam.rotation_euler = (-d).to_track_quat("-Z", "Y").to_euler()
            scene.render.filepath = str(out / f"{gender}-{view}.png"); bpy.ops.render.render(write_still=True)


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    opt = lambda k, d=None: argv[argv.index(k) + 1] if k in argv else d
    only = opt("--only", "").split(",") if opt("--only") else None
    report = []
    if "--export" in argv:
        # Each variant from a freshly opened studio: the export clears the rig's animation.
        import fit_opponents
        studio = bpy.data.filepath
        for name, (gender, outfit, over) in VARIANTS.items():
            if name in ("Male", "Female") or (only and name not in only): continue
            bpy.ops.wm.open_mainfile(filepath=studio)
            bpy.context.window.scene = bpy.data.scenes["03 TENNIS"]
            rig, body = install(name, gender, outfit, {**BASE, **over}, report)
            fit_opponents.export(name, gender, rig)
            print(f"CHAR exported {name}", flush=True)
        print("\n".join("CHAR " + r for r in report), flush=True)
        return
    bpy.context.window.scene = bpy.data.scenes["03 TENNIS"]
    # The studio keeps the standard bodies (one per rig) for export_tennis_runtime.py.
    for name in ("Male", "Female"):
        gender, outfit, over = VARIANTS[name]
        install(name, gender, outfit, {**BASE, **over}, report)
    print("\n".join("CHAR " + r for r in report), flush=True)
    if opt("--render"):
        import author_tennis_motion as author
        for g in ("Male", "Female"):
            author.render(opt("--render") + f"/{g}", opt("--clips", "Ready,Forehand").split(","), g, per=int(opt("--per", 6)), views=tuple(opt("--views", "front,back,three").split(",")))
    if opt("--ortho"): ortho(opt("--ortho"))
    if "--save" in argv:
        bpy.ops.wm.save_mainfile(); print("CHAR saved", flush=True)


if __name__ == "__main__":
    main()
