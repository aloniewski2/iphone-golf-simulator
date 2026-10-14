"""BAG: the fitted bag on a hero (skin body only, no clothes), several views.
Blender -b --factory-startup --python bag_hero_render.py -- <Male|Female> <outdir> <prefix> <views comma list> [WxH]
views: name[:crop]; names front 34 side back back34 sider front34r top ; crops full torso strap shoulder bag head back"""
import sys, os, math, json
sys.path.insert(0, os.path.dirname(__file__))
import numpy as np, bpy
import acc_common as A, acc_geo as G, acc_preview as PV

VIEWS = {'front': (0, 6), '34': (38, 12), 'side': (90, 4), 'back': (180, 6), 'back34': (146, 12), 'sider': (-90, 4), 'front34r': (-38, 12), 'top': (0, 70), 'frontup': (0, 22),
         'side34': (62, 10), 'back34r': (-146, 12)}


def crops(hd):
    head = hd.rest_pos['Head']
    return {'upper': ((0, 0.10, 1.10), 2.35, 62), 'full': ((0, 0.04, 0.95), 4.3, 62), 'torso': ((0, 0.04, 1.18), 2.0, 70), 'strap': ((-0.10, -0.02, 1.22), 1.1, 85), 'chest': ((-0.06, -0.05, 1.18), 1.5, 80), 'flank': ((-0.14, 0.02, 1.08), 1.2, 80), 'shoulder': ((-0.10, 0.0, 1.32), 0.95, 85),
            'bag': ((0, 0.20, 1.02), 2.0, 70), 'head': ((0, 0.05, head[2] - 0.05), 1.5, 80), 'back': ((0, 0.15, 1.1), 1.9, 70)}


def main():
    a = A.args_after_dashes()
    sex, outdir, prefix, views = a[0], a[1], a[2], a[3].split(',')
    res = tuple(int(v) for v in a[4].split('x')) if len(a) > 4 else (900, 1100)
    d = os.path.join(A.ROOT, 'out', sex, 'golf_bag')
    g = np.load(os.path.join(d, 'Kit_Bag_final.npz'))
    A.reset()
    hd = A.HeroData(sex)
    A.load_body(hd)
    PV._object_from_npz(g, 'Bag', d, 'Bag', 'golf_bag')
    A.studio(res=res, bg=(.33, .34, .36))
    C = crops(hd)
    for v in views:
        name, _, crop = v.partition(':')
        az, el = VIEWS.get(name, (0, 6))
        tgt, dist, lens = C.get(crop or 'upper')
        A.camera(tuple(tgt), az, el, dist, lens)
        A.render(os.path.join(outdir, f'{prefix}_{name}{("_" + crop) if crop else ""}.png'))


if __name__ == '__main__':
    main()
