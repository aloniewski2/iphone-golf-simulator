#!/usr/bin/env python3
"""Fetch MakeHuman's CC0 hairs (the ones in mhhair_sources.py) and prepare their textures for the game.

  python3 blender/scripts/fetch_mhhair.py [--src DIR]

Downloads only the hair files out of MakeHuman's 267 MB "system assets" zip (HTTP range requests: about 35 MB) into blender/mhhair/hair/<folder>/
(not committed), then writes the game's textures, Unity/Assets/Resources/Hero/Hair/<Name>.png: the strands as grey (luminance / 1.6 of the
average, so any hair colour can tint them and the strands stay), 1024 px, with the transparent texels' colour filled in (no dark fringe when mipped).
--src DIR: use hair/<folder>/ already extracted under DIR instead of downloading. Needs Pillow.
"""
import io, os, sys, shutil, zipfile, urllib.request
from PIL import Image
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from mhhair_sources import HAIRS

REPO = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
URL = "https://files2.makehumancommunity.org/asset_packs/makehuman_system_assets/makehuman_system_assets_cc0.zip"
DEST = os.path.join(REPO, "blender", "mhhair")
TEX = os.path.join(REPO, "Unity", "Assets", "Resources", "Hero", "Hair")
SIZE = 1024                      # (Unity compresses it; the locks need the pixels)
LOCK_FINE, LOCK_COARSE = 2.0, 13.0   # blur radii (px): the single hairs go below the first, the locks live between the two
DETAIL, SHADE = 1.9, 0.30        # how hard the lock streaks are drawn; how much of the broad shade (shadow painted into the original) stays
TONE_LO, TONE_HI = 0.28, 0.78    # the grey never goes past these (0.5 is the average): streaks and a little shade, no dirty patches
ALPHA_BLUR, ALPHA_LO, ALPHA_HI = 3.0, 0.16, 0.62   # the cut-out: a light blur, then a soft ramp (the shader dissolves it with a fine dither)
# per hair: a close crop or a quiff loses its fine wisps; the hairs with big dark painted-in patches keep less shade and tone
OVERRIDE = {"Crop": dict(alpha_blur=4.5, lo=0.14, hi=0.6), "Quiff": dict(alpha_blur=4.0, lo=0.14, hi=0.6),
            "Braid": dict(tone=(0.38, 0.66), shade=0.0, coarse=6.0, detail=2.4), "Long": dict(tone=(0.4, 0.64), shade=0.0, coarse=5.0, detail=2.2), "SideBob": dict(tone=(0.34, 0.7), shade=0.12), "Fringe": dict(tone=(0.34, 0.7)), "Ponytail": dict(tone=(0.34, 0.7))}


class HttpFile(io.RawIOBase):
    def __init__(self, url):
        self.url = url
        self.size = int(urllib.request.urlopen(urllib.request.Request(url, method="HEAD")).headers["Content-Length"])
        self.pos = 0
    def seekable(self): return True
    def readable(self): return True
    def tell(self): return self.pos
    def seek(self, off, whence=0):
        self.pos = off if whence == 0 else self.pos + off if whence == 1 else self.size + off
        return self.pos
    def readinto(self, b):
        n = min(len(b), self.size - self.pos)
        if n <= 0: return 0
        d = urllib.request.urlopen(urllib.request.Request(self.url, headers={"Range": f"bytes={self.pos}-{self.pos + n - 1}"})).read()
        b[:len(d)] = d; self.pos += len(d); return len(d)


def download():
    z = zipfile.ZipFile(io.BufferedReader(HttpFile(URL), 1 << 16))
    folders = {f for f, _, _ in HAIRS.values()}
    for n in z.namelist():
        parts = n.split("/")
        if len(parts) == 3 and parts[0] == "hair" and parts[1] in folders and not n.endswith("/") and parts[2].endswith((".obj", ".png")):
            z.extract(n, DEST)
    print("fetched into", DEST)


def prepare(name, src, dst):
    """Stylise the strand texture the way a stylised game does (Fortnite's hair is the aim: chunky locks with painted streaks and a clean edge), not
    photographed strands and not a smooth blob. The photo's three scales are separated: the single hairs are blurred away, the LOCKS (clump-sized streaks:
    the band between a blur of ~2 px and one of ~12) are kept and sharpened into the colour, and the big dark patches the originals have painted in
    (shadow under a layer, a root) are mostly dropped. Alpha: the card's cut-out lightly blurred and cut again, so the hair's own lock tips stay as
    clean points and thin gaps fill. The grey is normalised so its average is 1/1.6 of full (the shader multiplies by 1.6 and by the hair colour)."""
    import numpy as np
    from PIL import ImageFilter
    im = Image.open(src).convert("RGBA").resize((SIZE, SIZE), Image.LANCZOS)
    a = np.asarray(im.getchannel("A"), dtype=np.float32) / 255.0
    rgb = np.asarray(im.convert("RGB"), dtype=np.float32) / 255.0
    lum = rgb @ np.array([0.299, 0.587, 0.114], dtype=np.float32)
    solid = a > 0.5
    mean_l = max(float(lum[solid].mean()) if solid.any() else 0.5, 0.05)
    lum_f = np.where(solid, lum, mean_l)                                  # no dark halo from the transparent texels
    def blur(x, r): return np.asarray(Image.fromarray((np.clip(x, 0, 1) * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(r)), dtype=np.float32) / 255.0
    n = lum_f / (mean_l * 2.0)                                             # 0.5 is the average
    lo = blur(n, LOCK_FINE)                                                # the locks and their streaks
    mid = blur(n, OVERRIDE.get(name, {}).get("coarse", LOCK_COARSE))        # the broad shade
    streak = lo - mid                                                      # the streaks alone
    o = OVERRIDE.get(name, {})
    lo_t, hi_t = o.get("tone", (TONE_LO, TONE_HI))
    t = np.clip(0.5 + streak * o.get("detail", DETAIL) + (mid - 0.5) * o.get("shade", SHADE), lo_t, hi_t)
    grey = np.clip(t * 255.0 / 0.8, 0, 255)                                 # (average t = 0.5 -> stored 0.625: the shader gain of 1.6 brings it back to 1 x the hair colour)
    ab = blur(a, o.get("alpha_blur", ALPHA_BLUR))
    lo_a, hi_a = o.get("lo", ALPHA_LO), o.get("hi", ALPHA_HI)
    a2 = np.clip((ab - lo_a) / (hi_a - lo_a), 0, 1)
    a2 = blur(a2, 1.2)
    out = np.dstack([grey, grey, grey, a2 * 255.0]).astype(np.uint8)
    res = Image.fromarray(out, "RGBA")
    os.makedirs(os.path.dirname(dst), exist_ok=True)
    res.save(dst, optimize=True)
    print(f"  {name}: {os.path.getsize(dst) // 1024} KB")


def main():
    src = sys.argv[sys.argv.index("--src") + 1] if "--src" in sys.argv else None
    if src:
        for folder in {f for f, _, _ in HAIRS.values()}:
            os.makedirs(os.path.join(DEST, "hair", folder), exist_ok=True)
            for fn in os.listdir(os.path.join(src, "hair", folder)):
                if fn.endswith((".obj", ".png")): shutil.copy(os.path.join(src, "hair", folder, fn), os.path.join(DEST, "hair", folder, fn))
    else:
        download()
    for name, (folder, tex, _) in HAIRS.items():
        prepare(name, os.path.join(DEST, "hair", folder, tex), os.path.join(TEX, name + ".png"))


if __name__ == "__main__":
    main()
