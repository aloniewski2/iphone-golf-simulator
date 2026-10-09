"""Draw the base avatar's face expression atlas: simple Mii-style features, drawn as vector
shapes rather than painted parts, so they stay crisp at any size and can be recoloured or
swapped for customisation (eye colour, brow colour, mouth style).

Same layout and cell size as build_face_atlas.py, so the face decal and TennisActor's
expression cells work unchanged:

    0 neutral   1 blink   2 happy     3 focus
    4 effort    5 sad     6 surprised 7 cheer

Run:  python3 blender/scripts/build_avatar_face_atlas.py [--name AvatarF] [--lashes] [--eye R,G,B] [--brow R,G,B]
"""
import math
import sys
from pathlib import Path

from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[2]
NAME = sys.argv[sys.argv.index("--name") + 1] if "--name" in sys.argv else "Avatar"
LASHES = "--lashes" in sys.argv
OUT_UNITY = ROOT / f"Unity/Assets/Resources/Tennis/Characters/FaceAtlas_{NAME}.png"
OUT_LIBRARY = ROOT / f"SportsLibrary/ArtDirection/Tennis/Generated/FaceAtlas_{NAME}.png"
CELL_W, CELL_H = 512, 440
SS = 4                                   # supersampling for smooth edges
EXPRESSIONS = ["neutral", "blink", "happy", "focus", "effort", "sad", "surprised", "cheer"]

EYE = (24, 20, 22, 255)
BROW = (58, 38, 28, 255)
MOUTH = (70, 28, 30, 255)
TONGUE = (226, 110, 118, 255)
TEETH = (250, 248, 244, 255)
BLUSH = (240, 120, 120, 70)


def arg_colour(flag, default):
    if flag in sys.argv:
        r, g, b = (int(c) for c in sys.argv[sys.argv.index(flag) + 1].split(","))
        return (r, g, b, 255)
    return default


class Cell:
    """Drawing in cell fractions (x right, y up), supersampled."""
    def __init__(self):
        self.im = Image.new("RGBA", (CELL_W * SS, CELL_H * SS), (0, 0, 0, 0))
        self.d = ImageDraw.Draw(self.im)

    def p(self, x, y): return (x * CELL_W * SS, (1 - y) * CELL_H * SS)
    def w(self, width): return max(1, int(width * CELL_W * SS))

    def ellipse(self, cx, cy, rx, ry, fill):
        x0, y0 = self.p(cx - rx, cy + ry); x1, y1 = self.p(cx + rx, cy - ry)
        self.d.ellipse((x0, y0, x1, y1), fill=fill)

    def curve(self, pts, width, fill):
        """A thick polyline with round caps and joins."""
        px = [self.p(x, y) for x, y in pts]
        self.d.line(px, fill=fill, width=self.w(width), joint="curve")
        r = self.w(width) / 2
        for x, y in (px[0], px[-1]): self.d.ellipse((x - r, y - r, x + r, y + r), fill=fill)

    def polygon(self, pts, fill):
        self.d.polygon([self.p(x, y) for x, y in pts], fill=fill)

    def done(self): return self.im.resize((CELL_W, CELL_H), Image.LANCZOS)


def arc(cx, cy, rx, ry, a0, a1, n=24):
    """Points on an elliptical arc, angles in degrees (0 = right, 90 = up)."""
    return [(cx + rx * math.cos(math.radians(a0 + (a1 - a0) * i / n)), cy + ry * math.sin(math.radians(a0 + (a1 - a0) * i / n))) for i in range(n + 1)]


def expression(kind, eye, brow):
    c = Cell()
    L, R = .33, .67                      # eye centres (character's right eye on the viewer's left)
    ey, by, my = .52, .72, .24
    blush = kind in ("happy", "cheer")
    if blush:
        for x in (.19, .81): c.ellipse(x, .36, .075, .035, BLUSH)

    # Eyes.
    for i, x in enumerate((L, R)):
        side = -1 if i == 0 else 1       # -1 viewer's left
        if kind in ("neutral", "sad", "surprised", "focus") and LASHES:
            # Two small flicks off the outer corner of each open eye.
            top = ey + (.07 if kind == "surprised" else .05)
            for k in (0, 1):
                c.curve([(x + side * (.022 + .012 * k), top - .006 * k), (x + side * (.05 + .014 * k), top + .018 - .004 * k)], .008, eye)
        if kind in ("neutral", "sad"):
            c.ellipse(x, ey - (.015 if kind == "sad" else 0), .038, .062, eye)
            c.ellipse(x + .012, ey + .022, .011, .016, (255, 255, 255, 235))
        elif kind == "surprised":
            c.ellipse(x, ey + .01, .046, .076, eye)
            c.ellipse(x + .014, ey + .036, .013, .019, (255, 255, 255, 235))
        elif kind == "blink":
            c.curve(arc(x, ey + .02, .045, .035, 200, 340), .016, eye)
        elif kind in ("happy", "cheer"):
            c.curve(arc(x, ey - .02, .045, .05, 20, 160), .018, eye)
        elif kind == "focus":
            # Narrowed: the top of the eye flattened by a lid.
            c.ellipse(x, ey - .008, .036, .048, eye)
            c.polygon([(x - .06, ey + .05), (x + .06, ey + .05), (x + .06, ey + .018 + .012 * side), (x - .06, ey + .018 - .012 * side)], (0, 0, 0, 0))
            c.d.rectangle((0, 0, 0, 0))
        elif kind == "effort":
            # Squeezed shut: > <
            s = -side
            c.curve([(x - .035 * s, ey + .035), (x + .03 * s, ey), (x - .035 * s, ey - .035)], .017, eye)

    # Brows: (height offset, tilt in degrees toward the middle), per expression.
    raise_, tilt = {"neutral": (0, 0), "blink": (0, 0), "happy": (.025, 0), "focus": (-.03, 14), "effort": (-.035, 18),
                    "sad": (.015, -16), "surprised": (.06, 0), "cheer": (.035, 0)}[kind]
    for i, x in enumerate((L, R)):
        inward = 1 if i == 0 else -1     # toward the middle of the face
        pts = arc(x, by + raise_ - .03, .055, .03, 30, 150, 16)
        t = math.radians(tilt * inward)
        pts = [(x + (px - x) * math.cos(t) - (py - by) * math.sin(t) * .8, by + raise_ + (py - by - raise_) * math.cos(t) + (px - x) * math.sin(t)) for px, py in pts]
        c.curve(pts, .014, brow)

    # Mouth.
    if kind in ("neutral", "blink"):
        c.curve(arc(.5, my + .03, .05, .03, 205, 335), .014, MOUTH)
    elif kind == "focus":
        c.curve([(.465, my), (.535, my)], .013, MOUTH)
    elif kind == "sad":
        c.curve(arc(.5, my - .03, .045, .028, 25, 155), .014, MOUTH)
    elif kind == "surprised":
        c.ellipse(.5, my, .022, .034, MOUTH)
    elif kind == "effort":
        c.polygon([(.44, my + .025), (.56, my + .025), (.55, my - .025), (.45, my - .025)], MOUTH)
        c.polygon([(.452, my + .014), (.548, my + .014), (.542, my - .014), (.458, my - .014)], TEETH)
        c.curve([(.452, my), (.548, my)], .004, MOUTH)
    elif kind in ("happy", "cheer"):
        rx, ry = (.06, .06) if kind == "cheer" else (.045, .045)
        top = my + .02
        c.polygon([(.5 - rx, top), (.5 + rx, top)] + arc(.5, top, rx, ry, 0, -180)[1:-1][::-1], MOUTH)
        c.polygon([(.5 - rx, top)] + [(x, y) for x, y in arc(.5, top, rx, ry, 180, 360)] , MOUTH)
        c.ellipse(.5, top - ry * .62, rx * .55, ry * .3, TONGUE)
    return c.done()


def main():
    eye = arg_colour("--eye", EYE); brow = arg_colour("--brow", BROW)
    atlas = Image.new("RGBA", (CELL_W * 4, CELL_H * 2), (0, 0, 0, 0))
    for i, kind in enumerate(EXPRESSIONS):
        atlas.alpha_composite(expression(kind, eye, brow), ((i % 4) * CELL_W, (i // 4) * CELL_H))
    for out in (OUT_UNITY, OUT_LIBRARY):
        out.parent.mkdir(parents=True, exist_ok=True); atlas.save(out)
    print("atlas", atlas.size, "->", OUT_UNITY)


main()
