"""Quick Workbench preview of one clip: game camera (behind-right, like TennisShoulderCamera) | front 3/4 | right side, with a floor and a ground shadow.
  blender -b blend/<Hero>_Std.blend --python render_preview.py -- <male|female> <Clip> <outdir> [step]
Writes <outdir>/<view>_<f>.png; tile + encode with tile_preview.py."""
import math
import os
import sys

import bpy
import mathutils as mu

args = sys.argv[sys.argv.index('--') + 1:]
tag, clip, out = args[0], args[1], args[2]
step = int(args[3]) if len(args) > 3 else 1
move = float(args[4]) if len(args) > 4 else 0.0          # locomotion preview: the rig travels forward at this speed (m/s) and the cameras follow
loops = int(args[5]) if len(args) > 5 else 1
os.makedirs(out, exist_ok=True)
sc = bpy.context.scene
rig = bpy.data.objects['Rig_Male' if tag == 'male' else 'Rig_Female']
act = bpy.data.actions[clip]
rig.animation_data_create()
rig.animation_data.action = act
try:
    rig.animation_data.action_slot = act.slots[0]
except Exception:
    pass
rk = bpy.data.objects['Racket_Classic']
ract = bpy.data.actions.get(clip + '_Racket')
if rk.animation_data:
    rk.animation_data.action = ract
sc.render.engine = 'BLENDER_WORKBENCH'
sh = sc.display.shading
sh.light = 'STUDIO'
sh.color_type = 'OBJECT'
sh.show_shadows = True
sh.shadow_intensity = 0.55
sh.show_cavity = True
sh.cavity_type = 'WORLD'
sh.show_object_outline = True
sh.object_outline_color = (0.12, 0.12, 0.14)
sc.display.shadow_focus = 0.2
sc.display.light_direction = (0.35, 0.25, 0.9)
sc.render.resolution_x = 480
sc.render.resolution_y = 540
sc.render.film_transparent = False
if sc.world is None:
    sc.world = bpy.data.worlds.new('PV_World')
sc.world.color = (0.62, 0.70, 0.78)
for o in bpy.data.objects:
    if o.type == 'MESH':
        o.color = (0.80, 0.78, 0.76, 1)
for n in ('Racket_Classic', 'Grip', 'ButtCap', 'StringBed'):
    if n in bpy.data.objects:
        bpy.data.objects[n].color = (0.15, 0.25, 0.55, 1) if n != 'StringBed' else (0.9, 0.9, 0.6, 1)
for n in ('Face_M', 'Face_F', 'Hair_F'):
    if n in bpy.data.objects:
        bpy.data.objects[n].color = (0.35, 0.25, 0.2, 1) if n.startswith('Hair') else (0.80, 0.70, 0.64, 1)
if clip.endswith('_Free'):
    for n in ('Racket_Classic', 'Grip', 'ButtCap', 'StringBed', 'Collider_Racket'):
        if n in bpy.data.objects:
            bpy.data.objects[n].hide_render = True
if 'Collider_Racket' in bpy.data.objects:
    bpy.data.objects['Collider_Racket'].hide_render = True
# floor
fl = bpy.data.objects.get('PV_Floor')
if fl is None:
    me = bpy.data.meshes.new('PV_Floor')
    s = 3.0
    me.from_pydata([(-s, -s, 0), (s, -s, 0), (s, s, 0), (-s, s, 0)], [], [(0, 1, 2, 3)])
    fl = bpy.data.objects.new('PV_Floor', me)
    sc.collection.objects.link(fl)
fl.color = (0.36, 0.55, 0.42, 1)
# stripes every 0.5 m across the walking direction (they show any foot slide)
if move > 0 and bpy.data.objects.get('PV_Stripes') is None:
    me = bpy.data.meshes.new('PV_Stripes'); vs = []; fs = []
    for k in range(-6, 40):
        y = -k * 0.5
        b = len(vs); vs += [(-1.5, y, 0.001), (1.5, y, 0.001), (1.5, y - 0.04, 0.001), (-1.5, y - 0.04, 0.001)]; fs.append((b, b + 1, b + 2, b + 3))
    me.from_pydata(vs, [], fs); st = bpy.data.objects.new('PV_Stripes', me); sc.collection.objects.link(st); st.color = (0.95, 0.95, 0.95, 1)
    bpy.data.objects['PV_Floor'].scale = (1, 8, 1) if 'PV_Floor' in bpy.data.objects else (1, 1, 1)
# baseline-ish line in front of the hero (a thin white strip)
ln = bpy.data.objects.get('PV_Line')
if ln is None:
    me = bpy.data.meshes.new('PV_Line')
    me.from_pydata([(-3, 0.45, 0.001), (3, 0.45, 0.001), (3, 0.5, 0.001), (-3, 0.5, 0.001)], [], [(0, 1, 2, 3)])
    ln = bpy.data.objects.new('PV_Line', me)
    sc.collection.objects.link(ln)
ln.color = (0.95, 0.95, 0.95, 1)


def cam(name, loc, look, lens):
    c = bpy.data.objects.get(name)
    if c is None:
        c = bpy.data.objects.new(name, bpy.data.cameras.new(name))
        sc.collection.objects.link(c)
    c.location = loc
    d = mu.Vector(look) - mu.Vector(loc)
    c.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()
    c.data.lens = lens
    return c


views = [cam('PV_game', (-0.8, 4.0, 2.2), (0, -0.4, 1.0), 42), cam('PV_front', (-2.3, -3.8, 1.45), (0, 0, 0.95), 45), cam('PV_side', (-4.4, 0.0, 1.2), (0, 0, 0.95), 45)]
names = ['game', 'front', 'side']
f0, f1 = int(act.frame_range[0]), int(act.frame_range[1])
seq = list(range(f0, f1 + 1, step)) if loops <= 1 else [f for _ in range(loops) for f in range(f0, f1)]
fl = bpy.data.objects['PV_Floor']
if move > 0:
    fl.scale = (1, 8, 1)
    fl.location = (0, -10, 0)
base_cam = {c.name: c.location.copy() for c in views}
for n_out, f in enumerate(seq):
    sc.frame_set(f)
    if move > 0:
        dy = -move * n_out / 30.0
        rig.location = (0, dy, 0)
        for c in views:
            c.location = base_cam[c.name] + mu.Vector((0, dy, 0))
        bpy.context.view_layer.update()
    for c, n in zip(views, names):
        sc.camera = c
        sc.render.filepath = f'{out}/{n}_{(n_out if loops > 1 else f):03d}.png'
        bpy.ops.render.render(write_still=True)
print('PREVIEW_DONE', f1 - f0 + 1)
