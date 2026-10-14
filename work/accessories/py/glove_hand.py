"""GLOVE: hero hand skeleton + frames (numpy only, N frame). Left hand is the native one; the right hand is the same code with side='Right'.
Glove/product frame ('G'): origin at the wrist bone, +Y along the fingers (wrist -> middle MCP), +X toward the thumb side, +Z = dorsal (back of hand).
  X x Y = Z holds for the LEFT hand (thumb x fingers = dorsal), so G is right-handed for the left glove; the right glove is built as the mirror (x -> -x)."""
import numpy as np
import acc_common as A

FINGERS = ['Thumb', 'Index', 'Middle', 'Ring', 'Little']
SEGS = ['Proximal', 'Intermediate', 'Distal']


def unit(v): v = np.asarray(v, float); return v / (np.linalg.norm(v) + 1e-15)


class Hand:
    def __init__(self, hd, side='Left'):
        self.hd, self.side = hd, side
        rp = hd.rest_pos
        self.names = [f'{side}{f}{s}' for f in FINGERS for s in SEGS]
        self.wrist = rp[f'{side}Hand']; self.elbow = rp[f'{side}LowerArm']
        y = unit(rp[f'{side}MiddleProximal'] - self.wrist)
        # thumb side direction: from the little finger knuckle to the index knuckle, made orthogonal to y
        t = rp[f'{side}IndexProximal'] - rp[f'{side}LittleProximal']
        x = unit(t - y * np.dot(t, y))
        # dorsal axis: for the left hand z = x cross y; the right hand uses the mirror (z = y cross x) so that dorsal is the same physical side
        z = unit(np.cross(x, y)) if side == 'Left' else unit(np.cross(y, x))
        # dorsal check: the back of the hand is the side facing away from the body centre line / palm: verify with the palm vertices later
        self.R = np.stack([x, y, z], 1)                                # columns: G axes in N
        # joint positions (N)
        self.J = {}
        for f in FINGERS:
            for s in SEGS: self.J[f + s] = rp[f'{side}{f}{s}']

    def to_G(self, p): return (np.asarray(p) - self.wrist) @ self.R
    def from_G(self, q): return np.asarray(q) @ self.R.T + self.wrist
    def chain(self, f):
        """joint positions (N) prox, interm, distal + tip estimate"""
        a, b, c = [self.J[f + s] for s in SEGS]
        return a, b, c
