"""BAG: render the posed body + fitted bag at chosen clip frames.
Blender -b --factory-startup --python bag_pose_render.py -- <Male|Female> <outdir> <prefix> <HeroPrefix> <clip:frame[,clip:frame...]> <views comma list> [WxH]
views: front back side back34 front34r strap (close on the strap/armpit)  e.g.  GolfKitHero Idle:5,Walk:12 back34,front34r"""
import sys, os, json, math
sys.path.insert(0, os.path.dirname(__file__))
import numpy as np, bpy
import acc_common as A, acc_geo as G, acc_preview as PV
import bag_pose as BP

VIEWS = {'front': (0, 6), 'back': (180, 6), 'side': (90, 4), 'sider': (-90, 4), 'back34': (146, 12), 'front34r': (-38, 12), 'front34': (38, 12), 'strap': (-35, 10), 'strapside': (-80, 8)}


def main():
    a = A.args_after_dashes()
    sex, outdir, prefix, hp, cf, views = a[0], a[1], a[2], a[3], a[4].split(','), a[5].split(',')
    res = tuple(int(v) for v in a[6].split('x')) if len(a) > 6 else (800, 1000)
    rig = BP.Rig(sex, f'{hp}_{sex}')
    d = os.path.join(A.ROOT, 'out', sex, 'golf_bag')
    for item in cf:
        clip, fi = item.split(':'); fi = int(fi)
        c = next(c for c in rig.hero.clips if c['name'] == clip or c['name'].endswith('_' + clip))
        body, bones = rig.hero.clip(c['name'])
        q = rig.bag_world_N(bones[fi].astype(np.float64))
        bp = A.unity_to_N(body[fi].astype(np.float64))
        A.reset()
        ob = A.mesh_object('Body', bp, rig.hd.body_tri)
        ob.data.materials.append(A.material('Skin', A.srgb('B7744A' if sex == 'Male' else 'D49A74'), .55))
        g = {k: rig.g[k] for k in rig.g.files}; g['pos'] = q
        PV._object_from_npz(g, 'Bag', d, 'Bag', 'golf_bag')
        A.studio(res=res, bg=(.33, .34, .36))
        ctr = bp[np.nonzero((bp[:, 2] > 1.1) & (bp[:, 2] < 1.35) & (np.abs(bp[:, 0]) < 0.15))[0]].mean(0)
        for v in views:
            az, el = VIEWS.get(v, (0, 6))
            if v.startswith('strap'): tgt, dist, lens = (ctr + np.array([-0.12, 0.0, 0.0]), 0.9, 85)
            else: tgt, dist, lens = (ctr + np.array([0, 0.08, -0.1]), 2.4, 70)
            A.camera(tuple(tgt), az, el, dist, lens)
            A.render(os.path.join(outdir, f'{prefix}_{clip}_{fi}_{v}.png'))


if __name__ == '__main__':
    main()
