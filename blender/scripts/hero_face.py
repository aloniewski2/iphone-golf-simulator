"""The Hero's face, painted: the big dark eyes with their highlights and lash lines, the arched brows, the smile
and the rosy cheeks, as one RGBA decal that is laid on the front of the head (the game projects it from the front;
Blender's preview does the same). The head itself stays plain and smooth.

    python3 blender/scripts/hero_face.py <out.png>

The decal covers x in [-X, X] and z in [Z0, Z1] of the head (metres, the face looking down -Y); u = (x + X) / 2X,
v = (z - Z0) / (Z1 - Z0) (v up). The same numbers are in HeroKit.shader and hero_avatar.py.
"""
import sys
from PIL import Image, ImageDraw, ImageFilter

X, Z0, Z1 = 0.13, 1.29, 1.55
SIZE = 1024
SS = 2                      # supersampling

EYE_X, EYE_Z = 0.078, 1.425
EYE_RX, EYE_RZ = 0.030, 0.038
BROW_Z = 1.486
MOUTH_Z = 1.334
CHEEK = (0.093, 1.372)

EYE = (38, 20, 14)
EYE_LOW = (112, 62, 38)
LASH = (30, 15, 10)
BROW = (142, 82, 26)
MOUTH = (96, 44, 30)
BLUSH = (246, 104, 92)


def px(x, z, s=SIZE * SS):
    return ((x + X) / (2 * X) * s, (1 - (z - Z0) / (Z1 - Z0)) * s)


def length(m, s=SIZE * SS):
    return m / (2 * X) * s


def ellipse(d, c, rx, rz, fill):
    cx, cy = px(*c); rx, rz = length(rx), length(rz)
    d.ellipse((cx - rx, cy - rz, cx + rx, cy + rz), fill=fill)


def _catmull(pts, per=14):
    p = [pts[0]] + list(pts) + [pts[-1]]
    out = []
    for i in range(1, len(p) - 2):
        p0, p1, p2, p3 = p[i - 1], p[i], p[i + 1], p[i + 2]
        for k in range(per):
            t = k / per; t2 = t * t; t3 = t2 * t
            out.append(tuple(0.5 * ((2 * p1[j]) + (-p0[j] + p2[j]) * t + (2 * p0[j] - 5 * p1[j] + 4 * p2[j] - p3[j]) * t2 + (-p0[j] + 3 * p1[j] - 3 * p2[j] + p3[j]) * t3) for j in range(2)))
    out.append(tuple(pts[-1]))
    return out


def curve(d, pts, widths, fill, per=14):
    """A smooth round-ended stroke along pts (x, z) whose width (metres) eases along `widths` (one per point or per
    segment)."""
    path = _catmull(pts, per)
    n = len(path)
    w = widths if len(widths) == len(pts) else [widths[min(len(widths) - 1, int(i * len(widths) / max(1, len(pts) - 1)))] for i in range(len(pts))]
    def width_at(i):
        t = i / max(1, n - 1) * (len(w) - 1); k = min(len(w) - 2, int(t)) if len(w) > 1 else 0
        return w[k] + (w[min(k + 1, len(w) - 1)] - w[k]) * (t - k) if len(w) > 1 else w[0]
    for i, pt in enumerate(path):
        cx, cy = px(*pt); r = length(width_at(i)) / 2
        d.ellipse((cx - r, cy - r, cx + r, cy + r), fill=fill)


def paint(path):
    S = SIZE * SS
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    # cheeks first (soft)
    blush = Image.new("RGBA", (S, S), (0, 0, 0, 0)); bd = ImageDraw.Draw(blush)
    for side in (1, -1):
        ellipse(bd, (side * CHEEK[0], CHEEK[1]), 0.030, 0.021, BLUSH + (105,))
    blush = blush.filter(ImageFilter.GaussianBlur(S * 0.018))
    img.alpha_composite(blush)
    d = ImageDraw.Draw(img)
    for side in (1, -1):
        ex = side * EYE_X
        # the eye: dark, with a lighter lower rim, two highlights
        ellipse(d, (ex, EYE_Z), EYE_RX, EYE_RZ, EYE + (255,))
        low = Image.new("RGBA", (S, S), (0, 0, 0, 0)); ld = ImageDraw.Draw(low)
        ellipse(ld, (ex, EYE_Z - 0.020), EYE_RX * 0.78, EYE_RZ * 0.42, EYE_LOW + (200,))
        low = low.filter(ImageFilter.GaussianBlur(S * 0.006))
        mask = Image.new("L", (S, S), 0); md = ImageDraw.Draw(mask)
        cx, cy = px(ex, EYE_Z); md.ellipse((cx - length(EYE_RX), cy - length(EYE_RZ), cx + length(EYE_RX), cy + length(EYE_RZ)), fill=255)
        low.putalpha(Image.composite(low.getchannel("A"), Image.new("L", (S, S), 0), mask))
        img.alpha_composite(low)
        d = ImageDraw.Draw(img)
        ellipse(d, (ex - side * 0.008, EYE_Z + 0.014), 0.0115, 0.0115, (255, 255, 255, 255))
        ellipse(d, (ex + side * 0.012, EYE_Z - 0.013), 0.0055, 0.0055, (255, 255, 255, 235))
        # the upper lash line, thick, with a little flick at the outer corner
        lash = [(ex - side * 0.029, EYE_Z + 0.014), (ex - side * 0.022, EYE_Z + 0.029), (ex - side * 0.008, EYE_Z + 0.038),
                (ex + side * 0.010, EYE_Z + 0.038), (ex + side * 0.024, EYE_Z + 0.028), (ex + side * 0.034, EYE_Z + 0.016)]
        lash = lash + [(ex + side * 0.041, EYE_Z + 0.020)]
        curve(d, lash, [0.0080, 0.0098, 0.0106, 0.0104, 0.0090, 0.0070, 0.0030], LASH + (255,))
        # the brow: an arch, thicker at the inner end
        bz = BROW_Z
        brow = [(ex - side * 0.028, bz - 0.002), (ex - side * 0.014, bz + 0.006), (ex + side * 0.004, bz + 0.0085),
                (ex + side * 0.020, bz + 0.004), (ex + side * 0.031, bz - 0.006)]
        curve(d, brow, [0.0082, 0.0088, 0.0080, 0.0062, 0.0030], BROW + (255,))
    # the smile: a thin curve, a touch of a dimple at each end
    m = [(-0.020, MOUTH_Z + 0.004), (-0.012, MOUTH_Z - 0.002), (0.0, MOUTH_Z - 0.0045), (0.012, MOUTH_Z - 0.002), (0.020, MOUTH_Z + 0.004)]
    curve(d, m, [0.0030, 0.0040, 0.0044, 0.0040, 0.0030], MOUTH + (255,))
    curve(d, [(-0.0225, MOUTH_Z + 0.0055), (-0.0205, MOUTH_Z + 0.008)], [0.0028], MOUTH + (255,))
    curve(d, [(0.0225, MOUTH_Z + 0.0055), (0.0205, MOUTH_Z + 0.008)], [0.0028], MOUTH + (255,))
    img = img.resize((SIZE, SIZE), Image.LANCZOS)
    img.save(path)
    return path


if __name__ == "__main__":
    print(paint(sys.argv[1]))
