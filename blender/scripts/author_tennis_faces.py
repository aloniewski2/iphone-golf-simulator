"""Give the V4 tennis characters faces (and hair), authored on the rig so they animate with it.

The heads were blank. This adds, per character:

  * "V4 face decal": a grid shrink-wrapped onto the front of the head, skinned 100% to the
    Head bone, with planar UVs. Unity maps an expression atlas onto it (eyes, brows, mouth,
    blush) and swaps cells to blink, cheer or grimace. Feature art is generated with
    Higgsfield and assembled into the atlas by tools/build_face_atlas.py.
  * "V4 hair": a cap shell following the back and top of the skull, plus a bun for the
    female character, skinned to Head. Stylised, smooth shapes like the art target.

Both go into the character's BODY collection, which export_tennis_runtime.py exports.

Run:  Blender --background sports-animation-studio-v4.blend --python author_tennis_faces.py -- [--save]
"""
import sys

import bmesh
import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree

GRID = 28


def rig_forward(rig):
    """Horizontal direction the character faces, from foot to toes."""
    pose = rig.pose.bones
    foot = rig.matrix_world @ pose["Foot.L"].head
    toe = rig.matrix_world @ pose["Toes.L"].head
    f = Vector((toe.x - foot.x, toe.y - foot.y, 0))
    return f.normalized()


def world_bounds(obj):
    pts = [obj.matrix_world @ Vector(c) for c in obj.bound_box]
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    return lo, hi


def bvh_of(obj):
    dg = bpy.context.evaluated_depsgraph_get()
    ev = obj.evaluated_get(dg)
    mesh = ev.to_mesh()
    bm = bmesh.new(); bm.from_mesh(mesh); bm.transform(obj.matrix_world)
    tree = BVHTree.FromBMesh(bm)
    bm.free(); ev.to_mesh_clear()
    return tree


def skin_to_head(obj, rig):
    vg = obj.vertex_groups.new(name="Head")
    vg.add(range(len(obj.data.vertices)), 1.0, "REPLACE")
    # Keep the object where it was built: parenting without the inverse re-applied the rig's
    # own offset and pushed the face off the side of the head.
    world = obj.matrix_world.copy()
    obj.parent = rig
    obj.matrix_parent_inverse = rig.matrix_world.inverted()
    obj.matrix_world = world
    mod = obj.modifiers.new("Armature", "ARMATURE"); mod.object = rig


def material(name, color):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.diffuse_color = color
    return m


def remove_existing(collection, prefix):
    for o in list(collection.objects):
        if o.name.startswith(prefix):
            bpy.data.objects.remove(o, do_unlink=True)


def build_face(gender, rig, head, collection, report):
    remove_existing(collection, "V4 face decal")
    fwd = rig_forward(rig)
    right = fwd.cross(Vector((0, 0, 1))).normalized()
    lo, hi = world_bounds(head)
    centre = (lo + hi) / 2
    height = hi.z - lo.z
    width = max(hi.x - lo.x, hi.y - lo.y)
    # Face window: most of the front of the head, centred a little below the middle (chibi
    # proportions put the eyes low). Unity positions features inside it via the atlas.
    w, h = width * .86, height * .74
    mid = Vector((centre.x, centre.y, lo.z + height * .42))
    tree = bvh_of(head)
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    verts = []
    for j in range(GRID + 1):
        row = []
        for i in range(GRID + 1):
            u, v = i / GRID, j / GRID
            # Right of the character is the viewer's left, so u runs toward -right.
            target = mid + right * ((.5 - u) * w) + Vector((0, 0, (v - .5) * h))
            origin = target + fwd * width
            hit = tree.ray_cast(origin, -fwd, width * 2)
            p = hit[0] + fwd * .0025 if hit[0] is not None else target
            vert = bm.verts.new(p)
            row.append((vert, u, v))
        verts.append(row)
    for j in range(GRID):
        for i in range(GRID):
            quad = [verts[j][i], verts[j][i + 1], verts[j + 1][i + 1], verts[j + 1][i]]
            face = bm.faces.new([q[0] for q in quad])
            for loop, (_, u, v) in zip(face.loops, quad):
                loop[uv].uv = (u, v)
    bm.normal_update()
    mesh = bpy.data.meshes.new(f"V4 {gender} face decal")
    bm.to_mesh(mesh); bm.free()
    obj = bpy.data.objects.new(f"V4 face decal {gender}", mesh)
    collection.objects.link(obj)
    if mesh.polygons and mesh.polygons[0].normal.dot(fwd) < 0:
        for p in mesh.polygons: p.flip()
    obj.data.materials.append(material("V4 face decal", (1, 1, 1, 1)))
    skin_to_head(obj, rig)
    report.append(f"{gender}: face decal {w:.3f}x{h:.3f}m at z={mid.z:.3f}, forward={tuple(round(c, 2) for c in fwd)}")


def build_hair(gender, rig, head, collection, report):
    remove_existing(collection, "V4 hair")
    fwd = rig_forward(rig)
    lo, hi = world_bounds(head)
    centre = (lo + hi) / 2
    height = hi.z - lo.z
    hair = head.copy(); hair.data = head.data.copy(); hair.name = f"V4 hair {gender}"
    collection.objects.link(hair)
    for mod in list(hair.modifiers): hair.modifiers.remove(mod)
    hair.vertex_groups.clear()
    bm = bmesh.new(); bm.from_mesh(hair.data)
    mw = hair.matrix_world
    # Keep the skull cap: everything above the brow line, except the face; at the back,
    # down to the nape.
    doomed = []
    for v in bm.verts:
        p = mw @ v.co
        rel = p - centre
        front = rel.normalized().dot(fwd)
        z = (p.z - lo.z) / height
        keep = (z > .62 and front < .55) or (z > .30 and front < -.15)
        if not keep: doomed.append(v)
    bmesh.ops.delete(bm, geom=doomed, context="VERTS")
    # Puff the shell out a little so it sits on the head like hair, not paint.
    for v in bm.verts:
        p = mw @ v.co
        out = (p - centre).normalized()
        v.co = mw.inverted() @ (p + out * .018)
    bm.to_mesh(hair.data); bm.free()
    solid = hair.modifiers.new("Thickness", "SOLIDIFY"); solid.thickness = .012; solid.offset = -1
    bpy.context.view_layer.objects.active = hair
    bpy.ops.object.modifier_apply(modifier=solid.name)
    hair.data.materials.clear()
    hair.data.materials.append(material(f"V4 hair {gender.lower()}", (.42, .24, .10, 1) if gender == "Female" else (.86, .68, .36, 1)))
    if gender == "Female":
        bpy.ops.mesh.primitive_uv_sphere_add(segments=20, ring_count=12, radius=height * .17,
            location=centre - fwd * (height * .46) + Vector((0, 0, height * .10)))
        bun = bpy.context.active_object; bun.name = "V4 hair bun Female"
        for c in bun.users_collection: c.objects.unlink(bun)
        collection.objects.link(bun)
        bun.data.materials.append(hair.data.materials[0])
        bpy.ops.object.select_all(action="DESELECT")
        bun.select_set(True); hair.select_set(True); bpy.context.view_layer.objects.active = hair
        bpy.ops.object.join()
    bpy.ops.object.select_all(action="DESELECT")
    hair.select_set(True); bpy.context.view_layer.objects.active = hair
    bpy.ops.object.shade_smooth()
    skin_to_head(hair, rig)
    report.append(f"{gender}: hair {len(hair.data.vertices)} verts")


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    scene = bpy.data.scenes["03 TENNIS"]; bpy.context.window.scene = scene
    report = []
    for gender in ("Male", "Female"):
        rig = bpy.data.objects[f"{gender}_Tennis_Rig"]
        # Author against the rest pose: the armature modifier deforms from rest, so
        # anything placed on a posed head would be offset once skinned.
        rig.data.pose_position = "REST"
        bpy.context.view_layer.update()
        collection = bpy.data.collections[f"V4 {gender} Tennis | BODY"]
        head = next(o for o in collection.objects if o.name.startswith(f"V4 {gender} Tennis source head"))
        build_face(gender, rig, head, collection, report)
        build_hair(gender, rig, head, collection, report)
        rig.data.pose_position = "POSE"
    print("\n".join("FACE " + r for r in report), flush=True)
    if "--save" in argv:
        bpy.ops.wm.save_mainfile(); print("FACE saved", flush=True)


main()
