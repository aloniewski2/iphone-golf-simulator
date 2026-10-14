"""SWING COMPARE: our in-game golf swing (frames/<sex>_<club>/<view>_#####.jpg from SwingFilm.cs, 60 fps game clock)
next to the reference swings (reference/raw/*.jpg: Browser-pane contact sheets of the two YouTube Shorts, 2x4 cells).
    python3 scripts/compose.py            -> sheets/*.jpg, film/*.mp4
The reference images stay local (third-party footage, for comparison only)."""
import os, subprocess, glob, shutil
from PIL import Image, ImageDraw, ImageFont

W = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ENC = os.path.join(W, "..", "emotes", "scripts", "enc")
B = "/System/Library/Fonts/Supplemental/Arial Bold.ttf"
R = "/System/Library/Fonts/Supplemental/Arial.ttf"
f_big, f_mid, f_small, f_tiny = (ImageFont.truetype(B, s) for s in (34, 24, 19, 16))
f_reg = ImageFont.truetype(R, 19)
os.makedirs(f"{W}/sheets", exist_ok=True); os.makedirs(f"{W}/film", exist_ok=True)

PHASES = ["Address", "Takeaway", "Top", "Halfway down", "Impact", "Release", "Follow-through", "Finish"]
# our phase frames (60 fps game clock; trace.csv): takeaway = shaft ~parallel going back, top = end of the phone's backswing,
# halfway down = club head highest in the downswing, impact = ball launch, release = shaft ~parallel after impact,
# follow-through = club vertical, finish = last frame of the clip
OURS = [30, 49, 80, 92, 103, 107, 114, 164]
VIEWS = [("faceon", "Face-on"), ("broadcast", "Broadcast tee cam (behind, 25 deg)"), ("dtl", "Down the line"),
         ("q_front_trail", "3/4 front (trail side)"), ("target", "From the target"), ("q_front_target", "3/4 front (target side)"),
         ("overhead", "Overhead"), ("game", "Game camera (TV)")]
SHORT = "YouTube EndA6Mw2S4w"
PROS = [  # name, sheet, clip id in the Short, label times (s)
    ("Ludvig Aberg (#8)", "aberg", "S16", "31.85 32.35 32.80 32.89 32.99 33.05 33.15 33.62"),
    ("Tom Kim (#9)", "kim", "S12", "23.65 24.05 24.60 24.71 24.81 24.87 25.00 25.33"),
    ("Max Homa (#10)", "homa", "S10", "19.30 19.95 20.53 20.63 20.73 20.80 20.93 21.25"),
    ("Vijay Singh (#7)", "singh", "S19", "37.45 37.62 38.03 38.13 38.22 38.30 38.47 38.95"),
    ("Patrick Cantlay (#12)", "cantlay", "S2", "3.55 4.05 4.42 4.52 4.63 4.72 4.82 5.25"),
    ("Xander Schauffele (#11)", "schauffele", "S6", "11.65 12.05 12.58 12.70 12.81 12.88 12.98 13.42"),
]
OUR_C, PRO_C = (24, 120, 200), (70, 70, 78)


def sheet_cells(path, n=8, cols=2, rows=4):
    """The cells of a 560x1562 Browser-pane contact sheet (30 px title, then cols x rows), letterbox trimmed."""
    im = Image.open(path).convert("RGB")
    cw, ch = im.width / cols, (im.height - 30) / rows
    out = []
    for i in range(n):
        x, y = (i % cols) * cw, 30 + (i // cols) * ch
        c = im.crop((round(x), round(y), round(x + cw), round(y + ch)))
        px = c.load(); w, h = c.size
        def blank_row(yy): return all(max(abs(v - 17) for v in px[xx, yy]) < 6 for xx in range(110, w, 7))
        def blank_col(xx): return all(max(abs(v - 17) for v in px[xx, yy]) < 6 for yy in range(30, h, 7))
        top = 0
        while top < h - 1 and blank_row(top): top += 1
        bot = h - 1
        while bot > top and blank_row(bot): bot -= 1
        left = 0
        while left < w - 1 and blank_col(left): left += 1
        right = w - 1
        while right > left and blank_col(right): right -= 1
        out.append(c.crop((left, max(0, top - 24 if top > 24 else 0), right + 1, bot + 1)))
    return out


def fit(im, w, h, bg=(16, 16, 20)):
    """im scaled to fit w x h, centred."""
    s = min(w / im.width, h / im.height)
    r = im.resize((max(1, round(im.width * s)), max(1, round(im.height * s))), Image.LANCZOS)
    cell = Image.new("RGB", (w, h), bg); cell.paste(r, ((w - r.width) // 2, (h - r.height) // 2))
    return cell


def ours(view, f, sex="male", club="Driver"):
    return Image.open(f"{W}/frames/{sex}_{club}/{view}_{f:05d}.jpg").convert("RGB")


def our_crop(view, im):
    """Tighter framing of our portrait renders so the golfer fills a cell like the broadcast crops do."""
    if view == "game": return im.crop((400, 120, 900, 720))
    if view == "overhead": return im
    if view in ("broadcast", "dtl"): return im.crop((0, 60, 600, 860))
    if view == "target": return im.crop((120, 60, 720, 860))
    return im.crop((60, 70, 660, 870))


def label(d, xy, text, font, fill=(255, 255, 255), bg=None, pad=6):
    if bg:
        x0, y0, x1, y1 = d.textbbox(xy, text, font=font)
        d.rectangle([x0 - pad, y0 - pad + 2, x1 + pad, y1 + pad], fill=bg)
    d.text(xy, text, font=font, fill=fill)


def grid_sheet(rows, title, sub, out, cw=240, ch=330, lw=250):
    """rows: [(row label, row sublabel, colour, [8 PIL images], [8 cell captions])]"""
    head = 110
    im = Image.new("RGB", (lw + cw * 8, head + 36 + ch * len(rows)), (14, 14, 18)); d = ImageDraw.Draw(im)
    label(d, (16, 14), title, f_big); label(d, (16, 60), sub, f_reg, fill=(200, 200, 210))
    for c, p in enumerate(PHASES):
        x = lw + c * cw; tw = d.textlength(p, font=f_mid); label(d, (x + (cw - tw) / 2, head + 4), p, f_mid, fill=(255, 220, 90))
    for r, (name, subname, colour, cells, caps) in enumerate(rows):
        y = head + 36 + r * ch
        d.rectangle([0, y, lw - 6, y + ch - 6], fill=colour)
        yy = y + 12
        for line in name.split("\n"): label(d, (12, yy), line, f_mid); yy += 30
        for line in subname.split("\n"): label(d, (12, yy + 4), line, f_tiny, fill=(225, 225, 235)); yy += 22
        for c, cell in enumerate(cells):
            x = lw + c * cw
            im.paste(fit(cell, cw - 6, ch - 6), (x, y))
            if caps and caps[c]: label(d, (x + 6, y + ch - 32), caps[c], f_tiny, fill=(255, 255, 255), bg=(0, 0, 0), pad=3)
    im.save(out, quality=90)
    print(out, im.size)


# ------------------------------------------------------------------ 1. our swing, every angle
rows = []
for v, vname in VIEWS:
    rows.append((vname.split(" (")[0], ("(" + vname.split(" (")[1]) if " (" in vname else "", OUR_C,
                 [our_crop(v, ours(v, f)) for f in OURS], [f"f{f}" for f in OURS]))
grid_sheet(rows, "OUR SWING (male, driver) - in-game, 8 angles",
           "Unity Golf scene, Cliffside hole 1 tee, GolferView driven like the phone: 0.80 s backswing via OnLoad, then StrikeToward -> Strike (0.38 s downswing). Frames at a 60 fps game clock.",
           f"{W}/sheets/01_our_swing_all_angles.jpg", cw=230, ch=300, lw=230)

# female + iron, face-on and broadcast-ish
rows = []
for sex, club in [("male", "Driver"), ("female", "Driver"), ("male", "Iron")]:
    for v in ["faceon", "dtl"]:
        rows.append((f"{sex.title()} {club}", dict(VIEWS)[v], OUR_C, [our_crop(v, ours(v, f, sex, club)) for f in OURS], None))
grid_sheet(rows, "OUR SWING - male vs female, driver vs iron (same clip timing)",
           "Both bodies share the Drive / IronSwing clips (golfer_m/f.fbx); iron frames use the same frame numbers (its top is 1.80 s, impact 2.20 s).",
           f"{W}/sheets/05_our_male_female_iron.jpg", cw=230, ch=300, lw=230)

# ------------------------------------------------------------------ 2. face-on: ours vs Adam Scott
scott = sheet_cells(f"{W}/reference/raw/phases_scott.jpg")
grid_sheet([("OURS", "face-on camera\nin-game, 60 fps", OUR_C, [our_crop("faceon", ours("faceon", f)) for f in OURS], [f"f{f}  {(f - 103) / 60:+.2f}s" for f in OURS]),
            ("Adam Scott", "face-on, driver\nYouTube h-JQsjjsE6Y", PRO_C, scott,
             [f"{t}s  {float(t) - 12.95:+.2f}s" for t in "11.90 12.27 12.70 12.85 12.97 13.03 13.13 13.50".split()])],
           "FACE-ON: our swing vs Adam Scott (driver)",
           "Times under each frame are seconds from impact. Ours: top -0.38 s, settled finish +0.62 s (clip ends +1.02 s). Scott: top -0.21 s, finish ~+0.35 s.",
           f"{W}/sheets/02_faceon_ours_vs_scott.jpg", cw=270, ch=370, lw=230)

# ------------------------------------------------------------------ 3. broadcast angle: ours vs the six pros
rows = [("OURS", "broadcast tee cam\n(behind, 25 deg)", OUR_C, [our_crop("broadcast", ours("broadcast", f)) for f in OURS], [f"f{f}" for f in OURS]),
        ("OURS", "straight down\nthe line", OUR_C, [our_crop("dtl", ours("dtl", f)) for f in OURS], [f"f{f}" for f in OURS])]
for name, key, clip, times in PROS:
    rows.append((name.replace(" (", "\n("), f"{SHORT} {clip}", PRO_C, sheet_cells(f"{W}/reference/raw/phases_{key}.jpg"), [t + "s" for t in times.split()]))
grid_sheet(rows, "BEHIND THE GOLFER: our swing vs the 'Top 12 Smoothest Swings' pros",
           "Broadcast tee shots (camera behind the golfer, offset toward the ball). Pro frames picked at 30 fps; impact frames are within one frame of contact.",
           f"{W}/sheets/03_behind_ours_vs_pros.jpg", cw=230, ch=320, lw=230)


# ------------------------------------------------------------------ 4. videos
def encode(frames_dir, out, fps, w, h):
    paths = sorted(glob.glob(f"{frames_dir}/*.jpg"))
    subprocess.run([ENC, out, str(fps), str(w), str(h)] + paths, check=True, stdout=subprocess.DEVNULL)
    print(out, len(paths), "frames")


def fresh(d):
    shutil.rmtree(d, ignore_errors=True); os.makedirs(d)
    return d


IMPACT = 103
if os.environ.get("SC_VIDEOS", "1") == "1":
    # 4a. our swing, eight angles: real speed (every other 60 fps frame at 30 fps), then half speed (every frame)
    tmp = fresh(f"{W}/tmp/all_angles"); cw, ch = 480, 600; n = 0
    cache = {}
    for speed, frames in [("1x  real speed", list(range(20, 200, 2))), ("0.5x", list(range(60, 180)))]:
        for f in frames:
            im = Image.new("RGB", (1920, 1260), (14, 14, 18)); d = ImageDraw.Draw(im)
            label(d, (14, 10), "OUR SWING - male, driver, in-game (Unity Golf scene)", f_mid)
            t = (f - IMPACT) / 60
            label(d, (1100, 10), f"{speed}    t = {t:+.2f} s from impact", f_mid, fill=(255, 220, 90))
            for i, (v, vname) in enumerate(VIEWS):
                x, y = (i % 4) * cw, 52 + (i // 4) * (ch + 4)
                im.paste(fit(our_crop(v, ours(v, f)), cw - 4, ch), (x, y))
                label(d, (x + 8, y + 6), vname, f_tiny, bg=(0, 0, 0), pad=3)
            im.save(f"{tmp}/{n:05d}.jpg", quality=88); n += 1
    encode(tmp, f"{W}/film/01_our_swing_8_angles.mp4", 30, 1920, 1260)

    # 4b. face-on, ours vs Adam Scott, synced at impact: real speed, then quarter speed round impact
    scott = []
    for k in "ABCDEF":
        scott += sheet_cells(f"{W}/reference/raw/scott_run_{k}.jpg", n=8 if k != "F" else 7)
    S0, SI = 11.90, 12.95          # first Scott frame, his impact (s, 30 fps source)
    def scott_at(tau):
        i = round((SI + tau - S0) * 30); return scott[max(0, min(len(scott) - 1, i))], S0 + max(0, min(len(scott) - 1, i)) / 30
    tmp = fresh(f"{W}/tmp/scott"); n = 0
    plan = [("1x  real speed", [k / 30 for k in range(-40, 36)]), ("0.25x", [k / 120 for k in range(-80, 140)])]
    for speed, taus in plan:
        for tau in taus:
            im = Image.new("RGB", (1280, 860), (14, 14, 18)); d = ImageDraw.Draw(im)
            label(d, (14, 10), "FACE-ON  ·  ours vs Adam Scott (driver), synced at impact", f_mid)
            label(d, (14, 46), f"{speed}    t = {tau:+.2f} s from impact", f_small, fill=(255, 220, 90))
            f = max(0, min(230, round(IMPACT + tau * 60)))
            im.paste(fit(our_crop("faceon", ours("faceon", f)), 632, 760), (0, 90))
            simg, st = scott_at(tau)
            im.paste(fit(simg, 632, 760), (644, 90))
            label(d, (10, 98), "OURS (in-game)", f_small, bg=OUR_C, pad=5)
            label(d, (654, 98), "Adam Scott  ·  YouTube h-JQsjjsE6Y", f_small, bg=PRO_C, pad=5)
            ph = "backswing" if f < 81 else "downswing" if f < IMPACT else "through"
            label(d, (10, 820), f"f{f}  {ph}   (top -0.38 s · settled +0.62 s · clip ends +1.02 s)", f_tiny, bg=(0, 0, 0), pad=3)
            sph = "backswing" if st < 12.74 else "downswing" if st < SI else "through"
            label(d, (654, 820), f"{st:.2f} s  {sph}   (top -0.21 s · finish ~+0.35 s)", f_tiny, bg=(0, 0, 0), pad=3)
            im.save(f"{tmp}/{n:05d}.jpg", quality=88); n += 1
    encode(tmp, f"{W}/film/02_faceon_ours_vs_scott.mp4", 30, 1280, 860)
    shutil.rmtree(f"{W}/tmp", ignore_errors=True)
