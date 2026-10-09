"""Three App Store screenshot sets for Motion Club, 1320x2868 (iPhone 6.9").

Built ONLY from visuals verified as current on the final-build branch (Oct 8 build):
  - native phone UI captures (proof/menu-beta, ArtDir/review/post-match)
  - the shipped controller artwork (golf-controller-artwork.png)
  - in-engine golf captures (work/postcard-refresh/release-gates) and tennis gameplay
  - the bundled Cliffside golf / Skyscraper tennis gameplay clips, current venue map art
  - the club crest / sport icons used by the native menus
The older illustrated menu cast, club room art and early tennis clips are intentionally not used.

    python3 tools/prep_upscale.py && python3 tools/build_screenshots.py [A|B|C ...]
"""
import random
import sys
from PIL import Image
from render import page, render, ROOT

W, H = 1320, 2868
UP, SRC = "up", "../src"
CUR = f"{SRC}/current"
SHOTS = ROOT / "screenshots"
SHOTS.mkdir(exist_ok=True)

PHONE_RATIO = 874 / 402  # native capture aspect (iPhone 16 Pro points)
LIME, SKY = "#D7F044", "#35B8C7"
G = lambda h, p="tee": f"{CUR}/golf_h{h}-{p}.png"          # in-engine golf capture (900x1600)

COMMON_CSS = """
.abs{position:absolute}
.phone{position:absolute;background:linear-gradient(145deg,#3a3f48,#0b0d10);box-shadow:0 0 0 2px #6b717b inset,0 50px 70px rgba(5,12,28,.45),0 12px 24px rgba(5,12,28,.3)}
.phone .scr{width:100%;height:100%;overflow:hidden;background:#000;position:relative}
.phone .scr img{width:100%;height:100%;object-fit:cover;display:block}
.phone .isl{position:absolute;left:50%;transform:translateX(-50%);background:#000;border-radius:99px}
.tv{position:absolute;background:#05070b;box-shadow:0 40px 60px rgba(5,12,28,.45),0 10px 20px rgba(5,12,28,.3)}
.tv img{width:100%;height:100%;object-fit:cover;display:block}
.tv::after{content:'';position:absolute;inset:0;background:linear-gradient(120deg,rgba(255,255,255,.14) 0%,rgba(255,255,255,0) 30%);pointer-events:none;border-radius:inherit}
.tag{position:absolute;font:700 30px/1 'Rubik';letter-spacing:.06em;text-transform:uppercase;padding:14px 22px;border-radius:99px}
.hl{position:absolute;white-space:nowrap}
.fx{position:absolute;pointer-events:none}
"""

FIT_JS = """<script>
document.fonts.ready.then(()=>{document.querySelectorAll('.hl').forEach(e=>{
 const max=+e.dataset.max||1160;let fs=parseFloat(getComputedStyle(e).fontSize);
 while(e.scrollWidth>max&&fs>60){fs-=2;e.style.fontSize=fs+'px';}});});
</script>"""


def phone(name, x, y, sw, rot=0, z=1, src=None):
    b = sw * 0.032
    sh = sw * PHONE_RATIO
    path = src or f"{UP}/{name}.png"
    return (f'<div class="phone" style="left:{x}px;top:{y}px;width:{sw + 2 * b:.0f}px;height:{sh + 2 * b:.0f}px;'
            f'border-radius:{sw * 0.165:.0f}px;padding:{b:.0f}px;transform:rotate({rot}deg);z-index:{z}">'
            f'<div class="scr" style="border-radius:{sw * 0.135:.0f}px"><img src="{path}"></div>'
            f'<div class="isl" style="top:{b + sw * 0.028:.0f}px;width:{sw * 0.29:.0f}px;height:{sw * 0.085:.0f}px"></div></div>')


def tv(name, x, y, w, rot=0, z=1, src=None, glow=None):
    h = w * 9 / 16
    path = src or f"{UP}/{name}.png"
    bz = max(10, w * 0.011)
    extra = f"box-shadow:0 0 0 5px {glow},0 0 70px 14px {glow}66,0 40px 60px rgba(5,12,28,.45);" if glow else ""
    return (f'<div class="tv" style="left:{x}px;top:{y}px;width:{w}px;height:{h:.0f}px;padding:{bz:.0f}px;'
            f'border-radius:{w * 0.026:.0f}px;transform:rotate({rot}deg);z-index:{z};{extra}">'
            f'<img src="{path}" style="border-radius:{w * 0.016:.0f}px"></div>')


def card(path, x, y, w, h, rot=0, radius=34, z=1, border=None, pos="center", shadow=True):
    bd = f"border:{border};" if border else ""
    sh = "box-shadow:0 30px 50px rgba(5,12,28,.4),0 8px 16px rgba(5,12,28,.25);" if shadow else ""
    return (f'<div class="abs" style="left:{x}px;top:{y}px;width:{w}px;height:{h}px;border-radius:{radius}px;overflow:hidden;'
            f'transform:rotate({rot}deg);z-index:{z};{bd}{sh}background:#000 url({path}) {pos}/cover no-repeat"></div>')


def icon(name, x, y, w, rot=0, z=6):
    return (f'<img class="abs" src="{SRC}/brand/club-{name}.png" style="left:{x}px;top:{y}px;width:{w}px;z-index:{z};'
            f'transform:rotate({rot}deg);filter:drop-shadow(0 24px 28px rgba(16,36,61,.4))">')


def tag(text, x, y, bg, fg, rot=0, z=9, size=30):
    return (f'<div class="tag" style="left:{x}px;top:{y}px;background:{bg};color:{fg};transform:rotate({rot}deg);z-index:{z};'
            f'font-size:{size}px;box-shadow:0 10px 24px rgba(5,12,28,.28)">{text}</div>')


def build_page(name, css, body):
    html = page(f'{body}{FIT_JS}', COMMON_CSS + css, W, H)
    out = render(html, name, W, H)
    Image.open(out).convert("RGB").save(SHOTS / f"{name}.jpg", quality=93, subsampling=0)
    print("shot", name)


# ======================================================================================
# SET A - "Resort": warm, bright, cream + lime, navy headlines over a blurred real frame
# ======================================================================================
A_CSS = """
.bgimg{position:absolute;inset:-80px;background-size:cover;background-position:center;filter:blur(16px) saturate(1.2)}
.wash{position:absolute;inset:0;background:linear-gradient(180deg,rgba(255,249,238,.97) 0,rgba(255,249,238,.93) 470px,rgba(255,249,238,.40) 980px,rgba(255,249,238,.05) 1500px)}
.kick{position:absolute;left:90px;top:130px;font:700 34px/1 'Rubik';letter-spacing:.2em;color:#10243D;display:flex;align-items:center;gap:16px;text-transform:uppercase}
.kick i{display:block;width:44px;height:44px;border-radius:50%;background:#D7F044;box-shadow:0 0 0 6px rgba(215,240,68,.35)}
.hlA{left:90px;top:215px;font-family:'Bricolage';font-weight:800;font-size:172px;line-height:.98;letter-spacing:-5px;color:#10243D}
.hlA mark{background:linear-gradient(180deg,transparent 58%,#D7F044 58%,#D7F044 92%,transparent 92%);color:inherit;padding:0 8px;margin:0 -8px}
.pillA{background:#fff;color:#10243D;box-shadow:0 12px 30px rgba(16,36,61,.2)!important}
.cap{position:absolute;left:0;right:0;bottom:0;padding:18px 20px;font:700 30px/1.1 'Rubik';color:#fff;background:linear-gradient(0deg,rgba(10,25,45,.8),rgba(10,25,45,0));text-align:left}
"""


def A_head(kicker, l1, l2, mark):
    return (f'<div class="kick"><i></i>{kicker}</div>'
            f'<div class="hl hlA" data-max="1140">{l1}<br>{l2.replace(mark, f"<mark>{mark}</mark>")}</div>')


def A_bg(path):
    return f'<div class="bgimg" style="background-image:url({path})"></div><div class="wash"></div>'


# ======================================================================================
# SET B - "Arena Night": dark lagoon, neon lime, gameplay-first
# ======================================================================================
B_CSS = """
body{background:#0A192D}
.bgB{position:absolute;inset:0;background:
 radial-gradient(900px 700px at 90% 8%,rgba(91,43,217,.75),rgba(91,43,217,0) 70%),
 radial-gradient(900px 800px at 0% 78%,rgba(53,184,199,.38),rgba(53,184,199,0) 70%),
 linear-gradient(175deg,#10243D 0%,#0A192D 55%,#1B1250 130%)}
.dots{position:absolute;inset:0;background-image:radial-gradient(rgba(255,255,255,.07) 2px,transparent 2.5px);background-size:46px 46px}
.kickB{position:absolute;left:90px;top:130px;font:700 34px/1 'Rubik';letter-spacing:.24em;color:#D7F044;text-transform:uppercase}
.kickB::before{content:'';display:inline-block;width:70px;height:6px;background:#D7F044;vertical-align:middle;margin-right:20px;border-radius:9px}
.hlB{left:90px;top:210px;font-family:'Bricolage';font-weight:800;font-size:180px;line-height:.96;letter-spacing:-5px;color:#fff}
.hlB em{font-style:normal;color:#D7F044}
"""
B_BG = '<div class="bgB"></div><div class="dots"></div>'


def B_head(kicker, l1, l2):
    return f'<div class="kickB">{kicker}</div><div class="hl hlB" data-max="1140">{l1}<br>{l2}</div>'


# ======================================================================================
# SET C - "Party Pop": saturated blocks, sticker type, confetti
# ======================================================================================
C_CSS = """
.hlC{left:80px;top:150px;font-family:'Bricolage';font-weight:800;font-size:190px;line-height:.94;letter-spacing:-6px;color:#fff;
 text-shadow:0 8px 0 rgba(16,36,61,.9);paint-order:stroke fill}
.hlC span{color:#D7F044}
.hlC.dark{color:#10243D;text-shadow:0 8px 0 rgba(255,255,255,.7)}
.hlC.dark span{color:#FF7556;text-shadow:0 8px 0 #10243D}
.sticker{position:absolute;font-family:'Bricolage';font-weight:800;color:#10243D;background:#D7F044;border:10px solid #10243D;border-radius:38px;
 box-shadow:14px 14px 0 #10243D;padding:18px 34px;z-index:9;line-height:1}
"""


def C_head(l1, l2, dark=False, accent=None):
    st = f' style="color:{accent}"' if accent else ""
    return f'<div class="hl hlC{" dark" if dark else ""}" data-max="1170">{l1}<br><span{st}>{l2}</span></div>'


def confetti(seed, n=46, palette=("#D7F044", "#FFFFFF", "#FF7556", "#35B8C7", "#FFD145", "#5B2BD9"), area=(0, 0, W, H), z=0):
    rnd = random.Random(seed)
    out = ""
    for _ in range(n):
        x = rnd.randint(area[0], area[2]); y = rnd.randint(area[1], area[3])
        s = rnd.randint(18, 46); c = rnd.choice(palette); r = rnd.randint(0, 360)
        shape = rnd.choice(["border-radius:50%", "border-radius:8px", "clip-path:polygon(50% 0,100% 100%,0 100%)"])
        out += f'<div class="fx" style="left:{x}px;top:{y}px;width:{s}px;height:{int(s * rnd.choice([1, 1, .45]))}px;background:{c};transform:rotate({r}deg);{shape};opacity:.9;z-index:{z}"></div>'
    return out


def C_bg(c1, c2, pat="rgba(255,255,255,.14)"):
    return (f'<div class="abs" style="inset:0;background:linear-gradient(170deg,{c1},{c2})"></div>'
            f'<div class="abs" style="inset:0;background-image:radial-gradient({pat} 5px,transparent 5.5px);background-size:64px 64px"></div>')


def polaroid(path, label, x, y, w, ph, rot, pos="50% 30%", z=3):
    return (f'<div class="abs" style="left:{x}px;top:{y}px;width:{w}px;transform:rotate({rot}deg);background:#fff;padding:16px 16px 68px;border-radius:26px;'
            f'box-shadow:0 30px 50px rgba(5,12,28,.35);z-index:{z}"><div style="height:{ph}px;border-radius:12px;background:url({path}) {pos}/cover"></div>'
            f'<div style="position:absolute;left:0;right:0;bottom:16px;text-align:center;font:800 38px \'Bricolage\';color:#10243D">{label}</div></div>')




# ======================================================================================
# THE STORY (shared by all three sets) - Wii-Sports-simple copy, one idea per screenshot
#   1 controller  : your phone is the controller
#   2 swing       : swing it like a club or racket
#   3 active      : get up, get moving
#   4 friends     : play with your friends
#   5 bigscreen   : big screen, any room
#   6 courses     : golf and tennis for everyone
# Layouts are written once and styled three ways (A resort / B night / C party).
# ======================================================================================
def swoosh(d, color="#fff", width=90, opacity=.8, z=1, dash=None):
    da = f'stroke-dasharray="{dash}"' if dash else ""
    return (f'<svg class="abs" viewBox="0 0 {W} {H}" style="left:0;top:0;width:{W}px;height:{H}px;z-index:{z}">'
            f'<path d="{d}" fill="none" stroke="{color}" stroke-opacity="{opacity}" stroke-width="{width}" stroke-linecap="round" {da}/></svg>')


def arrows(y, color="#fff", z=8):
    """Left/right step arrows: the 'get up and move' cue."""
    ar = lambda x, flip: (f'<svg class="abs" viewBox="0 0 220 120" style="left:{x}px;top:{y}px;width:220px;height:120px;z-index:{z};'
                          f'transform:scaleX({-1 if flip else 1});filter:drop-shadow(0 8px 14px rgba(5,12,28,.4))">'
                          f'<path d="M10 40 H130 V10 L210 60 L130 110 V80 H10Z" fill="{color}"/></svg>')
    return ar(120, True) + ar(980, False)


class Style:
    """name, background builder, headline builder, tag builder, TV glow colours."""
    def __init__(self, key):
        self.key = key

    # backgrounds ------------------------------------------------------------------
    def bg(self, n, img):
        if self.key == "A":
            return A_bg(img)
        if self.key == "B":
            return B_BG
        c = [("#FF8A63", "#E8506E"), ("#EAFB6B", "#8ED94F"), ("#7B4BF0", "#3C1FA8"),
             ("#46C98F", "#1F9A67"), ("#35B8C7", "#1E8FB0"), ("#FFF9EE", "#FBE3B4")][n - 1]
        pat = "rgba(16,36,61,.08)" if n == 2 else ("rgba(255,117,86,.18)" if n == 6 else "rgba(255,255,255,.14)")
        return C_bg(c[0], c[1], pat) + confetti(n * 11, area=(0, 700, W, 2800))

    def head(self, n, kicker, l1, l2, mark):
        if self.key == "A":
            return A_head(kicker, l1, l2, mark)
        if self.key == "B":
            return B_head(kicker, l1, l2.replace(mark, f"<em>{mark}</em>"))
        return C_head(l1, l2, dark=n in (2, 6), accent="#fff" if n in (4, 5) else None)

    def tag(self, text, x, y, kind=0, rot=0, size=40):
        if self.key == "C":
            bg = "#fff" if kind else "#D7F044"
            return (f'<div class="sticker" style="left:{x}px;top:{y}px;font-size:{int(size * 1.35)}px;transform:rotate({rot}deg);background:{bg}">{text}</div>')
        return tag(text, x, y, "#fff" if kind else LIME, "#10243D", rot, size=size)

    @property
    def glow(self):
        return {"A": None, "B": LIME, "C": "#D7F044"}[self.key]

    @property
    def glow2(self):
        return {"A": None, "B": SKY, "C": None}[self.key]


def story(key):
    st = Style(key)
    out = lambda n, name, html: build_page(f"{key}{n}-{name}", {"A": A_CSS, "B": B_CSS, "C": C_CSS}[key], html)
    ctrl = f"{CUR}/golf-controller-artwork.png"

    # 1 - your phone is the controller -------------------------------------------------
    out(1, "controller",
        st.bg(1, f"{UP}/cliff_03.png") + st.head(1, "How it works", "Your phone is", "the controller", "controller")
        + tv("sky_04", 70, 800, 1180, rot=-2, z=2, glow=st.glow)
        + phone(None, 430, 1420, 720, rot=3, z=4, src=ctrl)
        + st.tag("No extra controllers", 70, 1700, 0, -3, 42) + st.tag("Just your phone", 70, 1850, 1, 2, 42)
        + st.tag("Plays on your TV", 70, 2000, 0, -2, 42))

    # 2 - swing it ---------------------------------------------------------------------
    out(2, "swing",
        st.bg(2, G("07")) + st.head(2, "Motion controls", "Swing to", "hit the ball", "hit the ball")
        + swoosh("M-60 2600 C120 1700 760 1250 1260 1180", "#fff", 130, .55, 1)
        + swoosh("M-20 2680 C260 1860 800 1420 1280 1330", LIME if key != "A" else "#35B8C7", 34, .9, 1)
        + tv("cliff_06", 330, 800, 920, rot=3, z=2, glow=st.glow)
        + phone(None, 40, 1330, 640, rot=-12, z=4, src=ctrl)
        + icon("golf", 700, 1760, 560, 8, 5)
        + st.tag("Swing like a club", 640, 1480, 0, 4, 42) + st.tag("Flick like a racket", 560, 2500, 1, -3, 42))

    # 3 - get up and move --------------------------------------------------------------
    out(3, "active",
        st.bg(3, f"{UP}/sky_02.png") + st.head(3, "Get active", "Get up.", "Get moving.", "Get moving.")
        + tv(None, 70, 800, 1180, rot=-2, z=2, glow=st.glow, src=f"{CUR}/tennis_resort.png")
        + tv("sky_04", 220, 1560, 1020, rot=3, z=3, glow=st.glow2)
        + arrows(2330, "#fff")
        + st.tag("Step to chase the ball", 330, 2360, 0, 0, 42)
        + st.tag("Serve, smash, rally", 70, 1480, 1, -4, 40))

    # 4 - friends ----------------------------------------------------------------------
    out(4, "friends",
        st.bg(4, G("12")) + st.head(4, "Multiplayer", "Play with", "your friends", "your friends")
        + tv("sky_03", -80, 780, 980, rot=-6, z=1, glow=st.glow2)
        + phone("route-onlineChoice", 20, 1180, 520, rot=-7, z=2)
        + phone("route-localChoice", 780, 1230, 520, rot=7, z=2)
        + phone("route-party", 340, 1020, 640, z=4)
        + st.tag("2-4 players, one phone", 60, 2450, 0, -2, 38) + st.tag("Online quick match", 560, 2570, 1, 2, 38)
        + st.tag("Nearby lobby", 240, 2690, 0, -1, 38))

    # 5 - big screen -------------------------------------------------------------------
    out(5, "bigscreen",
        st.bg(5, f"{UP}/sky_05.png") + st.head(5, "On your TV", "Big screen,", "any room", "any room")
        + tv("cliff_03", 40, 800, 1240, rot=-2, z=2, glow=st.glow)
        + tv("sky_04", 40, 1540, 1240, rot=2, z=3, glow=st.glow2)
        + phone(None, 760, 2000, 440, rot=6, z=6, src=ctrl)
        + st.tag("AirPlay or cable", 70, 2330, 0, -3, 44) + st.tag("Phone stays your controller", 70, 2480, 1, 2, 38))

    # 6 - courses ----------------------------------------------------------------------
    holes = [("09", "Coastal links"), ("10", "Crater"), ("12", "Lighthouse"), ("17", "Snow"), ("18", "Mesa"), ("22", "Volcano")]
    body = st.bg(6, G("09")) + st.head(6, "Golf and tennis", "Golf and tennis", "for everyone", "for everyone")
    for i, (h, n) in enumerate(holes):
        cx, cy = 80 + (i % 3) * 405, 800 + (i // 3) * 690
        body += card(G(h), cx, cy, 370, 658, (-1.5 if i % 2 == 0 else 1.5), 30, 2, "5px solid #fff", pos="50% 30%")
        body += tag(n, cx + 14, cy + 16, "#fff", "#10243D", 0, 6, 24)
    for i, (img, n) in enumerate([("map-resort.jpg", "Resort"), ("map-skyscraper.jpg", "Sky Tower"), ("map-volcano.jpg", "Volcano")]):
        cx = 80 + i * 405
        body += card(f"{SRC}/art/{img}", cx, 2210, 370, 208, (1.2 if i % 2 == 0 else -1.2), 24, 2, "5px solid #fff")
        body += tag(f"Tennis · {n}", cx + 14, 2226, "#fff", "#10243D", 0, 6, 24)
    body += st.tag("Easy to learn. Fun to win.", 220, 2560, 0, 0, 42)
    out(6, "courses", body)


if __name__ == "__main__":
    which = [a.upper() for a in sys.argv[1:]] or ["A", "B", "C"]
    for k in which:
        story(k)
