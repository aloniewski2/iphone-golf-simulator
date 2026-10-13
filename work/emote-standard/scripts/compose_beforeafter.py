"""Before / after film of two JoyFilm frame folders (same camera script): columns = before | after, rows = the given views.
  python3 compose_beforeafter.py <before_dir> <after_dir> <out_base> <title> [views=front,side,game]"""
import glob
import os
import subprocess
import sys

from PIL import Image, ImageDraw, ImageFont

ENC = '/Users/adnanyonathan/Documents/Codex/2026-09-20/wh/outputs/iphone-golf-simulator/work/emotes/scripts/enc'
bd, ad, base, title = sys.argv[1:5]
views = (sys.argv[5] if len(sys.argv) > 5 else 'front,side').split(',')
font = ImageFont.truetype('/System/Library/Fonts/Supplemental/Arial Bold.ttf', 26)
small = ImageFont.truetype('/System/Library/Fonts/Supplemental/Arial Bold.ttf', 20)
n = min(len(glob.glob(f'{bd}/{views[0]}_*.jpg')), len(glob.glob(f'{ad}/{views[0]}_*.jpg')))
CW = 540
out = f'{ad}/_ba'
os.makedirs(out, exist_ok=True)
tiles = []
for i in range(n):
    rows = []
    for v in views:
        ims = []
        for d in (bd, ad):
            im = Image.open(f'{d}/{v}_{i:05d}.jpg').convert('RGB')
            h = int(CW * im.size[1] / im.size[0])
            ims.append(im.resize((CW, h), Image.LANCZOS))
        rows.append(ims)
    H = 44 + sum(r[0].size[1] for r in rows)
    T = Image.new('RGB', (2 * CW + 8, H), (18, 18, 22))
    dr = ImageDraw.Draw(T)
    dr.text((10, 8), title, fill=(255, 225, 140), font=font)
    y = 44
    for r in rows:
        for c, im in enumerate(r):
            T.paste(im, (c * (CW + 8), y))
        y += r[0].size[1]
    dr.text((10, 50), 'BEFORE', fill=(255, 120, 120), font=small)
    dr.text((CW + 18, 50), 'AFTER', fill=(140, 255, 160), font=small)
    p = f'{out}/b_{i:04d}.png'
    T.save(p)
    tiles.append(p)
W, Hh = Image.open(tiles[0]).size
W -= W % 2
Hh -= Hh % 2
subprocess.run([ENC, base + '.mp4', '30', str(W), str(Hh)] + tiles, check=True, stdout=subprocess.DEVNULL)
print('BA', n, base)
