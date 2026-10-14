"""Export clips to FBX exactly like the shipped emotes (work/emote-overhaul/scripts/export_emotes2.py: metres, -Z forward, Y up, no leaf bones, baked 30 fps).
  blender -b blend/<Hero>_Std.blend --python export_fbx.py -- <male|female> <Action> [<Action> ...]
A *_Free action is the empty-handed variant (golf + menu plaza): exported without the racket objects.  Writes fbx/<Hero>_<Action>.fbx."""
import os
import sys

import bpy

W = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'))
a_ = sys.argv[sys.argv.index('--') + 1:]
tag, names = a_[0], a_[1:]
H = dict(male=dict(label='Male', body='Body_M', face='Face_M', hair=None, rig='Rig_Male'),
         female=dict(label='Female', body='Body_F', face='Face_F', hair='Hair_F', rig='Rig_Female'))[tag]
sc = bpy.context.scene
sc.render.fps, sc.render.fps_base = 30, 1.0
rig = bpy.data.objects[H['rig']]
rk = bpy.data.objects['Racket_Classic']
for clip in names:
    golf = clip.endswith('_Free')
    # tennis: the same object set as the shipped emotes; the empty-handed set (Resources, loaded at run time) is the skeleton only
    want = [rig.name] if golf else [rig.name, H['body'], H['face'], 'Hand_Racket', 'Racket_Classic', 'Grip', 'ButtCap', 'StringBed'] + ([H['hair']] if H['hair'] else [])
    act = bpy.data.actions[clip]
    rig.animation_data_create()
    rig.animation_data.action = act
    try:
        rig.animation_data.action_slot = act.slots[0]
    except Exception:
        pass
    if rk.animation_data is not None:
        rk.animation_data_clear()
    sc.frame_start, sc.frame_end = int(act.frame_start), int(act.frame_end)
    sc.frame_set(int(act.frame_start))
    for o in bpy.data.objects:
        o.select_set(False)
    for n in want:
        bpy.data.objects[n].select_set(True)
    bpy.context.view_layer.objects.active = rig
    path = f'{W}/fbx/{H["label"]}_{clip}.fbx'
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={'ARMATURE', 'MESH', 'EMPTY'}, global_scale=1.0, apply_unit_scale=True,
                             apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y', use_mesh_modifiers=True, mesh_smooth_type='OFF',
                             add_leaf_bones=False, bake_anim=True, bake_anim_use_all_bones=True, bake_anim_use_nla_strips=False, bake_anim_use_all_actions=False,
                             bake_anim_force_startend_keying=True, bake_anim_step=1.0, bake_anim_simplify_factor=0.0, path_mode='STRIP',
                             use_armature_deform_only=False)
    print('FBX', clip, int(act.frame_end - act.frame_start) + 1, os.path.getsize(path), path)
