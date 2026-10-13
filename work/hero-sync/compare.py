#!/usr/bin/env python3
"""Hero sync step 5: lobby (SceneKit) vs game (Unity) sheets from the same cameras, plus a colour report.

usage: compare.py <out dir with unity/<sport>/ and lobby/<sport>/> <sheets dir>
Writes <sheets>/<sport>_<sex>.png (all framings, default look), <sport>_<sex>_head.png (full-size faces), tennis_colours.png (every
look case) and PARITY.md: per shot, the mean sRGB colour of the hero pixels in each render, the lobby/game luminance ratio and the mean
absolute difference over the hero pixels (same camera, so the pixels line up; the background colour is shared and masked out).
"""
import glob, os, sys
import numpy as np
from PIL import Image, ImageDraw, ImageFont

out, sheets = sys.argv[1], sys.argv[2]
os.makedirs(sheets, exist_ok=True)
BG = np.array([.93, .62, .48]) * 255
try: FONT = ImageFont.truetype('/System/Library/Fonts/Helvetica.ttc', 26)
except OSError: FONT = ImageFont.load_default()
FRAMINGS = ['body', 'torso', 'head', 'feet']

def load(p): return np.asarray(Image.open(p).convert('RGB')).astype(np.float32)
def mask(a): return np.abs(a - BG).max(axis=2) > 18

def stats(lobby, game):
    a, b = load(lobby), load(game)
    m = mask(a) | mask(b)
    if m.sum() < 50: return None
    la, lb = a[m].mean(0), b[m].mean(0)
    lum = lambda c: .2126 * c[0] + .7152 * c[1] + .0722 * c[2]
    return dict(lobby=la, game=lb, ratio=lum(la) / max(1, lum(lb)), mad=np.abs(a - b)[m].mean(), pixels=int(m.sum()))

def pair(lobby, game, scale, title):
    a, b = Image.open(lobby).convert('RGB'), Image.open(game).convert('RGB')
    w, h = int(a.width * scale), int(a.height * scale)
    a, b = a.resize((w, h), Image.LANCZOS), b.resize((w, h), Image.LANCZOS)
    row = Image.new('RGB', (w * 2 + 8, h), (20, 20, 24)); row.paste(a, (0, 0)); row.paste(b, (w + 8, 0))
    d = ImageDraw.Draw(row); d.rectangle([0, 0, row.width, 36], fill=(20, 20, 24))
    d.text((10, 4), 'LOBBY  ' + title, font=FONT, fill=(255, 210, 120)); d.text((w + 18, 4), 'GAME  ' + title, font=FONT, fill=(150, 220, 255))
    return row

def stack(rows, path):
    if not rows: return
    W = max(r.width for r in rows); H = sum(r.height for r in rows) + 8 * (len(rows) - 1)
    sheet = Image.new('RGB', (W, H), (20, 20, 24)); y = 0
    for r in rows: sheet.paste(r, (0, y)); y += r.height + 8
    sheet.save(path)

report = ['# Lobby vs game (hero sync)', '', 'LOBBY = the app\'s SceneKit hero (GolfArcade/Unity/CharacterAssets, MatchHeroSurfaces); GAME = Unity drawing the same hero, same pose, same cameras and lights.',
          'Mean sRGB of the hero pixels; ratio = lobby/game luminance; MAD = mean absolute difference per channel over the hero pixels (0-255).', '',
          '| shot | lobby mean | game mean | lum ratio | MAD |', '|---|---|---|---|---|']
for sport in ['tennis', 'golf']:
    for sex in ['male', 'female']:
        rows = []
        for f in FRAMINGS:
            lobby, game = f'{out}/lobby/{sport}/{sex}_default_{f}.png', f'{out}/unity/{sport}/{sex}_default_{f}.png'
            if os.path.exists(lobby) and os.path.exists(game): rows.append(pair(lobby, game, .5, f'{sport} {sex} {f}'))
        stack(rows, f'{sheets}/{sport}_{sex}.png')
        lobby, game = f'{out}/lobby/{sport}/{sex}_default_head.png', f'{out}/unity/{sport}/{sex}_default_head.png'
        if os.path.exists(lobby) and os.path.exists(game): pair(lobby, game, 1, f'{sport} {sex} face').save(f'{sheets}/{sport}_{sex}_head.png')
        for game in sorted(glob.glob(f'{out}/unity/{sport}/{sex}_*.png')):
            shot = os.path.basename(game)[:-4]; lobby = f'{out}/lobby/{sport}/{shot}.png'
            if not os.path.exists(lobby): continue
            s = stats(lobby, game)
            if s: report.append('| %s %s | %s | %s | %.3f | %.1f |' % (sport, shot, '(%d,%d,%d)' % tuple(s['lobby']), '(%d,%d,%d)' % tuple(s['game']), s['ratio'], s['mad']))
cases = sorted({os.path.basename(p).split('_')[1] for p in glob.glob(f'{out}/unity/tennis/*_body.png')})
stack([pair(f'{out}/lobby/tennis/{sex}_{c}_body.png', f'{out}/unity/tennis/{sex}_{c}_body.png', .4, f'{sex} {c}')
       for sex in ['male', 'female'] for c in cases if os.path.exists(f'{out}/lobby/tennis/{sex}_{c}_body.png')], f'{sheets}/tennis_colours.png')
open(f'{sheets}/PARITY.md', 'w').write('\n'.join(report) + '\n')
print('  sheets + PARITY.md in', sheets)
