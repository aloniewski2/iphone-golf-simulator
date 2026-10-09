"""Builds the avatar from the kit and renders it: the Hero turnaround, a closet of looks, the parts lineups.

    Blender -b --factory-startup --python blender/scripts/avatar_mock.py -- --mode hero|closet|kit|one --out <dir>
        [--hair swept] [--hat cap] [--glasses round] [--beard beard] [--expr smile] [--name look]
"""
import bpy, sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from mathutils import Vector
import avatar_kit as K, avatar_base as B, avatar_parts as P, avatar_head_parts as H
import avatar_catalog as C

HAIR, HATS, GLASSES, BEARDS = C.by_key(C.HAIR), C.by_key(C.HATS), C.by_key(C.GLASSES), C.by_key(C.FACIAL)
TOPS, BOTTOMS = C.by_key(C.TOPS), C.by_key(C.BOTTOMS)
COLORS = {}     # role -> sRGB override for this avatar


def avatar(hair="swept", hat=None, glasses=None, beard=None, expr="smile", body=True, colors=None, top="polo", bottom="shorts"):
    """One avatar in the current scene; returns {slot: object}."""
    for role, c in (colors or {}).items():
        if role == "skin": K.skin_tone(c)
        elif role == "hair": K.hair_tone(c)
        else: K.role(role, c)
    head = B.head_mesh(); face = B.build_face(head, expr)
    out = {"head": head}
    head_space = [head] + face
    if hair:
        h = HAIR[hair](head, under_hat=hat is not None); out["hair"] = h; head_space.append(h)
    if beard:
        b = BEARDS[beard](head); out["beard"] = b; head_space.append(b)
    if hat:
        t = HATS[hat](head); out["hat"] = t; head_space.append(t)
    if glasses:
        g = GLASSES[glasses](head); out["glasses"] = g; head_space.append(g)
    B.place_head(head_space)
    out["face"] = K.join("Hero_Face", face)
    if body:
        out["skin"] = B.body_skin(); out["top"] = TOPS[top](); out["bottom"] = BOTTOMS[bottom](); out["shoes"] = P.golf_shoes()
    return out


def report(parts):
    tot = 0
    for k, o in parts.items():
        v, t = K.stats(o); tot += t; print(f"  {k:8s} {o.name:16s} {v:5d} verts {t:5d} tris")
    print("  total", tot, "tris")


def scene(**kw):
    K.reset()
    parts = avatar(**kw)
    sc = K.studio(); cam = K.camera(sc)
    return parts, sc, cam


def one(out, name, views, **kw):
    parts, sc, cam = scene(**kw)
    report(parts)
    for vname, (target, dist, yaw, res, lens, pitch) in views.items():
        K.shoot(sc, cam, f"{out}/{name}_{vname}.png", target, dist, yaw, res, lens=lens, pitch=pitch, samples=int(os.environ.get("AV_SAMPLES", 64)))


FULL = dict(front=((0, 0.03, 0.88), 5.0, 0, (640, 960), 62, 3), q34=((0, 0.03, 0.88), 5.0, -32, (640, 960), 62, 3),
            side=((0, 0.03, 0.88), 5.0, -90, (640, 960), 62, 3), back=((0, 0.03, 0.88), 5.0, 180, (640, 960), 62, 3))
HEAD = dict(head34=((0, 0.03, 1.56), 2.1, -30, (760, 760), 85, 3), headfront=((0, 0.03, 1.56), 2.1, 0, (760, 760), 85, 2))
BUST = dict(bust=((0, 0.03, 1.46), 2.6, -22, (520, 560), 70, 3))

if __name__ == "__main__":
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    arg = lambda k, d=None: argv[argv.index(k) + 1] if k in argv else d
    out = arg("--out", "/tmp/avatar"); os.makedirs(out, exist_ok=True)
    mode = arg("--mode", "hero")
    kw = dict(hair=arg("--hair", "swept") if arg("--hair", "swept") != "none" else None, hat=arg("--hat"), glasses=arg("--glasses"), beard=arg("--beard"), expr=arg("--expr", "smile"))
    if mode == "hero":
        one(out, "hero", {**FULL, **HEAD}, **kw)
    elif mode == "closet":
        # eight looks from the same kit: different haircut, hat, glasses, beard, skin tone, hair colour, outfit colours
        LOOKS = [
            ("swept", dict(hair="swept")),
            ("curls", dict(hair="curls", glasses="round", colors=dict(skin="#8B5A3C", hair="#2B1B14", Hero_Top="#F0501A", Hero_TopTrim="#FFFFFF"))),
            ("bob", dict(hair="bob", glasses="square", colors=dict(skin="#F2C6A4", hair="#8A4B2A", Hero_Top="#E8EEF8", Hero_TopTrim="#C2185B", Hero_Accent="#C2185B"))),
            ("straw", dict(hair="long", hat="straw", glasses="shades", colors=dict(skin="#D79B72", hair="#1E1A1A", Hero_Top="#FFD23F", Hero_TopTrim="#1B2F6B", Hero_HatB="#E8491D"))),
            ("spikes", dict(hair="spikes", beard="beard", colors=dict(skin="#C68A63", hair="#3A6FE8", Hero_Top="#1B2F6B", Hero_TopTrim="#F0501A"))),
            ("bucket", dict(hair="ponytail", hat="bucket", colors=dict(skin="#F4D0B0", hair="#C8372D", Hero_HatA="#2E7D4F", Hero_HatB="#F2F3F5", Hero_Top="#FFFFFF", Hero_TopTrim="#2E7D4F"))),
            ("cap", dict(hair="crop", hat="cap", beard="mustache", colors=dict(skin="#B9784F", hair="#5A3A22", Hero_HatA="#C8372D", Hero_HatB="#FFFFFF", Hero_Top="#FFFFFF", Hero_TopTrim="#C8372D"))),
            ("beanie", dict(hair=None, hat="beanie", glasses="round", colors=dict(skin="#6E4530", Hero_HatA="#2B2F3A", Hero_HatB="#F0501A", Hero_Top="#9AA7C7", Hero_TopTrim="#2B2F3A"))),
        ]
        for nm, k in LOOKS:
            one(out, "closet_" + nm, dict(bust=((0, 0.03, 1.46), 2.6, -20, (520, 580), 70, 3)), **k)
    elif mode == "kit":
        hero_cols = dict(skin="#E8A074")
        only = os.environ.get("KIT_ONLY")
        for nm in HAIR:
            if only and nm != only: continue
            low = nm == "long"                                   # long hair hangs below the chin: frame lower and wider
            one(out, "kit_hair_" + nm, dict(v=((0, 0.03, 1.44 if low else 1.56), 2.75 if low else 2.25, -25, (420, 440), 85, 3)), hair=nm, body=False)
        if only: sys.exit(0)
        for nm in HATS: one(out, "kit_hat_" + nm, dict(v=((0, 0.03, 1.58), 2.6, -25, (420, 440), 85, 3)), hair="swept", hat=nm, body=False)
        for nm in GLASSES: one(out, "kit_glasses_" + nm, dict(v=((0, 0.03, 1.56), 2.25, -25, (420, 440), 85, 3)), hair="crop", glasses=nm, body=False)
        for nm in BEARDS: one(out, "kit_face_" + nm, dict(v=((0, 0.03, 1.56), 2.25, -25, (420, 440), 85, 3)), hair="crop", beard=nm, body=False)
    elif mode == "icons":
        # one transparent picture per style, for the game's pickers: heads for the head styles, the garment alone for clothes.
        # (512 px; avatar_icons.py scales them to the tile size.)  ICON_ONLY=hair_afro renders just that one.
        only = {x for x in os.environ.get("ICON_ONLY", "").split(",") if x}
        brown = dict(hair="#5B4334")
        def icon(name, views, **k):
            if only and name not in only: return
            try: one(out, name, views, **k)
            except Exception as e:
                import traceback; print("ICON FAILED", name, e); traceback.print_exc()
        def head_view(z, dist, yaw=-22): return dict(i=((0, 0.03, z), dist, yaw, (512, 512), 85, 3))
        for key, _, f in C.HAIR:
            wide = key in ("long", "braids", "pigtails", "afro")
            icon("hair_" + key, head_view(1.50 if key in ("long", "braids") else 1.56, 2.9 if wide else 2.35), hair=None if key == "bald" else key, body=False, colors=brown)
        for key, _, f in C.HATS:
            if f: icon("hat_" + key, head_view(1.62, 3.7 if key in ("cowboy", "straw") else 3.2 if key in ("bucket", "fedora", "tophat", "crown", "beret", "flatcap") else 2.7), hair="crop", hat=key, body=False, colors=brown)
        for key, _, f in C.GLASSES:
            if f: icon("glasses_" + key, head_view(1.54, 1.85, -18), hair="crop", glasses=key, body=False, colors=brown)
        for key, _, f in C.FACIAL:
            if f: icon("facial_" + key, head_view(1.52, 1.85, -18), hair="crop", beard=key, body=False, colors=brown)
        def garment(name, make, target, dist, yaw=-20):
            if only and name not in only: return
            try:
                K.reset(); make(); sc = K.studio(); cam = K.camera(sc)
                K.shoot(sc, cam, f"{out}/{name}_i.png", target, dist, yaw, (512, 512), lens=70, pitch=3, samples=int(os.environ.get("AV_SAMPLES", 32)))
            except Exception as e:
                import traceback; print("ICON FAILED", name, e); traceback.print_exc()
        for key, _, f in C.TOPS: garment("top_" + key, f, (0, 0.03, 1.10 if key != "hoodie" else 1.06), {"hoodie": 2.2, "vest": 1.4}.get(key, 1.75))
        for key, _, f in C.BOTTOMS: garment("bottom_" + key, f, (0, 0.03, 0.76 if key != "trousers" else 0.58), 1.75 if key != "trousers" else 2.5)
        garment("shoes_golf", P.golf_shoes, (0, 0.0, 0.12), 1.35, -35)
        def tab_polo():
            K.role("Hero_Top", "#3A57C9"); K.role("Hero_TopTrim", "#F2F3F5"); return P.polo()
        garment("tab_outfit", tab_polo, (0, 0.03, 1.10), 1.75)
    elif mode == "shorts":
        one(out, "shorts", dict(front=((0, 0.03, 0.80), 1.5, 0, (700, 500), 70, 4), q34=((0, 0.03, 0.80), 1.5, -40, (700, 500), 70, 4), low=((0, 0.03, 0.80), 1.5, 0, (700, 500), 70, 25)), **kw)
    elif mode == "shoe":
        one(out, "shoe", dict(shoe=((0.05, -0.02, 0.14), 1.6, -40, (800, 560), 70, 10), shoeside=((0.187, 0.0, 0.14), 1.6, -90, (800, 560), 70, 4)), **kw)
    elif mode == "one":
        one(out, arg("--name", "look"), BUST, **kw)
    elif mode == "bust":
        one(out, arg("--name", "look"), {**BUST, **HEAD}, **kw)
