"""Dress the tennis players in the Tripo-generated modern kit (the user's pick over Higgsfield).

For each body name the Tripo model (one mesh, 4K base colour + normal) is imported, turned to
face the court, fitted to the V5 rig's height, cut to the game budget and weighted to the rig
(kit_compare.fit_to_rig / bind). It then replaces that rig's body under the runtime's naming:
mesh and material "V4 Higgs body <Name>", textures Higgs<Name>_Color / _Normal (2048), with
the usual face decal (fit_avatar.avatar_face) on the new head.

--floating-hands removes the arms and keeps only the hands (with their wristbands), which
float on the hand bones: every vertex weighted mostly to a forearm goes, and upper-arm vertices
below the short sleeve go. Every opening the cut leaves is sealed with a soft dome in the
colour of its rim, so the sleeves end in closed stubs and nothing shows through.

Run:  Blender --background STUDIO.blend --python install_tripo_kit.py -- MALE.fbx FEMALE.fbx [--floating-hands] [--save] [--export]
        --save    Male/Female into the studio (for export_tennis_runtime.py)
        --export  Avatar/AvatarF (the players' bodies) to Resources/Tennis/Opponents, each from a
                  freshly opened studio as build_video_character.py does
"""
import sys
from pathlib import Path

import bpy
import numpy as np
from mathutils import Vector

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import kit_compare  # noqa: E402
from build_video_character import TEXTURES  # noqa: E402

TURN = -90          # the Tripo models arrive facing +X


def drop_arms(body, rig):
    """Cut the arms away with clean planar cuts, keeping the short sleeves (as stubs fixed to
    the chest) and the hands with their wristbands (rigid on the hand bones)."""
    import bmesh
    names = [g.name for g in body.vertex_groups]
    bm = bmesh.new(); bm.from_mesh(body.data)
    dl = bm.verts.layers.deform.verify()
    to_world = body.matrix_world

    def dominant(v):
        items = v[dl].items()
        return names[max(items, key=lambda x: x[1])[0]] if items else ""

    def axis(bone):
        b = rig.data.bones[bone]
        a, t = rig.matrix_world @ b.head_local, rig.matrix_world @ b.tail_local
        return a, t

    inv = to_world.inverted()
    before = len(bm.verts)
    for s in "LR":
        # The sleeve: the arm's faces (upper arm and beyond) are cut straight across the upper
        # arm below the sleeve hem, and everything past the cut goes.
        arm = {f"UpperArm.{s}", f"LowerArm.{s}"}
        faces = [f for f in bm.faces if all(dominant(v) in arm for v in f.verts)]
        a, t = axis(f"UpperArm.{s}")
        co = inv @ (a + (t - a) * .42); no = (inv.to_3x3() @ (t - a)).normalized()
        geom = faces + list({e for f in faces for e in f.edges}) + list({v for f in faces for v in f.verts})
        bmesh.ops.bisect_plane(bm, geom=geom, plane_co=co, plane_no=no, clear_outer=True)
        # The wristband: the forearm is cut a fifth short of the wrist, keeping the band.
        faces = [f for f in bm.faces if all(dominant(v) in {f"LowerArm.{s}", f"Hand.{s}"} for v in f.verts)]
        a, t = axis(f"LowerArm.{s}")
        co = inv @ (a + (t - a) * .8); no = (inv.to_3x3() @ (t - a)).normalized()
        geom = faces + list({e for f in faces for e in f.edges}) + list({v for f in faces for v in f.verts})
        bmesh.ops.bisect_plane(bm, geom=geom, plane_co=co, plane_no=no, clear_inner=True)
        # Whatever forearm is left between the two cuts (faces mixing upper and lower arm).
        a, t = axis(f"LowerArm.{s}")
        stray = [v for v in bm.verts if dominant(v) == f"LowerArm.{s}" and
                 ((to_world @ v.co) - a).dot((t - a).normalized()) < (t - a).length * .78]
        bmesh.ops.delete(bm, geom=stray, context="VERTS")
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")
    removed = before - len(bm.verts)
    # Weights: what floats (hand + wristband) is rigid on its hand; with no arm inside, the
    # sleeve stubs and shoulders ride the chest instead of the arm bones (lifting those
    # stretched the stubs into torn strips).
    chest = names.index("Chest")
    for v in bm.verts:
        d = dominant(v)
        if d.startswith(("Hand.", "LowerArm.")):
            hand = names.index("Hand." + d[-1]); v[dl].clear(); v[dl][hand] = 1.0
            continue
        moved = 0.0
        for g, w in list(v[dl].items()):
            if names[g].startswith(("UpperArm.", "Shoulder.")): moved += w; del v[dl][g]
        if moved: v[dl][chest] = v[dl].get(chest, 0.0) + moved
    capped = cap_holes(bm)
    bm.to_mesh(body.data); bm.free(); body.data.update()
    return removed, capped


def boundary_loops(bm):
    """The mesh's open edges, chained into closed loops of vertices."""
    edges = {e for e in bm.edges if e.is_boundary}
    loops = []
    while edges:
        e = edges.pop(); loop = [e.verts[0], e.verts[1]]
        while True:
            nxt = next((x for x in loop[-1].link_edges if x in edges), None)
            if nxt is None: break
            edges.discard(nxt); v = nxt.other_vert(loop[-1])
            if v is loop[0]: break
            loop.append(v)
        if len(loop) >= 3: loops.append(loop)
    return loops


def cap_holes(bm):
    """Close every opening the cut left -- the sleeve ends and the wristbands' inner ends -- with
    a soft dome: a fan to a centre vertex pushed out of the hole. The cap takes the average
    colour (UV) and skin weights of its rim, so a sleeve cap rides the arm as the sleeve does."""
    uv = bm.loops.layers.uv.active
    deform = bm.verts.layers.deform.active
    count = 0
    for loop in boundary_loops(bm):
        rim = [v.co.copy() for v in loop]
        centre = sum(rim, Vector()) / len(rim)
        radius = sum((p - centre).length for p in rim) / len(rim)
        # Outward: away from the faces the rim belongs to (the tube's inside is behind it).
        inward = Vector()
        for v in loop:
            for f in v.link_faces: inward += f.calc_center_median() - centre
        normal = Vector()
        for i in range(len(rim)): normal += (rim[i] - centre).cross(rim[(i + 1) % len(rim)] - centre)
        if normal.length < 1e-9: continue
        normal.normalize()
        if normal.dot(inward) > 0: normal = -normal
        tip = bm.verts.new(centre + normal * radius * .35)
        if deform:
            acc = {}
            for v in loop:
                for g, w in v[deform].items(): acc[g] = acc.get(g, 0) + w / len(loop)
            for g, w in acc.items(): tip[deform][g] = w
        rim_uv = None
        if uv:
            us = [l[uv].uv.copy() for v in loop for l in v.link_loops]
            rim_uv = sum(us, Vector((0, 0))) / len(us) if us else None
        faces = []
        for i in range(len(loop)):
            try: f = bm.faces.new((loop[i], loop[(i + 1) % len(loop)], tip))
            except ValueError: continue
            f.smooth = True; faces.append(f)
            if rim_uv is not None:
                for l in f.loops: l[uv].uv = rim_uv
        # Wind the cap to face out of the hole.
        for f in faces:
            if f.normal_update() or f.normal.dot(normal) < 0: f.normal_flip()
        count += 1
    return count


def save_maps(name, material):
    TEXTURES.mkdir(parents=True, exist_ok=True)
    for node in material.node_tree.nodes:
        if node.type != "TEX_IMAGE" or not node.image: continue
        socket = [l.to_socket.name for l in node.outputs[0].links]
        kind = "Color" if "Base Color" in socket else "Normal" if node.image.name.endswith("normal") else None
        if not kind: continue
        img = node.image.copy(); img.scale(2048, 2048)
        img.filepath_raw = str(TEXTURES / f"Higgs{name}_{kind}.png"); img.file_format = "PNG"; img.save()


def install(name, gender, path, floating, report):
    import fit_avatar
    rig = bpy.data.objects[f"{gender}_Tennis_Rig"]
    collection = bpy.data.collections[f"V4 {gender} Tennis | BODY"]
    for o in list(collection.objects): bpy.data.objects.remove(o, do_unlink=True)
    body, extra = kit_compare.import_model(path)
    for o in extra: bpy.data.objects.remove(o, do_unlink=True)
    body.rotation_euler = (0, 0, np.radians(TURN)); bpy.context.view_layer.update()
    bpy.ops.object.select_all(action="DESELECT"); body.select_set(True); bpy.context.view_layer.objects.active = body
    bpy.ops.object.transform_apply(rotation=True)
    kit_compare.fit_to_rig(body, rig)
    kit_compare.bind(body, rig, gender)
    for c in body.users_collection: c.objects.unlink(body)
    collection.objects.link(body)
    rig.data.pose_position = "REST"; bpy.context.view_layer.update()
    cut, capped = drop_arms(body, rig) if floating else (0, 0)
    body.name = body.data.name = f"V4 Higgs body {name}"
    mat = body.data.materials[0]; mat.name = f"V4 Higgs body {name}"
    save_maps(name, mat)
    # Face decal on the new head; skin sampled from the base colour map.
    image = next(n.image for n in mat.node_tree.nodes if n.type == "TEX_IMAGE" and [l.to_socket.name for l in n.outputs[0].links] == ["Base Color"])
    px = np.empty(image.size[0] * image.size[1] * 4, np.float32); image.pixels.foreach_get(px)
    fit_avatar.use("AvatarF" if gender == "Female" else "Avatar"); fit_avatar.NAME = name
    fit_avatar.avatar_face(gender, rig, body, collection, px.reshape(image.size[1], image.size[0], 4), report)
    rig.data.pose_position = "POSE"
    report.append(f"{name}: {len(body.data.vertices)} verts{f', {cut} arm vertices removed, {capped} holes capped' if floating else ''}")
    return rig


def main():
    argv = sys.argv[sys.argv.index("--") + 1:]
    male, female = argv[0], argv[1]
    floating = "--floating-hands" in argv
    report = []
    studio = bpy.data.filepath
    if "--export" in argv:
        import fit_opponents
        for name, gender, path in (("Avatar", "Male", male), ("AvatarF", "Female", female)):
            bpy.ops.wm.open_mainfile(filepath=studio)
            bpy.context.window.scene = bpy.data.scenes["03 TENNIS"]
            rig = install(name, gender, path, floating, report)
            fit_opponents.export(name, gender, rig)
    else:
        bpy.context.window.scene = bpy.data.scenes["03 TENNIS"]
        for name, gender, path in (("Male", "Male", male), ("Female", "Female", female)):
            install(name, gender, path, floating, report)
        if "--save" in argv:
            bpy.ops.wm.save_mainfile(); report.append("saved")
    print("\n".join("TRIPO " + r for r in report), flush=True)


if __name__ == "__main__":
    main()
