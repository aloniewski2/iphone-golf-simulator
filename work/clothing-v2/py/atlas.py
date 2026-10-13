"""CLOTHING V2 fabric atlas (512x512, linear): quadrants (col,row) (0,0) woven twill, (1,0) pique knit, (0,1) pebble leather, (1,1) 1x1 rib.
Each quadrant: a 240 px tile repeating 8 threads (TennisCloth: atlasUV = (quadrant + 1/32 + frac(uv) * 15/16) / 2), wrapped into an 8 px gutter.
RG = surface slope (0.5 + 0.5 * slope), B = thread visibility (mean 1/1.28)."""
import sys, os, math
sys.path.insert(0, os.path.dirname(__file__))
import numpy as np
from PIL_free import write_png

T = 240
y, x = np.mgrid[0:T, 0:T].astype(np.float64) + .5
tau = 2 * math.pi


def twill():
    h = np.sin(tau * (x + y) / 30.0)                     # diagonal wales, 8 per tile along each axis
    h = np.sign(h) * np.abs(h) ** .7
    h *= .8 + .2 * np.sin(tau * x / 15) * np.sin(tau * y / 15)
    return h


def pique():
    row = np.floor(y / 30)
    xo = x + 15 * (row % 2)
    cx = (xo % 30) - 15; cy = (y % 30) - 15
    d = np.maximum(np.abs(cx), np.abs(cy) * 1.1) / 15    # small raised cells with sunk borders
    h = np.cos(np.clip(d, 0, 1) * math.pi * .5) ** 1.5
    h += .25 * np.cos(tau * x / 7.5) * np.cos(tau * y / 7.5) * (d < .8)
    return h


def leather():
    rng = np.random.default_rng(7)
    n = 160
    pts = rng.random((n, 2)) * T
    d1 = np.full((T, T), 1e9); d2 = np.full((T, T), 1e9)
    for ox in (-T, 0, T):
        for oy in (-T, 0, T):
            for p in pts:
                d = np.hypot(x - (p[0] + ox), y - (p[1] + oy))
                m = d < d1; d2 = np.where(m, d1, np.minimum(d2, d)); d1 = np.where(m, d, d1)
    edge = d2 - d1
    h = np.clip(edge / 6.0, 0, 1) ** .6                  # pebbles, crevices between them
    return h


def rib():
    h = np.cos(tau * x / 30)                             # 8 ribs per tile, running along v
    h = np.sign(h) * np.abs(h) ** .6
    h += .18 * np.cos(tau * y / 15) * np.cos(tau * x / 30)  # knit loops
    return h


def encode(h, strength):
    h = (h - h.min()) / (h.max() - h.min() + 1e-9)
    gx = (np.roll(h, -1, 1) - np.roll(h, 1, 1)) * .5; gy = (np.roll(h, -1, 0) - np.roll(h, 1, 0)) * .5
    s = strength / (np.abs(np.concatenate([gx.ravel(), gy.ravel()])).max() + 1e-9)
    r = np.clip(.5 - .5 * gx * s, 0, 1); g = np.clip(.5 - .5 * gy * s, 0, 1)
    b = (.62 + .38 * h); b = b / b.mean() / 1.28
    return np.stack([r, g, np.clip(b, 0, 1)], -1)


def tile_into(out, q, img):
    ox, oy = q[0] * 256, q[1] * 256
    idx = (np.arange(256) - 8) % T
    out[oy:oy + 256, ox:ox + 256] = img[np.ix_(idx, idx)]


out = np.zeros((512, 512, 3))
tile_into(out, (0, 0), encode(twill(), .55))
tile_into(out, (1, 0), encode(pique(), .6))
tile_into(out, (0, 1), encode(leather(), .45))
tile_into(out, (1, 1), encode(rib(), .65))
# rows: image row 0 is the TOP (v = 1): quadrant row 1 (leather, rib) is the top half
img = (out[::-1] * 255 + .5).astype(np.uint8)
write_png(sys.argv[1], img)
print('atlas', sys.argv[1])
