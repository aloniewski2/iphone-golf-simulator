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


def build():
    out = lambda n, name, html: build_page(f"C{n}-{name}", C_CSS, html)

    # 1 - your phone is the controller: the whole story in one picture
    out(1, "controller",
        C_bg("#FF8A63", "#E8506E") + floor(1640, "#C93D63", "#9E2A50") + confetti(11, area=(0, 700, W, 1500)) + C_head("Your phone is", "the controller")
        + tv("sky_04", 70, 800, 1180, rot=-1.5, z=2, glow="#D7F044") + console(1520)
        + rug(2250, LIME, "#fff")
        + swoosh("M330 1700 C240 1620 250 1560 330 1500", "#fff", 18, .9, 9, "2 34")
        + player("teal", "forehand", -70, 1570, .98, z=5) + player("navy", "lunge", 700, 1600, .9, z=5, flip=True)
        + player("pink", "cheer", 300, 1500, 1.02, z=6)
        + sticker("No extra controllers", 70, 2560, 60, -3) + sticker("Just your phone", 640, 2650, 56, 3, True))

    # 2 - swing it
    out(2, "swing",
        C_bg("#EAFB6B", "#8ED94F", "rgba(16,36,61,.08)") + confetti(22, area=(0, 700, W, 2800)) + C_head("Swing it,", "hit it!", dark=True)
        + swoosh_big()
        + phone(None, 640, 900, 560, rot=6, z=4, src=CTRL)
        + player("coral", "golf", -60, 1650, 1.3, z=5, clip=None)
        + player("violet", "forehand", 470, 1780, 1.05, z=6)
        + sticker("Swing like a club", 60, 960, 54, -5) + sticker("Flick like a racket", 700, 2540, 54, 4, True))

    # 3 - get up and move
    out(3, "active",
        C_bg("#7B4BF0", "#3C1FA8") + floor(2180, "#2A1480", "#1B0F5E") + confetti(33, area=(0, 700, W, 2800), z=1) + C_head("Get up.", "Get moving.")
        + tv(None, 70, 800, 1180, rot=-1.5, z=2, glow="#D7F044", src=f"{CUR}/tennis_resort.png")
        + speed_lines()
        + player("navy", "lunge", 280, 1380, 1.55, z=5)
        + arrows(2650, "#fff")
        + sticker("Step to chase the ball", 250, 2630, 56, -2))

    # 4 - friends on the couch
    out(4, "friends",
        C_bg("#46C98F", "#1F9A67") + floor(1520, "#17805A", "#0F5E43") + confetti(44, area=(0, 700, W, 2000)) + C_head("Play with", "your friends")
        + tv("sky_03", 160, 800, 1000, rot=-2, z=2, glow="#D7F044") + console(1400, 900, "#14735A")
        + player("teal", "ready", 60, 1590, .95, z=5, clip=440) + player("pink", "cheer", 380, 1520, 1.0, z=5, clip=520)
        + player("lime", "forehand", 700, 1590, .95, z=5, clip=440)
        + couch(1780, "#FF7556", "#D8543A")
        + phone("route-onlineChoice", 90, 2330, 400, rot=-5, z=8) + phone("route-localChoice", 830, 2330, 400, rot=5, z=8)
        + phone("route-party", 450, 2250, 420, rot=0, z=9)
        + sticker("2-4 players, one phone", 60, 2280, 44, -3, True))

    # 5 - big screen: a how-it-works flow (phone -> AirPlay/cable -> TV), deliberately unlike the couch scene in 4
    out(5, "bigscreen",
        C_bg("#35B8C7", "#1E8FB0") + confetti(55, area=(0, 700, W, 2300)) + C_head("Big screen,", "any room")
        + phone("home-female-tennis", 70, 840, 380, rot=-5, z=6)
        + swoosh("M480 1180 C560 1090 640 1090 700 1130", "#fff", 22, .95, 5, "2 40")
        + swoosh("M900 1270 C960 1380 930 1470 880 1560", "#fff", 22, .95, 5, "2 40")
        + airplay(640, 1000, .95)
        + tv("cliff_03", 400, 1590, 880, rot=2, z=3, glow="#D7F044")
        + sticker("1 · Open the game", 60, 1720, 42, -3, True)
        + sticker("2 · AirPlay or cable", 560, 1330, 46, 3)
        + sticker("3 · Play big", 430, 2200, 50, -2, True)
        + player("coral", "cheer", -60, 2150, .78, z=6) + player("violet", "forehand", 780, 2200, .74, z=6)
        + sticker("Phone stays your controller", 110, 2700, 48, 1))

    # 6 - fun for everyone
    out(6, "everyone",
        C_bg("#FFF9EE", "#FBE3B4", "rgba(255,117,86,.18)") + floor(2000, "#EFC48F", "#D9A066") + confetti(66, area=(0, 700, W, 1900)) + C_head("Fun for", "everyone!", dark=True)
        + rug(2330, "#FF8A63", "#fff")
        + player("lime", "cheer", -90, 1420, 1.0, z=5) + player("navy", "ready", 210, 1700, .78, z=6)
        + player("pink", "forehand", 440, 1380, 1.0, z=5) + player("coral", "ready", 800, 1720, .75, z=6)
        + player("violet", "cheer", 760, 1330, 1.0, z=4) + player("teal", "golf", 1000, 1760, .62, z=6)
        + sticker("Easy to learn. Fun to win.", 150, 2600, 56, -2))


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
