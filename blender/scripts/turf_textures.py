"""Turf detail textures for the course: seamless grey tiles the game multiplies into each
surface's own colour, so the palette in HoleView stays the look and these add the grain.

    Blender -b --python blender/scripts/turf_textures.py

Writes Unity/Assets/Resources/Course/Turf/{fairway,green,rough,sand}.png (1024², tiling in both
directions) and blender/previews/turf.png (the four side by side). Built with Blender's own numpy
from tileable fractal noise — every octave's lattice wraps at the tile's edge — so there is no seam
however far the tile repeats:

  fairway  fine blade grain over soft clumps (Hole 7's fairway has its own mown bands as geometry,
           so none here), one tile per 10 yd;
  green    two mowing stripes per tile, light and dark, over a very fine, even grain — a green
           reads as cut and quick — one tile per 4 yd, so 2-yd stripes;
  rough    longer, clumpier grass with dark tufts, one tile per 6 yd;
  sand     fine speckle with a faint raked ripple, one tile per 3 yd.
"""
import bpy, os
import numpy as np

REPO = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
OUT = os.path.join(REPO, "Unity", "Assets", "Resources", "Course", "Turf")
PREVIEW = os.path.join(REPO, "blender", "previews", "turf.png")
N = 1024
rng = np.random.default_rng(12)


def lattice(freq):
    """Value noise on a freq×freq lattice that wraps, sampled smoothly over the N×N tile."""
    grid = rng.random((freq, freq))
    t = np.arange(N) * freq / N
    i0 = np.floor(t).astype(int); f = t - i0
    f = f * f * f * (f * (f * 6 - 15) + 10)                    # quintic fade
    i1 = (i0 + 1) % freq; i0 %= freq
    a = grid[np.ix_(i0, i0)]; b = grid[np.ix_(i0, i1)]
    c = grid[np.ix_(i1, i0)]; d = grid[np.ix_(i1, i1)]
    fy, fx = f[:, None], f[None, :]
    return (a * (1 - fx) + b * fx) * (1 - fy) + (c * (1 - fx) + d * fx) * fy


def fbm(freqs, gain=0.55):
    total, amp, norm = np.zeros((N, N)), 1.0, 0.0
    for fq in freqs:
        total += amp * lattice(fq); norm += amp; amp *= gain
    return total / norm


def stretch(img, along=4):
    """Blade grain: noise smeared along one axis (a wrapping blur, so the tile still tiles) so it
    reads as grass lying one way."""
    smear = sum(np.roll(img, k, axis=1) for k in range(-along * 2, along * 2 + 1)) / (4 * along + 1)
    return 0.45 * img + 0.55 * normalise_to(smear, img)


def normalise_to(img, like):
    return (img - img.mean()) / (img.std() + 1e-6) * like.std() + like.mean()


def normalise(img, spread):
    img = (img - img.mean()) / (img.std() + 1e-6)
    return np.clip(0.5 + img * spread, 0, 1)


u = np.arange(N)[None, :] / N
v = np.arange(N)[:, None] / N

fairway = normalise(0.65 * stretch(fbm([64, 128, 256])) + 0.35 * fbm([4, 8, 16]), 0.16)
stripes = np.sin(2 * np.pi * 2 * v) * np.ones((1, N))
stripes = np.tanh(stripes * 3) / np.tanh(3)                     # soft-edged bands
green = normalise(0.25 * fbm([128, 256]) + 0.08 * fbm([8, 16]), 0.07) + 0.075 * stripes
green = np.clip(green, 0, 1)
tufts = np.clip((fbm([16, 32, 64]) - 0.56) * 6, 0, 1)
rough = normalise(0.5 * stretch(fbm([32, 64, 128]), 3) + 0.5 * fbm([4, 8, 16]), 0.2) - 0.18 * tufts
rough = np.clip(rough, 0, 1)
ripple = np.sin(2 * np.pi * (6 * v + 0.6 * fbm([4, 8]))) * 0.5
sand = normalise(fbm([128, 256, 512 if N >= 1024 else 256], 0.7), 0.12) + 0.03 * ripple
sand = np.clip(sand, 0, 1)


def save(name, img):
    path = os.path.join(OUT, name + ".png")
    image = bpy.data.images.new(name, N, N, alpha=False)
    rgba = np.ones((N, N, 4), dtype=np.float32)
    rgba[..., 0] = rgba[..., 1] = rgba[..., 2] = img[::-1].astype(np.float32)   # Blender rows run bottom-up
    image.pixels.foreach_set(rgba.ravel())
    image.filepath_raw = path; image.file_format = 'PNG'
    image.save()
    print(f"wrote {path}  mean {img.mean():.3f}  std {img.std():.3f}")


os.makedirs(OUT, exist_ok=True)
tiles = {"fairway": fairway, "green": green, "rough": rough, "sand": sand}
for name, img in tiles.items(): save(name, img)
# preview: the four at quarter size, side by side
small = [img[::4, ::4] for img in tiles.values()]
save_preview = np.concatenate(small, axis=1)
prev = bpy.data.images.new("turf_preview", save_preview.shape[1], save_preview.shape[0], alpha=False)
rgba = np.ones((*save_preview.shape, 4), dtype=np.float32)
rgba[..., 0] = rgba[..., 1] = rgba[..., 2] = save_preview[::-1]
prev.pixels.foreach_set(rgba.ravel())
os.makedirs(os.path.dirname(PREVIEW), exist_ok=True)
prev.filepath_raw = PREVIEW; prev.file_format = 'PNG'; prev.save()
print("preview", PREVIEW)
