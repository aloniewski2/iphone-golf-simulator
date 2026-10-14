"""Cap construction marks for the map baker: panel seams + topstitching, vent perforations (tennis), brim stitch rows, patch (golf), strap stitching.
Returns the B (trim mix) channel; modifies cv.H (height) and cv.C (cavity)."""
import math
import numpy as np
from acc_canvas import smoothstep, band, gauss
import cap as CAP


def wrap_pi(a): return (np.asarray(a) + math.pi) % (2 * math.pi) - math.pi


def vent_patch(cv, phic, z0, hw, hh, pitch, r, mask):
    """a grid of perforations in a rounded rectangle around (phi=phic, z=z0) on the crown."""
    phi = cv.A('phi', 0.0); zc = cv.A('zc', 0.0)
    R = .5 * (CAP.BASE['A'] + CAP.BASE['B']) * .965
    x = wrap_pi(phi - phic) * R; y = zc - z0
    inside = (np.abs(x) < hw) & (np.abs(y) < hh)
    row = np.floor(y / (pitch * .87) + .5)
    xo = x + (row % 2) * pitch * .5
    cx = ((xo / pitch + .5) % 1.0 - .5) * pitch
    cy = (y - row * pitch * .87)
    d = np.hypot(cx, cy)
    aa = cv.texel * .6
    hole = (1 - smoothstep(r - aa, r + aa, d)) * inside * mask
    cv.dimple(hole, depth=.0009, dark=.9)
    return hole


def details_cap(cv, variant):
    tennis = variant == 'tennis'
    k = cv.kind; z = cv.zone; aa = cv.texel * .7
    crown = ((k == CAP.K_CROWN) & (z != 2)).astype(float)
    lining = ((k == CAP.K_CROWN) & (z == 2)).astype(float)
    ds = cv.A('d_seam', 1.0); zc = cv.A('zc', 0.0)
    trim = np.zeros(cv.n)
    # ---- crown: seam crease + topstitching either side (double needle on the golf cap)
    cv.groove(ds, 0.0, crown, width=.0009, depth=.0006, dark=.34)
    rows = (.0034,) if tennis else (.0024, .0050)
    for at in rows:
        th = cv.stitch(ds, at, zc, crown * (ds < .012), pitch=.0032, width=.00055, height=.00026)
        if not tennis: trim = np.maximum(trim, th * 0.0)
    cv.groove(ds, 0.0, lining, width=.0010, depth=.0003, dark=.20)
    # the crown's own soft texture: a faint panel weave break-up
    cv.folds(cv.um * 3 + cv.vm, .020, .00005, crown, 3)
    # ---- tennis vents: two small patches on the front panels, two big ones low on the sides
    if tennis:
        for s_ in (-1, 1):
            h1 = vent_patch(cv, s_ * math.radians(31), .056, .0185, .0072, .0026, .00085, crown)
            h2 = vent_patch(cv, s_ * math.radians(102), .0200, .0270, .0135, .0029, .00095, crown)
            trim = np.maximum(trim, np.maximum(h1, h2))          # looking through the mesh: the holes read as the dark trim colour
    # ---- brim: edge welt + stitch rows on both faces, root seam to the crown
    brim = (k == CAP.K_BRIM).astype(float)
    de = cv.A('d_edge', 1.0); dr = cv.A('d_root', 1.0); psi = cv.A('psi', 0.0)
    along = psi * .12
    cv.ridge(de, .0013, brim, width=.0010, height=.00028)
    rows = (.0042, .0078, .0114) if tennis else (.0042, .0078, .0114, .0150)
    for at in rows:
        th = cv.stitch(de, at, along, brim * (de > .0035) * (dr > .004), pitch=.0030, width=.00055, height=.00026)
        if not tennis: trim = np.maximum(trim, th)           # white thread on the navy golf brim
    cv.groove(dr, .0006, brim, width=.0012, depth=.0004, dark=.25)
    # ---- strap
    st = (k == CAP.K_STRAP).astype(float)
    sx = cv.A('sx', 0.0); sy = cv.A('sy', 0.0)
    w2 = CAP.BASE['strap']['w'] / 2
    dedge = w2 - np.abs(sy)
    if tennis:
        for at in (.0026,):
            cv.stitch(dedge, at, sx, st * (dedge > .0008), pitch=.0030, width=.00050, height=.00022)
        # box stitch near each end of the webbing
        # (ends are tucked under the crown; the visible span only needs the edge rows)
    else:
        cv.ridge(dedge, .0022, st, width=.0009, height=.0004)
    # ---- patch (golf): woven white patch with a navy border stitch and a raised edge
    pt = (k == CAP.K_PATCH).astype(float)
    if pt.any():
        px = cv.A('px', 0.0); py = cv.A('py', 0.0)
        d = np.minimum(.054 - np.abs(px), .0290 - np.abs(py))
        cv.ridge(d, .0006, pt, width=.0010, height=.00035)
        th = cv.stitch(d, .0030, px + py, pt * (d > .0012), pitch=.0028, width=.00050, height=.00026)
        trim = np.where(pt > .5, 1.0, trim)
        trim = np.where((pt > .5) & (th > .35), 0.0, trim)
    # ---- the rest of the trim (colour B): tennis brim, piping, button, strap; golf: patch + brim thread
    if tennis:
        for kk in (CAP.K_BRIM, CAP.K_PIPING, CAP.K_BUTTON, CAP.K_STRAP):
            trim = np.where(k == kk, 1.0, trim)
    # band: a faint line where it folds over the crown edge
    bd = (k == CAP.K_BAND).astype(float)
    zb = cv.A('zc', 0.0)
    cv.groove(zb, CAP.BASE['band']['h'] - .0006, bd, width=.0007, depth=.0003, dark=.2)
    return np.clip(trim, 0, 1)
