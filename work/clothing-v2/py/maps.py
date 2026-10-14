"""Bake each garment's N (tangent-space normal, +Y up / OpenGL) and M (R AO visibility, G cavity, B trim mix, A fabric id / 9) maps from the
garment itself: stitch rows, seams, pockets, fly, pressed creases, placket, belt holes, shoe stitching / perforations / tread, soft fold wrinkles,
and ambient occlusion against the body and the other garments (fabric darkens where it meets the skin).
Blender -b --factory-startup --python maps.py -- <hero>"""
import sys, os, glob, math, ctypes, json
sys.path.insert(0, os.path.dirname(__file__))
import numpy as np
import cv2io, glib as G
from mathutils import Vector
from mathutils.bvhtree import BVHTree

ROOT = cv2io.ROOT
lib = ctypes.CDLL(os.path.join(ROOT, 'c', 'libraster.dylib'))
fp = ctypes.POINTER(ctypes.c_float); ip = ctypes.POINTER(ctypes.c_int); bp = ctypes.POINTER(ctypes.c_ubyte)
lib.raster.argtypes = [ctypes.c_int, fp, ctypes.c_int, ctypes.c_int, ip, fp]
lib.dilate.argtypes = [ctypes.c_int, ctypes.c_int, ctypes.c_int, fp, bp, ctypes.c_int]

KIND = {'shell': 0, 'collar': 1, 'placket': 2, 'button': 3, 'cord': 4, 'loop': 5, 'tongue': 6, 'lace': 7, 'eyestay': 8, 'sole': 9, 'nub': 10,
        'skirt': 11, 'band': 12, 'belt': 13, 'buckle': 14, 'tip': 15, 'pompom': 16, 'tab': 17, 'under': 18, 'upper': 19}


def smoothstep(a, b, x):
    t = np.clip((x - a) / (b - a), 0, 1); return t * t * (3 - 2 * t)


def gauss(d, w): return np.exp(-(d / w) ** 2)


class Canvas:
    """per-texel fields of one garment piece."""
    def __init__(self, g, res):
        self.g = g; self.res = res
        part = g['part']; tri = g['tri']
        sel = np.nonzero((part == 0) | (part == 3))[0]
        uv = g['uv'][tri[sel]].astype(np.float32).reshape(-1, 6)
        tid = np.full(res * res, -1, np.int32); bary = np.zeros(res * res * 3, np.float32)
        lib.raster(len(sel), uv.ctypes.data_as(fp), res, res, tid.ctypes.data_as(ip), bary.ctypes.data_as(fp))
        self.valid = tid >= 0
        self.face = sel[tid[self.valid]]
        self.V = tri[self.face]; self.B = bary.reshape(-1, 3)[self.valid].astype(np.float64)
        self.n = int(self.valid.sum())
        self.H = np.zeros(self.n); self.C = np.ones(self.n)
        S = float(g['S'])
        idx = np.nonzero(self.valid)[0]
        self.um = ((idx % res) + .5) / res * S; self.vm = ((idx // res) + .5) / res * S
        self.texel = S / res
        self.kind = g['kind'][self.face] if 'kind' in g.files else np.zeros(self.n, np.int32)
        self.fabric = g['fabric'][self.face]
        self.pos = self.I(g['pos'])

    def I(self, a):
        a = np.asarray(a)
        if a.ndim == 1: return (a[self.V] * self.B).sum(1)
        return (a[self.V] * self.B[:, :, None]).sum(1)

    def A(self, name, default=None):
        k = 'a_' + name
        if k not in self.g.files: return None if default is None else np.full(self.n, default)
        return self.I(self.g[k])

    # ---- construction marks
    def groove(self, d, at, mask, width=.0007, depth=.00045, dark=.28):
        w = gauss(d - at, width) * mask
        self.H -= depth * w; self.C *= 1 - dark * w

    def stitch(self, d, at, along, mask, pitch=.0035, width=.0005, height=.00022):
        line = gauss(d - at, width) * mask
        ph = (along / pitch) % 1.0
        dash = smoothstep(.0, .12, ph) * (1 - smoothstep(.62, .74, ph))
        self.H += height * line * dash
        self.C *= 1 - .22 * line * dash - .10 * line * (1 - dash)

    def ridge(self, d, at, mask, width=.0012, height=.0006):
        self.H += height * gauss(d - at, width) * mask
        self.C *= 1 - .05 * gauss(d - at, width * .6) * mask

    def folds(self, coord, wavelength, amp, mask, phase_seed=0, warp=None):
        rng = np.random.default_rng(phase_seed)
        ph = rng.uniform(0, 2 * math.pi, 4)
        c = coord / wavelength * 2 * math.pi
        if warp is not None: c = c + warp
        f = np.sin(c + ph[0]) * .6 + np.sin(c * 1.73 + ph[1]) * .3 + np.sin(c * .61 + ph[2]) * .25
        self.H += amp * f * mask
        self.C *= 1 - .06 * np.clip(-f, 0, None) * mask


def torso_frame(body):
    zs = np.array([body.head(b)[2] for b in ['Hips', 'Spine', 'Chest', 'UpperChest', 'Neck']])
    ys = np.array([body.head(b)[1] for b in ['Hips', 'Spine', 'Chest', 'UpperChest', 'Neck']])
    return zs, ys


TIP2 = [(.0035, .0085), (.0115, .0150)]


def details_polo2(cv, D):
    """construction marks of the panel-built polo (polo2.py): seams, turned hem, vents, rib cuffs + tipping, collar tipping, placket box, buttons."""
    k = cv.kind; p = cv.pos; aa = cv.texel * .7
    shell = (k == 0).astype(float); along = cv.um + cv.vm * .37
    ds = cv.A('d_seam', 1.0); dh = cv.A('d_hem', 1.0); dcu = cv.A('d_cuff', 1.0)
    cv.groove(ds, 0.0, shell, width=.0008, depth=.0005, dark=.30)
    for sgn in (.0035,): cv.stitch(ds, sgn, along, shell * (ds < .01))
    hw = D.get('hem_w', .026)
    cv.groove(dh, hw, shell, width=.0008, depth=.0004, dark=.22)
    for at in (.0185, .0215): cv.stitch(dh, at, along, shell * (dh < .03))
    # cuff: rib band, the join to the sleeve, tipping near the free edge
    cf = (k == 20).astype(float)
    cv.groove(dcu, D.get('cuff_w', .024), cf + shell * (dcu < .03), width=.0008, depth=.0005, dark=.25)
    trim = np.zeros(cv.n)
    plain = bool(D.get('plain'))
    for lo, hi in ([] if plain else TIP2): trim = np.maximum(trim, band(dcu, lo, hi, aa) * cf)
    # collar: tipping near the fall's free edge, a soft ridge at the roll line
    co = (k == 1).astype(float); ce = cv.A('collar_edge', 1.0)
    for lo, hi in ([] if plain else TIP2): trim = np.maximum(trim, band(ce, lo, hi, aa) * co)
    # placket: box stitch, the overlap edge, buttonholes
    pk = (k == 2).astype(float)
    if pk.any():
        px, pz = cv.A('pk_x', 0.0), cv.A('pk_z', 0.0); w = D.get('placket_w', .03)
        for s_ in (-1, 1): cv.stitch(px, s_ * (w / 2 - .0025), pz, pk * (pz > .003))
        cv.stitch(pz, .0035, px, pk * (np.abs(px) < w / 2 - .002))
        cv.groove(px, .0005, pk * (pz > .006), width=.0005, depth=.0006, dark=.35)
    bt = (k == 3).astype(float)
    if bt.any():
        # two holes per button (texel position relative to the button's own centre is unknown here: dark speckle at the dome centre)
        cv.C *= 1 - .25 * bt
    return np.clip(trim, 0, 1)


def details_shorts2(cv, D):
    """construction marks of the panel-built shorts (pants2.py): side / inseam / centre seams, turned hem with twin needle, waistband topstitching and
    elastic gather, slanted front pockets, back pocket welts, eyelets and cord."""
    k = cv.kind; p = cv.pos; aa = cv.texel * .7
    shell = (k == 0).astype(float); band = (k == 12).astype(float); along = cv.um + cv.vm * .37
    ds = cv.A('d_seam', 1.0); dh = cv.A('d_hem', 1.0); dt = cv.A('d_top', 1.0)
    cv.groove(ds, 0.0, shell, width=.0008, depth=.0005, dark=.30)
    for at in (.0035, .0075): cv.stitch(ds, at, along, shell * (ds < .012))
    hw = D.get('hem_w', .026)
    cv.groove(dh, hw, shell, width=.0008, depth=.0004, dark=.22)
    for at in (.0185, .0215): cv.stitch(dh, at, along, shell * (dh < .03))
    # waistband: topstitch rows, the join to the body of the shorts, elastic gather
    bw_ = D.get('band_h', .044)
    for at in (.0045, bw_ - .0045): cv.stitch(dt, at, along, band, pitch=.003)
    cv.groove(dt, bw_ - .0005, band + shell * (dt < bw_ + .004), width=.0008, depth=.0006, dark=.30)
    cv.folds(along, .011, .00016, band, 5)
    # slanted front pockets: the mouth (dark opening) with a stitched edge
    for nm in ('r', 'l'):
        f = cv.A('d_pocket_' + nm)
        if f is None: continue
        m = shell * (dt > bw_ - .002)
        cv.groove(f, 0.0, m, width=.0016, depth=.0012, dark=.55)
        cv.stitch(f, .0065, along, m * (f < .012))
        cv.stitch(f, .0105, along, m * (f < .014), pitch=.0035)
    # back pocket: two welts
    f = cv.A('d_bpocket')
    if f is not None:
        m = shell * (dt > bw_)
        cv.groove(f, 0.0, m, width=.0011, depth=.0009, dark=.50)
        cv.stitch(f, .0045, along, m * (f < .009))
    eye = (k == 14).astype(float)
    if eye.any(): cv.C *= 1 - .20 * eye
    return np.zeros(cv.n)


DETAILS = {'polo2': lambda cv: details_polo2(cv, dict(hem_w=.026, cuff_w=.024, placket_w=.030, plain='plain' in cv.g.files and int(cv.g['plain']) == 1)),
           'shorts2': lambda cv: details_shorts2(cv, dict(hem_w=.026, band_h=.044)),
           'shoe2': lambda cv: details_shoe2(cv, {}),
           'sock2': lambda cv: details_sock2(cv, {}),
           'tank2': lambda cv: details_tank2(cv, dict(hem_band=.030, trim_w=.0075)),
           'skirt2': lambda cv: details_skirt2(cv, dict(z_stitch=.905)),
           'trousers2': lambda cv: details_trousers2(cv, dict(band_h=.036)),
           'belt2': lambda cv: details_belt2(cv, {}),
           'glove2': lambda cv: details_glove2(cv, {})}


def details_shoe2(cv, D):
    """construction marks of the parametric sneaker (shoe2.py): panel zones (mesh vs overlay leather) decided PER TEXEL from smooth fields, stitched seams,
    side stripe trim, sole joint groove, outsole lugs / tread, lace weave, tongue + collar stitching."""
    k = cv.kind; p = cv.pos; aa = cv.texel * .7; along = cv.um + cv.vm * .37
    up = (k == 19).astype(float); tg = (k == 6).astype(float); lace = (k == 7).astype(float); roll = (k == 12).astype(float); sole = (k == 9).astype(float); tab = (k == 17).astype(float)
    d_rim = cv.A('d_rim', 1.0); f_toe = cv.A('f_toe', 1.0); f_counter = cv.A('f_counter', 1.0); f_eye = cv.A('f_eye', .05); f_stripe = cv.A('f_stripe', .05)
    for f in (f_toe, f_counter):
        m = up * (np.abs(f) < .012)
        cv.groove(f, 0.0, m, width=.0007, depth=.0005, dark=.32)
        cv.stitch(f, .0035, along, m); cv.stitch(f, -.0035, along, m)
    f_bump = cv.A('f_bump', .05)
    mb = up * (np.abs(f_bump) < .012)
    cv.groove(f_bump, 0.0, mb, width=.0008, depth=.0006, dark=.34); cv.stitch(f_bump, .0036, along, mb); cv.stitch(f_bump, -.0036, along, mb)
    m = up * (np.abs(f_eye) < .012) * (f_eye < .05)
    cv.groove(f_eye, 0.0, m, width=.0007, depth=.0005, dark=.30); cv.stitch(f_eye, -.0035, along, m)
    # collar roll / tongue / tab edge stitching
    cv.stitch(d_rim, .0035, along, up * (d_rim < .007)); cv.groove(d_rim, .0, up * (d_rim < .003), width=.0006, depth=.0003, dark=.2)
    tgy = cv.A('tg_edge', 1.0)
    cv.stitch(tgy, .09, along, tg * (tgy < .2), pitch=.0035)
    cv.folds(along, .012, .00045, roll + tg * .6, 3)                                    # padding
    # sole: the midsole / outsole joint, outsole lugs on the rim, herringbone tread on the bottom
    zos = cv.A('sole_zos', 0.0); pz = p[:, 2]; nz = cv.I(cv.g['nrm'])[:, 2]
    cv.groove(pz - zos, 0.0, sole, width=.0006, depth=.0007, dark=.42)
    rim_o = sole * (pz < zos - .0006) * (nz > -.55)
    tri_ = np.abs(((cv.um / .0095) % 1.0) * 2 - 1)
    h = ((pz - zos) / .0034 + tri_ * 1.1) % 1.0
    cv.H -= .00045 * smoothstep(.25, .0, h) * rim_o; cv.C *= 1 - .22 * smoothstep(.25, .0, h) * rim_o
    bot = sole * (nz < -.55)
    if cv.A('tr_sole', 0.0).max() < .5 or True:             # the outsole has real tread geometry (tennis) or cleats (golf): only a soft pattern on the bare sole bottom
        hb = ((p[:, 1] / .0075) + np.abs((p[:, 0] * 100 % 1.4) - .7) * 1.4) % 1.0
        cv.H -= .00020 * smoothstep(.3, .0, hb) * bot; cv.C *= 1 - .12 * smoothstep(.3, .0, hb) * bot
    # laces: a weave line
    cv.stitch(cv.vm + cv.um * 0, 0.0, cv.um, lace, pitch=.0016, width=.0004)
    # ---- trim colour (follows the picked shoe colour): two side stripes, heel chevron
    trim = np.zeros(cv.n)
    f_str = cv.A('f_stripe', .05); f_chv = cv.A('f_chev', .05); t_str = cv.A('tr_stripe', 0.0); t_chv = cv.A('tr_chev', 0.0)
    trim = np.maximum(trim, band(f_str, -1.0, .0070, aa) * up * (t_str > .5))
    trim = np.maximum(trim, band(f_chv, -1.0, .0034, aa) * up * (t_chv > .5))
    # perforations (golf): a hex dot field on the side panels between the heel counter, the toe cap and the eyestay
    if cv.A('perf', 0.0).max() > .5:
        L_ = cv.A('shoeL', .3); qy = cv.A('qy', 0.0); qz = cv.A('qz', 0.0); qx = cv.A('qx', 0.0)
        pitch = .0050
        u_ = qy * L_ / pitch; v_ = qz / (pitch * .87)
        u_ = u_ + .5 * (np.floor(v_) % 2)
        gx = (u_ % 1.0) - .5; gy = (v_ % 1.0 - .5) * .87
        dot = smoothstep(.34, .20, np.hypot(gx, gy * 1.15))
        pm = up * smoothstep(.20, .26, qy) * (1 - smoothstep(.60, .66, qy)) * smoothstep(.020, .030, qz) * (1 - smoothstep(.062, .074, qz)) * smoothstep(.025, .035, np.abs(qx)) \
            * smoothstep(.014, .020, d_rim) * smoothstep(.002, .008, f_eye if f_eye is not None else .05)
        cv.H -= .00060 * dot * pm; cv.C *= 1 - .55 * dot * pm
    if cv.A('tr_sole', 0.0).max() > .5:
        trim = np.maximum(trim, sole * (pz < zos - .0006) * (cv.A('tr_sole', 0.0) > .5))
        trim = np.maximum(trim, (k == 10).astype(float))
    return np.clip(trim, 0, 1)


def details_sock2(cv, D):
    """knit sock: rib cuff with a stitched join, soft ankle wrinkles, a pom."""
    k = cv.kind; along = cv.um + cv.vm * .37
    sk = (k == 0).astype(float); band = (k == 12).astype(float); pom = (k == 16).astype(float)
    f_top = cv.A('f_top', 1.0); f_cuff = cv.A('f_cuff', 1.0); qz = cv.A('q_z', 0.0)
    cv.groove(f_cuff, 0.0, band + sk * (np.abs(f_cuff) < .01), width=.0008, depth=.0006, dark=.28)
    cv.stitch(f_cuff, .004, along, sk * (f_cuff > -.01) * (f_cuff < .01))
    cv.groove(f_top, 0.0, band, width=.0007, depth=.0005, dark=.2)
    cv.folds(qz, .018, .00055, sk * smoothstep(.20, .12, qz), 4)
    cv.C *= 1 - .15 * pom
    return np.zeros(cv.n)


def details_tank2(cv, D):
    """racerback V-neck tank: seams, rib hem band, navy binding (trim channel) at the neck + armholes with topstitching."""
    k = cv.kind; aa = cv.texel * .7; along = cv.um + cv.vm * .37
    shell = (k == 0).astype(float); band = (k == 12).astype(float)
    ds = cv.A('d_seam', 1.0); dh = cv.A('d_hem', 1.0); db = cv.A('d_bind', 1.0)
    cv.groove(ds, 0.0, shell, width=.0008, depth=.0005, dark=.28)
    cv.stitch(ds, .0035, along, shell * (ds < .008))
    hw = D.get('hem_band', .030)
    cv.groove(dh, hw - .0005, band + shell * (dh < hw + .004), width=.0008, depth=.0006, dark=.28)
    cv.stitch(dh, hw + .0035, along, shell * (dh < hw + .008))
    bw = D.get('trim_w', .0075)
    trim = np.maximum(band_fn(db, -1.0, bw, aa) * shell, (k == 15).astype(float))
    cv.groove(db, bw, shell * (db < bw + .004), width=.0006, depth=.0004, dark=.25)
    cv.stitch(db, bw * .5, along, shell * (db < bw), pitch=.003)
    cv.folds(along, .012, .0003, band, 6)
    return np.clip(trim, 0, 1)


def details_skirt2(cv, D):
    """pleated skirt + rib waistband + the tight under-shorts (kind 18): pleat folds (cavity), stitched yoke, hem stitching, waistband rows, seams."""
    k = cv.kind; p = cv.pos; aa = cv.texel * .7; along = cv.um + cv.vm * .37
    sk = (k == 11).astype(float); wb = (k == 12).astype(float); un = (k == 18).astype(float)
    pu = cv.A('pl_u', 0.0); pd = cv.A('pl_depth', 0.0); dh = cv.A('d_hem', 1.0); dt = cv.A('d_top', 1.0)
    # the recessed underfold of every pleat is darker (cavity) and its edges are crisp
    fold = smoothstep(.76, .80, pu) * (1 - smoothstep(.93, .99, pu))
    cv.C *= 1 - .42 * fold * sk * smoothstep(.0, .35, pd)
    cv.groove(pu, .80, sk * smoothstep(.0, .2, pd), width=.012, depth=.0004, dark=.18)
    # yoke: pleats stitched flat down to the hip line, a topstitch row there
    yz = p[:, 2] - D.get('z_stitch', .905)
    cv.stitch(yz, 0.0, along, sk, pitch=.003)
    cv.groove(dh, .0, sk, width=.0007, depth=.0004, dark=.2)
    cv.stitch(dh, .016, along, sk * (dh < .03), pitch=.0032)
    # waistband: rib with two stitch rows and a join line
    cv.stitch(dt, .006, along, wb, pitch=.003); cv.stitch(dt, .028, along, wb, pitch=.003)
    cv.groove(dt, .0368, wb + sk * (dt < .045) * 0, width=.0007, depth=.0005, dark=.25)
    cv.folds(along, .010, .00028, wb, 4)
    # under-shorts: hem rib edge + seams
    uds = cv.A('d_seam', 1.0)
    cv.groove(uds, 0.0, un, width=.0007, depth=.0004, dark=.25); cv.stitch(uds, .0035, along, un * (uds < .007))
    cv.groove(dh, .016, un * (dh > 0) * (dh < .04), width=.0007, depth=.0004, dark=.22)
    return np.zeros(cv.n)


def details_trousers2(cv, D):
    """tailored flat-front trousers: seams, hem, waistband + belt loops, slant pockets, welt back pockets, pressed crease."""
    k = cv.kind; aa = cv.texel * .7; along = cv.um + cv.vm * .37
    shell = (k == 0).astype(float); band = (k == 12).astype(float); loop = (k == 5).astype(float)
    ds = cv.A('d_seam', 1.0); dh = cv.A('d_hem', 1.0); dt = cv.A('d_top', 1.0); dcr = cv.A('d_crease', 1.0)
    cv.groove(ds, 0.0, shell, width=.0008, depth=.0005, dark=.30)
    for at in (.0035,): cv.stitch(ds, at, along, shell * (ds < .008))
    cv.groove(dh, .028, shell, width=.0008, depth=.0004, dark=.22)
    cv.stitch(dh, .026, along, shell * (dh < .04), pitch=.0035)
    bw_ = D.get('band_h', .036)
    for at in (.0045, bw_ - .0045): cv.stitch(dt, at, along, band, pitch=.003)
    cv.groove(dt, bw_ - .0005, band + shell * (dt < bw_ + .004), width=.0008, depth=.0006, dark=.30)
    cv.stitch(cv.vm * 0 + cv.um, 0.0, cv.vm, loop, pitch=.003, width=.0004)
    for nm in ('r', 'l'):
        f = cv.A('d_pocket_' + nm)
        if f is None: continue
        m = shell * (dt > bw_ - .002)
        cv.groove(f, 0.0, m, width=.0016, depth=.0012, dark=.55); cv.stitch(f, .0065, along, m * (f < .012))
    f = cv.A('d_bpocket')
    if f is not None:
        m = shell * (dt > bw_); cv.groove(f, 0.0, m, width=.0011, depth=.0009, dark=.50); cv.stitch(f, .0045, along, m * (f < .009))
    # pressed crease: a crisp line, light on one side, soft shadow on the other
    cr = shell * (dcr < .02) * (dt > bw_ + .01)
    cv.groove(dcr, 0.0, cr, width=.0010, depth=.0004, dark=.14)
    cv.C *= 1 - .06 * gauss(dcr - .004, .004) * cr
    return np.zeros(cv.n)


def details_belt2(cv, D):
    """leather belt: edge stitching on both sides, belt holes on the tail, a soft crease where it passes the keeper; the buckle (kind 14) stays clean."""
    k = cv.kind; p = cv.pos; along = cv.um + cv.vm * .37
    lea = ((k == 13) | (k == 5)).astype(float)
    qz = p[:, 2]
    # distance to the strap edges: the loft's v coordinate is z; use the face's own extent per part
    zc = np.median(qz[lea > 0]) if lea.any() else 0
    hw = (qz[lea > 0].max() - qz[lea > 0].min()) / 2 if lea.any() else .015
    de = hw - np.abs(qz - zc)
    cv.groove(de, 0.0, lea * (de < .006), width=.0007, depth=.0004, dark=.28)
    cv.stitch(de, .0042, along, lea * (de < .008), pitch=.0032)
    tl = cv.A('tl_u', -1.0)
    hole = np.zeros(cv.n)
    for i in range(5):
        u0 = .030 + i * .018
        hole = np.maximum(hole, gauss(np.hypot(tl - u0, qz - zc), .0030))
    hole *= (tl >= 0)
    cv.H -= .0008 * hole; cv.C *= 1 - .55 * hole
    return np.zeros(cv.n)


def details_glove2(cv, D):
    """leather golf glove: perforations on the back of the hand, finger-side seams, wrist strap with a join line + stitching, fine leather folds."""
    k = cv.kind; p = cv.pos; aa = cv.texel * .7; along = cv.um + cv.vm * .37
    sh = (k == 0).astype(float); sp = (k == 12).astype(float)
    dors = cv.A('dorsal', 0.0); fid = cv.A('fid', -1.0); fs = cv.A('f_strap', 1.0); tw = cv.A('t_wrist', 0.0)
    # perforations: a dot grid in texture space on the dorsal side, past the strap, fading before the finger tips
    pitch = .0032
    gx = (cv.um / pitch) % 1.0 - .5; gy = (cv.vm / pitch) % 1.0 - .5
    dot = smoothstep(.30, .16, np.hypot(gx, gy))
    pmask = sh * smoothstep(.45, .8, dors) * smoothstep(.0, .012, fs) * (fid < 0)
    cv.H -= .0005 * dot * pmask; cv.C *= 1 - .5 * dot * pmask
    # the strap
    cv.groove(fs, 0.0, sh * (np.abs(fs) < .006) + sp * (np.abs(fs) < .006), width=.0009, depth=.0007, dark=.35)
    cv.stitch(fs, .006, along, sh * (fs < .012) * (fs > 0), pitch=.0032)
    cv.stitch(fs, -.0045, along, sp * (fs > -.012), pitch=.0032)
    # soft leather folds at the knuckles / palm creases
    cv.folds(cv.um + cv.vm * 1.7, .010, .00035, sh, 3)
    return np.zeros(cv.n)


def details(cv, piece, hero, body, D):
    g = cv.g; k = cv.kind; p = cv.pos
    along = cv.um + cv.vm * .37
    shell = (k == 0).astype(float)
    zs, ys = torso_frame(body)
    yc = np.interp(p[:, 2], zs, ys)
    th = np.arctan2(p[:, 0], -(p[:, 1] - yc)); r = np.hypot(p[:, 0], p[:, 1] - yc)
    if piece == 'Kit_Top':
        P = D.get('top', {})
        side = cv.A('side', 0.0)
        torso = shell * (np.abs(side) < .5)
        fh = cv.A('f_hem')
        if fh is not None:
            if P.get('builder') == 'tops.build_tank':
                cv.groove(fh, P.get('hem_band', .024), shell)
            else:
                cv.groove(fh, .0215, shell); cv.stitch(fh, .0185, cv.um, shell)
        for sd in (-1, 1):
            f = cv.A('f_sl%d' % sd)
            if f is None: continue
            arm = shell * (np.abs(side - sd) < .5)
            if P.get('rib_cuff', True) and P.get('builder') != 'tops.build_tank':
                cv.groove(f, P.get('cuff', .025), arm)
            elif P.get('builder') != 'tops.build_tank':
                cv.groove(f, P.get('cuff', .022), arm); cv.stitch(f, P.get('cuff', .022) - .0035, along, arm)
        # side seams (hem to armpit)
        z_arm = P.get('z_chest', 1.2)
        for s_ in (-1, 1):
            d = np.abs(np.angle(np.exp(1j * (th - s_ * math.pi / 2)))) * r
            cv.groove(d, 0.0, torso * (p[:, 2] < z_arm), width=.0008)
        # shoulder seams (set-in polos): along the top of each shoulder
        if P.get('builder') == 'tops.build_polo' and not P.get('raglan'):
            top = shell * (p[:, 2] > P['z_neck_front'] - .07) * (np.abs(p[:, 0]) > .06)
            cv.groove(p[:, 1] - (body.head('Neck')[1] + .005), 0.0, top, width=.0008)
            # armhole seam: plane across each arm near the shoulder point, only on the sleeve tube itself (close to the arm axis)
            for sd, nm in ((-1, 'Left'), (1, 'Right')):
                sh = body.head(nm + 'UpperArm'); el = body.head(nm + 'LowerArm'); ax = (el - sh) / np.linalg.norm(el - sh)
                t = (p - sh) @ ax
                rad = np.linalg.norm((p - sh) - np.outer(t, ax), axis=1)
                cv.groove(t, P.get('armhole_t', .03), shell * (np.sign(p[:, 0]) == sd) * (rad < .085), width=.0008)
        for key in ('raglan_d-1-1', 'raglan_d-11', 'raglan_d1-1', 'raglan_d11'):
            d = cv.A(key)
            if d is None: continue
            m = shell * (d < .5)
            cv.groove(d, 0.0, m, width=.0008); cv.stitch(d, .0038, along, m)
        # soft drape: vertical folds below the chest on untucked tops, blousing over the waistband on tucked ones
        if P.get('tuck_z'):
            tz = P['tuck_z']
            m = torso * gauss(p[:, 2] - (tz + .03), .035)
            cv.folds(cv.um, .045, .0018, m, 3, warp=np.sin(p[:, 2] * 60) * .8)
        else:
            m = torso * (1 - smoothstep(P.get('z_hem', .9) + .05, P.get('z_hem', .9) + .22, p[:, 2]))
            cv.folds(cv.um, .065, .0011, m, 5, warp=np.sin(p[:, 2] * 25) * .6)
        # armpit drag lines (front and back of each armpit)
        for sd, nm in ((-1, 'Left'), (1, 'Right')):
            sh = body.head(nm + 'UpperArm')
            a = np.array([sd * (abs(sh[0]) - .05), sh[1], sh[2] - .09])
            d = p - a; dist = np.linalg.norm(d, axis=1)
            m = shell * gauss(dist, .06) * (np.abs(side) < .9)
            dirp = np.array([0, 0, 1.]) * .7 + np.array([-sd, 0, 0.]) * .7
            cv.folds(d @ dirp, .022, .0009, m, 11 + sd)
        # placket
        pk = (k == KIND['placket']).astype(float)
        if pk.any():
            px, pz = cv.A('pk_x', 0.0), cv.A('pk_z', 0.0)
            w = P.get('placket_w', .03)
            for s_ in (-1, 1): cv.stitch(px, s_ * (w / 2 - .0025), pz, pk * (pz > .003))
            cv.stitch(pz, .0035, px, pk * (np.abs(px) < w / 2 - .002))
            cv.groove(px, .0035, pk * (pz > .006), width=.0006, depth=.0006, dark=.35)   # the overlap edge
        col = (k == KIND['collar']).astype(float)
        if col.any():
            pv = cv.A('pv', 0.0)
            hs = P.get('stand_back', .03)
            cv.ridge(pv, hs + .004, col, width=.0018, height=.0004)       # the rolled fold
    elif piece == 'Kit_Bottom':
        P = D.get('bottom', {})
        pants = shell + (k == KIND['under']).astype(float)
        fw = cv.A('f_waist')
        U = P.get('under', P)
        if fw is not None:
            band = U.get('band', .038)
            cv.groove(fw, band, pants)
            if P.get('drawcord'):
                for at in (.008, .019, .030): cv.stitch(fw, at, cv.um, pants * (fw < band))
                cv.folds(cv.um, .009, .00035, pants * (fw < band), 7)            # gathered elastic
            else:
                cv.stitch(fw, band - .003, cv.um, pants); cv.stitch(fw, .004, cv.um, pants * (fw < band))
        for sd in (-1, 1):
            f = cv.A('f_leg%d' % sd)
            if f is None: continue
            leg = pants * (np.abs(cv.A('legside', 0.0) - sd) < .5)
            hem = U.get('hem', .025)
            cv.groove(f, hem, leg)
            if not U.get('hem_z_front'): cv.stitch(f, hem - .004, cv.um, leg)
        # outseams / inseams / crotch seam from the leg frames
        for sd, nm in ((-1, 'Left'), (1, 'Right')):
            hip = body.head(nm + 'UpperLeg'); an = body.head(nm + 'Foot'); ax = (an - hip) / np.linalg.norm(an - hip)
            out = np.array([sd, 0, 0.]); out -= ax * (out @ ax); out /= np.linalg.norm(out); fwd = np.cross(ax, out)
            q = p - hip; q -= np.outer(q @ ax, ax); rr = np.linalg.norm(q, axis=1)
            a = np.arctan2(q @ fwd, q @ out)
            leg = pants * (np.sign(p[:, 0]) == sd) * (p[:, 2] < hip[2] + .02)
            cv.groove(np.abs(a) * rr, 0.0, leg, width=.0008)                          # outseam
            if P.get('crease'):
                front = -fwd if (fwd @ np.array([0, -1, 0.])) < 0 else fwd
                af = np.arctan2(q @ np.cross(ax, front), q @ front)
                cv.ridge(np.abs(af) * rr, 0.0, leg * (p[:, 2] < hip[2] - .06), width=.0011, height=.0007)
                cv.ridge(np.abs(np.angle(np.exp(1j * (af - math.pi)))) * rr, 0.0, leg * (p[:, 2] < hip[2] - .2), width=.0011, height=.0005)
        crotch = pants * (np.abs(cv.A('legside', 0.0)) < .5)
        cv.groove(np.abs(p[:, 0]), 0.0, crotch, width=.0008)
        if P.get('fly'):
            fly = shell * (p[:, 1] < yc) * (p[:, 2] > P['z_waist_front'] - .17) * (p[:, 2] < P['z_waist_front'] - P.get('band', .04))
            zb = P['z_waist_front'] - .16
            dj = np.where(p[:, 2] > zb + .03, np.abs(p[:, 0] + .03), np.hypot(p[:, 0] + .0, p[:, 2] - (zb + .03)) - .03)
            cv.stitch(dj, 0.0, p[:, 2] + np.abs(p[:, 0]), fly * (p[:, 0] < .002))
        if P.get('pockets'):
            for sd in (-1, 1):
                a = np.array([sd * .105, P['z_waist_front'] - P.get('band', .04)]); b = np.array([sd * .165, P['z_waist_front'] - .17])
                q2 = np.stack([p[:, 0], p[:, 2]], 1); ab = b - a; t = np.clip(((q2 - a) @ ab) / (ab @ ab), 0, 1)
                d = np.linalg.norm(q2 - (a + np.outer(t, ab)), axis=1)
                front = shell * (p[:, 1] < yc + .02) * (np.sign(p[:, 0]) == sd) * (p[:, 2] > b[1] - .01)
                cv.groove(d, 0.0, front, width=.0009, depth=.0008, dark=.4); cv.stitch(d, .007, p[:, 2] + p[:, 0], front)
            if P.get('back_pockets'):
                for sd in (-1, 1) if P['back_pockets'] == 2 else (1,):
                    cz = P['z_waist_back'] - .085; cx = sd * .075
                    back = shell * (p[:, 1] > yc) * (np.abs(p[:, 0] - cx) < .065)
                    for dz in (-.006, .006): cv.groove(p[:, 2] - (cz + dz), 0.0, back, width=.0006, depth=.0006, dark=.35)
        if P.get('crease'):      # ankle break over the shoe
            for sd in (-1, 1):
                f = cv.A('f_leg%d' % sd)
                if f is None: continue
                leg = shell * (np.abs(cv.A('legside', 0.0) - sd) < .5) * (p[:, 1] < -.0)
                cv.folds(f, .038, .0016, leg * (1 - smoothstep(.02, .12, f)), 21 + sd)
        else:
            # radial folds from the crotch, front and back
            zc = P.get('z_crotch', .76)
            d = np.hypot(p[:, 0], p[:, 2] - zc)
            m = pants * gauss(d - .06, .05) * (np.abs(p[:, 0]) < .12)
            cv.folds(np.arctan2(p[:, 2] - zc, p[:, 0]) * .08, .02, .0007, m, 31)
        sk = (k == KIND['skirt']).astype(float)
        if sk.any():
            fh = cv.A('f_hem', 1.0)
            cv.stitch(fh, .012, cv.A('pu', 0.0), sk)
        bd = (k == KIND['band']).astype(float)
        if bd.any() and P.get('band', {}).get('fabric', 9) != 9:
            bv = cv.A('band_v', .5)
            for at in (.12, .88): cv.stitch(bv * .042, at * .042, cv.A('pu', 0.0), bd)
    elif piece.startswith('Kit_Shoe'):
        P = D.get('shoes', {})
        up = (k == KIND['upper']).astype(float)
        L = float(np.median(g['a_qL'])) if 'a_qL' in g.files else .28
        qx, qy, qz = cv.A('qx', 0.0), cv.A('qy', 0.0), cv.A('qz', 0.0)
        ft, fc = cv.A('f_toecap', 1.0), cv.A('f_counter', 1.0)
        cv.groove(ft, 0.0, up, width=.0007); cv.stitch(ft, .0028, along, up * (ft > 0))
        cv.groove(fc, 0.0, up, width=.0007); cv.stitch(fc, .0028, along, up * (fc > 0))
        fa = cv.A('f_accent')
        if P.get('accent') and fa is not None:
            for s_ in (-1, 1):
                cv.stitch(fa, s_ * (P['accent_w'] / 2 - .0025), along, up * (np.abs(fa) < P['accent_w'] / 2))
        dc = cv.A('d_collar', 1.0)
        cv.groove(dc, .014, up, width=.0009); cv.stitch(dc, .017, along, up)
        fth = cv.A('f_throat', 1.0)
        cv.stitch(fth, .004, along, up * (fth < .02)); cv.stitch(fth, .0075, along, up * (fth < .02))
        # perforations on the toe box
        hole_r = .0006; sp = .0045
        gx = (qx / sp) % 1.0 - .5; gy = ((qy + .5 * sp * (np.floor(qx / sp) % 2)) / sp) % 1.0 - .5
        dd = np.hypot(gx, gy) * sp
        holes = up * (ft > .008) * (qz > P.get('insole_z', 0) + .018) * smoothstep(hole_r * 1.6, hole_r * .8, dd)
        cv.H -= .0005 * holes; cv.C *= 1 - .45 * holes
        if not P.get('accent'):   # golf: a saddle panel over the midfoot
            for at in (.44, .58):
                d = qy - at * L
                cv.groove(d, 0.0, up * (qz > .02), width=.0007); cv.stitch(d, .0028 if at < .5 else -.0028, along, up * (qz > .02))
        so = (k == KIND['sole']).astype(float)
        if so.any():
            # midsole sidewall line and the outsole tread on the underside
            nrm = cv.I(g['nrm']); bottom = so * (nrm[:, 2] < -.6)
            side = so * (np.abs(nrm[:, 2]) < .6)
            zt = P.get('sole_bottom', -.03) + P.get('outsole', .004) + .011
            cv.groove(p[:, 2], zt, side, width=.0008, depth=.0005)
            if not P.get('nubs'):
                band = np.floor(p[:, 1] / .009)
                hb = np.abs(((p[:, 0] * (1 - 2 * (band % 2)) + p[:, 1]) / .0045) % 1.0 - .5) * 2
                cv.H += .0006 * (1 - hb) * bottom; cv.C *= 1 - .3 * hb * bottom
    elif piece.startswith('Kit_Sock'):
        P = D.get('socks', {})
        f = cv.A('f_top')
        if f is not None: cv.groove(f, P.get('cuff', .02), shell, width=.0008)
        pm = (k == KIND['pompom']).astype(float)
        if pm.any():
            n = np.sin(p[:, 0] * 2900) * np.sin(p[:, 1] * 3100) * np.sin(p[:, 2] * 2700)
            cv.H += .0004 * n * pm; cv.C *= 1 - .15 * np.clip(-n, 0, 1) * pm
    elif piece == 'Kit_Belt':
        bt = (k == KIND['belt']).astype(float); tp = (k == KIND['tip']).astype(float)
        bv = cv.A('band_v', .5)
        for at in (.12, .88): cv.stitch(bv, at, cv.A('pu', 0.0), bt, pitch=.004, width=.0006 / .03)
        zc = (p[:, 2] * tp).sum() / max(tp.sum(), 1)
        bh = cv.A('belt_hole', 0.0)
        for at in (.03, .05):
            d = np.hypot(bh - at, p[:, 2] - zc)
            hole = tp * smoothstep(.0021, .0014, d)
            cv.H -= .0009 * hole; cv.C *= 1 - .65 * hole
        cv.stitch(np.abs(p[:, 2] - zc), D['belt']['width'] / 2 - .004, bh, tp, pitch=.004)
    elif piece.startswith('Kit_Glove'):
        f = cv.A('f_cuff')
        if f is not None: cv.groove(f, .012, shell)
        tb = (k == KIND['tab']).astype(float)
        cv.C *= 1 - .1 * tb


def band_fn(d, lo, hi, aa):
    return smoothstep(lo - aa, lo + aa, d) * (1 - smoothstep(hi - aa, hi + aa, d))


def band(d, lo, hi, aa):
    """1 inside [lo, hi] of a distance field, anti-aliased over aa metres."""
    return smoothstep(lo - aa, lo + aa, d) * (1 - smoothstep(hi - aa, hi + aa, d))


TIP = [(.0035, .0085), (.0115, .0150)]          # tipping stripes, metres from the edge (wider outer stripe, then a thinner one)


def trims(cv, piece, D, vertex_trim):
    """trim mask per texel from smooth fields (stripes and panels narrower than the mesh spacing stay crisp)."""
    k = cv.kind; aa = cv.texel * .7
    T = vertex_trim.copy()
    if piece == 'Kit_Top':
        P = D.get('top', {})
        shell = (k == 0)
        if P.get('builder') == 'tops.build_tank':
            d = cv.A('d_edge', 1.0)
            T = np.where(shell, 1 - smoothstep(P['binding'] - aa, P['binding'] + aa, d), T)
        elif P.get('tipping'):
            side = cv.A('side', 0.0); t = np.zeros(cv.n)
            for sd in (-1, 1):
                f = cv.A('f_sl%d' % sd)
                if f is None: continue
                on = shell & (np.abs(side - sd) < .5)
                for lo, hi in TIP: t = np.maximum(t, band(f, lo, hi, aa) * on)
            col = (k == KIND['collar'])
            e = cv.A('collar_edge', 1.0)
            for lo, hi in TIP: t = np.maximum(t, band(e, lo, hi, aa) * col)
            T = np.where(shell | col, t, T)
    elif piece.startswith('Kit_Shoe'):
        P = D.get('shoes', {})
        up = (k == KIND['upper'])
        qx, qy, qz = cv.A('qx', 0.0), cv.A('qy', 0.0), cv.A('qz', 0.0)
        t = np.zeros(cv.n)
        z0 = P.get('insole_z', 0)
        if P.get('accent'):
            fa = cv.A('f_accent', 1.0)
            w = P['accent_w']
            side = smoothstep(P['eyestay_w'] * .6, P['eyestay_w'] * 1.2, np.abs(qx))
            height = smoothstep(z0 + .004, z0 + .009, qz) * (1 - smoothstep(P['collar_side'] - .012, P['collar_side'] - .006, qz))
            t = np.maximum(t, band(fa, -w / 2, w / 2, aa) * side * height)
        if P.get('counter_trim'):
            fc = cv.A('f_counter', -1.0)
            t = np.maximum(t, smoothstep(-aa, aa, fc) * smoothstep(z0 + .006, z0 + .01, qz))
        if P.get('eyestay_trim'):
            t = np.maximum(t, (cv.A('eyestay', 0.0) > .5).astype(float))
        T = np.where(up, t, T)
    return np.clip(T, 0, 1)


def ambient_occlusion(hero_id, body, pieces, rays=20, dist=.05, only=None):
    """per-vertex AO of every piece against the body + every other piece (rest pose)."""
    allp = [body.pos]; allt = [body.tri]; off = len(body.pos)
    cf = os.path.join(ROOT, 'out', hero_id, 'unity', 'cover.npz')
    if os.path.exists(cf):
        cvz = np.load(cf); hid = np.zeros(len(body.pos), bool)
        for k in cvz.files:
            if k != 'Kit_Glove_R': hid |= cvz[k]
        allt = [body.tri[~hid[body.tri].all(1)]]
    for name, g in pieces.items():
        allp.append(g['pos']); allt.append(g['tri'][(g['part'] == 0) | (g['part'] == 3)] + off); off += len(g['pos'])
    P = np.concatenate(allp); T = np.concatenate(allt)
    tree = BVHTree.FromPolygons([tuple(x) for x in P], [tuple(t) for t in T], all_triangles=True)
    rng = np.random.default_rng(1)
    # cosine-weighted hemisphere directions (z up), rotated per vertex
    u1 = rng.random(rays); u2 = rng.random(rays)
    dirs = np.stack([np.sqrt(u1) * np.cos(2 * math.pi * u2), np.sqrt(u1) * np.sin(2 * math.pi * u2), np.sqrt(1 - u1)], 1)
    out = {}
    for name, g in pieces.items():
        if only and name not in only: continue
        pos = g['pos']; nrm = g['nrm']
        ao = np.ones(len(pos))
        for i in range(len(pos)):
            n = nrm[i]
            a = np.cross(n, [0, 0, 1.] if abs(n[2]) < .9 else [1, 0, 0.]); a /= np.linalg.norm(a); b = np.cross(n, a)
            o = pos[i] + n * .0006
            hit = 0.0
            for d in dirs:
                w = a * d[0] + b * d[1] + n * d[2]
                r = tree.ray_cast(Vector(o), Vector(w), dist)
                if r[0] is not None: hit += 1 - (r[3] / dist) ** .5
            ao[i] = 1 - hit / rays
        # 20 rays per vertex are noisy and the garments' coarse triangles show it as blotches: smooth over the cloth (positions are split at UV seams, so weld by position first)
        key = {}; wid = np.zeros(len(pos), np.int64)
        for i, q in enumerate(np.round(pos * 20000).astype(np.int64)): wid[i] = key.setdefault((q[0], q[1], q[2]), len(key))
        nw = len(key); wt = np.array([wid[t] for t in g['tri']]).reshape(-1, 3)
        acc = np.zeros(nw); cnt = np.zeros(nw); np.add.at(acc, wid, ao); np.add.at(cnt, wid, 1); aw = acc / np.maximum(cnt, 1)
        nbw = G.neighbours(nw, wt)
        for _ in range(int(os.environ.get('CV2_AO_SMOOTH', '4'))): aw = aw + .5 * G.umbrella(aw[:, None], nbw)[:, 0]
        out[name] = aw[wid]
    return out


def bake(hero_id):
    import designs
    hero = cv2io.Hero(hero_id); body = G.Body(hero)
    D = designs.design(hero_id)
    udir = os.path.join(ROOT, 'out', hero_id, 'unity')
    pieces = {os.path.basename(f)[:-10]: dict(np.load(f)) for f in sorted(glob.glob(os.path.join(udir, 'Kit_*_final.npz')))}
    only = set(os.environ['CV2_PIECES'].split(',')) if os.environ.get('CV2_PIECES') else None
    aos = ambient_occlusion(hero_id, body, pieces, only=only)
    from PIL_free import write_png
    report = {}
    for name, g0 in pieces.items():
        if only and name not in only: continue
        g = np.load(os.path.join(udir, name + '_final.npz'))
        res = int(g['res'])
        cv = Canvas(g, res)
        polo2_trim = None
        dname = str(g['detail']) if 'detail' in g.files else 'polo2' if 'polo2' in g.files else None
        if dname: polo2_trim = DETAILS[dname](cv)
        else: details(cv, name, hero, body, D)
        ao = cv.I(aos[name]) if name in aos else np.ones(cv.n)
        trim = polo2_trim if polo2_trim is not None else trims(cv, name, D, np.clip(cv.A('trim', 0.0), 0, 1))
        cav = np.clip(cv.C * np.clip(cv.A('cavity', 1.0), 0, 1), .45, 1)
        fab = cv.fabric.astype(np.float64)
        # normal from height (finite differences in texel space)
        Himg = np.zeros(res * res); Himg[cv.valid] = cv.H
        mask = cv.valid.astype(np.uint8).copy()
        Hf = Himg.astype(np.float32).reshape(res, res).copy()
        lib.dilate(res, res, 1, Hf.ctypes.data_as(fp), mask.reshape(-1).ctypes.data_as(bp), 6)
        Hd = Hf.astype(np.float64)
        dx = (np.roll(Hd, -1, 1) - np.roll(Hd, 1, 1)) / (2 * cv.texel)
        dy = (np.roll(Hd, -1, 0) - np.roll(Hd, 1, 0)) / (2 * cv.texel)
        nz = 1.0 / np.sqrt(1 + dx * dx + dy * dy)
        N = np.stack([-dx * nz, -dy * nz, nz], -1).reshape(-1, 3)
        M = np.zeros((res * res, 4)); M[:, :] = (1, 1, 0, 0)
        M[cv.valid, 0] = np.clip(.62 + .38 * ao, .7, 1) if False else np.clip(ao, 0, 1)
        M[cv.valid, 1] = cav; M[cv.valid, 2] = trim; M[cv.valid, 3] = fab / 9.0
        # fill the gutters from the islands (mips never pull in empty texels)
        m2 = cv.valid.astype(np.uint8).copy(); Mf = M.astype(np.float32).copy()
        lib.dilate(res, res, 4, Mf.ctypes.data_as(fp), m2.ctypes.data_as(bp), 24)
        m3 = cv.valid.astype(np.uint8).copy(); Nf = N.astype(np.float32).copy()
        lib.dilate(res, res, 3, Nf.ctypes.data_as(fp), m3.ctypes.data_as(bp), 24)
        Nf[m3 == 0] = (0, 0, 1)
        Mf[m2 == 0] = (1, 1, 0, Mf[cv.valid, 3].mean() if cv.n else 0)
        # rows: PNG row 0 = top = v 1
        n8 = np.clip((Nf.reshape(res, res, 3)[::-1] * .5 + .5) * 255 + .5, 0, 255).astype(np.uint8)
        m8 = np.clip(Mf.reshape(res, res, 4)[::-1] * 255 + .5, 0, 255).astype(np.uint8)
        tag = name[4:].replace('_', '')
        write_png(os.path.join(udir, f'{tag}_N.png'), n8)
        write_png(os.path.join(udir, f'{tag}_M.png'), m8)
        cov = cv.n / (res * res)
        report[name] = dict(res=res, coverage=round(cov, 3), metres_per_uv=float(g['S']), px_per_cm=round(res / float(g['S']) / 100, 2),
                            ao_min=round(float(ao.min()), 3) if cv.n else 1, ao_mean=round(float(ao.mean()), 3) if cv.n else 1)
        print(name, report[name])
    json.dump(report, open(os.path.join(udir, 'maps_report.json'), 'w'), indent=1)


if __name__ == '__main__' or '--' in sys.argv:
    bake(sys.argv[sys.argv.index('--') + 1])
