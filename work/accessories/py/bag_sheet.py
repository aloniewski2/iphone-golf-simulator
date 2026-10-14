"""BAG: compose review sheets with PIL (plain python3). Usage: python3 py/bag_sheet.py product | hero <Male|Female> | closeups | pose <Male|Female>"""
import sys, os
from PIL import Image, ImageDraw, ImageFont
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..'))
REF = os.path.abspath(os.path.join(ROOT, '..', '..', 'ArtDir', 'hero', 'targets-2026-10-13', 'accessories'))
try: F = ImageFont.truetype('/System/Library/Fonts/Helvetica.ttc', 34); F2 = ImageFont.truetype('/System/Library/Fonts/Helvetica.ttc', 26)
except Exception: F = F2 = None


def autocrop(im, pad=36, thr=14):
    bg = im.getpixel((2, 2))
    px = im.load(); w, h = im.size
    xs, ys = [], []
    small = im.resize((w // 4, h // 4))
    sp = small.load()
    x0, y0, x1, y1 = small.size[0], small.size[1], 0, 0
    for y in range(small.size[1]):
        for x in range(small.size[0]):
            p = sp[x, y]
            if abs(p[0] - bg[0]) + abs(p[1] - bg[1]) + abs(p[2] - bg[2]) > thr:
                x0 = min(x0, x); y0 = min(y0, y); x1 = max(x1, x); y1 = max(y1, y)
    if x1 <= x0: return im
    return im.crop((max(0, x0 * 4 - pad), max(0, y0 * 4 - pad), min(w, x1 * 4 + pad), min(h, y1 * 4 + pad)))


def cell(path, size, label=None, crop=True):
    im = Image.open(path).convert('RGB')
    if crop: im = autocrop(im)
    im.thumbnail(size, Image.LANCZOS)
    c = Image.new('RGB', size, im.getpixel((1, 1)))
    c.paste(im, ((size[0] - im.size[0]) // 2, (size[1] - im.size[1]) // 2 + (18 if label else 0)))
    if label:
        d = ImageDraw.Draw(c); d.rectangle([0, 0, size[0], 46], fill=(30, 36, 70)); d.text((14, 6), label, fill=(240, 240, 240), font=F2)
    return c


def grid(cells, cols, size, gap=6):
    rows = (len(cells) + cols - 1) // cols
    sheet = Image.new('RGB', (cols * size[0] + (cols + 1) * gap, rows * size[1] + (rows + 1) * gap), (20, 20, 22))
    for i, c in enumerate(cells):
        sheet.paste(c, (gap + (i % cols) * (size[0] + gap), gap + (i // cols) * (size[1] + gap)))
    return sheet


def with_ref(top_path, body, title):
    ref = Image.open(top_path).convert('RGB')
    w = body.size[0]
    ref = ref.resize((w, int(ref.size[1] * w / ref.size[0])), Image.LANCZOS)
    d = ImageDraw.Draw(ref); d.rectangle([0, 0, 520, 48], fill=(0, 0, 0)); d.text((12, 6), title, fill=(255, 255, 255), font=F2)
    out = Image.new('RGB', (w, ref.size[1] + body.size[1]), (20, 20, 22))
    out.paste(ref, (0, 0)); out.paste(body, (0, ref.size[1]))
    return out


def main():
    a = sys.argv[1:]
    P = os.path.join(ROOT, 'proof', 'bag', 'product')
    if a[0] == 'product':
        names = [('po_front.png', '1. FRONT (legs open)'), ('pf_side.png', '2. SIDE (legs folded)'), ('po_side.png', '3. SIDE (legs out, standing)'),
                 ('pf_back.png', '4. BACK (straps)'), ('pf_top.png', '5. TOP (dividers and grips)'), ('pf_34.png', '6. 3/4')]
        cells = [cell(os.path.join(P, n), (900, 1000), l) for n, l in names]
        body = grid(cells, 3, (900, 1000))
        body.save(os.path.join(ROOT, 'proof', 'bag', 'product_six_views.png'))
        out = with_ref(os.path.join(REF, '04_golf_bag_product.jpg'), body, 'REFERENCE (04_golf_bag_product)')
        out.save(os.path.join(ROOT, 'proof', 'bag', 'product_vs_reference.png'))
        print('wrote product sheets', out.size)
    elif a[0] == 'closeups':
        names = [('cf_front_pocket.png', 'front pocket + welted zip + vertical zip'), ('cf_34_side_pocket.png', 'side zip pocket, piped outline'), ('cf_34_covers.png', 'knit head covers, ribbed cuffs'),
                 ('cf_top_rim.png', 'top: rolled rim, 4-way divider, grips, irons'), ('cf_back34_rim.png', 'rim, handle, strap arch'), ('cf_back_legs.png', 'leg bracket + hinges + folded legs'),
                 ('cf_back34_strap.png', 'padded strap, webbing, back pocket'), ('cf_side_buckle.png', 'strap slider buckle + stitched webbing'), ('cf_34_base.png', 'navy boot + rubber base'),
                 ('co_back34_legs.png', 'legs open (back 3/4)'), ('cf_back_hub.png', 'hub patch + strap tabs'), ('cf_back34_handle.png', 'carry handle')]
        cells = [cell(os.path.join(P, n), (900, 760), l, crop=False) for n, l in names]
        grid(cells, 4, (900, 760)).save(os.path.join(ROOT, 'proof', 'bag', 'product_closeups.png'))
        print('wrote closeups')



def hero_sheets():
    refp = os.path.join(REF, '06_fit_golf_cap_glove_bag.jpg')
    rows = []
    for sex in ('Male', 'Female'):
        d = os.path.join(ROOT, 'proof', 'bag', f'hero_{sex}')
        names = [(f'h_front.png', 'front'), (f'h_34.png', '3/4'), (f'h_side.png', 'side'), (f'h_back34.png', 'back 3/4'), (f'h_back.png', 'back')]
        cells = [cell(os.path.join(d, n), (700, 860), f'{sex} {l}', crop=False) for n, l in names]
        rows.append(grid(cells, 5, (700, 860)))
        names2 = [('hc_front_shoulder.png', 'strap over the right shoulder'), ('hc_front34r_strap.png', 'strap, front 3/4 (hero right)'), ('hc_back34_bag.png', 'bag from behind 3/4'),
                  ('hc_back_head.png', 'back: head covers vs head'), ('h_sider.png', 'hero right side')]
        cells2 = [cell(os.path.join(d, n), (700, 860), f'{sex} {l}', crop=False) for n, l in names2]
        g2 = grid(cells2, 5, (700, 860))
        out = Image.new('RGB', (rows[-1].size[0], rows[-1].size[1] + g2.size[1]), (20, 20, 22)); out.paste(rows[-1], (0, 0)); out.paste(g2, (0, rows[-1].size[1]))
        with_ref(refp, out, 'REFERENCE (06_fit: only WHERE it sits)').save(os.path.join(ROOT, 'proof', 'bag', f'hero_{sex}_vs_reference.png'))
    print('wrote hero sheets')


def pose_sheet(sex):
    import json
    res = json.load(open(os.path.join(ROOT, 'out', sex, 'golf_bag', 'pose_check.json')))
    d = os.path.join(ROOT, 'proof', 'bag', f'pose_{sex}')
    cells = []
    for key, r in res.items():
        if not (r['hero'].startswith('GolfKitHero') and r['critical']): continue
        for fr, what in ((r['strap_frame'], 'worst strap'), (r['bag_frame'], 'worst bag')):
            if what == 'worst bag' and fr == r['strap_frame']: continue
            lab = f"{sex} {r['clip']} f{fr} {what}: bag {r['bag_min_mm']} / strap {r['strap_min_mm']} mm"
            for v in ('back34', 'front34r'):
                p = os.path.join(d, f"pw_{r['clip']}_{fr}_{v}.png")
                if os.path.exists(p): cells.append(cell(p, (600, 760), lab if v == 'back34' else lab.split(':')[0], crop=False))
    if cells:
        grid(cells, 4, (600, 760)).save(os.path.join(ROOT, 'proof', 'bag', f'pose_{sex}_worst_frames.png')); print('wrote pose sheet', sex, len(cells))


if __name__ == '__main__':
    if len(sys.argv) > 1 and sys.argv[1] == 'hero': hero_sheets()
    elif len(sys.argv) > 1 and sys.argv[1] == 'pose': pose_sheet(sys.argv[2])
    else: main()
