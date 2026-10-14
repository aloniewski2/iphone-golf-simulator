"""montage.py out.png cols label_height img1 img2 ... (labels = file stems). Plain python3 + PIL."""
import sys, os
from PIL import Image, ImageDraw, ImageFont
out, cols = sys.argv[1], int(sys.argv[2]); paths = sys.argv[3:]
ims = [Image.open(p).convert('RGB') for p in paths]
w = max(i.width for i in ims); h = max(i.height for i in ims); lab = 22
rows = (len(ims) + cols - 1) // cols
sheet = Image.new('RGB', (cols * w, rows * (h + lab)), (28, 28, 30))
d = ImageDraw.Draw(sheet)
try: f = ImageFont.truetype('/System/Library/Fonts/Helvetica.ttc', 15)
except Exception: f = None
for k, (p, im) in enumerate(zip(paths, ims)):
    x, y = (k % cols) * w, (k // cols) * (h + lab)
    sheet.paste(im, (x, y + lab)); d.text((x + 6, y + 3), os.path.splitext(os.path.basename(p))[0], fill=(235, 220, 160), font=f)
sheet.save(out); print(out, sheet.size)
