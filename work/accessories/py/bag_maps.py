"""BAG construction marks for the map baker (acc_canvas.bake): seams + topstitching, piping, zip teeth, quilting channels, knit stitches on the head covers, grips.
details_bag(cv) returns the B (trim colour mix) channel and modifies cv.H (height, metres) / cv.C (cavity).
Everything is driven by per-point fields written by bag.py, so the marks stay glued to the geometry in any pose of the product or on the hero."""
import math
import numpy as np
from acc_canvas import smoothstep, band, gauss
from bag import K, FAB, COVER_CUFF_Z, COVER_TOP_Z


def wrap_pi(a): return (np.asarray(a) + math.pi) % (2 * math.pi) - math.pi


def hash2(i, j):
    v = np.sin(i * 12.9898 + j * 78.233) * 43758.5453
    return v - np.floor(v)


def knit_relief(cv, mask, kphi, kz, ncol, pitch, amp, width=0.17):
    """chunky stockinette: straight columns of V stitches (two leaning oval legs each). returns (relief 0..1, column index, row index)."""
    col = kphi / (2 * math.pi) * ncol
    row = kz / pitch
    ci = np.floor(col + 0.5); ri = np.floor(row)
    u = col - ci                                   # -0.5 .. 0.5 across the column
    v = row - ri                                   # 0 .. 1 along it
    rel = np.zeros(cv.n)
    for kk in (-1.0, 1.0):
        cx = kk * 0.215
        ax, ay = -kk * 0.34, 1.0
        nrm = math.hypot(ax, ay); ax /= nrm; ay /= nrm
        du = u - cx; dv = (v - 0.5) * 1.15
        along = du * ax + dv * ay
        across = -du * ay + dv * ax
        rel = np.maximum(rel, np.exp(-(along / 0.62) ** 2 - (across / width) ** 2))
    cv.H += amp * rel * mask
    cv.C *= 1 - 0.55 * (1 - rel) * mask * 0.5
    return rel, ci, ri


def details_bag(cv):
    k = cv.kind; zn = cv.zone; tex = cv.texel
    trim = np.zeros(cv.n)

    def M(kind): return (k == kind).astype(float)

    # ---------------------------------------------------------------- shell: navy boot, seam, topstitching, corner seams
    sh = M(K['SHELL'])
    if sh.any():
        d_b = cv.A('d_b', 0.0); phi = cv.A('phi', 0.0); zc = cv.A('zc', 0.0)
        navy = 1 - smoothstep(-0.0004, 0.0004, d_b)
        trim = np.where(sh > 0, navy, trim)
        cv.groove(d_b, 0.0, sh, width=.0011, depth=.0007, dark=.40)
        for at in (-0.0042, 0.0042):
            th = cv.stitch(d_b, at, phi * 0.098, sh * (np.abs(d_b) < .02), pitch=.0034, width=.00075, height=.00028)
            if at < 0: trim = np.where((sh > 0) & (th > .35), 0.0, trim)       # white thread on the navy boot
        # vertical corner seams (front panel to side gusset) + double topstitch
        for ph0 in (-0.82, 0.82, wrap_pi(math.pi - 0.82), wrap_pi(math.pi + 0.82)):
            dd = np.abs(wrap_pi(phi - ph0)) * 0.098
            cv.groove(dd, 0.0, sh, width=.0011, depth=.0007, dark=.38)
            for off in (-.0042, .0042):
                cv.stitch(dd, off, zc, sh * (dd < .012), pitch=.0034, width=.00075, height=.00028)
        # soft panel puff / micro folds on the white twill so the big areas are not dead flat
        cv.folds(zc * 1.0 + phi * 0.04, .030, .00007, sh * (1 - navy), 5)
    # ---------------------------------------------------------------- lining / floor / dividers / rim / base / piping
    li = M(K['LINING']) + M(K['FLOOR'])
    trim = np.where(li > 0, 1.0, trim)
    base = M(K['BASE'])
    if base.any():
        trim = np.where(base > 0, 1.0, trim)
        zc = cv.A('zc', 0.0)
        cv.groove(zc, 0.0245, base, width=.0012, depth=.0008, dark=.35)
        cv.groove(zc, 0.0105, base, width=.0010, depth=.0006, dark=.30)
    div = M(K['DIVIDER'])
    pip = M(K['PIPING'])
    trim = np.where(pip > 0, 1.0, trim)
    rim = M(K['RIM'])
    # ---------------------------------------------------------------- patches: pockets, hub, back pocket
    for kk in (K['POCKET'], K['HUB'], K['BACKPOCKET']):
        pm = M(kk)
        if not pm.any(): continue
        de = cv.A('d_edge', 1.0)
        trim = np.where(pm > 0, 1.0, trim)
        cv.ridge(de, .0012, pm, width=.0013, height=.00045)
        th = cv.stitch(de, .0050, cv.A('px', 0.0) + cv.A('py', 0.0), pm * (de > .002), pitch=.0034, width=.00078, height=.00030)
        trim = np.where((pm > 0) & (th > .35), 0.0, trim)
        if kk == K['BACKPOCKET']:
            py = cv.A('py', 0.0)
            cv.groove(py - 0.082, 0.0, pm, width=.0012, depth=.0007, dark=.35)
            th = cv.stitch(py - 0.082 - 0.0045, 0.0, cv.A('px', 0.0), pm * (np.abs(py) < .12), pitch=.0034, width=.00075, height=.00028)
            trim = np.where((pm > 0) & (th > .35), 0.0, trim)
    # outlined pockets (white fill, navy border + piping look)
    p2 = M(K['POCKET2'])
    if p2.any():
        de = cv.A('d_edge', 1.0)
        border = 1 - smoothstep(0.0040, 0.0048, de)
        trim = np.where(p2 > 0, border, trim)
        cv.ridge(de, .0014, p2, width=.0013, height=.00045)
        cv.groove(de - 0.0044, 0.0, p2, width=.0007, depth=.0004, dark=.30)
        th = cv.stitch(de, .0070, cv.A('px', 0.0) + cv.A('py', 0.0), p2 * (de > .003), pitch=.0034, width=.00075, height=.00028)
        trim = np.where((p2 > 0) & (th > .35), 1.0, trim)
    # ---------------------------------------------------------------- zips: tape, teeth, centre slot
    zp = M(K['ZIP'])
    if zp.any():
        sx = cv.A('sx', 0.0); sa = cv.A('sa', 0.0); hw = cv.A('hwid', 0.004)
        trim = np.where(zp > 0, 1.0, trim)
        cv.groove(np.abs(sx), 0.0, zp, width=.00055, depth=.0006, dark=.55)
        for sgn in (-1, 1):
            cv.stitch(np.abs(sx), 0.00205, sa, zp, pitch=.0023, width=.00060, height=.00024)
        cv.ridge(hw - np.abs(sx), .0004, zp, width=.0006, height=.0002)
    # ---------------------------------------------------------------- straps + handle: webbing, foam pad, binding, stitching, quilting channels
    for kk in (K['STRAP'], K['HANDLE']):
        st = M(kk)
        if not st.any(): continue
        sx = cv.A('sx', 0.0); sa = cv.A('sa', 0.0); hw = cv.A('hwid', 0.02); tot = cv.A('tot', 1.0)
        sec = cv.A('sec', 1.0) if kk == K['STRAP'] else np.ones(cv.n)
        pad = (sec > 0.5).astype(float)
        dedge = hw - np.abs(sx)
        bind = (1 - smoothstep(0.0042, 0.0050, dedge)) * pad
        web = 1 - pad
        trim = np.where(st > 0, np.maximum(bind, web), trim)
        th = cv.stitch(dedge, 0.0070, sa, st * pad * (dedge > .003), pitch=.0036, width=.00078, height=.00030)
        trim = np.where((st > 0) & (th > .35), 0.0, trim)
        th = cv.stitch(dedge, 0.0030, sa, st * web * (dedge > .0012), pitch=.0032, width=.00075, height=.00026)
        trim = np.where((st > 0) & (th > .35) & (web > 0.5), 0.0, trim)
        cv.groove(np.abs(sx) - 0.0090, 0.0, st * pad * (hw > 0.020), width=.0016, depth=.0010, dark=.28)
        cv.ridge(dedge, .0016, st * pad, width=.0016, height=.0005)
        if kk == K['STRAP']:
            # bar tacks / box stitch near both ends
            for at in (0.034, 0.042):
                cv.stitch(sa - at, 0.0, sx, st * (np.abs(sx) < hw - .0015), pitch=.0032, width=.00075, height=.00028)
            cv.stitch((tot - sa) - 0.032, 0.0, sx, st * (np.abs(sx) < hw - .0015), pitch=.0032, width=.00075, height=.00028)
            cv.groove(sa - 0.050, 0.0, st * web, width=.0010, depth=.0005, dark=.25)
    # ---------------------------------------------------------------- grips
    gp = M(K['GRIP'])
    if gp.any():
        kz = cv.A('kz', 0.0); kphi = cv.A('kphi', 0.0)
        trim = np.where(gp > 0, 1.0, trim)
        stripe = (smoothstep(-0.0205, -0.0195, kz) * (1 - smoothstep(-0.0135, -0.0125, kz))) * gp
        trim = np.where(stripe > 0.5, 0.0, trim)
        cv.groove(kz + 0.0205, 0.0, gp, width=.0006, depth=.0004, dark=.3)
        cv.groove(kz + 0.0130, 0.0, gp, width=.0006, depth=.0004, dark=.3)
        cv.folds(kphi * 0.011 * 9, .0030, .00018, gp * (1 - stripe), 2)
    # ---------------------------------------------------------------- head covers: chunky knit + ribbed cuff + colour patterns
    cm = M(K['COVER'])
    if cm.any():
        kz = cv.A('kz', 0.0); kphi = cv.A('kphi', 0.0); pat = np.round(cv.A('kpat', 0.0))
        cz = COVER_CUFF_Z
        cuff = (kz < cz).astype(float)
        body = 1 - cuff
        rel, ci, ri = knit_relief(cv, cm * body, kphi, kz - cz, 14, 0.0120, 0.0015, width=0.17)
        top = smoothstep(COVER_TOP_Z - 0.010, COVER_TOP_Z, kz)           # fade the stitches out over the pole (columns converge there)
        cv.H -= 0.0015 * rel * cm * body * top
        cv.C = np.where((cm * body * top) > 0, 1 - (1 - cv.C) * (1 - top), cv.C)
        rel = rel * (1 - top)
        # cuff: vertical ribs + a fold line where it meets the body
        rib = np.sin(kphi * 12)
        cv.H += 0.00055 * rib * cm * cuff
        cv.C *= 1 - 0.10 * np.clip(-rib, 0, 1) * cm * cuff
        cv.groove(kz - cz, 0.0, cm, width=.0014, depth=.0010, dark=.45)
        hoops = np.maximum(smoothstep(0.005, 0.006, kz) * (1 - smoothstep(0.011, 0.012, kz)), smoothstep(0.019, 0.020, kz) * (1 - smoothstep(0.026, 0.027, kz)))
        hv = hash2(ci * 1.0, ri * 1.0)                                    # per-stitch value (a heathered yarn)
        blk = hash2(np.floor(ci / 2) + 7.0, np.floor(ri / 2) + 3.0)       # 2x2 blocks
        # pattern 0 (driver): navy knit with ~25 % white stitches, white cuff with two navy hoops
        p0 = np.where(body > 0, np.where(hv > 0.74, 0.0, 1.0), hoops)
        # pattern 1 (fairway): white knit with ~22 % navy stitches and a navy lower band, navy cuff with white hoops
        p1 = np.where(body > 0, np.clip(np.where(hv > 0.78, 1.0, 0.0) + smoothstep(cz + 0.028, cz + 0.014, kz), 0, 1), 1.0 - hoops)
        # pattern 2 (hybrid): 2x2 blocks navy / white, navy cuff
        p2c = np.where(body > 0, np.where(blk > 0.50, 1.0, 0.0), 1.0)
        # over the pole: the plain colour of the pattern
        p0 = np.where(top > 0.5, 1.0, p0); p1 = np.where(top > 0.5, 0.0, p1); p2c = np.where(top > 0.5, np.where(pat == 2, 0.0, p2c), p2c)
        t = np.where(pat == 0, p0, np.where(pat == 1, p1, p2c))
        trim = np.where(cm > 0, t, trim)
    # ---------------------------------------------------------------- feet: dark rubber
    trim = np.where(M(K['FOOT']) > 0, 1.0, trim)
    return np.clip(trim, 0, 1)
