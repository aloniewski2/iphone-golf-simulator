"""Dump what a golfer FBX holds, for a before/after comparison of a splice: per mesh its vertices (armature space), normals, UVs, materials and skin
weights; the rig's rest matrices; every take sampled (all bone heads and tails, every frame).
    Blender -b --factory-startup --python dump_fbx.py -- <in.fbx> <out.npz>"""
import bpy, sys, numpy as np
fbx, out = sys.argv[sys.argv.index("--") + 1:][:2]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=fbx)
scn = bpy.context.scene
rig = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
data = {}
for o in sorted((o for o in bpy.data.objects if o.type == 'MESH'), key=lambda o: o.name):
    me = o.data
    n = len(me.vertices)
    co = np.zeros(n * 3); me.vertices.foreach_get("co", co)
    M = np.array(o.matrix_world)
    co = (np.c_[co.reshape(-1, 3), np.ones(n)] @ M.T)[:, :3]
    data[f"mesh/{o.name}/co"] = co
    nl = np.zeros(len(me.loops) * 3); me.corner_normals.foreach_get("vector", nl) if hasattr(me, "corner_normals") else None
    data[f"mesh/{o.name}/normals"] = nl
    for uv in me.uv_layers:
        a = np.zeros(len(me.loops) * 2); uv.data.foreach_get("uv", a); data[f"mesh/{o.name}/uv/{uv.name}"] = a
    data[f"mesh/{o.name}/faces"] = np.array([len(p.vertices) for p in me.polygons])
    data[f"mesh/{o.name}/mats"] = np.array([hash(m.name if m else "") % 1000003 for m in me.materials])
    data[f"mesh/{o.name}/matnames"] = np.array([m.name if m else "" for m in me.materials])
    g = np.zeros((n, len(o.vertex_groups)))
    for v in me.vertices:
        for e in v.groups: g[v.index, e.group] = e.weight
    data[f"mesh/{o.name}/weights"] = g
    data[f"mesh/{o.name}/groups"] = np.array([vg.name for vg in o.vertex_groups])
for b in rig.data.bones:
    data[f"bone/{b.name}"] = np.array(b.matrix_local)
for act in sorted(bpy.data.actions, key=lambda a: a.name):
    rig.animation_data_create(); rig.animation_data.action = act
    if len(act.slots): rig.animation_data.action_slot = act.slots[0]
    f0, f1 = int(act.frame_range[0]), int(act.frame_range[1])
    arr = []
    for f in range(f0, f1 + 1):
        scn.frame_set(f)
        arr.append([list(pb.head) + list(pb.tail) for pb in rig.pose.bones])
    data[f"take/{act.name.split('|')[-1]}"] = np.array(arr)
data["bones"] = np.array([pb.name for pb in rig.pose.bones])
np.savez_compressed(out, **data)
print("DUMPED", out, len(data))
