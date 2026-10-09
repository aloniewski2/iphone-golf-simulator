"""Set C v2 "Party Pop": colourful, with illustrated players enjoying the game in front of a TV.
Type follows the in-game HUD: Rubik Black with the navy outline used for moments like SMASH!. Copy never limits the game to two sports."""
import sys
from render import page, render, ROOT
import build_screenshots as B
from build_screenshots import (phone, tv, card, icon, tag, build_page, C_CSS, C_bg, C_head, confetti, polaroid,
                               swoosh, arrows, UP, SRC, CUR, G, W, H)
from characters import fig, swoosh_arc

CTRL = f"{CUR}/golf-controller-artwork.png"
LIME, SKY = "#D7F044", "#35B8C7"


def player(outfit, pose, x, y, s=1.0, uid=None, flip=False, z=5, clip=None):
    """clip = visible height in px (hides legs behind a couch)."""
    uid = uid or f"{outfit}{pose}{x}{y}".replace("-", "m")
    h = f"height:{clip}px;overflow:hidden;" if clip else ""
    return (f'<div class="abs" style="left:{x}px;top:{y}px;width:{int(600 * s)}px;height:{int(790 * s) if not clip else int(clip * s)}px;z-index:{z};{h}">'
            f'<div style="width:600px;height:790px;transform:scale({s});transform-origin:0 0">{fig(outfit, pose, uid, flip=flip, shadow=not clip)}</div></div>')


def rug(y, c1, c2, w=1500, h=330):
    return (f'<div class="abs" style="left:{(W - w) // 2}px;top:{y}px;width:{w}px;height:{h}px;border-radius:50%;'
            f'background:radial-gradient(ellipse at center,{c1} 0 55%,{c2} 56% 66%,{c1} 67% 100%);box-shadow:0 30px 60px rgba(16,36,61,.25);z-index:1"></div>')


def floor(y, c1, c2):
    return (f'<div class="abs" style="left:0;top:{y}px;width:{W}px;height:{H - y}px;z-index:0;'
            f'background:repeating-linear-gradient(90deg,rgba(255,255,255,.06) 0 3px,transparent 3px 190px),linear-gradient(180deg,{c1},{c2})"></div>'
            f'<div class="abs" style="left:0;top:{y - 8}px;width:{W}px;height:16px;background:rgba(255,255,255,.35);z-index:0"></div>')


def console(y, w=1040, col="#3C1FA8"):
    return (f'<div class="abs" style="left:{(W - w) // 2}px;top:{y}px;width:{w}px;height:120px;border-radius:32px;'
            f'background:linear-gradient(180deg,#6B4BDE,{col});box-shadow:0 30px 50px rgba(16,36,61,.35);z-index:2"></div>')


def couch(y, c1="#FF7556", c2="#D8543A", w=1240):
    x = (W - w) // 2
    return (f'<div class="abs" style="left:{x}px;top:{y}px;width:{w}px;height:360px;border-radius:90px 90px 40px 40px;background:linear-gradient(180deg,{c1},{c2});z-index:3;box-shadow:0 20px 40px rgba(16,36,61,.3)"></div>'
            f'<div class="abs" style="left:{x - 40}px;top:{y + 190}px;width:150px;height:330px;border-radius:70px;background:linear-gradient(180deg,{c1},{c2});z-index:7;box-shadow:0 20px 40px rgba(16,36,61,.3)"></div>'
            f'<div class="abs" style="left:{x + w - 110}px;top:{y + 190}px;width:150px;height:330px;border-radius:70px;background:linear-gradient(180deg,{c1},{c2});z-index:7;box-shadow:0 20px 40px rgba(16,36,61,.3)"></div>'
            f'<div class="abs" style="left:{x + 60}px;top:{y + 250}px;width:{w - 120}px;height:270px;border-radius:50px;background:linear-gradient(180deg,{c1},{c2});z-index:7;box-shadow:inset 0 10px 0 rgba(255,255,255,.25),0 26px 40px rgba(16,36,61,.3)"></div>')


def sticker(text, x, y, size=62, rot=0, white=False):
    bg = "#fff" if white else "#D7F044"
    return f'<div class="sticker" style="left:{x}px;top:{y}px;font-size:{size}px;transform:rotate({rot}deg);background:{bg}">{text}</div>'


CORAL = ("#FF8A63", "#E8506E")
VIOLET = ("#7B4BF0", "#3C1FA8")


def label(text, x, y, size=40, rot=0, white=False):
    """Small functional overlay (an input or a step), not a marketing line."""
    return sticker(text, x, y, size, rot, white)


def step_arrows(y, x1=480, x2=860, color="#fff", z=8):
    ar = lambda x, flip: (f'<svg class="abs" viewBox="0 0 220 120" style="left:{x}px;top:{y}px;width:200px;height:110px;z-index:{z};'
                          f'transform:scaleX({-1 if flip else 1});filter:drop-shadow(0 8px 14px rgba(5,12,28,.4))">'
                          f'<path d="M10 40 H130 V10 L210 60 L130 110 V80 H10Z" fill="{color}" stroke="#10243D" stroke-width="8" stroke-linejoin="round"/></svg>')
    return ar(x1, True) + ar(x2, False)


def build():
    """Set C v3, written to Apple's App Store asset best practices:
    real gameplay/UI dominate, a short headline, functional overlays only, one palette (coral/violet), key content in the safe area."""
    out = lambda n, name, html: build_page(f"C{n}-{name}", C_CSS, html)
    dots = lambda n, seed: confetti(seed, n=n, area=(40, 620, W - 80, 2550))

    # 1 - the controller, in use: game on the TV, controller in hand
    out(1, "controller",
        C_bg(*CORAL) + dots(10, 11) + C_head("Your phone is", "the controller")
        + tv("sky_04", 100, 640, 1120, rot=-1.5, z=2, glow="#D7F044")
        + swoosh("M300 1930 C240 1760 280 1520 420 1360", "#fff", 18, .95, 9, "2 38")
        + phone(None, 560, 1240, 600, rot=3, z=4, src=CTRL)
        + player("teal", "forehand", 20, 1830, .64, z=6))

    # 2 - swing: the shot you actually take (HUD, then the controls that make it)
    out(2, "swing",
        C_bg(*VIOLET) + dots(10, 22) + C_head("Swing to", "hit the ball")
        + swoosh("M30 2500 C70 2000 330 1560 640 1330", "#fff", 120, .35, 1)
        + tv("cliff_06", 170, 640, 980, rot=1.5, z=2, glow="#D7F044")
        + phone(None, 500, 1250, 600, rot=-3, z=4, src=CTRL)
        + label("Pick a club", 40, 2000, 36, -3) + label("Drag to aim", 40, 2270, 36, 3, True) + label("Swing to hit", 40, 1520, 36, -3))

    # 3 - move: the court on the TV, the player stepping
    out(3, "active",
        C_bg(*CORAL) + dots(10, 33) + C_head("Get up.", "Get moving.")
        + tv(None, 100, 640, 1120, rot=-1.5, z=2, glow="#D7F044", src=f"{CUR}/tennis_resort.png")
        + tv("sky_04", 360, 1410, 860, rot=2, z=3)
        + player("navy", "lunge", -30, 1930, .8, z=6)
        + step_arrows(2450, 540, 880) + label("Step side to side", 520, 2340, 38, -2))

    # 4 - friends: the real multiplayer options, a friendly room under them
    out(4, "friends",
        C_bg(*VIOLET) + dots(10, 44) + C_head("Play with", "your friends")
        + tv("sky_03", 230, 640, 860, rot=-2, z=1, glow="#D7F044")
        + phone("route-onlineChoice", 70, 1260, 340, rot=-4, z=3) + phone("route-localChoice", 910, 1270, 340, rot=4, z=3)
        + phone("route-party", 450, 1190, 410, z=5)
        + player("teal", "ready", 60, 2170, .6, z=5, clip=300) + player("pink", "cheer", 410, 2120, .64, z=5, clip=340)
        + player("lime", "forehand", 770, 2170, .6, z=5, clip=300)
        + couch(2290, "#FF7556", "#D8543A", 1100))

    # 5 - big screen: how it connects (real UI -> AirPlay/cable -> TV), with players using their phones
    out(5, "bigscreen",
        C_bg(*CORAL) + dots(8, 55) + C_head("Big screen,", "any room")
        + phone("home-female-tennis", 90, 680, 400, rot=-5, z=6)
        + swoosh("M520 1000 C600 920 690 920 750 960", "#fff", 22, .95, 5, "2 40")
        + swoosh("M930 1150 C990 1270 970 1370 930 1470", "#fff", 22, .95, 5, "2 40")
        + airplay(700, 830, .95)
        + tv("cliff_03", 200, 1600, 1060, rot=2, z=3, glow="#D7F044")
        + label("1 · Open the game", 60, 1560, 36, -3, True) + label("2 · AirPlay or cable", 590, 1180, 38, 3)
        + label("3 · Play big", 420, 2290, 40, -2, True)
        + player("coral", "cheer", 40, 2330, .62, z=6) + player("violet", "forehand", 760, 2360, .6, z=6))

    # 6 - places to play (real captures, nothing but place names)
    holes = [("09", "Coastal links"), ("10", "Crater"), ("12", "Lighthouse"), ("17", "Snow"), ("18", "Mesa"), ("22", "Volcano")]
    body = C_bg(*VIOLET) + dots(8, 66) + C_head("Wild places", "to play")
    for i, (h, n) in enumerate(holes):
        cx, cy = 80 + (i % 3) * 405, 690 + (i // 3) * 730
        body += card(G(h), cx, cy, 370, 700, (-1.5 if i % 2 == 0 else 1.5), 30, 2, "5px solid #fff", pos="50% 30%")
        body += tag(n, cx + 14, cy + 16, "#fff", "#10243D", 0, 6, 24)
    for i, (img, n) in enumerate([("map-resort.jpg", "Resort"), ("map-skyscraper.jpg", "Sky Tower"), ("map-volcano.jpg", "Volcano")]):
        cx = 80 + i * 405
        body += card(f"{SRC}/art/{img}", cx, 2200, 370, 250, (1.2 if i % 2 == 0 else -1.2), 24, 2, "5px solid #fff")
        body += tag(n, cx + 14, 2216, "#fff", "#10243D", 0, 6, 24)
    out(6, "places", body)


def swoosh_big():
    return (swoosh("M-60 2800 C80 2100 640 1500 1120 1260", "#fff", 140, .55, 1)
            + swoosh("M-20 2860 C160 2180 700 1620 1160 1420", "#10243D", 28, .16, 1))


def speed_lines():
    ls = ""
    for i, (y, w, op) in enumerate([(1500, 520, .5), (1650, 640, .4), (1810, 560, .5), (1960, 700, .35), (2120, 540, .45)]):
        ls += f'<div class="abs" style="left:{40 + (i % 2) * 40}px;top:{y}px;width:{w}px;height:20px;border-radius:99px;background:#fff;opacity:{op};z-index:3"></div>'
    return ls


def airplay(x, y, s=1.0):
    """AirPlay-style badge: screen with a triangle rising from the bottom edge."""
    return (f'<svg class="abs" viewBox="0 0 120 112" style="left:{x}px;top:{y}px;width:{int(300 * s)}px;height:{int(280 * s)}px;z-index:9;filter:drop-shadow(10px 12px 0 rgba(16,36,61,.9))">'
            f'<rect x="6" y="6" width="108" height="72" rx="14" fill="#D7F044" stroke="#10243D" stroke-width="7"/>'
            f'<path d="M60 52 L96 104 H24Z" fill="#fff" stroke="#10243D" stroke-width="7" stroke-linejoin="round"/></svg>')


if __name__ == "__main__":
    build()
