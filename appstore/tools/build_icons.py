"""Ten App Store icon variations (1024x1024, opaque PNG, square corners) for Motion Club."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter
from render import page, render, ROOT

OUT = ROOT / "icons"
OUT.mkdir(exist_ok=True)
S = "../src"

# ---- reusable vector bits -------------------------------------------------------------
GOLF_BALL = """<svg viewBox="0 0 200 200" width="{s}" height="{s}"><defs>
<radialGradient id="gb{n}" cx="35%" cy="30%" r="80%"><stop offset="0" stop-color="#fff"/><stop offset=".7" stop-color="#EEF2F6"/><stop offset="1" stop-color="#B9C6D2"/></radialGradient>
<pattern id="dm{n}" width="22" height="19" patternUnits="userSpaceOnUse" patternTransform="rotate(10)"><circle cx="5" cy="5" r="3.1" fill="#9FB0C0" opacity=".55"/><circle cx="16" cy="14.5" r="3.1" fill="#9FB0C0" opacity=".55"/></pattern>
<clipPath id="cc{n}"><circle cx="100" cy="100" r="96"/></clipPath></defs>
<circle cx="100" cy="100" r="96" fill="url(#gb{n})"/><g clip-path="url(#cc{n})"><rect width="200" height="200" fill="url(#dm{n})"/>
<ellipse cx="70" cy="58" rx="46" ry="30" fill="#fff" opacity=".55" transform="rotate(-30 70 58)"/></g></svg>"""

TENNIS_BALL = """<svg viewBox="0 0 200 200" width="{s}" height="{s}"><defs>
<radialGradient id="tb{n}" cx="35%" cy="30%" r="85%"><stop offset="0" stop-color="#F0FF73"/><stop offset=".55" stop-color="#D7F044"/><stop offset="1" stop-color="#9DBA16"/></radialGradient>
<clipPath id="tc{n}"><circle cx="100" cy="100" r="96"/></clipPath></defs>
<circle cx="100" cy="100" r="96" fill="url(#tb{n})"/><g clip-path="url(#tc{n})" fill="none" stroke="#fff" stroke-width="9" stroke-linecap="round">
<path d="M-10 40 C60 70 60 130 -10 160"/><path d="M210 40 C140 70 140 130 210 160"/></g>
<ellipse cx="72" cy="58" rx="40" ry="24" fill="#fff" opacity=".35" transform="rotate(-30 72 58)"/></svg>"""

PALM = """<svg viewBox="0 0 400 520" width="{w}" height="{h}" fill="{c}">
<path d="M186 520 C200 420 196 300 226 184 L256 190 C238 300 246 420 246 520Z"/>
<g transform="translate(242 186)">
<path id="lf" d="M0 0 C52-62 160-54 222 58 C156-6 64-24 0 0Z"/>
<use href="#lf" transform="rotate(-62)"/><use href="#lf" transform="rotate(-24)"/><use href="#lf" transform="rotate(12)"/>
<use href="#lf" transform="scale(-1 1) rotate(-62)"/><use href="#lf" transform="scale(-1 1) rotate(-24)"/><use href="#lf" transform="scale(-1 1) rotate(12)"/>
<use href="#lf" transform="rotate(-100)"/><use href="#lf" transform="scale(-1 1) rotate(-100)"/>
<circle cx="-8" cy="16" r="15"/><circle cx="14" cy="22" r="14"/></g></svg>"""


def hero(name, left, top, h, extra=""):
    return (f'<img src="{S}/heroes/menu-hero-{name}.png" style="position:absolute;left:{left}px;top:{top}px;'
            f'height:{h}px;filter:drop-shadow(0 22px 28px rgba(8,18,40,.45));{extra}">')


def asset(path, left, top, w, extra=""):
    return f'<img src="{S}/{path}" style="position:absolute;left:{left}px;top:{top}px;width:{w}px;{extra}">'


def rays(color="rgba(255,255,255,.18)", n=14, cx=512, cy=560):
    wedges = ""
    import math
    step = 360 / n
    for i in range(n):
        a0 = math.radians(i * step)
        a1 = math.radians(i * step + step / 2)
        r = 1500
        wedges += (f'<path d="M{cx} {cy} L{cx + r * math.cos(a0):.0f} {cy + r * math.sin(a0):.0f} '
                   f'L{cx + r * math.cos(a1):.0f} {cy + r * math.sin(a1):.0f}Z" fill="{color}"/>')
    return f'<svg viewBox="0 0 1024 1024" style="position:absolute;inset:0;width:1024px;height:1024px">{wedges}</svg>'


ICONS = {}

# 01 Crest — the club's own mark, on a deep lagoon with sun rays
ICONS["01-crest"] = (
    "background:radial-gradient(circle at 50% 42%,#1D5A86 0%,#10243D 62%,#0A192D 100%)",
    rays("rgba(215,240,68,.10)", 18, 512, 470)
    + asset("brand/club-crest.png", 118, 112, 788, "filter:drop-shadow(0 30px 34px rgba(0,0,0,.5))"),
)

# 02 Crossed sports — golf club + racket on lime, with a swing trail
ICONS["02-crossed"] = (
    "background:linear-gradient(160deg,#EAFB6B 0%,#D7F044 45%,#8ED94F 100%)",
    '<svg viewBox="0 0 1024 1024" style="position:absolute;inset:0"><path d="M120 820 C330 980 760 900 930 470" fill="none" '
    'stroke="#fff" stroke-opacity=".75" stroke-width="46" stroke-linecap="round"/>'
    '<path d="M120 820 C330 980 760 900 930 470" fill="none" stroke="#10243D" stroke-opacity=".12" stroke-width="12" stroke-linecap="round" transform="translate(10 14)"/></svg>'
    + asset("brand/club-golf.png", 10, 330, 640, "transform:rotate(-8deg);filter:drop-shadow(0 26px 24px rgba(16,36,61,.4))")
    + asset("brand/club-tennis.png", 380, 40, 640, "transform:rotate(6deg);filter:drop-shadow(0 26px 24px rgba(16,36,61,.4))"),
)

# 03 Phone swing - the real controller screen on a tilted phone, with a swing arc into the ball
PHONE_CSS = ("position:absolute;background:linear-gradient(145deg,#3a3f48,#0b0d10);border-radius:70px;padding:14px;"
             "box-shadow:0 0 0 2px #6b717b inset,0 40px 60px rgba(5,12,28,.5)")
ICONS["03-phone-swing"] = (
    "background:linear-gradient(165deg,#35B8C7 0%,#1E6FA8 55%,#10243D 120%)",
    rays("rgba(255,255,255,.12)", 16, 420, 700)
    + '<svg viewBox="0 0 1024 1024" style="position:absolute;inset:0"><defs><linearGradient id="s3" x1="0" y1="1" x2="1" y2="0"><stop offset="0" stop-color="#fff" stop-opacity="0"/><stop offset="1" stop-color="#fff" stop-opacity=".9"/></linearGradient></defs>'
    '<path d="M40 960 C120 560 480 300 900 260" fill="none" stroke="url(#s3)" stroke-width="110" stroke-linecap="round"/></svg>'
    + f'<div style="{PHONE_CSS};left:250px;top:120px;width:470px;height:960px;transform:rotate(-24deg)">'
      f'<div style="width:100%;height:100%;border-radius:58px;overflow:hidden;background:url({S}/current/golf-controller-artwork.png) 50% 0/cover"></div></div>'
    + f'<div style="position:absolute;left:700px;top:70px;filter:drop-shadow(0 0 50px rgba(215,240,68,.8))">{GOLF_BALL.format(s=230, n=3)}</div>',
)

# 04 Phone + TV as a LOGO: bold shapes, one outline weight, flat sticker shadows, a single swing arc
def _img(name, x, y, w, rot=0):
    cx, cy = x + w / 2, y + w / 2
    return f'<image href="{S}/brand/club-{name}.png" x="{x}" y="{y}" width="{w}" height="{w}" transform="rotate({rot} {cx} {cy})"/>'


_NAVY = "#10243D"
_LOGO04 = f"""<svg viewBox="0 0 1024 1024" style="position:absolute;inset:0;width:1024px;height:1024px">
<defs>
 <linearGradient id="sky4" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#5FD0E6"/><stop offset=".62" stop-color="#C9F4F7"/><stop offset="1" stop-color="#FFE7A8"/></linearGradient>
 <linearGradient id="scr4" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#7BDDEB"/><stop offset="1" stop-color="#5B7CF0"/></linearGradient>
 <linearGradient id="arc4" x1="0" y1="1" x2="1" y2="0"><stop offset="0" stop-color="#D7F044" stop-opacity="0"/><stop offset=".5" stop-color="#D7F044"/><stop offset="1" stop-color="#F6FFB0"/></linearGradient>
 <clipPath id="tvclip4"><rect x="84" y="136" width="856" height="564" rx="46"/></clipPath>
 <clipPath id="phclip4"><rect x="584" y="506" width="256" height="560" rx="44"/></clipPath>
</defs>
<!-- TV: flat shadow, lime bezel, thick navy outline -->
<rect x="62" y="128" width="940" height="644" rx="82" fill="{_NAVY}" opacity=".45"/>
<rect x="44" y="96" width="940" height="644" rx="80" fill="#D7F044" stroke="{_NAVY}" stroke-width="18"/>
<rect x="84" y="136" width="856" height="564" rx="46" fill="url(#sky4)" stroke="{_NAVY}" stroke-width="14"/>
<g clip-path="url(#tvclip4)">
 <rect x="84" y="470" width="856" height="230" fill="#35B87E"/>
 <path d="M170 700 L334 400 L690 400 L854 700Z" fill="#2FA06B"/>
 <path d="M170 700 L334 400 L690 400 L854 700Z" fill="none" stroke="#fff" stroke-width="9" stroke-linejoin="round"/>
 <path d="M512 700 L512 400 M246 560 L778 560" stroke="#fff" stroke-width="9" stroke-linecap="round"/>
 <path d="M296 470 L728 470" stroke="{_NAVY}" stroke-width="12" stroke-linecap="round"/>
 <path d="M296 470 L728 470" stroke="#fff" stroke-width="5" stroke-dasharray="3 12" stroke-linecap="round"/>
</g>
<!-- swing arc into the ball -->
<path d="M600 620 C590 440 660 330 724 262" fill="none" stroke="url(#arc4)" stroke-width="34" stroke-linecap="round"/>
<g transform="translate(742 236)"><circle r="54" fill="#D7F044" stroke="{_NAVY}" stroke-width="12"/>
 <path d="M-52 -16 C-14 -2 -14 26 -48 38 M52 -16 C14 -2 14 26 48 38" fill="none" stroke="#fff" stroke-width="9" stroke-linecap="round"/></g>
<!-- stand -->
<rect x="440" y="736" width="144" height="46" fill="{_NAVY}"/>
<rect x="352" y="770" width="320" height="40" rx="20" fill="{_NAVY}"/>
<!-- phone: smaller, in front, same outline weight -->
<g transform="rotate(8 712 790)">
 <rect x="580" y="512" width="300" height="600" rx="64" fill="{_NAVY}" opacity=".45" transform="translate(16 18)"/>
 <rect x="566" y="486" width="300" height="600" rx="64" fill="#fff" stroke="{_NAVY}" stroke-width="18"/>
 <rect x="584" y="506" width="264" height="560" rx="46" fill="url(#scr4)"/>
 <rect x="658" y="518" width="116" height="28" rx="14" fill="{_NAVY}"/>
 {_img("boxing", 592, 560, 118, -8)}{_img("tennis", 716, 548, 124, 6)}
 {_img("golf", 634, 690, 176, -4)}
 {_img("football", 592, 850, 118, -6)}{_img("bowling", 716, 850, 124, 8)}
</g>
</svg>"""

ICONS["04-phone-tv"] = (
    "background:radial-gradient(circle at 28% 18%,#8B5CF6 0%,#5B2BD9 52%,#2A1480 125%)",
    _LOGO04,
)

# 05 Trophy — palm trophy with gold glow
ICONS["05-trophy"] = (
    "background:radial-gradient(circle at 50% 46%,#3C2D9C 0%,#241868 55%,#10243D 120%)",
    rays("rgba(255,213,69,.16)", 20, 512, 520)
    + '<div style="position:absolute;left:212px;top:212px;width:600px;height:600px;border-radius:50%;background:radial-gradient(circle,rgba(255,214,90,.55),rgba(255,214,90,0) 68%)"></div>'
    + asset("brand/club-trophy.png", 142, 120, 740, "filter:drop-shadow(0 30px 30px rgba(0,0,0,.5))"),
)

# 06 Two balls — golf and tennis ball, one trail
ICONS["06-two-balls"] = (
    "background:linear-gradient(150deg,#FF8A63 0%,#FF7556 50%,#E8506E 100%)",
    '<svg viewBox="0 0 1024 1024" style="position:absolute;inset:0"><defs><linearGradient id="tr" x1="0" x2="1"><stop offset="0" stop-color="#fff" stop-opacity="0"/><stop offset="1" stop-color="#fff" stop-opacity=".85"/></linearGradient></defs>'
    '<path d="M-40 900 C260 800 420 640 560 470" fill="none" stroke="url(#tr)" stroke-width="64" stroke-linecap="round"/></svg>'
    + f'<div style="position:absolute;left:150px;top:300px;filter:drop-shadow(0 28px 26px rgba(80,10,40,.45))">{GOLF_BALL.format(s=520, n=1)}</div>'
    + f'<div style="position:absolute;left:430px;top:190px;filter:drop-shadow(0 28px 26px rgba(80,10,40,.45))">{TENNIS_BALL.format(s=520, n=1)}</div>',
)

# 07 M monogram — cream, sun disc, lime swoosh
ICONS["07-monogram"] = (
    "background:linear-gradient(180deg,#FFF9EE 0%,#FBEFD2 100%)",
    '<div style="position:absolute;left:172px;top:128px;width:680px;height:680px;border-radius:50%;background:radial-gradient(circle at 40% 35%,#FFE27A,#FFC53D)"></div>'
    '<div class="display" style="position:absolute;left:0;right:0;top:170px;text-align:center;font-size:760px;line-height:760px;color:#10243D;letter-spacing:-30px;font-weight:800">M</div>'
    '<svg viewBox="0 0 1024 1024" style="position:absolute;inset:0"><path d="M150 830 C420 920 700 900 880 760" fill="none" stroke="#D7F044" stroke-width="64" stroke-linecap="round"/>'
    '<path d="M150 830 C420 920 700 900 880 760" fill="none" stroke="#10243D" stroke-width="14" stroke-linecap="round" transform="translate(6 12)" opacity=".18"/></svg>',
)

# 08 Palm & sun — resort sunset
ICONS["08-palm-sun"] = (
    "background:linear-gradient(180deg,#5B2BD9 0%,#C2468D 34%,#FF7556 62%,#FFC857 100%)",
    '<div style="position:absolute;left:262px;top:392px;width:500px;height:500px;border-radius:50%;background:radial-gradient(circle at 50% 40%,#FFF3A8,#FFD04A 70%);box-shadow:0 0 120px 40px rgba(255,214,90,.45)"></div>'
    '<svg viewBox="0 0 1024 1024" style="position:absolute;inset:0"><path d="M0 820 C200 780 330 860 512 820 C700 780 850 860 1024 810 L1024 1024 L0 1024Z" fill="#10243D"/>'
    '<path d="M0 900 C220 860 360 940 540 900 C720 860 880 930 1024 890 L1024 1024 L0 1024Z" fill="#0A192D"/></svg>'
    + f'<div style="position:absolute;left:130px;top:120px">{PALM.format(w=800, h=1040, c="#0A192D")}</div>',
)

# 09 Cliffside — a real course render with the golf kit up front
ICONS["09-cliffside"] = (
    f"background:url({S}/art/map-golf-cliffside.jpg) 62% 50%/auto 1024px no-repeat",
    '<div style="position:absolute;inset:0;background:linear-gradient(180deg,rgba(10,25,45,0) 30%,rgba(10,25,45,.55) 100%)"></div>'
    + asset("brand/club-golf.png", 172, 190, 680, "filter:drop-shadow(0 30px 30px rgba(0,0,0,.5))"),
)

# 10 Swing arc — abstract motion trail into a ball
ICONS["10-swing-arc"] = (
    "background:radial-gradient(circle at 70% 25%,#1B4B78 0%,#10243D 55%,#0A192D 110%)",
    '<svg viewBox="0 0 1024 1024" style="position:absolute;inset:0"><defs><linearGradient id="sw" x1="0" y1="1" x2="1" y2="0">'
    '<stop offset="0" stop-color="#D7F044" stop-opacity="0"/><stop offset=".55" stop-color="#D7F044" stop-opacity=".95"/><stop offset="1" stop-color="#F4FF9A"/></linearGradient></defs>'
    '<path d="M70 880 C150 480 470 250 800 270" fill="none" stroke="url(#sw)" stroke-width="130" stroke-linecap="round"/>'
    '<path d="M110 900 C230 560 520 360 800 360" fill="none" stroke="#35B8C7" stroke-opacity=".55" stroke-width="34" stroke-linecap="round"/></svg>'
    + f'<div style="position:absolute;left:640px;top:140px;filter:drop-shadow(0 0 60px rgba(215,240,68,.7))">{GOLF_BALL.format(s=250, n=2)}</div>',
)


def build():
    for key, (bg, inner) in ICONS.items():
        html = page(f'<div style="position:absolute;inset:0;overflow:hidden;{bg}">{inner}</div>', w=1024, h=1024)
        png = render(html, f"icon-{key}", 1024, 1024)
        Image.open(png).convert("RGB").save(OUT / f"AppIcon-{key}.png", optimize=True)
        print("icon", key)


def preview_sheet():
    """Contact sheet: each icon with iOS-style rounded mask, plus 120/60 px legibility checks."""
    files = sorted(OUT.glob("AppIcon-*.png"))
    cell, pad, cols = 300, 36, 5
    rows = (len(files) + cols - 1) // cols
    W = cols * (cell + pad) + pad
    H = rows * (cell + 150) + pad
    sheet = Image.new("RGB", (W, H), (236, 238, 242))
    d = ImageDraw.Draw(sheet)

    def rounded(im, size):
        im = im.resize((size, size), Image.LANCZOS).convert("RGBA")
        m = Image.new("L", (size * 4, size * 4), 0)
        ImageDraw.Draw(m).rounded_rectangle((0, 0, size * 4 - 1, size * 4 - 1), radius=int(size * 4 * 0.2237), fill=255)
        im.putalpha(m.resize((size, size), Image.LANCZOS))
        return im

    for i, f in enumerate(files):
        x = pad + (i % cols) * (cell + pad)
        y = pad + (i // cols) * (cell + 150)
        big = Image.open(f)
        sheet.paste(rounded(big, cell), (x, y), rounded(big, cell))
        s120 = rounded(big, 120)
        s60 = rounded(big, 60)
        sheet.paste(s120, (x, y + cell + 12), s120)
        sheet.paste(s60, (x + 136, y + cell + 42), s60)
        d.text((x + 210, y + cell + 12), f.stem.replace("AppIcon-", ""), fill=(30, 40, 60))
    sheet.save(OUT / "_preview-all-icons.png")


if __name__ == "__main__":
    build()
    preview_sheet()
