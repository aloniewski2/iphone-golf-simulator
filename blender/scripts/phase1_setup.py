import bpy, math, sys, importlib
import os, bpy; sys.path.insert(0, os.path.join(bpy.path.abspath("//"), "scripts"))
import hole07_lib as H
importlib.reload(H)

sc = bpy.context.scene
sc.name = "HOLE_07"

# --- clear default startup content
for name in ("Cube", "Light", "Camera"):
    H.remove_object(name)
default_col = bpy.data.collections.get("Collection")
if default_col and len(default_col.objects) == 0:
    bpy.data.collections.remove(default_col)

# --- collections
for c in H.COLLECTIONS:
    H.get_collection(c)

root = H.get_root()

# --- cameras
def make_camera(name, loc, rot_deg, ortho=False, ortho_scale=100, lens=35):
    H.remove_object(name)
    cam = bpy.data.cameras.new(name)
    ob = bpy.data.objects.new(name, cam)
    sc.collection.objects.link(ob)
    H.link_to(ob, "GAMEPLAY")
    ob.parent = root
    ob.location = loc
    ob.rotation_euler = [math.radians(a) for a in rot_deg]
    if ortho:
        cam.type = 'ORTHO'
        cam.ortho_scale = ortho_scale
    else:
        cam.type = 'PERSP'
        cam.lens = lens
    cam.clip_end = 5000
    return ob

# Overview: orthographic, elevated, looking north-west so the east (ocean) cliff faces show.
make_camera("CAM_HOLE_OVERVIEW", (147, -410, 396), (50, 0, 20), ortho=True, ortho_scale=440)
make_camera("CAM_TEE_VIEW",      (-5, -215, 22),  (78, 0, 0), lens=28)
make_camera("CAM_FAIRWAY_VIEW",  (-40, -60, 20),  (80, 0, -20), lens=30)
make_camera("CAM_GREEN_VIEW",    (-20, 120, 22),  (75, 0, -20), lens=32)
sc.camera = bpy.data.objects["CAM_HOLE_OVERVIEW"]

# --- basic key light so previews are readable (refined in Phase 12)
H.remove_object("SUN_KEY")
sun_data = bpy.data.lights.new("SUN_KEY", 'SUN')
sun_data.energy = 4.0
sun_data.angle = math.radians(4)
sun = bpy.data.objects.new("SUN_KEY", sun_data)
sc.collection.objects.link(sun)
H.link_to(sun, "LIGHTING")
sun.location = (0, 0, 200)
sun.rotation_euler = (math.radians(48), 0, math.radians(135))

# --- world
world = bpy.data.worlds.get("World") or bpy.data.worlds.new("World")
sc.world = world
world.use_nodes = True
bg = world.node_tree.nodes.get("Background")
bg.inputs[0].default_value = H.rgb(150, 205, 245)
bg.inputs[1].default_value = 0.9

# --- render settings: stylized, saturated
sc.render.engine = 'BLENDER_EEVEE'
sc.view_settings.view_transform = 'Standard'
sc.view_settings.look = 'None'
sc.render.film_transparent = False
if hasattr(sc, "eevee"):
    ev = sc.eevee
    ev.taa_render_samples = 16
    if hasattr(ev, "use_shadows"): ev.use_shadows = True
    if hasattr(ev, "use_gtao"): ev.use_gtao = True

# --- scene units
sc.unit_settings.system = 'METRIC'
sc.unit_settings.scale_length = 1.0

path = H.save()
result = {"saved": path, "collections": [c.name for c in bpy.data.collections],
          "objects": [o.name for o in bpy.data.objects]}
