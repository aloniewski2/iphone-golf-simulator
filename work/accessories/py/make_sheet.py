"""Review sheet: the reference sheet on top, our renders in labelled rows below. Plain python3 + PIL.
python3 py/make_sheet.py out.png ref.jpg [--width 2400] [--cell 600] --row "Male|a.png,b.png,c.png,d.png" --row "Female|..."  [--title "..."]"""
import sys, os, argparse
from PIL import Image, ImageDraw, ImageFont

ap = argparse.ArgumentParser()
ap.add_argument('out'); ap.add_argument('ref', nargs='?')
ap.add_argument('--width', type=int, default=2400); ap.add_argument('--cell', type=int, default=600)
ap.add_argument('--row', action='append', default=[]); ap.add_argument('--title', default='')
ap.add_argument('--refcap', default='REFERENCE'); ap.add_argument('--crop', default='')
a = ap.parse_args()
try:
    F = ImageFont.truetype('/System/Library/Fonts/Helvetica.ttc', 26); Fs = ImageFont.truetype('/System/Library/Fonts/Helvetica.ttc', 20)
except Exception:
    F = Fs = None
BG = (28, 28, 30)
parts = []
if a.title:
    t = Image.new('RGB', (a.width, 56), BG); ImageDraw.Draw(t).text((16, 12), a.title, fill=(240, 232, 190), font=F); parts.append(t)
if a.ref:
    r = Image.open(a.ref).convert('RGB')
    if a.crop:
        x0, y0, x1, y1 = [float(v) for v in a.crop.split(',')]; r = r.crop((int(x0 * r.width), int(y0 * r.height), int(x1 * r.width), int(y1 * r.height)))
    r = r.resize((a.width, int(r.height * a.width / r.width)), Image.LANCZOS)
    cap = Image.new('RGB', (a.width, 34), BG); ImageDraw.Draw(cap).text((16, 6), a.refcap, fill=(150, 200, 255), font=Fs)
    parts += [cap, r]
for spec in a.row:
    label, _, files = spec.partition('|')
    ims = [Image.open(f).convert('RGB') for f in files.split(',') if f]
    n = len(ims); cw = a.width // n
    ch = max(int(im.height * cw / im.width) for im in ims)
    row = Image.new('RGB', (a.width, ch + 34), BG)
    ImageDraw.Draw(row).text((16, 6), label, fill=(240, 232, 190), font=Fs)
    for k, im in enumerate(ims):
        row.paste(im.resize((cw, int(im.height * cw / im.width)), Image.LANCZOS), (k * cw, 34))
    parts.append(row)
H = sum(p.height for p in parts)
sheet = Image.new('RGB', (a.width, H), BG); y = 0
for p in parts: sheet.paste(p, (0, y)); y += p.height
os.makedirs(os.path.dirname(os.path.abspath(a.out)), exist_ok=True)
sheet.save(a.out); print(a.out, sheet.size)
