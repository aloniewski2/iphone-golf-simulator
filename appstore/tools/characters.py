"""Vector character kit: chunky, friendly players in the spirit of the in-game characters
(round bald heads, sport outfits in the game's colours), posed holding a phone.

Each figure is an SVG fragment in a 520x760 box. Poses are joint coordinates, so more can be added.
    fig(outfit, pose, uid)  -> '<svg ...>...</svg>'
"""
import math

SKIN = {  # (base, shade)
    "light": ("#F7C9A9", "#E39F7C"),
    "warm": ("#F0B088", "#D68A60"),
    "tan": ("#D9976A", "#B87449"),
    "deep": ("#A8694A", "#7F4A31"),
}
OUT = "#1B2438"  # outline ink

# outfits mirror the in-game kit colours (teal polo / pink top / lime polo + white skirt / etc.)
OUTFITS = {
    "teal":  dict(skin="warm", top="#1FB5A8", top2="#118D83", bot="pants", botc="#1F2D55", shoe="#F4F4F6", acc="#1F2D55"),
    "lime":  dict(skin="tan", top="#C9F05A", top2="#9CC62E", bot="skirt", botc="#FFF6E6", shoe="#FFFFFF", acc="#35B8C7"),
    "pink":  dict(skin="light", top="#FF8FB0", top2="#E8638A", bot="skirt", botc="#223A7A", shoe="#F4F4F6", acc="#223A7A"),
    "navy":  dict(skin="deep", top="#2E4FD8", top2="#2139A8", bot="shorts", botc="#F2F2F6", shoe="#FFFFFF", acc="#D7F044"),
    "coral": dict(skin="light", top="#FF7556", top2="#D8543A", bot="shorts", botc="#1F2D55", shoe="#F4F4F6", acc="#FFD145"),
    "violet": dict(skin="tan", top="#7B4BF0", top2="#5B2BD9", bot="skirt", botc="#F2F2F6", shoe="#FFFFFF", acc="#D7F044"),
}

# joints: head, neck, shoulders (l/r), elbows, wrists, hips, knees, ankles. "l" is the viewer's left.
# phone: (x, y, angle, hand) hand: 'r', 'l' or 'both'
POSES = {
    "forehand": dict(head=(250, 128), neck=(255, 196), sh_l=(212, 232), sh_r=(300, 228),
                     el_l=(150, 300), wr_l=(104, 372), el_r=(392, 246), wr_r=(458, 168),
                     hip_l=(222, 432), hip_r=(292, 432), kn_l=(184, 566), an_l=(132, 700),
                     kn_r=(330, 562), an_r=(392, 700), phone=(474, 140, 38, "r"), look=(1, -1), tilt=-4),
    "golf": dict(head=(262, 152), neck=(262, 220), sh_l=(218, 254), sh_r=(304, 248),
                 el_l=(250, 348), wr_l=(318, 414), el_r=(354, 342), wr_r=(332, 408),
                 hip_l=(222, 450), hip_r=(292, 446), kn_l=(200, 578), an_l=(160, 704),
                 kn_r=(312, 574), an_r=(350, 704), phone=(332, 432, -18, "both"), look=(1, 1), tilt=3),
    "cheer": dict(head=(260, 128), neck=(260, 196), sh_l=(214, 232), sh_r=(306, 232),
                  el_l=(146, 168), wr_l=(104, 66), el_r=(376, 168), wr_r=(418, 70),
                  hip_l=(226, 430), hip_r=(296, 430), kn_l=(204, 560), an_l=(176, 668),
                  kn_r=(318, 552), an_r=(346, 660), phone=(424, 36, 18, "r"), look=(0, -1), tilt=0),
    "lunge": dict(head=(286, 140), neck=(280, 206), sh_l=(236, 244), sh_r=(322, 238),
                  el_l=(168, 296), wr_l=(110, 330), el_r=(408, 262), wr_r=(484, 236),
                  hip_l=(214, 448), hip_r=(290, 440), kn_l=(144, 600), an_l=(64, 702),
                  kn_r=(402, 548), an_r=(414, 702), phone=(500, 214, 22, "r"), look=(1, 0), tilt=6),
    "ready": dict(head=(260, 130), neck=(260, 198), sh_l=(216, 236), sh_r=(304, 236),
                  el_l=(176, 340), wr_l=(228, 330), el_r=(344, 340), wr_r=(292, 330),
                  hip_l=(226, 436), hip_r=(294, 436), kn_l=(206, 570), an_l=(190, 704),
                  kn_r=(314, 570), an_r=(330, 704), phone=(260, 316, 0, "both"), look=(1, -1), tilt=0),
    "sit": dict(head=(260, 150), neck=(260, 218), sh_l=(216, 254), sh_r=(304, 254),
                el_l=(190, 350), wr_l=(238, 332), el_r=(330, 350), wr_r=(284, 332),
                hip_l=(232, 470), hip_r=(290, 470), kn_l=(262, 490), an_l=(300, 640),
                kn_r=(420, 480), an_r=(420, 650), phone=(262, 316, 0, "both"), look=(1, -1), tilt=0, seated=True),
}


def _lerp(a, b, t):
    return (a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t)


def _path(pts):
    return "M" + " L".join(f"{x:.1f},{y:.1f}" for x, y in pts)


def _limb(pts, w, col, out=OUT):
    d = _path(pts)
    return (f'<path d="{d}" fill="none" stroke="{out}" stroke-width="{w + 10}" stroke-linecap="round" stroke-linejoin="round"/>'
            f'<path d="{d}" fill="none" stroke="{col}" stroke-width="{w}" stroke-linecap="round" stroke-linejoin="round"/>')


def _phone(x, y, ang, uid):
    return (f'<g transform="translate({x} {y}) rotate({ang})">'
            f'<circle r="86" fill="url(#glow{uid})"/>'
            f'<rect x="-27" y="-52" width="54" height="104" rx="13" fill="#0E1118" stroke="{OUT}" stroke-width="5"/>'
            f'<rect x="-21" y="-45" width="42" height="90" rx="9" fill="url(#scr{uid})"/>'
            f'<rect x="-8" y="-43" width="16" height="5" rx="2.5" fill="#0E1118"/>'
            f'<circle cx="0" cy="12" r="11" fill="#fff" fill-opacity=".9"/><circle cx="0" cy="12" r="5" fill="#2E8AE0"/></g>')


def fig(outfit="teal", pose="forehand", uid="a", flip=False, shadow=True):
    o = OUTFITS[outfit]
    p = POSES[pose]
    sk, sk2 = SKIN[o["skin"]]
    L = []
    defs = (f'<defs><radialGradient id="skin{uid}" cx="38%" cy="32%" r="85%"><stop offset="0" stop-color="{sk}"/><stop offset="1" stop-color="{sk2}"/></radialGradient>'
            f'<radialGradient id="glow{uid}"><stop offset="0" stop-color="#D7F044" stop-opacity=".75"/><stop offset="1" stop-color="#D7F044" stop-opacity="0"/></radialGradient>'
            f'<linearGradient id="scr{uid}" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#6FD3E3"/><stop offset="1" stop-color="#2E8AE0"/></linearGradient>'
            f'<linearGradient id="top{uid}" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="{o["top"]}"/><stop offset="1" stop-color="{o["top2"]}"/></linearGradient></defs>')
    seated = p.get("seated")
    ground = 700
    if shadow and not seated:
        L.append(f'<ellipse cx="260" cy="{ground + 22}" rx="200" ry="26" fill="#10243D" fill-opacity=".22"/>')

    # legs
    legs = {}
    for s in "lr":
        legs[s] = [p[f"hip_{s}"], p[f"kn_{s}"], p[f"an_{s}"]]
    bot, botc = o["bot"], o["botc"]
    for s in "lr":
        hp, kn, an = legs[s]
        if bot == "pants":
            L.append(_limb([hp, kn, an], 64, botc))
        else:
            L.append(_limb([kn, an], 46, sk))
            L.append(_limb([hp, kn], 52, sk))
            if bot == "shorts":
                L.append(_limb([hp, _lerp(hp, kn, .8)], 72, botc))
        # shoes
        d = 1 if s == "r" else -1
        L.append(f'<ellipse cx="{an[0] + d * 18:.0f}" cy="{an[1] + 14:.0f}" rx="54" ry="26" fill="{OUT}"/>'
                 f'<ellipse cx="{an[0] + d * 18:.0f}" cy="{an[1] + 11:.0f}" rx="48" ry="21" fill="{o["shoe"]}"/>'
                 f'<ellipse cx="{an[0] + d * 18 - 12:.0f}" cy="{an[1] + 3:.0f}" rx="22" ry="7" fill="#fff" fill-opacity=".7"/>'
                 f'<path d="M{an[0] - 20 + d * 16:.0f},{an[1] + 12:.0f} h40" stroke="{o["acc"]}" stroke-width="6" stroke-linecap="round"/>')
    if bot == "skirt":
        hl, hr = p["hip_l"], p["hip_r"]
        yb = max(hl[1], hr[1])
        pts = [(hl[0] - 30, yb - 30), (hr[0] + 30, yb - 30), (hr[0] + 82, yb + 96), (hl[0] - 82, yb + 96)]
        L.append(f'<path d="{_path(pts)} Z" fill="{botc}" stroke="{OUT}" stroke-width="9" stroke-linejoin="round"/>')
        for k in range(1, 6):
            x0 = pts[0][0] + (pts[1][0] - pts[0][0]) * k / 6
            x1 = pts[3][0] + (pts[2][0] - pts[3][0]) * k / 6
            L.append(f'<path d="M{x0:.0f},{yb - 24} L{x1:.0f},{yb + 92}" stroke="{OUT}" stroke-opacity=".18" stroke-width="5"/>')

    # torso
    tl, tr = p["sh_l"], p["sh_r"]
    bl, br = (p["hip_l"][0] + 12, p["hip_l"][1]), (p["hip_r"][0] - 12, p["hip_r"][1])
    tp = f'{_path([tl, tr, br, bl])} Z'
    L.append(f'<path d="{tp}" fill="{OUT}" stroke="{OUT}" stroke-width="72" stroke-linejoin="round"/>')
    L.append(f'<path d="{tp}" fill="url(#top{uid})" stroke="url(#top{uid})" stroke-width="62" stroke-linejoin="round"/>')
    L.append(f'<ellipse cx="{tl[0] + 26:.0f}" cy="{tl[1] + 52:.0f}" rx="18" ry="62" fill="#fff" fill-opacity=".22" transform="rotate(8 {tl[0]:.0f} {tl[1]:.0f})"/>')
    # collar + belt
    nx, ny = p["neck"]
    L.append(f'<path d="M{nx - 34},{ny + 14} L{nx},{ny + 54} L{nx + 34},{ny + 14}" fill="none" stroke="#fff" stroke-width="14" stroke-linecap="round" stroke-linejoin="round" stroke-opacity=".9"/>')
    mid = _lerp(bl, br, .5)
    L.append(f'<path d="M{bl[0] - 14},{bl[1] - 6} L{br[0] + 14},{br[1] - 6}" stroke="{o["acc"]}" stroke-width="12" stroke-linecap="round"/>')

    # neck + head
    hx, hy = p["head"]
    L.append(_limb([(hx, hy + 50), p["neck"]], 46, sk2))
    L.append(f'<circle cx="{hx - 76}" cy="{hy + 8}" r="22" fill="{sk2}" stroke="{OUT}" stroke-width="6"/><circle cx="{hx + 76}" cy="{hy + 8}" r="22" fill="{sk2}" stroke="{OUT}" stroke-width="6"/>')
    L.append(f'<circle cx="{hx}" cy="{hy}" r="78" fill="url(#skin{uid})" stroke="{OUT}" stroke-width="7"/><ellipse cx="{hx - 24}" cy="{hy - 46}" rx="30" ry="14" fill="#fff" fill-opacity=".28" transform="rotate(-24 {hx - 24} {hy - 46})"/>')
    lx, ly = p.get("look", (0, 0))
    for sx in (-30, 30):
        ex, ey = hx + sx, hy - 4
        L.append(f'<ellipse cx="{ex}" cy="{ey}" rx="14" ry="19" fill="#fff" stroke="{OUT}" stroke-width="4"/>'
                 f'<ellipse cx="{ex + lx * 4}" cy="{ey + ly * 4 + 1}" rx="8.5" ry="12" fill="#1B2430"/>'
                 f'<circle cx="{ex + lx * 4 + 2}" cy="{ey + ly * 4 - 3}" r="2.6" fill="#fff"/>')
        L.append(f'<path d="M{ex - 14},{ey - 26} Q{ex},{ey - 34} {ex + 14},{ey - 25}" fill="none" stroke="{OUT}" stroke-width="6" stroke-linecap="round"/>')
    L.append(f'<ellipse cx="{hx - 44}" cy="{hy + 22}" rx="12" ry="8" fill="#FF6B7A" fill-opacity=".38"/><ellipse cx="{hx + 44}" cy="{hy + 22}" rx="12" ry="8" fill="#FF6B7A" fill-opacity=".38"/>')
    L.append(f'<path d="M{hx - 28},{hy + 26} Q{hx},{hy + 62} {hx + 28},{hy + 26} Z" fill="#7A2230" stroke="{OUT}" stroke-width="5" stroke-linejoin="round"/>'
             f'<path d="M{hx - 22},{hy + 28} Q{hx},{hy + 34} {hx + 22},{hy + 28} L{hx + 20},{hy + 36} Q{hx},{hy + 40} {hx - 20},{hy + 36} Z" fill="#fff"/>'
             f'<ellipse cx="{hx}" cy="{hy + 48}" rx="12" ry="6" fill="#FF8C98"/>')

    # arms (sleeves over upper arm, hands at the wrists)
    hand = p["phone"][3]
    for s in "lr":
        sh, el, wr = p[f"sh_{s}"], p[f"el_{s}"], p[f"wr_{s}"]
        L.append(_limb([sh, el, wr], 42, sk))
        L.append(_limb([sh, _lerp(sh, el, .8)], 58, o["top"]))
        L.append(f'<circle cx="{wr[0]}" cy="{wr[1]}" r="26" fill="{sk}" stroke="{OUT}" stroke-width="6"/>')
    px, py, pa, _h = p["phone"]
    L.append(_phone(px, py, pa, uid))
    # fingers over the phone so it looks held
    for s in ("r",) if hand == "r" else ("l",) if hand == "l" else ("l", "r"):
        wr = p[f"wr_{s}"]
        L.append(f'<circle cx="{wr[0]}" cy="{wr[1]}" r="23" fill="{sk}" stroke="{OUT}" stroke-width="6"/>')

    inner = "".join(L)
    tf = f' transform="translate(520 0) scale(-1 1)"' if flip else ""
    rot = p.get("tilt", 0)
    return (f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="-30 -20 600 790" width="600" height="790" style="overflow:visible">'
            f'{defs}<g{tf}><g transform="rotate({rot} 260 700)">{inner}</g></g></svg>')


def swoosh_arc(d, color="#fff", w=34, op=.9):
    return f'<path d="{d}" fill="none" stroke="{color}" stroke-opacity="{op}" stroke-width="{w}" stroke-linecap="round"/>'


if __name__ == "__main__":
    from render import page, render
    items = [("teal", "forehand"), ("lime", "golf"), ("pink", "cheer"), ("navy", "lunge"), ("coral", "ready"), ("violet", "sit")]
    body = "".join(f'<div style="position:absolute;left:{i * 420}px;top:20px;width:420px">{fig(o, p, str(i))}</div>' for i, (o, p) in enumerate(items))
    render(page(body, "body{background:#7AD3E0}", 2520, 880), "chars-test", 2520, 880)
