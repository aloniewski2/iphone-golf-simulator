import sys
from PIL import Image
# montage.py out.png cols file...
out, cols = sys.argv[1], int(sys.argv[2]); fs = sys.argv[3:]
ims = [Image.open(f).convert('RGB') for f in fs]; w, h = ims[0].size; rows = (len(ims) + cols - 1) // cols
M = Image.new('RGB', (w * cols, h * rows)); [M.paste(im, ((i % cols) * w, (i // cols) * h)) for i, im in enumerate(ims)]
M.save(out)
