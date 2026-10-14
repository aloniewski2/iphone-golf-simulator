"""GLOVE: the glove's pattern on the product (curves + perforation centres), written in the product frame G with the product skeleton."""
import os, sys, math
sys.path.insert(0, os.path.dirname(__file__))
import numpy as np
import glove_sdf as S, glove_curves as C

MM = 1e-3


def mm(a): return np.asarray(a, float) * MM


def loft_centre(P=S.PRODUCT):
    L = P['loft']
    ys = np.array(L['ys']) * MM; cx = np.array(L['cx']) * MM; cz = np.array(L['cz']) * MM
    return lambda y: (float(np.interp(y, ys, cx)), float(np.interp(y, ys, cz)))


def build_pattern(surf, parts, P=S.PRODUCT, sx=1.0, sy=1.0):
    cur = {}
    def mmx(pts): return mm([(x * sx, y * sy, 0) for x, y in pts])
    fingers = parts['fingers']; names = ['Index', 'Middle', 'Ring', 'Little']
    cen = loft_centre(P)
    for nm, ch in zip(names, fingers):
        if nm == 'Little':
            c = C.finger_equator(surf, ch, s_start=0.019, name='fin_' + nm, s_start2=0.0)
            # continue down the ulnar side of the hand to the cuff edge
            yl = ch.P[0][1]
            ys = np.arange(yl, -0.036 - 1e-9, -0.0006)
            u = C.side_line(surf, ys, cen, -1, 'ulnar', z_off=0.0)
            c.extend(u)
        else:
            c = C.finger_equator(surf, ch, s_start=0.019, name='fin_' + nm)
        cur['fin_' + nm] = c.clean().smooth(surf).densify(surf).smooth_wf()
    th = parts['thumb']
    ct = C.finger_equator(surf, th, s_start=0.006, name='thumb', s_start2=0.0155)
    # radial side seam from the cuff edge up to the thumb root joins the thumb's outer side
    ys = np.arange(-0.036, 0.016, 0.0006)
    r = C.side_line(surf, ys, cen, +1, 'radial', z_off=-0.0035)
    r.extend(ct); ct = r; ct.name = 'thumb'
    apex = th.P[-1] + S.unit(th.P[-1] - th.P[-2]) * th.r[-1]
    ct.tip = int(np.argmin(np.linalg.norm(ct.pts0 - apex, axis=1)))
    cur['thumb'] = ct.clean().smooth(surf, win=8).densify(surf).smooth_wf()
    # palm pad outline (palmar side, rays from below)
    pad = [(30, 64), (19, 78), (0, 85), (-22, 84), (-36, 76), (-41, 56), (-39, 30), (-31, 13), (-14, 6), (3, 6), (17, 14), (25, 30), (29, 48)]
    cur['pad'] = C.surface_path(surf, mmx(pad), (0, 0, 1), closed=True)
    cur['pad'].name = 'pad'; cur['pad'].kind = 'closed'
    # thenar crease
    cre = [(31, 60), (24, 52), (14, 40), (6, 27), (3, 12)]
    cur['crease'] = C.surface_path(surf, mmx(cre), (0, 0, 1))
    cur['crease'].name = 'crease'
    # back of hand: knuckle yoke and wrist band seams
    yoke = [(-43, 69), (-30, 76), (-10, 79.5), (10, 78.5), (28, 73), (41, 63)]
    cur['yoke'] = C.surface_path(surf, mmx(yoke), (0, 0, -1)); cur['yoke'].name = 'yoke'
    band = [(-34, 7), (-17, 10), (0, 11), (17, 10), (33, 6)]
    cur['band'] = C.surface_path(surf, mmx(band), (0, 0, -1)); cur['band'].name = 'band'
    return cur


def perforations(surf, parts, sx=1.0, sy=1.0):
    """perforation centres (dorsal side): hex field on the back of the hand + two columns along each finger"""
    pts = []
    for (x, y) in C.hex_grid(-33 * MM * sx, 33 * MM * sx, 22 * MM * sy, 64 * MM * sy, 8.0 * MM * max(sx, .75)):
        pts.append((x, y))
    holes = C.Curve('perf', kind='points')
    for (x, y) in pts:
        holes.add(surf.ray(np.array([x, y, .12]), np.array([0, 0, -1.0]), .3))
    fingers = parts['fingers']
    for ch in fingers:
        L = ch.seglens().sum()
        k = 0
        for s in np.arange(0.017, L - 0.004, 0.0072):
            p, u, e1, e2 = ch.frame_at(s)
            lat = (-1 if k % 2 == 0 else 1) * 0.0026
            o = p + e1 * lat + e2 * .05
            holes.add(surf.ray(o, -e2, .1)); k += 1
    return holes.finish()
