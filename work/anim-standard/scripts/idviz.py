"""idviz.py <clipdir> <frame> [views] - the render next to the same render with every 'inside showing' pixel in magenta (debug)."""
import sys
import numpy as np
from PIL import Image
d, f = sys.argv[1], int(sys.argv[2]); views = (sys.argv[3] if len(sys.argv) > 3 else 'front,back,left,right').split(',')
rows = []
for v in views:
    a = np.array(Image.open(f'{d}/f{f:02d}_{v}_id.png').convert('RGB')).astype(int)
    R, G, B = a[..., 0], a[..., 1], a[..., 2]
    idm = np.rint(R / 20).astype(int); front = G > 200; back = (G > 100) & (G < 165)
    ins = ((idm >= 2) & (idm <= 8) & ((front & (B > 128)) | (back & (B < 128)))) | ((idm == 1) & back & (B < 128))
    rgb = np.array(Image.open(f'{d}/f{f:02d}_{v}.jpg').convert('RGB')); m = rgb.copy(); m[ins] = [255, 0, 255]
    rows.append(np.concatenate([rgb, m], 1))
Image.fromarray(np.concatenate(rows, 0) if len(rows) < 3 else np.concatenate([np.concatenate(rows[:2], 1), np.concatenate(rows[2:], 1)], 0)).save(sys.argv[4] if len(sys.argv) > 4 else '/tmp/idviz.png')
