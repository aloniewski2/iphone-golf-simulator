"""In-game film -> mp4: the game's own picture (left, 1280x720) beside the body-relative front and side views (stacked, 360x360 each), 30 fps + half speed,
and a contact sheet.   python3 compose_film.py <framesdir> <out_base> <title>"""
import glob
import os
import subprocess
import sys

from PIL import Image, ImageDraw, ImageFont

ENC = '/Users/adnanyonathan/Documents/Codex/2026-09-20/wh/outputs/iphone-golf-simulator/work/emotes/scripts/enc'
d, base, title = sys.argv[1], sys.argv[2], sys.argv[3]
font = ImageFont.truetype('/System/Library/Fonts/Supplemental/Arial Bold.ttf', 24)
small = ImageFont.truetype('/System/Library/Fonts/Supplemental/Arial Bold.ttf', 18)
games = sorted(glob.glob(f'{d}/game_*.jpg'))
out = f'{d}/_comp'
os.makedirs(out, exist_ok=True)
tiles = []
for i, g in enumerate(games):
    idx = os.path.basename(g)[5:10]
    G = Image.open(g).convert('RGB')
    Fr = Image.open(f'{d}/front_{idx}.jpg').convert('RGB').resize((360, 360), Image.LANCZOS)
    Sd = Image.open(f'{d}/side_{idx}.jpg').convert('RGB').resize((360, 360), Image.LANCZOS)
    T = Image.new('RGB', (1280 + 360, 720), (18, 18, 22))
    T.paste(G, (0, 0))
    T.paste(Fr, (1280, 0))
    T.paste(Sd, (1280, 360))
    dr = ImageDraw.Draw(T)
    dr.rectangle([0, 0, dr.textlength(title, font=font) + 20, 34], fill=(20, 20, 24))
    dr.text((10, 4), title, fill=(255, 225, 140), font=font)
    dr.text((1290, 6), 'front', fill=(255, 255, 255), font=small)
    dr.text((1290, 366), 'side', fill=(255, 255, 255), font=small)
    dr.text((10, 690), f'{i / 30:.2f}s', fill=(255, 255, 255), font=small)
    p = f'{out}/c_{i:04d}.png'
    T.save(p)
    tiles.append(p)
subprocess.run([ENC, base + '.mp4', '30', '1640', '720'] + tiles, check=True, stdout=subprocess.DEVNULL)
subprocess.run([ENC, base + '_half.mp4', '30', '1640', '720'] + [t for t in tiles for _ in (0, 1)], check=True, stdout=subprocess.DEVNULL)
pick = tiles[::max(1, len(tiles) // 16)][:16]
S = Image.new('RGB', (4 * 820, 4 * 360), (18, 18, 22))
for j, p in enumerate(pick):
    S.paste(Image.open(p).resize((820, 360)), ((j % 4) * 820, (j // 4) * 360))
S.save(base + '_sheet.jpg', quality=85)
print('FILM', len(tiles), base)
