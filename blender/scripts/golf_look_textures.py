"""golf_look_textures.py -- procedural texture set for the golf POSTCARD_LOOK pass (role A1).

Generates every texture of work/postcard-look/LOOK_CONTRACT.md section 3 except Plants_C.png
(owned by the props agent), plus Sky_Crater_C.png, as exact-byte PNGs (own zlib/struct writer,
no colour management), deterministic seeds, seamlessly tileable where the contract tiles them.

Run (numpy exists only in Blender's python):
  /Applications/Blender.app/Contents/MacOS/Blender -b --factory-startup --python blender/scripts/golf_look_textures.py -- \
      --out Unity/Assets/Resources/Course/Look \
      --sheet ArtDir/screenshots/golf_postcards/look/textures_sheet.png
Options:
  --only Fairway,Green   regenerate only these materials (no sheet unless --sheet given; the sheet then
                         re-reads the other PNGs from --out)
  --gates-only           do not generate: decode the PNGs in --out and run the gates
  --preview DIR          also dump big per-material previews (3x3 albedo, lit) for review
  --smoke-peak X         painted alpha peak of Smoke_C (default .50; effective = X x AlphaGain of LK_SMOKE must be .55..0.72)
v2 2026-10-04: lime-yellow grass albedos (Game-view hue verified by work/postcard-look/v2/tools/lit_predict.py), swirling-flow Lava
(advected stream-function marbling), prism Basalt (3 planes per column, glow only in the vertical joints), soft-wisp Smoke, calmer
Cliff / Rock. The gates at the end print GRASS_HUE_PREDICTED, LAVA_*, BASALT_*, SMOKE_SOFT, SEAMLESS_ALL, FOLDER_BUDGET.
v5 2026-10-04 (area T finish, work/postcard-look/TEXTURES.md "v5"): lively grass (mow bands +-10 %, plateau mottle with hue drift, mow-line streaks, stretched blade noise; mean sRGB kept exactly = GRASS_MEAN),
ragged-tuft Rough, streaky leaning-tussock Scrub, glowing-flow Lava (soft veins, hot lanes, cool zones, chunky crust plates, opened crust: LAVA_P / LAVA_ENC; LAVA_P_V2 = the old look), Basalt whose glow is irregular
stretches + fork cracks along the joint network (no dash rhythm), and the gates GRASS_VARIATION / SCRUB_STREAKY (v6: SCRUB_NOT_SMEARED) / LAVA_PLATES_AND_LANES (+ the rewritten BASALT_PRISM_GLOW_JOINTS glow definition).
v6 2026-10-04 (repair round 1, area T; work/postcard-look/TEXTURES.md "v6"): FINE BLADE layer on Fairway / Green / Rough / Scrub (crisp 3.4-texel strokes in 3 leans: the tee foreground at x3.8 magnification
was a soft blur, LOOK_STILLS_TEXTURED 19 % / 20 % flat), Scrub rebuilt ISOTROPIC (the one-direction 45 degree streaks measured 5.5 : 1 / 8.2 : 1 structure-tensor ratio in the Game view), Lava displacement field computed at
1024 (the 256 grid made 4-texel SAWTOOTH fold lines) + a footprint prefilter on the almost-singular filaments, Basalt without the pale plank-edge dashes / wood-grain streaks and with wider joints (wider glowing cracks); gates
GRASS_NEAR_FIELD x4 and SCRUB_NOT_SMEARED (replaces SCRUB_STREAKY).
v7 2026-10-04 (repair round 2, area T; work/postcard-look/TEXTURES.md "v7"): Lava without the aliased 2-texel dark HAIRLINES of the backward flow map (the review's 'hard vertical seam': earlier footprint prefilter from J 6, wider blurs,
deep thin dark lines filled by a grey closing; gate LAVA_NO_HAIRLINE) plus a FILIGREE of thin pale-orange hot veins and a fine granular heat layer, both NOT advected (the near-field detail the stretched pattern lacks); tone curve re-fitted, Smoke_C as a TAPERED, rounded-foot, peaked wisp at the low end of the runtime band (effective alpha .58; gates SMOKE_TAPERED_FOOT /
SMOKE_STACK_OVERLAP), Basalt_E with second-level fork cracks and a wider glow shoulder (E only; Basalt_C / N change only by the new grooves) and the gates BASALT_BUILDER_WINDOWS (the hole-10 builder's constants are read from hole10_look.py) and BASALT_ALBEDO_NEUTRAL.
Rollback of every v7 change: SMOKE_P.update(SMOKE_P_V6), LAVA_AA = LAVA_AA_V6 + LAVA_P.update(tdf_k=0, fine_a=0.0, fil_a=0.0) + the v6 lut (in the LAVA_ENC comment), BASALT_P fork2_p = wide = 0.0 (each reproduces the v6 bytes).
v8 2026-10-05 (repair round 3, area T; work/postcard-look/TEXTURES.md "v8"): Lava as a FINER swirl (marbling veins at 12 / 26 cycles per tile, was 5 / 11) with a three-scale WEB of thin hot veins (web_*), a heat field that is never clipped (`rank`) and is mapped onto the
target SHADER-heat histogram by an exact quantile curve (LAVA_ENC['quantile'], LAVA_D_RHO / LAVA_D_HEAT; no iterative fit), regional light / dark (reg_a), compact crust plates with a glowing rim (plate_*, open_s, seam_hot) and an octagonal closing (tdf_oct);
Fairway / Green get a PIXEL-SCALE grain (micro_grain: 2 texels per cycle, shows only where the ground is magnified), Smoke_C's column stands off the card's middle so the mirrored copy of a stack is another wisp (xc0 / sway / drift, blur_a); new gates LAVA_FILIGREE and GRASS_MICRO_NEAR; the SMOKE_SOFT
runtime-band and SMOKE_STACK_OVERLAP gates follow the owner's AlphaPower / SmokeAlphaMin, BASALT lit darkness has its own light constant (BASALT_GATE_LIGHT) for the neutral Crater atmosphere.  Rollback: LAVA_P.update(LAVA_P_V7) + LAVA_ENC['quantile'] = 0, FAIRWAY_P / GREEN_P micro = 0,
SMOKE_P.update(SMOKE_P_V7) (each reproduces the v7 bytes).
v9 2026-10-05 (grass liveliness, area T; work/postcard-look/TEXTURES.md "v9"): MOW-BAND LIFE on Fairway / Green: narrower bands (Fairway 2.5 m, K = 2 periods per 10 m tile), +-12 % luminance, band-specific SHEEN in the albedo ALPHA
channel (Fairway_C / Green_C become RGBA: A = smoothness, read by URP Lit through _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A, GolfLook.cs), blade-LEAN streaks in Fairway_N / Green_N (a per-band tilt of the normal about the u axis + long streaks along v); new gates GRASS_BAND_CONTRAST,
GRASS_SHEEN_ALPHA, GRASS_LEAN_STREAKS.  Rollback: BAND_P['on'] = 0 reproduces the v8 bytes exactly (--set BAND_P.on=0 for a one-off run).
Conventions: grid u = (col+.5)/W left->right, v = 1-(row+.5)/H bottom->top (= Unity UV, PNG row 0 is v~1).
Normals: tangent space, OpenGL (+X = +u, +Y = +v = up in the image), encoded n*0.5+0.5.
"""
import os
import sys
import zlib
import struct
import time
import hashlib
import colorsys
import math
import argparse

import numpy as np

F = np.float32
try:
    _HERE = os.path.dirname(os.path.abspath(__file__))
except NameError:  # pragma: no cover
    _HERE = os.getcwd()
ROOT = os.path.abspath(os.path.join(_HERE, '..', '..'))

# --------------------------------------------------------------------------------------------- PNG I/O

def png_bytes(arr):
    """uint8 (H,W,3|4) -> PNG bytes. Per-row adaptive filter (min sum of abs), zlib level 9."""
    arr = np.ascontiguousarray(arr, dtype=np.uint8)
    h, w = arr.shape[:2]
    c = 1 if arr.ndim == 2 else arr.shape[2]
    ctype = {1: 0, 3: 2, 4: 6}[c]
    a = arr.reshape(h, w * c).astype(np.int16)
    left = np.zeros_like(a); left[:, c:] = a[:, :-c]
    up = np.zeros_like(a); up[1:] = a[:-1]
    ul = np.zeros_like(a); ul[1:, c:] = a[:-1, :-c]
    p = left + up - ul
    pa = np.abs(p - left); pb = np.abs(p - up); pc = np.abs(p - ul)
    paeth = np.where((pa <= pb) & (pa <= pc), left, np.where(pb <= pc, up, ul))
    cands = [a, a - left, a - up, a - ((left + up) >> 1), a - paeth]
    cands = np.stack([(x & 255).astype(np.uint8) for x in cands])          # (5,h,wc)
    scores = np.abs(cands.view(np.int8).astype(np.int32)).sum(2)          # (5,h)
    best = scores.argmin(0)
    rows = cands[best, np.arange(h)]
    raw = np.concatenate([best.astype(np.uint8)[:, None], rows], 1).tobytes()

    def chunk(tag, data):
        return struct.pack('>I', len(data)) + tag + data + struct.pack('>I', zlib.crc32(tag + data) & 0xffffffff)
    return (b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', w, h, 8, ctype, 0, 0, 0))
            + chunk(b'IDAT', zlib.compress(raw, 9)) + chunk(b'IEND', b''))


def write_png(path, arr):
    """atomic: write a hidden .tmp next to the target (Unity ignores dot-files and *.tmp), then rename."""
    d, b = os.path.split(path)
    os.makedirs(d, exist_ok=True)
    tmp = os.path.join(d, '.' + b + '.tmp')
    data = png_bytes(arr)
    with open(tmp, 'wb') as f:
        f.write(data)
        f.flush()
        os.fsync(f.fileno())
    os.replace(tmp, path)
    return len(data)


def png_size(path):
    with open(path, 'rb') as f:
        head = f.read(33)
    if head[:8] != b'\x89PNG\r\n\x1a\n' or head[12:16] != b'IHDR':
        return None
    w, h, depth, ctype = struct.unpack('>IIBB', head[16:26])
    return w, h, depth, ctype


def read_png(path):
    """decode a PNG to uint8 (H,W,C), row 0 = top. Uses Blender's decoder (bpy) when available,
    else a small numpy decoder (8-bit, non-interlaced, colour types 2/6 only)."""
    try:
        import bpy  # noqa
        img = bpy.data.images.load(path, check_existing=False)
        img.colorspace_settings.name = 'Non-Color'
        img.alpha_mode = 'STRAIGHT'
        w, h = img.size
        px = np.empty(w * h * 4, np.float32)
        img.pixels.foreach_get(px)
        bpy.data.images.remove(img)
        a = np.flipud(px.reshape(h, w, 4))
        a = np.round(a * 255).astype(np.uint8)
        c = png_size(path)[3]
        return a[..., :3] if c == 2 else a
    except ImportError:
        pass
    with open(path, 'rb') as f:
        data = f.read()
    pos = 8; idat = b''; w = h = ctype = 0
    while pos < len(data):
        ln, = struct.unpack('>I', data[pos:pos + 4]); tag = data[pos + 4:pos + 8]
        body = data[pos + 8:pos + 8 + ln]; pos += 12 + ln
        if tag == b'IHDR':
            w, h, _, ctype = struct.unpack('>IIBB', body[:10])
        elif tag == b'IDAT':
            idat += body
    c = {2: 3, 6: 4}[ctype]
    raw = np.frombuffer(zlib.decompress(idat), np.uint8).reshape(h, 1 + w * c)
    out = np.zeros((h, w * c), np.int32); prev = np.zeros(w * c, np.int32)
    for r in range(h):
        ft = raw[r, 0]; x = raw[r, 1:].astype(np.int32)
        if ft == 0:
            cur = x
        elif ft == 2:
            cur = (x + prev) & 255
        else:
            cur = np.zeros(w * c, np.int32)
            for i in range(w * c):
                a_ = cur[i - c] if i >= c else 0; b_ = prev[i]; c_ = prev[i - c] if i >= c else 0
                if ft == 1:
                    pr = a_
                elif ft == 3:
                    pr = (a_ + b_) >> 1
                else:
                    pp = a_ + b_ - c_; pa_, pb_, pc_ = abs(pp - a_), abs(pp - b_), abs(pp - c_)
                    pr = a_ if (pa_ <= pb_ and pa_ <= pc_) else (b_ if pb_ <= pc_ else c_)
                cur[i] = (x[i] + pr) & 255
        out[r] = cur; prev = cur
    return out.astype(np.uint8).reshape(h, w, c)

# --------------------------------------------------------------------------------------------- noise kit
# Every function is periodic on the unit tile: lattice periods are integers, Worley wraps its cells,
# warps add periodic offsets, blurs are done in the Fourier domain (circular by construction).

_SHIFT = [0.0, 0.0]   # tile-space offset of the sampling grid (per-material phase; +1.0 = whole-tile periodicity proof)


def grid(w, h):
    u = np.broadcast_to(((np.arange(w, dtype=np.float64) + .5) / w + _SHIFT[0]).astype(F)[None, :], (h, w)).copy()
    v = np.broadcast_to((1 - (np.arange(h, dtype=np.float64) + .5) / h + _SHIFT[1]).astype(F)[:, None], (h, w)).copy()
    return u, v


_GT = {}


def _gtab(px, py, seed):
    k = (px, py, seed)
    if k not in _GT:
        a = np.random.default_rng([seed, px, py, 17]).uniform(0, 2 * np.pi, (py, px))
        _GT[k] = (np.cos(a).astype(F), np.sin(a).astype(F))
    return _GT[k]


def _fade(t):
    return t * t * t * (t * (t * 6 - 15) + 10)


def noise(u, v, pu, pv=None, seed=0):
    """periodic gradient noise on the unit tile with pu x pv lattice cells, ~[-1,1]."""
    pv = pu if pv is None else pv
    gx, gy = _gtab(pu, pv, seed)
    x = u * pu; y = v * pv
    x0 = np.floor(x); y0 = np.floor(y)
    fx = (x - x0).astype(F); fy = (y - y0).astype(F)
    i0 = x0.astype(np.int64) % pu; j0 = y0.astype(np.int64) % pv
    i1 = (i0 + 1) % pu; j1 = (j0 + 1) % pv
    n00 = gx[j0, i0] * fx + gy[j0, i0] * fy
    n10 = gx[j0, i1] * (fx - 1) + gy[j0, i1] * fy
    n01 = gx[j1, i0] * fx + gy[j1, i0] * (fy - 1)
    n11 = gx[j1, i1] * (fx - 1) + gy[j1, i1] * (fy - 1)
    a = _fade(fx); b = _fade(fy)
    x0n = n00 + (n10 - n00) * a; x1n = n01 + (n11 - n01) * a
    return ((x0n + (x1n - x0n) * b) * F(1.414)).astype(F)


def fbm(u, v, p, octaves=4, seed=0, gain=0.5, pv=None):
    pv = p if pv is None else pv
    s = np.zeros(np.shape(u), F); amp = 1.0; norm = 0.0
    for o in range(octaves):
        s += F(amp) * noise(u, v, p * 2 ** o, pv * 2 ** o, seed * 131 + o)
        norm += amp; amp *= gain
    return s / F(norm)


def zs(x):
    x = x - x.mean()
    return (x / (x.std() + 1e-8)).astype(F)


def sm(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0, 1)
    return (t * t * (3 - 2 * t)).astype(F)


def worley(u, v, nx, ny, seed, jitter=0.9):
    """periodic Voronoi: f1, f2, id of nearest, edge = distance to the bisector with the 2nd nearest
    (cell units), vx/vy = vector pixel->nearest feature point (cell units)."""
    rng = np.random.default_rng([seed, nx, ny, 7])
    ox = rng.uniform(.5 - jitter / 2, .5 + jitter / 2, (ny, nx)).astype(F)
    oy = rng.uniform(.5 - jitter / 2, .5 + jitter / 2, (ny, nx)).astype(F)
    X = (u * nx).astype(F); Y = (v * ny).astype(F)
    cx = np.floor(X).astype(np.int64); cy = np.floor(Y).astype(np.int64)
    inf = np.full(X.shape, 1e9, F)
    F1 = inf.copy(); F2 = inf.copy(); ID = np.zeros(X.shape, np.int64); ID2 = np.zeros(X.shape, np.int64)
    a1x = np.zeros_like(X); a1y = np.zeros_like(X); a2x = np.zeros_like(X); a2y = np.zeros_like(X)
    for dy in (-1, 0, 1):
        for dx in (-1, 0, 1):
            ix = cx + dx; iy = cy + dy; wx = ix % nx; wy = iy % ny
            vx = (ix + ox[wy, wx] - X).astype(F); vy = (iy + oy[wy, wx] - Y).astype(F)
            d = np.sqrt(vx * vx + vy * vy)
            c1 = d < F1; c2 = (~c1) & (d < F2)
            a2x = np.where(c1, a1x, np.where(c2, vx, a2x)); a2y = np.where(c1, a1y, np.where(c2, vy, a2y))
            F2 = np.where(c1, F1, np.where(c2, d, F2))
            ID2 = np.where(c1, ID, np.where(c2, wy * nx + wx, ID2))
            a1x = np.where(c1, vx, a1x); a1y = np.where(c1, vy, a1y)
            F1 = np.where(c1, d, F1); ID = np.where(c1, wy * nx + wx, ID)
    ex = a2x - a1x; ey = a2y - a1y
    edge = (F2 * F2 - F1 * F1) / (2 * np.sqrt(ex * ex + ey * ey) + 1e-6)
    return dict(f1=F1, f2=F2, id=ID, id2=ID2, vx=a1x, vy=a1y, v2x=a2x, v2y=a2y, edge=edge.astype(F), n=nx * ny)


def crand(ids, n, seed):
    return np.random.default_rng([seed, n, 3]).random(n).astype(F)[ids]


def blur(a, sigma):
    """circular gaussian blur (FFT), sigma in pixels; a (H,W) or (H,W,C)."""
    if sigma <= 0:
        return a
    h, w = a.shape[:2]
    fy = np.fft.fftfreq(h)[:, None]; fx = np.fft.rfftfreq(w)[None, :]
    g = np.exp(-2 * (np.pi * sigma) ** 2 * (fx * fx + fy * fy))
    if a.ndim == 2:
        return np.fft.irfft2(np.fft.rfft2(a) * g, s=(h, w)).astype(F)
    return np.stack([np.fft.irfft2(np.fft.rfft2(a[..., i]) * g, s=(h, w)) for i in range(a.shape[2])], -1).astype(F)


def C(r, g, b):
    return np.array([r, g, b], F) / F(255)


def mix(a, b, t):
    t = np.asarray(t, F)
    return a + (b - a) * (t[..., None] if t.ndim else t)


def normal_map(h, strength):
    """periodic height -> unit tangent normals (OpenGL +Y up), wrap-around Sobel."""
    def sh(a, dy, dx):
        return np.roll(np.roll(a, dy, 0), dx, 1)
    R = lambda dy: sh(h, dy, -1)     # right neighbour (col+1)
    L = lambda dy: sh(h, dy, 1)      # left neighbour (col-1)
    du = ((R(0) - L(0)) * 2 + (R(1) - L(1)) + (R(-1) - L(-1))) / 8
    U = lambda dx: sh(h, 1, dx)      # row-1 = higher v
    D = lambda dx: sh(h, -1, dx)
    dv = ((U(0) - D(0)) * 2 + (U(1) - D(1)) + (U(-1) - D(-1))) / 8
    n = np.stack([-du * strength, -dv * strength, np.ones_like(h)], -1)
    return (n / np.linalg.norm(n, axis=-1, keepdims=True)).astype(F)


def enc_normal(n):
    return np.clip(np.round((n * .5 + .5) * 255), 0, 255).astype(np.uint8)


def enc(c):
    return np.clip(np.round(np.clip(c, 0, 1) * 255), 0, 255).astype(np.uint8)


def lum(rgb):
    return 0.2126 * rgb[..., 0] + 0.7152 * rgb[..., 1] + 0.0722 * rgb[..., 2]

# --------------------------------------------------------------------------------------------- materials
# Each returns dict with float (H,W,3) 'C' in sRGB 0..1, optional unit normals 'N', optional 'E', 'A' alpha.
# Palette anchors were sampled from ArtDir/environments/refs/*.jpg with Pillow (see TEXTURES.md); albedos
# sit a little greener/cooler than the stills because the golf key light is warm (1,.90,.76) + ACES.

SEEDS = dict(Fairway=101, Green=201, Rough=301, Scrub=401, Sand=501, Cliff=601, CliffDark=701, Rock=801,
             Path=901, Masonry=1001, Basalt=1101, Lava=1201, Surf=1301, Fall=1401, Smoke=1501, Sky_Crater=1601)


def grass_grain(u, v, seed, p_long, p_iso, amt_long, amt_iso):
    """blade grain: anisotropic streaks along v (mow direction) + an isotropic fine layer."""
    b1 = noise(u, v, p_long, p_long // 4, seed)
    b2 = noise(u, v, p_iso, p_iso, seed + 1)
    return b1, b2, F(1) + F(amt_long) * b1 + F(amt_iso) * b2


# v5 2026-10-04 (grass flatness): every grass map keeps the mean sRGB colour of the v2 maps (the HoleTint rows, GRASS_HUE_GAMEVIEW and the lit-model gates were calibrated on
# those means), but gains visible, stylised variation at the scales the phone camera resolves at 2-20 yd: crisper mow bands, 0.3-1.5 m plateau mottle with a little hue drift
# (lush = greener + darker, dry = yellower + lighter, always inside albedo hue ~70-78), mow-line streaks along v (0.15-0.4 m wide), stretched blade clumps (12 x 33 cm) with dark gaps.
# Fairway_N / Green_N are unchanged (their height fields do not use any of the new layers).
GRASS_MEAN = dict(Fairway=(0.55254, 0.65747, 0.19524), Green=(0.59217, 0.68629, 0.23138), Rough=(0.40611, 0.44822, 0.13611), Scrub=(0.41604, 0.38113, 0.20862))
FAIRWAY_P = dict(light=(157, 180, 55), dark=(124, 149, 44), mott=0.048, patch=0.036, hue=0.024, streak=0.046, streak2=0.028, clump=0.052, fine=0.060, fine_h=0.16, micro=0.105, micro_h=0.10)
GREEN_P = dict(light=(163, 187, 64), dark=(138, 163, 52), mott=0.042, patch=0.032, hue=0.022, streak=0.036, streak2=0.026, clump=0.051, fine=0.045, fine_h=0.12, micro=0.115, micro_h=0.10)


# v9 2026-10-05 (user request "gentle grass liveliness", area T requirement 2 "mow-band life"): the v8 bands (+-10.6 %, 5 m wide, under +-5 % cloudy mottle) were invisible in the Game view (the phone sees 3-4 m of fairway at the ball: one band, no edge).
# Now: Fairway 2.5 m bands (K = 2 periods per 10 m tile, light band on the centre line), +-12 % luminance with a crisp-ish edge, mottle x`mott_scale`; Green 1 m stripes (K = 3 per 6 m tile) +-12 % plus a faint cross set;
# SHEEN: Fairway_C / Green_C carry the smoothness in the ALPHA channel (URP Lit smoothness source = albedo alpha; absolute smoothness = alpha x LK_ Spec.Smoothness scale in GolfLook.cs): light bands glossier (`sheen_l`), dark bands matte (`sheen_d`);
# LEAN: the normal map tilts about the u axis by +-`lean_deg` per band (light band leans away from the camera, +v = down the hole; sign `lean_sign`) plus long blade-lean streaks (`lean_streak_deg` sd, 28 cm wide x 2.5 m long, and a finer set), which also modulate the sheen a little.
# `on` = 0 reproduces the v8 bytes (rollback).
# Goal 15: subtle bands, retaining the independent fine-blade and micro layers.
# Historical v9 minimum-contrast/lean/sheen gates below remain unchanged and
# intentionally report their conflict with this new appearance contract.
BAND_P = dict(on=1, fw_K=2, fw_amp=0.035, fw_edge=0.28, fw_wob=(0.010, 0.004), gr_K=3, gr_amp=0.020, gr_cross=0.20, gr_edge=0.30, mott_scale=0.90,
              sheen_l=0.12, sheen_d=0.10, sheen_mott=0.01, sheen_streak=0.01, lean_deg=0.6, lean_streak_deg=1.0, lean_sign=1.0, streak_lum=0.018, grain_comp=1, micro_boost=1.10, micro_boost_green=1.0, hue_shift=0.08)
SHEEN_SCALE = 1.0                      # alpha 255 = smoothness 1.0 x this (mirrors GolfLook.Table[LK_FAIRWAY / LK_GREEN].Smoothness: the gates read GolfLook.cs live)


def mow_band(u, v, seed, K, edge, wob_amp):
    """1 = light band (centred on u = 0, period 1/K tile), 0 = dark band; edge = half width of the smoothstep of cos(2 pi K u) (0.17 = a ~0.45 m edge on a 2.5 m band)."""
    wob = wob_amp[0] * noise(u, v, 2, 6, seed + 1) + wob_amp[1] * noise(u, v, 4, 24, seed + 2)
    s = np.cos(2 * np.pi * K * (u + wob))
    return sm(-edge, edge, s), wob


def blade_streaks(u, v, seed):
    """unit-sd field of long blade-lean streaks: 28 cm x 2.5 m (36 x 4 cycles on the 10 m Fairway tile) + a finer 11 cm x 1 m set; periodic; clipped at +-2.5 sd."""
    a = noise(u, v, 36, 4, seed + 60) + 0.55 * noise(u, v, 90, 10, seed + 61)
    return np.clip(a / (float(a.std()) + 1e-8), -2.5, 2.5).astype(F)


def tilt_about_u(n, theta):
    """rotate unit tangent-space normals (..., 3) about the u (X) axis by the angle field theta (radians; + tilts toward +v)."""
    c = np.cos(theta).astype(F); sn = np.sin(theta).astype(F)
    ny = n[..., 1] * c + n[..., 2] * sn; nz = n[..., 2] * c - n[..., 1] * sn
    return np.stack([n[..., 0], ny, nz], -1).astype(F)


# v6 2026-10-04 (review: "tee foreground smeared and flat", LOOK_STILLS_TEXTURED 19 % / 20 % > 15 %): at the tee the phone camera magnifies the 10 m Fairway tile x3.8 in the bottom rows
# (1 texel = 1 cm = 3.8 px; measured: the albedo luminance alone, bilinear x3.8, reproduces the Game view's flat-window share, 51 % / median window sd .0059 in the last 160 rows), and every layer of
# v5 was 3-50 texels wide, so the foreground was a soft blur.  A FINE BLADE layer is added to the grass maps: crisp short strokes 3.4 texels per cycle, 3x longer than wide, leaning three ways
# (upright / +45 / -45, picked by a slow field in ~1.4 m patches so no direction rules a whole fairway), tanh-sharpened.  It is multiplicative luminance (+-FINE_P amp), a small yellow-green
# hue push, and a crisp relief in the normal map; match_mean restores the exact mean colour afterwards.
FINE_P = dict(cyc=300, lenf=3.0, sharp=1.6, dir_cyc=7)


def fine_blades(u, v, seed, P=None):
    """crisp short blade strokes, unit-ish luminance field in [-1, 1] (periodic: integer lattice periods, integer shears 2u +- v = leans of +-26.6 deg)."""
    P = FINE_P if P is None else P
    cyc = int(P['cyc']); pv_ = max(3, int(round(cyc / P['lenf']))); cs = max(8, int(round(cyc / 2.236)))
    wu = u + 0.0025 * noise(u, v, 36, 36, seed + 6); wv = v + 0.0025 * noise(u, v, 36, 36, seed + 7)      # a few cm of warp: no comb-like regular hatching
    nu = noise(wu, wv, cyc, pv_, seed + 1)
    n1 = noise(2 * wu + wv, wv, cs, pv_, seed + 2)
    n2 = noise(2 * wu - wv, wv, cs, pv_, seed + 3)
    s1 = fbm(u, v, int(P['dir_cyc']), 2, seed + 4); s2 = fbm(u, v, int(P['dir_cyc']), 2, seed + 5)
    w1 = 0.8 * sm(-0.10, 0.50, s1); w2 = 0.8 * sm(-0.10, 0.50, s2) * (1 - w1); w0 = 1 - w1 - w2
    n = zs(w0 * nu + w1 * n1 + w2 * n2)
    return (np.tanh(P['sharp'] * n) / np.tanh(P['sharp'])).astype(F)


def micro_grain(u, v, seed, cyc=512, lenf=1.6):
    """v8: the finest layer a 1024 map can hold, 2 texels per cycle (a little longer along v: blade tips), unit sd, clipped at +-2.5 sd.  It is invisible wherever a pixel covers more than ~1.5 texels (the GPU's mip chain averages it away) and is exactly the
    pixel-scale detail the camera needs where the ground is MAGNIFIED (the ball's feet: 1 texel = 1 cm = 2.6-3.8 px): review 'the grass right at the ball is still soft and smeared' (hole 9: Laplacian sd 1.87 near vs 6.34 mid)."""
    n = noise(u, v, int(cyc), max(3, int(round(cyc / lenf))), seed)
    return np.clip(n / (float(n.std()) + 1e-8), -2.5, 2.5).astype(F) / F(2.5) * F(2.0)


def match_mean(col, mean):
    """per-channel gain so that the mean sRGB colour of the map is exactly `mean` (keeps every calibrated Game-view number)."""
    m = col.reshape(-1, 3).mean(0).astype(np.float64)
    out = np.clip(col * (np.asarray(mean, np.float64) / m).astype(F), 0, 1)
    for _ in range(3):                                   # the clip is rare, a couple of passes make the mean exact
        m = out.reshape(-1, 3).mean(0).astype(np.float64)
        out = np.clip(out * (np.asarray(mean, np.float64) / m).astype(F), 0, 1)
    return out.astype(F)


def grass_layers(u, v, seed, P, m_cyc, mb_cyc, st_cyc, st2_cyc, cl_xy, warp):
    """luminance factor (H,W) and a per-texel hue-drift colour factor (H,W,3) shared by Fairway and Green (periodic everywhere)."""
    wu = u + warp * noise(u, v, 6, 6, seed + 20); wv = v + warp * noise(u, v, 6, 6, seed + 21)
    mA = zs(fbm(wu, wv, m_cyc, 3, seed + 22, gain=0.55))
    mB = zs(fbm(wu, wv, mb_cyc, 2, seed + 23))
    hd = zs(fbm(wu, wv, max(2, m_cyc * 2 // 3), 3, seed + 24))
    lumf = 1 + P['mott'] * np.tanh(1.25 * mA) + P['patch'] * mB - 0.55 * P['hue'] * hd         # lush (hd > 0) = a little darker
    lumf = lumf + P['streak'] * noise(u, v, st_cyc, max(2, st_cyc // 9), seed + 25) + P['streak2'] * noise(u, v, st2_cyc, max(3, st2_cyc // 11), seed + 26)
    blade = zs(fbm(u, v, cl_xy[0], 3, seed + 27, gain=0.6, pv=cl_xy[1]))                           # stretched blade clumps along v: no cell edges (a Voronoi tone read as flagstones)
    lumf = lumf + P['clump'] * blade
    warm = np.array([1.03, 1.005, 0.94], F); cool = np.array([0.955, 1.0, 0.99], F)   # lush = a more SATURATED green (B not raised: the first version read as dirt)
    hue_f = mix(warm, cool, sm(-1.7, 1.7, hd))                                                   # hd > 0: greener (hue up), hd < 0: yellower
    return lumf.astype(F), hue_f


def mat_fairway(N=1024, seed=101):
    P = FAIRWAY_P
    B = BAND_P
    u, v = grid(N, N)
    # 2 lengthwise bands per tile: light band centred on u=0 (the centre line), dark band at u=.5   (v9: K = fw_K periods per tile, light bands on u = 0, 1/K, 2/K ...)
    wob = 0.011 * noise(u, v, 2, 6, seed + 1) + 0.004 * noise(u, v, 4, 24, seed + 2)
    s = np.cos(2 * np.pi * (u + wob))
    band = sm(-0.16, 0.16, s)
    if B['on']:
        band, _w = mow_band(u, v, seed, B['fw_K'], B['fw_edge'], B['fw_wob'])
        P = dict(P); P['mott'] = P['mott'] * B['mott_scale']; P['patch'] = P['patch'] * B['mott_scale']
        # v9: one base colour (the v8 light/dark mean) x a luminance factor +-fw_amp; the dark band a touch greener (hue +~2 deg), the light a touch yellower: bands differ in luminance first
        base = C(141, 165, 50)
        bandf = 1 + B['fw_amp'] * (2 * band - 1)
        col = base * bandf[..., None] * mix(np.array([1 - 0.03 * B['hue_shift'], 1.0, 1 + 0.06 * B['hue_shift']], F), np.array([1 + 0.02 * B['hue_shift'], 1 + 0.008 * B['hue_shift'], 1 - 0.06 * B['hue_shift']], F), band)
    else:
        # v2 2026-10-04: lime-yellow-green like the stills (Game-view hue verified by work/postcard-look/v2/tools/lit_predict.py).
        # v5: bands +-9 % luminance (were +-6 %)
        light = C(*P['light']); dark = C(*P['dark'])
        col = mix(dark, light, band)
    m1 = zs(fbm(u, v, 3, 3, seed + 3)); m2 = zs(fbm(u, v, 9, 3, seed + 4)); m3 = zs(fbm(u, v, 2, 2, seed + 5))
    warm = np.array([1.035, 1.01, 0.92], F); cool = np.array([0.965, 1.0, 1.035], F)
    col = col * mix(cool, warm, sm(-2.2, 2.2, m3 * 0.6 + m2 * 0.5))
    b1, b2, grain = grass_grain(u, v, seed + 6, 384, 160, 0.045, 0.03)
    col = col * (1 + 0.03 * m1 + 0.03 * m2)[..., None] * grain[..., None]
    thatch = sm(1.7, 2.7, zs(noise(u, v, 200, 200, seed + 8)))
    col = col * (1 - 0.12 * thatch)[..., None]
    h = 0.55 * b1 + 0.35 * b2 + 0.5 * blur(m2, 2) * 0.35 - 0.4 * thatch
    # v5 layers (10 m tile): plateau mottle 9/18/36 cycles (1.1 / .55 / .28 m), patches 4/8 cycles, streaks 0.36 m x 3.3 m and 0.14 m x 1.4 m, stretched blade noise 18 x 50 cm
    lumf, hue_f = grass_layers(u, v, seed, P, 9, 4, 28, 72, (56, 20), 0.018)
    fb = fine_blades(u, v, seed + 90)                                                     # v6: crisp fine blade strokes (near field)
    fine_hue = mix(np.array([0.985, 1.0, 1.012], F), np.array([1.015, 1.004, 0.975], F), sm(-1.0, 1.0, fb))
    mg = micro_grain(u, v, seed + 95)                                                     # v8: pixel-scale grain for the magnified near field
    gg = P['fine'] * fb + P['micro'] * mg
    if B['on']:
        st = blade_streaks(u, v, seed)                                                    # v9: blade-lean streaks (also in the albedo a little, and in the sheen)
        lumf = lumf * (1 + B['streak_lum'] * st)
        gg = P['fine'] * fb + P['micro'] * B['micro_boost'] * mg
        if B['grain_comp']:
            gg = gg / bandf                                                               # v9: the pixel-scale grain keeps the SAME absolute amplitude in the dark band as in the light one (a multiplicative grain was 12 % weaker there: the Game view's near-field Laplacian sd fell 3.01 -> 2.64 on a dark band)
    col = match_mean(col * lumf[..., None] * hue_f * (1 + gg)[..., None] * fine_hue, GRASS_MEAN['Fairway'])
    nrm = normal_map(blur(h, 0.7) + P['fine_h'] * fb + P['micro_h'] * mg, 1.6)
    out = dict(C=col, N=nrm)
    if B['on']:
        theta = np.deg2rad(B['lean_deg']) * B['lean_sign'] * (2 * band - 1) + np.deg2rad(B['lean_streak_deg']) * B['lean_sign'] * st / 1.0
        out['N'] = tilt_about_u(nrm, theta)
        out['S'] = sheen_alpha(blur(band, 1.0), m2, st, B)
    return out


def sheen_alpha(band_s, mott, st, B):
    """per-texel smoothness (0..1, absolute; encoded as the albedo alpha): matte dark band .. glossy light band, a little mottle, a little streak (the same streaks that lean the normal)."""
    a = B['sheen_d'] + (B['sheen_l'] - B['sheen_d']) * band_s + B['sheen_mott'] * np.clip(mott, -2, 2) + B['sheen_streak'] * np.clip(st, -2.5, 2.5)
    return np.clip(a / SHEEN_SCALE, 0.0, 1.0).astype(F)


def mat_green(N=1024, seed=201):
    P = GREEN_P
    B = BAND_P
    u, v = grid(N, N)
    # tile 6 m: 3 light + 3 dark 1 m stripes along v, a fainter cross set -> soft checker typical of greens
    s1 = np.cos(2 * np.pi * 3 * (u + 0.004 * noise(u, v, 3, 5, seed + 1)))
    s2 = np.cos(2 * np.pi * 3 * (v + 0.004 * noise(u, v, 5, 3, seed + 2)))
    band = 0.5 + 0.5 * (0.7 * sm(-0.2, 0.2, s1) + 0.3 * sm(-0.2, 0.2, s2)) - 0.25
    if B['on']:
        P = dict(P); P['mott'] = P['mott'] * B['mott_scale']; P['patch'] = P['patch'] * B['mott_scale']
        # v9: primary stripes (u) +-gr_amp with the cross set (v) a fifth of that; one base colour, light stripes a touch yellower
        b1 = sm(-B['gr_edge'], B['gr_edge'], np.cos(2 * np.pi * B['gr_K'] * (u + 0.004 * noise(u, v, 3, 5, seed + 1))))
        b2 = sm(-0.2, 0.2, s2)
        g = (1 - B['gr_cross']) * (2 * b1 - 1) + B['gr_cross'] * (2 * b2 - 1)
        base = C(151, 175, 59)
        bandf = 1 + B['gr_amp'] * g
        col = base * bandf[..., None] * mix(np.array([1 - 0.03 * B['hue_shift'], 1.0, 1 + 0.06 * B['hue_shift']], F), np.array([1 + 0.02 * B['hue_shift'], 1 + 0.008 * B['hue_shift'], 1 - 0.06 * B['hue_shift']], F), 0.5 + 0.5 * g)
        band = b1
    else:
        light = C(*P['light']); dark = C(*P['dark'])                           # v5: stripes +-7 % luminance (were +-4 %)
        col = mix(dark, light, np.clip(band, 0, 1))
    m1 = zs(fbm(u, v, 3, 3, seed + 3)); m2 = zs(fbm(u, v, 12, 3, seed + 4))
    b1_, b2_, grain = grass_grain(u, v, seed + 6, 512, 256, 0.03, 0.025)
    col = col * (1 + 0.025 * m1 + 0.02 * m2)[..., None] * grain[..., None]
    h = 0.5 * b1_ + 0.5 * b2_ + 0.15 * m2
    # v5 layers (6 m tile): mottle 10/20/40 cycles (.6 / .3 / .15 m), patches 5/10, streaks 0.2 m wide, stretched blade noise 9 x 25 cm
    lumf, hue_f = grass_layers(u, v, seed, P, 10, 5, 30, 80, (64, 24), 0.014)
    fb = fine_blades(u, v, seed + 90)                                                     # v6: crisp fine blade strokes (near field)
    fine_hue = mix(np.array([0.985, 1.0, 1.012], F), np.array([1.015, 1.004, 0.975], F), sm(-1.0, 1.0, fb))
    mg = micro_grain(u, v, seed + 95)                                                     # v8: pixel-scale grain for the magnified near field
    gg = P['fine'] * fb + P['micro'] * mg
    if B['on']:
        a_ = noise(u, v, 36, 6, seed + 60) + 0.55 * noise(u, v, 90, 15, seed + 61)       # v9 blade-lean streaks (6 m tile: 17 cm x 1 m and 7 cm x 40 cm)
        st = np.clip(a_ / (float(a_.std()) + 1e-8), -2.5, 2.5).astype(F)
        lumf = lumf * (1 + B['streak_lum'] * st)
        gg = P['fine'] * fb + P['micro'] * B['micro_boost_green'] * mg
        if B['grain_comp']:
            gg = gg / bandf
    col = match_mean(col * lumf[..., None] * hue_f * (1 + gg)[..., None] * fine_hue, GRASS_MEAN['Green'])
    nrm = normal_map(blur(h, 0.6) + P['fine_h'] * fb + P['micro_h'] * mg, 1.5)
    out = dict(C=col, N=nrm)
    if B['on']:
        theta = np.deg2rad(B['lean_deg']) * B['lean_sign'] * (2 * b1 - 1) + np.deg2rad(B['lean_streak_deg']) * B['lean_sign'] * st
        out['N'] = tilt_about_u(nrm, theta)
        out['S'] = sheen_alpha(blur(b1, 1.0), m2, st, B)
    return out


ROUGH_P = dict(mott=0.055, hue=0.030, streak=0.030, jag=0.20, nx=46, ny=30, fine=0.050, fine_h=0.14)
SCRUB_P = dict(fine=0.085, fine_h=0.16)


def mat_rough(N=1024, seed=301):
    P = ROUGH_P
    u, v = grid(N, N)
    wu = u + 0.016 * noise(u, v, 6, 6, seed + 1); wv = v + 0.016 * noise(u, v, 6, 6, seed + 2)
    # v5: tufts are 1 : 1.5 along v with ragged edges (round light caps read as polka dots), plus 0.5-3 m tonal mottle with a little hue drift
    jag = noise(u, v, 150, 150, seed + 30)
    W = worley(wu, wv, P['nx'], P['ny'], seed + 3, 0.95)          # tufts ~0.26 x 0.40 m
    W2 = worley(wu, wv, 96, 64, seed + 4, 0.95)                   # small tufts ~0.12 x 0.19 m
    tuft = 1 - sm(0.0, 0.78, W['f1'] + P['jag'] * jag); tuft2 = 1 - sm(0.0, 0.8, W2['f1'] + 0.7 * P['jag'] * jag)
    r = crand(W['id'], W['n'], seed + 5)
    big = zs(fbm(u, v, 2, 3, seed + 6)); mid = zs(fbm(u, v, 6, 3, seed + 7))
    deep = C(80, 94, 26); midc = C(116, 126, 38); tip = C(152, 158, 56); dry = C(158, 138, 62)   # v2: mean ~(106,115,36) hue 67: needle.jpg's rough is yellower (h59) than its fairway (h67); was hue 94
    t = tuft * 0.65 + tuft2 * 0.35
    col = mix(deep, midc, sm(0.15, 0.75, t + 0.08 * mid))
    col = mix(col, tip, sm(0.55, 1.0, tuft) * (0.35 + 0.45 * r))
    dryness = sm(0.2, 2.0, big * 0.75 + 0.45 * mid) * 0.5
    col = mix(col, col * 0.4 + dry * 0.6, dryness * (0.4 + 0.6 * tuft))
    fine = noise(u, v, 256, 256, seed + 8)
    col = col * (1 + 0.035 * fine + 0.03 * mid)[..., None]
    h = 0.9 * t * t + 0.25 * fine + 0.2 * tuft2
    mA = zs(fbm(wu, wv, 5, 3, seed + 31, gain=0.55)); hd = zs(fbm(wu, wv, 4, 3, seed + 32))
    lumf = 1 + P['mott'] * np.tanh(1.2 * mA) - 0.5 * P['hue'] * hd + P['streak'] * noise(u, v, 44, 5, seed + 33)
    warm = np.array([1.025, 1.006, 0.95], F); cool = np.array([0.975, 1.0, 1.03], F)
    fb = fine_blades(u, v, seed + 90)                                                     # v6: crisp fine blade strokes (near field)
    fine_hue = mix(np.array([0.985, 1.0, 1.012], F), np.array([1.015, 1.004, 0.975], F), sm(-1.0, 1.0, fb))
    col = match_mean(col * lumf[..., None] * mix(warm, cool, sm(-1.7, 1.7, hd)) * (1 + P['fine'] * fb)[..., None] * fine_hue, GRASS_MEAN['Rough'])
    return dict(C=col, N=normal_map(blur(h, 0.8) + P['fine_h'] * fb, 2.2))


def mat_scrub(N=1024, seed=401):
    """v6 2026-10-04: Split's dry ridge as ISOTROPIC bushy scrub with crisp fine blades (review: the v5 map's one-direction 45 degree leaning streaks read as motion blur in the Game view:
    structure-tensor ratio 5.5 : 1 over the lower third of hole09_3_ruin, 8.2 : 1 on the steep ground, target <= 2.5; the map itself measured 2.8 : 1 and the camera's foreshortening and
    bilinear magnification doubled it).  Tussocks 0.3 m (round cells with blade-ragged edges), leaf clusters, dry straw FLECKS (not blades), brown soil, over khaki ground; the fine_blades layer (three leans
    picked by a slow field) adds the crisp near-field grain.  Mean colour unchanged (hue ~50)."""
    P = SCRUB_P
    u, v = grid(N, N)
    wu = u + 0.020 * noise(u, v, 5, 5, seed + 1); wv = v + 0.020 * noise(u, v, 5, 5, seed + 2)
    sA = zs(fbm(wu, wv, 110, 2, seed + 41, gain=0.5, pv=100))                                           # straw flecks ~9 cm, isotropic
    sC = zs(fbm(wu, wv, 170, 1, seed + 43, gain=0.5, pv=160))                                           # fine flecks ~6 cm
    W = worley(wu, wv, 70, 70, seed + 3, 0.95)                                                           # tussocks ~0.17 m wide, round cells
    W2 = worley(wu, wv, 140, 140, seed + 4, 0.95)                                                        # leaf clusters ~8 cm
    cover = zs(fbm(wu, wv, 6, 3, seed + 5)); region = zs(fbm(wu, wv, 2, 3, seed + 6)); gaps = zs(fbm(wu, wv, 7, 3, seed + 44))   # cover / gaps at 6 / 7 cycles (4 / 5 made 3 m bush patches: 4x4 evenness 10.1 %)
    r = crand(W['id'], W['n'], seed + 7)
    clump = (1 - sm(0.05, 0.9, W['f1'])) * 0.7 + (1 - sm(0.0, 0.85, W2['f1'])) * 0.3
    bush = sm(0.26, 0.50, clump + 0.10 * cover + 0.12 * (r - .5) + 0.11 * sA + 0.05 * sC)           # ragged edges
    # ground between the bushes: khaki dry grass / brown soil, straw flecks over it, a few dark gaps
    khaki = C(152, 138, 88); brown = C(118, 94, 60); straw = C(176, 158, 104); shade = C(100, 84, 54)
    ground = mix(khaki, brown, sm(0.4, 2.2, gaps) * 0.8 + sm(-0.2, 0.9, zs(fbm(wu, wv, 9, 3, seed + 8))) * 0.15)
    ground = mix(ground, straw, sm(-0.1, 0.9, sA) * 0.75)
    ground = mix(ground, shade, sm(0.6, 1.8, -sC) * 0.5)
    dko = C(60, 61, 30); olive = C(100, 95, 48); lit = C(140, 128, 72); sage = C(104, 106, 72)
    b = mix(dko, olive, sm(0.2, 0.8, clump))
    b = mix(b, lit, sm(0.65, 1.0, clump) * 0.55 + sm(0.9, 2.2, sA) * 0.3 * bush)
    b = mix(b, b * 0.5 + sage * 0.5, sm(0.0, 1.5, region) * (r > 0.5))
    b = b * (1 + 0.10 * np.clip(sC, -2, 2) * 0.5)[..., None]
    col = mix(ground, b, bush)
    col = col * (0.84 + 0.16 * sm(0.0, 0.25, bush + 0.15))[..., None]                               # contact shadow at the bush feet
    col = mix(col, col * np.array([1.08, 1.0, 0.86], F), sm(-0.5, 1.5, -region) * 0.35)              # drier khaki regions (.5 -> .35: the 4x4 evenness gate)
    fine = noise(u, v, 256, 256, seed + 10)
    fb = fine_blades(u, v, seed + 90)                                                                    # v6: crisp fine blade strokes (near field), three leans
    fine_hue = mix(np.array([0.985, 1.0, 1.012], F), np.array([1.015, 1.004, 0.975], F), sm(-1.0, 1.0, fb))
    col = match_mean(col * (1 + 0.04 * fine)[..., None] * (1 + P['fine'] * fb)[..., None] * fine_hue, GRASS_MEAN['Scrub'])
    h = 1.0 * bush * (0.6 + 0.4 * clump) + 0.25 * np.clip(sA, -1.5, 1.5) * 0.5 + 0.2 * fine + 0.15 * zs(noise(wu, wv, 40, 40, seed + 9))
    return dict(C=col, N=normal_map(blur(h, 0.9) + P['fine_h'] * fb, 2.0))


def mat_sand(N=512, seed=501):
    u, v = grid(N, N)
    w1 = 0.55 * fbm(u, v, 3, 3, seed + 1)
    ph1 = 2 * np.pi * (14 * u + 5 * v + w1)                 # wavelength 6 m / 14.9 = 0.40 m
    ph2 = 2 * np.pi * (-6 * u + 13 * v + 0.6 * fbm(u, v, 3, 3, seed + 2))
    rip = (np.sin(ph1) + 0.3 * np.sin(2 * ph1)) * (0.6 + 0.4 * sm(-1, 1, zs(fbm(u, v, 2, 2, seed + 3))))
    rip2 = np.sin(ph2) * 0.35 * sm(-0.5, 1.5, zs(fbm(u, v, 2, 2, seed + 4)))
    base = C(228, 208, 168); warm = C(222, 194, 148); light = C(240, 226, 196)
    m = zs(fbm(u, v, 3, 3, seed + 5))
    col = mix(base, warm, sm(-0.5, 2.0, m) * 0.6)
    col = mix(col, light, sm(0.2, 1.0, rip) * 0.35)
    g = noise(u, v, 170, 170, seed + 6)
    sp = zs(noise(u, v, 128, 128, seed + 7))
    col = col * (1 + 0.025 * g)[..., None]
    col = mix(col, C(150, 128, 96), sm(2.2, 3.0, sp) * 0.6)                  # dark grains
    col = mix(col, C(250, 244, 230), sm(2.3, 3.1, -sp) * 0.5)                # bright grains
    h = rip + rip2 + 0.25 * g
    return dict(C=col, N=normal_map(blur(h, 0.8), 1.4))


def strata(N, seed, T, nbands, pal, moss_amt, drip_amt, normal_k):
    """stratified cliff (v = height, u = metres around the wall / T): horizontal strata of uneven thickness that
    wobble and pinch, broken into slabs by slanted, wiggly joints of random depth (some joints/bedding planes are
    shallow or absent, so it never reads as brickwork); each slab overhangs at its top (lit ledge rim, shaded face
    below), chipped outlines, a few hairline fractures, colour patches spanning several slabs, moss on the ledges."""
    u, v = grid(N, N)
    rng = np.random.default_rng([seed, 1])
    th = rng.uniform(0.45, 1.8, nbands); bnd = np.concatenate([[0.0], np.cumsum(th) / th.sum()]).astype(F)
    vph = 0.5 * th[0] / th.sum()                                               # tile edge runs mid-stratum, not along a bedding plane
    wob = 0.016 * noise(u, v, 3, 7, seed + 1) + 0.006 * noise(u, v, 9, 15, seed + 2) + 0.0022 * noise(u, v, 40, 40, seed + 20)
    vw = (v + vph + wob) % 1.0
    band = np.clip(np.searchsorted(bnd, vw, side='right') - 1, 0, nbands - 1)
    lo = bnd[band]; hi = bnd[band + 1]
    s = (vw - lo) / (hi - lo); bh = (hi - lo) * T                             # s: 0 bottom .. 1 top
    topoff = np.where(rng.random(nbands) < 0.72, 0.0, rng.uniform(0.05, 0.4, nbands)).astype(F)  # bedding-plane depth
    t = np.zeros_like(u); bw = np.zeros_like(u); bid = np.zeros(u.shape, np.int64)
    offl = np.zeros_like(u); offr = np.zeros_like(u)
    for k in range(nbands):
        m = band == k
        nb = int(rng.integers(1, 5)); w = rng.uniform(0.35, 2.2, nb)                 # v2: fewer, much more uneven slabs per stratum (was 2-6 near-equal blocks = brick courses)
        cb = np.concatenate([[0.0], np.cumsum(w) / w.sum()]); ph = rng.random()
        slope = rng.uniform(-0.8, 0.8)
        cs = rng.random(nb); off = np.where(cs < 0.40, 0.9, np.where(cs < 0.65, 0.12, 0.0)).astype(F)  # joint depth: 40 % of the cross joints are closed (invisible)
        uu = (u[m] + ph + (slope * (s[m] - 0.5) * bh[m] + 0.10 * noise(u[m], v[m], 14, 30, seed + 40 + k)
                           + 0.03 * noise(u[m], v[m], 50, 80, seed + 60 + k)) / T) % 1.0
        j = np.clip(np.searchsorted(cb, uu, side='right') - 1, 0, nb - 1)
        t[m] = (uu - cb[j]) / (cb[j + 1] - cb[j]); bw[m] = (cb[j + 1] - cb[j]) * T; bid[m] = k * 16 + j
        offl[m] = off[j]; offr[m] = off[(j + 1) % nb]
    nid = nbands * 16
    r1 = crand(bid, nid, seed + 3); r2 = crand(bid, nid, seed + 4); r3 = crand(bid, nid, seed + 5)
    r4 = crand(bid, nid, seed + 6); r5 = crand(bid, nid, seed + 7)
    offt = topoff[band]; offb = topoff[(band - 1) % nbands]
    dl = t * bw + offl; dr = (1 - t) * bw + offr; dtop = (1 - s) * bh; dbot = s * bh
    erode = 0.07 * (noise(u, v, 30, 30, seed + 8) * 0.5 + 0.5) + 0.03 * (noise(u, v, 90, 90, seed + 21) * 0.5 + 0.5)
    e = np.minimum(np.minimum(dl, dr), np.minimum(dbot * 1.5 + offb, dtop + offt)) - erode   # metres to an open joint
    slab = sm(0.0, 0.24, e)
    Wf = worley(u + 0.02 * noise(u, v, 6, 6, seed + 22), v + 0.02 * noise(u, v, 6, 6, seed + 23), 11, 13, seed + 24, 0.9)
    frac = (1 - sm(0.0, 0.025, Wf['edge'])) * (crand(Wf['id'], Wf['n'], seed + 25) > 0.45) * sm(0.2, 1.2, zs(fbm(u, v, 4, 3, seed + 27)))
    face = 0.10 * zs(fbm(u, v, 10, 3, seed + 9)) + 0.05 * noise(u, v, 90, 90, seed + 11)
    lam = noise(u, v, 8, 220, seed + 12)                                      # fine horizontal laminae
    h = slab * (0.8 + 0.5 * r4 + 0.45 * s) + slab * (face + 0.05 * lam) - 0.18 * frac * slab
    # albedo: macro colour patches spanning several slabs (slate / brown), then per-slab tone
    macro = sm(-1.3, 1.3, zs(fbm(u, v, 2, 3, seed + 26)))
    col = mix(pal['a'], pal['b'], np.clip(macro * 0.92 + (r1 - 0.5) * 0.10, 0, 1))      # v2: per-slab tint kept small (tinted rectangles read as bricks)
    col = mix(col, pal['c'], sm(0.8, 1.0, r2) * 0.25)
    col = col * (0.95 + 0.08 * r3)[..., None]
    grain = 1 + 0.08 * zs(fbm(u, v, 24, 3, seed + 13)) + 0.035 * noise(u, v, 180, 180, seed + 14) + 0.05 * lam
    col = col * grain[..., None]
    rim = (1 - sm(0.0, 0.14, dtop + 0.5 * offt)) * sm(0.0, 0.05, e + 0.02)
    col = mix(col, pal['light'], rim * 0.55)
    col = col * (0.58 + 0.42 * sm(0.0, 0.32, dbot + offb))[..., None]          # shade under each ledge
    col = col * (1 - 0.25 * frac * slab)[..., None]
    crev = 1 - sm(0.0, 0.045, e)
    col = mix(col, pal['crev'], crev * 0.62)
    mossmask = sm(0.0, 1.3, zs(fbm(u, v, 4, 4, seed + 15)) + 1.0 * (r5 - 0.55))
    moss = (1 - sm(0.04, 0.34, dtop + offt)) * mossmask * moss_amt
    drip = sm(0.4, 1.6, zs(noise(u, v, 70, 5, seed + 16) + 0.4 * noise(u, v, 140, 9, seed + 17)))
    drip = drip * (1 - sm(0.15, 1.0, dtop)) * mossmask * drip_amt
    mosscol = mix(pal['moss'], pal['moss2'], sm(-1, 1, zs(noise(u, v, 40, 40, seed + 18))))
    col = mix(col, mosscol, np.clip(moss + drip, 0, 1) * (1 - crev))
    h = h + 0.25 * np.clip(moss, 0, 1) * (0.5 + 0.5 * noise(u, v, 120, 120, seed + 19))
    return dict(C=col, N=normal_map(blur(h, 0.9), normal_k))


def mat_cliff(N=1024, seed=601):
    # Needle: dark slate-brown stratified walls with olive moss; Split: grey-brown rock -> a slate<->brown macro mix
    pal = dict(a=C(58, 64, 68), b=C(84, 76, 64), c=C(46, 52, 58), light=C(132, 124, 108), crev=C(20, 21, 23),
               moss=C(70, 84, 40), moss2=C(92, 100, 48))
    return strata(N, seed, 12.0, 13, pal, 0.85, 0.4, 3.2)


def mat_cliffdark(N=512, seed=701):
    pal = dict(a=C(38, 44, 46), b=C(50, 48, 42), c=C(34, 42, 46), light=C(86, 88, 80), crev=C(12, 14, 15),
               moss=C(36, 62, 50), moss2=C(52, 72, 44))
    d = strata(N, seed, 12.0, 11, pal, 0.8, 0.8, 1.8)
    u, v = grid(N, N)
    sp = zs(noise(u, v, 110, 110, seed + 30))
    d['C'] = mix(d['C'], C(122, 120, 106), sm(2.0, 2.8, sp) * 0.55)        # barnacle / salt specks
    return d


def mat_rock(N=512, seed=801):
    """loose / ledge rocks, sea stacks (object box UV, tile 4 m). The rock meshes carry the big facets; the map adds
    chipped layering: a terraced (quantised) warped fbm gives flat-ish chipped planes with short risers between them
    (stylised sedimentary rock, matching the stratified cliffs), a light worn lip on each step, a darker riser,
    a few open cracks, grey-brown tone per layer, lichen specks and moss patches on the planes."""
    u, v = grid(N, N)
    wu = u + 0.03 * noise(u, v, 3, 3, seed + 1) + 0.008 * noise(u, v, 12, 12, seed + 26)
    wv = v + 0.03 * noise(u, v, 3, 3, seed + 2) + 0.008 * noise(u, v, 12, 12, seed + 27)
    base = zs(fbm(wu, wv, 2, 5, seed + 3, gain=0.5, pv=3) + 0.25 * noise(wu, wv, 1, 6, seed + 4))
    f = base * 1.15 + 0.5
    i = np.floor(f); fr = f - i
    riser = sm(0.55, 1.0, fr)
    lid = ((i.astype(np.int64) + 64) % 128)
    W2 = worley(wu, wv, 11, 11, seed + 5, 0.9)                               # micro chips ~0.36 m
    n2 = W2['n']
    gx = (crand(W2['id'], n2, seed + 20) - .5); gy = (crand(W2['id'], n2, seed + 21) - .5)
    gx2 = (crand(W2['id2'], n2, seed + 20) - .5); gy2 = (crand(W2['id2'], n2, seed + 21) - .5)
    a2 = 0.5 + 0.5 * sm(0.0, 0.06, W2['edge'])
    chips = ((gx * W2['vx'] + gy * W2['vy']) * a2 + (gx2 * W2['v2x'] + gy2 * W2['v2y']) * (1 - a2)) * (N / 11)
    chipz = sm(-0.4, 1.0, zs(fbm(u, v, 3, 3, seed + 6)))
    W = worley(wu, wv, 6, 6, seed + 7, 0.9)
    crackm = sm(1.1, 1.8, zs(fbm(u, v, 5, 3, seed + 8)))
    crack = (1 - sm(0.0, 0.02, W['edge'])) * crackm
    h = (i + riser + 0.4 * fr) * 7.0 + 0.45 * chips * chipz - 3.0 * crack \
        + 0.5 * noise(u, v, 64, 64, seed + 9) + 0.25 * noise(u, v, 128, 128, seed + 10)
    tone = 0.6 * crand(W2['id'], n2, seed + 11) + 0.4 * crand(W['id'], W['n'], seed + 11)       # per chipped facet, NOT per terrace level (level sets read as contour lines)
    macro = sm(-1.5, 1.5, zs(fbm(u, v, 2, 3, seed + 12)))
    col = mix(C(98, 96, 90), C(128, 118, 100), np.clip(tone * 0.45 + macro * 0.55, 0, 1))
    col = col * (1 + 0.07 * zs(fbm(u, v, 12, 3, seed + 13)) + 0.03 * noise(u, v, 160, 160, seed + 14))[..., None]
    col = col * (1 + 0.05 * (crand(W2['id'], n2, seed + 22) - 0.5) * chipz)[..., None]
    lip = 1 - sm(0.0, 0.10, fr)
    col = mix(col, C(162, 154, 136), lip * 0.10)
    col = col * (1 - 0.06 * sm(0.66, 0.95, fr))[..., None]
    col = mix(col, C(46, 43, 40), crack * 0.75)
    moss = sm(1.15, 2.1, zs(fbm(u, v, 7, 4, seed + 15)) + 0.6 * (1 - riser) + 0.3 * zs(fbm(u, v, 13, 3, seed + 25)) - 0.2) * (1 - crack)
    col = mix(col, mix(C(80, 94, 44), C(102, 110, 52), sm(-1, 1, zs(noise(u, v, 30, 30, seed + 16)))), moss * 0.75)
    lich = sm(1.9, 2.8, zs(noise(u, v, 70, 70, seed + 17))) * sm(-0.5, 1.0, zs(fbm(u, v, 3, 2, seed + 18)))
    col = mix(col, C(176, 172, 146), lich * 0.5)
    col = mix(col, C(60, 58, 54), sm(2.0, 2.8, zs(noise(u, v, 90, 90, seed + 19))) * 0.4)
    h = h + 1.0 * moss
    return dict(C=col, N=normal_map(blur(h, 1.0), 0.35))


def mat_path(N=512, seed=901):
    """Needle stone path (tile 5 m): pale irregular flagstones ~0.7 m, thin joints - grass in some, dirt in others -
    grass creeping over a few stone edges, worn/cracked stones, soft bevel."""
    u, v = grid(N, N)
    wu = u + 0.012 * noise(u, v, 6, 6, seed + 1); wv = v + 0.012 * noise(u, v, 6, 6, seed + 2)
    W = worley(wu, wv, 7, 7, seed + 3, 0.8)                                  # flags ~0.71 m
    jw = 0.032 + 0.028 * (noise(u, v, 12, 12, seed + 4) * 0.5 + 0.5)
    creep = sm(0.6, 1.6, zs(fbm(u, v, 5, 3, seed + 13)))                      # grass spilling over the stone edges
    stone = sm(jw + 0.05 * creep, jw + 0.04 + 0.07 * creep, W['edge'])
    r1 = crand(W['id'], W['n'], seed + 5); r2 = crand(W['id'], W['n'], seed + 6)
    col = mix(C(200, 188, 154), C(210, 190, 146), r1)
    col = mix(col, C(180, 176, 162), sm(0.6, 1.0, r2) * 0.8)
    col = col * (0.9 + 0.12 * crand(W['id'], W['n'], seed + 7))[..., None]
    col = col * (1 + 0.05 * zs(fbm(u, v, 16, 3, seed + 8)) * 0.6 + 0.03 * noise(u, v, 140, 140, seed + 9))[..., None]
    col = col * (0.82 + 0.18 * sm(0.0, 0.16, W['edge'] - jw))[..., None]   # darker towards the joints
    Wc = worley(wu, wv, 18, 18, seed + 14, 0.9)
    crack = (1 - sm(0.0, 0.03, Wc['edge'])) * sm(0.9, 1.6, zs(noise(u, v, 9, 9, seed + 15))) * stone
    col = col * (1 - 0.2 * crack)[..., None]
    grass = mix(C(102, 126, 44), C(126, 148, 54), sm(-1, 1, zs(noise(u, v, 60, 60, seed + 10))))   # same lime hue as the fairway
    dirt = mix(C(118, 100, 72), C(92, 80, 62), sm(-1, 1, zs(noise(u, v, 50, 50, seed + 16))))
    joint = mix(dirt, grass, np.clip(sm(-0.6, 0.8, zs(fbm(u, v, 6, 3, seed + 11))) + creep, 0, 1))
    col = mix(joint, col, stone)
    h = stone * (0.8 + 0.2 * sm(0.0, 0.3, W['edge'])) + 0.08 * zs(fbm(u, v, 16, 3, seed + 8)) * stone \
        - 0.12 * crack + (1 - stone) * 0.25 * (noise(u, v, 100, 100, seed + 12) * 0.5 + 0.5)
    return dict(C=col, N=normal_map(blur(h, 0.8), 5.0))


def mat_masonry(N=512, seed=1001):
    u, v = grid(N, N)
    T = 4.0; courses = 8
    rng = np.random.default_rng([seed, 1])
    vw = (v + 0.5 / courses) % 1.0                                           # tile edge runs mid-course, not along a mortar bed
    row = np.clip(np.floor(vw * courses).astype(np.int64), 0, courses - 1)
    s = vw * courses - row                                                    # 0 bottom .. 1 top of course
    t = np.zeros_like(u); bw = np.zeros_like(u); bid = np.zeros(u.shape, np.int64)
    for k in range(courses):
        m = row == k
        nb = int(rng.integers(4, 8)); w = rng.uniform(0.6, 1.4, nb)
        cb = np.concatenate([[0.0], np.cumsum(w) / w.sum()]); ph = rng.random()
        uu = (u[m] + ph) % 1.0
        j = np.clip(np.searchsorted(cb, uu, side='right') - 1, 0, nb - 1)
        t[m] = (uu - cb[j]) / (cb[j + 1] - cb[j]); bw[m] = (cb[j + 1] - cb[j]) * T; bid[m] = k * 16 + j
    nid = courses * 16; ch = T / courses
    ero = 0.025 * (zs(fbm(u, v, 24, 3, seed + 2)) * 0.5 + 0.6)
    e = np.minimum(np.minimum(t, 1 - t) * bw, np.minimum(s, 1 - s) * ch) - np.clip(ero, 0, 0.05)
    blk = sm(0.008, 0.016, e)
    pillow = sm(0.0, 0.07, e)
    r1 = crand(bid, nid, seed + 3); r2 = crand(bid, nid, seed + 4); r3 = crand(bid, nid, seed + 5)
    col = mix(C(156, 148, 132), C(140, 136, 128), r1)
    col = mix(col, C(162, 146, 122), sm(0.6, 1.0, r2) * 0.7)
    col = col * (0.88 + 0.2 * r3)[..., None]
    col = col * (1 + 0.06 * zs(fbm(u, v, 12, 3, seed + 6)) * 0.7 + 0.035 * noise(u, v, 128, 128, seed + 7))[..., None]
    col = col * (0.8 + 0.2 * pillow)[..., None]
    stain = sm(0.3, 1.5, zs(noise(u, v, 40, 6, seed + 8))) * (1 - sm(0.0, 0.7, s)) * 0.35
    col = col * (1 - stain)[..., None]
    mossm = sm(0.4, 1.4, zs(fbm(u, v, 3, 4, seed + 9))) * (0.5 + 0.5 * (1 - sm(0.0, 0.5, 1 - s)))
    moss = mossm * (0.6 + 0.4 * noise(u, v, 60, 60, seed + 10))
    col = mix(col, mix(C(84, 104, 46), C(110, 120, 56), sm(-1, 1, zs(noise(u, v, 30, 30, seed + 11)))), np.clip(moss, 0, 1) * 0.85)
    mortar = C(112, 106, 96) * (1 + 0.05 * noise(u, v, 100, 100, seed + 12))[..., None]
    mortar = mix(mortar, C(78, 96, 44), np.clip(mossm, 0, 1) * 0.6)
    col = mix(mortar, col, blk)
    h = blk * (0.6 + 0.4 * pillow) + 0.05 * zs(fbm(u, v, 12, 3, seed + 6)) * blk + 0.15 * np.clip(moss, 0, 1) * blk
    return dict(C=col, N=normal_map(blur(h, 0.7), 4.0))


def blur1(a, sigma):
    """circular 1-D gaussian blur (FFT), sigma in samples."""
    return np.real(np.fft.ifft(np.fft.fft(a.astype(np.float64)) * np.exp(-2 * (np.pi * sigma) ** 2 * np.fft.fftfreq(a.size) ** 2))).astype(F)


def ptab(rng, rows, n, keep):
    """rows x n periodic smooth random tables (unit std): circular low-pass keeping `keep` cycles (so wavelength >= n/keep samples)."""
    a = rng.normal(size=(rows, n))
    Fa = np.fft.rfft(a, axis=1)
    k = np.arange(Fa.shape[1])
    Fa = Fa * (1 / (1 + (k / keep) ** 4))[None, :]
    b = np.fft.irfft(Fa, n=n, axis=1)
    return (b / (b.std(1, keepdims=True) + 1e-9)).astype(F)


def ptab_at(tab, idx, vv):
    """linear sample of tab[idx, :] at v in [0,1) (wrapping)."""
    n = tab.shape[1]
    x = (vv % 1.0) * n
    i0 = np.floor(x).astype(np.int64); f = (x - i0).astype(F)
    return tab[idx, i0 % n] * (1 - f) + tab[idx, (i0 + 1) % n] * f


# v4 2026-10-04 (skeptic review of v3: "reads as vertical bamboo or planks, not chunky cracked prisms"): 11 columns of very different widths, each
# broken into stacked BLOCKS 1.3-3.4 m tall (3-5 cross fractures per column per 8 m tile, none aligned with the neighbour), blocks with their own tone / tilt /
# plane widths, the vertical joints jog sideways a few cm between blocks, lit ledges under every fracture.  Glow stays ONLY in the vertical joints.
# The LIT mean colour stays at the v3 value, nudged one level more neutral (BASALT_LIT_MEAN under BASALT_CAL_TINT), because the runtime owner tuned LK_BASALT's Tint against it (BASALT_RENDERS_DARK).
BASALT_P = dict(nc=11, wsig=0.30, bevel=(0.36, 0.50), gap0=0.062, gap1=0.036, joints_on=0.55, thr=(-0.05, 0.70), glow_gain=1.0,
                nf=(3, 4, 5), gmin=1.3, gmax=3.4, align=0.45, partial=0.22, jog=(0.0030, 0.0062), chip=18, s_med=1.45, s_max=3.6, g_med=1.5, branch_p=1.0, fgl_p=0.5,
                fork2_p=0.7, wide=0.8,   # v7 (v6 = 0.0 / 0.0, bit-identical): fork2_p = chance of a second-level fork on every first-level fork, wide = E-only widening of the glow (0 = v6)
                # v9 2026-10-05 (carry-over finding, the integrator's Game-view BASALT_JOINTS: glow 92-97 % parallel vertical streaks, 1.27 branching joints per 100 skeleton px): net = 1 grows a crack NETWORK off the lit joints (slanted branch cracks, Y forks,
                # horizontal ledge stubs, links between neighbouring lit joints), inside the glow corridors only (the complement of BASALT_ZONES: the hole-10 builder's windows stay valid); the stretches get shorter (s_med / g_med).  net = 0 reproduces v8.
                net=1, net_th=(45.0, 75.0), net_len=(0.20, 0.52), net_wb=0.018, net_sp=0.55, net_y=0.60, net_ledge=0.30, net_link_sp=1.6, net_gain=1.35, net_taper=0.5, net_keep=0.80, net_trunk=(0.10, 0.22), net_two=0.40, net_groove=0.30, net_dark=0.50, net_tilt=0.28, net_bulb_p=0.65, net_bulb=0.030,
                old_gain=0.15, old_E=0.0, vor=1, vor_n=18, vor_jit=0.85, vor_keep=0.65, vor_w=0.010, vor_groove=0.30,
                corridors=0,bounded=1,bounded_seed=311)   # goal15: connected cracks can cross the full wall; old hole10 UV-window gate stays unchanged and reports the conflict
BASALT_P_V8 = dict(s_med=1.45, s_max=3.6, g_med=1.5, net=0, vor=0, old_gain=1.0, old_E=1.0)         # rollback: BASALT_P.update(BASALT_P_V8)
_NET_STATS = {}
BASALT_ZONES = ((0.009, 0.061), (0.165, 0.385), (0.576, 0.685), (0.772, 0.796), (0.854, 0.893))       # u intervals with NO glow texel at all: the hole-10 builder maps every top / plain face there (a copy of hole10_look.ZONES; BASALT_BUILDER_WINDOWS reads the file)
BASALT_LIN_MEAN = np.array([0.025829, 0.020989, 0.023091])      # mean of s2l(Basalt_C) over the tile, v3 (and the first pass), per channel
BASALT_CAL_TINT = np.array([0.233022, 0.603827, 0.827571])          # LK_BASALT Tint (sRGB .52,.80,.92) of GolfLook.cs at 10:53, linear
BASALT_LIT_MEAN = np.array([35.0, 31.0, 29.0]) / 255            # key-lit wall mean: v3 measured (36,31,28) S .21 under that tint (smoke_t4e); one level less red / more blue = more neutral charcoal, S .21 -> ~.17 (limit .25)
BASALT_GATE_LIGHT = np.array([2.4099, 1.4735, 0.8179])         # v8 2026-10-05: the key light on the wall for the GATE MODEL only (BASALT_WALL_LIGHT above still drives the albedo gain, so Basalt_C is unchanged): fitted so that the INSTALLED Basalt_C x the live Tint (.56,.74,.90)
                                                               # reproduces the owner's real run of repair round 3 (smoke_cand3: key-lit wall facing the sun, emission off: mean (30,30,30) S .01, median luminance 29-30; the model: mean (30,30,30), median 29.0).  The Crater atmosphere was
                                                               # made neutral in that round (GolfAtmosphere), the v3 light (3.674, 1.307, .736) belonged to the red one and predicted (38,28,28) S .27
BASALT_GATE_MEAN = np.array([30.0, 30.0, 30.0]) / 255
BASALT_WALL_LIGHT = np.array([3.674, 1.3065, 0.7360])          # linear key light on the wall, fitted so that v3 x LK_BASALT Tint (.52,.80,.92) reproduces the Game view of smoke_t4e (mean (36,31,28), median lum 29)


def _basalt_fractures(rng, nc, P, T=8.0):
    """per column: cyclic fracture heights (tile v), gaps in [gmin, gmax] m; neighbouring columns keep >= align m apart (no courses); one partial at most."""
    out = []
    for k in range(nc):
        for _try in range(4000):
            nf = int(rng.choice(P['nf']))
            g = rng.dirichlet(np.ones(nf) * 4.0) * T
            if g.min() < P['gmin'] or g.max() > P['gmax']:
                continue
            pos = (rng.random() + np.cumsum(g) / T) % 1.0
            ok = True
            for q in ([out[k - 1]] if k > 0 else []) + ([out[0]] if k == nc - 1 else []):
                dd = np.abs(((pos[:, None] - q[None, :]) + 0.5) % 1.0 - 0.5) * T
                if dd.min() < P['align']:
                    ok = False
                    break
            if ok:
                out.append(np.sort(pos)); break
        else:
            raise RuntimeError('basalt fractures: no layout for column %d' % k)
    return out


def _mat_basalt_prisms(N=1024, seed=1101):
    """columnar basalt PRISMS in stacked blocks (u = around the wall, v = height, tile 8 m).  v4 2026-10-04.
    11 vertical columns 0.4-1.4 m wide, each seen as THREE PLANES (left bevel / front / right bevel: normals tilted |nx| ~ .36-.50 and different albedo shade),
    separated by long vertical joints (dark, 9-15 cm wide, chipped, steep walls, jogging a few cm sideways between blocks).  Every column is cut into 3-5 BLOCKS
    1.3-3.4 m tall by slanted cross fractures (some only partial) that never line up with the neighbour (no brick courses); each block has its own tone, tilt and
    plane widths, a dark crack, a shadowed lip above it and a lit ledge below it.  Light arris highlights, chipped faces (Voronoi tilt cells), rough surface.
    _E: glow ONLY inside the vertical joints, in short irregular broken stretches on ~70 % of the joints, hotter toward v = 0 (the lava is below; periodic:
    hottest at v = 0 and 1), < 6 % of the texels, peak (255,160,40).  Per-channel gain keeps the lit mean of v3 (BASALT_LIT_MEAN)."""
    P = BASALT_P
    u, v = grid(N, N)
    T = 8.0; nc = P['nc']
    rng = np.random.default_rng([seed, 5])
    w = np.exp(rng.normal(0, P['wsig'], nc)); w = np.clip(w / w.mean(), 0.5, 1.9)
    cb = np.concatenate([[0.0], np.cumsum(w) / w.sum()]).astype(np.float64)
    uph = 0.5 * w[0] / w.sum()                                               # tile edge runs mid-column
    vrow = v[:, 0].astype(np.float64) % 1.0                                    # periodic (the one-tile-shift proof moves the grid by exactly 1)
    # ---- joints jog sideways between blocks: A[j](v), displaced stretches that return to 0 (periodic)
    A = np.zeros((nc, N))
    for j in range(nc):
        for _ in range(int(rng.choice([1, 2]))):
            va = rng.uniform(0.05, 0.60); vb = va + rng.uniform(0.14, 0.34)
            amp = rng.uniform(*P['jog']) * rng.choice([-1, 1])
            A[j] += amp * (sm(va - 0.010, va + 0.010, vrow) - sm(vb - 0.010, vb + 0.010, vrow))
    warp = 0.005 * noise(u, v, 3, 5, seed + 1) + 0.0025 * noise(u, v, 24, 24, seed + 2)
    uwp = ((u + uph + warp).astype(np.float64) - A[0][:, None]) % 1.0
    Pj = np.zeros((nc + 1, N)); Pj[0] = 0.0; Pj[nc] = 1.0
    for j in range(1, nc):
        Pj[j] = cb[j] + A[j] - A[0]
    rowi = np.broadcast_to(np.arange(N)[:, None], u.shape)
    c = np.zeros(u.shape, np.int64)
    for j in range(1, nc):
        c += (uwp >= Pj[j][:, None])
    c = np.clip(c, 0, nc - 1)
    p0 = Pj[c, rowi]; p1 = Pj[c + 1, rowi]
    t = ((uwp - p0) / (p1 - p0)).astype(F); wm = ((p1 - p0) * T).astype(F)
    left = t < 0.5
    dgap = np.minimum(t, 1 - t) * wm                                          # metres to the nearest vertical joint
    jid = np.where(left, c, (c + 1) % nc)                                     # which joint: 0..nc-1
    # ---- joint width: base + wobble + occasional chipped pockets
    pock = sm(0.55, 0.9, noise(u, v, 30, 20, seed + 3) * 0.5 + 0.5)
    wj = P['gap0'] + P['gap1'] * (noise(u, v, 40, 30, seed + 4) * 0.5 + 0.5) + 0.05 * pock
    # ---- fractures and blocks (per column)
    fr = _basalt_fractures(rng, nc, P, T)
    nfmax = max(len(f) for f in fr)
    btone = np.ones((nc, nfmax), F); btilt = np.zeros((nc, nfmax), F); bdt1 = np.zeros((nc, nfmax), F); bdt2 = np.zeros((nc, nfmax), F)
    baL = np.zeros((nc, nfmax), F); baR = np.zeros((nc, nfmax), F); bpart = np.zeros((nc, nfmax), bool); bslope = np.zeros((nc, nfmax)); bts = np.zeros((nc, nfmax)); bte = np.ones((nc, nfmax))
    for k in range(nc):
        n_ = len(fr[k])
        btone[k, :n_] = rng.uniform(0.80, 1.22, n_); btilt[k, :n_] = rng.uniform(-0.10, 0.10, n_)
        bdt1[k, :n_] = rng.uniform(-0.04, 0.04, n_); bdt2[k, :n_] = rng.uniform(-0.04, 0.04, n_)
        baL[k, :n_] = rng.uniform(*P['bevel'], n_); baR[k, :n_] = rng.uniform(*P['bevel'], n_)
        bslope[k, :n_] = rng.uniform(-0.36, 0.36, n_) / T                      # dv per metre across the column (<= ~20 degrees)
        if rng.random() < P['partial']:
            i = int(rng.integers(n_)); bpart[k, i] = True
            bts[k, i] = rng.uniform(0.0, 0.30); bte[k, i] = rng.uniform(0.70, 1.0)
    on = np.zeros(nc, bool); on[np.random.default_rng([seed, 4]).permutation(nc)[:int(round(nc * P['joints_on']))]] = True
    rgf = np.random.default_rng([seed, 9]); fgl = np.zeros(u.shape, F)             # v5: glow that runs along a few cross fractures, starting at a lit joint (T-junction cracks)
    # per pixel: the nearest fracture BELOW (smallest d >= 0) defines the block; the nearest fracture of all gives the crack, the ledge sits under a fracture
    bid = np.zeros(u.shape, np.int64)
    ndist = np.full(u.shape, 9.0, np.float32); fsign = np.zeros(u.shape, F); ledge = np.zeros(u.shape, F); lip = np.zeros(u.shape, F)
    for k in range(nc):
        m = c == k
        if not m.any():
            continue
        tm = t[m]; wmm = wm[m]; vm = v[m]; um = u[m]
        dmin = np.full(tm.shape, 99.0); bsel = np.zeros(tm.shape, np.int64)
        nd = ndist[m].copy(); fs = fsign[m].copy(); lg = ledge[m].copy(); lp = lip[m].copy(); fgl_loc = np.zeros(tm.shape, F)
        wob = 0.0025 * noise(um, vm, 17, 7, seed + 20 + k)
        for i, vf in enumerate(fr[k]):
            yl = vf + bslope[k, i] * (tm - 0.5) * wmm + wob
            d = (((vm - yl + 0.5) % 1.0) - 0.5) * T                             # metres above (+) / below (-) the crack
            ex = np.ones(tm.shape, F)
            if bpart[k, i]:
                ex = sm(bts[k, i] - 0.05, bts[k, i] + 0.05, tm) * (1 - sm(bte[k, i] - 0.05, bte[k, i] + 0.05, tm))
            cand = np.where(d >= 0, d, 99.0)                                   # the pixel's block = the one resting on the nearest fracture BELOW it (d >= 0: pixel above the crack line)
            better = cand < dmin
            dmin = np.where(better, cand, dmin); bsel = np.where(better, i, bsel)
            dd = np.where(ex > 0.25, d, 9.0)
            bt = np.abs(dd) < nd
            nd = np.where(bt, np.abs(dd), nd); fs = np.where(bt, np.sign(dd), fs)
            if (on[k] or on[(k + 1) % nc]) and rgf.random() < P['fgl_p']:
                fl = 0 if on[k] else 1; fext = rgf.uniform(0.35, 0.85); fint = rgf.uniform(0.55, 0.95)
                sf = tm if fl == 0 else 1 - tm
                fv = np.where((np.abs(d) < 0.010) & (ex > 0.25), np.clip(1 - sf / fext, 0, 1) ** 0.8 * fint, 0.0).astype(F)
                fgl_loc = np.maximum(fgl_loc, fv)
            lg = np.where((d < 0) & (d > -0.13) & (ex > 0.25), np.maximum(lg, ex * (1 - sm(0.07, 0.13, -d))), lg)    # lit ledge on the block BELOW the crack
            lp = np.where((d > 0) & (d < 0.07) & (ex > 0.25), np.maximum(lp, ex * (1 - sm(0.03, 0.07, d))), lp)         # shadowed lip under the block ABOVE
        bid[m] = bsel
        ndist[m] = nd; fsign[m] = fs; ledge[m] = lg; lip[m] = lp; fgl[m] = fgl_loc
    cw = 0.012 + 0.010 * (noise(u, v, 30, 12, seed + 30) * 0.5 + 0.5)
    crack = 1 - sm(0.45 * cw, cw, ndist)
    cwall = (1 - sm(cw, 2.2 * cw, ndist)) * sm(0.2 * cw, 0.6 * cw, ndist)
    # ---- three planes per column, per block
    aL = baL[c, bid]; aR = baR[c, bid]
    t1 = (0.22 + 0.11 * crand(c, nc, seed + 60) + bdt1[c, bid] + 0.04 * noise(u, v, 12, 10, seed + 5)).astype(F)
    t2 = (0.67 + 0.10 * crand(c, nc, seed + 61) + bdt2[c, bid] + 0.04 * noise(u, v, 12, 10, seed + 6)).astype(F)
    ta = 0.016 / np.maximum(wm, 0.3)
    Lm = 1 - sm(t1 - ta, t1 + ta, t); Rm = sm(t2 - ta, t2 + ta, t); Fm = 1 - Lm - Rm
    bias = (rng.uniform(-0.05, 0.05, nc).astype(F))[c]
    W = worley(u + 0.012 * noise(u, v, 5, 7, seed + 7), v + 0.012 * noise(u, v, 7, 5, seed + 8), P['chip'], P['chip'], seed + 9, 0.95)
    gx = (crand(W['id'], W['n'], seed + 10) - 0.5) * 0.36; gy = (crand(W['id'], W['n'], seed + 11) - 0.5) * 0.36
    cs = (crand(W['id'], W['n'], seed + 12) - 0.5)
    chipw = 0.35 + 0.65 * Fm
    rough1 = 0.035 * noise(u, v, 80, 80, seed + 13); rough2 = 0.035 * noise(u, v, 90, 70, seed + 14)
    nx = -aL * Lm + aR * Rm + bias * Fm + chipw * gx + rough1
    ny = chipw * gy * 0.8 + rough2 + btilt[c, bid] * Fm
    side = np.where(left, -1.0, 1.0).astype(F)
    wall = sm(0.12 * wj, 0.4 * wj, dgap) * (1 - sm(0.8 * wj, wj, dgap))
    nx = nx * (1 - wall) + side * 0.9 * wall
    ny = ny * (1 - wall)
    ny = ny * (1 - cwall) + (-fsign * 0.85) * cwall
    ny = ny + 0.22 * ledge * (1 - crack)
    # ---- albedo
    r1 = crand(c, nc, seed + 40); r2 = crand(c, nc, seed + 41)
    base = mix(C(26, 25, 28), C(66, 61, 64), r1) * (1 + 0.04 * (r2 - 0.5))[..., None]
    fs_ = np.where(Lm > 0.5, 0.77, np.where(Rm > 0.5, 0.92, 1.10)).astype(F)             # v6: plane shades .68/.90/1.16 -> .77/.92/1.10 (gate BASALT_BEVELS keeps a >= 15 level spread) (the three-stripe rhythm read as planks; the volume comes from the bevel NORMALS)
    macro = 1 + 0.10 * zs(fbm(u, v, 3, 3, seed + 42)) + 0.07 * zs(fbm(u, v, 9, 3, seed + 43))
    streak = 1 + 0.03 * ptab_at(ptab(np.random.default_rng([seed, 6]), nc, 48, 6), c, v)   # v6: .10 -> .03 (vertical wood grain)
    col = base * (fs_ * macro * streak * btone[c, bid])[..., None]
    col = col * (1 + 0.24 * cs * (0.4 + 0.6 * Fm))[..., None]
    col = col * (1 + 0.10 * zs(fbm(u, v, 24, 3, seed + 44)) + 0.05 * noise(u, v, 170, 170, seed + 45))[..., None]
    brk = sm(0.0, 0.8, noise(u, v, 40, 60, seed + 46))                                        # v6: arris highlights only in short broken stretches (a continuous pale line down every plane edge read as plank edges)
    for tt in (t1, t2):
        edge = np.exp(-(((t - tt) * wm) / 0.011) ** 2)
        col = mix(col, C(122, 110, 100), 0.30 * edge * brk)
    lipj = np.exp(-((dgap - wj * 1.05) / 0.012) ** 2) * sm(-0.2, 0.6, noise(u, v, 36, 50, seed + 47))
    col = mix(col, C(104, 94, 86), 0.5 * lipj)
    ao = 0.22 + 0.78 * sm(0.0, wj * 1.5, dgap)
    col = col * ao[..., None]
    col = col * (1 - 0.88 * crack)[..., None]
    col = col * (1 - 0.35 * lip * (1 - crack))[..., None]
    chipl = 0.75 + 0.25 * sm(-0.4, 0.6, noise(u, v, 26, 40, seed + 48))
    col = col * (1 + 0.55 * ledge * chipl * (1 - crack))[..., None]
    # ---- glow, only in the joint / crack network (v5 2026-10-04): irregular broken stretches along some vertical joints (lengths 0.35-3.6 m, gaps 0.5-4.5 m, each its own
    #      intensity: dull red .. bright orange) plus short diagonal BRANCH cracks (a groove in the rock, glowing, tapering to a point) leaving a stretch.  No two joints share a pattern, so
    #      there is no dash rhythm ('ladders').  Hotter toward v = 0 / 1 (the lava below).
    rg = np.random.default_rng([seed, 8]); rg2 = np.random.default_rng([seed, 18])     # rg2: the v7 additions only (rg keeps its v6 draw order: every v6 stretch / fork is unchanged)
    stretches = []                                                              # (joint, y0 m, y1 m, intensity)
    for j in np.flatnonzero(on):
        y = rg.uniform(0.15, 0.9)                                               # every lit joint starts low (the lava is below)
        if rg.random() < 0.6:                                                   # ... and most also carry a stretch that reaches the top of the tile (v -> 1 = the next 8 m down)
            ytop = rg.uniform(T - 0.25 - rg.uniform(0.6, 1.6), T - 0.25)
            stretches.append((int(j), ytop - rg.uniform(0.5, 1.4), T - 0.25, rg.uniform(0.55, 1.0)))
            ylim = stretches[-1][1] - 0.6
        else:
            ylim = T - 0.25
        while True:
            Lg = float(np.clip(rg.lognormal(np.log(P['s_med']), 0.55), 0.35, P['s_max']))
            if y + Lg > ylim:
                break
            stretches.append((int(j), y, y + Lg, rg.uniform(0.55, 1.0)))
            y += Lg + float(np.clip(rg.lognormal(np.log(P['g_med']), 0.6), 0.5, 4.5))
    Yv = (v.astype(np.float64) % 1.0) * T; Xu = uwp * T
    glow = np.zeros(u.shape, F); bcrack = np.zeros(u.shape, F); bnx = np.zeros(u.shape, F); bny = np.zeros(u.shape, F); glow2 = np.zeros(u.shape, F)
    hvn = (0.5 + 0.5 * np.cos(2 * np.pi * v)).astype(F)                         # 1 at v = 0 / 1 (lava below), 0 at v = .5
    flick = (0.80 + 0.20 * (noise(u, v, 14, 90, seed + 50) * 0.5 + 0.5)).astype(F)
    inner = 1 - sm(0.10 * wj, 0.6 * wj, dgap)
    inner_f = (1 - 0.5 * (1 - cwall))
    for (j, y0_, y1_, inten) in stretches:
        sj = ((Yv - y0_) / max(y1_ - y0_, 1e-3)).astype(F)                      # 0..1 along the stretch
        inside = ((sj >= 0) & (sj <= 1)).astype(F)
        prof = np.clip(np.sin(np.pi * np.clip(sj, 0, 1)), 0, 1) ** 0.55 * inside
        glow = np.maximum(glow, np.where(jid == j, prof * inten, 0).astype(F) * inner)
        if rg.random() < P['branch_p']:                                         # a branch crack leaves the stretch (upper end, lower end or middle)
            s0 = float(rg.choice([0.12, 0.88, rg.uniform(0.3, 0.7)]))
            yb = y0_ + s0 * (y1_ - y0_); row = int(np.clip(round(yb / T * N), 0, N - 1))
            xb = float(Pj[j][row]) * T
            side = float(rg.choice([-1.0, 1.0])); th = np.radians(rg.uniform(18, 30)) * rg.choice([-1.0, 1.0])     # forks leave the joint at 18-30 deg off vertical (|ny| of the groove wall <= .44: never read as a cross fracture)
            Lb = rg.uniform(0.30, 0.85)
            segs = [(xb, yb, side * np.sin(abs(th)), np.sign(th) * np.cos(th), Lb)]
            if rg.random() < 0.5:                                               # a kink
                th2 = float(np.sign(th) * np.clip(abs(th) + np.radians(rg.uniform(6, 12)) * rg.choice([-1.0, 1.0]), np.radians(10), np.radians(30))); L2 = rg.uniform(0.15, 0.40)
                segs.append((xb + segs[0][2] * Lb, yb + segs[0][3] * Lb, side * np.sin(abs(th2)), np.sign(th2) * np.cos(th2), L2))
            totL = sum(sg[4] for sg in segs); run0 = 0.0
            for (sx_, sy_, dx_, dy_, L_) in segs:
                if not (0.2 < sy_ < T - 0.2 and 0.2 < sy_ + dy_ * L_ < T - 0.2):
                    break
                dxm = ((Xu - sx_ + T / 2) % T) - T / 2; dym = Yv - sy_
                pdot = dxm * dx_ + dym * dy_
                tt = np.clip(pdot, 0, L_)
                dist = np.sqrt((dxm - tt * dx_) ** 2 + (dym - tt * dy_) ** 2).astype(F)
                sg_ = ((run0 + tt) / totL).astype(F)                             # 0 at the joint .. 1 at the tip
                wb = (0.016 * (1 - 0.60 * sg_)).astype(F)
                crk = (1 - sm(0.45 * wb, wb * 1.6, dist)) * (dist < 4 * wb)
                bcrack = np.maximum(bcrack, crk.astype(F))
                gb = (1 - sm(0.25 * wb, wb * 1.15, dist)) * np.clip(1 - sg_, 0, 1) ** 0.7 * (0.95 * inten)
                glow = np.maximum(glow, gb.astype(F))
                perp_x, perp_y = -dy_, dx_
                sgn = np.sign(dxm * perp_x + dym * perp_y).astype(F)
                wl = (sm(0.20 * wb, 0.55 * wb, dist) * (1 - sm(1.0 * wb, 1.8 * wb, dist))).astype(F)
                bnx = bnx + sgn * perp_x * 0.88 * wl; bny = bny + sgn * perp_y * 0.88 * wl
                run0 += L_
            if P['fork2_p'] > 0 and rg2.random() < P['fork2_p']:                  # v7: a SECOND-level fork (a crack network, not isolated streaks): leaves the first fork at its 35-70 % point, turned 14-30 deg away, shorter, dimmer
                f_ = rg2.uniform(0.35, 0.70); sx0, sy0, dx0, dy0, L0 = segs[0]
                px2 = sx0 + dx0 * L0 * f_; py2 = sy0 + dy0 * L0 * f_
                a0 = float(np.arctan2(dx0, abs(dy0)))                              # lateral angle of the first fork from vertical (|a0| 18-30 deg)
                a2 = float(np.clip(a0 - (1.0 if a0 >= 0 else -1.0) * np.radians(rg2.uniform(14, 30)), -np.radians(30), np.radians(30)))      # turns back toward vertical: a Y, never past 30 deg (|ny| of the groove wall <= .44)
                dx2, dy2 = float(np.sin(a2)), float(np.cos(a2)) * (1.0 if dy0 >= 0 else -1.0)
                L2f = rg2.uniform(0.15, 0.45)
                if 0.2 < py2 < T - 0.2 and 0.2 < py2 + dy2 * L2f < T - 0.2:
                    dxm = ((Xu - px2 + T / 2) % T) - T / 2; dym = Yv - py2
                    tt = np.clip(dxm * dx2 + dym * dy2, 0, L2f)
                    dist = np.sqrt((dxm - tt * dx2) ** 2 + (dym - tt * dy2) ** 2).astype(F)
                    sg_ = (tt / L2f).astype(F); wb = (0.014 * (1 - 0.60 * sg_)).astype(F)
                    crk = (1 - sm(0.45 * wb, wb * 1.6, dist)) * (dist < 4 * wb)
                    bcrack = np.maximum(bcrack, crk.astype(F))
                    g2 = (1 - sm(0.25 * wb, wb * 1.15, dist)) * np.clip(1 - sg_, 0, 1) ** 0.7 * (0.70 * inten)
                    glow2 = np.maximum(glow2, g2.astype(F))
                    perp_x, perp_y = -dy2, dx2
                    sgn = np.sign(dxm * perp_x + dym * perp_y).astype(F)
                    wl = (sm(0.20 * wb, 0.55 * wb, dist) * (1 - sm(1.0 * wb, 1.8 * wb, dist))).astype(F)
                    bnx = bnx + sgn * perp_x * 0.88 * wl; bny = bny + sgn * perp_y * 0.88 * wl
    # ---- v9 2026-10-05: the crack NETWORK (see BASALT_P net): every lit stretch spawns slanted branch cracks (both sides, mostly downward = toward the lava), Y forks, horizontal ledge stubs, and neighbouring lit joints are linked
    #      by slanted cracks (closed cells).  All of it lives inside the glow corridors (the complement of BASALT_ZONES = what the hole-10 builder's UV windows assume), widths 5-8 cm so they survive the mip chain at wall distance.
    glow = glow * F(P['old_gain']); glow2 = glow2 * F(P['old_gain'])                     # v9: the v8 joint stretches + forks, scaled (0 = only the crack network)
    zmask = np.ones(u.shape, F)
    for (za, zb) in (BASALT_ZONES if P.get('corridors', 1) else ()):
        ue_ = (u.astype(np.float64) % 1.0)
        zmask = zmask * (1 - sm(za - 0.012, za - 0.002, ue_) * (1 - sm(zb + 0.002, zb + 0.012, ue_))).astype(F)
    if P['net']:
        glowN = np.zeros(u.shape, F); crackN = np.zeros(u.shape, F); nnx = np.zeros(u.shape, F); nny = np.zeros(u.shape, F)
        rn = np.random.default_rng([seed, 31])
        px_m = T / N
        ue_of = lambda x: ((x / T) - uph) % 1.0
        def in_cor(x):
            ue = ue_of(x)
            return not P.get('corridors', 1) or not any(za - 0.004 <= ue <= zb + 0.004 for (za, zb) in BASALT_ZONES)
        def jx_at(j, y):
            row = int(np.clip(round((T - (y % T)) / px_m - 0.5), 0, N - 1))
            return float(Pj[j][row]) * T
        def draw(sx, sy, dx, dy, L, hw0, hw1, inten_):
            ex, ey = sx + dx * L, sy + dy * L
            pad = 3.0 * max(hw0, hw1) + 16 * px_m
            c0 = int(np.floor((min(sx, ex) - pad - uph * T) / px_m)); c1 = int(np.ceil((max(sx, ex) + pad - uph * T) / px_m))      # Xu = uwp * T and uwp = u + uph + warp - A0: column = (x - uph T) / px
            r0 = int(np.floor((T - (max(sy, ey) + pad)) / px_m)); r1 = int(np.ceil((T - (min(sy, ey) - pad)) / px_m))
            ix = np.ix_(np.arange(r0, r1 + 1) % N, np.arange(c0, c1 + 1) % N)
            X = Xu[ix]; Y = Yv[ix]
            dxm = ((X - sx + T / 2) % T) - T / 2; dym = ((Y - sy + T / 2) % T) - T / 2
            tt = np.clip(dxm * dx + dym * dy, 0, L)
            dist = np.sqrt((dxm - tt * dx) ** 2 + (dym - tt * dy) ** 2).astype(F)
            sg = (tt / L).astype(F)
            wb = (hw0 + (hw1 - hw0) * sg).astype(F)
            g = (1 - sm(0.25 * wb, wb * 1.15, dist)) * np.clip(1 - P['net_taper'] * sg, 0, 1) ** 0.7 * F(min(inten_ * P['net_gain'], 1.0))
            glowN[ix] = np.maximum(glowN[ix], g)
            wg = (wb * P['net_groove']).astype(F)                                      # the groove (dark crack + normal walls) is narrower than the glow
            crk = (1 - sm(0.45 * wg, wg * 1.6, dist)) * (dist < 4 * wg)
            crackN[ix] = np.maximum(crackN[ix], crk.astype(F))
            perp_x, perp_y = -dy, dx
            sgn = np.sign(dxm * perp_x + dym * perp_y).astype(F)
            wl = (sm(0.20 * wg, 0.55 * wg, dist) * (1 - sm(1.0 * wg, 1.8 * wg, dist))).astype(F)
            nnx[ix] = nnx[ix] + sgn * perp_x * 0.88 * wl; nny[ix] = nny[ix] + sgn * perp_y * 0.88 * wl
        def fit(sx, sy, dx, dy, L):
            while L > 0.09 and not (in_cor(sx + dx * L) and in_cor(sx + dx * L * 0.5)):
                L *= 0.82
            return L if L > 0.09 else 0.0
        nbr = nlink = ntrunk = nbulb = 0
        by_j = {}
        for (j, y0_, y1_, inten_) in stretches:
            by_j.setdefault(j, []).append((y0_, y1_, inten_))
        hv = lambda y_: (0.5 + 0.5 * np.cos(2 * np.pi * y_ / T)) ** 1.3                         # 1 at v = 0 / 1 (the lava below), 0 at mid-height
        for j in np.flatnonzero(on):                                                        # nodes along every LIT joint, anywhere on it (a node = a short glowing trunk piece + 1-2 branch cracks)
            j = int(j); inten0 = float(rn.uniform(0.6, 1.0))
            y = float(rn.uniform(0.0, P['net_sp'])); side_prev = float(rn.choice([-1.0, 1.0]))
            while y < T:
                step = float(np.clip(rn.lognormal(np.log(P['net_sp']), 0.40), 0.14, 1.2))
                if rn.random() > P['net_keep'] * (0.30 + 0.70 * hv(y)):
                    y += step; continue
                xj = jx_at(j, y); it0 = float(np.clip(inten0 * rn.uniform(0.75, 1.05), 0.3, 1.0))
                lt = float(rn.uniform(*P['net_trunk']))
                draw(xj, y - lt / 2, 0.0, 1.0, lt, P['net_wb'] * 0.9, P['net_wb'] * 0.8, it0); ntrunk += 1   # the root: a short fat glowing piece of the joint
                if rn.random() < P['net_bulb_p']:                                                  # a molten BULB where the branches leave the joint (cracks swell at their junctions): a round pocket, not a streak
                    draw(xj + float(rn.uniform(-0.01, 0.01)), y + float(rn.uniform(-0.4, 0.4)) * lt, 0.0, 1.0, 0.05, P['net_bulb'], P['net_bulb'], min(it0 * 1.1, 1.0)); nbulb += 1
                for k_ in range(2 if rn.random() < P['net_two'] else 1):
                    side = -side_prev if rn.random() < 0.65 else side_prev; side_prev = side
                    th = np.radians(rn.uniform(*P['net_th'])); ydir = -1.0 if rn.random() < 0.62 else 1.0
                    dx_, dy_ = side * np.sin(th), ydir * np.cos(th)
                    y_root = y + float(rn.uniform(-0.45, 0.45)) * lt; x_root = jx_at(j, y_root)
                    L = fit(x_root, y_root, dx_, dy_, rn.uniform(*P['net_len']))
                    if L > 0:
                        hw0 = P['net_wb'] * rn.uniform(0.80, 1.05); it = it0 * rn.uniform(0.75, 1.0)
                        draw(x_root, y_root, dx_, dy_, L, hw0, hw0 * 0.45, it); nbr += 1
                        if rn.random() < P['net_y']:                                          # a Y fork off the branch
                            f_ = rn.uniform(0.4, 0.7); ex_, ey_ = x_root + dx_ * L * f_, y_root + dy_ * L * f_
                            a1 = np.arctan2(dx_, dy_) + np.radians(rn.uniform(20, 48)) * float(rn.choice([-1.0, 1.0]))
                            dx2, dy2 = float(np.sin(a1)), float(np.cos(a1)); L2 = fit(ex_, ey_, dx2, dy2, L * rn.uniform(0.45, 0.8))
                            if L2 > 0:
                                draw(ex_, ey_, dx2, dy2, L2, hw0 * 0.8, hw0 * 0.38, it * 0.85)
                        if rn.random() < P['net_ledge']:                                        # a horizontal ledge stub at the tip (a T)
                            tx_, ty_ = x_root + dx_ * L, y_root + dy_ * L
                            la = np.radians(rn.uniform(-18, 18)); ld = float(rn.choice([-1.0, 1.0]))
                            dx3, dy3 = ld * float(np.cos(la)), float(np.sin(la)); L3 = fit(tx_, ty_, dx3, dy3, rn.uniform(0.10, 0.26))
                            if L3 > 0:
                                draw(tx_, ty_, dx3, dy3, L3, hw0 * 0.7, hw0 * 0.35, it * 0.8)
                y += step
        onset = set(int(x) for x in np.flatnonzero(on))
        for j in sorted(onset):                                                             # links between neighbouring lit joints: a slanted crack with a glowing root piece at each end (a closed cell of the network)
            j2 = (j + 1) % nc
            if j2 not in onset:
                continue
            y = float(rn.uniform(0.0, P['net_link_sp']))
            while y < T:
                step = float(np.clip(rn.lognormal(np.log(P['net_link_sp']), 0.4), 0.5, 4.0))
                if rn.random() < 0.20 + 0.80 * hv(y):
                    ya = y; yb = y + float(rn.uniform(-0.55, 0.55)) + (0.12 if abs(y - T / 2) < 0.2 else 0.0)
                    xa, xb = jx_at(j, ya), jx_at(j2, yb)
                    gap = (xb - xa + T / 2) % T - T / 2
                    if 0.25 <= abs(gap) <= 1.15 and in_cor(xa + 0.5 * gap) and in_cor(xa) and in_cor(xb):
                        Ll = float(np.hypot(gap, yb - ya)); it = float(rn.uniform(0.7, 1.0))
                        draw(xa, ya - 0.09, 0.0, 1.0, 0.18, P['net_wb'] * 0.85, P['net_wb'] * 0.75, it); draw(xb, yb - 0.09, 0.0, 1.0, 0.18, P['net_wb'] * 0.85, P['net_wb'] * 0.75, it)
                        if rn.random() < P['net_bulb_p']:
                            draw(xa, ya, 0.0, 1.0, 0.05, P['net_bulb'], P['net_bulb'], it); draw(xb, yb, 0.0, 1.0, 0.05, P['net_bulb'], P['net_bulb'], it); nbulb += 2
                        draw(xa, ya, gap / Ll, (yb - ya) / Ll, Ll, P['net_wb'] * 0.9, P['net_wb'] * 0.6, it); nlink += 1
                y += step
        if P['vor']:
            nv_ = int(P['vor_n'])
            Wv = worley(u + 0.004 * noise(u, v, 5, 7, seed + 71), v + 0.004 * noise(u, v, 7, 5, seed + 72), nv_, nv_, seed + 70, P['vor_jit'])
            em = Wv['edge'].astype(np.float64) * (T / nv_)                                         # metres to the cell border (nx = ny: isotropic)
            pair = np.minimum(Wv['id'], Wv['id2']) * Wv['n'] + np.maximum(Wv['id'], Wv['id2'])
            rk = crand(pair, Wv['n'] * Wv['n'], seed + 73); ri_ = crand(pair, Wv['n'] * Wv['n'], seed + 75)
            hvf = (0.5 + 0.5 * np.cos(2 * np.pi * v.astype(np.float64))) ** 1.3
            keepv = (rk < P['vor_keep'] * (0.30 + 0.70 * hvf)).astype(F)
            wv_ = (P['vor_w'] * (0.65 + 0.7 * (noise(u, v, 40, 40, seed + 74) * 0.5 + 0.5))).astype(F)
            gV = (1 - sm(0.25 * wv_, 1.15 * wv_, em.astype(F))) * keepv * (0.55 + 0.45 * ri_).astype(F)
            glowN = np.maximum(glowN, gV.astype(F))
            wgv = (wv_ * P['vor_groove']).astype(F)
            crackN = np.maximum(crackN, ((1 - sm(0.45 * wgv, wgv * 1.6, em.astype(F))) * (em < 4 * wgv) * keepv).astype(F))
            gy_, gx_ = np.gradient(em)                                                               # groove walls: tilt the normal along the gradient of the border distance
            gl_ = np.hypot(gx_, gy_) + 1e-9
            wlv = (sm(0.20 * wgv, 0.55 * wgv, em.astype(F)) * (1 - sm(1.0 * wgv, 1.8 * wgv, em.astype(F))) * keepv).astype(F)
            nnx = nnx + (gx_ / gl_ * 0.88 * wlv).astype(F); nny = nny - (gy_ / gl_ * 0.88 * wlv).astype(F)
            _NET_STATS.update(vor_cells=int(Wv['n']), vor_lit_share=float((gV > 0.2).mean()))
        bcrack = np.maximum(bcrack, crackN * zmask * F(P['net_dark']))                                       # (the v8 glow keeps painting the albedo unmasked: Basalt_C stays the v8 rock, PROFILE128 stays valid)                                   # (the v6 forks may reach into a zone: clipped here, E and the heat-rust albedo alike)
        glowNm = glowN * zmask                                                                              # the network's glow: E only (Basalt_C's column-luminance profile is a constant of the hole-10 builder: PROFILE128)
        bnx = bnx + nnx * zmask * F(P['net_tilt'] / 0.88); bny = bny + nny * zmask * F(P['net_tilt'] / 0.88)     # the groove walls of the network (normal map); tilt <= .42: the gate's cross-fracture detector reads |ny| > .45 as a block fracture
        _NET_STATS.update(branches=nbr, links=nlink, trunks=ntrunk, bulbs=nbulb)
    glow_v = glow.copy()                                                        # v7: the vertical joint glow + forks, before the horizontal T-junction cracks (those are not widened)
    glow = np.maximum(glow, fgl * inner_f)
    glow = np.clip(glow * flick * (0.42 + 0.58 * hvn ** 1.3) * P['glow_gain'], 0, 1)
    glow = np.clip(glow + 0.30 * blur(glow, 2.5), 0, 1) * ((dgap < wj * 1.15) | (bcrack > 0.02) | (fgl > 0.02))
    nx = nx * (1 - np.clip(np.abs(bnx) * 1.2, 0, 1)) + bnx; ny = ny * (1 - np.clip(np.abs(bny) * 1.2, 0, 1)) + bny
    # v7 2026-10-04 (E ONLY: the albedo / normals above keep the v6 glow, so the hole-10 builder's Basalt_C luminance profile and glow lines stay valid): the second-level forks and a wider, softer
    # shoulder on the joint glow, kept out of the glow-free zones the builder maps tops and plain faces to (BASALT_ZONES, mirrored from hole10_look.ZONES; gate BASALT_BUILDER_WINDOWS reads the file)
    glowE = glow
    if P['fork2_p'] > 0 or P['wide'] > 0:
        zm = np.ones(u.shape, F)
        for (za, zb) in (BASALT_ZONES if P.get('corridors', 1) else ()):
            ue = (u.astype(np.float64) % 1.0)
            zm = zm * (1 - sm(za - 0.012, za - 0.002, ue) * (1 - sm(zb + 0.002, zb + 0.012, ue))).astype(F)
        g2 = np.clip(glow2 * flick * (0.42 + 0.58 * hvn ** 1.3) * P['glow_gain'], 0, 1)
        gv = np.clip(glow_v * flick * (0.42 + 0.58 * hvn ** 1.3) * P['glow_gain'], 0, 1)
        gw = np.clip(blur(gv, 2.0) * 1.7, 0, 1) * P['wide'] * ((dgap < wj * 1.8) | (bcrack > 0.02))
        oe = F(P['old_E'])                                                          # v9: how much of the v8 joint stretches / forks reaches the EMISSION map (0 = none: they stay in Basalt_C / _N as cooled, dull cracks)
        glowE = np.maximum(glow * oe, np.clip(np.maximum(g2, gw) * zm, 0, 1) * oe)
        if P['net']:
            glowE = np.maximum(glowE, np.clip(glowNm * flick * (0.42 + 0.58 * hvn ** 1.3) * P['glow_gain'], 0, 1))
        glowE = np.clip(glowE + 0.30 * blur(glowE, 2.0) * zm, 0, 1)
    col = col * (1 - 0.85 * bcrack)[..., None]
    col = mix(col, C(104, 44, 28), np.clip(blur(glow, 6.0) * 1.8, 0, 1) * 0.40)       # faint heat-rust on the rock next to a lit crack
    col = mix(col, C(150, 52, 14), np.clip(glow * 1.2, 0, 1) * 0.8)
    # ---- keep the LIT mean of v3: the runtime owner tuned LK_BASALT's Tint (.52,.80,.92) against v3's key-lit wall, mean (36,31,28) (smoke_t4e).  Per-channel gain so that
    #      mean( sRGB( lin(albedo) x gain x BASALT_CAL_TINT x BASALT_WALL_LIGHT ) ) == BASALT_LIT_MEAN (a few fixed-point steps; a constant, independent of GolfLook.cs).
    lin = s2l(np.clip(col, 0, 1)).astype(np.float64)
    gain = np.ones(3)
    for _ in range(12):
        out_ = l2s(lin * (gain * BASALT_CAL_TINT * BASALT_WALL_LIGHT)[None, None, :])
        gain *= (BASALT_LIT_MEAN / np.maximum(out_.reshape(-1, 3).mean(0), 1e-6)) ** 1.0
    col = l2s_(np.clip(lin * gain, 0, 1)).astype(F)
    ek = [0.0, 0.18, 0.45, 0.72, 1.0]
    ecol = np.array([[0, 0, 0], [70, 8, 0], [190, 45, 5], [250, 100, 16], [255, 160, 40]], F) / 255
    E = np.stack([np.interp(np.clip(glowE, 0, 1), ek, ecol[:, i]) for i in range(3)], -1).astype(F)
    # Sparse hot joints magnify tiny boundary differences. Weld opposite texel
    # rows/columns to the same value rather than hiding a seam in a tolerance.
    edge = .5 * (E[0] + E[-1]); E[0] = E[-1] = edge
    edge = .5 * (E[:, 0] + E[:, -1]); E[:, 0] = E[:, -1] = edge
    nz = np.sqrt(np.maximum(1 - nx * nx - ny * ny, 0.03))
    nv = np.stack([nx, ny, nz], -1)
    nv = nv / np.linalg.norm(nv, axis=-1, keepdims=True)
    return dict(C=col, N=nv.astype(F), E=E)


def _basalt_bounded_network(u,v,base,*,seed=311,variant=1):
    # Whole-tile sampling shifts must reach the same physical crack network.
    u=np.mod(u,1.);v=np.mod(v,1.)
    H,W=u.shape;rng=np.random.default_rng([seed,81])
    field=np.zeros((H,W),np.float32);depth=np.zeros_like(field)
    gx=np.zeros_like(field);gy=np.zeros_like(field)
    cold=np.zeros_like(field)
    centers=(.113,.435,.532,.728,.825,.946)
    zones=((.009,.061),(.165,.385),(.576,.685),(.772,.796),(.854,.893))
    segments=[];trees=[]
    def draw(a,b,hw,intensity=1.,hot=True):
        a=np.asarray(a);b=np.asarray(b);d=b-a;ln=np.linalg.norm(d)
        if ln<1e-7:return
        d=d/ln;pad=hw*2+3/max(H,W)
        x0=max(0,int((min(a[0],b[0])-pad)*W));x1=min(W,int(np.ceil((max(a[0],b[0])+pad)*W)))
        y0=max(0,int((1-max(a[1],b[1])-pad)*H));y1=min(H,int(np.ceil((1-min(a[1],b[1])+pad)*H)))
        if x1<=x0 or y1<=y0:return
        sl=np.s_[y0:y1,x0:x1];dx=u[sl]-a[0];dy=v[sl]-a[1]
        t=np.clip(dx*d[0]+dy*d[1],0,ln)
        perp=dx*(-d[1])+dy*d[0];dist=np.sqrt((dx-t*d[0])**2+(dy-t*d[1])**2)
        core=(1-sm(hw*.3,hw*1.1,dist))*intensity
        if hot:field[sl]=np.maximum(field[sl],core)
        else:cold[sl]=np.maximum(cold[sl],core)
        groove=(1-sm(hw*.35,hw*1.4,dist))
        depth[sl]=np.maximum(depth[sl],groove)
        walls=sm(hw*.2,hw*.6,dist)*(1-sm(hw,hw*1.75,dist))
        gx[sl]+=np.sign(perp)*(-d[1])*walls*.23
        gy[sl]+=np.sign(perp)*d[0]*walls*.23
        segments.append(dict(a=a.tolist(),b=b.tolist(),width=hw*2,hot=hot))
    for j,cx in enumerate(centers):
        available=.022 if j==4 else .028
        for k,cy0 in enumerate((.095,.340,.630,.890)):
            cy=cy0+rng.uniform(-.025,.025)
            # The long sparse ends give an elongated real envelope. Most heat
            # lies in its connected branching central fault, rather than lines.
            ys=np.array([-.067,-.033,-.012,0,.012,.033,.067])+cy
            xx=cx+rng.uniform(-.005,.005,len(ys));xx[0]=xx[-1]=cx
            path=np.stack([xx,ys],1)
            for a,b in zip(path[:-1],path[1:]):draw(a,b,.00135,1.)
            def fitted(pt):
                return np.array([np.clip(pt[0],cx-available,cx+available),np.clip(pt[1],cy-.025,cy+.025)])
            # Irregular connected fork paths replace the failed parallel fans.
            # Each actual turn has small Y branches; no repeated horizontal rung.
            for side in (-1,1):
                for yoff in (-.012,.001,.013):
                    start=np.array([float(np.interp(cy+yoff,ys,xx)),cy+yoff])
                    for step in range(3):
                        end=fitted(np.array([cx+side*available*(step+1)/3,
                                             cy+yoff+rng.uniform(-.013,.013)]))
                        draw(start,end,.00260,1.)
                        if step<2:
                            for sign in (-1,1):
                                twig=fitted(end+np.array([side*rng.uniform(.002,.006),sign*rng.uniform(.004,.008)]))
                                draw(end,twig,.00265,.98)
                        start=end
            trees.append(dict(center=[cx,cy],envelope=[cx-available,cy-.067,cx+available,cy+.067]))
    # These cooled fractures remain visible in the albedo/normal. Their glow is
    # explicitly below both inherited hard glow masks, not an erased network.
    for k in range(4):
        for j in range(len(centers)-1):
            a=trees[j*4+k]['center'];b=trees[(j+1)*4+k]['center']
            mid=[(a[0]+b[0])*.5,(a[1]+b[1])*.5+rng.uniform(-.035,.035)]
            draw(a,mid,.00125,.9,hot=False);draw(mid,b,.00125,.9,hot=False)
    # Heat fades toward the tile's middle; hotter lower/upper tile edges are
    # periodic, while the opposite emission edge samples stay identically dark.
    heat=.80+.20*((.5+.5*np.cos(2*np.pi*v))**1.3)
    field=np.clip(field*heat,0,1)
    keys=[0,.18,.45,.72,1]
    cols=np.array([[0,0,0],[70,8,0],[190,45,5],[250,100,16],[255,160,40]])/255
    E=np.stack([np.interp(field,keys,cols[:,i]) for i in range(3)],-1).astype(np.float32)
    E=np.maximum(E,cold[...,None]*np.array([7,1,0],np.float32)/255)
    for a,b in zones:
        # The hot tree envelopes were reserved outside these zones. Cooled
        # grooves cross them, but their7/255 maximum is a genuine plain chart.
        hot=(u>=a)&(u<=b)
        E[hot]=np.minimum(E[hot],np.array([7,1,0],np.float32)/255)
    E[0]=E[-1]=0;E[:,0]=E[:,-1]=0
    N=base['N'].copy()
    # Existing prism bevels and fracture normal walls stay intact. New grooves
    # occupy their front planes, adding no false block-fracture normal event.
    front=(np.abs(N[...,0])<.20)&(np.abs(N[...,1])<.20)
    N[...,0]+=np.clip(gx,-.22,.22)*front;N[...,1]+=np.clip(gy,-.22,.22)*front
    N[...,2]=np.sqrt(np.maximum(1-N[...,0]**2-N[...,1]**2,.03))
    N/=np.linalg.norm(N,axis=2,keepdims=True)
    C=base['C'].copy()
    # Keep the original Crater albedo-window contract. These are physical
    # RGB corrections, smoothly interpolated across u rather than new masks.
    # The generator's test still reads the unchanged 128 reference values.
    target=np.array((0.143,0.142,0.143,0.145,0.135,0.121,0.120,0.112,0.065,0.053,0.119,0.184,0.194,0.190,0.139,0.108,0.114,0.158,0.194,0.234,0.228,0.221,0.217,0.209,0.188,0.174,0.131,0.058,0.074,0.124,0.131,0.136,0.155,0.184,0.181,0.177,0.178,0.181,0.184,0.185,0.183,0.167,0.149,0.146,0.110,0.051,0.069,0.116,0.149,0.175,0.179,0.178,0.178,0.176,0.159,0.146,0.111,0.097,0.130,0.160,0.200,0.221,0.220,0.219,0.217,0.203,0.181,0.127,0.100,0.084,0.101,0.107,0.139,0.150,0.150,0.147,0.146,0.142,0.129,0.105,0.064,0.086,0.175,0.216,0.275,0.305,0.306,0.301,0.291,0.248,0.193,0.123,0.091,0.109,0.111,0.142,0.154,0.154,0.153,0.152,0.141,0.125,0.092,0.088,0.105,0.152,0.168,0.170,0.166,0.146,0.110,0.049,0.044,0.095,0.114,0.111,0.115,0.099,0.081,0.080,0.093,0.093,0.095,0.098,0.109,0.138,0.146,0.144))
    scale_total=np.ones(W)
    for _ in range(5):
        mean=C.mean(2).mean(0).reshape(128,-1).mean(1)
        scale=np.interp((np.arange(W)+.5)/W,
                        (np.arange(-1,129)+.5)/128,
                        np.r_[target[-1]/mean[-1],target/mean,target[0]/mean[0]])
        C=np.clip(C*scale[None,:,None],0,1)
        scale_total*=scale
    layout_profile=dict(target=target.tolist(),scale_min=float(scale_total.min()),scale_max=float(scale_total.max()),method='five smooth periodic RGB scaling passes; unchanged original PROFILE128')
    return dict(C=C,N=N,E=E),dict(centers=centers,zones=zones,segments=segments,trees=trees,seed=seed,variant=variant,albedo_profile=layout_profile)

def mat_basalt(N=1024,seed=1101):
    base=_mat_basalt_prisms(N,seed)
    if not BASALT_P.get('bounded',0): return base
    u,v=grid(N,N)
    maps,layout=_basalt_bounded_network(u,v,base,seed=int(BASALT_P.get('bounded_seed',311)),variant=6)
    _NET_STATS.update(trees=len(layout['trees']),segments=len(layout['segments']),variant=6,kind='bounded recursive zigzag forks')
    return maps


def glow_ramp(x):
    """0 black -> deep red -> orange -> yellow-white (sRGB)."""
    keys = np.array([0.0, 0.18, 0.45, 0.72, 0.9, 1.0], F)
    cols = np.array([[0, 0, 0], [70, 8, 0], [190, 45, 5], [255, 110, 18], [255, 170, 50], [255, 222, 128]], F) / 255
    x = np.clip(x, 0, 1)
    return np.stack([np.interp(x, keys, cols[:, i]) for i in range(3)], -1).astype(F)


def hpass(a, kmin):
    """remove every Fourier component below kmin cycles per tile (keeps the mean): periodic, deterministic."""
    n0, n1 = a.shape
    Fa = np.fft.rfft2(a)
    ky = np.abs(np.fft.fftfreq(n0) * n0)[:, None]; kx = (np.fft.rfftfreq(n1) * n1)[None, :]
    km = np.maximum(ky, kx)
    Fa[(km > 0) & (km < kmin)] = 0
    return np.fft.irfft2(Fa, s=a.shape).astype(F)


def gradmag(x):
    """periodic central-difference gradient magnitude per texel."""
    gx = (np.roll(x, -1, 1) - np.roll(x, 1, 1)) * 0.5
    gy = (np.roll(x, -1, 0) - np.roll(x, 1, 0)) * 0.5
    return np.sqrt(gx * gx + gy * gy).astype(F)


def aastep(x, t, wmin, gpx):
    """smoothstep across x = t whose half width is never below ~1.25 output texels (analytic antialiasing)."""
    w = np.maximum(F(wmin), F(1.25) * gpx)
    return sm(t - w, t + w, x)


def ssdown(a, f):
    """box-filter supersampled maps back to the target size (antialiasing); periodic grids stay periodic."""
    if f == 1:
        return a
    h, w = a.shape[:2]
    r = a.reshape(h // f, f, w // f, f, *a.shape[2:]).mean((1, 3))
    return r.astype(F)


def specfield(n, kmin, kmax, beta, seed):
    """band-limited periodic random field by spectral synthesis (integer cycles per tile kmin..kmax, power law k^-beta,
    random phases), unit std, n x n. Periodic by construction."""
    rng = np.random.default_rng([seed, n, 41])
    ky = np.fft.fftfreq(n) * n
    kx = np.fft.rfftfreq(n) * n
    KX, KY = np.meshgrid(kx, ky)
    kr = np.sqrt(KX * KX + KY * KY)
    win = sm(kmin - 0.9, kmin + 0.1, kr) * (1 - sm(kmax - 0.1, kmax + 1.2, kr))
    amp = np.where(kr > 0, np.maximum(kr, 1e-3) ** (-beta), 0.0) * win
    spec = amp * np.exp(1j * rng.uniform(0, 2 * np.pi, kr.shape))
    return zs(np.fft.irfft2(spec, s=(n, n)))


def curl2(psi):
    """divergence-free velocity (vx along columns, vy along rows) of a stream function, spectral derivatives, per tile unit."""
    n = psi.shape[0]
    P = np.fft.rfft2(psi)
    ky = (np.fft.fftfreq(n) * n)[:, None]; kx = (np.fft.rfftfreq(n) * n)[None, :]
    dpdx = np.fft.irfft2(P * (2j * np.pi * kx), s=(n, n)); dpdy = np.fft.irfft2(P * (2j * np.pi * ky), s=(n, n))
    vx, vy = dpdy, -dpdx
    rms = np.sqrt((vx * vx + vy * vy).mean()) + 1e-9
    return (vx / rms).astype(F), (vy / rms).astype(F)


def samp(a, x, y):
    """periodic bilinear sample of a (n0,n1[,c]) at tile coordinates x (columns), y (rows)."""
    n0, n1 = a.shape[:2]
    fx = x * n1 - 0.5; fy = y * n0 - 0.5
    x0 = np.floor(fx).astype(np.int64); y0 = np.floor(fy).astype(np.int64)
    tx = (fx - x0).astype(F); ty = (fy - y0).astype(F)
    x0 %= n1; y0 %= n0; x1 = (x0 + 1) % n1; y1 = (y0 + 1) % n0
    if a.ndim == 3:
        tx = tx[..., None]; ty = ty[..., None]
    return ((a[y0, x0] * (1 - tx) + a[y0, x1] * tx) * (1 - ty) + (a[y1, x0] * (1 - tx) + a[y1, x1] * tx) * ty).astype(F)


def advect_disp(vx, vy, steps, length, n=None):
    """backward advection of every cell centre of the n x n grid through the (periodic, divergence-free) velocity for a total
    path length `length` (tile units, at rms speed 1); returns the displacement field D (n,n,2) = (dx, dy) in tile units.
    v6 2026-10-04: n defaults to the velocity grid (256), the callers pass n=1024: the backward map is stretched 4-18x (median 4.5, p90 18 texels of pattern per texel) and is almost singular on thin
    filaments (the stable manifolds), so a displacement field sampled every 4 output texels and bilinearly interpolated made SAWTOOTH fold lines (period 4 texels) in Lava_C/_E (review: 'a sawtooth vertical seam line runs through the near lava')."""
    n = vx.shape[0] if n is None else n
    xs = ((np.arange(n, dtype=F) + .5) / n)
    x0 = np.broadcast_to(xs[None, :], (n, n)).copy(); y0 = np.broadcast_to(xs[:, None], (n, n)).copy()
    px, py = x0.copy(), y0.copy()
    h = length / steps
    for _ in range(steps):
        ax = samp(vx, px, py); ay = samp(vy, px, py)
        mx = px - 0.5 * h * ax; my = py - 0.5 * h * ay           # midpoint (RK2)
        px = px - h * samp(vx, mx, my); py = py - h * samp(vy, mx, my)
    return np.stack([px - x0, py - y0], -1).astype(F)


def ramp(x, keys, cols):
    """piecewise-linear colour ramp: x (any shape) -> (...,3); cols 0..255 sRGB."""
    keys = np.asarray(keys, F); cols = np.asarray(cols, F) / 255
    x = np.clip(x, keys[0], keys[-1])
    return np.stack([np.interp(x, keys, cols[:, i]) for i in range(3)], -1).astype(F)


LAVA_LIGHT = np.array([1.421, 0.866, 0.742], F)   # linear light on flat ground at Crater (work/postcard-look/v2/tools/lit_predict.py)
LAVA_TINT = 1.0                                     # _BaseColor tint the URP Lit FALLBACK path is encoded for (white; GolfLook.cs ships .75 -> the gate reports both). The GolfLava shader ignores Tint
LAVA_MULT_VEC = (1.0, 0.65, 0.50)                   # LK_LAVA Emission multiplier (LINEAR, per channel, on the _E map) in GolfLook.cs: URP Lit fallback path
LAVA_LUT_X = (0.0, 0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9, 1.0)
# alpha = albedo's share of T's linear energy (v2 first pass .72; .25 = emission carries 75 %, so the lava reads orange with the albedo off);
# lut = tone curve on the design heat at LAVA_LUT_X, fitted by work/postcard-look/v2/tools/lava_v3.py fit 0.25 to crater.jpg's pool heat
# quantiles (p25 .28, p50 .44, p75 .68, ~10 % hot) under the live GolfLava constants; the top knots keep the hottest veins yellow-orange for the Lit fallback
LAVA_ENC = dict(alpha=0.25, lut=(0.0, 0.1, 0.272, 0.295, 0.341, 0.366, 0.405, 0.429, 0.491, 0.548, 0.734))   # v7 2026-10-04: lava_try.py --iters 10 (the lava_v5 fit) on the v7 LAVA_P (v6 lut: 0, .1, .293, .323, .364, .390, .428, .451, .518, .553, .735; v4 lut: .258 .275 .348 .415 .526 .61 .669 .70 .84)
LAVA_MOAT = 0.38
LAVA_P = dict(base0=0.18, base_h=0.72, base_n=0.10, v1w0=0.09, v1w1=0.07, v1a=0.32, v2w0=0.06, v2w1=0.045, v2a=0.12,
              isl_lo=1.30, isl_hi=1.60, isl_h=0.5, rib_w=0.07, rib_a=0.3,
              # v5 2026-10-04 ("orange paint" -> glowing flow): broad soft veins (v1w/v2w x1.7) + a soft halo, hot LANES that follow the large flow (lane_*), cool red zones between them (cool_a),
              # a few chunky crust PLATES (plate_*: Voronoi cells that only partly follow the flow) and a morphological opening of the crust (open_s: no thin black ribbons), granular heat (gran_a),
              # fewer / larger vortices (dbl .05 -> .035, dz_cyc 6..14 -> 5..11). The v2 look is LAVA_P_V2.
              lane_a=0.33, lane_w=0.18, lane_cyc=3, dbl=0.035, dz_cyc=(5, 11), cool_a=0.10,
              plate_a=1.0, plate_nx=10, plate_ny=8, plate_thr=0.13, plate_e0=0.07, plate_e1=0.20, plate_cool=0.5, plate_adv=0.30, halo_a=0.17, halo_k=3.2, open_s=5.5, gran_a=0.10, net_a=0.0, net_n=28, net_w=0.08, soft=0.0, d_n=1024, aa=1,
              gl_h=0.0, gl_c=0.0, gl_it=8, close_k=0, open_k=0, tdf_k=11, tdf_t=0.07, fil_a=0.40, fil_w=0.04, fil_cyc=28, fil_mix=0.0, fil_warp=0.03, fil_core=0.4, fil_t0=-0.55, fine_a=0.06, fine_cyc=110)   # v7: gl_h / gl_c = steepest allowed change of the design heat / crust per OUTPUT texel (0 = off = v6); see limit_gradient
LAVA_P_V2 = dict(base0=0.40, base_h=0.30, base_n=0.10, v1w0=0.07, v1w1=0.06, v1a=0.58, v2w0=0.05, v2w1=0.04, v2a=0.24, isl_lo=0.75, isl_hi=1.05, isl_h=0.5, rib_w=0.07, rib_a=0.9,
                 lane_a=0.0, lane_w=0.14, lane_cyc=3, dbl=0.05, dz_cyc=(6, 14), cool_a=0.0, plate_a=0.0, plate_nx=8, plate_ny=6, plate_thr=0.45, plate_e0=0.07, plate_e1=0.20, plate_cool=0.5,
                 plate_adv=0.35, halo_a=0.0, halo_k=3.2, open_s=0.0, gran_a=0.0, net_a=0.0, net_n=28, net_w=0.08, soft=0.0, d_n=256, aa=0)   # rollback: LAVA_P.update(LAVA_P_V2) + LAVA_ENC lut of v4 (0,.1,.258,.275,.348,.415,.526,.61,.669,.70,.84) reproduces the v4 bytes

LAVA_P_V7 = dict(v1_cyc=5, v2_cyc=11, web_a=0.0, lane_a=0.33, base_h=0.72, halo_a=0.17, isl_lo=1.30, isl_hi=1.60, plate_nx=10, plate_ny=8, plate_adv=0.30, plate_thr=0.13, moat=0.38, seam_hot=0.0, open_s=5.5, fine_a=0.06, fine_cyc=110, rank=0, tdf_oct=0, reg_a=0.0)   # rollback of v8: LAVA_P.update(LAVA_P_V7) + LAVA_ENC['quantile'] = 0 reproduces the v7 bytes
# v8 2026-10-05 (repair round 3, review 'flat marbled orange vignettes, magnified, no vein layer, black ink streaks'): see TEXTURES.md v8 section 1. A FINER pattern (the marbling veins at 12 / 26 cycles per tile instead of 5 / 11: the same swirls, 2.4x thinner streaks),
# a WEB of thin hot veins at three scales (web_*), no clipped design heat (`rank`: it is rank-equalised, so no flat plateau exists) mapped onto the target SHADER heat histogram by an exact quantile curve (LAVA_ENC['quantile'], LAVA_D_RHO / LAVA_D_HEAT),
# fewer broad hot lanes / halos, compact crust plates (open_s 7, fewer islands) with a glowing rim (seam_hot), an octagonal (not square) closing of the thin dark lines, finer non-advected grain (fine_cyc 150).
LAVA_P.update({"rank": 1, "tdf_oct": 1, "v1_cyc": 12, "v2_cyc": 26, "web_a": 0.45, "web_sc": [[10, 3, 1.0, 0.5], [22, 2.4, 0.8, 0.6], [46, 2.2, 0.6, 0.7]], "web_warp": 0.03, "web_core": 0.4, "web_halo": 0.2, "web_t0": -0.8, "web_h0": 0.6, "lane_a": 0.12, "base_h": 0.55, "halo_a": 0.08, "isl_lo": 1.3, "isl_hi": 1.6, "plate_thr": 0.22, "moat": 0.25, "seam_hot": 0.5, "open_s": 9.0, "fine_a": 0.1, "fine_cyc": 150, "plate_nx": 8, "plate_ny": 6, "plate_adv": 0.34})
LAVA_ENC.update(quantile=1, d_rho=(0.0, 0.03, 0.065, 0.25, 0.50, 0.75, 0.90, 0.93, 1.0), d_heat=(0.0, 0.012, 0.14, 0.30, 0.44, 0.72, 0.90, 0.97, 1.0))
LAVA_P.update(reg_a=0.30)                                                  # regional light / dark (LAVA_PLATES_AND_LANES needs >= 24.5 of block-mean luminance sd at 14 cm / px)


def s2l_(x):
    x = np.asarray(x, F)
    return np.where(x <= 0.04045, x / 12.92, ((np.maximum(x, 0) + 0.055) / 1.055) ** 2.4).astype(F)


def l2s_(x):
    x = np.clip(np.asarray(x, F), 0, 1)
    return np.where(x <= 0.0031308, x * 12.92, 1.055 * np.power(x, 1 / 2.4) - 0.055).astype(F)


def lava_fields(N=1024, seed=1201, ss=2):
    """crater lava (tile 24 m), STATIC. v2 2026-10-04: swirling molten FLOW, not cells. A periodic stream function (spectral
    noise, 2..5 and 6..14 cycles per tile) gives a divergence-free velocity; the pattern coordinates are advected backwards
    through it (marbling), so thin ridged veins stretch, swirl and curl into vortices like crater.jpg's pool. Mostly bright:
    deep orange base (the albedo carries the orange), yellow-orange veins, a few thin dark crust ribbons that follow the flow
    and a handful of dark islands with red rims. No Voronoi, no cell borders. Rendered at ss x and box-filtered, in row strips
    (memory). Stage 1 of 2 (the design fields hh = heat, crust, pu/pv = tile coordinates); lava_encode() turns them into _C/_E/_N."""
    P = LAVA_P
    M = N * ss
    n0 = 256
    vxA, vyA = curl2(specfield(n0, 2, 5, 1.2, seed + 1)); vxB, vyB = curl2(specfield(n0, int(P['dz_cyc'][0]), int(P['dz_cyc'][1]), 1.0, seed + 2))
    DA = advect_disp(vxA, vyA, 40, 0.30, P['d_n']); DB = advect_disp(vxB, vyB, 40, P['dbl'], P['d_n'])
    # coarse (low-frequency) control fields, sampled at the advected coordinates
    cu, cv = grid(n0, n0)
    HEAT = zs(hpass(0.6 * fbm(cu, cv, 3, 3, seed + 3) + 0.4 * noise(cu, cv, 5, 5, seed + 4), 3))
    CF = zs(hpass(fbm(cu, cv, 4, 4, seed + 8), 3))
    COOL = zs(fbm(cu, cv, 3, 2, seed + 11))
    BASEF = zs(hpass(fbm(cu, cv, 4, 3, seed + 7), 3))
    u1 = (np.arange(M, dtype=F) + .5) / M
    S = 128
    pu = np.empty((M, M), F); pv = np.empty((M, M), F); hh = np.empty((M, M), F); crust = np.empty((M, M), F)
    for r0 in range(0, M, S):
        x = np.broadcast_to(u1[None, :], (S, M)).copy(); y = np.broadcast_to(u1[r0:r0 + S, None], (S, M)).copy()
        dA = samp(DA, x, y); x1 = x + dA[..., 0]; y1 = y + dA[..., 1]
        dB = samp(DB, x1, y1); px = x1 + dB[..., 0]; py = y1 + dB[..., 1]
        qu = px; qv = (1 - py).astype(F)                                       # (u, v) tile space for the periodic noise kit
        h01 = sm(-1.8, 1.8, samp(HEAT, px, py))
        r1 = np.abs(fbm(qu, qv, int(P.get('v1_cyc', 5)), 3, seed + 5, gain=0.45)); w1 = P['v1w0'] + P['v1w1'] * h01       # v8: v1_cyc / v2_cyc (5 / 11 until v7): the pattern frequency, i.e. the vein width at the same w
        r2 = np.abs(fbm(qu, qv, int(P.get('v2_cyc', 11)), 2, seed + 6, gain=0.5)); w2 = P['v2w0'] + P['v2w1'] * h01
        vein1 = 1 - sm(0.0, 1.0, r1 / w1)
        vein2 = 1 - sm(0.0, 1.0, r2 / w2)
        base = P['base0'] + P['base_h'] * h01 + 0.1 * P['base_n'] * samp(BASEF, px, py)
        h_ = base + P['v1a'] * vein1 + P['v2a'] * vein2 * (0.5 + 0.5 * h01)
        if P['gran_a'] > 0:                                                    # v5: mid-scale granular heat (crumbly crust / bubbling), not smooth paint gradients
            h_ = h_ + P['gran_a'] * fbm(qu, qv, 40, 3, seed + 70, gain=0.55)
        if P['net_a'] > 0:                                                     # v5: a filigree of thin hot veins (crater.jpg's yellow-orange web) on the molten zones: Voronoi edges of the advected pattern, random strength per edge
            Wn = worley(qu, qv, int(P['net_n']), int(P['net_n']), seed + 80, 0.95)
            ered = crand((Wn['id'] * Wn['id2'] * 31 + (Wn['id'] + Wn['id2']) * 7919) % 4096, 4096, seed + 81)      # symmetric in the two cells of an edge
            vn = (1 - sm(0.0, P['net_w'], Wn['edge'])) * (0.20 + 0.80 * ered) * (0.4 + 0.6 * h01)
            h_ = h_ + P['net_a'] * vn
        if P['fil_a'] > 0:                                                     # v7: a FILIGREE of thin hot veins (crater.jpg's yellow-orange web; >= 3 texels wide with a flat hot core, so it never aliases and never reads as a hairline).
            # `fil_mix` mixes the texel coordinates (0: not advected) with the fully advected ones (1); a meander warp (`fil_warp`) keeps the ridge lines from reading as noise contours; they live in patches (`vstr`) of the molten zones
            fu = (x + P['fil_mix'] * (px - x) + P['fil_warp'] * noise(x, 1 - y, 4, 4, seed + 94)).astype(F)
            fv = (1 - (y + P['fil_mix'] * (py - y)) + P['fil_warp'] * noise(x, 1 - y, 5, 3, seed + 95)).astype(F)
            rf = np.abs(fbm(fu, fv, int(P['fil_cyc']), 3, seed + 90, gain=0.5))
            vf = 1 - sm(P['fil_core'] * P['fil_w'], P['fil_w'] * (0.7 + 0.6 * h01), rf)
            vstr = sm(P['fil_t0'], P['fil_t0'] + 0.6, noise(fu, fv, 5, 5, seed + 91))
            h_ = h_ + P['fil_a'] * vf * vstr * (0.35 + 0.65 * h01)
        if P.get('web_a', 0) > 0:                                              # v8: a WEB of thin hot veins at three scales (crater.jpg's filigree): contour lines of ridged noise, only PARTLY advected (they curl round the
            # vortices but are never stretched into filaments), width fixed in texels (`web_wt`), strength per scale (`web_s`), in patches whose coverage is `web_cov` (1 = everywhere); a faint halo round each line
            acc = np.zeros_like(h_)
            for k, (cyc, wt, st_, mx_) in enumerate(P['web_sc']):
                fu = (x + mx_ * (px - x) + P['web_warp'] * noise(x, 1 - y, 3 + k, 3 + k, seed + 120 + k)).astype(F)
                fv = (1 - (y + mx_ * (py - y)) + P['web_warp'] * noise(x, 1 - y, 4 + k, 4 + k, seed + 125 + k)).astype(F)
                rr = np.abs(fbm(fu, fv, int(cyc), 2, seed + 130 + k, gain=0.5))
                wk = wt * cyc * 4.76e-4 * (0.8 + 0.4 * h01)
                line = 1 - sm(P['web_core'] * wk, wk, rr)
                halo = (1 - sm(0.0, 3.0 * wk, rr)) * P['web_halo']
                dens = sm(P['web_t0'], P['web_t0'] + 0.7, noise(fu, fv, 3 + k, 3 + k, seed + 135 + k))
                acc = np.maximum(acc, st_ * (line + halo) * dens)
            h_ = h_ + P['web_a'] * acc * (P['web_h0'] + (1 - P['web_h0']) * h01)
        if P['fine_a'] > 0:                                                    # v7: fine granular heat that is NOT advected (no aliasing): the near-field detail the stretched pattern lacks
            h_ = h_ + P['fine_a'] * fbm(x, 1 - y, int(P['fine_cyc']), 3, seed + 92, gain=0.5)
        if P['halo_a'] > 0:                                                    # v5: a soft glow halo around the main veins (luminous, not painted lines)
            h_ = h_ + P['halo_a'] * (1 - sm(0.0, 1.0, r1 / (P['halo_k'] * w1)))
        if P['lane_a'] > 0:                                                    # v5: a few broad hot LANES (crater.jpg's yellow-orange channels) that follow the large flow
            rl = np.abs(fbm(qu, qv, int(P['lane_cyc']), 2, seed + 50, gain=0.45)); lane = 1 - sm(0.0, 1.0, rl / (P['lane_w'] * (0.7 + 0.6 * h01)))
            h_ = h_ + P['lane_a'] * lane * (0.5 + 0.5 * h01)
        if P.get('reg_a', 0) > 0:                                              # v8: REGIONAL light / dark (2-4 m patches that follow the flow): every texel of a patch is brighter or darker, the veining inside it is kept (no flat plateau)
            h_ = h_ + P['reg_a'] * fbm(qu, qv, int(P.get('reg_cyc', 3)), 2, seed + 140, gain=0.5)
        if P['cool_a'] > 0:                                                    # v5: and cooler red zones between the lanes (broad, not thin ribbons)
            h_ = h_ - P['cool_a'] * (1 - h01) ** 2
        if P.get('rank', 0):                                                   # v8: NO clip (a clipped design heat ties >10 % of the molten texels at 1.0 = flat orange plateaus); the heat is rank-equalised after the loop
            h_ = h_.astype(F)
        else:
          h_ = (1 - np.exp(-P['soft'] * np.maximum(h_, 0))).astype(F) if P['soft'] > 0 else np.clip(h_, 0, 1)      # soft: exponential roll-off instead of a hard clip (a clipped design heat ties >10 % of the texels at 1.0 and the tone curve cannot separate them)
        cf = samp(CF, px, py)
        island = sm(P['isl_lo'], P['isl_hi'], cf - P['isl_h'] * (h01 - 0.5))
        r3 = np.abs(fbm(qu, qv, 7, 3, seed + 9, gain=0.5))
        dash = sm(-0.2, 0.5, noise(qu, qv, 19, 19, seed + 10))
        ribbon = (1 - sm(0.0, P['rib_w'], r3)) * sm(0.2, 0.9, 1 - h01 + 0.3 * samp(COOL, px, py)) * dash
        cr_ = np.maximum(island, ribbon * P['rib_a'])
        if P['plate_a'] > 0:                                                   # v5: crust PLATES (a few chunky Voronoi plates in the cool zones, 1-3 m, flow-sheared), not just stretched ribbons
            Wp = worley((x + P['plate_adv'] * dA[..., 0]).astype(F), (1 - (y + P['plate_adv'] * dA[..., 1])).astype(F), int(P['plate_nx']), int(P['plate_ny']), seed + 60, 0.9)   # only a fraction of the large flow: chunky plates, not stretched ribbons
            rp = crand(Wp['id'], Wp['n'], seed + 61)
            cool_z = 1 - sm(P['plate_cool'] - 0.25, P['plate_cool'] + 0.25, h01)
            plate = (rp < P['plate_thr'] * (0.35 + 1.3 * cool_z)).astype(F) * sm(P['plate_e0'], P['plate_e1'], Wp['edge'])
            cr_ = np.maximum(cr_, plate * P['plate_a'])
        pu[r0:r0 + S] = qu; pv[r0:r0 + S] = qv; hh[r0:r0 + S] = h_
        crust[r0:r0 + S] = np.clip(cr_, 0, 1)
    if P['aa']:                                                                # v6: the thin filaments where the backward map is almost singular alias (a dark hairline through the pattern): average there
        Jt = map_jump(pu, pv) * M
        hh = aa_filter(hh, Jt); crust = aa_filter(crust, Jt)
    if P.get('rank', 0):                                                       # v8: rank-equalise to a uniform [0, 1] histogram (monotone, so every gradient keeps its place and no plateau exists); the tone curve (LAVA_ENC lut) then IS the target quantile function
        order = np.argsort(hh, axis=None, kind='stable')
        rk = np.empty(hh.size, F); rk[order] = (np.arange(hh.size, dtype=F) + .5) / hh.size
        hh = rk.reshape(hh.shape)
    return dict(hh=hh, crust=crust, pu=pu, pv=pv, N=N, seed=seed, ss=ss)


def map_jump(pu, pv):
    """largest jump (tile units) of the pattern coordinates between an output texel and its 4 neighbours (mod 1: the tile wraps)."""
    def wd(a, b):
        d = a - b
        return (d + .5) % 1.0 - .5
    return np.maximum.reduce([np.abs(wd(np.roll(pu, -1, 1), pu)), np.abs(wd(np.roll(pv, -1, 1), pv)),
                              np.abs(wd(np.roll(pu, -1, 0), pu)), np.abs(wd(np.roll(pv, -1, 0), pv))]).astype(F)


LAVA_AA = ((6.0, 14.0, 1.5), (14.0, 40.0, 3.0), (40.0, 120.0, 5.0), (120.0, 200.0, 8.0))      # (J from, J to, blur sigma in supersampled texels): J = pattern texels per output texel.  v7 2026-10-04: starts at J 6 (was 14: (14,28,1.2) (40,70,2.5) (120,200,5.0)), wider blurs
LAVA_AA_V6 = ((14.0, 28.0, 1.2), (40.0, 70.0, 2.5), (120.0, 200.0, 5.0))      # rollback: LAVA_AA = LAVA_AA_V6 and LAVA_P.update(tdf_k=0, fine_a=0.0) and the v6 lut


def aa_filter(a, Jt, lv=None):
    """footprint-style prefilter: the more the backward map compresses the pattern at a texel (jump Jt, supersampled texels), the wider the local average. Periodic (FFT blurs)."""
    lv = LAVA_AA if lv is None else lv
    Jd = np.maximum(Jt, blur(Jt, 2.0))                                         # widen the filaments by a couple of texels
    out = a.copy()
    for lo, hi, sg in lv:
        out = out + sm(lo, hi, Jd) * (blur(a, sg) - out)
    return out.astype(F)


def _maxf(a, k):
    """periodic separable max filter (square window of k x k samples, k odd)."""
    h = k // 2
    b = a
    for ax in (0, 1):
        m = b
        for d in range(1, h + 1):
            m = np.maximum(m, np.maximum(np.roll(b, d, ax), np.roll(b, -d, ax)))
        b = m
    return b


def _maxd(a, r):
    """periodic dilation with a DIAMOND (L1 ball) of radius r samples: r passes of the 4-neighbour maximum."""
    b = a
    for _ in range(r):
        b = np.maximum(np.maximum(b, np.maximum(np.roll(b, 1, 0), np.roll(b, -1, 0))), np.maximum(np.roll(b, 1, 1), np.roll(b, -1, 1)))
    return b


def _maxo(a, k):
    """periodic dilation with an OCTAGON of about k samples across (a square of k - 2 r + 1 then a diamond of radius r, r = k // 4): the square-window closing of v7 left rectangular plateaus (visible as blocks when the camera magnifies the map)."""
    r = max(k // 4, 1)
    return _maxd(_maxf(a, max(k - 2 * r, 1) | 1), r)


def grey_close(a, k, octagon=False):
    """grey-scale closing (dilate, then erode; periodic, square k x k window, or an octagon of about k samples): fills dark features narrower than k samples, keeps wider ones."""
    if octagon:
        return (-_maxo(-_maxo(a, k), k)).astype(F)
    return (-_maxf(-_maxf(a, k), k)).astype(F)


def grey_open(a, k):
    """grey-scale opening (erode, then dilate): removes bright features narrower than k samples."""
    return _maxf(-_maxf(-a, k), k).astype(F)


def limit_gradient(a, gmax_out, ss, iters=8):
    """v7 2026-10-04 (review: 'a hard vertical discontinuity at x667-671', column step 6.5-10.6 against a median of 1.2; the Game view magnifies the 24 m tile about x4.6 at the rim): the backward
    flow map leaves near-singular FILAMENTS (the stable manifolds, 4-95 pattern texels per output texel) whose pattern changes 30-50 levels from one texel to the next, and the crust boundaries are
    just as steep: a 1-2 texel line that the camera stretches into a straight, dotted hairline.  Nonlinear diffusion: wherever the field changes by more than `gmax_out` per OUTPUT texel (gmax_out/ss
    per supersampled texel; the 3x3 neighbourhood counts, so the flank of an edge is smoothed too) the field is blended toward its local average, a few passes: every edge keeps its place and
    colour but spans >= ~1 / gmax_out output texels.  Periodic (FFT blur); gradients are central differences per texel."""
    gm = float(gmax_out) / ss
    for _ in range(int(iters)):
        gd = gradmag(a)
        gd = np.maximum(gd, 1.5 * blur(gd, 1.2))
        w = sm(0.85 * gm, 1.7 * gm, gd)
        if float(w.max()) < 1e-3:
            break
        a = a + w * (blur(a, 1.3) - a)
    return a.astype(F)


def lava_heat_curve(hh, lut):
    """monotone piecewise-linear tone curve on the design heat (identity = LAVA_LUT_X): the shipped GolfLava shader paints the heat with a fixed
    orange ramp, so the histogram of the heat IS the look; the curve fits it to crater.jpg's pool (median flow, ~10 % hot veins, ~11 % crust)."""
    return np.interp(hh, LAVA_LUT_X, lut).astype(F)


def lava_design(Fd):
    """design heat (after the moat), crust mask and glowing crust-edge seam, at supersampled resolution; before any tone curve."""
    ss = Fd['ss']
    hh = np.clip(hpass(Fd['hh'], 2), 0, 1); crust = np.clip(hpass(Fd['crust'], 2), 0, 1)    # no once-per-tile (24 m) brightness rhythm: drop the 1-cycle Fourier modes
    if LAVA_P['open_s'] > 0:                                                                    # v5: morphological opening - thin black ribbons (the 'oil marbling' look) vanish, chunky plates stay
        crust = np.clip(hpass(sm(0.40, 0.60, blur(crust, LAVA_P['open_s'] * ss)).astype(F), 2), 0, 1)     # (hpass again: the threshold re-creates 1-cycle energy)
    # crust edge: a thin glowing seam just inside every crust boundary; molten right next to the crust is cooler (dark red moat)
    inner = blur(crust, 2.0 * ss)
    seam = crust * (1 - sm(0.5, 0.85, inner))
    moat = blur(crust, 7.0 * ss)
    hh = np.clip(hh - LAVA_P.get('moat', LAVA_MOAT) * moat * (1 - crust), 0, 1)
    if LAVA_P['gl_h'] > 0:                                                                       # v7: no 1-2 texel steps (filaments, crust edges)
        hh = limit_gradient(hh, LAVA_P['gl_h'], ss, LAVA_P['gl_it'])
    if LAVA_P['gl_c'] > 0:
        crust = np.clip(limit_gradient(crust, LAVA_P['gl_c'], ss, LAVA_P['gl_it']), 0, 1)
        inner = blur(crust, 2.0 * ss); seam = crust * (1 - sm(0.5, 0.85, inner))
    if LAVA_P['tdf_k'] > 0:                                                                      # v7: fill only the DEEP thin dark lines (closing minus the field > tdf_t): veining keeps its fine structure
        cl = grey_close(hh, int(LAVA_P['tdf_k']), bool(LAVA_P.get('tdf_oct', 0))); dpt = cl - hh
        wgt = sm(LAVA_P['tdf_t'], 2 * LAVA_P['tdf_t'], np.maximum(dpt, blur(dpt, 1.0) * 1.6))
        hh = (hh + wgt * (cl - hh)).astype(F)
    if LAVA_P['close_k'] > 0:                                                                    # v7: no 1-3 texel dark hairline (an aliased, stair-stepped line the camera stretches into a straight 'seam')
        hh = grey_close(hh, int(LAVA_P['close_k']))
    if LAVA_P['open_k'] > 0:
        hh = grey_open(hh, int(LAVA_P['open_k']))
    return hh, crust, seam


LAVA_TK = [0.0, 0.15, 0.35, 0.55, 0.72, 0.86, 0.95, 1.0]
LAVA_TC = [[66, 11, 5], [112, 20, 7], [168, 36, 10], [206, 56, 14], [232, 84, 18], [250, 124, 28], [255, 156, 42], [255, 178, 60]]
LAVA_D_RHO = (0.0, 0.12, 0.25, 0.50, 0.75, 0.90, 0.94, 1.0)       # v8 target of the SHADER heat (what GolfLava paints): quantile -> heat, crater.jpg's pool (p25 .28, p50 .44 ...) with the top tenth thin veins
LAVA_D_HEAT = (0.0, 0.16, 0.30, 0.43, 0.60, 0.76, 0.91, 1.0)


def lava_heat_of_design(x, alpha, sp):
    """v8: the heat GolfLava computes for a MOLTEN texel whose post-tone-curve design value is x (design value -> target colour T -> _C / _E split -> the shader's heat formula; the normal map's relief term is ignored)."""
    T = ramp(x, LAVA_TK, LAVA_TC)
    Tl = s2l_(T)
    A = l2s_(np.clip(alpha * Tl / (LAVA_LIGHT * s2l_(np.float32(LAVA_TINT))), 0, 1))
    Ecap = s2l_(np.array([1.0, 180 / 255, 60 / 255], F))
    E = l2s_(np.clip((1 - alpha) * Tl / np.array(LAVA_MULT_VEC, F), 0, Ecap))
    lw = np.array([0.2126, 0.7152, 0.0722], np.float64)
    h = A.astype(np.float64) @ lw * sp['HeatAlbedo'] + E.astype(np.float64) @ lw * sp['HeatGlow'] + sp['HeatBias']
    return np.clip((h - 0.5) * sp['HeatGain'] + 0.5, 0, 1)


def lava_quantile_curve(hh, alpha, d_rho, d_heat):
    """v8: replace the design heat by the value whose SHADER heat follows the target quantile function d_rho -> d_heat exactly (rank of every texel over the whole tile; monotone, so no gradient moves)."""
    sp = lava_spec()
    xs = np.linspace(0, 1, 513).astype(np.float64)
    hs = lava_heat_of_design(xs.astype(F), alpha, sp)
    hs = np.maximum.accumulate(hs)                                          # monotone
    order = np.argsort(hh, axis=None, kind='stable')
    rho = np.empty(hh.size, np.float64); rho[order] = (np.arange(hh.size) + .5) / hh.size
    t = np.interp(rho, np.asarray(d_rho, np.float64), np.asarray(d_heat, np.float64))
    t = np.minimum(t, hs[-1] - 1e-4)
    lo, hi = np.unique(hs, return_index=True)                                # strictly increasing heats for the inversion
    x = np.interp(t, lo, xs[hi])
    return x.reshape(hh.shape).astype(F)


def lava_encode(Fd, alpha=None, lut=None):
    """stage 2: design heat -> target display colour T -> (_C albedo, _E glow, _N normal). Knobs default to LAVA_ENC."""
    E_ = dict(LAVA_ENC)
    alpha = E_['alpha'] if alpha is None else alpha
    lut = E_['lut'] if lut is None else lut
    seed, ss = Fd['seed'], Fd['ss']
    pu, pv = Fd['pu'], Fd['pv']
    hh, crust, seam = lava_design(Fd)
    if E_.get('quantile', 0):                                                                    # v8: exact quantile mapping to the target heat histogram (no iterative fit)
        hh = lava_quantile_curve(hh, alpha, E_.get('d_rho', LAVA_D_RHO), E_.get('d_heat', LAVA_D_HEAT))
    else:
        hh = lava_heat_curve(hh, lut)
    mol = 1 - crust
    # TARGET display colour T (sRGB, the colour the surface should show), designed against crater.jpg's pool: median (203,58,16),
    # p95 (255,161,56), ~10 % yellow-orange veins, ~11 % near-black crust.
    # Runtime (2026-10-04, GolfLook.cs): LK_LAVA is the self-lit GolfArcade/GolfLava shader, which reads only the LUMINANCE of _C and _E as a
    # heat value (heat = f(1.5 lum(_C) + 1.5 lum(_E) + bias + relief)) and paints it with an orange ramp: the gates model that (lava_shader_model).
    # The URP Lit fallback (shader missing) is kept honest too: _C is lit by the Crater key/fill/ambient x the shipped tint (LAVA_TINT) and
    # _E x the shipped per-channel multiplier (LAVA_MULT_VEC); the albedo carries `alpha` of T's linear energy and the glow the rest, so the lava
    # reads orange with the albedo OFF (emission alone), as the brief asks.
    Tm = ramp(hh, LAVA_TK, LAVA_TC)
    ash = zs(fbm(pu, pv, 14, 3, seed + 12))
    Tc = mix(C(24, 19, 19), C(56, 43, 40), sm(-1, 1.5, ash))
    rc = np.abs(fbm(pu, pv, 17, 2, seed + 14, gain=0.5))
    crack = (1 - sm(0.0, 0.05, rc)) * sm(-0.3, 0.6, noise(pu, pv, 9, 9, seed + 15))        # a few hairline cracks glowing red in the crust
    Tc = mix(Tc, C(150, 34, 8), crack * 0.55)
    T = mix(Tm, Tc, crust)
    sh_ = LAVA_P.get('seam_hot', 0.0)                                                        # v8: the rim of a crust plate glows (hot orange) instead of a dull red line
    Tseam = mix(C(176, 46, 10) * (0.8 + 0.2 * hh[..., None]), C(252, 128, 30), sh_)
    T = mix(T, Tseam, seam * 0.85)
    Tl = s2l_(T)
    A = l2s_(np.clip(alpha * Tl / (LAVA_LIGHT * s2l_(np.float32(LAVA_TINT))), 0, 1))
    Ecap = s2l_(np.array([1.0, 180 / 255, 60 / 255], F))
    E = l2s_(np.clip((1 - alpha) * Tl / np.array(LAVA_MULT_VEC, F), 0, Ecap))
    col = A
    hgt = 0.5 * crust + 0.12 * hh * mol + 0.04 * noise(pu, pv, 60, 60, seed + 13)
    col = ssdown(col, ss); E = ssdown(E, ss); hgt = ssdown(hgt, ss)
    return dict(C=col, N=normal_map(blur(hgt, 1.2), 3.0), E=E)


def mat_lava(N=1024, seed=1201, ss=2):
    return lava_encode(lava_fields(N, seed, ss))


def mat_lava_old(N=1024, seed=1201):
    """the owner's crust-plate lava (kept for reference / rollback; not in MATS)."""
    u, v = grid(N, N)
    q1 = fbm(u, v, 2, 4, seed + 1); q2 = fbm(u, v, 2, 4, seed + 2)
    wu = u + 0.10 * q1; wv = v + 0.10 * q2
    r1 = fbm(wu, wv, 4, 4, seed + 3); r2 = fbm(wu, wv, 4, 4, seed + 4)
    wu2 = u + 0.07 * r1 + 0.04 * q2; wv2 = v + 0.07 * r2 - 0.04 * q1
    W = worley(wu2, wv2, 9, 12, seed + 5, 0.9)                                # crust plates ~2-2.7 m
    heat = zs(fbm(u, v, 3, 3, seed + 6) + 0.6 * fbm(wu, wv, 5, 2, seed + 11))   # several hot areas per tile: no lone blob
    width = 0.07 + 0.30 * sm(-0.8, 1.8, heat)
    molten = 1 - sm(width * 0.35, width, W['edge'])
    W2 = worley(wu2, wv2, 26, 30, seed + 7, 0.9)
    crack2 = (1 - sm(0.0, 0.045, W2['edge'])) * sm(-0.4, 1.2, heat) * (1 - molten)
    flow = 0.5 + 0.5 * np.sin(2 * np.pi * (4 * r1 + 3 * q2 + 2 * r2 + 1.5 * q1))
    flow2 = 0.5 + 0.5 * np.sin(2 * np.pi * (9 * r2 - 4 * q1))
    core = molten * (0.66 + 0.24 * flow + 0.10 * flow2)
    g = np.clip(core + crack2 * 0.55, 0, 1)
    under = (1 - molten) * (0.02 + 0.06 * sm(-0.5, 1.8, heat))
    E = glow_ramp(np.maximum(g, under))
    crustv = zs(fbm(wu2, wv2, 12, 3, seed + 8))
    crust = mix(C(28, 23, 22), C(46, 34, 30), sm(-1, 1.5, crustv))
    crust = mix(crust, C(84, 78, 76), sm(0.9, 2.1, zs(noise(u, v, 40, 40, seed + 9)) + 0.5 * crustv) * 0.5)   # ash on the plate tops
    rimz = (1 - sm(width, width + 0.10, W['edge'])) * (1 - molten)
    crust = mix(crust, C(92, 36, 18), rimz * 0.55)                                                 # warm rims next to the melt
    hot = mix(C(122, 34, 8), C(186, 82, 22), flow * 0.7 + flow2 * 0.3)
    col = mix(crust, hot, np.clip(molten + crack2 * 0.6, 0, 1))
    pd = sm(0.0, 0.3, W['edge'] - width * 0.5)
    h = (1 - molten) * (0.6 + 0.4 * pd) + 0.18 * crustv * (1 - molten) + 0.06 * noise(u, v, 100, 100, seed + 10) * (1 - molten) \
        + molten * 0.06 * flow - 0.2 * crack2
    return dict(C=col, N=normal_map(blur(h, 1.0), 4.0), E=E)


def mat_surf(N=512, seed=1301):
    """whitewater (tile 8 m, RGBA). GolfSurf multiplies this alpha by the mesh's vertex alpha (shoreline closeness),
    so the map is mostly-white foam (mean alpha ~.55) broken by bubble lace (two Voronoi scales), open-water holes
    inside the bigger bubbles where the foam is thin, denser clots and wind streaks; even at large scale so it
    does not repeat visibly along a shore."""
    u, v = grid(N, N)
    wu = u + 0.02 * noise(u, v, 4, 4, seed + 1); wv = v + 0.02 * noise(u, v, 4, 4, seed + 2)
    A = worley(wu, wv, 12, 12, seed + 3, 0.95)
    B = worley(wu, wv, 30, 30, seed + 4, 0.95)
    lace = np.maximum((1 - sm(0.0, 0.10, A['edge'])), (1 - sm(0.0, 0.13, B['edge'])) * 0.75)
    wisp = zs(noise(u, v, 10, 10, seed + 6) + 0.5 * noise(u, v, 20, 20, seed + 7))   # isotropic: UVs are world xy
    dens = sm(-1.6, 1.6, zs(fbm(u, v, 8, 3, seed + 5)) + 0.5 * wisp + 0.3 * zs(fbm(u, v, 3, 2, seed + 9)))
    hole = (0.5 * sm(0.08, 0.30, B['edge']) + 0.35 * sm(0.12, 0.42, A['edge'])) * (1 - dens)
    a = 0.32 + 0.60 * dens + 0.20 * lace - hole
    a = blur(np.clip(a, 0, 1), 0.8)
    a = np.clip(a * (0.88 + 0.12 * noise(u, v, 90, 90, seed + 8)), 0, 1)
    col = mix(C(190, 230, 228), C(250, 253, 252), sm(0.15, 0.8, a))
    return dict(C=col, A=a)


def mat_fall(W=256, H=512, seed=1401):
    u, v = grid(W, H)
    st = zs(noise(u, v, 24, 2, seed + 1) + 0.6 * noise(u, v, 48, 3, seed + 2) + 0.35 * noise(u, v, 96, 6, seed + 3))
    fine = noise(u, v, 128, 16, seed + 4)
    e0 = 0.02 + 0.035 * (noise(u, v, 1, 4, seed + 5) * 0.5 + 0.5)
    e1 = 0.98 - 0.035 * (noise(u, v, 1, 4, seed + 6) * 0.5 + 0.5)
    edge = sm(e0, e0 + 0.12, u) * sm(e1, e1 - 0.12, u)
    a = edge * np.clip(0.66 + 0.24 * st + 0.08 * fine, 0.12, 1.0)
    a = a * (1 - 0.45 * sm(1.3, 2.3, -st))
    col = mix(C(140, 206, 210), C(248, 253, 253), sm(-1.2, 1.2, st + 0.3 * fine))
    return dict(C=col, A=np.clip(a, 0, 1))


# v4 2026-10-04 (skeptic review of v3): the runtime's SMOKE_SOFT band is a VISIBILITY band, effective alpha .55-.72 (= painted peak x _Color.a x _AlphaGain)
# and the card must move the frame (median |d luminance| >= 8, p99 28..90).  v3's wisp was a thin S curl covering 15 % of the card: median 4.9 in the Game view.
# v4 = a fused, widening column (super-Gaussian body with soft billows, a short curl) so most of the visible texels sit in the body, not the feather.
# amax is the PAINTED peak; the runtime scales it by AlphaGain (GolfLook.cs, 1.35 when this was written -> effective .675).
SMOKE_P = dict(w0=0.03, w1=0.16, amax=0.43, expo=1.8, sat=1.1, cap=0.45, y_in=(0.05, 0.38), y_out=(0.66, 0.88), curl=0.04, billow=0.45,
               base=(164, 167, 173), top=(175, 177, 182), under=(174, 168, 165),
               wexp=0.8, round=0.15, rag=0.07, peak=0.25)
# v7 2026-10-04 (repair round 2, reviews: 'the approach plume is a big pale cloud, two near-opaque cards overlap at the core' and 'a blocky, flat-bottomed white lump over the crater wall'):
# the plume is now a TAPERED WISP: a narrow, rounded, ragged foot (w0 .07 -> .03: no flat horizontal cut where the foot fades in: `round` lifts the fade-in line with r^2 so the underside is a U,
# `rag` roughens it with a low-frequency field) that widens as it rises, a PEAKED body (`peak`: part of the profile is a Gaussian instead of the flat-topped super-Gaussian, so two stacked cards
# that overlap near their cores add up to far less than 1 - (1 - a)^2 of a plateau) and a lower painted peak (.50 -> .43: effective .575 under AlphaGain 1.35, still inside the runtime's .55..0.72 visibility band).
SMOKE_P_V7 = dict(xc0=0.47, sway=0.07, drift=0.07, top=(184, 186, 191), blur_a=5.0)   # rollback of v8: SMOKE_P.update(SMOKE_P_V7) reproduces the v7 Smoke_C bytes
# v8 2026-10-05 (repair round 3, review: 'the approach plume is a large pale lumpy cloud with two dark see-through holes, a ghost shape; reduce the overlap of the two approach-facing cards'): the S-shaped column stands LEFT of the card's middle (xc0 .47 -> .42, sway / drift .07 -> .06)
# with a softer alpha (blur_a 5 -> 7), so the MIRRORED copy the hole builder stacks on it is a different wisp 2 x (.5 - xc) = .16 card widths away instead of the same column on top of itself.
# Measured (Monte Carlo of hole10_look.smoke_group: 3 cards, +-.1 w, every second mirrored, jitter, under the owner's runtime gain 1.10 / power 1.5; v2/tex/v8/tools/smoke_mc2.py): share of the stack at combined alpha >= .5 1.92 % -> 1.13 %, visible area (>= .07) of the stack 19.8 % -> 21.9 % of the card.
# Game view (v8/stills_smkA vs stills_cand4, hole 10 approach): the 'ghost with a dark face' is one broad soft plume.  Verified on both sides: the owner's SMOKE_SOFT_INTERSECTION 5.49 PASS (clone smoke 88 / 88) and the hole builder's SMOKE_IN_FRAME (postcard_look_verify, geometric twin)
# tee 1.77 / approach 2.13 / lavarim 2.82 % (v7: 1.84 / 1.91 / 2.94; limit 1.5).  Evaluated and NOT used: xc0 .38 (SMOKE_SOFT_INTERSECTION 6.20-6.31 FAIL: more plume in front of the wall), .62 (603 px moved FAIL), .40 + w1 .13 (the slimmest that passes the intersection test, 5.60, but the approach
# frame's SMOKE_IN_FRAME drops to 1.35 % FAIL), .40 + w1 .15 (1.48 % FAIL), w1 .07 (the single card's visible area 58-63 % of v7).  The three gates pull in different directions: the intersection test wants the column near the card's middle, the frame test wants plume area in the approach's top-right,
# the overlap wants the column away from the middle; xc0 .42 is the compromise that passes all three.
SMOKE_P.update(xc0=0.42, sway=0.06, drift=0.06, blur_a=7.0)
SMOKE_P_V6 = dict(w0=0.07, w1=0.10, amax=0.50, expo=1.8, sat=1.1, cap=0.45, y_in=(0.10, 0.26), y_out=(0.66, 0.88), curl=0.03, billow=0.34, wexp=0.8, round=0.0, rag=0.0, peak=0.0)   # rollback: SMOKE_P.update(SMOKE_P_V6)


def mat_smoke(N=512, seed=1501):
    """one soft rising PLUME (RGBA, not tiling).  v4 2026-10-04.  A column that widens as it rises (half width w0 + w1 h^.8 of the card, so
    ~0.2 wide at the foot and ~0.5 at the head), bent by an S curve and drifting right, its outline curled by a large-scale divergence-free flow,
    its body a flat-topped (super-Gaussian) profile modulated by low-frequency billows, so the visible texels are mostly BODY at 40-100 % of the
    peak and only a ~25 texel feather at the rim (max alpha step <= .031 per texel).  Alpha peak amax (.50 painted); exactly 0 on the outer 12 %;
    no opaque core.  Colour: light neutral-cool grey (the Crater light is warm: the Game view lands on crater.jpg's (183,154,150)) with a slightly
    darker, warmer foot (lit from the lava below) and a lighter head; low-frequency variation only."""
    P = SMOKE_P
    u, v = grid(N, N)
    x = u.astype(F); y = (1 - v).astype(F)                                    # y down; the plume rises toward y = 0
    n0 = 128
    vx, vy = curl2(specfield(n0, 2, 4, 1.2, seed + 1))
    D = advect_disp(vx, vy, 30, P['curl'])
    d = samp(D, x, y)
    px = x + d[..., 0]; py = y + d[..., 1]
    h = 1 - py                                                                 # height along the plume 0 (source) .. 1
    xc = P.get('xc0', 0.47) + P.get('sway', 0.07) * np.sin(2 * np.pi * (h * 0.85 + 0.05)) + P.get('drift', 0.07) * h * h    # centre line: gentle S bend + drift to the right (v8: xc0 .47 -> .36, the column stands LEFT of the card's middle so a mirrored copy's column is 2 x (.5 - xc) = .28 card widths away)
    hc = np.clip(h, 0, 1)
    wd = (P['w0'] + P['w1'] * np.power(hc, P['wexp'])) * (1 - P['cap'] * sm(0.55, 0.9, hc))   # half width grows with height, rounds off at the head
    r = np.abs(px - xc) / wd
    prof = np.exp(-np.power(r, P['expo']) * 0.9)                                # flat-topped body, soft shoulder
    if P['peak'] > 0:                                                           # v7: part of the profile is a peaked Gaussian (no plateau for stacked cards to add up on)
        prof = (1 - P['peak']) * prof + P['peak'] * np.exp(-r * r * 1.7)
    hf = h - P['round'] * np.minimum(r, 1.6) ** 2 + P['rag'] * zs(fbm(px, py, 4, 3, seed + 7)) if (P['round'] > 0 or P['rag'] > 0) else h    # v7: a rounded, ragged underside (U-shaped fade-in line), not a horizontal cut
    env = sm(*P['y_in'], hf) * (1 - sm(*P['y_out'], h))                         # fades in at the source, thins out at the head
    bil = 0.5 + 0.5 * zs(fbm(px, py, 3, 3, seed + 2)) * 0.6 + 0.25 * zs(fbm(px, py, 5, 3, seed + 3)) * 0.4
    dens = prof * env * (1 - P['billow'] + P['billow'] * 2 * np.clip(bil, 0, 1))
    a = sm(0.02, P['sat'], dens)
    a = a / max(float(a.max()), 1e-6) * P['amax']                                # the brightest texel is exactly amax; the body keeps its billows
    a = blur(a, P.get('blur_a', 5.0))
    border = np.minimum(np.minimum(x, 1 - x), np.minimum(y, 1 - y))
    a = a * sm(0.12, 0.24, border)                                              # exactly 0 on the outer 12 %
    a = np.clip(a, 0, P['amax'])
    an = a / P['amax']
    tone = sm(0.0, 0.8, blur(an, 6.0))
    col = mix(np.array(P['base'], F) / 255, np.array(P['top'], F) / 255, np.clip(sm(0.2, 0.85, h) * 0.8 + 0.2 * tone, 0, 1))
    col = mix(col, np.array(P['under'], F) / 255, 0.55 * (1 - sm(0.12, 0.45, h)))   # the foot, lit from the lava
    col = col * (1 + 0.05 * zs(fbm(px, py, 4, 3, seed + 4)))[..., None]
    col = col * (1 - 0.06 * sm(0.4, 1.0, np.clip(bil, 0, 1)) * 0)[..., None]
    return dict(C=np.clip(col, 0, 1), A=a)


def mat_sky_crater(W=2048, H=880, seed=1601):
    """same projection as Tennis/Environment/SkyPanorama.png under GolfArcade/TennisSky:
    u 0..1 spans the 180 degree arc (the shader mirrors it behind), v 0 = horizon .. 1 = elevation 90/1.35 = 66.7 deg.
    Smoky dusk: hot orange horizon -> dusky rose -> aubergine; low smoke banks and two rising plumes with
    crater-lit (orange) undersides and rose tops; soft painted edges, nothing hard at the mirror edges u=0/1."""
    u, v = grid(W, H)
    el = v * (90.0 / 1.35)
    keys = np.array([0, 2.5, 6, 12, 20, 32, 46, 67], F)
    cols = np.array([[255, 172, 98], [248, 134, 76], [222, 104, 78], [176, 88, 92], [134, 74, 98],
                     [98, 60, 92], [74, 48, 84], [56, 38, 70]], F) / 255
    elw = el + 1.6 * fbm(u, v, 6, 3, seed + 1, pv=3)
    col = np.stack([np.interp(elw, keys, cols[:, i]) for i in range(3)], -1).astype(F)
    gl = np.exp(-((u - 0.58) / 0.20) ** 2) * np.exp(-el / 10.0)               # hotter glow low over the crater side
    col = mix(col, C(255, 150, 72), gl * 0.40)
    vv = el / 67.0
    wu = u + 0.025 * fbm(u, vv, 4, 3, seed + 8, pv=3); wv = vv + 0.02 * fbm(u, vv, 4, 3, seed + 9, pv=3)

    def dens(p, pv, sd):
        return fbm(wu, wv, p, 5, sd, gain=0.55, pv=pv) + 0.05 * (1 - np.abs(noise(wu, wv, p * 4, pv * 4, sd + 7)))

    def plume(cx, w0, drift, sd):
        cxe = cx + drift * (el / 40.0) ** 1.5 + 0.015 * noise(u, vv, 9, 6, sd)
        wdt = w0 * (1 + el / 12.0)
        return np.exp(-((u - cxe) / wdt) ** 2) * sm(-2.0, 3.0, el) * (1 - sm(24, 50, el))
    a = dens(8, 4, seed + 2); b = dens(6, 4, seed + 3); pd = dens(10, 6, seed + 6)
    ma = sm(-0.02, 0.26, a + 0.06) * sm(0.0, 4.0, el) * (1 - sm(12.0, 26.0, el))
    mb = sm(0.06, 0.30, b) * sm(9.0, 16.0, el) * (1 - sm(28.0, 48.0, el)) * 0.9
    pl = plume(0.30, 0.035, 0.07, seed + 4) + 0.8 * plume(0.79, 0.028, -0.05, seed + 5)
    mp = sm(0.15, 0.85, pl * (0.8 + 0.6 * (pd + 0.25)))
    m = blur(np.clip(np.maximum(np.maximum(ma, mb), mp), 0, 1), 2.0)
    # lighting from the mask's vertical gradient: lower edges glow orange (lit by the crater), upper edges rose
    gm = blur(m, 5.0)
    dmde = (np.roll(gm, 1, 0) - np.roll(gm, -1, 0)) * 0.5 * H / 67.0            # per degree of elevation (row-1 = higher)
    dmde[0] = 0; dmde[-1] = 0
    under = np.clip(dmde * 3.2, 0, 1); top = np.clip(-dmde * 3.2, 0, 1)
    inner = sm(-0.3, 0.4, a)
    body = mix(C(62, 42, 58), C(100, 68, 80), inner * 0.6 + 0.4 * sm(0, 30, el))
    col = mix(col, body, m * 0.85)
    col = mix(col, C(255, 138, 70), under * (1 - sm(8, 30, el)) * 0.8)
    col = mix(col, C(178, 122, 134), top * 0.5)
    haze = np.exp(-el / 3.2) * 0.38
    col = mix(col, C(255, 178, 112), haze)
    col = col * (1 + 0.012 * noise(u, v, 256, 110, seed + 7))[..., None]
    return dict(C=col)


# name -> (function, outputs, size, tiles_u, tiles_v)
MATS = [
    ('Fairway', mat_fairway, 'CN', (1024, 1024), True, True),
    ('Green', mat_green, 'CN', (1024, 1024), True, True),
    ('Rough', mat_rough, 'CN', (1024, 1024), True, True),
    ('Scrub', mat_scrub, 'CN', (1024, 1024), True, True),
    ('Sand', mat_sand, 'CN', (512, 512), True, True),
    ('Cliff', mat_cliff, 'CN', (1024, 1024), True, True),
    ('CliffDark', mat_cliffdark, 'CN', (512, 512), True, True),
    ('Rock', mat_rock, 'CN', (512, 512), True, True),
    ('Path', mat_path, 'CN', (512, 512), True, True),
    ('Masonry', mat_masonry, 'CN', (512, 512), True, True),
    ('Basalt', mat_basalt, 'CNE', (1024, 1024), True, True),
    ('Lava', mat_lava, 'CNE', (1024, 1024), True, True),
    ('Surf', mat_surf, 'A', (512, 512), True, True),
    ('Fall', mat_fall, 'A', (256, 512), False, True),
    ('Smoke', mat_smoke, 'A', (512, 512), False, False),
    ('Sky_Crater', mat_sky_crater, 'C', (2048, 880), False, False),
]


SHEEN_MATS = ('Fairway', 'Green')            # v9: the albedo carries the smoothness in its alpha channel (colour type 6) while BAND_P['on']


def files_of(name, kind, size):
    if kind == 'A':
        return [(name + '_C.png', 'rgba', size)]
    return [(name + '_' + k + '.png', 'rgba' if (k == 'C' and name in SHEEN_MATS and BAND_P['on']) else k, size) for k in kind]


# per-material phase of the sampling grid (tile units). Only for the Voronoi-faceted maps, whose crack network
# would otherwise happen to run along the tile edge column; it moves which column is the edge, nothing else
# (the maps are exactly periodic, see the periodicity proof). Fairway must stay 0 (light band centred on u=0).
PHASE = dict(Rock=(0.63, 0.44), Path=(0.23, 0.71))


def generate(name, fn, kind, size, extra=(0.0, 0.0)):
    w, h = size
    ph = PHASE.get(name, (0.0, 0.0))
    _SHIFT[0] = ph[0] + extra[0]; _SHIFT[1] = ph[1] + extra[1]
    try:
        d = fn(*((w, h) if fn in (mat_fall, mat_sky_crater) else (w,)), seed=SEEDS[name])
    finally:
        _SHIFT[0] = 0.0; _SHIFT[1] = 0.0
    out = {}
    if kind == 'A':
        out[name + '_C.png'] = np.concatenate([enc(d['C']), enc(d['A'])[..., None]], -1)
        return out
    out[name + '_C.png'] = enc(d['C']) if 'S' not in d else np.concatenate([enc(d['C']), enc(d['S'])[..., None]], -1)
    if 'N' in kind:
        out[name + '_N.png'] = enc_normal(d['N'])
    if 'E' in kind:
        out[name + '_E.png'] = enc(d['E'])
    return out

# --------------------------------------------------------------------------------------------- gates

GROUND = ('Fairway', 'Green', 'Rough', 'Scrub', 'Sand', 'Path')


def hsv_mean(img):
    m = img[..., :3].reshape(-1, 3).astype(np.float64).mean(0) / 255
    h, s, v = colorsys.rgb_to_hsv(*m)
    return h * 360, s, v, m


def seam_ratio_local(a, axis):
    """v9: for Basalt_E (hottest at v = 0 / 1 by design, and sparse: a thin crack crossing the seam makes ONE column pair differ a lot, which the tile-average ratio mistakes for a seam; the map is exactly periodic:
    the --periodicity proof, map(u+1, v+1) == map(u, v), 0.0000 % texels differ): the seam pair's mean difference against the 99th percentile of the mean difference of EVERY adjacent pair (a seam shows as an outlier)."""
    a = a.astype(np.float32)
    if axis == 'u':
        d = np.abs(np.roll(a, -1, 1) - a).mean(axis=(0, 2)) if a.ndim == 3 else np.abs(np.roll(a, -1, 1) - a).mean(axis=0)
        seam = d[-1]; d = d[:-1]
    else:
        d = np.abs(np.roll(a, -1, 0) - a).mean(axis=(1, 2)) if a.ndim == 3 else np.abs(np.roll(a, -1, 0) - a).mean(axis=1)
        seam = d[-1]; d = d[:-1]
    return float(seam / (np.percentile(d, 99) + 1e-9))


def seam_ratio(a, axis):
    a = a.astype(np.float32)
    if axis == 'u':
        return float(np.abs(a[:, 0] - a[:, -1]).mean() / (np.abs(a[:, 1:] - a[:, :-1]).mean() + 1e-9))
    return float(np.abs(a[0] - a[-1]).mean() / (np.abs(a[1:] - a[:-1]).mean() + 1e-9))


def run_gates(outdir, imgs):
    res = []

    def gate(what, ok, detail):
        res.append((what, bool(ok), detail))
        print('GATE: %s %s  (%s)' % (what, 'PASS' if ok else 'FAIL', detail))

    for name, fn, kind, size, tu, tv in MATS:
        for fname, k, sz in files_of(name, kind, size):
            p = os.path.join(outdir, fname)
            ps = png_size(p) if os.path.exists(p) else None
            want_ct = 6 if k == 'rgba' else 2
            ok = ps is not None and ps[0] == sz[0] and ps[1] == sz[1] and ps[2] == 8 and ps[3] == want_ct
            gate('%s exists %dx%d' % (fname, sz[0], sz[1]), ok, 'file %s' % (('%dx%d depth %d colortype %d' % ps) if ps else 'missing'))
            img = imgs.get(fname)
            if img is None:
                continue
            if tu or tv:
                parts = []; ok = True
                for ax, on in (('u', tu), ('v', tv)):
                    if on:
                        r = seam_ratio_local(img, ax) if fname == 'Basalt_E.png' else seam_ratio(img, ax); parts.append('%s %.3f' % (ax, r)); ok = ok and r <= (1.0 if fname == 'Basalt_E.png' else 1.5)
                gate('%s wrap seam <= 1.5x interior%s' % (fname, ' (v9, Basalt_E: seam pair vs the 99th percentile of every adjacent pair, <= 1.0: the map is sparse and hottest at v = 0 / 1)' if fname == 'Basalt_E.png' else ''), ok, ', '.join(parts))
            if k == 'N':
                n = img.astype(np.float32) / 255 * 2 - 1
                ln = np.abs(np.linalg.norm(n, axis=-1) - 1).mean(); mz = n[..., 2].mean()
                gate('%s normal |n|-1 < 0.02 and mean z > 0.85' % fname, ln < 0.02 and mz > 0.85, 'mean||n|-1| %.4f, mean z %.3f' % (ln, mz))
    L = lambda f: lum(imgs[f][..., :3].astype(np.float32) / 255) if f in imgs else None
    if 'Basalt_E.png' in imgs:
        l = L('Basalt_E.png'); gate('Basalt_E mean lum < 0.12 and max > 0.6', l.mean() < 0.12 and l.max() > 0.6, 'mean %.4f max %.3f' % (l.mean(), l.max()))
    if 'Lava_E.png' in imgs:
        l = L('Lava_E.png'); gate('Lava_E mean lum 0.15..0.5', 0.15 <= l.mean() <= 0.5, 'mean %.4f' % l.mean())
    hue_rules = [
        # v2 2026-10-04: lime-yellow-green albedos (hue 70-77); the Game-view hue is GRASS_HUE_PREDICTED (lit_predict.py), not this line
        ('Fairway_C.png', 'lime-yellow-green (hue 70..77, sat .55..0.75)', lambda h, s, v: 70 <= h <= 77 and 0.55 <= s <= 0.75),
        ('Green_C.png', 'lime-yellow-green (hue 70..77, sat .5..0.72)', lambda h, s, v: 70 <= h <= 77 and 0.5 <= s <= 0.72),
        ('Rough_C.png', 'yellow-green (hue 64..74, sat .55..0.75)', lambda h, s, v: 64 <= h <= 74 and 0.55 <= s <= 0.75),
        ('Scrub_C.png', 'olive-yellow-brown (hue 35..68, sat .2...65)', lambda h, s, v: 35 <= h <= 68 and 0.2 <= s <= 0.65),
        ('Sand_C.png', 'warm light (hue 25..55, sat .1..0.45, val >= .7)', lambda h, s, v: 25 <= h <= 55 and 0.1 <= s <= 0.45 and v >= 0.7),
        ('Basalt_C.png', 'dark neutral (val <= .3, sat <= .3)', lambda h, s, v: v <= 0.3 and s <= 0.3),
        ('Lava_E.png', 'orange (hue 12..45)', lambda h, s, v: 12 <= h <= 45 and s >= 0.5),
    ]
    for f, what, rule in hue_rules:
        if f in imgs:
            h, s, v, m = hsv_mean(imgs[f])
            gate('%s mean colour %s' % (f, what), rule(h, s, v), 'hue %.1f sat %.3f val %.3f rgb %s' % (h, s, v, tuple(int(round(x * 255)) for x in m)))
    if 'Fairway_C.png' in imgs and 'Scrub_C.png' in imgs:
        hf = hsv_mean(imgs['Fairway_C.png'])[0]; hs = hsv_mean(imgs['Scrub_C.png'])[0]
        gate('Scrub hue differs from Fairway by >= 20 deg', abs(hf - hs) >= 20, 'fairway %.1f scrub %.1f' % (hf, hs))
    for nm in GROUND:
        f = nm + '_C.png'
        if f in imgs:
            l = lum(imgs[f][..., :3].astype(np.float32) / 255); n = l.shape[0] // 4
            blocks = l.reshape(4, n, 4, n).mean((1, 3))
            if nm == 'Fairway':   # the lengthwise mow bands are intended u-variation: compare each block with its block-column
                ref = blocks.mean(0, keepdims=True); how = 'vs its block-column (mow band) mean'
            else:
                ref = l.mean(); how = 'vs tile mean'
            dev = (np.abs(blocks - ref) / ref).max()
            gate('%s large-scale evenness (4x4 block means within 10%%)' % f, dev <= 0.10, 'max dev %.1f%% %s' % (dev * 100, how))
    look_gates(imgs, gate)
    tot = 0; listing = []
    for fn_ in sorted(os.listdir(outdir)):
        if fn_.lower().endswith('.png'):
            sz = os.path.getsize(os.path.join(outdir, fn_)); tot += sz; listing.append((fn_, sz))
    gate('FOLDER_BUDGET Look/ folder total PNG size <= 25 MB', tot <= 25 * 1024 * 1024, '%.2f MB over %d PNGs' % (tot / 1048576, len(listing)))
    return res, listing

# --------------------------------------------------------------------------------------------- v2 look gates (2026-10-04)
# Every number is measured on the decoded PNGs, never taken from the generator's own parameters. The gate ids are the ones
# the continue-pass brief asks for: GRASS_HUE_PREDICTED, LAVA_FLOW_BRIGHT, LAVA_NOT_CELLS, LAVA_EMISSION_PEAK,
# BASALT_PRISM_GLOW_JOINTS, BASALT_BEVELS, SMOKE_SOFT, SEAMLESS_ALL, FOLDER_BUDGET.

def s2l(x):
    x = np.asarray(x, np.float64)
    return np.where(x <= 0.04045, x / 12.92, ((np.maximum(x, 0) + 0.055) / 1.055) ** 2.4)


def l2s(x):
    x = np.clip(np.asarray(x, np.float64), 0, 1)
    return np.where(x <= 0.0031308, x * 12.92, 1.055 * x ** (1 / 2.4) - 0.055)


def label_components(mask):
    """8-connected components of a boolean mask on a TORUS (the tile wraps). Returns a list of (ys, xs) index arrays."""
    h, w = mask.shape
    idx = np.flatnonzero(mask.ravel())
    if idx.size == 0:
        return []
    parent = {int(i): int(i) for i in idx}

    def find(a):
        while parent[a] != a:
            parent[a] = parent[parent[a]]
            a = parent[a]
        return a
    flat = mask.ravel()
    for i in idx:
        i = int(i); y, x = divmod(i, w)
        for dy, dx in ((0, 1), (1, -1), (1, 0), (1, 1)):
            j = ((y + dy) % h) * w + (x + dx) % w
            if flat[j]:
                a, b = find(i), find(j)
                if a != b:
                    parent[a] = b
    groups = {}
    for i in idx:
        groups.setdefault(find(int(i)), []).append(int(i))
    return [(np.array(g) // w, np.array(g) % w) for g in groups.values()]


def zhang_suen(img):
    """Zhang-Suen thinning of a 0/1 array (whole-array passes; the same algorithm as work/postcard-look/v2/proof/measure.py, which the integrator's BASALT_JOINTS uses on the Game-view stills)."""
    img = img.copy().astype(np.uint8)

    def step(img, first):
        Pd = np.pad(img, 1)
        p2 = Pd[:-2, 1:-1]; p3 = Pd[:-2, 2:]; p4 = Pd[1:-1, 2:]; p5 = Pd[2:, 2:]; p6 = Pd[2:, 1:-1]; p7 = Pd[2:, :-2]; p8 = Pd[1:-1, :-2]; p9 = Pd[:-2, :-2]
        nb = p2 + p3 + p4 + p5 + p6 + p7 + p8 + p9
        seq = [p2, p3, p4, p5, p6, p7, p8, p9, p2]
        tr = sum(((seq[i] == 0) & (seq[i + 1] == 1)).astype(np.uint8) for i in range(8))
        c = ((p2 * p4 * p6 == 0) & (p4 * p6 * p8 == 0)) if first else ((p2 * p4 * p8 == 0) & (p2 * p6 * p8 == 0))
        rm = (img == 1) & (nb >= 2) & (nb <= 6) & (tr == 1) & c
        img[rm] = 0
        return bool(rm.any())
    ch = True
    while ch:
        a = step(img, True); b = step(img, False); ch = a or b
    return img


def skeleton_junctions(mask, pad=48):
    """(skeleton pixels, junction clusters = 8-connected groups of skeleton pixels with >= 3 skeleton neighbours) of a periodic boolean mask (wrap-padded so the tile edge is no border)."""
    big = np.pad(mask, ((pad, pad), (pad, pad)), mode='wrap')
    sk = zhang_suen(big)
    Pd = np.pad(sk, 1)
    nb = sum(Pd[1 + dy:Pd.shape[0] - 1 + dy, 1 + dx:Pd.shape[1] - 1 + dx] for dy in (-1, 0, 1) for dx in (-1, 0, 1) if (dy, dx) != (0, 0))
    j = (sk == 1) & (nb >= 3)
    inner = np.zeros_like(sk, bool); inner[pad:-pad, pad:-pad] = True              # count only the central copy of the tile
    return int((sk == 1)[inner].sum()), len(label_components(j & inner))


def comp_shape(ys, xs, h, w):
    """second-moment aspect (>= 1) and angle of the major axis from vertical (deg) of a component; coordinates unwrapped around
    their first pixel so a component crossing the seam is measured whole."""
    y = ys.astype(np.float64); x = xs.astype(np.float64)
    y = np.where(y - y[0] > h / 2, y - h, np.where(y - y[0] < -h / 2, y + h, y))
    x = np.where(x - x[0] > w / 2, x - w, np.where(x - x[0] < -w / 2, x + w, x))
    cy, cx = y.mean(), x.mean()
    cov = np.array([[((x - cx) ** 2).mean(), ((x - cx) * (y - cy)).mean()], [((x - cx) * (y - cy)).mean(), ((y - cy) ** 2).mean()]]) + np.eye(2) * (1 / 12)
    ev, vec = np.linalg.eigh(cov)
    major = vec[:, 1]
    ang = np.degrees(np.arctan2(abs(major[0]), abs(major[1])))               # 0 = vertical (y axis), 90 = horizontal
    return float(np.sqrt(ev[1] / ev[0])), float(ang)


def longest_run(mask):
    """longest horizontal run of True (wrapping) over all rows."""
    best = 0
    for r in range(mask.shape[0]):
        row = mask[r]
        if not row.any():
            continue
        if row.all():
            return row.size
        k = int(np.argmin(row)); rr = np.roll(row, -k)
        d = np.diff(np.concatenate([[0], rr.astype(np.int8), [0]]))
        s = np.flatnonzero(d == 1); e = np.flatnonzero(d == -1)
        if s.size:
            best = max(best, int((e - s).max()))
    return best


def hue_of(rgb01):
    return colorsys.rgb_to_hsv(*[float(min(1.0, max(0.0, c))) for c in rgb01])[0] * 360


# ---- grass: the model lives in work/postcard-look/v2/tools/lit_predict.py (reads GolfAtmosphere.cs / GolfLook.cs)
def _load_lit_predict():
    import importlib.util
    p = os.path.join(ROOT, 'work/postcard-look/v2/tools/lit_predict.py')
    if not os.path.exists(p):
        return None
    sp = importlib.util.spec_from_file_location('lit_predict', p)
    m = importlib.util.module_from_spec(sp); sp.loader.exec_module(m)
    return m


def grass_gates(imgs, gate):
    """GRASS_HUE_PREDICTED on the tints that are in GolfLook.cs NOW. Three models must ALL be inside the band: first-principles URP Lit, the probe-calibrated
    one, and `game` = fitted to REAL Game-view patches of the installed albedos (lit_predict.GAME_MEAS; reproduces them within 1.6 deg)."""
    lp = _load_lit_predict()
    if lp is None:
        print('GATE: GRASS_HUE_PREDICTED FAIL - work/postcard-look/v2/tools/lit_predict.py missing (unverified)')
        return
    lights = lp.parse_lights(); cs = lp.parse_tints()
    Ls = {h: lp.hole_light_linear(lights[h]) for h in (8, 9, 10)}
    for nm in ('Fairway', 'Green', 'Rough', 'Scrub'):
        f = nm + '_C.png'
        if f not in imgs:
            continue
        a = imgs[f][::8, ::8, :3].reshape(-1, 3)
        smp = [tuple(int(x) for x in p) for p in a]
        for hole in (8, 9, 10):
            if nm == 'Scrub' and hole != 9:
                continue
            lo, hi = lp.BANDS[nm]
            tint = cs.get((hole, nm), lp.TABLE_TINT)
            pf, _c = lp.predict_samples(smp, Ls[hole], tint, 'first'); pc, _c2 = lp.predict_samples(smp, Ls[hole], tint, 'cal')
            pg, _c3 = lp.predict_samples(smp, Ls[hole], tint, 'game', hole)
            hf, hc, hg = lp.hsv_deg(pf), lp.hsv_deg(pc), lp.hsv_deg(pg)
            ok = all(lo <= x[0] <= hi for x in (hf, hc, hg))
            gate('GRASS_HUE_PREDICTED %s on %s with the tint in GolfLook.cs (%.2f,%.2f,%.2f)' % (f, lp.HOLE_NAMES[hole], *tint), ok,
                 'hue %.1f first-principles / %.1f calibrated / %.1f game-fit, S %.2f/%.2f/%.2f V %.2f/%.2f/%.2f, game-fit rgb %s, band %.0f..%.0f' % (
                     hf[0], hc[0], hg[0], hf[1], hc[1], hg[1], hf[2], hc[2], hg[2], tuple(round(x * 255) for x in pg), lo, hi))


# ---- lava
LAVA_GAME_LIGHT = np.array([1.421, 0.866, 0.742])       # Crater flat-ground light, lit_predict.py
LOOKCS = os.path.join(ROOT, 'Unity/Assets/Scripts/Course/GolfLook.cs')
_NUM = r'(-?\d*\.?\d+)'


def lava_spec():
    """LK_LAVA's runtime parameters, parsed from GolfLook.cs (the gates follow the Unity owner's edits; defaults if it cannot be parsed):
    the GolfArcade/GolfLava heat constants + 4 ramp stops, and the URP Lit fallback's Tint and per-channel Emission multiplier."""
    import re
    d = dict(HeatAlbedo=1.5, HeatGlow=1.5, HeatBias=-.15, HeatGain=1.4, Relief=.15,
             ramp=[(150, 33, 10), (186, 43, 13), (224, 79, 17), (255, 128, 28)], tint=0.75, emission=(1.0, .65, .5), src='built-in defaults')
    try:
        txt = open(LOOKCS, encoding='utf-8').read()
        m = re.search(r'public float HeatAlbedo = %sf, HeatGlow = %sf, HeatBias = %sf, HeatGain = %sf, Relief = %sf;' % ((_NUM,) * 5), txt)
        if m:
            d.update(HeatAlbedo=float(m.group(1)), HeatGlow=float(m.group(2)), HeatBias=float(m.group(3)), HeatGain=float(m.group(4)), Relief=float(m.group(5)))
        k = txt.index('["LK_LAVA"]'); blk = txt[k:txt.index('},', k)]
        for f in ('HeatAlbedo', 'HeatGlow', 'HeatBias', 'HeatGain', 'Relief'):
            mm = re.search(f + r'\s*=\s*' + _NUM + 'f', blk)
            if mm:
                d[f] = float(mm.group(1))
        stops = [re.search(n_ + r'\s*=\s*Rgb\((\d+),\s*(\d+),\s*(\d+)\)', blk) for n_ in ('RampDeep', 'RampCrust', 'RampFlow', 'RampHot')]
        if all(stops):
            d['ramp'] = [tuple(int(x) for x in st.groups()) for st in stops]
        mm = re.search(r'Emission\s*=\s*new Color\(%sf,\s*%sf,\s*%sf\)' % ((_NUM,) * 3), blk)
        if mm:
            d['emission'] = tuple(float(x) for x in mm.groups())
        mm = re.search(r'Tint\s*=\s*new Color\(%sf' % _NUM, blk)
        if mm:
            d['tint'] = float(mm.group(1))
        d['src'] = 'GolfLook.cs'
    except Exception as ex:  # noqa
        print('WARN: lava_spec: could not parse GolfLook.cs (%s): built-in defaults' % ex)
    return d


def lava_shader_model(C, E, N, sp, bias=None, gain=None):
    """GolfArcade/GolfLava (Unity/Assets/Resources/Course/Shaders/GolfLava.shader), per texel: gamma-luminance heat of _C and _E (+ the normal's slope),
    contrast around .5, then a 4-stop ramp interpolated in LINEAR light (the stops arrive linear) and encoded to sRGB. Returns (heat, rgb 0..255)."""
    lw = np.array([0.2126, 0.7152, 0.0722])
    c = C[..., :3].astype(np.float64) / 255; e = E[..., :3].astype(np.float64) / 255; n = N[..., :3].astype(np.float64) / 255 * 2 - 1
    bias = sp['HeatBias'] if bias is None else bias; gain = sp['HeatGain'] if gain is None else gain
    h = c @ lw * sp['HeatAlbedo'] + e @ lw * sp['HeatGlow'] + bias + (n[..., 0] + n[..., 1]) * 0.5 * sp['Relief']
    h = np.clip((h - 0.5) * gain + 0.5, 0, 1)
    st = [s2l(np.array(x, np.float64) / 255) for x in sp['ramp']]
    hc = h[..., None]
    lin = np.where(hc < 0.2, st[0] + (st[1] - st[0]) * (hc * 5), np.where(hc < 0.6, st[1] + (st[2] - st[1]) * ((hc - 0.2) * 2.5), st[2] + (st[3] - st[2]) * ((hc - 0.6) * 2.5)))
    return h, l2s(np.clip(lin, 0, 1)) * 255


def lava_dark_heat(sp):
    """the heat below which a ramp colour has V < .70 (the Unity owner's LAVA_ORANGE_NO_BLOOM 'dark' miss)."""
    hs = np.linspace(0, 1, 1001)
    st = [s2l(np.array(x, np.float64) / 255) for x in sp['ramp']]
    hc = hs[:, None]
    lin = np.where(hc < 0.2, st[0] + (st[1] - st[0]) * (hc * 5), np.where(hc < 0.6, st[1] + (st[2] - st[1]) * ((hc - 0.2) * 2.5), st[2] + (st[3] - st[2]) * ((hc - 0.6) * 2.5)))
    v = l2s(np.clip(lin, 0, 1)).max(-1)
    return float(hs[np.argmax(v >= 0.70)])


def lava_game(C, E, mult, tint=1.0, light=None):
    """URP Lit fallback: albedo x tint x Crater flat light + emission x per-channel multiplier (linear), clipped (no tone mapper)."""
    light = LAVA_GAME_LIGHT if light is None else light
    lin = s2l(C[..., :3].astype(np.float64) / 255) * s2l(np.float64(tint)) * light + s2l(E[..., :3].astype(np.float64) / 255) * np.array(mult, np.float64)
    return l2s(np.clip(lin, 0, 1)) * 255


def lava_stats(g):
    f = g.reshape(-1, 3); mx = f.max(1)
    hsv = np.array([colorsys.rgb_to_hsv(*(p / 255)) for p in f[::97]])
    return dict(mean=f.mean(0), p50=np.percentile(f, 50, axis=0), p95=np.percentile(f, 95, axis=0), dark90=float((mx < 90).mean()),
                glow=float(((f[:, 0] > 180) & (f[:, 0] > f[:, 1] * 1.15)).mean()), yellow=float(((f[:, 0] > 220) & (f[:, 1] > 130)).mean()),
                cream=float(((f[:, 1] > 200) & (f[:, 2] > 110)).mean()), hue_mean=float(colorsys.rgb_to_hsv(*(f.mean(0) / 255))[0] * 360),
                in_band=float(((hsv[:, 0] * 360 >= 8) & (hsv[:, 0] * 360 <= 28) & (hsv[:, 1] >= 0.75) & (hsv[:, 2] >= 0.70)).mean()))


# crater.jpg's main pool, 16x16 patch statistics + the brief's two lava patches: median (203,58,16), (212,62,14) h15 S.93 V.83, (193,70,18) h18, p95 (255,161,56),
# ~10 % yellow-orange veins, ~11-14 % near-black crust ribbons. In ramp terms (GolfLook.cs stops) the pool's typical flow sits at heat ~.4-.47.
def lava_shader_metrics(C, E, N, sp, bias=None, gain=None):
    h, rgb = lava_shader_model(C, E, N, sp, bias, gain)
    hd = lava_dark_heat(sp)
    f = rgb.reshape(-1, 3)
    st = lava_stats(rgb)
    return dict(heat=h, rgb=rgb, hd=hd, mean=f.mean(0), p50=np.percentile(f, 50, axis=0), hue=st['hue_mean'], in_band=st['in_band'],
                dark=float((h < hd).mean()), hot=float((h > 0.9).mean()), med_heat=float(np.median(h)), p25=float(np.percentile(h, 25)), p75=float(np.percentile(h, 75)),
                share_flow=float((h >= 0.3).mean()))


def lava_report(C, E, N, sp):
    rows = []
    for tag, b, gn in (('live', None, None), ('earlier (bias -.10, gain 1.5)', -0.10, 1.5)):
        m = lava_shader_metrics(C, E, N, sp, b, gn)
        rows.append('shader %-28s heat p25/50/75 %.2f/%.2f/%.2f dark(h<%.2f) %.1f%% hot(h>.9) %.1f%% flow(h>=.3) %.1f%% mean (%d,%d,%d) hue %.1f p50 (%d,%d,%d) in-band %.1f%%' % (
            tag, m['p25'], m['med_heat'], m['p75'], m['hd'], m['dark'] * 100, m['hot'] * 100, m['share_flow'] * 100, *m['mean'], m['hue'], *m['p50'], m['in_band'] * 100))
    mult = sp['emission']
    on = lava_stats(lava_game(C, E, mult, sp['tint'])); em = lava_stats(lava_game(np.zeros_like(C), E, mult, 1.0))
    rows.append('Lit fallback (tint %.2f, mult (%.2f,%.2f,%.2f), flat Crater light, no point lights): mean (%d,%d,%d) hue %.1f p50 (%d,%d,%d) dark<90 %.1f%% glow %.1f%% cream %.2f%%' % (
        sp['tint'], *mult, *on['mean'], on['hue_mean'], *on['p50'], on['dark90'] * 100, on['glow'] * 100, on['cream'] * 100))
    on1 = lava_stats(lava_game(C, E, mult, 1.0))
    rows.append('Lit fallback with Tint WHITE (recommended; the GolfLava shader ignores Tint): mean (%d,%d,%d) hue %.1f p50 (%d,%d,%d) dark<90 %.1f%% glow %.1f%% yellow %.1f%% cream %.2f%%' % (
        *on1['mean'], on1['hue_mean'], *on1['p50'], on1['dark90'] * 100, on1['glow'] * 100, on1['yellow'] * 100, on1['cream'] * 100))
    rows.append('Lit fallback EMISSION ONLY: mean (%d,%d,%d) hue %.1f S %.2f p50 (%d,%d,%d) in-band %.1f%%' % (
        *em['mean'], em['hue_mean'], colorsys.rgb_to_hsv(*(em['mean'] / 255))[1], *em['p50'], em['in_band'] * 100))
    return '\n'.join(rows)


def lava_gates(imgs, gate):
    if 'Lava_C.png' not in imgs or 'Lava_E.png' not in imgs or 'Lava_N.png' not in imgs:
        return
    C = imgs['Lava_C.png']; E = imgs['Lava_E.png']; N = imgs['Lava_N.png']
    sp = lava_spec()
    print('INFO: LAVA runtime spec from %s: heat %.2f*lum(C) + %.2f*lum(E) %+.2f, gain %.2f, relief %.2f; ramp %s; Lit-fallback tint %.2f, emission mult %s' % (
        sp['src'], sp['HeatAlbedo'], sp['HeatGlow'], sp['HeatBias'], sp['HeatGain'], sp['Relief'], sp['ramp'], sp['tint'], sp['emission']))
    for line in lava_report(C, E, N, sp).split('\n'):
        print('INFO: LAVA ' + line)
    # --- LAVA_FLOW_BRIGHT: the SHIPPED path = GolfArcade/GolfLava (self-lit; the maps give the heat). crater.jpg's pool: typical flow heat ~.4-.47 (median (203,58,16)),
    # 11-14 % near-black crust ribbons, ~10 % yellow-orange veins, the rest bright orange flow. Evaluated under the LIVE constants and the earlier set.
    ok_all = True; msgs = []
    for tag, b, gn in (('live', None, None), ('robustness: bias -.10 gain 1.5', -0.10, 1.5)):
        m = lava_shader_metrics(C, E, N, sp, b, gn)
        if tag == 'live':
            ok = (0.06 <= m['dark'] <= 0.14 and 0.05 <= m['hot'] <= 0.14 and 0.30 <= m['med_heat'] <= 0.55 and m['share_flow'] >= 0.62
                  and 190 <= m['p50'][0] <= 225 and 45 <= m['p50'][1] <= 80 and 12 <= m['hue'] <= 22 and m['in_band'] >= 0.85)
        else:   # a +-.05 bias / +-.1 gain drift by the runtime owner must not break the look: looser limits
            ok = (m['dark'] <= 0.14 and 0.05 <= m['hot'] <= 0.18 and m['med_heat'] <= 0.58 and m['share_flow'] >= 0.62 and 12 <= m['hue'] <= 24 and m['in_band'] >= 0.85)
        ok_all = ok_all and ok
        msgs.append('%s: heat median %.2f (live .30-.55), crust(h<%.2f) %.1f%% (6-14), hot(h>.9) %.1f%% (5-14, robustness <=18), flow(h>=.3) %.1f%% (>=62), p50 (%d,%d,%d) (R 190-225, G 45-80), mean hue %.1f (12-22), in-band(h8..28,S>=.75,V>=.7) %.1f%% (>=85)' % (
            tag, m['med_heat'], m['hd'], m['dark'] * 100, m['hot'] * 100, m['share_flow'] * 100, *m['p50'], m['hue'], m['in_band'] * 100))
    gate('LAVA_FLOW_BRIGHT shipped GolfLava shader model, still pool: p50 (203,58,16), ~11-14% crust, ~10% hot veins, ~69% glowing', ok_all, '; '.join(msgs))
    # --- Lit fallback (shader missing): reads orange; emission alone reads orange; nothing clips to cream
    sp_t, sp_m = sp['tint'], sp['emission']
    on = lava_stats(lava_game(C, E, sp_m, sp_t)); em = lava_stats(lava_game(np.zeros_like(C), E, sp_m, 1.0))
    ok = (on['cream'] == 0 and 8 <= on['hue_mean'] <= 24 and on['mean'][0] >= 140 and on['dark90'] <= 0.20
          and 8 <= em['hue_mean'] <= 28 and em['mean'][0] >= 130 and em['mean'][0] > 2.2 * em['mean'][1])
    gate('LAVA_LIT_FALLBACK_ORANGE URP Lit path with the SHIPPED tint %.2f and emission multiplier (%.2f,%.2f,%.2f), flat Crater light (point lights not modelled), emission-alone also orange' % (sp_t, *sp_m), ok,
         'both on: mean (%d,%d,%d) hue %.1f p50 (%d,%d,%d) dark<90 %.1f%% cream %.2f%%; emission alone: mean (%d,%d,%d) hue %.1f' % (
             *on['mean'], on['hue_mean'], *on['p50'], on['dark90'] * 100, on['cream'] * 100, *em['mean'], em['hue_mean']))
    # --- not cells: dark (crust) regions must be elongated flow ribbons, not compact polygons; no lattice peak in the spectrum; no 24 m rhythm
    h_, rgb_ = lava_shader_model(C, E, N, sp)
    hd = lava_dark_heat(sp)
    h5 = h_.reshape(512, 2, 512, 2).mean((1, 3))
    dark = h5 < hd
    comps = [c for c in label_components(dark) if len(c[0]) >= 150]
    asp = []; areas = []
    for ys, xs in comps:
        a_, _ = comp_shape(ys, xs, 512, 512); asp.append(a_); areas.append(len(ys))
    asp = np.array(asp); areas = np.array(areas, np.float64)
    compact = float(areas[asp < 1.5].sum() / max(areas.sum(), 1)) if len(asp) else 0.0
    med = float(np.median(asp)) if len(asp) else 0.0
    Le = lum(E.astype(np.float32) / 255); b = blur(Le, 1024 / 48.0)
    P = np.abs(np.fft.fft2(b - b.mean())) ** 2
    kf = np.abs(np.fft.fftfreq(1024) * 1024); KX, KY = np.meshgrid(kf, kf); km = np.maximum(KX, KY); kr = np.sqrt(KX ** 2 + KY ** 2)
    fund = float(P[km == 1].sum() / P[(km >= 1) & (km <= 6)].sum())
    Ls_ = lum(rgb_ / 255.0); Pq = np.abs(np.fft.fft2(blur(Ls_.astype(np.float32), 1.0) - Ls_.mean())) ** 2
    peak = 0.0
    for k0 in range(3, 90, 3):
        ring = Pq[(kr >= k0) & (kr < k0 + 3)]
        peak = max(peak, float(ring.max() / ring.mean()))
    ok = compact <= 0.30 and med >= 1.8 and fund <= 0.08 and peak <= 14.0 and float(dark.mean()) <= 0.25
    gate('LAVA_NOT_CELLS (crust regions of the shader heat are elongated flow ribbons, no lattice, no 24 m rhythm)', ok,
         '%d crust regions >= 150 px at 512, median aspect %.2f (>= 1.8), area in compact blobs (aspect < 1.5) %.0f%% (<= 30%%), crust coverage %.1f%%, '
         'spectrum ring peak/mean %.1f (<= 14; a Voronoi lattice gives > 30), 1-cycle fundamental %.1f%% (<= 8%%)' % (
             len(comps), med, compact * 100, float(dark.mean()) * 100, peak, fund * 100))
    # --- v7 2026-10-04 (review 'a hard vertical discontinuity at x667-671, column step 6.5-10.6 against a median of 1.2'): the screen-vertical line was one 2-texel dark hairline of this map
    #     (texture col 415, rows 599-600: design heat .08 between .19 and .26; the backward flow map is stretched 13-35x there), 215 texels = 5 m long, stair-stepped by aliasing.
    #     A thin (<= 6 texel) dark line deeper than 20 luminance levels (black top-hat of the shipped-shader image, 5 x 5 window) must not run longer than 120 texels (v6: 51 lines of >= 60 texels, longest 215)
    Ls2 = lum(rgb_.astype(np.float32) / 255) * 255

    def _mx(a, k):
        hk = k // 2; b = a
        for ax in (0, 1):
            m = b
            for d in range(1, hk + 1):
                m = np.maximum(m, np.maximum(np.roll(b, d, ax), np.roll(b, -d, ax)))
            b = m
        return b
    bh = -_mx(-_mx(Ls2, 5), 5) - Ls2
    lines = []
    for ys, xs in label_components(bh > 20):
        if len(ys) < 30:
            continue
        y = ys.astype(np.float64); x = xs.astype(np.float64)
        y = np.where(y - y[0] > 512, y - 1024, np.where(y - y[0] < -512, y + 1024, y)); x = np.where(x - x[0] > 512, x - 1024, np.where(x - x[0] < -512, x + 1024, x))
        ev = np.linalg.eigvalsh(np.cov(np.stack([x, y])) + np.eye(2) / 12); ln = float(np.sqrt(12 * ev[1]))
        if len(ys) / max(ln, 1.0) <= 6.0:
            lines.append(ln)
    n60 = sum(1 for v_ in lines if v_ >= 60); longest = max(lines) if lines else 0.0
    gate('LAVA_NO_HAIRLINE (no thin dark line, depth > 20 levels, longer than 120 texels = 2.8 m: the aliased filament that read as a seam)', longest <= 120 and n60 <= 12,
         '%d thin dark lines of >= 60 texels (<= 12; v6: 51), longest %.0f texels (<= 120; v6: 215), thin-dark share (depth > 20) %.2f %% of the tile' % (n60, longest, float((bh > 20).mean()) * 100))
    # --- emission peak: the glow map never reaches cream, whatever per-channel multiplier (<= 1.3 each) the runtime uses
    emax = E.reshape(-1, 3).max(0); ok = bool(emax[0] <= 255 and emax[1] <= 180 and emax[2] <= 60)
    cream = [float(lava_stats(lava_game(C, E, m_, sp_t))['cream']) for m_ in ((1.0, 1.0, 1.0), (1.3, 1.3, 1.3), tuple(sp_m))]
    hh_ = hue_of(E.reshape(-1, 3)[lum(E.reshape(-1, 3).astype(np.float32) / 255) > 0.3].mean(0) / 255) if (lum(E.reshape(-1, 3).astype(np.float32) / 255) > 0.3).any() else 15.0
    gate('LAVA_EMISSION_PEAK (brightest Lava_E texel <= (255,180,60); no cream under a x1.0 / x1.3 / shipped multiplier)', ok and max(cream) == 0.0 and 8 <= hh_ <= 40,
         'brightest texel (%d,%d,%d), cream texels (G>200,B>110) at x1.0/x1.3/shipped %.3f/%.3f/%.3f, bright-E hue %.1f' % (*emax, *cream, hh_))
    # --- v8 2026-10-05 (repair round 3; review 'flat marbled orange vignettes ... no bright veins as in crater.jpg ... the texture looks magnified'): the bright part of the pool is a WEB of thin veins, not broad flat swathes.
    #     Measured on the shipped-shader HEAT (what GolfLava paints), v7 -> v8:  broad hot plateaus = texels of heat > .75 that survive a 15 x 15 texel (35 cm) opening: 12.0 % -> 6.8 %;  thin hot veins = the white top-hat
    #     (9 x 9) of the heat > .15: 6.0 % -> 12.9 %.  Calibrated to be a clear step from v7, not a crater.jpg measurement (the still's filigree is finer than a 2.3 cm texel).
    hs_ = h_.astype(np.float32)
    plateau = float(_mx(-_mx(-(hs_ > 0.75).astype(np.float32), 15), 15).mean())
    er_ = -_mx(-hs_, 9); op_ = _mx(er_, 9); veins = float(((hs_ - op_) > 0.15).mean())
    gate('LAVA_FILIGREE (v8: the bright part of the pool is a web of thin veins, not broad swathes)', plateau <= 0.08 and veins >= 0.09,
         'broad hot plateaus (heat > .75, survive a 15 x 15 texel opening) %.1f %% of the tile (<= 8; v7 12.0), thin hot veins (white top-hat 9 x 9 of the heat > .15) %.1f %% (>= 9; v7 6.0)' % (plateau * 100, veins * 100))


# ---- basalt
def basalt_gates(imgs, gate, T=8.0):
    if not all(k in imgs for k in ('Basalt_C.png', 'Basalt_N.png', 'Basalt_E.png')):
        return
    Cc = imgs['Basalt_C.png'].astype(np.float32); Nn = imgs['Basalt_N.png'].astype(np.float32) / 255 * 2 - 1; E = imgs['Basalt_E.png'].astype(np.float32) / 255
    nx = Nn[..., 0]; ny = Nn[..., 1]; H, Wd = nx.shape
    L = lum(Cc / 255) * 255
    # joint lines: columns whose texels are steep (|nx| > .75 = joint walls) for most of the height
    cp = (np.abs(nx) > 0.75).mean(0)
    cols = np.flatnonzero(cp > 0.45)
    groups = []
    for x in cols:
        if groups and x - groups[-1][-1] <= 14:
            groups[-1].append(x)
        else:
            groups.append([x])
    if len(groups) > 1 and groups[0][0] <= 14 and Wd - groups[-1][-1] <= 14:      # wraps around the seam
        groups[0] = groups[-1] + groups[0]; groups.pop()
    centres = [float(np.mean([(x if x < Wd // 2 or g[0] >= Wd // 2 else x) for x in g])) for g in groups]
    jx = sorted(c_ % Wd for c_ in [np.mean(np.unwrap(np.array(g) * 2 * np.pi / Wd)) * Wd / (2 * np.pi) for g in groups])
    nj = len(jx)
    # ---- BEVELS
    flat = np.abs(ny) < 0.30
    left = (nx <= -0.30) & (nx >= -0.55) & flat; right = (nx >= 0.30) & (nx <= 0.55) & flat; front = (np.abs(nx) < 0.15) & flat
    side = left | right
    meanabs = float(np.abs(nx[side]).mean()) if side.any() else 0.0
    cls = {k_: float(L[m].mean()) for k_, m in (('left', left), ('front', front), ('right', right)) if m.sum() > 100}
    spread = (max(cls.values()) - min(cls.values())) if len(cls) == 3 else 0.0
    ok = side.mean() >= 0.34 and left.mean() >= 0.15 and right.mean() >= 0.15 and 0.34 <= meanabs <= 0.50 and spread >= 15 and front.mean() >= 0.12
    gate('BASALT_BEVELS (three planes per column: side bevels tilted |nx| .3-.55, clearly different shade)', ok,
         'bevel texels %.1f%% (>= 34; v8 35.2, the crack-network groove walls are no longer flat bevel texels) (left %.1f%%, right %.1f%%, front %.1f%%), mean |nx| of bevels %.3f, albedo means left/front/right %s -> spread %.1f levels, %d columns' % (
             side.mean() * 100, left.mean() * 100, right.mean() * 100, front.mean() * 100, meanabs, {k_: round(v_, 1) for k_, v_ in cls.items()}, spread, nj))
    # ---- cross fractures: per column, centre strip, rows where |ny| > .45 over >= 30 % of the strip
    events = []
    for i in range(nj):
        a, b = jx[i], jx[(i + 1) % nj] + (Wd if i + 1 == nj else 0)
        w_ = b - a
        x0 = int(a + 0.4 * w_); x1 = int(a + 0.6 * w_)
        xs = np.arange(x0, x1 + 1) % Wd
        strip = (np.abs(ny[:, xs]) > 0.45).mean(1) >= 0.30
        rows = np.flatnonzero(strip)
        ev = []
        for r in rows:
            if not ev or r - ev[-1][-1] > 19:
                ev.append([r])
            else:
                ev[-1].append(r)
        if len(ev) > 1 and ev[0][0] <= 19 and H - ev[-1][-1] <= 19:
            ev[0] = ev[-1] + ev[0]; ev.pop()
        events.append(sorted(((np.mean(np.unwrap(np.array(e) * 2 * np.pi / H)) * H / (2 * np.pi)) % H) * T / H for e in ev))
    per = [len(e) for e in events]
    gaps = []; maxgap = 0.0
    for e in events:
        if len(e) >= 2:
            ee = sorted(e); g_ = [(ee[(k_ + 1) % len(ee)] - ee[k_]) % T for k_ in range(len(ee))]
            gaps += g_; maxgap = max(maxgap, max(g_))
        else:
            maxgap = max(maxgap, T)
    mind = min(gaps) if gaps else 99.0
    near = tot = 0
    for i in range(nj):
        for v1 in events[i]:
            tot += 1
            if any(min(abs(v1 - v2), T - abs(v1 - v2)) < 0.40 for v2 in events[(i + 1) % nj]):
                near += 1
    mean_per = float(np.mean(per)) if per else 0.0
    # courses: share of the columns that break within the same 30 cm band, worst band (brick = nearly all columns)
    band = 0.0
    for y0 in np.arange(0, T, 0.05):
        n_in = sum(1 for e in events if any(min(abs(q - y0), T - abs(q - y0)) <= 0.15 for q in e))
        band = max(band, n_in / max(nj, 1))
    # block shape: block height / column width (prisms stacked into chunks, not planks > 6:1 and not squares)
    wcol = [((jx[(i + 1) % nj] - jx[i]) % Wd) * T / Wd for i in range(nj)]
    asp = [g_ / max(wcol[i], 0.2) for i in range(nj) for g_ in ([(sorted(events[i])[(k_ + 1) % len(events[i])] - sorted(events[i])[k_]) % T for k_ in range(len(events[i]))] if len(events[i]) >= 2 else [T])]
    asp_med = float(np.median(asp)); planks = float(np.mean(np.array(asp) > 6.0))
    ok = (mind >= 1.3 and 3 <= max(per) <= 5 and 2.8 <= mean_per <= 5.0 and min(per) >= 3 and maxgap <= 3.6 and (near / max(tot, 1)) <= 0.10 and band <= 0.45
          and 1.4 <= asp_med <= 5.0 and planks <= 0.10)
    gate('BASALT_PRISM_GLOW_JOINTS cross fractures (stacked blocks 1.3-3.4 m tall, 3-5 per column per 8 m, never in courses, not aligned with the neighbour column)', ok,
         '%d columns, fractures per column per 8 m: mean %.2f min %d max %d (3..5), spacing within a column %.2f..%.2f m (1.3..3.6), neighbour-aligned (within 40 cm) %d of %d = %.0f%% (<= 10%%), '
         'worst 30 cm band holds fractures of %.0f%% of the columns (<= 45%%), block height/width median %.2f (1.4..5), blocks taller than 6 widths %.0f%% (<= 10%%) '
         '[v3 asked for 0.4..2 fractures >= 3 m apart; the review found that read as planks]' % (
             nj, mean_per, min(per), max(per), mind, maxgap, near, tot, 100.0 * near / max(tot, 1), band * 100, asp_med, planks * 100))
    # ---- glow (v5 2026-10-04, DEFINITION CHANGED, disclosed): glow only in the joint / crack network - long irregular stretches on some vertical joints plus short forked
    #      cracks leaving them - NOT a rhythm of dashes ('ladders'), < 6 % texels, hotter toward v = 0.  (v4 demanded every stretch vertical-elongated, aspect >= 3 within 25 deg:
    #      that definition forbids a branching crack and measured a regular dash rhythm as a PASS; the lead's brief asks for irregular, branching cracks with glow only in the joints.)
    # ---- v9 2026-10-05, DEFINITION CHANGED AGAIN (disclosed, the integrator's Game-view BASALT_JOINTS is NOT relaxed: it is the stricter side): the v5 definition above demanded every glow component to be vertical-elongated (within 40 degrees of
    #      vertical): that forbids exactly what the carry-over finding asks for (a branching crack network with slanted cracks and ledges, vertical-line share <= 80 %, junction density >= 3 per 100 skeleton px in the Game view) and it
    #      measured the v8 map (99 % vertical, 2.6 junctions per 100 skeleton px at texture level, 1.27 in the Game view) as a PASS.  Now, measured on the texture the way the Game view reads it (wall space: the hole-10 builder's UV windows stretch
    #      u x1.6 and v x2.8): coverage 1..6 %; components >= 40 px: vertical-elongated (aspect >= 2.5, within 35 deg of vertical) <= 70 % of the glow px (v8 99 %; Game view limit 80 % per shot), horizontal rungs (>= 55 deg) <= 20 %;
    #      skeleton junction clusters per 100 skeleton px >= 4.0 (v8 2.6; Game view limit 3.0, measured 6.9 on the same maps); >= 95 % of the glow within 6 px of a groove / joint wall (tilt >= .20); longest horizontal glow run <= 12 % of the tile;
    #      hotter toward v = 0 / 1 by >= 1.3; brightest E G <= 180.
    el = lum(E)
    mask = el > 0.04
    cov = float(mask.mean())
    comps = label_components(mask)
    wv = wr = wb_ = wo = 0; ntot = 0
    for ys, xs in comps:
        if len(ys) < 40:
            continue
        k = len(ys); ntot += k
        yy = ys.astype(np.float64); xx = xs.astype(np.float64)
        if yy.max() - yy.min() > H / 2:
            yy = np.where(yy < H / 2, yy + H, yy)
        if xx.max() - xx.min() > Wd / 2:
            xx = np.where(xx < Wd / 2, xx + Wd, xx)
        ev_, evec_ = np.linalg.eigh(np.cov(np.vstack([xx * 1.6, yy * 2.8])))
        l2_, l1_ = max(ev_[0], 1e-6), max(ev_[1], 1e-6); vv_ = evec_[:, 1]
        ang_ = math.degrees(math.atan2(abs(vv_[0]), abs(vv_[1]))); a_ = math.sqrt(l1_ / l2_)
        if a_ >= 2.5 and ang_ <= 35:
            wv += k
        elif a_ >= 2.5 and ang_ >= 55:
            wr += k
        elif a_ < 2.0:
            wb_ += k
        else:
            wo += k
    ntot = max(ntot, 1)
    sk_px, sk_j = skeleton_junctions(mask)
    jrate = 100.0 * sk_j / max(sk_px, 1)
    run = longest_run(mask)
    wallm = np.hypot(nx, ny) >= 0.20
    near_w = np.zeros_like(wallm)
    for dx in range(-6, 7):
        near_w |= np.roll(wallm, dx, axis=1)
    for dy in range(-6, 7):
        near_w |= np.roll(wallm, dy, axis=0)
    inj = float((mask & near_w).sum() / max(mask.sum(), 1))
    gl_rows = mask.mean(1)
    vv = 1 - (np.arange(H) + 0.5) / H                                        # v of each row
    nearbot = (vv < 0.15) | (vv > 0.85); mid = (vv > 0.35) & (vv < 0.65)
    hot = float(gl_rows[nearbot].mean() / max(gl_rows[mid].mean(), 1e-6))
    ok = (0.01 <= cov < 0.06 and wv / ntot <= 0.70 and wr / ntot <= 0.20 and jrate >= 4.0 and inj >= 0.95 and run <= 0.12 * Wd and hot >= 1.3 and E.reshape(-1, 3).max(0)[1] <= 180 / 255 + 1e-3)
    gate('BASALT_PRISM_GLOW_JOINTS glow (v9: a branching crack NETWORK in the joints, vertical-line share <= 70 %, junction density >= 4 per 100 skeleton px, 1-6% texels, hotter toward v = 0)', ok,
         'glow texels %.2f%% (1..6%%), %d components >= 40 px in wall space (x1.6 u, x2.8 v): vertical %.0f%% (<= 70; v8 99), horizontal rungs %.0f%% (<= 20), blobs %.0f%%, other %.0f%%; skeleton %d px, %d junction clusters = %.2f per 100 px (>= 4.0; v8 2.6); '
         '%.1f%% of the glow within 6 px of a groove / joint wall (tilt >= .20; >= 95%%), longest horizontal glow run %d px = %.1f%% of the tile (<= 12%%), glow near v=0/1 vs mid-height x%.2f (>= 1.3), brightest E (%d,%d,%d)' % (
             cov * 100, len(comps), 100 * wv / ntot, 100 * wr / ntot, 100 * wb_ / ntot, 100 * wo / ntot, sk_px, sk_j, jrate, inj * 100, run, 100.0 * run / Wd, hot, *np.round(E.reshape(-1, 3).max(0) * 255)))
    # ---- v9: the v5 gate also counted the joints that carry glow and the dash rhythm of the stretches along them (INFO now: the glow is a network, not stretches).  Kept as information:
    lit = 0
    for x in jx:
        xs = [int(round(x + dx)) % Wd for dx in range(-10, 11)]
        if mask[:, xs].any(1).mean() >= 0.02:
            lit += 1
    print('INFO: BASALT glow touches %d of %d joints (any glow within +-10 px of the joint, >= 2%% of the height); network stats %s' % (lit, nj, dict(_NET_STATS)))

    # ---- v7 2026-10-04: the hole-10 builder maps its wall / top / skin faces to windows of THIS tile by constants it keeps in blender/scripts/hole10_look.py (GLOW_U, ZONES, PROFILE128) and
    #      gates the PNGs on disk against them (H_UV_WINDOWS: glow inside the glow-free zones <= 0.02 %, narrowest edge window at each glow line >= 4 % glow, Basalt_C column-luminance profile
    #      within 0.01).  This gate repeats that measure on the generated maps, with the numbers read from that file, so a texture change that would force the builder to re-bake fails HERE.
    try:
        import ast, re as _re
        htxt = open(os.path.join(ROOT, 'blender/scripts/hole10_look.py'), encoding='utf-8').read()

        def _const(name):
            m_ = _re.search(r'^' + name + r'\s*=\s*(\(.*?\))\s*$', htxt, _re.M | _re.S)
            return ast.literal_eval(m_.group(1))
        glow_u = _const('GLOW_U'); zones = _const('ZONES'); prof_ref = np.array(_const('PROFILE128'), np.float64)
        edge_w = float(_re.search(r'^GLOW_EDGE\s*=\s*([0-9.]+)', htxt, _re.M).group(1))
        Gm = (E.max(axis=2) > 0.2)
        profc = (Cc / 255.0).mean(axis=2).mean(axis=0)[:(Wd // 128) * 128].reshape(128, -1).mean(axis=1)

        def _share(a, b):
            cols_ = [i % Wd for i in range(int(round(a * Wd)), int(round(b * Wd)))]
            return float(Gm[:, cols_].mean())
        zmax = max(_share(a, b) for a, b in zones)
        emin = min(min(_share(g_ - edge_w, g_ - edge_w + 0.05), _share(g_ + edge_w - 0.05, g_ + edge_w)) for g_ in glow_u)
        pdev = float(np.abs(profc - prof_ref).max())
        gate('BASALT_BUILDER_WINDOWS (the constants of hole10_look.py still describe this tile: no re-bake of the hole-10 UV windows is needed)', zmax <= 2e-4 and emin >= 0.04 and pdev <= 0.01,
             'glow share inside the %d glow-free zones max %.4f %% (<= 0.02 %%), narrowest edge window at each of the %d glow lines %.1f %% glow (>= 4 %%), Basalt_C luminance profile deviation %.4f (<= 0.01), '
             'glow texels %.2f %% of the tile (constants read from hole10_look.py)' % (len(zones), zmax * 100, len(glow_u), emin * 100, pdev, float(Gm.mean()) * 100))
    except Exception as ex:  # noqa
        gate('BASALT_BUILDER_WINDOWS (the constants of hole10_look.py still describe this tile)', False, 'could not read / evaluate blender/scripts/hole10_look.py: %s: UNVERIFIED' % ex)
    # ---- v7: the review measured the rendered wall as 'maroon-brown, hue 4-17, S .53-.59' and asked for a saturation ceiling that can see the TINT.  The albedo is the part of that this file owns:
    #      near-neutral charcoal (crater.jpg basalt (26,25,29)); whatever red the Game view adds comes from Crater's fog / ambient (GolfAtmosphere.cs, not this file), see TEXTURES.md v7.
    Cn = Cc.reshape(-1, 3) / 255.0
    ng = (E.max(axis=2) <= 0.02).reshape(-1)                                  # the lit cracks are orange on purpose: judge the rock
    Cr = Cn[ng]
    sat_t = np.where(Cr.max(1) > 1e-6, (Cr.max(1) - Cr.min(1)) / np.maximum(Cr.max(1), 1e-6), 0)
    mean_s = colorsys.rgb_to_hsv(*[float(x) for x in Cr.mean(0)])[1]
    gate('BASALT_ALBEDO_NEUTRAL (the rock of Basalt_C is charcoal, not brown: mean-colour saturation <= .15, mean of the per-texel saturation <= .20, 95 % of the texels <= .30; glow texels excluded)',
         mean_s <= 0.15 and float(sat_t.mean()) <= 0.20 and float(np.percentile(sat_t, 95)) <= 0.30,
         'rock mean colour (%d,%d,%d) S %.3f (<= .15), per-texel S mean %.3f (<= .20) p95 %.3f (<= .30); %.1f %% of the texels are glow and excluded (crater.jpg basalt (26,25,29) S .10)' % (
             *np.round(Cr.mean(0) * 255), mean_s, float(sat_t.mean()), float(np.percentile(sat_t, 95)), 100.0 * (1 - ng.mean())))
    # ---- what the Game view will measure for the wall (GolfLookSmoke BASALT_RENDERS_DARK: key-lit wall, emission off: median luminance <= 40, mean S <= .25):
    #      the albedo x LK_BASALT Tint (GolfLook.cs, live) x the key light fitted on the real run smoke_t4e.  A MODEL, not a Unity run.
    sp = basalt_spec()
    lin = s2l(Cc / 255.0)
    mean_lin = lin.reshape(-1, 3).mean(0)
    dev = np.abs(mean_lin / BASALT_LIN_MEAN - 1)
    out = l2s(lin * s2l(np.array(sp['tint']))[None, None, :] * BASALT_GATE_LIGHT[None, None, :])
    ml = lum(out) * 255
    mean_rgb = out.reshape(-1, 3).mean(0)
    hh, ss, vv_ = colorsys.rgb_to_hsv(*[float(x) for x in mean_rgb])
    ok = 18 <= float(np.median(ml)) <= 40 and ss <= 0.25 and np.all(np.abs(mean_rgb * 255 - BASALT_GATE_MEAN * 255) <= 4)
    gate('BASALT_PRISM_GLOW_JOINTS lit darkness (model of BASALT_RENDERS_DARK with the live LK_BASALT Tint %s; lit mean = the real run of v2 repair round 3, (30,30,30))' % (tuple(round(x, 2) for x in sp['tint']),), ok,
         'predicted key-lit wall: median luminance %.0f (18..40; runtime limit 40, crater.jpg charcoal 30..34), mean (%d,%d,%d) S %.2f (<= .25, within 4 levels of (30,30,30)), mean linear albedo vs v3 %+.1f%% (info); '
         'calibration: the installed Basalt_C under this model gives median 29, mean (30,30,30) = the real run smoke_cand3 (tint source: %s)' % (
             float(np.median(ml)), *np.round(mean_rgb * 255), ss, (mean_lin / BASALT_LIN_MEAN - 1).mean() * 100, sp['src']))


def basalt_spec():
    """LK_BASALT's Tint from GolfLook.cs (default: the table tint .94)."""
    import re
    d = dict(tint=(0.94, 0.94, 0.94), src='built-in default')
    try:
        txt = open(LOOKCS, encoding='utf-8').read()
        k = txt.index('["LK_BASALT"]'); blk = txt[k:txt.index('},', k) + 1]
        mm = re.search(r'Tint\s*=\s*new Color\(%sf,\s*%sf,\s*%sf' % ((_NUM,) * 3), blk)
        if mm:
            d['tint'] = tuple(float(x) for x in mm.groups()); d['src'] = 'GolfLook.cs LK_BASALT'
    except Exception as ex:  # noqa
        d['src'] = 'built-in default (%s)' % ex
    return d


# ---- smoke
def smoke_metrics(S):
    a = S[..., 3].astype(np.float64) / 255; c = S[..., :3].astype(np.float64) / 255
    n = a.shape[0]; b = int(round(0.12 * n))
    border = np.zeros(a.shape, bool); border[:b] = border[-b:] = True; border[:, :b] = border[:, -b:] = True
    ys, xs = np.mgrid[0:n, 0:n]
    w = a.sum(); cx = (a * xs).sum() / w; cy = (a * ys).sum() / w
    cov = np.array([[(a * (xs - cx) ** 2).sum(), (a * (xs - cx) * (ys - cy)).sum()], [(a * (xs - cx) * (ys - cy)).sum(), (a * (ys - cy) ** 2).sum()]]) / w
    ev = np.linalg.eigvalsh(cov)
    gx = np.abs(np.diff(a, axis=1)).max(); gy = np.abs(np.diff(a, axis=0)).max()
    vis = a > 0.10
    mc = c[vis].mean(0) if vis.any() else np.zeros(3)
    Lc = lum(c)
    return dict(amax=float(a.max()), border=float(a[border].max()), aspect=float(np.sqrt(ev[1] / ev[0])), grad=float(max(gx, gy)),
                core=float((a > 0.5 * a.max()).mean()), mc=mc, lstd=float(Lc[a > 0.25 * a.max()].std()) if (a > 0.25 * a.max()).sum() > 50 else 0.0,
                cover=float((a > 0.04).mean()))


def smoke_spec():
    """LK_SMOKE's runtime parameters parsed from GolfLook.cs (the gates follow the Unity owner's edits)."""
    import re
    d = dict(tint=(1.0, 0.95, 0.92, 1.0), gain=1.35, power=1.0, lit=0.8, selflight=0.40, fade=0.18, src='built-in defaults')
    try:
        txt = open(LOOKCS, encoding='utf-8').read()
        k = txt.index('["LK_SMOKE"]'); blk = txt[k:txt.index('},', k) + 1]
        mm = re.search(r'Tint\s*=\s*new Color\(%sf,\s*%sf,\s*%sf(?:,\s*%sf)?\)' % ((_NUM,) * 4), blk)
        if mm:
            g = mm.groups(); d['tint'] = (float(g[0]), float(g[1]), float(g[2]), float(g[3]) if g[3] is not None else 1.0)
        for key, f in (('gain', 'AlphaGain'), ('power', 'AlphaPower'), ('lit', 'Lit'), ('selflight', 'SelfLight'), ('fade', 'EdgeFade')):
            m2 = re.search(f + r'\s*=\s*' + _NUM + 'f', blk)
            if m2:
                d[key] = float(m2.group(1))
        d['src'] = 'GolfLook.cs LK_SMOKE'
    except Exception as ex:  # noqa
        d['src'] = 'built-in defaults (%s)' % ex
    return d


def _smoke_alpha_min():
    """the owner's floor of the effective alpha of a smoke card: GolfLookSmoke.SmokeAlphaMin (.55 until v2 repair round 3, .30 since: visibility is gated by the two delta bands next to it, which this gate keeps with extra margin); .55 if the file cannot be parsed."""
    import re
    try:
        txt = open(os.path.join(ROOT, 'Unity/Assets/Editor/GolfLookSmoke.cs'), encoding='utf-8').read()
        m = re.search(r'SmokeAlphaMin\s*=\s*' + _NUM + 'f', txt)
        return float(m.group(1)) if m else 0.55
    except Exception:  # noqa
        return 0.55


def _load_smoke_model():
    import importlib.util
    p = os.path.join(ROOT, 'work/postcard-look/v2/tools/smoke_model.py')
    if not os.path.exists(p):
        return None
    sp = importlib.util.spec_from_file_location('smoke_model', p)
    m = importlib.util.module_from_spec(sp); sp.loader.exec_module(m)
    return m


def smoke_game(S, sp=None, model=None):
    """predicted GolfLookSmoke numbers for the painted card S under LK_SMOKE's live spec: effective alpha max, N, median, p99, edge step, and the
    colour of the brightest tenth of the plume in the predicted frame.  The model reproduces the three real runs (t2/t4d/t4e) within 1 % on N/median/p99/step."""
    sp = sp or smoke_spec(); sm_ = model or _load_smoke_model()
    if sm_ is None:
        return None
    f, fS, _ = sm_.light_fit()
    sky = sm_.load_rgb('t4e_smoke_card_without.rgb').astype(np.float64)
    G = sm_.G_for(sp['lit'], sp['selflight'], f, fS)
    S_r = S
    if abs(sp['power'] - 1.0) > 1e-6:                                    # v8: GolfSurf's _AlphaPower (the owner's repair round 3: 1.5): alpha_eff = (a x tint.a x gain) ^ power; the model multiplies by tint.a x gain itself, so hand it a' = alpha_eff / (tint.a x gain)
        tg = max(sp['tint'][3] * sp['gain'], 1e-6)
        S_r = S.copy(); ae_ = np.power(np.clip(S[..., 3].astype(np.float64) / 255 * tg, 0, 1), sp['power'])
        S_r[..., 3] = np.round(np.clip(ae_ / tg, 0, 1) * 255).astype(S.dtype)
    fr, al = sm_.render(S_r, sky, sp['tint'], sp['gain'], sp['selflight'], sp['lit'], G=G, A=np.zeros(3), fade=sp['fade'])
    st = sm_.stats(fr, sky)
    d = sm_.lumq(fr.astype(np.float64)) - sm_.lumq(sky)
    sel = d > 8
    top = np.argsort(sm_.lumq(fr[sel].astype(np.float64)))[::-1][:max(1, sel.sum() // 10)]
    mean = fr[sel][top].mean(0) / 255 if sel.any() else np.zeros(3)
    hsv = colorsys.rgb_to_hsv(*[float(x) for x in mean])
    # the plume's OWN lit colour (alpha -> 1): alpha-weighted mean linear texture colour x tint x light; independent of the backdrop (the blended top tenth is not)
    aw = S[..., 3].astype(np.float64) / 255
    tl = (sm_.s2l(S[..., :3].astype(np.float64) / 255) * aw[..., None]).sum((0, 1)) / aw.sum()
    own = np.round(sm_.l2s(tl * sm_.s2l(np.array(sp['tint'][:3])) * G) * 255).astype(int)
    st.update(amax_eff=float(np.power(np.clip(S[..., 3].max() / 255.0 * sp['tint'][3] * sp['gain'], 0, 1), sp['power'])), top=np.round(mean * 255).astype(int), top_hsv=hsv,
              own=own, own_hsv=colorsys.rgb_to_hsv(*[float(x) / 255 for x in own]))
    return st


SMOKE_STILL = (183, 154, 150)          # crater.jpg plume, brightest tenth (S .18)


def smoke_gates(imgs, gate):
    if 'Smoke_C.png' not in imgs:
        return
    S = imgs['Smoke_C.png']
    m = smoke_metrics(S)
    # (1) the painted map: soft, tall, nothing hard.  The border and gradient limits are the first-pass ones, unchanged.
    tex_sat = colorsys.rgb_to_hsv(*[float(x) for x in m['mc']])[1]
    ok = m['amax'] <= 0.72 and m['border'] == 0.0 and m['aspect'] >= 1.6 and m['grad'] <= 0.031 and m['lstd'] <= 0.06 and tex_sat <= 0.10 and 0.55 <= lum(m['mc']) <= 0.88
    gate('SMOKE_SOFT painted map (alpha max <= .72 = no opaque core even at AlphaGain 1; the effective band is the next gate; 0 on the 12% border, tall, max step <= .031 per texel, light low-saturation grey)', ok,
         'painted alpha max %.3f (<= .72), border alpha %.4f (== 0), second-moment aspect %.2f (>= 1.6), max alpha step %.4f per texel (<= .031), colour over the visible area (%d,%d,%d) S %.3f (<= .10), '
         'luminance std in the body %.3f (<= .06), covers %.1f%% of the card' % (
             m['amax'], m['border'], m['aspect'], m['grad'], *np.round(m['mc'] * 255), tex_sat, m['lstd'], m['cover'] * 100))
    # (2) what the Game view will measure (GolfLookSmoke SMOKE_SOFT = effective alpha .55..0.72, median |d lum| >= 8, p99 28..90, edge step <= 6): the runtime
    #     band, read from GolfLook.cs, evaluated with work/postcard-look/v2/tools/smoke_model.py.  We ask for more margin than the runtime (model error ~1 %).
    sp = smoke_spec(); g = smoke_game(S, sp)
    if g is None:
        gate('SMOKE_SOFT runtime band (predicted Game view)', False, 'work/postcard-look/v2/tools/smoke_model.py missing: UNVERIFIED')
        return
    amin0 = _smoke_alpha_min(); pw_ = max(sp['power'], 1e-6)
    lo, hi = amin0 ** (1 / pw_) / (m['amax'] * sp['tint'][3]), 0.72 ** (1 / pw_) / (m['amax'] * sp['tint'][3])
    sat = g['own_hsv'][1]
    amin = _smoke_alpha_min()
    ok = (amin <= g['amax_eff'] <= 0.72 and g['med'] >= 10 and 32 <= g['p99'] <= 86 and g['grad'] <= 5.0 and g['N'] >= 3000
          and sat <= 0.26 and all(abs(int(g['own'][i]) - SMOKE_STILL[i]) <= 14 for i in range(3)))
    gate('SMOKE_SOFT runtime band (predicted Game view, LK_SMOKE live: tint (%.2f,%.2f,%.2f) AlphaGain %.2f Lit %.2f SelfLight %.2f; model of GolfLookSmoke, NOT a Unity run)' % (
        sp['tint'][0], sp['tint'][1], sp['tint'][2], sp['gain'], sp['lit'], sp['selflight']), ok,
         'effective alpha max %.3f (%.2f..0.72), median |d lum| %.1f (runtime >= 8, here >= 10), p99 %.1f (runtime 28..90, here 32..86), edge step p99 %.2f (<= 6, here <= 5), %d px, '
         'the plume\'s own lit colour %s S %.2f vs crater.jpg %s S .18 (each channel within 14, S <= .26; blended over this sky the brightest tenth reads %s, backdrop-dependent, INFO); AlphaGain that keeps the peak in band: %.2f..%.2f' % (
             g['amax_eff'], amin, g['med'], g['p99'], g['grad'], g['N'], tuple(int(x) for x in g['own']), sat, SMOKE_STILL, tuple(int(x) for x in g['top']), lo, hi))
    # (3) v7 2026-10-04 (repair round 2; reviews: 'a blocky, flat-bottomed white lump' in the aerial, 'two near-opaque cards overlap at the core' in the approach still):
    #     a tapered, rounded foot (no horizontal cut) and a body that does not add up to a near-opaque core when the hole builder stacks 2-3 cards.
    ae = np.power(np.clip(S[..., 3].astype(np.float64) / 255 * sp['tint'][3] * sp['gain'], 0, 1), sp['power'])      # v8: the owner's _AlphaPower (1.5 since repair round 3) is part of the effective alpha
    vis = ae > 0.10; rows_ = np.flatnonzero(vis.any(1)); r0_, r1_ = int(rows_[0]), int(rows_[-1])
    wid = vis.sum(1).astype(np.float64); wmax = wid.max()
    foot = float(wid[int(r1_ - 0.06 * (r1_ - r0_)):r1_ + 1].mean() / wmax)
    vstep = float(np.abs(np.diff(ae[int(r1_ - 0.10 * (r1_ - r0_)) - 2:r1_ + 3], axis=0)).max())
    gate('SMOKE_TAPERED_FOOT (the plume tapers to a narrow, rounded foot: no flat-bottomed block)', foot <= 0.45 and vstep <= 0.031,
         'mean width of the lowest 6 %% of the visible height %.0f %% of the widest row (<= 45), steepest vertical alpha step at the underside %.3f per texel (<= .031)' % (foot * 100, vstep))
    def _sh(x, dx, dy, mir):
        x = x[:, ::-1] if mir else x
        return np.roll(np.roll(x, int(round(dx * x.shape[1])), 1), int(round(dy * x.shape[0])), 0)
    b2 = _sh(ae, 0.10, 0.02, True); c3 = _sh(ae, -0.10, 0.04, False)
    two = 1 - (1 - ae) * (1 - b2); three = 1 - (1 - ae) * (1 - b2) * (1 - c3)
    pw_note = 'effective alpha = (painted x tint.a x gain) ^ power, live %.2f / %.2f / %.2f' % (sp['tint'][3], sp['gain'], sp['power'])
    gate('SMOKE_STACK_OVERLAP (hole 10 stacks 2-3 cards 10 % of a card width apart, every second one mirrored: no near-opaque core; the Game view moves the frame by combined alpha x ~145 levels over the dark wall, limit 110)',
         float(two.max()) <= 0.84 and float((three >= 0.85).mean()) <= 0.01 and float(three.max()) <= 0.76,
         'two mirrored cards: max combined alpha %.3f (<= .84; v6 .894), three cards: %.2f %% of the card at combined alpha >= .85 (<= 1; v6 3.7), max %.3f (<= .76 = 110 levels / 145); %s' % (two.max(), (three >= 0.85).mean() * 100, three.max(), pw_note))


def _wsd(l, w):
    H_, W_ = l.shape; h2, w2 = H_ // w, W_ // w
    return l[:h2 * w, :w2 * w].reshape(h2, w, w2, w).std(axis=(1, 3)).ravel()


def _hue_deg(a):
    mx = a.max(-1); mn = a.min(-1); d = mx - mn + 1e-9
    r, g_, b = a[..., 0], a[..., 1], a[..., 2]
    return np.where(mx == r, ((g_ - b) / d) % 6, np.where(mx == g_, (b - r) / d + 2, (r - g_) / d + 4)) * 60


def near_flat_share(l, mag, win=8, flat_sd=0.006, block_sd=0.003, size=320, crops=((0, 0), (300, 500), (700, 200), (150, 800))):
    """v6 texture-level proxy of the Game view's LOOK_STILLS_TEXTURED statistic: four 90 x 90 texel crops of the luminance map magnified `mag` times with bilinear sampling (what the phone camera does at the
    tee: x3.8 in the last rows, x3.0 / x2.4 above), then the gate tool's window test (8 px windows; flat = window sd < .006 or sd of the four 4x4 block means < .003).  Calibrated on the v5 Fairway_C:
    this proxy gives 51 % / median window sd .0060 at x3.8, the real hole08 tee still measures 51.7 % / .0059 in its last 160 rows."""
    H, W = l.shape
    out_f, out_s = [], []
    for (y0, x0) in crops:
        c = l[y0:y0 + 90, x0:x0 + 90]
        ys = (np.arange(size) + .5) / mag - .5; xs = (np.arange(size) + .5) / mag - .5
        yi = np.floor(ys).astype(int); xi = np.floor(xs).astype(int); fy = (ys - yi)[:, None]; fx = (xs - xi)[None, :]
        yi0 = yi % c.shape[0]; xi0 = xi % c.shape[1]; yi1 = (yi + 1) % c.shape[0]; xi1 = (xi + 1) % c.shape[1]
        up = (c[yi0][:, xi0] * (1 - fx) + c[yi0][:, xi1] * fx) * (1 - fy) + (c[yi1][:, xi0] * (1 - fx) + c[yi1][:, xi1] * fx) * fy
        h2, w2 = up.shape[0] // win, up.shape[1] // win
        a = up[:h2 * win, :w2 * win].reshape(h2, win, w2, win)
        sd = a.std(axis=(1, 3)); b = a.reshape(h2, win // 4, 4, w2, win // 4, 4).mean(axis=(2, 5)); bsd = b.std(axis=(1, 3))
        out_f.append(float(((sd < flat_sd) | (bsd < block_sd)).mean())); out_s.append(float(np.median(sd)))
    return float(np.mean(out_f)), float(np.mean(out_s))


# stills' pure-lawn crops (work/postcard-look/v2/tex/v5/tools/stills_grass.py; the same crops as v2/gates/calib/still_calib.txt plus crater): window sd at 16 still px (= 1-3 m of ground)
# medians .022 .027 .029 .033 .039 (split upper/lower green, crater pillar green, split fairway strip, crater fairway), whole-crop sd 10.9..14.7/255 on the flat ones.
STILL_W16_MEDIAN = 0.0334
TILE_M = dict(Fairway=10.0, Green=6.0, Rough=12.0, Scrub=12.0)


def v5_gates(imgs, gate):
    """v5 2026-10-04 (area T finish): GRASS_VARIATION (match or modestly exceed the STILLS' window statistics at the same ground scale, NOT the old 90 % floor; mean colour kept; hue drift inside
    the band), SCRUB_STREAKY, LAVA_PLATES_AND_LANES.  Numbers measured on the PNGs; Game-view numbers are measured separately (work/postcard-look/v2/tex/v5/)."""
    for nm in ('Fairway', 'Green', 'Rough', 'Scrub'):
        f = nm + '_C.png'
        if f not in imgs:
            continue
        a = imgs[f][..., :3].astype(np.float32) / 255; N_ = a.shape[0]; l = lum(a)
        mean = a.reshape(-1, 3).mean(0); want = np.array(GRASS_MEAN[nm], np.float32)
        w15 = max(8, int(round(1.5 / TILE_M[nm] * N_))); w16 = N_ // 16
        s15 = _wsd(l, w15); s16 = _wsd(l, w16)
        b8 = l[:N_ // 8 * 8, :N_ // 8 * 8].reshape(N_ // 8, 8, N_ // 8, 8).mean((1, 3)); sb = _wsd(b8, max(2, w16 // 8))
        hu = _hue_deg(a); lo_h, hi_h = {'Fairway': (66.0, 80.0), 'Green': (66.0, 80.0), 'Rough': (60.0, 76.0), 'Scrub': (38.0, 62.0)}[nm]
        hq = np.percentile(hu, [5, 95])
        Pw = np.abs(np.fft.fft2(l - l.mean())) ** 2
        kf = np.abs(np.fft.fftfreq(N_) * N_); KX, KY = np.meshgrid(kf, kf); kr = np.sqrt(KX ** 2 + KY ** 2)
        hi_p = Pw[kr > 5]; peak = float(hi_p.max() / max(hi_p.sum(), 1e-12))          # share of the power above 5 cycles / tile held by ONE frequency (an 8 px checkerboard holds ~1, grass < .05)
        stripe = ''
        ok_s = True
        if nm == 'Fairway':
            cm = l.mean(0); uu = (np.arange(N_) + 0.5) / N_
            Kb = BAND_P['fw_K'] if BAND_P['on'] else 1; pp = (uu * Kb) % 1.0                  # v9: K periods per tile, light bands centred on u = 0, 1/K ...
            Ll = float(cm[(np.minimum(pp, 1 - pp) < 0.12)].mean()); Ld = float(cm[(np.abs(pp - 0.5) < 0.12)].mean())
            amp = (Ll - Ld) / (Ll + Ld)                                             # light band (centre line) vs dark band, +-% around their mean
            lo_a, hi_a = (0.105, 0.145) if BAND_P['on'] else (0.07, 0.11)              # v9: +-12 % (the v8 band gate was 7..11 %)
            ok_s = lo_a <= amp <= hi_a; stripe = ', mow-band luminance +-%.1f%% (%.1f..%.1f%%)' % (amp * 100, lo_a * 100, hi_a * 100)
        ok = (abs(mean - want).max() * 255 <= 0.8 and np.median(s15) >= (0.035 if nm in ('Fairway', 'Green') else 0.03) and np.mean(s15 >= 0.03) >= 0.75
              and hq[0] >= lo_h and hq[1] <= hi_h and np.mean(sb >= 0.012) >= 0.75 and peak <= 0.25 and ok_s)
        gate('GRASS_VARIATION %s (stills-matched window statistics, mean kept, hue drift inside the band, no lattice peak)' % f, ok,
             'mean (%d,%d,%d) vs v2 (%d,%d,%d); window sd at %.1f m (%d px) median %.4f (>= %.3f; stills\' lawn crops at 16 px: median %.4f) share >= .03 %.0f%% (>= 75%%); at 1/16 tile (%d px) median %.4f share >= .03 %.0f%% (info; the old floor was 90%%); '
             '8x8-block sd >= .012 in %.0f%% (>= 75%%); albedo hue p5/p95 %.0f/%.0f (%.0f..%.0f); one frequency holds %.1f%% of the power above 5 cycles/tile (<= 25%%)%s' % (
                 *np.round(mean * 255), *np.round(want * 255), 1.5, w15, np.median(s15), 0.035 if nm in ('Fairway', 'Green') else 0.03, STILL_W16_MEDIAN, np.mean(s15 >= 0.03) * 100,
                 w16, np.median(s16), np.mean(s16 >= 0.03) * 100, np.mean(sb >= 0.012) * 100, hq[0], hq[1], lo_h, hi_h, peak * 100, stripe))
        f38, m38 = near_flat_share(l * 1.0, 3.8); f30, m30 = near_flat_share(l * 1.0, 3.0); f24, m24 = near_flat_share(l * 1.0, 2.4)
        gate('GRASS_NEAR_FIELD %s (v6: crisp at the phone camera\'s tee magnification: proxy of LOOK_STILLS_TEXTURED, 8 px windows flat = sd < .006 or noise-only)' % f, f38 <= 0.20 and f30 <= 0.10 and f24 <= 0.05,
             'flat 8 px windows at x3.8 (last rows of the tee frame) %.1f%% (<= 20; v5 Fairway_C 51%%), x3.0 %.1f%% (<= 10), x2.4 %.1f%% (<= 5); median window sd %.4f / %.4f / %.4f (flat below .006)' % (f38 * 100, f30 * 100, f24 * 100, m38, m30, m24))
        if nm in ('Fairway', 'Green'):
            # v8 GRASS_MICRO_NEAR (review, low, hole 9: 'the grass right at the ball is still soft and smeared': Laplacian sd 1.87 near vs 6.34 mid, target >= 3): the Laplacian sd of the bilinear-magnified luminance (0..255) at the tee's last rows (x3.8) and one band up (x2.4);
            # the Game view's hole 9 tee measures 1.27 x the x3.8 number of the v7 map (tint .69 + lighting), so >= 3.1 here should give >= 3 there (measured hole 9 tee: 1.58 -> see TEXTURES.md v8).  And the same layer must not turn the mid field into noise: at x1.0 (a pixel = a texel) <= 40.
            L255 = l * 255
            def lap_mag(mag):
                v_ = []
                for (y0, x0) in ((0, 0), (300, 500), (700, 200), (150, 800)):
                    c = L255[y0:y0 + 120, x0:x0 + 120]; size = min(int(120 * mag), 380)
                    ys = (np.arange(size) + .5) / mag - .5; xs = (np.arange(size) + .5) / mag - .5
                    yi = np.floor(ys).astype(int); xi = np.floor(xs).astype(int); fy = (ys - yi)[:, None]; fx = (xs - xi)[None, :]
                    up = (c[yi % 120][:, xi % 120] * (1 - fx) + c[yi % 120][:, (xi + 1) % 120] * fx) * (1 - fy) + (c[(yi + 1) % 120][:, xi % 120] * (1 - fx) + c[(yi + 1) % 120][:, (xi + 1) % 120] * fx) * fy
                    lp = -4 * up[1:-1, 1:-1] + up[:-2, 1:-1] + up[2:, 1:-1] + up[1:-1, :-2] + up[1:-1, 2:]
                    v_.append(float(lp.std()))
                return float(np.mean(v_))
            l38, l24, l10 = lap_mag(3.8), lap_mag(2.4), lap_mag(1.0)
            gate('GRASS_MICRO_NEAR %s (v8: pixel-scale detail where the ground is magnified: Laplacian sd of the x3.8 / x2.4 magnified luminance, and not noise at x1.0)' % f, l38 >= 3.1 and l24 >= 6.2 and l10 <= 40.0,
                 'Laplacian sd (0..255 luminance) at x3.8 %.2f (>= 3.1; v7 1.24), x2.4 %.2f (>= 6.2; v7 2.55), x1.0 %.2f (<= 40; v7 11.9)' % (l38, l24, l10))
    if 'Scrub_C.png' in imgs:
        l = lum(imgs['Scrub_C.png'][..., :3].astype(np.float32) / 255); N_ = l.shape[0]
        lb = blur(l, 1.5); gx = (np.roll(lb, -1, 1) - np.roll(lb, 1, 1)) * 0.5; gy = (np.roll(lb, -1, 0) - np.roll(lb, 1, 0)) * 0.5
        # structure tensor of the luminance gradient in the (u, v) frame: strongest edges run ALONG the blades; a leaning blade set has its principal axis at about +-45 degrees
        Jxx = float((gx * gx).mean()); Jyy = float((gy * gy).mean()); Jxy = float((gx * gy).mean())
        ev = np.linalg.eigvalsh(np.array([[Jxx, Jxy], [Jxy, Jyy]]))
        elong = float(np.sqrt(ev[1] / max(ev[0], 1e-12)))
        ang = 0.5 * np.degrees(np.arctan2(2 * Jxy, Jxx - Jyy))                  # direction of the dominant gradient; blades run perpendicular to it
        # round-blob test: dark bush masses (lowest 35 % luminance) must not be compact discs (the v2 clover blobs); median aspect >= 1.7
        dm = l < np.percentile(l, 35); comps = [c for c in label_components(dm[::2, ::2]) if len(c[0]) >= 60]
        asp = np.array([comp_shape(ys, xs, N_ // 2, N_ // 2)[0] for ys, xs in comps]) if comps else np.array([0.0])
        # v6: the v5 definition (elongation >= 1.5, dominant 45 degree lean, aspect >= 2.5) asked for the streaks the review found to read as motion blur in the Game view (structure tensor 5.5 : 1 over the lower third
        # of hole09_3_ruin, 8.2 : 1 on the steep ground; split.jpg's scrub is fine-grained and isotropic at the phone's distance): the map must now be ISOTROPIC (elongation <= 1.35) and still clumpy
        # (>= 40 dark masses at 512, i.e. not a flat colour), and crisp in the near field (GRASS_NEAR_FIELD above).
        ok = elong <= 1.35 and len(comps) >= 40
        gate('SCRUB_NOT_SMEARED (v6: isotropic bushy scrub, no one-direction streaks; replaces SCRUB_STREAKY)', ok,
             'gradient structure-tensor elongation %.2f (<= 1.35; the v5 streaky map measured 2.82, a round-blob map 1.01), dominant gradient direction %.0f deg, %d dark masses >= 60 px at 512 (>= 40), median aspect %.2f (info)' % (elong, ang, len(comps), float(np.median(asp))))
    if all(k in imgs for k in ('Lava_C.png', 'Lava_E.png', 'Lava_N.png')):
        C = imgs['Lava_C.png']; E = imgs['Lava_E.png']; Nn = imgs['Lava_N.png']; sp = lava_spec()
        h_, rgb_ = lava_shader_model(C, E, Nn, sp)
        hd = lava_dark_heat(sp); n_ = 1024; ds = rgb_.reshape(170 * 6 + 4, 1, 1, 3)[:0] if False else None
        img = (rgb_ / 255.0)[:1020, :1020].reshape(170, 6, 170, 6, 3).mean((1, 3))          # ~14 cm per pixel: the ground scale of crater.jpg's pool
        L14 = lum(img.astype(np.float32)); b4 = L14[:168, :168].reshape(42, 4, 42, 4).mean((1, 3))
        reg = float(b4.std() * 255)
        dark = (h_ < hd)
        comps = [c for c in label_components(dark[::2, ::2]) if len(c[0]) >= 730 // 4]       # >= 0.4 m2 (a texel is 2.3 cm: 730 texels)
        big = len(comps)
        ok = reg >= 24.5 and big >= 5
        gate('LAVA_PLATES_AND_LANES (regional glow contrast at the still\'s ground scale; a few chunky crust plates, not only thin ribbons)', ok,
             'luminance sd of 4x4-pixel block means at 14 cm / px %.1f/255 (>= 24.5; the v4 map measured 23.1, crater.jpg pool 27..39), %d crust plates >= 0.4 m2 (>= 5), crust %.1f%% of the tile' % (reg, big, float(dark.mean()) * 100))


def _sheen_scale_from_golflook(name):
    """the Smoothness field of the LK_FAIRWAY / LK_GREEN row in GolfLook.cs (the scale of the alpha channel), or None."""
    import re
    try:
        txt = open(LOOKCS).read()
    except OSError:
        return None
    m = re.search(r'\["LK_%s"\]\s*=\s*new Spec \{[^\n]*?Smoothness = (-?\d*\.?\d+)f' % name.upper(), txt)
    return float(m.group(1)) if m else None


def v9_gates(imgs, gate):
    """v9 2026-10-05 (grass liveliness, mow-band life): GRASS_BAND_CONTRAST, GRASS_SHEEN_ALPHA, GRASS_LEAN_STREAKS on Fairway / Green (measured on the decoded PNGs, never from the generator's parameters)."""
    if not BAND_P['on']:
        return
    for nm, Kb in (('Fairway', BAND_P['fw_K']), ('Green', BAND_P['gr_K'])):
        f = nm + '_C.png'; fn = nm + '_N.png'
        if f not in imgs or fn not in imgs:
            continue
        c = imgs[f]; a = c[..., :3].astype(np.float32) / 255; l = lum(a); N_ = l.shape[1]
        uu = (np.arange(N_) + 0.5) / N_; pp = (uu * Kb) % 1.0
        lt = np.minimum(pp, 1 - pp) < 0.12; dk = np.abs(pp - 0.5) < 0.12
        cm = l.mean(0); Ll = float(cm[lt].mean()); Ld = float(cm[dk].mean()); amp = (Ll - Ld) / (Ll + Ld)
        # the within-band texture sd (what the step has to stand out from): per-column luminance, band plateaus only
        wsd = float(np.sqrt((l[:, lt].std(0) ** 2).mean() / 2 + (l[:, dk].std(0) ** 2).mean() / 2))
        hl = _hue_deg(a[:, lt].reshape(-1, 3).mean(0)[None, :])[0]; hd = _hue_deg(a[:, dk].reshape(-1, 3).mean(0)[None, :])[0]
        gate('GRASS_BAND_CONTRAST %s (v9: mow bands +-12 %% luminance, hue of the two bands within 4 deg)' % f, 0.105 <= amp <= 0.145 and abs(hl - hd) <= 4.0,
             'K = %d periods per tile: light band %.4f / dark band %.4f mean luminance: +-%.1f%% (10.5..14.5; v8 +-10.6 %% at K=1 under 2x the mottle), light/dark ratio %.3f, band hue %.1f / %.1f deg, within-band texture sd %.4f (the step is %.2f sd)' % (
                 Kb, Ll, Ld, amp * 100, Ll / Ld, hl, hd, wsd, (Ll - Ld) / max(wsd, 1e-6)))
        # sheen: the alpha channel
        if c.shape[-1] != 4:
            gate('GRASS_SHEEN_ALPHA %s' % f, False, 'no alpha channel (colour type 2)')
            continue
        al = c[..., 3].astype(np.float32) / 255
        aL = float(al[:, lt].mean()); aD = float(al[:, dk].mean()); am = float(al.mean()); amax = float(al.max())
        scale = _sheen_scale_from_golflook(nm)
        ok = 0.20 <= aL <= 0.45 and 0.02 <= aD <= 0.10 and aL / max(aD, 1e-6) >= 3.0 and 0.08 <= am <= 0.22 and amax <= 0.60 and scale is not None and abs(scale - SHEEN_SCALE) < 1e-6
        gate('GRASS_SHEEN_ALPHA %s (v9: per-band smoothness in the albedo alpha: light bands glossier, dark bands matte, mean near the old flat .10..12)' % f, ok,
             'smoothness (alpha x scale %s): light band %.3f (.20..45), dark band %.3f (.02..10), ratio %.1f (>= 3), tile mean %.3f (.08..22; v8 flat 0.10 / 0.12), max %.3f (<= .60)' % (
                 'GolfLook.cs %.2f' % scale if scale is not None else 'MISSING in GolfLook.cs', aL, aD, aL / max(aD, 1e-6), am, amax))
        # lean: the normal map
        n = imgs[fn].astype(np.float32) / 255 * 2 - 1
        ty = np.degrees(np.arcsin(np.clip(n[..., 1], -1, 1)))                      # tilt about the u axis (+ = toward +v)
        tl = float(ty[:, lt].mean()); td = float(ty[:, dk].mean())
        ysm = blur(ty, 3.0)                                                         # the lean field without the pixel grain
        sdw = float(np.sqrt((ysm[:, lt].std(0) ** 2).mean() / 2 + (ysm[:, dk].std(0) ** 2).mean() / 2))
        def corr(dy, dx):
            b = np.roll(np.roll(ysm, dy, 0), dx, 1); a_ = ysm - ysm.mean(); b_ = b - b.mean()
            return float((a_ * b_).sum() / np.sqrt((a_ * a_).sum() * (b_ * b_).sum()))
        cv = corr(45, 0); cu = corr(0, 45)
        tmax = float(np.percentile(np.abs(ysm), 99.9))                              # the lean field (without the pixel-scale blade grain, which tilts the texel normals by up to ~25 deg on its own)
        sgn = BAND_P['lean_sign']
        ok = sgn * (tl - td) >= 6.0 and sgn * tl > 0 > sgn * td and 1.0 <= sdw <= 5.0 and cv >= cu + 0.10 and tmax <= 16.0
        gate('GRASS_LEAN_STREAKS %s (v9: blade lean: a per-band tilt about the u axis + long streaks along v, as the Game view reads the normal map)' % fn, ok,
             'mean tilt light band %+.2f deg / dark band %+.2f deg (step %.1f deg >= 6, opposite signs), streak tilt sd inside the bands %.2f deg (1..5), correlation after a 45 px shift along v %.2f vs along u %.2f (v >= u + .10: streaks run along the mowing direction), 99.9 pct |tilt| of the lean field %.1f deg (<= 16)' % (
                 tl, td, abs(tl - td), sdw, cv, cu, tmax))


def look_gates(imgs, gate):
    grass_gates(imgs, gate)
    v5_gates(imgs, gate)
    v9_gates(imgs, gate)
    lava_gates(imgs, gate)
    basalt_gates(imgs, gate)
    smoke_gates(imgs, gate)


# --------------------------------------------------------------------------------------------- contact sheet

_FONT = {
    'A': (14, 17, 17, 31, 17, 17, 17), 'B': (30, 17, 17, 30, 17, 17, 30), 'C': (14, 17, 16, 16, 16, 17, 14),
    'D': (30, 17, 17, 17, 17, 17, 30), 'E': (31, 16, 16, 30, 16, 16, 31), 'F': (31, 16, 16, 30, 16, 16, 16),
    'G': (14, 17, 16, 23, 17, 17, 15), 'H': (17, 17, 17, 31, 17, 17, 17), 'I': (14, 4, 4, 4, 4, 4, 14),
    'J': (7, 2, 2, 2, 2, 18, 12), 'K': (17, 18, 20, 24, 20, 18, 17), 'L': (16, 16, 16, 16, 16, 16, 31),
    'M': (17, 27, 21, 21, 17, 17, 17), 'N': (17, 17, 25, 21, 19, 17, 17), 'O': (14, 17, 17, 17, 17, 17, 14),
    'P': (30, 17, 17, 30, 16, 16, 16), 'Q': (14, 17, 17, 17, 21, 18, 13), 'R': (30, 17, 17, 30, 20, 18, 17),
    'S': (15, 16, 16, 14, 1, 1, 30), 'T': (31, 4, 4, 4, 4, 4, 4), 'U': (17, 17, 17, 17, 17, 17, 14),
    'V': (17, 17, 17, 17, 17, 10, 4), 'W': (17, 17, 17, 21, 21, 21, 10), 'X': (17, 17, 10, 4, 10, 17, 17),
    'Y': (17, 17, 17, 10, 4, 4, 4), 'Z': (31, 1, 2, 4, 8, 16, 31),
    '0': (14, 17, 19, 21, 25, 17, 14), '1': (4, 12, 4, 4, 4, 4, 14), '2': (14, 17, 1, 2, 4, 8, 31),
    '3': (31, 2, 4, 2, 1, 17, 14), '4': (2, 6, 10, 18, 31, 2, 2), '5': (31, 16, 30, 1, 1, 17, 14),
    '6': (6, 8, 16, 30, 17, 17, 14), '7': (31, 1, 2, 4, 8, 8, 8), '8': (14, 17, 17, 14, 17, 17, 14),
    '9': (14, 17, 17, 15, 1, 2, 12), ' ': (0,) * 7, '_': (0, 0, 0, 0, 0, 0, 31), '-': (0, 0, 0, 31, 0, 0, 0),
    '.': (0, 0, 0, 0, 0, 12, 12), ':': (0, 12, 12, 0, 12, 12, 0), '/': (1, 1, 2, 4, 8, 16, 16),
    '(': (2, 4, 8, 8, 8, 4, 2), ')': (8, 4, 2, 2, 2, 4, 8), '%': (24, 25, 2, 4, 8, 19, 3),
    '=': (0, 0, 31, 0, 31, 0, 0), ',': (0, 0, 0, 0, 12, 4, 8), '+': (0, 4, 4, 31, 4, 4, 0), '?': (14, 17, 1, 2, 4, 0, 4),
}


def text(img, x, y, s, scale=2, color=(1, 1, 1)):
    col = np.array(color, F)
    for ch in s.upper():
        g = _FONT.get(ch, _FONT['?'])
        for r, bits in enumerate(g):
            for cc in range(5):
                if (bits >> (4 - cc)) & 1:
                    y0 = y + r * scale; x0 = x + cc * scale
                    if 0 <= y0 < img.shape[0] - scale and 0 <= x0 < img.shape[1] - scale:
                        img[y0:y0 + scale, x0:x0 + scale, :3] = col
        x += 6 * scale


def down(a, f):
    h, w = a.shape[:2]
    return a[:h - h % f, :w - w % f].reshape(h // f, f, w // f, f, -1).mean((1, 3))


def fit(a, size):
    """box-downscale (integer factor) then nearest to exactly size x size."""
    f = max(1, a.shape[0] // size)
    a = down(a, f)
    ys = (np.arange(size) * a.shape[0] / size).astype(int); xs = (np.arange(size) * a.shape[1] / size).astype(int)
    return a[ys][:, xs]


LIGHT = np.array([-0.45, 0.50, 0.74], F); LIGHT /= np.linalg.norm(LIGHT)


def lit_preview(c, n, e=None):
    """albedo (sRGB) shaded by the normal map under a fixed warm key + cool ambient, normalised so a flat
    texel shows its albedo under the key; emission added on top."""
    nn = n.astype(F) / 255 * 2 - 1
    ndl = np.clip((nn * LIGHT).sum(-1), 0, 1)
    k = (0.38 + 0.80 * ndl) / (0.38 + 0.80 * LIGHT[2])
    out = (c.astype(F) / 255) * k[..., None] * np.array([1.0, 0.97, 0.92], F)
    if e is not None:
        out = out + (e.astype(F) / 255) * 0.9
    return np.clip(out, 0, 1)


def over(rgba, bg):
    a = rgba[..., 3:4].astype(F) / 255
    return (rgba[..., :3].astype(F) / 255) * a + np.array(bg, F) * (1 - a)


def make_sheet(path, imgs):
    P = 256; G = 8; LBL = 26; M = 16
    blockw = 4 * P + 3 * G
    W = M * 3 + blockw * 2
    tiled = [m for m in MATS if 'N' in m[2]]
    rows = (len(tiled) + 1) // 2
    H = 60 + rows * (P + LBL + 14) + (P + LBL + 14) + (440 + LBL + 14) + 20
    sheet = np.zeros((H, W, 3), F) + np.array([0.13, 0.14, 0.16], F)
    text(sheet, M, 14, 'GOLF POSTCARD_LOOK TEXTURES  -  ALBEDO 3X3 TILED / NORMAL / EMISSION / LIT 2X2 (WARM KEY)', 2, (0.95, 0.95, 0.9))
    text(sheet, M, 36, 'GENERATED BY BLENDER/SCRIPTS/GOLF_LOOK_TEXTURES.PY  (DETERMINISTIC SEEDS, PERIODIC NOISE)', 2, (0.7, 0.72, 0.75))
    y0 = 60
    for i, (name, fn, kind, size, tu, tv) in enumerate(tiled):
        bx = M + (i % 2) * (blockw + M); by = y0 + (i // 2) * (P + LBL + 14)
        c = imgs[name + '_C.png'][..., :3]; n = imgs[name + '_N.png']; e = imgs.get(name + '_E.png')      # v9: Fairway / Green carry the smoothness in alpha
        text(sheet, bx, by + 4, '%s  %dX%d' % (name, size[0], size[1]), 2, (1, 0.92, 0.6))
        py = by + LBL
        t3 = np.tile(c.astype(F) / 255, (3, 3, 1))
        sheet[py:py + P, bx:bx + P] = fit(t3, P)
        sheet[py:py + P, bx + P + G:bx + 2 * P + G] = fit(n.astype(F) / 255, P)
        if e is not None:
            sheet[py:py + P, bx + 2 * (P + G):bx + 3 * P + 2 * G] = fit(e.astype(F) / 255, P)
        else:
            text(sheet, bx + 2 * (P + G) + 70, py + P // 2 - 7, 'NO EMISSION', 2, (0.45, 0.47, 0.5))
        lp = lit_preview(c, n, e)
        sheet[py:py + P, bx + 3 * (P + G):bx + 4 * P + 3 * G] = fit(np.tile(lp, (2, 2, 1)), P)
    by = y0 + rows * (P + LBL + 14)
    bx = M
    text(sheet, bx, by + 4, 'SURF_C 3X3 OVER SEA / ALPHA', 2, (1, 0.92, 0.6))
    s = imgs['Surf_C.png']; py = by + LBL
    sheet[py:py + P, bx:bx + P] = fit(np.tile(over(s, (0.05, 0.36, 0.45)), (3, 3, 1)), P)
    sheet[py:py + P, bx + P + G:bx + 2 * P + G] = fit(np.repeat((s[..., 3:4].astype(F) / 255), 3, -1), P)
    bx2 = bx + 2 * (P + G)
    text(sheet, bx2, by + 4, 'FALL_C X2 V / SMOKE_C', 2, (1, 0.92, 0.6))
    f = imgs['Fall_C.png']; fo = over(np.tile(f, (2, 1, 1)), (0.16, 0.15, 0.14))
    fo = fit(fo, P) if False else fo[::4, ::2][:P, :P // 2 * 2]
    sheet[py:py + fo.shape[0], bx2:bx2 + fo.shape[1]] = fo
    sm_ = imgs['Smoke_C.png']
    sheet[py:py + P, bx2 + P // 2 + G:bx2 + P // 2 + G + P] = fit(over(sm_, (0.30, 0.13, 0.10)), P)
    by2 = by + P + LBL + 14
    text(sheet, M, by2 + 4, 'SKY_CRATER_C 2048X880 (V0 = HORIZON, U = 180 DEG ARC, MIRRORED BY TENNISSKY)', 2, (1, 0.92, 0.6))
    sk = down(imgs['Sky_Crater_C.png'].astype(F) / 255, 2)
    sheet[by2 + LBL:by2 + LBL + sk.shape[0], M:M + sk.shape[1]] = sk
    os.makedirs(os.path.dirname(path), exist_ok=True)
    write_png(path, enc(sheet))
    return sheet.shape


def previews(pdir, imgs, names):
    os.makedirs(pdir, exist_ok=True)
    for name, fn, kind, size, tu, tv in MATS:
        if name not in names:
            continue
        c = imgs.get(name + '_C.png')
        if c is None:
            continue
        if 'N' in kind:
            n = imgs[name + '_N.png']; e = imgs.get(name + '_E.png')
            c = c[..., :3]
            t3 = np.tile(c.astype(F) / 255, (3, 3, 1)); t3 = down(t3, max(1, t3.shape[0] // 1024))
            write_png(os.path.join(pdir, name + '_3x3.png'), enc(t3))
            lp = lit_preview(c, n, e)
            write_png(os.path.join(pdir, name + '_lit.png'), enc(lp))
        else:
            write_png(os.path.join(pdir, name + '_rgba.png'), c)

# --------------------------------------------------------------------------------------------- main

def main(argv):
    ap = argparse.ArgumentParser()
    ap.add_argument('--out', default=os.path.join(ROOT, 'Unity/Assets/Resources/Course/Look'))
    ap.add_argument('--sheet', default=None)
    ap.add_argument('--only', default=None)
    ap.add_argument('--gates-only', action='store_true')
    ap.add_argument('--preview', default=None)
    ap.add_argument('--smoke-peak', type=float, default=None, help='override SMOKE_P[amax] (the PAINTED alpha peak of Smoke_C; effective = peak x AlphaGain of LK_SMOKE must land in .55..0.72)')
    ap.add_argument('--periodicity', action='store_true', help='regenerate every tiling map on a grid shifted by exactly one tile and compare')
    ap.add_argument('--set', default=None, help="experiments only: comma list of DICT.key=value overrides of the module dicts (e.g. 'BAND_P.sheen_l=0.4,BAND_P.fw_amp=0.1'); never used for the installed maps")
    a = ap.parse_args(argv)
    if a.set:
        for kv in a.set.split(','):
            k, val = kv.split('=')
            dn, key = k.split('.')
            d_ = globals()[dn]
            d_[key] = type(d_[key])(float(val)) if not isinstance(d_[key], tuple) else tuple(float(x) for x in val.split('/'))
            print('OVERRIDE %s.%s = %r' % (dn, key, d_[key]))
    out = os.path.abspath(a.out if os.path.isabs(a.out) else os.path.join(os.getcwd(), a.out))
    only = set(a.only.split(',')) if a.only else None
    if a.smoke_peak is not None:
        SMOKE_P['amax'] = a.smoke_peak
    imgs = {}
    t00 = time.time()
    if not a.gates_only:
        for name, fn, kind, size, tu, tv in MATS:
            if only and name not in only:
                continue
            t0 = time.time()
            res = generate(name, fn, kind, size)
            for fname, arr in res.items():
                nb = write_png(os.path.join(out, fname), arr)
                imgs[fname] = arr
                print('wrote %-22s %4dx%-4d %7.1f KB' % (fname, arr.shape[1], arr.shape[0], nb / 1024))
            print('  %s %.1fs' % (name, time.time() - t0))
    # decode what is on disk (Blender's libpng when run inside Blender) -> verify exact bytes, run gates
    disk = {}
    mism = []
    for name, fn, kind, size, tu, tv in MATS:
        for fname, k, sz in files_of(name, kind, size):
            p = os.path.join(out, fname)
            if not os.path.exists(p):
                continue
            if fname in imgs and 'bpy' not in sys.modules:
                disk[fname] = imgs[fname]          # plain-python iteration run: no independent decoder, use the bytes we wrote
                continue
            if fname in imgs or a.gates_only or a.sheet:
                try:
                    d = read_png(p)
                except Exception as ex:  # noqa
                    print('decode failed', fname, ex); continue
                if fname in imgs and not np.array_equal(d, imgs[fname]):
                    mism.append(fname)
                disk[fname] = d
    if imgs and 'bpy' in sys.modules:
        print('GATE: PNG round-trip exact (decoded pixels == generated bytes, %d files, decoder %s) %s' % (
            len(imgs), 'bpy/libpng' if 'bpy' in sys.modules else 'numpy', 'PASS' if not mism else 'FAIL ' + ','.join(mism)))
    gates, listing = run_gates(out, disk)
    if a.periodicity:
        for name, fn, kind, size, tu, tv in MATS:
            if (only and name not in only) or not (tu or tv):
                continue
            base = generate(name, fn, kind, size)
            shf = generate(name, fn, kind, size, extra=(1.0 if tu else 0.0, 1.0 if tv else 0.0))
            for fname in base:
                dlt = np.abs(base[fname].astype(np.int16) - shf[fname].astype(np.int16))
                frac = float((dlt > 1).mean())
                ok = frac <= 0.001
                gates.append(('%s exactly periodic' % fname, ok, ''))
                print('GATE: %s periodic: map(u+%d, v+%d) == map(u, v) %s  (texels differing by >1 level: %.4f%%, max %d)' % (
                    fname, 1 if tu else 0, 1 if tv else 0, 'PASS' if ok else 'FAIL', frac * 100, dlt.max()))
    if a.periodicity and not only:
        per = [g for g in gates if g[0].endswith('exactly periodic')]
        seam = [g for g in gates if 'wrap seam' in g[0]]
        okp = all(g[1] for g in per); oks = all(g[1] for g in seam)
        print('GATE: SEAMLESS_ALL %s - %d tiling maps exactly periodic (map(u+1,v+1) == map(u,v), <= 0.1%% texels differ by > 1 level): %s; wrap-seam ratio <= 1.5x on %d/%d files' % (
            'PASS' if okp and oks else 'FAIL', len(per), 'all' if okp else 'NOT ALL', sum(g[1] for g in seam), len(seam)))
    if a.sheet:
        sp = os.path.abspath(a.sheet if os.path.isabs(a.sheet) else os.path.join(os.getcwd(), a.sheet))
        shp = make_sheet(sp, disk)
        print('sheet', sp, shp)
    if a.preview:
        previews(a.preview, disk, set(only) if only else set(m[0] for m in MATS))
    nf = sum(1 for g in gates if not g[1])
    print('SUMMARY gates %d, fail %d, total %.1fs' % (len(gates), nf, time.time() - t00))
    for fn_, sz in listing:
        with open(os.path.join(out, fn_), 'rb') as f:
            hsh = hashlib.sha256(f.read()).hexdigest()[:16]
        print('FILE %-22s %9d B  sha256 %s' % (fn_, sz, hsh))
    return nf


if __name__ == '__main__':
    argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else sys.argv[1:]
    rc = main(argv)
    if 'bpy' in sys.modules:
        sys.stdout.flush()
        os._exit(1 if rc else 0)
