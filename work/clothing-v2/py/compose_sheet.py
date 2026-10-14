"""Before / current / NEW / reference sheet per hero + a band with each shoe alone next to the shoe reference (05).
python3 compose_sheet.py <hero> <old_sheet.jpg> <unity_dir_of_new_renders> <shoe_alone_dir> <05.jpg> <out.jpg> [footer text]
Layout (2850 px wide): title | label column + 3 rows (BEFORE, CURRENT, NEW) of 4 crops | reference panel (from the old sheet) | shoe band: 6 views of the new shoe alone + 05."""
import sys, os
from PIL import Image, ImageDraw, ImageFont

hero, old_p, ndir, adir, ref05_p, out_p = sys.argv[1:7]
foot = sys.argv[7] if len(sys.argv) > 7 else ''
old = Image.open(old_p).convert('RGB')
W = old.width; assert W == 2850, W
LAB_W, X0, X1 = 220, 220, 1570          # label column, crops block
ROW_H = 360; Y_B, Y_C = 57, 417
HDR = 57
FONT = None
for fp in ('/System/Library/Fonts/Supplemental/Arial Bold.ttf', '/System/Library/Fonts/Helvetica.ttc', '/Library/Fonts/Arial.ttf'):
    if os.path.exists(fp):
        try: FONT = lambda sz, fp=fp: ImageFont.truetype(fp, sz); FONT(10); break
        except Exception: FONT = None
if FONT is None: FONT = lambda sz: ImageFont.load_default()

title = old.crop((0, 0, W, HDR)); labels_b = old.crop((0, Y_B, LAB_W, Y_B + ROW_H)); labels_c = old.crop((0, Y_C, LAB_W, Y_C + ROW_H))
row_b = old.crop((X0, Y_B, X1, Y_B + ROW_H)); row_c = old.crop((X0, Y_C, X1, Y_C + ROW_H))
ref = old.crop((X1 + 3, HDR, W, 780))                                   # the reference panel (target + detail crops)
bg = row_b.getpixel((row_b.width - 6, 6))                               # studio grey of the old renders

# ---- NEW row: same four crops as the old rows, from the new Unity renders
cw = (X1 - X0) // 4
def fit_crop(im, w, h):
    """cover-fit centre crop"""
    s = max(w / im.width, h / im.height); im = im.resize((int(im.width * s + .5), int(im.height * s + .5)), Image.LANCZOS)
    l, t = (im.width - w) // 2, (im.height - h) // 2; return im.crop((l, t, l + w, t + h))
def full_body(im, w, h):
    s = h / im.height; im = im.resize((int(im.width * s + .5), h), Image.LANCZOS); c = Image.new('RGB', (w, h), bg); c.paste(im, ((w - im.width) // 2, 0)); return c
def L(name): return Image.open(os.path.join(ndir, hero, name)).convert('RGB')
row_n = Image.new('RGB', (X1 - X0, ROW_H), bg)
row_n.paste(full_body(L('rest_full_front_left.png'), cw, ROW_H), (0, 0))
row_n.paste(fit_crop(L('rest_top_front.png'), cw, ROW_H), (cw, 0))
row_n.paste(fit_crop(L('rest_bottom_front.png'), cw, ROW_H), (2 * cw, 0))
row_n.paste(fit_crop(L('rest_shoes_left.png'), X1 - X0 - 3 * cw, ROW_H), (3 * cw, 0))

# ---- shoe band
BAND_H = 600; BAND_HDR = 44
band = Image.new('RGB', (W, BAND_H + BAND_HDR), (28, 28, 34)); bd = ImageDraw.Draw(band)
bd.text((16, 10), 'SHOES ALONE (new, game maps, trim colour = example)', fill=(255, 210, 120), font=FONT(22))
bd.text((X1 + 20, 10), 'REFERENCE 05', fill=(130, 180, 255), font=FONT(22))
views = ['side', '34', 'top', 'bottom', 'back', 'front']
vw, vh = (X1 - 10) // 3, 290
for i, v in enumerate(views):
    im = Image.open(os.path.join(adir, '%s_%s.png' % (hero, v))).convert('RGB')
    im = fit_crop(im, vw - 6, vh - 6)
    band.paste(im, (6 + (i % 3) * vw, BAND_HDR + 6 + (i // 3) * (vh + 6)))
r5 = Image.open(ref05_p).convert('RGB')
golf = hero.startswith('Golf')
r5c = r5.crop((0, 372, 1280, 720)) if golf else r5.crop((0, 60, 1280, 360))
rw = W - X1 - 30; r5c = r5c.resize((rw, int(r5c.height * rw / r5c.width)), Image.LANCZOS)
band.paste(r5c, (X1 + 15, BAND_HDR + 4))
# the detail crop of the old reference (shoe in the target character) under it
sh = ref.crop((ref.width - 300, ref.height - 190, ref.width, ref.height - 20)) if False else None

# ---- compose
body_h = 3 * ROW_H
FOOT_H = 56
out = Image.new('RGB', (W, HDR + body_h + BAND_HDR + BAND_H + FOOT_H), (28, 28, 34))
out.paste(title, (0, 0))
def label(y, text, col):
    d = ImageDraw.Draw(out); d.rectangle((0, y, LAB_W, y + ROW_H), fill=(28, 28, 34)); d.text((10, y + ROW_H // 2 - 12), text, fill=col, font=FONT(26))
label(HDR, 'BEFORE (start)', (255, 200, 90)); label(HDR + ROW_H, 'CURRENT (V2 before)', (120, 230, 130)); label(HDR + 2 * ROW_H, 'NEW (this pass)', (255, 130, 220))
out.paste(row_b, (X0, HDR)); out.paste(row_c, (X0, HDR + ROW_H)); out.paste(row_n, (X0, HDR + 2 * ROW_H))
out.paste(ref, (X1 + 3, HDR))
# reference panel is 723 high, three rows are 1080: fill the rest of the right column with the reference's own shoe crop enlarged
rest_h = body_h - ref.height
if rest_h > 40:
    d = ImageDraw.Draw(out); d.rectangle((X1 + 3, HDR + ref.height, W, HDR + body_h), fill=(40, 40, 46))
    r5full = r5c.copy(); r5full = r5full.resize((rw, int(r5full.height * (rest_h - 20) / r5full.height)))
    out.paste(r5.resize((int((rest_h - 24) * 1280 / 720), rest_h - 24), Image.LANCZOS), (X1 + 3 + 12, HDR + ref.height + 12)) if False else None
out.paste(band, (0, HDR + body_h))
d = ImageDraw.Draw(out)
d.text((14, HDR + body_h + BAND_HDR + BAND_H + 14), foot, fill=(255, 130, 130), font=FONT(26))
d.text((14, 14), hero + ': BEFORE vs CURRENT vs NEW vs REFERENCE', fill=(255, 255, 255), font=FONT(30)) if False else None
out.save(out_p, quality=92)
print('SHEET', out_p, out.size)
