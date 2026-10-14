"""PNG writer for Blender's python (no PIL there). 8-bit RGB / RGBA, rows top to bottom."""
import zlib, struct
import numpy as np


def write_png(path, img):
    img = np.ascontiguousarray(img, np.uint8)
    h, w = img.shape[:2]; c = 1 if img.ndim == 2 else img.shape[2]
    ct = {1: 0, 3: 2, 4: 6}[c]
    raw = b''.join(b'\x00' + img[y].tobytes() for y in range(h))
    def chunk(t, d): return struct.pack('>I', len(d)) + t + d + struct.pack('>I', zlib.crc32(t + d) & 0xffffffff)
    with open(path, 'wb') as f:
        f.write(b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', w, h, 8, ct, 0, 0, 0)) + chunk(b'IDAT', zlib.compress(raw, 6)) + chunk(b'IEND', b''))
