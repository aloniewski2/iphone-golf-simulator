"""Breakdown pages: per clip, pages of 4 frames (rows) x 4 views (front, back, left, right), 400x560 tiles.
  python3 sheets.py <breakdown_dir> <context> [--final verdicts.json]
review pages (pages/review/<context>/<clip>_pNN.jpg): the automatic candidates of detect.py (auto.json) as thin circles with a short tag
(magenta = garment inside showing, orange = see-through hole, yellow = skin through clothes, cyan = hand in body / under cloth / hands overlap).   final pages (pages/final/...): only what I confirmed (verdicts.json, scripts/verdict.py): accepted automatic candidates + manual circles,
red, with labels; the clip's summary lines under the title."""
import glob, json, os, sys
from PIL import Image, ImageDraw, ImageFont

TW, TH, HEAD, LAB = 400, 560, 46, 22
HW, HH = 240, 280   # hand close-ups column
VIEWS = ['front', 'back', 'left', 'right']
ROWS = 4
bd, ctx = sys.argv[1], sys.argv[2]
final = '--final' in sys.argv
ann = json.load(open(sys.argv[sys.argv.index('--final') + 1])) if final else {}
big = ImageFont.truetype('/System/Library/Fonts/Supplemental/Arial Bold.ttf', 24)
mid = ImageFont.truetype('/System/Library/Fonts/Supplemental/Arial Bold.ttf', 17)
sm = ImageFont.truetype('/System/Library/Fonts/Supplemental/Arial Bold.ttf', 15)
root = os.path.join(bd, ctx)
if os.path.exists(os.path.join(root, 'index.json')):
    index = json.load(open(os.path.join(root, 'index.json')))
else:      # still rendering: the finished clip folders (a clip is finished once its hints.json exists)
    index = []
    for d in sorted(glob.glob(os.path.join(root, '*', 'hands.json')), key=os.path.getmtime):
        dd = os.path.dirname(d); K = len(glob.glob(os.path.join(dd, 'f*_front.jpg')))
        index.append({'clip': os.path.basename(dd).replace('_', ':', 1), 'dir': os.path.basename(dd), 'frames': K, 'length': '?'})
only = os.environ.get('ONLY')
if only: index = [c for c in index if c['dir'] in only.split(',')]
out = os.path.join(bd, 'pages', 'final' if final else 'review', ctx)
os.makedirs(out, exist_ok=True)
COL = {'lining': (255, 0, 255), 'hole': (255, 140, 0), 'skin': (255, 230, 0), 'hand': (0, 230, 255)}
TAG = {'lining': 'L', 'hole': 'H', 'skin': 'S', 'hand': 'h'}
made = []
for c in index:
    d = os.path.join(root, c['dir'])
    auto = json.load(open(os.path.join(d, 'auto.json'))) if os.path.exists(os.path.join(d, 'auto.json')) else []
    by_img = {}
    for a in auto:
        if a['kind'] == 'lining' and a['what'].startswith('Kit_Shoe') and a['area'] < 80: continue   # the shoe opening around the sock
        by_img.setdefault(a['img'], []).append(a)
    K = c['frames']
    vkey = f"{ctx}/{c['dir']}"; ver = ann.get(vkey, {'accept': [], 'manual': [], 'summary': []})
    def confirmed(k, img):
        out = []
        for a in by_img.get(f'f{k:02d}_{img}', []):
            for acc in ver['accept']:
                if a['kind'] == acc['kind'] and acc['f0'] <= k <= acc['f1'] and (acc['what'] == '*' or acc['what'] in a['what']) and img in acc.get('views', [img]):
                    out.append((a['x'], a['y'], a['r'], acc['label'])); break
        out += [(m['x'], m['y'], m['r'], m['label']) for m in ver['manual'] if m['frame'] == k and m['img'] == img]
        return out
    def red(dr, items, ox, oy, W0, H0):
        said = set()
        for x, y, r, label in items:
            cx, cy = ox + x, oy + y
            dr.ellipse([cx - r, cy - r, cx + r, cy + r], outline=(255, 30, 30), width=3)
        boxes = []
        for x, y, r, label in items:
            if label in said: continue
            said.add(label); cx, cy = ox + x, oy + y
            tw = dr.textlength(label, font=sm)
            lx = min(max(ox + 2, cx - tw / 2), ox + W0 - tw - 2); ly = cy + r + 3
            if ly > oy + H0 - 18: ly = cy - r - 19
            while any(abs(ly - by) < 18 and lx < bx1 and lx + tw > bx0 for bx0, bx1, by in boxes): ly += 18
            ly = min(ly, oy + H0 - 18); boxes.append((lx, lx + tw, ly))
            dr.rectangle([lx - 2, ly - 1, lx + tw + 2, ly + 16], fill=(220, 20, 20, 225))
            dr.text((lx, ly), label, fill=(255, 255, 255), font=sm)
        return len(items)
    for p0 in range(0, K, ROWS):
        rows = list(range(p0, min(K, p0 + ROWS)))
        W, H = TW * 4 + HW, HEAD + len(rows) * (TH + LAB)
        P = Image.new('RGB', (W, H), (16, 16, 20))
        dr = ImageDraw.Draw(P, 'RGBA'); ncirc = 0
        page = p0 // ROWS + 1
        npages = (K + ROWS - 1) // ROWS
        dr.text((10, 9), f"{ctx}   {c['clip']}   page {page}/{npages}   ({c['length']} s, {K} frames)", fill=(255, 225, 140), font=big)
        for ri, k in enumerate(rows):
            y0 = HEAD + ri * (TH + LAB)
            t = c['length'] * k / max(1, K - 1) if c['length'] != '?' else float('nan')
            for vi, v in enumerate(VIEWS):
                x0 = vi * TW
                f = os.path.join(d, f'f{k:02d}_{v}.jpg')
                if os.path.exists(f):
                    P.paste(Image.open(f), (x0, y0 + LAB))
                if final: ncirc += red(dr, confirmed(k, v), x0, y0 + LAB, TW, TH)
                dr.text((x0 + 6, y0 + 2), f'frame {k}  t={t:.2f}s  {v}', fill=(210, 210, 220), font=sm)
                if not final:
                    for a in by_img.get(f'f{k:02d}_{v}', []):
                        cx, cy, r = x0 + a['x'], y0 + LAB + a['y'], a['r']
                        dr.ellipse([cx - r, cy - r, cx + r, cy + r], outline=COL[a['kind']] + (230,), width=2)
                        dr.text((cx + r * .7, cy - r - 4), TAG[a['kind']] + (a['what'].split()[-1] if a['kind'] == 'hand' else ''), fill=COL[a['kind']], font=sm)
            for hi, hn in enumerate(['handL', 'handR']):
                f = os.path.join(d, f'f{k:02d}_{hn}.jpg')
                if os.path.exists(f):
                    P.paste(Image.open(f), (TW * 4, y0 + LAB + hi * HH))
                if not final:
                    for a in by_img.get(f'f{k:02d}_{hn}', []):
                        cx, cy, r = TW * 4 + a['x'], y0 + LAB + hi * HH + a['y'], a['r']
                        dr.ellipse([cx - r, cy - r, cx + r, cy + r], outline=COL[a['kind']] + (230,), width=2)
                dr.text((TW * 4 + 6, y0 + LAB + hi * HH + 2), 'left hand' if hn == 'handL' else 'right hand', fill=(255, 255, 255), font=sm)
                if final: ncirc += red(dr, confirmed(k, hn), TW * 4, y0 + LAB + hi * HH, HW, HH)
            dr.text((TW * 4 + 6, y0 + 2), 'hands (close-up)', fill=(210, 210, 220), font=sm)
            dr.line([(0, y0), (W, y0)], fill=(60, 60, 70), width=1)
        for vi in range(1, 5):
            dr.line([(vi * TW, HEAD), (vi * TW, H)], fill=(60, 60, 70), width=1)
        key = f"{ctx}/{c['dir']}_p{page:02d}"
        if final:
            n = ncirc
            dr.text((W - 330, 11), f'{n} circle{"s" if n != 1 else ""} on this page' if n else 'nothing on this page', fill=(255, 120, 120) if n else (140, 230, 140), font=big)
        fn = os.path.join(out, f"{c['dir']}_p{page:02d}.jpg")
        P.save(fn, quality=88)
        made.append(fn)
print('PAGES', len(made), out)
