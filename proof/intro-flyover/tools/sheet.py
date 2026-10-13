"""sheet.py <framesDir> <out.jpg> [every=10] [cols=6] — contact sheet with frame number + seconds (20 fps) stamped."""
import sys, os
from PIL import Image, ImageDraw
d, out = sys.argv[1], sys.argv[2]
every = int(sys.argv[3]) if len(sys.argv) > 3 else 10
cols = int(sys.argv[4]) if len(sys.argv) > 4 else 6
files = sorted(f for f in os.listdir(d) if f.endswith('.jpg'))[::every]
tw, th = 320, 180
rows = (len(files) + cols - 1) // cols
sheet = Image.new('RGB', (cols * tw, rows * th), (0, 0, 0))
for i, f in enumerate(files):
    im = Image.open(os.path.join(d, f)).convert('RGB').resize((tw, th))
    n = int(f[1:5]); dr = ImageDraw.Draw(im)
    dr.rectangle((0, 0, 86, 16), fill=(0, 0, 0)); dr.text((3, 2), f"{n} {n/20:.2f}s", fill=(255, 255, 0))
    sheet.paste(im, ((i % cols) * tw, (i // cols) * th))
sheet.save(out, quality=85)
print(out, len(files), 'tiles')
