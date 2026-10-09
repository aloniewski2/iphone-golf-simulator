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


# ---------------------------------------------------------------- key art (Apple asset best practices)
# One short phrase, a centred focal point inside a safe area, no store UI, no icon, no pills/subtitle/logo.
def key_art(W, H, name, to_dir, bg=("#FF8A63", "#E8506E"), seed=5):
    wu, hu = 1920, round(1920 * H / W)
    k = hu / 1080
    head_fs = int(150 * min(1.1, max(.78, k)))
    tvw = int(900 * min(1.1, max(.72, k)))
    tvh = tvw * 9 / 16
    cs = {True: .72}.get(hu <= 1100, .95) if hu > 900 else .62
    if hu > 1200: cs = .95
    elif hu > 900: cs = .72
    head_y = int(hu * .07)
    chars_h = 790 * cs
    chars_y = hu - chars_h - hu * .06
    tv_y = head_y + head_fs + hu * .045
    tv_x = (wu - tvw) / 2
    pw = 600 * cs
    stage = (C_bg(bg[0], bg[1]) + confetti(seed, n=22, area=(int(wu * .12), int(hu * .12), int(wu * .88), int(hu * .92)))
             + f'<div class="h hl" data-max="{int(wu * .66)}" style="left:50%;transform:translateX(-50%);top:{head_y}px;font-size:{head_fs}px">Swing. <span>Move.</span> Play.</div>'
             + tv("sky_04", tv_x, tv_y, tvw, rot=-1.5, z=2, glow=LIME)
             + player("pink", "cheer", wu / 2 - pw * .86, chars_y, cs, z=6)
             + player("navy", "lunge", wu / 2 - pw * .18, chars_y + 10 * cs, cs, z=6, flip=True))
    render_art(name, W, H, stage, wu, hu, to_dir)


def search_169(): key_art(5244, 2950, "SearchResults-5244x2950", "search", seed=5)
def search_32a(): key_art(3840, 2560, "SearchResults-3840x2560", "search", bg=("#7B4BF0", "#3C1FA8"), seed=7)
def search_32b(): key_art(1920, 1280, "SearchResults-1920x1280", "search", bg=("#7B4BF0", "#3C1FA8"), seed=7)
def header_169(): key_art(5244, 2950, "Header-5244x2950", "header", seed=21)
def header_wide(): key_art(3840, 1646, "Header-3840x1646", "header", seed=33)


if __name__ == "__main__":
    search_169(); search_32a(); search_32b(); header_169(); header_wide()
