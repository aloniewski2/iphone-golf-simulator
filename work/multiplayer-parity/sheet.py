import sys, subprocess, os
from PIL import Image, ImageDraw
# sheet.py video out.jpg cols t1 t2 ...
v, out, cols = sys.argv[1], sys.argv[2], int(sys.argv[3]); ts = sys.argv[4:]
tmp = out + ".d"; os.makedirs(tmp, exist_ok=True)
subprocess.run([os.path.dirname(__file__) + "/frames", v, tmp] + ts, check=True, capture_output=True)
w, h = 640, 360; rows = (len(ts) + cols - 1) // cols
S = Image.new('RGB', (w * cols, h * rows))
for i, t in enumerate(ts):
    p = f"{tmp}/f_{t}.jpg"
    if not os.path.exists(p): continue
    im = Image.open(p).resize((w, h)); d = ImageDraw.Draw(im); d.rectangle((0, 0, 70, 22), fill=(0, 0, 0)); d.text((4, 4), f"{float(t):.1f}s", fill=(255, 255, 0))
    S.paste(im, ((i % cols) * w, (i // cols) * h))
S.save(out, quality=82)
