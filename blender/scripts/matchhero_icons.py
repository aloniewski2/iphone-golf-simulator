"""The locker's picture tiles for the match heroes: sizes the transparent renders of `matchhero_golf.py --assemble-only --icons`
(head_<style>_<m|f> for every hairstyle and bald, polo_m) to 256 px and puts them in Unity/Assets/Resources/UI/Look.

    for sex in Male Female; do Blender -b --factory-startup --python blender/scripts/matchhero_golf.py -- --sex $sex \
        --assemble-only --icons --preview <dir>; done
    python3 blender/scripts/matchhero_icons.py <dir>
"""
import os, sys
from PIL import Image

src = sys.argv[1]
out = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Unity", "Assets", "Resources", "UI", "Look")
STYLES = ("classic", "short", "curly", "long", "tail", "bald")
names = {f"head_{s}_{t}": f"hair_{s}_{t}" for s in STYLES for t in "mf"}
names["polo_m"] = "tab_outfit"


def tile(im, size=256, pad=14):
    """Trimmed to what is drawn, centred in a square with a margin."""
    box = im.getbbox()
    if box: im = im.crop(box)
    s = (size - 2 * pad) / max(im.width, im.height)
    im = im.resize((max(1, round(im.width * s)), max(1, round(im.height * s))), Image.LANCZOS)
    t = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    t.alpha_composite(im, ((size - im.width) // 2, (size - im.height) // 2))
    return t


for a, b in names.items():
    tile(Image.open(os.path.join(src, a + ".png")).convert("RGBA")).save(os.path.join(out, b + ".png"))
    print("wrote", b)
