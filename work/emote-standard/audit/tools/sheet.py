import sys, glob, os
from PIL import Image
# sheet.py out.jpg first last [cols] [tile]
out, a, b = sys.argv[1], int(sys.argv[2]), int(sys.argv[3]); cols = int(sys.argv[4]) if len(sys.argv) > 4 else 4; T = int(sys.argv[5]) if len(sys.argv) > 5 else 400
fs = sorted(glob.glob('CLIPPING_AUDIT_images/[0-9]*.jpg'))
fs = [f for f in fs if a <= int(os.path.basename(f)[:3]) <= b]
rows = (len(fs) + cols - 1) // cols
P = Image.new('RGB', (cols * T, rows * T))
for i, f in enumerate(fs): P.paste(Image.open(f).resize((T, T)), ((i % cols) * T, (i // cols) * T))
P.save(out, quality=82)
