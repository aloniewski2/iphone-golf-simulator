"""In-game reel: for each JoyFilm segment folder (game_/front_/side_ frames) a title card then the segment (game picture left, front + side
stacked right).  python3 reel_film.py <out.mp4> <title> <dir:label> ...  (also writes <out>_sheet.jpg: one mid frame per segment)"""
import glob
import os
import subprocess
import sys

from PIL import Image, ImageDraw, ImageFont

ENC = '/Users/adnanyonathan/Documents/Codex/2026-09-20/wh/outputs/iphone-golf-simulator/work/emotes/scripts/enc'
out, title, items = sys.argv[1], sys.argv[2], sys.argv[3:]
big = ImageFont.truetype('/System/Library/Fonts/Supplemental/Arial Bold.ttf', 50)
mid = ImageFont.truetype('/System/Library/Fonts/Supplemental/Arial Bold.ttf', 24)
small = ImageFont.truetype('/System/Library/Fonts/Supplemental/Arial Bold.ttf', 18)
tmp = os.path.splitext(out)[0] + '_frames'
os.makedirs(tmp, exist_ok=True)
W, H = 1280 + 360, 720
frames, mids = [], []
k = 0
for it in items:
    d, label = it.split(':', 1)
    games = sorted(glob.glob(f'{d}/game_*.jpg'))
    if not games:
        print('EMPTY', d)
        continue
    card = Image.new('RGB', (W, H), (22, 22, 28))
    dr = ImageDraw.Draw(card)
    dr.text((70, H // 2 - 50), label, fill=(255, 225, 140), font=big)
    dr.text((70, H // 2 + 20), title, fill=(200, 200, 210), font=mid)
    cp = f'{tmp}/f_{k:05d}.jpg'
    card.save(cp, quality=90)
    k += 1
    frames += [cp] * 20
    for i, g in enumerate(games):
        idx = os.path.basename(g)[5:10]
        T = Image.new('RGB', (W, H), (18, 18, 22))
        T.paste(Image.open(g).convert('RGB'), (0, 0))
        for j, v in enumerate(('front', 'side')):
            p = f'{d}/{v}_{idx}.jpg'
            if os.path.exists(p):
                T.paste(Image.open(p).convert('RGB').resize((360, 360), Image.LANCZOS), (1280, 360 * j))
        dr = ImageDraw.Draw(T)
        dr.rectangle([0, 0, dr.textlength(label, font=mid) + 20, 34], fill=(20, 20, 24))
        dr.text((10, 5), label, fill=(255, 225, 140), font=mid)
        dr.text((1290, 6), 'front', fill=(255, 255, 255), font=small)
        dr.text((1290, 366), 'side', fill=(255, 255, 255), font=small)
        p = f'{tmp}/f_{k:05d}.jpg'
        T.save(p, quality=88)
        k += 1
        frames.append(p)
        if i == len(games) // 3:
            mids.append(p)
subprocess.run([ENC, out, '30', str(W), str(H)] + frames, check=True, stdout=subprocess.DEVNULL)
cols = 4
rows = (len(mids) + cols - 1) // cols
S = Image.new('RGB', (cols * 410, rows * 180), (18, 18, 22))
for j, p in enumerate(mids):
    S.paste(Image.open(p).resize((410, 180)), ((j % cols) * 410, (j // cols) * 180))
S.save(os.path.splitext(out)[0] + '_sheet.jpg', quality=85)
print('REEL', len(frames), out)
