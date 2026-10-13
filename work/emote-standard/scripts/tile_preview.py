"""Tile the preview views side by side, label them, encode an mp4 (30 fps and a half-speed copy) and a contact sheet.
  python3 tile_preview.py <framesdir> <out_basename> <label> [sheet_every]"""
import glob
import os
import subprocess
import sys

from PIL import Image, ImageDraw, ImageFont

ENC = '/Users/adnanyonathan/Documents/Codex/2026-09-20/wh/outputs/iphone-golf-simulator/work/emotes/scripts/enc'
d, base, label = sys.argv[1], sys.argv[2], sys.argv[3]
every = int(sys.argv[4]) if len(sys.argv) > 4 else 4
font = ImageFont.truetype('/System/Library/Fonts/Menlo.ttc', 16)
small = ImageFont.truetype('/System/Library/Fonts/Menlo.ttc', 13)
frames = sorted(int(os.path.basename(p)[5:8]) for p in glob.glob(f'{d}/game_*.png'))
tiles = []
os.makedirs(f'{d}/_tiles', exist_ok=True)
for f in frames:
    ims = [Image.open(f'{d}/{v}_{f:03d}.png').convert('RGB') for v in ('game', 'front', 'side')]
    w, h = ims[0].size
    T = Image.new('RGB', (w * 3, h + 28), (24, 24, 28))
    for i, im in enumerate(ims):
        T.paste(im, (i * w, 28))
    dr = ImageDraw.Draw(T)
    dr.text((8, 5), f'{label}   frame {f:02d}  t={f / 30:.2f}s', fill=(255, 225, 140), font=font)
    for i, v in enumerate(('game camera', 'front 3/4', 'right side')):
        dr.text((i * w + 8, 32), v, fill=(20, 20, 20), font=small)
    p = f'{d}/_tiles/t_{f:03d}.png'
    T.save(p)
    tiles.append(p)
W, Hh = Image.open(tiles[0]).size
subprocess.run([ENC, base + '.mp4', '30', str(W), str(Hh)] + tiles, check=True, stdout=subprocess.DEVNULL)
slow = [t for t in tiles for _ in (0, 1)]
subprocess.run([ENC, base + '_half.mp4', '30', str(W), str(Hh)] + slow, check=True, stdout=subprocess.DEVNULL)
pick = tiles[::every]
cols = 4
rows = (len(pick) + cols - 1) // cols
sw, shh = W // 2, Hh // 2
S = Image.new('RGB', (cols * sw, rows * shh), (20, 20, 24))
for j, p in enumerate(pick):
    S.paste(Image.open(p).resize((sw, shh)), ((j % cols) * sw, (j // cols) * shh))
S.save(base + '_sheet.jpg', quality=85)
print('TILED', len(tiles), base)
