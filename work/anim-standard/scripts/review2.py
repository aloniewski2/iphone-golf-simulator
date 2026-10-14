"""One review sheet per clip (pages/review2/<ctx>/<clipdir>.jpg), what I read to decide:
  A  overview: every frame, front / back / left / right, half size (crop of the middle 240 px)
  B  hands: every frame's left and right close-up, full size, 8 per row
  C  candidate crops: per automatic group (kind + what), up to 4 instances (largest first), 2x zoom around the candidate
  python3 review2.py <bd> <ctx> [clipdir ...]
Prints for each clip the candidate groups with their frame ranges (so a verdict can name them)."""
import glob, json, os, sys
from collections import defaultdict
from PIL import Image, ImageDraw, ImageFont

bd, ctx = sys.argv[1], sys.argv[2]
sm = ImageFont.truetype('/System/Library/Fonts/Supplemental/Arial Bold.ttf', 15)
big = ImageFont.truetype('/System/Library/Fonts/Supplemental/Arial Bold.ttf', 22)
VIEWS = ['front', 'back', 'left', 'right']
CW, CH = 120, 280            # overview tile (crop 240x560 -> half)
HW, HH = 240, 280
COL = {'lining': (255, 0, 255), 'hole': (255, 140, 0), 'skin': (255, 230, 0), 'hand': (0, 230, 255)}
root = os.path.join(bd, ctx)
dirs = sys.argv[3:] or sorted(os.path.basename(os.path.dirname(p)) for p in glob.glob(os.path.join(root, '*', 'auto.json')))
out = os.path.join(bd, 'pages', 'review2', ctx); os.makedirs(out, exist_ok=True)
for dn in dirs:
    d = os.path.join(root, dn)
    K = len(glob.glob(os.path.join(d, 'f*_front.jpg')))
    auto = json.load(open(os.path.join(d, 'auto.json'))) if os.path.exists(os.path.join(d, 'auto.json')) else []
    groups = defaultdict(list)
    for a in auto:
        what = a['what'] if a['kind'] != 'hand' else a['what']
        groups[(a['kind'], what)].append(a)
    gl = sorted(groups.items(), key=lambda kv: -len(kv[1]))
    ncol = max(K, 8)
    W = max(ncol * CW, 8 * HW, 1920)
    hand_rows = 2 * ((K + 7) // 8)
    crops = []
    for gi, ((kind, what), items) in enumerate(gl):
        items = sorted(items, key=lambda a: -(a.get('area', 0) + a.get('n', 0) * 3))
        seen = set(); pick = []
        for a in items:
            f = int(a['img'][1:3])
            if f in seen: continue
            seen.add(f); pick.append(a)
            if len(pick) == 4: break
        for a in pick: crops.append((gi, kind, what, a))
    crow = (len(crops) + 7) // 8
    H = 40 + 4 * (CH + 18) + 30 + hand_rows * (HH + 18) + 30 + crow * (240 + 18) + 10
    P = Image.new('RGB', (W, H), (16, 16, 20)); dr = ImageDraw.Draw(P, 'RGBA')
    dr.text((8, 8), f'{ctx}  {dn}  ({K} frames)   A overview  B hands  C candidates', fill=(255, 225, 140), font=big)
    y = 40
    for vi, v in enumerate(VIEWS):
        for k in range(K):
            f = os.path.join(d, f'f{k:02d}_{v}.jpg')
            if os.path.exists(f):
                im = Image.open(f).crop((80, 0, 320, 560)).resize((CW, CH))
                P.paste(im, (k * CW, y + 18))
            dr.text((k * CW + 3, y + 1), f'{k} {v[0]}', fill=(200, 200, 210), font=sm)
            # candidate marks (thin) on the overview
            for a in auto:
                if a['img'] == f'f{k:02d}_{v}':
                    cx, cy, r = k * CW + (a['x'] - 80) / 2, y + 18 + a['y'] / 2, max(5, a['r'] / 2)
                    dr.ellipse([cx - r, cy - r, cx + r, cy + r], outline=COL[a['kind']] + (220,), width=1)
        y += CH + 18
    dr.text((8, y + 5), 'B  hands (left row, then right row)', fill=(255, 225, 140), font=sm); y += 30
    for hb in range(0, K, 8):
        for hn in ['handL', 'handR']:
            for j, k in enumerate(range(hb, min(K, hb + 8))):
                f = os.path.join(d, f'f{k:02d}_{hn}.jpg')
                if os.path.exists(f): P.paste(Image.open(f), (j * HW, y + 18))
                dr.text((j * HW + 3, y + 1), f'f{k} {"L" if hn == "handL" else "R"}', fill=(200, 200, 210), font=sm)
                for a in auto:
                    if a['img'] == f'f{k:02d}_{hn}':
                        cx, cy, r = j * HW + a['x'], y + 18 + a['y'], a['r']
                        dr.ellipse([cx - r, cy - r, cx + r, cy + r], outline=COL[a['kind']] + (220,), width=2)
            y += HH + 18
    dr.text((8, y + 5), 'C  candidates (2x crops)', fill=(255, 225, 140), font=sm); y += 30
    for i, (gi, kind, what, a) in enumerate(crops):
        cx0, cy0 = (i % 8) * 240, y + (i // 8) * (240 + 18)
        img = a['img']; isHand = 'hand' in img
        f = os.path.join(d, f'{img}.jpg')
        if os.path.exists(f):
            im = Image.open(f); w0, h0 = im.size
            x0 = int(min(max(0, a['x'] - 60), w0 - 120)); y0 = int(min(max(0, a['y'] - 60), h0 - 120))
            P.paste(im.crop((x0, y0, x0 + 120, y0 + 120)).resize((240, 240)), (cx0, cy0 + 18))
            cx, cy, r = cx0 + (a['x'] - x0) * 2, cy0 + 18 + (a['y'] - y0) * 2, a['r'] * 2
            dr.ellipse([cx - r, cy - r, cx + r, cy + r], outline=COL[kind] + (160,), width=1)
        dr.text((cx0 + 3, cy0 + 1), f'g{gi} {kind[:4]} {what[:16]} {img}', fill=COL[kind], font=sm)
    P.save(os.path.join(out, dn + '.jpg'), quality=86)
    print(dn, K, 'frames |', '; '.join(f'g{gi} {k}/{w}: f{min(int(a["img"][1:3]) for a in it)}-{max(int(a["img"][1:3]) for a in it)} x{len(it)}' for gi, ((k, w), it) in enumerate(gl)))
