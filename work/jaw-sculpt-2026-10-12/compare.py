"""Before/now review strip: side overlay zoom + shaded before/now (three-quarter, side, front). python3 compare.py <before_tag> <now_tag> <sex>"""
import sys
from PIL import Image, ImageDraw
W = '/Users/adnanyonathan/Documents/Codex/2026-09-20/wh/outputs/iphone-golf-simulator/work/jaw-sculpt-2026-10-12/eval/'
b, n, sex = sys.argv[1:4]
ovbox = (330, 700, 830, 1100) if sex == 'Female' else (200, 650, 700, 1050)
tiles = [('overlay: target green / before yellow / now red', Image.open(f'{W}{n}_ov_side_{sex}.png').convert('RGB').crop(ovbox).resize((600, 480)))]
for v, box in (('three_quarter', (60, 180, 600, 612)), ('side', (40, 180, 580, 612)), ('front', (50, 180, 590, 612))):
    for tag, lab in ((b, 'before'), (n, 'now')):
        tiles.append((f'{lab} {v}', Image.open(f'{W}{tag}_{v}_{sex}.png').convert('RGB').crop(box).resize((600, 480))))
cols = 4
rows = (len(tiles) + cols - 1) // cols
s = Image.new('RGB', (cols * 600, rows * 510), (20, 20, 20)); d = ImageDraw.Draw(s)
for i, (lab, im) in enumerate(tiles):
    x, y = (i % cols) * 600, (i // cols) * 510
    s.paste(im, (x, y + 30)); d.text((x + 8, y + 8), lab, fill=(230, 230, 230))
s.save(f'{W}cmp_{b}_{n}_{sex}.png')
