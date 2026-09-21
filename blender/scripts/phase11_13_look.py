import bpy, math, sys, importlib
import os, bpy; sys.path.insert(0, os.path.join(bpy.path.abspath("//"), "scripts"))
import hole07_lib as H; importlib.reload(H)
import hole07_design as D; importlib.reload(D)
from hole07_design import *
from mathutils import Vector

sc = bpy.context.scene

# ------------------------------------------------------------------ Phase 11: materials (flat, saturated, game-ready)
PALETTE = {
    # name: (sRGB, roughness)
    "MAT_FAIRWAY":        ((118, 208, 56), 0.95),
    "MAT_FAIRWAY_STRIPE": ((100, 192, 48), 0.95),
    "MAT_FIRSTCUT":       ((84, 176, 44), 0.95),
    "MAT_ROUGH":          ((58, 148, 38), 0.95),
    "MAT_GREEN":          ((156, 228, 72), 0.9),
    "MAT_BUNKER_LIP":     ((166, 228, 90), 0.95),
    "MAT_SAND":           ((240, 218, 160), 0.95),
    "MAT_WATER":          ((16, 70, 170), 0.12),
    "MAT_WATER_SHALLOW":  ((40, 146, 222), 0.12),
    "MAT_FOAM":           ((226, 244, 252), 0.6),
    "MAT_CLIFF":          ((118, 122, 130), 0.95),
    "MAT_CLIFF_DARK":     ((84, 90, 100), 0.95),
    "MAT_ROCK":           ((138, 140, 146), 0.95),
    "MAT_ROCK_DARK":      ((98, 102, 110), 0.95),
    "MAT_TREE_DARK":      ((32, 104, 54), 0.95),
    "MAT_TREE_MID":       ((50, 140, 62), 0.95),
    "MAT_TREE_LIGHT":     ((94, 178, 70), 0.95),
    "MAT_PATH":           ((200, 202, 204), 0.95),
    "MAT_PATH_EDGE":      ((152, 156, 158), 0.95),
    "MAT_WOOD":           ((112, 74, 46), 0.9),
    "MAT_ROOF":           ((104, 84, 74), 0.9),
    "MAT_WALL":           ((224, 208, 178), 0.9),
    "MAT_GLASS":          ((150, 205, 235), 0.2),
    "MAT_STONE":          ((196, 188, 176), 0.95),
    "MAT_FLAG":           ((232, 40, 40), 0.8),
    "MAT_POLE":           ((240, 240, 240), 0.5),
    "MAT_CUP":            ((28, 28, 28), 0.9),
    "MAT_BALL":           ((250, 250, 250), 0.4),
}
for name, (col, rough) in PALETTE.items():
    m = H.get_material(name, H.rgb(*col), roughness=rough)
    bsdf = next(n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
    if "Specular IOR Level" in bsdf.inputs:
        bsdf.inputs["Specular IOR Level"].default_value = 0.35 if "WATER" in name or name == "MAT_GLASS" else 0.15
    m.use_backface_culling = False
# water gets a faint emissive lift so it stays luminous in shadow (mobile-friendly: still one BSDF)
for wn, strength in (("MAT_WATER", 0.10), ("MAT_WATER_SHALLOW", 0.18)):
    m = bpy.data.materials[wn]
    bsdf = next(n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
    if "Emission Color" in bsdf.inputs:
        bsdf.inputs["Emission Color"].default_value = bsdf.inputs["Base Color"].default_value
        bsdf.inputs["Emission Strength"].default_value = strength

# ------------------------------------------------------------------ Phase 12: lighting
def sun(name, energy, rot_deg, angle_deg, shadow=True, color=(1, 1, 1)):
    H.remove_object(name)
    data = bpy.data.lights.new(name, 'SUN')
    data.energy = energy
    data.angle = math.radians(angle_deg)
    data.use_shadow = shadow
    data.color = color
    ob = bpy.data.objects.new(name, data)
    sc.collection.objects.link(ob)
    H.link_to(ob, "LIGHTING")
    ob.parent = H.get_root()
    ob.location = (0, 0, 300)
    ob.rotation_euler = [math.radians(a) for a in rot_deg]
    return ob

# key from the south-east so the cliff faces the overview camera sees are lit, like the mockup
sun("SUN_KEY", 3.6, (50, 0, 28), 3.0, shadow=True, color=(1.0, 0.97, 0.9))
# soft cool fill from the opposite side, no shadows
sun("SUN_FILL", 0.9, (60, 0, 222), 25.0, shadow=False, color=(0.82, 0.9, 1.0))

world = sc.world
world.use_nodes = True
bg = world.node_tree.nodes.get("Background")
bg.inputs[0].default_value = H.rgb(160, 210, 248)
bg.inputs[1].default_value = 0.9

sc.render.engine = 'BLENDER_EEVEE'
sc.view_settings.view_transform = 'Standard'
sc.view_settings.look = 'None'
sc.view_settings.exposure = 0.0
sc.view_settings.gamma = 1.0
ev = sc.eevee
ev.taa_render_samples = 32
for attr, val in (("use_shadows", True), ("use_gtao", True), ("gtao_distance", 6.0),
                  ("use_raytracing", False), ("shadow_ray_count", 2), ("shadow_step_count", 4),
                  ("use_fast_gi", True), ("fast_gi_distance", 8.0)):
    if hasattr(ev, attr):
        try:
            setattr(ev, attr, val)
        except Exception:
            pass

# ------------------------------------------------------------------ Phase 13: cameras
def aim(name, loc, target, lens=None, ortho_scale=None):
    cam = bpy.data.objects[name]
    cam.location = loc
    d = Vector(target) - Vector(loc)
    cam.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()
    if lens: cam.data.lens = lens
    if ortho_scale: cam.data.ortho_scale = ortho_scale
    cam.data.clip_start = 0.5
    cam.data.clip_end = 6000
    return cam

# overview: orthographic, elevated, slightly angled (mockup isometric read), whole hole visible
ov = bpy.data.objects["CAM_HOLE_OVERVIEW"]
ov.data.type = 'ORTHO'
ov.location = (155, -430, 398)
ov.rotation_euler = [math.radians(a) for a in (50, 0, 20)]
ov.data.ortho_scale = 470
ov.data.shift_x = 0.0; ov.data.shift_y = 0.0

# tee view: behind the tee markers, looking down the fairway toward the green
tz = height(TEE["cx"], TEE["cy"])
aim("CAM_TEE_VIEW", (TEE["cx"] + 1, TEE["cy"] - 30, tz + 5.5), (0, -20, height(0, -20) + 4), lens=26)
# fairway / approach view: from the west edge past the landing area toward the green with water on the right
aim("CAM_FAIRWAY_VIEW", (-30, -72, height(-30, -72) + 11), (16, 120, height(16, 120) + 5), lens=30)
# green view: low from the front-left of the green, clubhouse behind
aim("CAM_GREEN_VIEW", (-8, 116, height(-8, 116) + 8), (GREEN["cx"] + 3, GREEN["cy"] + 4, height(GREEN["cx"], GREEN["cy"]) + 1.5), lens=30)
sc.camera = ov

H.save()
result = {"materials": sorted(m.name for m in bpy.data.materials if m.name.startswith("MAT_")),
          "lights": [o.name for o in bpy.data.objects if o.type == 'LIGHT']}
