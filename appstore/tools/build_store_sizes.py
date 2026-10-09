"""Exact-size store artwork (Set C, icon 04):
  search results : 5244x2950 (16:9), 3840x2560 (3:2), 1920x1280 (3:2)
  header         : 5244x2950 (16:9), 3840x1646 (wide banner)
Layouts are authored in 1920-unit-wide "stages" and scaled to the target pixel size.
"""
from PIL import Image
from render import page, render, ROOT
from build_screenshots import (COMMON_CSS, C_CSS, C_bg, confetti, tv, FIT_JS, SRC, LIME)
from build_header_search import HEAD_CSS, SEARCH_CSS
from build_set_c import player

OUT = ROOT / "final"
(OUT / "search").mkdir(parents=True, exist_ok=True)
(OUT / "header").mkdir(parents=True, exist_ok=True)

ICON = "../icons/AppIcon-04-phone-tv.png"
SHOTS = ["../screenshots/C1-controller.jpg", "../screenshots/C2-swing.jpg", "../screenshots/C4-friends.jpg"]
SEARCH_CSS = SEARCH_CSS.replace("body{background:#F2F2F7;font-family:'Inter','Rubik',sans-serif;color:#000}", "")

ART_CSS = """
.stage{position:absolute;left:0;top:0;transform-origin:0 0;overflow:hidden}
.h{position:absolute;white-space:nowrap;font-family:'Rubik';font-weight:900;color:#fff;letter-spacing:-3px;word-spacing:12px;
 -webkit-text-stroke:14px #10243D;paint-order:stroke fill;text-shadow:0 8px 0 #10243D;line-height:1.0}
.h span{color:#D7F044}
.sb{position:absolute;font:600 40px/1.25 'Rubik';color:#fff;text-shadow:0 4px 0 rgba(16,36,61,.55)}
.wmk{position:absolute;display:flex;align-items:center;gap:20px;font:800 44px 'Rubik';color:#fff;letter-spacing:-1px;text-shadow:0 4px 0 rgba(16,36,61,.5)}
.wmk img{width:84px;height:84px;filter:drop-shadow(0 8px 14px rgba(5,12,28,.35))}
.scard{position:absolute;transform-origin:0 0}
.scard .card{position:relative;left:0;top:0;font-family:'Inter','Rubik',sans-serif;color:#000;box-shadow:0 50px 90px rgba(16,36,61,.45)}
"""

PILLS = ('<div class="pill" style="left:{x}px;top:{y}px;background:#D7F044;color:#10243D;border:6px solid #10243D;box-shadow:8px 8px 0 #10243D">Swing</div>'
         '<div class="pill" style="left:{x2}px;top:{y}px;background:#fff;color:#10243D;border:6px solid #10243D;box-shadow:8px 8px 0 #10243D">Move</div>'
         '<div class="pill" style="left:{x3}px;top:{y}px;background:#fff;color:#10243D;border:6px solid #10243D;box-shadow:8px 8px 0 #10243D">Play together</div>')


def card_html(s, x, y):
    imgs = "".join(f'<img src="{p}">' for p in SHOTS)
    return (f'<div class="scard" style="left:{x}px;top:{y}px;width:1210px;transform:scale({s})"><div class="card">'
            f'<div class="row"><div class="ic"><img src="{ICON}"></div><div><div class="nm" style="font-family:Inter,Rubik,sans-serif;font-weight:600">Motion Club</div>'
            f'<div class="st">Swing your phone with friends</div><div class="rt">Games · Sports</div></div><div class="get">GET</div></div>'
            f'<div class="shots">{imgs}</div></div></div>')


def render_art(name, W, H, stage_html, wu, hu, to_dir):
    scale = W / wu
    body = f'<div class="stage" style="width:{wu}px;height:{hu}px;transform:scale({scale})">{stage_html}</div>{FIT_JS}'
    css = COMMON_CSS + C_CSS + HEAD_CSS + SEARCH_CSS + ART_CSS
    out = render(page(body, css, W, H), name, W, H)
    im = Image.open(out).convert("RGB")
    assert im.size == (W, H), (im.size, W, H)
    path = OUT / to_dir / f"{name}.png"
    im.save(path, optimize=True)
    im.save(OUT / to_dir / f"{name}.jpg", quality=94, subsampling=0)
    print(name, im.size)


# ---------------------------------------------------------------- search results
def search_169(W=5244, H=2950):
    wu, hu = 1920, 1080
    s = (hu - 150) / 1125
    cx = wu - 80 - 1210 * s
    stage = (C_bg("#FF8A63", "#E8506E") + confetti(5, n=30, area=(0, 0, 1900, 1050))
             + '<div class="wmk" style="left:90px;top:80px"><img src="../src/brand/club-crest.png">Motion Club</div>'
             + '<div class="h hl" data-max="700" style="left:90px;top:230px;font-size:120px">Your phone is<br><span>the controller</span></div>'
             + '<div class="sb" style="left:94px;top:540px">Swing it. Move. Play with friends.</div>'
             + player("pink", "cheer", 70, 640, .52, z=5) + player("navy", "lunge", 330, 660, .46, z=5, flip=True)
             + card_html(s, cx, (hu - 1125 * s) / 2))
    render_art("SearchResults-5244x2950", W, H, stage, wu, hu, "search")


def search_32(W, H):
    wu, hu = 1920, 1280
    s = (hu - 330) / 1125
    cx = (wu - 1210 * s) / 2
    stage = (C_bg("#7B4BF0", "#3C1FA8") + confetti(7, n=34, area=(0, 0, 1900, 1250))
             + '<div class="h hl" data-max="1740" style="left:90px;top:60px;font-size:104px;text-align:center;width:1740px">Your phone is <span>the controller</span></div>'
             + card_html(s, cx, 250)
             + player("coral", "cheer", 40, 560, .62, z=5) + player("lime", "lunge", 1450, 590, .56, z=5, flip=True))
    render_art(f"SearchResults-{W}x{H}", W, H, stage, wu, hu, "search")


# ---------------------------------------------------------------- headers
def header_169(W=5244, H=2950):
    wu, hu = 1920, 1080
    stage = (C_bg("#FF8A63", "#E8506E") + confetti(21, n=34, area=(960, 20, 1900, 1060))
             + '<div class="wmk" style="left:110px;top:96px"><img src="../src/brand/club-crest.png">Motion Club</div>'
             + '<div class="h hl" data-max="800" style="left:106px;top:250px;font-size:132px">Your phone is<br><span>the controller</span></div>'
             + '<div class="sb" style="left:112px;top:600px;font-size:44px">Swing it. Move. Play with friends.</div>'
             + PILLS.format(x=112, x2=330, x3=560, y=790)
             + tv("sky_04", 1000, 150, 960, rot=3, z=2, glow=LIME)
             + player("pink", "cheer", 1020, 330, .78, z=6) + player("navy", "lunge", 1380, 380, .72, z=6, flip=True))
    render_art(f"Header-{W}x{H}", W, H, stage, wu, hu, "header")


def header_wide(W=3840, H=1646):
    wu, hu = 1920, round(1920 * H / W)  # 823
    stage = (C_bg("#FF8A63", "#E8506E") + confetti(33, n=26, area=(900, 10, 1900, hu - 20))
             + '<div class="wmk" style="left:100px;top:56px"><img src="../src/brand/club-crest.png">Motion Club</div>'
             + '<div class="h hl" data-max="820" style="left:96px;top:176px;font-size:118px">Your phone is<br><span>the controller</span></div>'
             + '<div class="sb" style="left:100px;top:450px;font-size:38px">Swing it. Move. Play with friends.</div>'
             + PILLS.format(x=100, x2=318, x3=548, y=580)
             + tv("sky_04", 1020, 60, 860, rot=3, z=2, glow=LIME)
             + player("pink", "cheer", 1010, 190, .66, z=6) + player("navy", "lunge", 1360, 230, .6, z=6, flip=True))
    render_art(f"Header-{W}x{H}", W, H, stage, wu, hu, "header")


if __name__ == "__main__":
    search_169()
    search_32(3840, 2560)
    search_32(1920, 1280)
    header_169()
    header_wide()
