"""Shared helpers: render an HTML snippet to a PNG with headless Chromium.

Pages live in appstore/build/ (git-ignored) and reference ../src/... assets, which
tools/fetch_assets.sh pulls from the final-build branch.
"""
import os
import subprocess
import glob
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
BUILD = ROOT / "build"
BUILD.mkdir(exist_ok=True)

CHROME = next(iter(glob.glob("/opt/pw-browsers/chromium-*/chrome-linux/chrome")), "chromium")

# Motion Club brand tokens (from GolfArcade/Unity/ClubDesign.swift on final-build)
PALETTE = dict(
    lagoon="#10243D", lagoon_deep="#0A192D", violet="#5B2BD9", coral="#FF7556",
    green="#35B87E", sun="#D7F044", sun_deep="#ACCA23", cream="#FFF9EE",
    cream_deep="#E5DDCC", ink="#10243D", muted="#526B7E", sky="#35B8C7",
)

FONTS_CSS = """
@font-face{font-family:'Bricolage';src:url('../src/fonts/Bricolage.ttf');font-weight:200 800;font-stretch:75% 100%}
@font-face{font-family:'Rubik';src:url('../src/fonts/Rubik.ttf');font-weight:300 900}
*{box-sizing:border-box;margin:0;padding:0}
html,body{overflow:hidden}
body{font-family:'Rubik',sans-serif}
.display{font-family:'Bricolage','Rubik',sans-serif;font-weight:800}
/* the app sets its display face to wght 800, wdth 78, opsz = size (ClubDesign.swift) */
/* NOTE: headlines now use Rubik (the in-game HUD face); Bricolage is kept only as .display for menu-title looks */
.display{font-stretch:78%;font-optical-sizing:auto}
"""


def page(body: str, css: str = "", w: int = 1024, h: int = 1024) -> str:
    return f"""<!doctype html><html><head><meta charset="utf-8"><style>{FONTS_CSS}
:root{{{''.join(f'--{k.replace("_","-")}:{v};' for k, v in PALETTE.items())}}}
body{{width:{w}px;height:{h}px;position:relative}}
{css}</style></head><body>{body}</body></html>"""


def render(html: str, name: str, w: int, h: int) -> Path:
    """Write build/<name>.html and screenshot it to build/<name>.png at exactly w x h."""
    src = BUILD / f"{name}.html"
    out = BUILD / f"{name}.png"
    src.write_text(html, encoding="utf-8")
    subprocess.run(
        [CHROME, "--headless", "--no-sandbox", "--disable-gpu", "--hide-scrollbars",
         "--force-device-scale-factor=1", "--allow-file-access-from-files",
         "--default-background-color=00000000",
         f"--window-size={w},{h + 200}", f"--screenshot={out}", f"file://{src}"],
        check=True, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, timeout=120)
    # headless reserves part of the window, so shoot taller and crop to the exact size
    from PIL import Image
    im = Image.open(out)
    im.crop((0, 0, w, h)).save(out)
    return out
