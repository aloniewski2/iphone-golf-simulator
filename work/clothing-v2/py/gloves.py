"""Golf glove: the hand region of the real body offset by a thin leather shell (fingers kept), cut a little above the wrist,
a rib wrist band and a closure tab on the back of the hand. Left glove for right-handers, right glove for the left-handed option."""
import math
import numpy as np
import glib as G
from garment import Garment, solidify, LEATHER, RIB
import parts


def build_gloves(body, P):
    out = {}
    for sd in (-1, 1):
        out['Kit_Glove_' + ('L' if sd < 0 else 'R')] = glove(body, P, sd)
    return out


def glove(body, P, sd):
    s = 'Left' if sd < 0 else 'Right'
    hand = body.head(s + 'Hand'); elbow = body.head(s + 'LowerArm')
    ax = hand - elbow; ax /= np.linalg.norm(ax)
    hw = body.handL if sd < 0 else body.handR
    fw = body.forearmL if sd < 0 else body.forearmR
    p = body.pos
    t = (p - hand) @ ax
    region = (fw > .3) & (t > -P['cuff_len'] - .03)
    tri = body.tri[region[body.tri].all(1)]
    r = G.compact(p, tri, body.W)
    pos, tri = r[0], r[1]
    x = G.solve_shell(body, pos, tri, np.full(len(pos), P['clear']), iters=6, lam=.35, mu=-.37)
    g = Garment(x, tri, fabric=LEATHER, roles=('Kit_Glove',), W=r[2])
    g.A['f_cuff'] = (g.pos - hand) @ ax + P['cuff_len']
    g.cut(g.A['f_cuff'])
    for v in [.012, .011]: g.insert(g.A['f_cuff'] - v)
    band = g.A['f_cuff'] < .012
    g.A['lift'][band] = .0006
    g.F['fabric'][(g.A['f_cuff'][g.tri].mean(1) < .012)] = RIB
    shell = solidify(g, P.get('thickness', .0012), facing=.012)
    # closure tab on the back of the hand: a rounded raised patch near the wrist
    tree = parts.bvh(g.pos, g.tri)
    # back of the hand: away from the palm; palm normal ~ the hand bone's local axis toward the thumb side is unreliable, use the body's
    # mean normal of the hand surface 2-5 cm from the wrist on the side facing outward/up in the rest pose
    m = ((body.pos - hand) @ ax > .01) & ((body.pos - hand) @ ax < .05) & (hw > .5)
    nmean = body.nrm[m]
    # pick the cluster most aligned with the hand's lateral outward direction (back of hand faces away from the body in the rest pose)
    lateral = np.array([sd, 0, 0.]); lateral -= ax * (lateral @ ax); lateral /= np.linalg.norm(lateral)
    best = nmean[np.argmax(nmean @ lateral)]
    c0 = hand + ax * .02
    rr = tree.ray_cast(parts.Vector(c0 + best * .2), parts.Vector(-best))
    if rr[0] is not None:
        c = np.array(rr[0]); n = np.array(rr[1])
        u = ax - n * (ax @ n); u /= np.linalg.norm(u); v = np.cross(n, u)
        ring = []
        for k in range(24):
            a = 2 * math.pi * k / 24
            q = c + u * (.016 * math.cos(a)) + v * (.013 * math.sin(a))
            rr2 = tree.ray_cast(parts.Vector(q + n * .05), parts.Vector(-n))
            ring.append(np.array(rr2[0]) + np.array(rr2[1]) * .0012 if rr2[0] is not None else q + n * .0012)
        ring = np.array(ring)
        pos2 = np.concatenate([[c + n * .0018], ring])
        tri2 = np.array([(0, 1 + k, 1 + (k + 1) % 24) for k in range(24)], np.int64)
        tri2 = parts.orient_outward(pos2, tri2, lambda q: np.tile(c - n * .02, (len(q), 1)))
        tab = Garment(pos2, tri2, fabric=LEATHER, roles=('Kit_GolfGlove',), kind='tab')
        tab.A['cavity'][:] = .9
        shell.merge(solidify(tab, .0012, facing=None))
    return shell
