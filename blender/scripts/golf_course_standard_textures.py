"""Deterministic, seamless specialty surfaces for the shared golf course catalog.

Run with Python and numpy/Pillow. Existing postcard textures are reused unchanged.
"""
from pathlib import Path
import argparse
import hashlib
import json
import numpy as np
from PIL import Image


def generate(output):
    output.mkdir(parents=True, exist_ok=True)
    n = 512
    y, x = np.mgrid[0:n, 0:n] / n
    rng = np.random.default_rng(48123)

    def noise(seed, octaves=5):
        r = np.random.default_rng(seed)
        value = np.zeros((n, n))
        weight = 0
        for octave in range(octaves):
            frequency = 2 ** octave
            for _ in range(5):
                a, b = r.integers(-frequency, frequency + 1, size=2)
                if a == b == 0:
                    a = 1
                amplitude = 1 / (1.7 ** octave)
                value += np.sin(2 * np.pi * (a * x + b * y) + r.random() * 2 * np.pi) * amplitude
                weight += amplitude
        return value / weight

    broad = noise(29)
    fine = noise(312, 8)
    grain = rng.uniform(-1, 1, (n, n))
    manifest = {}

    def write(name, base, height, variation, strength=2.0):
        base = np.asarray(base) / 255.0
        color = np.clip(base[None, None, :] * variation[:, :, None], 0, 1)
        # Central differences wrap, so both the albedo and normal tile continuously.
        dx = (np.roll(height, -1, axis=1) - np.roll(height, 1, axis=1)) * strength
        dy = (np.roll(height, -1, axis=0) - np.roll(height, 1, axis=0)) * strength
        normal = np.stack((-dx, dy, np.ones_like(dx)), axis=-1)
        normal /= np.linalg.norm(normal, axis=-1, keepdims=True)
        for suffix, pixels in [('C', color), ('N', normal * .5 + .5)]:
            path = output / f'{name}_{suffix}.png'
            Image.fromarray(np.uint8(np.round(pixels * 255))).save(path)
            manifest[path.name] = {'size': [n, n], 'sha256': hashlib.sha256(path.read_bytes()).hexdigest()}

    write('Snow', [226, 237, 243], broad * .4 + grain * .06, 1 + broad * .12 + grain * .025, 1.8)
    cracks = np.exp(-np.abs(np.sin(2*np.pi*(x*3+y*2)+broad*3))*28)
    cross = np.exp(-np.abs(np.sin(2*np.pi*(x*2-y*5)+broad*2))*35)
    ice = np.maximum(cracks*.6, cross*.35)
    write('Ice', [139, 188, 210], ice*.3+broad*.3, .93 + broad*.2+ice*.16, 1.5)
    write('Ash', [67, 67, 70], broad*.3+fine+grain*.10, 1+broad*.35+grain*.10)
    strata = np.sin(2*np.pi*y*14 + np.sin(2*np.pi*x)*.7+fine*.4)
    write('Sandstone', [208, 190, 159], strata*.12+fine*.5, 1+strata*.065+broad*.22+grain*.025)
    ripple = np.sin(2*np.pi*(x*8+y*3)+broad*4)
    write('Desert', [202, 154, 105], ripple*.12+grain*.05, 1+broad*.17+ripple*.035+grain*.025)
    grain_lines = np.sin(2*np.pi*x*42 + np.sin(2*np.pi*y*2)*1.3 + broad*6)
    plank = np.exp(-np.abs(np.sin(2*np.pi*x*4))*45)
    write('Wood', [214, 207, 188], grain_lines*.12-plank*.3, 1+broad*.14+grain_lines*.05-plank*.2)
    bark = np.abs(np.sin(2*np.pi*x*18+np.sin(2*np.pi*y*3)+broad*8))
    write('Bark', [196, 184, 159], bark*.7+fine*.5, .87+bark*.2+broad*.2)
    leaf = np.sin(2*np.pi*x*19+np.sin(2*np.pi*y*17))*np.sin(2*np.pi*y*13)
    write('Leaves', [218, 229, 208], leaf*.14+fine*.4, 1+leaf*.055+broad*.16)
    return manifest


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--out', type=Path, required=True)
    parser.add_argument('--manifest', type=Path)
    args = parser.parse_args()
    data = generate(args.out)
    if args.manifest:
        args.manifest.parent.mkdir(parents=True, exist_ok=True)
        args.manifest.write_text(json.dumps(data, indent=2)+'\n')
    print(f'Generated {len(data)} specialty maps; original postcard maps unchanged.')
