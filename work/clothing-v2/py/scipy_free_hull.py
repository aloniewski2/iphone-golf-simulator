"""per-slice 2D convex hulls of a point cloud in the shoe frame (x = lateral, y = along the foot, z = up)."""
import math
import numpy as np
import glib as G


def slices_envelope(q, y0, y1, dy, core_z=.035, nth=96, band=.004):
    ths = np.linspace(-math.pi, math.pi, nth, endpoint=False)
    ys = np.arange(y0, y1 + dy * .5, dy)
    R = np.zeros((nth, len(ys)))
    for j, y in enumerate(ys):
        m = np.abs(q[:, 1] - y) < band
        if m.sum() < 4: continue
        pts = np.stack([q[m, 0], q[m, 2] - core_z], 1)
        h = G.convex_hull_2d(pts)
        if len(h) < 3: continue
        R[:, j] = G.hull_radius(h, np.zeros(2), ths)
    for i in range(nth):
        nz = np.nonzero(R[i])[0]
        if len(nz): R[i] = np.interp(np.arange(len(ys)), nz, R[i, nz])
    env = G.Envelope(y0, dy, ths, R)
    env.smooth(iters=2)
    return env
