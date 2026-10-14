"""compose preview renders into one sheet: python3 sheet.py out.png scale img1 img2 ..."""
import sys
from PIL import Image
out, scale = sys.argv[1], float(sys.argv[2])
ims = [Image.open(f) for f in sys.argv[3:]]
W = sum(i.width for i in ims); H = max(i.height for i in ims)
s = Image.new('RGB', (W, H), (120, 120, 122)); x = 0
for i in ims: s.paste(i, (x, 0)); x += i.width
s.resize((int(W * scale), int(H * scale))).save(out)
