"""Turn the base avatar's raw Tripo model (about two million triangles, 4K maps) into the
game's source model: decimated to a hero-character budget with its UVs and textures kept.

The avatar is the customisable blank-canvas player (see base-avatars/). The result is fitted
onto the rig by fit_avatar.py exactly like the opponents.

Run:  Blender --background --python prepare_avatar.py -- IN.glb OUT.glb [TRIANGLES]
"""
import sys

import bpy

argv = sys.argv[sys.argv.index("--") + 1:]
src, dst = argv[0], argv[1]
budget = int(argv[2]) if len(argv) > 2 else 45000
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)
body = next(o for o in bpy.data.objects if o.type == "MESH")
tris = sum(len(p.vertices) - 2 for p in body.data.polygons)
mod = body.modifiers.new("Decimate", "DECIMATE")
mod.decimate_type = "COLLAPSE"; mod.ratio = budget / tris; mod.use_collapse_triangulate = True
# Keep the silhouette symmetric (the model faces +X, so its left/right axis is Y).
mod.use_symmetry = True; mod.symmetry_axis = "Y"
bpy.context.view_layer.objects.active = body
bpy.ops.object.modifier_apply(modifier=mod.name)
bpy.ops.object.shade_smooth()
# Centre on the shins, not the bounding box: a ponytail behind the head shifts the box, and
# the fit places the joints relative to the model's origin.
me = body.data
vs = [v.co for v in me.vertices]
lo = min(v.z for v in vs); hi = max(v.z for v in vs); H = hi - lo
shins = [v for v in vs if lo + .12 * H < v.z < lo + .28 * H]
cx = (min(v.x for v in shins) + max(v.x for v in shins)) / 2
cy = (min(v.y for v in shins) + max(v.y for v in shins)) / 2
for v in me.vertices: v.co.x -= cx; v.co.y -= cy
me.update()
print("CENTRED", round(cx / H, 3), round(cy / H, 3), "of the height")
print("PREPARED", tris, "->", sum(len(p.vertices) - 2 for p in body.data.polygons), "triangles,", len(body.data.vertices), "vertices")
bpy.ops.export_scene.gltf(filepath=dst, export_format="GLB", use_selection=False)
