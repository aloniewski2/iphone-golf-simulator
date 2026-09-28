"""Hero V5 review renders (Eevee): the same framings for BEFORE and AFTER.
Usage: Blender -b <hero.blend> --python render_review.py -- <out_dir> [bg=white|magenta]
Views: turnaround (front, 3/4, side, back), hair close-up (3/4 + back + top + low front), legs (front, side),
stress poses: knees-bent ready, deep lunge (dive), serve arms-up — each front + side + back."""
import bpy, sys, math
from mathutils import Vector, Euler, Matrix
argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
OUT = argv[0]; BG = argv[1] if len(argv) > 1 else 'white'
scene = bpy.context.scene
scene.render.engine = 'BLENDER_EEVEE' if 'BLENDER_EEVEE' in [e.identifier for e in bpy.types.RenderSettings.bl_rna.properties['engine'].enum_items] else 'BLENDER_EEVEE_NEXT'
scene.render.film_transparent = False
scene.render.resolution_percentage = 100
rig = bpy.data.objects['Hero_01_Rig']
if rig.animation_data: rig.animation_data.action = None
for pb in rig.pose.bones: pb.matrix_basis = Matrix.Identity(4)
for o in bpy.data.objects:
    if o.type == 'MESH' and o.name in ('Studio floor',): o.hide_render = True
    if o.type == 'LIGHT': o.hide_render = True
# world / background
w = scene.world or bpy.data.worlds.new('W'); scene.world = w; w.use_nodes = True
bgn = w.node_tree.nodes.get('Background')
bgn.inputs[0].default_value = (1, 0, 1, 1) if BG == 'magenta' else (0.93, 0.93, 0.92, 1); bgn.inputs[1].default_value = 0.6 if BG != 'magenta' else 1.0
# lights: warm key 40 deg, fill 35%, rim
def light(name, kind, energy, rot, color, size=2):
    d = bpy.data.lights.get(name) or bpy.data.lights.new(name, kind); d.energy = energy; d.color = color
    if kind == 'AREA': d.size = size
    o = bpy.data.objects.get(name) or bpy.data.objects.new(name, d); o.data = d
    if o.name not in scene.collection.objects: scene.collection.objects.link(o)
    o.rotation_euler = Euler([math.radians(a) for a in rot]); o.location = (0, 0, 3); o.hide_render = False
    return o
light('V5 Key', 'SUN', 3.2, (50, 0, 35), (1, .93, .84))
light('V5 Fill', 'SUN', 1.1, (70, 0, -60), (.85, .9, 1))
light('V5 Rim', 'SUN', 1.6, (120, 0, 180), (1, 1, 1))
import os
if os.environ.get('VISOR_WHITE') == '1':   # match the game: the visor is all white (HeroCosmetics)
    hv = bpy.data.objects['Hat_Visor']; hv.data = hv.data.copy()
    for i, m0 in enumerate(hv.data.materials):
        m = m0.copy() if m0 else None; hv.data.materials[i] = m      # the visor's own copy (shared with shirt/shorts)
        if m and m.use_nodes and m.node_tree.nodes.get('Principled BSDF'):
            bs = m.node_tree.nodes['Principled BSDF']
            for l in list(bs.inputs['Base Color'].links): m.node_tree.links.remove(l)
            bs.inputs['Base Color'].default_value = (.9, .9, .88, 1)
# HAIRCUT=<Style> shows that Hair_<Style> (and hides the rest); FEMALE=1 sets the 'Female' shape; NOHAT=1 hides the visor
cut = os.environ.get('HAIRCUT', 'Default'); nohat = os.environ.get('NOHAT') == '1'
for o in bpy.data.objects:
    if o.type == 'MESH' and o.name.startswith('Hair_'):
        fill = o.name.startswith('Hair_BandFill'); has_free = ('Hair_' + cut + '_Free') in bpy.data.objects
        if fill: o.hide_render = not (nohat and not has_free and o.name == 'Hair_BandFill' + ('' if cut == 'Default' else '_' + cut))
        else: o.hide_render = o.name != ('Hair_' + cut + ('_Free' if nohat and has_free else ''))
    if o.type == 'MESH' and o.data.shape_keys and 'Female' in o.data.shape_keys.key_blocks:
        o.data.shape_keys.key_blocks['Female'].value = 1.0 if os.environ.get('FEMALE') == '1' else 0.0
if nohat: bpy.data.objects['Hat_Visor'].hide_render = True
if os.environ.get('QUICK') == '1':
    import builtins; QUICK = True
else: QUICK = False
cam_d = bpy.data.cameras.get('V5 Cam') or bpy.data.cameras.new('V5 Cam')
cam = bpy.data.objects.get('V5 Cam') or bpy.data.objects.new('V5 Cam', cam_d)
if cam.name not in scene.collection.objects: scene.collection.objects.link(cam)
scene.camera = cam
def shoot(name, target, yaw, pitch, dist, lens=50, res=(900, 1200)):
    scene.render.resolution_x, scene.render.resolution_y = res
    cam_d.lens = lens
    t = Vector(target); r = math.radians(yaw); p = math.radians(pitch)
    # Blender: forward of the hero is -Y
    off = Vector((math.sin(r) * math.cos(p), -math.cos(r) * math.cos(p), math.sin(p))) * dist
    cam.location = t + off
    cam.rotation_euler = (t - cam.location).to_track_quat('-Z', 'Y').to_euler()
    scene.render.filepath = f'{OUT}/{name}.png'
    bpy.ops.render.render(write_still=True)
def pose(spec):
    for pb in rig.pose.bones: pb.matrix_basis = Matrix.Identity(4)
    for bone, (x, y, z) in spec.items():
        pb = rig.pose.bones.get(bone)
        if pb: pb.rotation_mode = 'XYZ'; pb.rotation_euler = (math.radians(x), math.radians(y), math.radians(z))
    bpy.context.view_layer.update()
def hips_drop(dz):
    rig.pose.bones['Hips'].location = (0, 0, 0); bpy.context.view_layer.update()
    pb = rig.pose.bones['Hips']; pb.location = pb.bone.matrix_local.inverted().to_3x3() @ Vector((0, 0, -dz)); bpy.context.view_layer.update()
H = 1.70
# --- rest views
pose({})
for yaw, n in ((0, 'front'), (35, 'three_quarter'), (90, 'side'), (180, 'back')):
    shoot('turn_' + n, (0, 0, .85), yaw, 4, 4.4, 50, (700, 1200))
if QUICK:
    for yaw, pitch, n in ((25, 4, 'hair_34'), (180, 8, 'hair_back'), (90, 0, 'hair_side'), (0, -18, 'hair_low_front')):
        shoot(n, (0, 0, 1.40), yaw, pitch, 1.6, 50, (900, 900))
    print('REVIEW_DONE', OUT); raise SystemExit
for yaw, pitch, n in ((25, 4, 'hair_34'), (180, 8, 'hair_back'), (150, 60, 'hair_top_back'), (0, -18, 'hair_low_front'), (90, 0, 'hair_side')):
    shoot(n, (0, 0, 1.47), yaw, pitch, 1.25, 50, (1000, 900))
for yaw, n in ((0, 'legs_front'), (90, 'legs_side'), (35, 'legs_34')):
    shoot(n, (0, 0, .36), yaw, 2, 2.0, 50, (1000, 800))
# --- stress poses (bone names from the V4 rig; axes found by trial: X bends legs/arms forward)
ready = {'UpperLeg.L': (-38, 0, 0), 'UpperLeg.R': (-38, 0, 0), 'LowerLeg.L': (62, 0, 0), 'LowerLeg.R': (62, 0, 0), 'Foot.L': (-24, 0, 0), 'Foot.R': (-24, 0, 0), 'Spine': (12, 0, 0)}
pose(ready); hips_drop(.12)
for yaw, n in ((0, 'knees_front'), (90, 'knees_side'), (35, 'knees_34')):
    shoot('stress_' + n, (0, 0, .45), yaw, 5, 2.4, 50, (1000, 900))
lunge = {'UpperLeg.R': (-95, 0, 0), 'LowerLeg.R': (95, 0, 0), 'UpperLeg.L': (35, 0, 0), 'LowerLeg.L': (40, 0, 0), 'Spine': (25, 0, 0)}
pose(lunge); hips_drop(.3)
for yaw, n in ((0, 'dive_front'), (90, 'dive_side'), (270, 'dive_side2')):
    shoot('stress_' + n, (0, 0, .45), yaw, 5, 2.6, 50, (1000, 900))
serve = {'UpperArm.R': (0, 0, 0), 'UpperArm.L': (0, 0, 0)}
pose({})
for side, sgn in (('R', -1), ('L', 1)):
    pb = rig.pose.bones['UpperArm.' + side]; pb.rotation_mode = 'XYZ'
for ang in (150,):
    # raise both arms overhead about the body's forward axis in the bone's own frame (try both signs, keep the one that goes up)
    best = None
    for s in (1, -1):
        for side in ('R', 'L'):
            pb = rig.pose.bones['UpperArm.' + side]; pb.rotation_euler = (0, 0, 0)
        bpy.context.view_layer.update()
        for side, sg in (('R', 1), ('L', -1)):
            pb = rig.pose.bones['UpperArm.' + side]; pb.rotation_euler = (0, 0, math.radians(ang) * s * sg)
        bpy.context.view_layer.update()
        hz = (rig.matrix_world @ rig.pose.bones['Hand.R'].head).z
        if best is None or hz > best[0]: best = (hz, s)
    for side, sg in (('R', 1), ('L', -1)):
        rig.pose.bones['UpperArm.' + side].rotation_euler = (0, 0, math.radians(ang) * best[1] * sg)
    bpy.context.view_layer.update()
for yaw, pitch, n in ((0, 5, 'serve_front'), (90, 5, 'serve_side'), (35, 15, 'serve_34')):
    shoot('stress_' + n, (0, 0, 1.2), yaw, pitch, 2.4, 50, (1000, 1000))
print('REVIEW_DONE', OUT)
