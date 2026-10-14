"""Compose eval images into one review sheet (system python3 + Pillow)."""
import sys
from PIL import Image
W = '/Users/adnanyonathan/Documents/Codex/2026-09-20/wh/outputs/iphone-golf-simulator/work/jaw-sculpt-2026-10-12/eval/'
tag, sex = sys.argv[1], sys.argv[2]
ov = Image.open(f'{W}{tag}_ov_side_{sex}.png').convert('RGB')
crop = ov.crop((230, 450, 830, 1050)).resize((640, 640), Image.LANCZOS)
ovf = Image.open(f'{W}{tag}_ov_front_{sex}.png').convert('RGB').crop((250, 450, 950, 1150)).resize((640, 640), Image.LANCZOS)
ims = [crop, ovf] + [Image.open(f'{W}{tag}_{v}_{sex}.png').convert('RGB') for v in ('front', 'three_quarter', 'side')]
c = Image.new('RGB', (640 * 5, 640), (20, 20, 20))
for i, im in enumerate(ims): c.paste(im, (i * 640, 0))
c.save(f'{W}{tag}_sheet_{sex}.png')
