"""Scales the style thumbnails (avatar_mock.py --mode icons, 512 px, transparent) to the game's tile size and writes them
to Unity/Assets/Resources/UI/Look/<kind>_<name>.png, the names the locker looks for (HeroGolfer's style names, lower case).

    python3 blender/scripts/avatar_icons.py <icons dir> [--size 256]
"""
import sys, os, glob
from PIL import Image

src = sys.argv[1]
size = int(sys.argv[sys.argv.index("--size") + 1]) if "--size" in sys.argv else 256
here = os.path.dirname(os.path.abspath(__file__))
out = os.path.normpath(os.path.join(here, "..", "..", "Unity", "Assets", "Resources", "UI", "Look"))
os.makedirs(out, exist_ok=True)
RENAME = {"hair_curls": "hair_curly"}          # the kit's key is "curls", the game's style is "Curly"
n = 0
for f in sorted(glob.glob(os.path.join(src, "*_i.png"))):
    name = os.path.basename(f)[:-6]
    name = RENAME.get(name, name)
    im = Image.open(f).convert("RGBA")
    box = im.getchannel("A").getbbox()
    im = im.resize((size, size), Image.LANCZOS)
    im.save(os.path.join(out, name + ".png"), optimize=True); n += 1
print(n, "icons ->", out)
