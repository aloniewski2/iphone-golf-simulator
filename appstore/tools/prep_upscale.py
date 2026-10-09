"""Pre-upscale the small native captures once (Lanczos + light unsharp) so layouts stay crisp."""
from pathlib import Path
from PIL import Image, ImageFilter
ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / "build" / "up"; OUT.mkdir(parents=True, exist_ok=True)

def up(src, scale, name=None):
    im = Image.open(src).convert("RGB")
    im = im.resize((im.width * scale, im.height * scale), Image.LANCZOS)
    im = im.filter(ImageFilter.UnsharpMask(radius=1.6, percent=70, threshold=2))
    im.save(OUT / (name or Path(src).stem + ".png"))

for f in (ROOT / "src/phone").glob("*.png"): up(f, 3)
up(ROOT / "src/phone/feedback-composer.jpg", 3, "feedback-composer.png")
for f in (ROOT / "src/frames").glob("*_0?.png"):
    if f.stem.split("_")[0] in ("cliff", "sky", "tgame", "golf1"): up(f, 2)
print(len(list(OUT.iterdir())), "upscaled")
