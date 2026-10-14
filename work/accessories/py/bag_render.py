"""BAG: renders of a written piece (npz + N/M maps) alone, with the maps on.
Blender -b --factory-startup --python bag_render.py -- <piece_dir> <outdir> <prefix> <views comma list> [res WxH]
views: name or name:crop ; names front side back 34 back34 top under ; crops: all rim pocket zip strap buckle legs covers base side_pocket hub handle
"""
import sys, os, math
sys.path.insert(0, os.path.dirname(__file__))
import numpy as np, bpy
import acc_common as A, acc_geo as G, acc_preview as PV

CENTRE = np.array([0.0, 0.0, 0.46])
CROPS = {   # name -> (target xyz, distance, lens)
    'all': (CENTRE + [0, 0, 0.015], 2.40, 70),
    'rim': ((0, 0.0, 0.84), 0.95, 85),
    'covers': ((0, -0.02, 0.88), 0.85, 85),
    'pocket': ((-0.05, -0.11, 0.50), 0.85, 80),
    'zip': ((0.02, -0.11, 0.58), 0.60, 80),
    'side_pocket': ((0.13, -0.02, 0.52), 0.95, 80),
    'strap': ((0.10, 0.12, 0.40), 1.0, 75),
    'buckle': ((0.16, 0.05, 0.40), 0.55, 85),
    'legs': ((0, 0.12, 0.62), 0.95, 80),
    'base': ((0, 0, 0.12), 0.75, 80),
    'hub': ((0, 0.12, 0.62), 0.7, 80),
    'handle': ((0, 0.10, 0.80), 0.7, 80),
}
VIEWS = {'front': (0, 6), 'side': (90, 6), 'sider': (-90, 6), 'back': (180, 6), '34': (38, 18), 'back34': (146, 18), 'top': (0, 80), 'under': (0, -60),
         'front34r': (-38, 18)}


def main():
    a = A.args_after_dashes()
    d, outdir, prefix, views = a[0], a[1], a[2], a[3].split(',')
    res = (900, 1000)
    if len(a) > 4 and 'x' in a[4]: res = tuple(int(v) for v in a[4].split('x'))
    g = np.load(os.path.join(d, 'Kit_Bag_final.npz'))
    A.reset()
    ob = PV._object_from_npz(g, 'Bag', d, 'Bag', 'golf_bag')
    A.studio(res=res, bg=(.33, .34, .36))
    for v in views:
        name, _, crop = v.partition(':')
        az, el = VIEWS.get(name, (0, 6))
        tgt, dist, lens = CROPS.get(crop or 'all')
        A.camera(tuple(tgt), az, el, dist, lens)
        A.render(os.path.join(outdir, f'{prefix}_{name}{("_" + crop) if crop else ""}.png'))


if __name__ == '__main__':
    main()
