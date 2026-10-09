"""Wide header art (one per style, 3840x2160 + 1920x1080) and App Store search-result mockups.

Header art is designed in 1920x1080 units and rendered at 2x. Search mockups are review comps
(what the listing looks like in App Store search), not upload assets.
"""
from PIL import Image
from render import page, render, ROOT
import build_screenshots as B
from build_set_c import player
from build_screenshots import phone, tv, icon, tag, COMMON_CSS, A_CSS, B_CSS, C_CSS, A_bg, B_BG, C_bg, confetti, UP, SRC, CUR, LIME, SKY

HDR = ROOT / "header"
SRCH = ROOT / "search"
HDR.mkdir(exist_ok=True)
SRCH.mkdir(exist_ok=True)

CTRL = f"{CUR}/golf-controller-artwork.png"

HEAD_CSS = """
.stage{position:absolute;left:0;top:0;width:1920px;height:1080px;transform:scale(2);transform-origin:0 0;overflow:hidden}
.wm{position:absolute;left:110px;top:96px;display:flex;align-items:center;gap:22px;font:800 46px 'Bricolage';letter-spacing:-1px}
.wm img{width:92px;height:92px;filter:drop-shadow(0 8px 14px rgba(5,12,28,.35))}
.h1{position:absolute;left:110px;top:270px;font-family:'Bricolage';font-weight:800;font-size:176px;line-height:.93;letter-spacing:-3px;white-space:nowrap}
.sub{position:absolute;left:112px;top:640px;font:600 44px/1.25 'Rubik'}
.pill{position:absolute;font:700 30px/1 'Rubik';letter-spacing:.08em;text-transform:uppercase;padding:18px 30px;border-radius:99px}
"""


def header(key):
    if key == "A":
        bg = A_bg(f"{UP}/cliff_03.png")
        txt, accent = "#10243D", "background:linear-gradient(180deg,transparent 58%,#D7F044 58%,#D7F044 92%,transparent 92%);padding:0 8px;margin:0 -8px"
        extra_css = ".wash{background:linear-gradient(90deg,rgba(255,249,238,.97) 0,rgba(255,249,238,.93) 45%,rgba(255,249,238,.15) 80%,rgba(255,249,238,0) 100%)!important}"
        pills = (f'<div class="pill" style="left:112px;top:800px;background:{LIME};color:#10243D">Swing</div>'
                 f'<div class="pill" style="left:260px;top:800px;background:#fff;color:#10243D;box-shadow:0 10px 24px rgba(16,36,61,.2)">Move</div>'
                 f'<div class="pill" style="left:440px;top:800px;background:#fff;color:#10243D;box-shadow:0 10px 24px rgba(16,36,61,.2)">Play together</div>')
        glow = None
    elif key == "B":
        bg, txt = B_BG, "#fff"
        accent = "color:#D7F044"
        extra_css = ""
        pills = (f'<div class="pill" style="left:112px;top:800px;background:{LIME};color:#10243D">Swing</div>'
                 f'<div class="pill" style="left:260px;top:800px;background:#fff;color:#10243D">Move</div>'
                 f'<div class="pill" style="left:440px;top:800px;background:#fff;color:#10243D">Play together</div>')
        glow = LIME
    else:
        bg = C_bg("#FF8A63", "#E8506E") + confetti(21, n=34, area=(960, 20, 1900, 1060))
        txt, accent = "#fff", "color:#D7F044"
        extra_css = ".h1{text-shadow:0 8px 0 rgba(16,36,61,.9)}"
        pills = (f'<div class="pill" style="left:112px;top:800px;background:{LIME};color:#10243D;border:6px solid #10243D;box-shadow:8px 8px 0 #10243D">Swing</div>'
                 f'<div class="pill" style="left:330px;top:800px;background:#fff;color:#10243D;border:6px solid #10243D;box-shadow:8px 8px 0 #10243D">Move</div>'
                 f'<div class="pill" style="left:560px;top:800px;background:#fff;color:#10243D;border:6px solid #10243D;box-shadow:8px 8px 0 #10243D">Play together</div>')
        glow = LIME

    stage = (
        bg
        + f'<div class="wm" style="color:{txt}"><img src="{SRC}/brand/club-crest.png">Motion Club</div>'
        + f'<div class="h1" style="color:{txt}">Your phone is<br><span style="{accent}">the controller</span></div>'
        + f'<div class="sub" style="color:{txt}">Swing it. Move. Play with friends.</div>'
        + pills
        + tv("sky_04", 1000, 150, 960, rot=3, z=2, glow=glow)
        + (tv("cliff_03", 960, 540, 760, rot=-3, z=3, glow=glow) if key != "C" else "")
        + (phone(None, 1560, 380, 330, rot=7, z=5, src=CTRL) if key != "C" else
           player("pink", "cheer", 1020, 330, .78, z=6) + player("navy", "lunge", 1380, 380, .72, z=6, flip=True))
    )
    css = COMMON_CSS + {"A": A_CSS, "B": B_CSS, "C": C_CSS}[key] + HEAD_CSS + extra_css
    html = page(f'<div class="stage">{stage}</div>', css, 3840, 2160)
    out = render(html, f"header-{key}", 3840, 2160)
    im = Image.open(out).convert("RGB")
    im.save(HDR / f"Header-{key}-3840x2160.jpg", quality=92, subsampling=0)
    im.resize((1920, 1080), Image.LANCZOS).save(HDR / f"Header-{key}-1920x1080.jpg", quality=92, subsampling=0)
    print("header", key)


SEARCH_CSS = """
body{background:#F2F2F7;font-family:'Inter','Rubik',sans-serif;color:#000}
.card{position:absolute;left:40px;top:40px;width:1210px;background:#fff;border-radius:44px;padding:44px 44px 48px;box-shadow:0 2px 0 rgba(0,0,0,.04)}
.row{display:flex;align-items:center;gap:36px}
.ic{width:200px;height:200px;border-radius:45px;overflow:hidden;flex:none;box-shadow:0 0 0 2px rgba(0,0,0,.08) inset}
.ic img{width:100%;height:100%;display:block}
.nm{font:600 56px/1.1 'Inter'}
.st{font:400 40px/1.25 'Inter';color:#6e6e73;margin-top:8px}
.rt{font:400 32px 'Inter';color:#6e6e73;margin-top:12px}
.get{margin-left:auto;background:#EEF0F6;color:#0A66E8;font:700 44px 'Inter';padding:22px 58px;border-radius:99px}
.shots{display:flex;gap:26px;margin-top:40px}
.shots img{width:366px;height:792px;object-fit:cover;border-radius:38px;box-shadow:0 0 0 2px rgba(0,0,0,.08)}
.cap{font:500 30px 'Inter';color:#6e6e73;position:absolute;left:44px;top:1600px}
"""


def search(key, icon_file, shots):
    imgs = "".join(f'<img src="../screenshots/{s}">' for s in shots)
    body = (f'<div class="card"><div class="row"><div class="ic"><img src="../icons/{icon_file}"></div>'
            f'<div><div class="nm">Motion Club</div><div class="st">Swing your phone with friends</div>'
            f'<div class="rt">Games · Sports</div></div><div class="get">GET</div></div>'
            f'<div class="shots">{imgs}</div></div>')
    html = page(body, SEARCH_CSS, 1290, 1280)
    out = render(html, f"search-{key}", 1290, 1280)
    Image.open(out).convert("RGB").save(SRCH / f"SearchResult-{key}.png", optimize=True)
    print("search", key)


if __name__ == "__main__":
    for k in "ABC":
        header(k)
    search("A", "AppIcon-01-crest.png", [f"A{n}.jpg" for n in ("1-controller", "2-swing", "4-friends")])
    search("B", "AppIcon-03-phone-swing.png", [f"B{n}.jpg" for n in ("1-controller", "2-swing", "4-friends")])
    search("C", "AppIcon-06-two-balls.png", [f"C{n}.jpg" for n in ("1-controller", "2-swing", "4-friends")])
