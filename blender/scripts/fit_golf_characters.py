"""Put the golf versions of Adnan's Higgsfield players (blender/characters/golf, Tripo H3.1) onto
the V4 golf rig — Adnan's fit_higgs_characters.py (origin/final-characters) for tennis, run on the
golf scene. His method, unchanged:

  1. Import the GLB, turn it to face -Y like the rig, scale it to the rig's height.
  2. Build a temporary copy of the rig whose joints sit at the mesh's own joints (landmarks
     measured from horizontal slices) and skin the mesh to it.
  3. Pose that copy onto the real rig's rest joints -- limbs stretch along their length only,
     head, hands and feet move rigidly so they keep their shape -- and bake the result.
  4. The baked mesh now matches the rig, and its vertex groups already name the rig's bones,
     so it binds to the real rig directly.

The mesh's own fists are removed: the V4 grip hands hold the club. The face decal is rebuilt on
the new head, inside the bare-skin window under the cap / visor (GolferView draws the face from
Adnan's expression atlas). The rig cannot change: the club is baked around the V4 arm lengths.

Run it ahead of the golfer export, in one Blender session:
    Blender -b sports-animation-studio-v4.blend --python blender/scripts/fit_golf_characters.py \
        --python blender/scripts/adnan_golfer_export.py -- --gender Male|Female
"""
import math
import sys
from pathlib import Path

import bmesh
import bpy
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree
from mathutils.kdtree import KDTree

ROOT = Path(__file__).resolve().parents[2]
import os
# The full-resolution Tripo GLBs fit cleanly; heat weighting fails on the repo's slimmed copies.
MODELS = Path(os.environ.get("GOLF_MODELS", ROOT / "blender/characters/golf"))
# Scale: the mesh is sized so its shoulders land on the rig's shoulders (1.20 m). These
# characters have a tall collar-and-neck above the shoulders that the rig does not (its neck
# starts at shoulder height); sizing by the crown instead crushed the upper chest by half and
# the torso vanished. The neck and head ride on top rigidly, so the figure stands ~2 m tall.
HEIGHT = 2.01

# Mesh joints as fractions of the mesh height. y: lateral (+ is the character's left),
# z: height, f: forward. Measured from slices of each model. The models are generated in an
# A-pose (arms clear of the body): in the arms-down designs the fists fused into the shorts
# and skirt, and removing them tore the cloth.
LANDMARKS = {
    "Male": dict(hips=.40, spine=.47, chest=.54, neck=.61, head=.755, crown=.95,
                 leg=.085, knee=.22, knee_y=.09, ankle=.07, ankle_y=.1, ball_f=.075, toe_f=.14,
                 clav=.61, shoulder=.595, shoulder_y=.13, elbow=.54, elbow_y=.20,
                 wrist=.48, wrist_y=.273, hand=.41, hand_y=.30, arm_f=0),
    "Female": dict(hips=.40, spine=.47, chest=.545, neck=.63, head=.76, crown=.95,
                   leg=.08, knee=.22, knee_y=.09, ankle=.07, ankle_y=.105, ball_f=.075, toe_f=.14,
                   clav=.63, shoulder=.615, shoulder_y=.12, elbow=.555, elbow_y=.185,
                   wrist=.485, wrist_y=.25, hand=.43, hand_y=.27, arm_f=0),
}
RIGID = {"Head", "Neck", "Hand.L", "Hand.R", "Foot.L", "Foot.R", "Toes.L", "Toes.R"}


def mesh_joints(k):
    """Head/tail of every bone on the mesh, in mesh-height fractions (y lateral, f forward, z)."""
    j = {}
    j["Root"] = ((0, 0, 0), (0, 0, .08))
    j["Hips"] = ((0, 0, k["hips"]), (0, 0, k["spine"]))
    j["Spine"] = ((0, 0, k["spine"]), (0, 0, k["chest"]))
    j["Chest"] = ((0, 0, k["chest"]), (0, 0, k["neck"]))
    j["Neck"] = ((0, 0, k["neck"]), (0, 0, k["head"]))
    j["Head"] = ((0, 0, k["head"]), (0, 0, k["crown"]))
    for side, s in (("L", 1), ("R", -1)):
        j[f"Shoulder.{side}"] = ((0, 0, k["clav"]), (s * k["shoulder_y"], k["arm_f"], k["shoulder"]))
        j[f"UpperArm.{side}"] = ((s * k["shoulder_y"], k["arm_f"], k["shoulder"]), (s * k["elbow_y"], k["arm_f"], k["elbow"]))
        j[f"LowerArm.{side}"] = ((s * k["elbow_y"], k["arm_f"], k["elbow"]), (s * k["wrist_y"], k["arm_f"], k["wrist"]))
        j[f"Hand.{side}"] = ((s * k["wrist_y"], k["arm_f"], k["wrist"]), (s * k["hand_y"], k["arm_f"], k["hand"]))
        j[f"UpperLeg.{side}"] = ((s * k["leg"], 0, k["hips"]), (s * k["knee_y"], 0, k["knee"]))
        j[f"LowerLeg.{side}"] = ((s * k["knee_y"], 0, k["knee"]), (s * k["ankle_y"], 0, k["ankle"]))
        j[f"Foot.{side}"] = ((s * k["ankle_y"], 0, k["ankle"]), (s * k["ankle_y"], k["ball_f"], .035))
        j[f"Toes.{side}"] = ((s * k["ankle_y"], k["ball_f"], .035), (s * k["ankle_y"], k["toe_f"], .035))
    return j


def to_world(p, origin):
    """Mesh fractions -> world, for a character facing -Y whose left is +X."""
    y, f, z = p
    return Vector((origin.x + y * HEIGHT, origin.y - f * HEIGHT, .007 + z * HEIGHT))


def import_body(gender, rig):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=str(MODELS / f"golfer-{gender.lower()}.glb"))
    new = [o for o in bpy.data.objects if o not in before]
    body = next(o for o in new if o.type == "MESH")
    body.parent = None
    for o in new:
        if o is not body: bpy.data.objects.remove(o, do_unlink=True)
    bpy.context.view_layer.objects.active = body
    bpy.ops.object.select_all(action="DESELECT"); body.select_set(True)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    vs = [v.co for v in body.data.vertices]
    lo = min(v.z for v in vs); hi = max(v.z for v in vs)
    s = HEIGHT / (hi - lo)
    me = body.data
    for v in me.vertices:
        x, y, z = v.co
        # Tripo faces +X with its left on +Y; the rig faces -Y with its left on +X.
        v.co = Vector((rig.location.x + y * s, rig.location.y - x * s, .007 + (z - lo) * s))
    me.update()
    body.name = f"V4 Higgs body {gender}"
    body.data.name = body.name
    body.data.materials[0].name = body.name
    return body


def fit_armature(gender, rig, joints):
    arm = rig.copy(); arm.data = rig.data.copy(); arm.animation_data_clear()
    # The rig is held in rest position while fitting; the copy must not be, or its
    # constraints never evaluate and nothing is fitted.
    arm.data.pose_position = "POSE"
    # The copy also carries the rig's current animated pose (the Ready stance turns the chest);
    # clear it, or that roll survives the constraints and twists the fitted body.
    for pb in arm.pose.bones:
        pb.location = (0, 0, 0); pb.rotation_quaternion = (1, 0, 0, 0)
        pb.rotation_euler = (0, 0, 0); pb.scale = (1, 1, 1)
    arm.name = f"FIT {gender}"
    bpy.context.scene.collection.objects.link(arm)
    for o in bpy.context.selected_objects: o.select_set(False)
    bpy.context.view_layer.objects.active = arm; arm.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    inv = arm.matrix_world.inverted()
    for eb in arm.data.edit_bones:
        eb.use_connect = False
        head, tail = joints[eb.name]
        eb.head = inv @ to_world(head, rig.location)
        eb.tail = inv @ to_world(tail, rig.location)
        eb.inherit_scale = "NONE"
    bpy.ops.object.mode_set(mode="OBJECT")
    return arm


def distance_weights(body, arm):
    """When bone heat finds no solution at all (the pleated skirt defeats it on the female golf
    model), weight each vertex from its two nearest bones by distance instead; the smoothing,
    rigid head and skirt passes that follow shape it as they do heat weights."""
    segs = {b.name: (arm.matrix_world @ b.head_local, arm.matrix_world @ b.tail_local) for b in arm.data.bones if b.name != "Root"}
    groups = {n: body.vertex_groups.get(n) or body.vertex_groups.new(name=n) for n in segs}

    def dist(p, a, b):
        ab = b - a; t = max(0, min(1, (p - a).dot(ab) / ab.length_squared)); return (a + ab * t - p).length

    for v in body.data.vertices:
        p = body.matrix_world @ v.co
        near = sorted((dist(p, *seg), n) for n, seg in segs.items())[:2]
        w = [1 / max(d, 1e-4) ** 4 for d, _ in near]
        total = sum(w)
        for (d, n), wi in zip(near, w): groups[n].add([v.index], wi / total, "REPLACE")


def prune_and_fill(body, arm):
    """Bone heat leaks weight across touching parts (fists against shorts). Drop weights of
    bones much further away than the nearest bone, and give unweighted vertices the weights
    of their nearest weighted neighbour."""
    segs = {b.name: (arm.matrix_world @ b.head_local, arm.matrix_world @ b.tail_local) for b in arm.data.bones if b.name != "Root"}
    names = {vg.index: vg.name for vg in body.vertex_groups}

    def dist(p, a, b):
        ab = b - a; t = max(0, min(1, (p - a).dot(ab) / ab.length_squared)); return (a + ab * t - p).length

    groups = {vg.name: vg for vg in body.vertex_groups}
    empty = []
    for v in body.data.vertices:
        p = v.co
        d = {n: dist(p, *seg) for n, seg in segs.items()}
        nearest = min(d.values())
        kept = 0.0
        for g in list(v.groups):
            n = names[g.group]
            if n in d and d[n] > nearest * 2.2 + .03:
                groups[n].remove([v.index])
            else:
                kept += g.weight
        if kept < 1e-4: empty.append(v.index)
    if empty:
        tree = KDTree(len(body.data.vertices))
        missing = set(empty)
        for v in body.data.vertices:
            if v.index not in missing: tree.insert(v.co, v.index)
        tree.balance()
        for i in empty:
            _, j, _ = tree.find(body.data.vertices[i].co)
            for g in body.data.vertices[j].groups:
                groups[names[g.group]].add([i], g.weight, "REPLACE")
    bpy.context.view_layer.objects.active = body
    bpy.ops.object.mode_set(mode="WEIGHT_PAINT")
    bpy.ops.object.vertex_group_normalize_all(lock_active=False)
    bpy.ops.object.mode_set(mode="OBJECT")
    return len(empty)


def rigid_head(proxy, k):
    """Everything above the jaw -- face, hair, visor -- belongs to the head alone. Bone heat
    gave the jaw some Neck weight, and fitting the neck then pulled the face into an egg."""
    head = proxy.vertex_groups["Head"]
    jaw = .007 + (k["head"] + .01) * HEIGHT
    blend = .03 * HEIGHT
    for v in proxy.data.vertices:
        if v.co.z < jaw - blend: continue
        t = min(1, (v.co.z - (jaw - blend)) / blend)
        if t >= 1:
            for vg in proxy.vertex_groups: vg.remove([v.index])
            head.add([v.index], 1, "REPLACE")
        else:
            for g in v.groups: g.weight *= 1 - t
            head.add([v.index], t, "ADD")


def weld_proxy(body):
    """A welded copy for weighting. Tripo splits vertices along UV seams; weighting the split
    mesh lets the two sides of a seam take different weights and tear apart when posed."""
    proxy = body.copy(); proxy.data = body.data.copy(); proxy.name = body.name + " (weights)"
    bpy.context.scene.collection.objects.link(proxy)
    bm = bmesh.new(); bm.from_mesh(proxy.data)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=.002)
    bm.to_mesh(proxy.data); bm.free()
    return proxy


def smooth_weights(obj, repeat=6):
    bpy.ops.object.select_all(action="DESELECT"); obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.mode_set(mode="WEIGHT_PAINT")
    bpy.ops.object.vertex_group_smooth(group_select_mode="ALL", factor=.5, repeat=repeat)
    bpy.ops.object.vertex_group_limit_total(group_select_mode="ALL", limit=4)
    bpy.ops.object.vertex_group_normalize_all(lock_active=False)
    bpy.ops.object.mode_set(mode="OBJECT")


def copy_weights(proxy, body):
    """Every body vertex takes the weights of the welded vertex at its position."""
    tree = KDTree(len(proxy.data.vertices))
    for v in proxy.data.vertices: tree.insert(v.co, v.index)
    tree.balance()
    names = {vg.index: vg.name for vg in proxy.vertex_groups}
    groups = {vg.name: body.vertex_groups.get(vg.name) or body.vertex_groups.new(name=vg.name) for vg in proxy.vertex_groups}
    for v in body.data.vertices:
        _, j, _ = tree.find(v.co)
        for g in proxy.data.vertices[j].groups:
            groups[names[g.group]].add([v.index], g.weight, "REPLACE")


def vertex_colours(obj, px):
    """Texture colour under every vertex (from the first loop that uses it)."""
    h, w = px.shape[:2]
    uv = obj.data.uv_layers.active.data
    out = np.zeros((len(obj.data.vertices), 3), dtype=np.float32)
    for loop in obj.data.loops:
        u, v = uv[loop.index].uv
        out[loop.vertex_index] = px[min(h - 1, max(0, int(v * h))), min(w - 1, max(0, int(u * w)))][:3]
    return out


def soften_skirt(body, px):
    """A pleated skirt moves as one piece with the hips. Weighted by nearest bone, neighbouring
    pleats followed different thighs and shredded when the legs spread. The skirt is found by
    its colour (navy cloth and its white hem stripe) in the band between waist and hem; the
    top is fully on Hips and the hem follows its own side's thigh a little."""
    colours = vertex_colours(body, px)
    groups = {vg.name: vg for vg in body.vertex_groups}
    lo, hi = .007 + .27 * HEIGHT, .007 + .44 * HEIGHT
    centre_x = sum(v.co.x for v in body.data.vertices) / len(body.data.vertices)
    moved = 0
    for v in body.data.vertices:
        if not lo < v.co.z < hi: continue
        r, g, b = colours[v.index]
        navy = b > r + .06
        white = min(r, g, b) > .75
        if not (navy or (white and v.co.z < lo + .06 * HEIGHT)): continue
        t = 1 - (v.co.z - lo) / (hi - lo)              # 1 at the hem, 0 at the waist
        leg = .28 * t * t
        for vg in body.vertex_groups: vg.remove([v.index])
        side = "L" if v.co.x > centre_x else "R"
        groups["Hips"].add([v.index], 1 - leg, "REPLACE")
        if leg > .001: groups[f"UpperLeg.{side}"].add([v.index], leg, "REPLACE")
        moved += 1
    return moved


def remove_fragments(body, joints, rig):
    """Loose bits of fist left near the wrists after the cut."""
    wrists = [to_world(joints[f"Hand.{side}"][0], rig.location) for side in ("L", "R")]
    bm = bmesh.new(); bm.from_mesh(body.data); bm.verts.ensure_lookup_table()
    seen = set(); doomed = []
    for v in bm.verts:
        if v in seen: continue
        island = []; stack = [v]; seen.add(v)
        while stack:
            x = stack.pop(); island.append(x)
            for e in x.link_edges:
                n = e.other_vert(x)
                if n not in seen: seen.add(n); stack.append(n)
        if len(island) < 400:
            c = sum((x.co for x in island), Vector()) / len(island)
            if min((c - w).length for w in wrists) < .16: doomed += island
    bmesh.ops.delete(bm, geom=doomed, context="VERTS")
    bm.to_mesh(body.data); bm.free()
    return len(doomed)


def pose_onto_rig(arm, rig):
    """Constrain each fitting bone onto the rig's rest joint, then read the result."""
    targets = {}
    for b in rig.data.bones:
        for end, p in (("head", b.head_local), ("tail", b.tail_local)):
            e = bpy.data.objects.new(f"FIT target {rig.name} {b.name} {end}", None)
            bpy.context.scene.collection.objects.link(e)
            e.location = rig.matrix_world @ p
            targets[(b.name, end)] = e
    for pb in arm.pose.bones:
        # The head rides on the (rigid) neck: pinning it to the rig's head joint as well would
        # pull it down into the collar.
        if pb.name != "Head":
            loc = pb.constraints.new("COPY_LOCATION"); loc.target = targets[(pb.name, "head")]
        if pb.name in RIGID:
            aim = pb.constraints.new("DAMPED_TRACK"); aim.target = targets[(pb.name, "tail")]
        else:
            st = pb.constraints.new("STRETCH_TO"); st.target = targets[(pb.name, "tail")]
            st.volume = "NO_VOLUME"; st.rest_length = pb.bone.length
            # Pure swing: the default keeps the X axis in a plane, which rolls the bone about
            # its length -- it twisted the spine 80 degrees and turned the head sideways.
            st.keep_axis = "SWING_Y"
    bpy.context.view_layer.update()
    if "--debug" in sys.argv:
        for pb in arm.pose.bones:
            delta = (pb.bone.matrix_local.to_quaternion().inverted() @ pb.matrix.to_quaternion()).to_euler()
            print("FITDBG", pb.name, tuple(round(math.degrees(a), 1) for a in delta))
    return targets


def remove_fists(body, joints, rig):
    """Cut the fists off just below the wristband with a plane across the forearm, and cap the
    opening. The V4 grip hands (which hold the racket) take their place."""
    bm = bmesh.new(); bm.from_mesh(body.data)
    doomed = set()
    for side in ("L", "R"):
        wrist, tip = (to_world(p, rig.location) for p in joints[f"Hand.{side}"])
        elbow = to_world(joints[f"LowerArm.{side}"][0], rig.location)
        axis = (tip - wrist).normalized()
        # Through the lower half of the wristband: the band becomes a clean cuff.
        cut = wrist + axis * .01
        for v in bm.verts:
            rel = v.co - cut
            along = rel.dot(axis)
            if along > 0 and (rel - axis * along).length < .075 and (v.co - elbow).length > (wrist - elbow).length * .8:
                doomed.add(v)
    # Cap only the openings the cut made. The mesh is open along every UV seam, so filling all
    # boundaries stitches junk faces into each seam.
    rim = {n for v in doomed for e in v.link_edges for n in e.verts if n not in doomed}
    bmesh.ops.delete(bm, geom=list(doomed), context="VERTS")
    edges = [e for e in bm.edges if e.is_boundary and e.verts[0] in rim and e.verts[1] in rim]
    if edges: bmesh.ops.holes_fill(bm, edges=edges, sides=0)
    bm.to_mesh(body.data); bm.free()
    return len(doomed)


def texture_pixels(body):
    """The colour map as an array, for sampling the skin tone and finding the bare face."""
    img = next(n.image for n in body.data.materials[0].node_tree.nodes
               if n.type == "TEX_IMAGE" and n.image and "normal" not in n.image.name.lower())
    px = np.empty(img.size[0] * img.size[1] * 4, dtype=np.float32); img.pixels.foreach_get(px)
    px = px.reshape(img.size[1], img.size[0], 4)
    return px


def sample(body, px, hit_face, hit_point):
    """Texture colour at a surface point (nearest-corner UV of the hit polygon)."""
    poly = body.data.polygons[hit_face]
    uv = body.data.uv_layers.active.data
    best = min(poly.loop_indices, key=lambda li: (body.data.vertices[body.data.loops[li].vertex_index].co - hit_point).length)
    u, v = uv[best].uv
    h, w = px.shape[:2]
    return px[min(h - 1, max(0, int(v * h))), min(w - 1, max(0, int(u * w)))][:3]


def build_face(gender, rig, body, collection, px, report):
    for o in list(collection.objects):
        if o.name.startswith(("V4 face decal", "V4 hair")): bpy.data.objects.remove(o, do_unlink=True)
    bm = bmesh.new(); bm.from_mesh(body.data); bm.transform(body.matrix_world)
    tree = BVHTree.FromBMesh(bm); bm.free()
    fwd = Vector((0, -1, 0)); right = Vector((-1, 0, 0))    # character's right is -X
    # The head is wherever the fitted mesh put it (it rides above the rig's head joint):
    # measure it from the vertices that belong to the head.
    hg = body.vertex_groups["Head"].index
    head_z = [(body.matrix_world @ v.co).z for v in body.data.vertices if any(g.group == hg and g.weight > .9 for g in v.groups)]
    jaw, crown = min(head_z), max(head_z)
    head = Vector((0, 0, jaw))
    cx = rig.location.x
    # Reference skin: straight into the middle of the face.
    mid_z = jaw + (crown - jaw) * .34
    hit = tree.ray_cast(Vector((cx, rig.location.y - 1, mid_z)), -fwd, 3)
    ref = sample(body, px, hit[2], body.matrix_world.inverted() @ hit[0])

    def skin_at(x, z):
        h = tree.ray_cast(Vector((x, rig.location.y - 1, z)), -fwd, 3)
        if h[0] is None: return False
        c = sample(body, px, h[2], body.matrix_world.inverted() @ h[0])
        return sum(abs(float(a) - float(b)) for a, b in zip(c, ref)) < .12
    # The bare face is the run of skin around the reference point: up to the visor, down to
    # the chin.
    top = mid_z
    while skin_at(cx, top + .005) and top < head.z + .5: top += .005
    bottom = mid_z
    while skin_at(cx, bottom - .005) and bottom > head.z - .1: bottom -= .005
    zc = (top + bottom) / 2
    xl = cx
    while skin_at(xl + .005, zc) and xl < cx + .3: xl += .005
    xr = cx
    while skin_at(xr - .005, zc) and xr > cx - .3: xr -= .005
    width_avail, height_avail = (xl - xr) * .92, (top - bottom) * .96
    aspect = 512 / 440
    h = min(height_avail, width_avail / aspect); w = h * aspect
    centre = Vector((cx, 0, bottom + (top - bottom) * .5))
    grid = 28
    bm = bmesh.new(); uvl = bm.loops.layers.uv.new("UVMap"); rows = []
    for j in range(grid + 1):
        row = []
        for i in range(grid + 1):
            u, v = i / grid, j / grid
            # u runs from the character's right (viewer's left) to their left.
            origin = Vector((centre.x + right.x * (.5 - u) * w, rig.location.y - 1, centre.z + (v - .5) * h))
            hit = tree.ray_cast(origin, -fwd, 3)
            # A ray past the edge of the head misses: cling to the nearest surface instead of
            # leaving the vertex floating out in front of the face.
            surface = hit[0] if hit[0] is not None else tree.find_nearest(Vector((origin.x, rig.location.y, origin.z)))[0]
            p = surface + fwd * .0025
            row.append((bm.verts.new(p), u, v))
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
    mat = bpy.data.materials.get("V4 face decal") or bpy.data.materials.new("V4 face decal")
    decal.data.materials.append(mat)
    vg = decal.vertex_groups.new(name="Head"); vg.add(range(len(mesh.vertices)), 1.0, "REPLACE")
    decal.parent = rig; decal.matrix_parent_inverse = rig.matrix_world.inverted()
    decal.modifiers.new("Armature", "ARMATURE").object = rig
    report.append(f"{gender}: face window {w:.3f}x{h:.3f} at z={centre.z:.3f} (skin {bottom:.3f}-{top:.3f}, x {xr - cx:.3f}..{xl - cx:.3f}) ref {tuple(round(float(c), 3) for c in ref)}")


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    scene = bpy.data.scenes["01 GOLF"]; bpy.context.window.scene = scene
    report = []
    for gender in ("Male", "Female"):
        rig = bpy.data.objects[f"{gender}_Golf_Rig"]
        rig.data.pose_position = "REST"; bpy.context.view_layer.update()
        k = LANDMARKS[gender]
        body = import_body(gender, rig)
        joints = mesh_joints(k)
        removed = remove_fists(body, joints, rig)
        removed += remove_fragments(body, joints, rig)
        px = texture_pixels(body)
        arm = fit_armature(gender, rig, joints)
        proxy = weld_proxy(body)
        bpy.ops.object.select_all(action="DESELECT")
        proxy.select_set(True); arm.select_set(True); bpy.context.view_layer.objects.active = arm
        bpy.ops.object.parent_set(type="ARMATURE_AUTO")
        if sum(1 for v in proxy.data.vertices if len(v.groups)) < len(proxy.data.vertices) // 2:
            report.append(f"{gender}: bone heat found no solution; weighted by distance to the bones")
            distance_weights(proxy, arm)
        filled = prune_and_fill(proxy, arm)
        # What is left of the wrist follows the forearm; the hand is a separate object.
        for side in ("L", "R"):
            hand = proxy.vertex_groups.get(f"Hand.{side}"); fore = proxy.vertex_groups[f"LowerArm.{side}"]
            if not hand: continue
            for v in proxy.data.vertices:
                for g in v.groups:
                    if g.group == hand.index: fore.add([v.index], g.weight, "ADD")
            proxy.vertex_groups.remove(hand)
        skirt = soften_skirt(proxy, px) if gender == "Female" else 0
        smooth_weights(proxy)
        rigid_head(proxy, k)
        copy_weights(proxy, body)
        bpy.data.objects.remove(proxy, do_unlink=True)
        mod = body.modifiers.new("Armature", "ARMATURE"); mod.object = arm
        targets = pose_onto_rig(arm, rig)
        # Bake the fitted shape.
        bpy.context.view_layer.objects.active = body
        mod = next(m for m in body.modifiers if m.type == "ARMATURE")
        bpy.ops.object.modifier_apply(modifier=mod.name)
        body.parent = None
        for e in targets.values(): bpy.data.objects.remove(e, do_unlink=True)
        bpy.data.objects.remove(arm, do_unlink=True)
        # Move into the character's BODY collection, bound to the real rig.
        collection = bpy.data.collections[f"V4 {gender} Golf | BODY"]
        kit = bpy.data.collections[f"V4 {gender} Golf | KIT 1"]
        for o in list(collection.objects):
            if "grip hand" not in o.name: bpy.data.objects.remove(o, do_unlink=True)
        for o in list(kit.objects): bpy.data.objects.remove(o, do_unlink=True)
        for c in list(body.users_collection): c.objects.unlink(body)
        collection.objects.link(body)
        body.parent = rig; body.matrix_parent_inverse = rig.matrix_world.inverted()
        body.modifiers.new("Armature", "ARMATURE").object = rig
        bpy.ops.object.select_all(action="DESELECT"); body.select_set(True); bpy.context.view_layer.objects.active = body
        bpy.ops.object.shade_smooth()
        build_face(gender, rig, body, collection, px, report)
        rig.data.pose_position = "POSE"
        report.append(f"{gender}: {len(body.data.vertices)} verts, filled {filled}, fist verts removed {removed}, skirt verts {skirt}")
    print("\n".join("FIT " + r for r in report), flush=True)
    if "--save" in argv:
        bpy.ops.wm.save_mainfile(); print("FIT saved", flush=True)


main()
