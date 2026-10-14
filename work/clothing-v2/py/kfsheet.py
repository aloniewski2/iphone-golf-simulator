import sys, glob
from PIL import Image
out, pat = sys.argv[1], sys.argv[2]
files = sorted(glob.glob(pat), key=lambda f: (f.split('_')[-3], int(f.split('_')[-2])))
ims = [Image.open(f).crop((150, 80, 750, 1150)).resize((300, 535)) for f in files]
cols = 9; rows = (len(ims) + cols - 1) // cols
s = Image.new('RGB', (cols * 300, rows * 535), (120, 120, 122))
for i, im in enumerate(ims): s.paste(im, ((i % cols) * 300, (i // cols) * 535))
s.save(out); print(len(ims))
