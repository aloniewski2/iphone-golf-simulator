"""Assemble the tennis face expression atlas from Higgsfield-generated face parts.

Each part (one eye, one brow, one mouth...) was generated separately on a transparent
background, so every expression can be composed with the eyes, brows and mouth in exactly
the same place -- swapping expressions never makes the face jump. Cells are laid out 4x2:

    0 neutral   1 blink   2 happy     3 focus
    4 effort    5 sad     6 surprised 7 cheer

Cell aspect matches the face decal authored by author_tennis_faces.py (0.86 x 0.74 of the
head), with u running from the character's right (viewer's left) to their left.

Run:  python3 blender/scripts/build_face_atlas.py
"""
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
PARTS = ROOT / "SportsLibrary/ArtDirection/Tennis/Generated/face-parts"
OUT_UNITY = ROOT / "Unity/Assets/Resources/Tennis/Characters/FaceAtlas.png"
OUT_LIBRARY = ROOT / "SportsLibrary/ArtDirection/Tennis/Generated/FaceAtlas.png"
CELL_W, CELL_H = 512, 440
EXPRESSIONS = ["neutral", "blink", "happy", "focus", "effort", "sad", "surprised", "cheer"]


def part(name):
    im = Image.open(PARTS / f"{name}.png").convert("RGBA")
    alpha = im.split()[3].point(lambda a: 255 if a > 24 else 0)
    return im.crop(alpha.getbbox())


def place(cell, im, cx, cy, width, mirror=False, rotate=0.0, squash=1.0, alpha=1.0):
    """Paste `im` centred at (cx, cy) in cell fractions (y up), `width` of the cell wide."""
    if mirror: im = im.transpose(Image.FLIP_LEFT_RIGHT)
    if rotate: im = im.rotate(rotate, resample=Image.BICUBIC, expand=True)
    w = max(1, int(width * CELL_W))
    h = max(1, int(im.height * w / im.width * squash))
    im = im.resize((w, h), Image.LANCZOS)
    if alpha < 1:
        a = im.split()[3].point(lambda v: int(v * alpha)); im.putalpha(a)
    x = int(cx * CELL_W - w / 2); y = int((1 - cy) * CELL_H - h / 2)
    cell.alpha_composite(im, (max(0, x), max(0, y)))


def expression(kind, p):
    cell = Image.new("RGBA", (CELL_W, CELL_H), (0, 0, 0, 0))
    L, R = .31, .69          # eye centres: character's right eye on the viewer's left
    eye_y, brow_y, mouth_y, blush_y = .50, .72, .23, .35
    blush = .75 if kind in ("happy", "cheer") else .5
    place(cell, p["blush"], .20, blush_y, .18, alpha=blush)
    place(cell, p["blush"], .80, blush_y, .18, alpha=blush)
    eyes = {"neutral": "eye_open", "blink": "eye_blink", "happy": "eye_happy", "focus": "eye_focus",
            "effort": "eye_focus", "sad": "eye_open", "surprised": "eye_open", "cheer": "eye_happy"}[kind]
    eye_w = {"eye_open": .205, "eye_focus": .215, "eye_happy": .17, "eye_blink": .17}[eyes]
    squash = .86 if kind == "sad" else 1.08 if kind == "surprised" else 1.0
    y = eye_y - (.02 if kind == "sad" else 0)
    # Asymmetric features mirror for the other side; the glossy eye keeps its highlight
    # on the same side, as painted eyes do.
    place(cell, p[eyes], L, y, eye_w, squash=squash)
    place(cell, p[eyes], R, y, eye_w, mirror=eyes != "eye_open", squash=squash)
    brow = {"neutral": (0, 0), "blink": (0, 0), "happy": (.03, 0), "focus": (-.02, -14), "effort": (-.03, -18),
            "sad": (.02, 16), "surprised": (.06, 0), "cheer": (.04, 0)}[kind]
    if eyes != "eye_focus":  # the focused eye carries its own heavy lid
        place(cell, p["brow"], L, brow_y + brow[0], .15, rotate=brow[1])
        place(cell, p["brow"], R, brow_y + brow[0], .15, mirror=True, rotate=-brow[1])
    mouth = {"neutral": ("mouth_smile", .12), "blink": ("mouth_smile", .12), "happy": ("mouth_cheer", .15),
             "focus": ("mouth_smile", .09), "effort": ("mouth_effort", .17), "sad": ("mouth_frown", .11),
             "surprised": ("mouth_o", .07), "cheer": ("mouth_cheer", .19)}[kind]
    place(cell, p[mouth[0]], .5, mouth_y, mouth[1])
    return cell


def main():
    p = {n: part(n) for n in ["eye_open", "eye_happy", "eye_blink", "eye_focus", "brow", "mouth_smile",
                             "mouth_cheer", "mouth_o", "mouth_frown", "mouth_effort", "blush"]}
    atlas = Image.new("RGBA", (CELL_W * 4, CELL_H * 2), (0, 0, 0, 0))
    for i, kind in enumerate(EXPRESSIONS):
        atlas.alpha_composite(expression(kind, p), ((i % 4) * CELL_W, (i // 4) * CELL_H))
    for out in (OUT_UNITY, OUT_LIBRARY):
        out.parent.mkdir(parents=True, exist_ok=True); atlas.save(out)
    print("atlas", atlas.size, "->", OUT_UNITY)


main()
