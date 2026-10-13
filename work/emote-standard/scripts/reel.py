"""One review reel from preview tile folders: a title card (0.8 s) then the clip, for each.  python3 reel.py <out.mp4> <title> <dir:label> ..."""
import glob, subprocess, sys
from PIL import Image, ImageDraw, ImageFont
ENC = '/Users/adnanyonathan/Documents/Codex/2026-09-20/wh/outputs/iphone-golf-simulator/work/emotes/scripts/enc'
out, title, items = sys.argv[1], sys.argv[2], sys.argv[3:]
big = ImageFont.truetype('/System/Library/Fonts/Supplemental/Arial Bold.ttf', 54)
small = ImageFont.truetype('/System/Library/Fonts/Supplemental/Arial Bold.ttf', 26)
frames = []
W = H = None
import os
tmp = os.path.splitext(out)[0] + '_cards'
os.makedirs(tmp, exist_ok=True)
for k, it in enumerate(items):
    d, label = it.split(':', 1)
    tiles = sorted(glob.glob(f'{d}/_tiles/t_*.png'))
    if W is None:
        W, H = Image.open(tiles[0]).size
    card = Image.new('RGB', (W, H), (22, 22, 28))
    dr = ImageDraw.Draw(card)
    dr.text((60, H // 2 - 60), label, fill=(255, 225, 140), font=big)
    dr.text((60, H // 2 + 20), title, fill=(200, 200, 210), font=small)
    cp = f'{tmp}/card_{k:02d}.png'
    card.save(cp)
    frames += [cp] * 24 + tiles + [tiles[-1]] * 10
subprocess.run([ENC, out, '30', str(W), str(H)] + frames, check=True, stdout=subprocess.DEVNULL)
print('REEL', len(frames), out)
