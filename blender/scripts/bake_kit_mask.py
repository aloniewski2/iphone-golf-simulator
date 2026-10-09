"""Bake the player kits' recolour masks (character screen → outfit colours in Unity).

For each exported player body (Resources/Tennis/Opponents/Avatar.fbx, AvatarF.fbx) this reads
its colour map (Resources/Tennis/Characters/Higgs<Name>_Color.png) and writes
Higgs<Name>_Mask.png, 1024², one region per channel:

    R  shirt   — the garment on the torso (Spine, Chest, Neck), main colour or white
    G  shorts  — the garment on the hips and thighs (main colour or white)
    B  accent  — the bright orange stripes, headband and wristbands, anywhere
    A  skin    — skin tone, anywhere

The colour family comes from the texel (the Tripo kit is flat colours: teal, white, orange,
tan skin, dark hair); the body part comes from the triangle's dominant bone, rasterised into
UV space. Alongside, Higgs<Name>_Mask.json holds each region's mean linear luminance, which
the recolour shader divides by to keep the texture's shading under the new colour.

Run:  Blender --background --factory-startup --python bake_kit_mask.py -- [Avatar AvatarF]
"""
import colorsys
import json
import sys
from pathlib import Path

import bpy
import numpy as np

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]
BODIES = ROOT / "Unity/Assets/Resources/Tennis/Opponents"
TEXTURES = ROOT / "Unity/Assets/Resources/Tennis/Characters"
SIZE = 1024
UPPER = {"Spine", "Chest", "Neck"}
LOWER = {"Hips", "UpperLeg.L", "UpperLeg.R"}
REGION = {"upper": 1, "lower": 2, "other": 3}


def load_rgb(path):
    img = bpy.data.images.load(str(path))
    w, h = img.size
    px = np.array(img.pixels[:], dtype=np.float32).reshape(h, w, img.channels)[:, :, :3]
    bpy.data.images.remove(img)
    # Down to the mask size (box filter), flipped so row 0 is v = 0 like Blender's pixels.
    k = w // SIZE
    return px.reshape(SIZE, k, SIZE, k, 3).mean(axis=(1, 3))


def classify(rgb):
    """Colour families per texel (sRGB 0..1): garment, white, accent, skin."""
    flat = rgb.reshape(-1, 3)
    hsv = np.array([colorsys.rgb_to_hsv(*c) for c in flat[:: 1]]).reshape(SIZE, SIZE, 3)
    h, s, v = hsv[..., 0] * 360, hsv[..., 1], hsv[..., 2]
    accent = (h > 8) & (h < 40) & (s > .8) & (v > .6)
    skin = (h > 12) & (h < 45) & (s > .25) & (s <= .8) & (v > .38) & ~((s > .8) & (v > .6))
    white = (s < .16) & (v > .7)
    garment = (h > 170) & (h < 230) & (s > .25)
    return garment, white, accent, skin


def region_map(obj):
    """Each texel's body region from the triangle covering it in UV space."""
    me = obj.data
    me.calc_loop_triangles()
    groups = {g.index: g.name for g in obj.vertex_groups}
    vert_region = np.full(len(me.vertices), REGION["other"], np.int8)
    for v in me.vertices:
        if not v.groups: continue
        best = max(v.groups, key=lambda g: g.weight)
        name = groups.get(best.group, "")
        vert_region[v.index] = REGION["upper"] if name in UPPER else REGION["lower"] if name in LOWER else REGION["other"]
    uv = me.uv_layers.active.data
    out = np.zeros((SIZE, SIZE), np.int8)
    for tri in me.loop_triangles:
        regs = [vert_region[i] for i in tri.vertices]
        r = max(set(regs), key=regs.count)
        p = np.array([uv[l].uv[:] for l in tri.loops]) * SIZE
        x0, y0 = np.floor(p.min(0)).astype(int); x1, y1 = np.ceil(p.max(0)).astype(int)
        x0, y0 = max(x0, 0), max(y0, 0); x1, y1 = min(x1, SIZE - 1), min(y1, SIZE - 1)
        if x1 < x0 or y1 < y0: continue
        xs, ys = np.meshgrid(np.arange(x0, x1 + 1) + .5, np.arange(y0, y1 + 1) + .5)
        (ax, ay), (bx, by), (cx, cy) = p
        d = (by - cy) * (ax - cx) + (cx - bx) * (ay - cy)
        if abs(d) < 1e-9: continue
        l1 = ((by - cy) * (xs - cx) + (cx - bx) * (ys - cy)) / d
        l2 = ((cy - ay) * (xs - cx) + (ax - cx) * (ys - cy)) / d
        inside = (l1 >= -.02) & (l2 >= -.02) & (1 - l1 - l2 >= -.02)
        sub = out[y0:y1 + 1, x0:x1 + 1]
        sub[inside] = r
    # Grow regions a few texels into the gutters so filtering at seams stays in-region.
    for _ in range(4):
        empty = out == 0
        if not empty.any(): break
        grown = out.copy()
        for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            shifted = np.roll(out, (dy, dx), axis=(0, 1))
            fill = empty & (shifted != 0) & (grown == 0)
            grown[fill] = shifted[fill]
        out = grown
    return out


def linear(c):
    return np.where(c <= .04045, c / 12.92, ((c + .055) / 1.055) ** 2.4)


def bake(name):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(BODIES / f"{name}.fbx"))
    body = next(o for o in bpy.data.objects if o.type == "MESH" and o.name.startswith("V4 Higgs body"))
    rgb = load_rgb(TEXTURES / f"Higgs{name}_Color.png")
    garment, white, accent, skin = classify(rgb)
    regions = region_map(body)
    shirt = (garment | white) & (regions == REGION["upper"])
    shorts = (garment | white) & (regions == REGION["lower"])
    mask = np.zeros((SIZE, SIZE, 4), np.float32)
    mask[..., 0] = shirt; mask[..., 1] = shorts; mask[..., 2] = accent; mask[..., 3] = skin
    img = bpy.data.images.new(f"Higgs{name}_Mask", SIZE, SIZE, alpha=True)
    img.colorspace_settings.name = "Non-Color"
    img.alpha_mode = "CHANNEL_PACKED"   # four independent masks: never premultiply
    img.pixels.foreach_set(mask.ravel())
    img.filepath_raw = str(TEXTURES / f"Higgs{name}_Mask.png"); img.file_format = "PNG"; img.save()
    lum = linear(rgb) @ np.array([.2126, .7152, .0722])
    ref = {k: float(lum[m].mean()) if m.any() else .5 for k, m in
           (("shirt", shirt), ("shorts", shorts), ("accent", accent), ("skin", skin))}
    (TEXTURES / f"Higgs{name}_Mask.json").write_text(json.dumps(ref))
    print(f"MASK {name}: shirt {shirt.mean():.3f} shorts {shorts.mean():.3f} accent {accent.mean():.3f} skin {skin.mean():.3f} ref {ref}", flush=True)


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    for name in argv or ["Avatar", "AvatarF"]:
        bake(name)


if __name__ == "__main__":
    main()
