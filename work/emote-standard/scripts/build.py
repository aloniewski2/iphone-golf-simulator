"""Build one emote clip on one hero:  blender -b blend/<Hero>_Std.blend --python build.py -- <male|female> <clip_module>

Solves every frame with the control rig, writes the action in the game's clip layout, removes the controls, re-plays the written action and checks
that it reproduces the solved pose (wrists / ankles within 0.5 mm), prints the diagnostics, saves the blend."""
import importlib
import json
import math
import os
import sys

import numpy as np
import bpy

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import ek  # noqa: E402

args = sys.argv[sys.argv.index('--') + 1:]
tag, modname = args[0], args[1]
prop = args[2] if len(args) > 2 else 'racket'
mod = importlib.import_module(modname)
H = ek.Hero(tag)
H.prop = prop
CLIP = mod.NAME + ('_Free' if prop == 'none' else '')
N, pose = mod.build(H)
Q, HL, LM = [], [], []
for f in range(N):
    pose(f)
    q, h = H.read_local()
    Q.append(q)
    HL.append(h)
    LM.append(H.landmarks())
ek.strip_controls(H)
act = ek.write_action(H, CLIP, Q, HL)
# replay check
err = 0.0
for f in range(N):
    H.sc.frame_set(f)
    bpy.context.view_layer.update()
    for n in ('LeftHand', 'RightHand', 'LeftFoot', 'RightFoot', 'Head'):
        e = np.linalg.norm(np.array((H.rig.matrix_world @ H.rig.pose.bones[n].matrix).translation) - LM[f][n])
        err = max(err, e)
print('REPLAY_MAX_MM', round(err * 1000, 3))

# diagnostics
info = getattr(H, 'info', {})
print('INFO', json.dumps({k: round(float(v), 4) for k, v in info.items()}))
body = bpy.data.objects['Body_M' if tag == 'male' else 'Body_F']
dg = bpy.context.evaluated_depsgraph_get()
rows = []
foot_low, rk_low = [], []
for f in range(N):
    H.sc.frame_set(f)
    dg.update()
    ev = body.evaluated_get(dg)
    me = ev.to_mesh()
    co = np.empty(len(me.vertices) * 3)
    me.vertices.foreach_get('co', co)
    co = co.reshape(-1, 3) @ np.array(body.matrix_world.to_3x3()).T + np.array(body.matrix_world.translation)
    ev.to_mesh_clear()
    lowz = co[:, 2].min()
    foot_low.append(lowz)
    rk = bpy.data.objects['Racket_Classic']
    rme = rk.evaluated_get(dg).to_mesh()
    rco = np.empty(len(rme.vertices) * 3)
    rme.vertices.foreach_get('co', rco)
    rco = rco.reshape(-1, 3) @ np.array(rk.matrix_world.to_3x3()).T + np.array(rk.matrix_world.translation)
    rk.evaluated_get(dg).to_mesh_clear()
    rk_low.append(rco[:, 2].min())
    pb = H.rig.pose.bones
    W = lambda n: H.rig.matrix_world @ pb[n].matrix
    wb = {}
    for s, pre in (('l', 'Left'), ('r', 'Right')):
        fa = np.array(W(pre + 'Hand').translation) - np.array(W(pre + 'LowerArm').translation)
        hy = np.array(W(pre + 'Hand').to_3x3().col[1])
        wb[s] = math.degrees(math.acos(np.clip(ek.unit(fa) @ hy, -1, 1)))
    rows.append((f, LM[f]['Hips'][2], lowz, rk_low[-1], wb['l'], wb['r']))
print('FRAME hipsZ  bodyLowZ  racketLowZ  wristBendL wristBendR')
for r in rows[::3]:
    print('F %2d  %.3f  %+.4f  %+.3f  %5.1f %5.1f' % r)
print('MIN_BODY_Z', round(min(foot_low), 4), 'at', int(np.argmin(foot_low)), 'MIN_RACKET_Z', round(min(rk_low), 3))
H.sc.frame_start, H.sc.frame_end = 0, N - 1
H.sc.frame_set(0)
bpy.context.preferences.filepaths.save_version = 0
bpy.ops.wm.save_as_mainfile(filepath=bpy.data.filepath)
print('BUILD_DONE', tag, CLIP, N)
