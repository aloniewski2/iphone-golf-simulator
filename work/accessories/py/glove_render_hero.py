"""Render the fitted glove on a hero (maps on). Blender -b --python glove_render_hero.py -- <Male|Female> <outdir> [views] [--nobody]
views: hand_back,hand_palm,hand_thumb,hand_little,hand_q34,hand_wrist,front,q34,side,back"""
import sys, os, json
sys.path.insert(0, os.path.dirname(__file__))
import numpy as np, bpy
import acc_common as A, acc_preview as AP, glove_view as GV, glove_hand as GH

OUT = os.path.join(A.ROOT, 'out')


def load_glove(sex, pieces=None, base_hex=None, trim_hex=None):
    d = os.path.join(OUT, sex, 'golf_glove')
    meta = json.load(open(os.path.join(d, 'piece.json')))
    objs = []
    for pc in (pieces or meta['pieces']):
        g = np.load(os.path.join(d, pc + '_final.npz'))
        objs.append(AP._object_from_npz(g, 'golf_glove_' + pc, d, meta['tags'].get(pc, meta['tag']), 'golf_glove', base_hex=base_hex, trim_hex=trim_hex))
    return objs


def hero_views(sex, side='Left'):
    hd = A.HeroData(sex); H = GH.Hand(hd, side)
    R = H.R
    c = (hd.rest_pos[f'{side}Hand'] + hd.rest_pos[f'{side}MiddleIntermediate']) / 2
    def d(v): return tuple(R @ np.array(v, float))
    V = {
        'hand_back': (c, d((0, 0, 1)), d((0, 1, 0)), .55),
        'hand_palm': (c, d((0, 0, -1)), d((0, 1, 0)), .55),
        'hand_thumb': (c, d((1, 0, 0)), d((0, 0, 1)), .55),
        'hand_little': (c, d((-1, 0, 0)), d((0, 0, 1)), .55),
        'hand_q34': (c, d((.6, -.5, .7)), d((0, 1, 0)), .5),
        'hand_wrist': (H.wrist - np.array([0, 0, 0]) + R @ np.array([0, -.02, 0]), d((.3, -1, .5)), d((0, 0, 1)), .35),
        'hand_close': (c, d((.2, -.15, 1)), d((0, 1, 0)), .30),
        'hand_close_palm': (c, d((.1, -.2, -1)), d((0, 1, 0)), .30),
    }
    return V


if __name__ == '__main__':
    a = A.args_after_dashes()
    sex, outdir = a[0], a[1]
    views = a[2].split(',') if len(a) > 2 and not a[2].startswith('--') else ['hand_back', 'hand_palm', 'hand_thumb', 'hand_q34', 'front', 'q34', 'side']
    A.reset()
    hd = A.HeroData(sex)
    if '--handonly' in a:
        import glove_fit as FT
        hs = FT.HeroSkin(sex, 'Right' if '--right' in a else 'Left')
        ob = A.mesh_object('Body', hd.body_pos[hs.ids], hs.T if '--right' not in a else hs.T[:, ::-1])
        ob.data.materials.append(A.material('Skin', A.srgb('B7744A' if sex == 'Male' else 'D49A74'), .55))
    elif '--nobody' not in a: A.load_body(hd)
    side = 'Right' if '--right' in a else 'Left'
    only_hand = any(v.startswith('hand_') for v in views)
    load_glove(sex, pieces=([('Kit_Glove_R' if side == 'Right' else 'Kit_Glove_L')] if only_hand else None))
    GV.setup((1000, 1000), bg=(.34, .35, .37), samples=64)
    side = 'Right' if '--right' in a else 'Left'
    hv = hero_views(sex, side)
    for v in views:
        if v in hv:
            c, dirv, up, dist = hv[v]
            GV.shoot(os.path.join(outdir, f'{sex}{"R" if side == "Right" else ""}_glove_{v}.png'), tuple(c), dirv, dist=dist, lens=70, up=up)
        else:
            az = {'front': 0, 'q34': 38, 'side': 90, 'back': 180}[v]
            import math
            e = 8.0
            dirv = (math.sin(math.radians(az)) * math.cos(math.radians(e)), -math.cos(math.radians(az)) * math.cos(math.radians(e)), math.sin(math.radians(e)))
            GV.shoot(os.path.join(outdir, f'{sex}{"R" if side == "Right" else ""}_glove_{v}.png'), (0, -.02, 0.95), dirv, dist=2.7, lens=70, up=(0, 0, 1))
