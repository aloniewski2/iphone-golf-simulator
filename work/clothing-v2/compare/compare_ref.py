"""Current male hero vs the clothing-v2 target 01: the same four views side by side at the same body height, with garment landmarks
measured on both (colour segmentation) as a fraction of the height head-top -> floor.
python3 work/clothing-v2/compare/compare_ref.py <turnaround dir> <out dir>"""
import sys, os, json
import numpy as np
from PIL import Image, ImageDraw, ImageFont

R = os.path.abspath(os.path.join(os.path.dirname(__file__), '../../..'))
TARGET = os.path.join(R, 'ArtDir/hero/targets-2026-10-12/clothing-v2/01_tennis_male_target.jpg')
src, out = sys.argv[1], sys.argv[2]
os.makedirs(out, exist_ok=True)
VIEWS = [('front', 'rest_full_front', (0, 350)), ('3/4', 'rest_full_front_right', (350, 700)),
         ('side', 'rest_full_left', (700, 1050)), ('back', 'rest_full_back', (1050, 1400))]
try: FONT = ImageFont.truetype('/System/Library/Fonts/Helvetica.ttc', 22); SMALL = ImageFont.truetype('/System/Library/Fonts/Helvetica.ttc', 17)
except Exception: FONT = SMALL = None


def classes(rgb):
    """per pixel: 0 background, 1 skin, 2 light cloth (shirt / socks / shoe upper), 3 navy, 4 other"""
    f = rgb.astype(np.float32)
    r, g, b = f[..., 0], f[..., 1], f[..., 2]
    mx, mn = f.max(-1), f.min(-1)
    sat = (mx - mn) / np.maximum(mx, 1)
    c = np.full(r.shape, 4, np.uint8)
    c[(sat < .10) & (mx < 185)] = 0                                    # grey studio background + floor shadow
    c[(r > g + 12) & (g > b + 8) & (sat > .22) & (mx > 90)] = 1        # skin (orange-tan)
    c[(mx > 185) & (sat < .16)] = 2                                    # off-white / white
    c[(b > r + 18) & (mx < 140)] = 3                                   # navy
    return c


def landmarks(rgb):
    c = classes(rgb)
    fg = c != 0
    rows = np.where(fg.sum(1) > 3)[0]
    top, floor = rows[0], rows[-1]
    H = floor - top
    cnt = lambda k: (c == k).sum(1)
    navy, light, skin = cnt(3), cnt(2), cnt(1)
    # shorts = the biggest run of rows with a lot of navy in the middle half of the body
    mid = np.zeros_like(navy, bool); mid[int(top + .35 * H):int(top + .75 * H)] = True
    nv = (navy > 12) & mid
    runs, start = [], None
    for y in range(len(nv)):
        if nv[y] and start is None: start = y
        if (not nv[y] or y == len(nv) - 1) and start is not None: runs.append((start, y)); start = None
    s0, s1 = max(runs, key=lambda t: t[1] - t[0])
    # shirt = light rows above the shorts (below the head), collar = first light row
    sh = np.where((light > 6) & (np.arange(len(light)) < s0 + 5) & (np.arange(len(light)) > top + .08 * H))[0]
    collar, hem = sh[0], max(sh[-1], s0)
    # socks = first light row below the shorts and knees; shoes = navy/light below that
    below = np.where((light > 4) & (np.arange(len(light)) > s1 + .08 * H))[0]
    sock = below[0] if len(below) else floor
    # widths (px) at a few heights
    def width(y0, y1, mask):
        w = [np.ptp(np.where(mask[y])[0]) if mask[y].any() else 0 for y in range(int(y0), int(y1))]
        return float(np.median(w)) if w else 0.
    shirt_w = width(collar + .30 * (hem - collar), collar + .45 * (hem - collar), c == 2)
    shorts_w = width(s0 + .15 * (s1 - s0), s0 + .35 * (s1 - s0), c == 3)
    head = fg & (np.arange(fg.shape[0])[:, None] < collar)
    head_w = width(top + .25 * (collar - top), top + .55 * (collar - top), head)
    f = lambda y: round(float((y - top) / H), 3)
    return dict(top=int(top), floor=int(floor), H=int(H), collar=f(collar), shirt_hem=f(hem), shorts_top=f(s0), shorts_hem=f(s1), sock_top=f(sock),
                head=f(collar), shirt_w=round(shirt_w / H, 3), shorts_w=round(shorts_w / H, 3), head_w=round(head_w / H, 3)), c


tgt = np.array(Image.open(TARGET).convert('RGB'))
tgt_top = tgt[:530]
rows, table = [], {}
PH, CW = 760, 600                          # common body height (px) and panel width in the sheet
for label, ours_name, (x0, x1) in VIEWS:
    t = tgt_top[:, x0:x1]
    o = np.array(Image.open(os.path.join(src, ours_name + '.png')).convert('RGB'))
    lt, ct = landmarks(t); lo, co = landmarks(o)
    table[label] = {'target': lt, 'ours': lo}
    Image.fromarray((ct * 60).astype(np.uint8)).save(os.path.join(out, f'mask_target_{label.replace("/", "")}.png'))
    Image.fromarray((co * 60).astype(np.uint8)).save(os.path.join(out, f'mask_ours_{label.replace("/", "")}.png'))
    pair = []
    for img, lm in ((t, lt), (o, lo)):
        s_ = PH / lm['H']
        im = Image.fromarray(img).resize((round(img.shape[1] * s_), round(img.shape[0] * s_)), Image.LANCZOS)
        cls = classes(np.array(im)); xs = np.where((cls != 0).any(0))[0]; cx = (xs[0] + xs[-1]) // 2
        bg = tuple(int(v) for v in np.median(np.array(im)[-8:, :8].reshape(-1, 3), 0))
        canvas = Image.new('RGB', (CW, PH + 60), bg)
        canvas.paste(im, (CW // 2 - cx, int(30 - lm['top'] * s_)))
        d = ImageDraw.Draw(canvas)
        for k, col in (('collar', (255, 210, 60)), ('shirt_hem', (255, 120, 60)), ('shorts_hem', (90, 200, 255)), ('sock_top', (160, 255, 120))):
            y = 30 + lm[k] * PH
            d.line([(0, y), (CW, y)], fill=col, width=2)
        pair.append(canvas)
    rows.append((label, pair))

# views sheet: two rows (front, 3/4 | side, back), each view = TARGET | OURS at the same body height
W = 2 * CW + 8
RH = PH + 60 + 40
sheet = Image.new('RGB', (2 * W + 30, 2 * RH + 40), (28, 28, 32))
d = ImageDraw.Draw(sheet)
for i, (label, (a, b)) in enumerate(rows):
    x, y = (i % 2) * (W + 30), (i // 2) * RH
    d.text((x + 8, y + 8), f'{label}: TARGET 01', fill=(250, 215, 140), font=FONT)
    d.text((x + CW + 16, y + 8), f'{label}: OURS (Unity, now)', fill=(140, 215, 250), font=FONT)
    sheet.paste(a, (x, y + 40)); sheet.paste(b, (x + CW + 8, y + 40))
d.text((8, 2 * RH + 10), 'Same body height (head top to floor). Lines measured on each image: yellow collar, orange shirt hem, blue shorts hem, green sock top. Shorts, socks, shoes are still the old ones.', fill=(220, 220, 220), font=SMALL)
sheet.save(os.path.join(out, 'compare_views.png'))

# details sheet: the target's five close-ups next to the same detail on ours
TT = [(22, 285), (298, 551), (565, 826), (840, 1097), (1110, 1378)]
ld = table['side']['ours']
hy = ld['top'] + ld['shorts_hem'] * ld['H']
OC = [('rest_top_front', (290, 70, 790, 490), 'collar + placket'), ('rest_top_front', (700, 170, 1080, 490), 'sleeve + cuff'),
      ('rest_bottom_front', (170, 230, 910, 850), 'shorts front (old)'), ('rest_full_left', (430, int(hy - .10 * ld['H']), 720, int(hy + .03 * ld['H'])), 'shorts hem (old)'),
      ('rest_shoes_right', (60, 120, 1050, 980), 'shoe (old)')]
TH = 300
cells = []
for (tx0, tx1), (name, box, lab) in zip(TT, OC):
    a = Image.fromarray(tgt[545:765, tx0:tx1]); b = Image.open(os.path.join(src, name + '.png')).convert('RGB').crop(box)
    a = a.resize((round(a.width * TH / a.height), TH), Image.LANCZOS); b = b.resize((round(b.width * TH / b.height), TH), Image.LANCZOS)
    cells.append((lab, a, b))
ws = [max(a.width, b.width) for _, a, b in cells]; xs = np.cumsum([0] + [w + 16 for w in ws])
det = Image.new('RGB', (int(xs[-1]), 2 * TH + 110), (28, 28, 32)); d = ImageDraw.Draw(det)
for i, (lab, a, b) in enumerate(cells):
    x = int(xs[i])
    d.text((x + 4, 6), 'TARGET 01', fill=(250, 215, 140), font=SMALL); det.paste(a, (x, 30))
    d.text((x + 4, TH + 40), 'OURS: ' + lab, fill=(140, 215, 250), font=SMALL); det.paste(b, (x, TH + 64))
det.save(os.path.join(out, 'compare_details.png'))
json.dump(table, open(os.path.join(out, 'landmarks.json'), 'w'), indent=1)
for k in ('collar', 'shirt_hem', 'shorts_hem', 'sock_top', 'head_w', 'shirt_w', 'shorts_w'):
    print('%-10s' % k, '  '.join('%s %.3f/%.3f' % (v, table[v]['target'][k], table[v]['ours'][k]) for v in table))
