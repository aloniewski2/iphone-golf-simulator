"""SWING FIX before / after: the in-game swing filmed by SwingFilm.cs before the fix (work/swing-compare/frames) and after it (work/swing-fix/frames),
next to the tour references (work/swing-compare/reference/raw, local only).
    python3 scripts/compose_fix.py     -> sheets/*.jpg, film/*.mp4, measurements.txt"""
import os, sys, csv, math, glob, shutil, subprocess
from PIL import Image, ImageDraw, ImageFont

W = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CMP = os.path.join(W, "..", "swing-compare")
sys.argv = [sys.argv[0]]
ENC = os.path.join(W, "..", "emotes", "scripts", "enc")
B = "/System/Library/Fonts/Supplemental/Arial Bold.ttf"; R = "/System/Library/Fonts/Supplemental/Arial.ttf"
f_big, f_mid, f_small, f_tiny = (ImageFont.truetype(B, s) for s in (34, 24, 19, 16)); f_reg = ImageFont.truetype(R, 19)
os.makedirs(f"{W}/sheets", exist_ok=True); os.makedirs(f"{W}/film", exist_ok=True)
PHASES = ["Address", "Takeaway", "Top", "Halfway down", "Impact", "Release", "Follow-through", "Finish"]
BEFORE, AFTER = f"{CMP}/frames", f"{W}/frames"
OLD_C, NEW_C, PRO_C = (150, 60, 60), (24, 120, 200), (70, 70, 78)


def load_csv(p): return list(csv.DictReader(open(p)))
def V(s): return tuple(map(float, s.split(";"))) if s else None


def phases(root, tag):
    """Our 8 phase frames from the take's trace + bones (60 fps game clock)."""
    tr = load_csv(f"{root}/{tag}/trace.csv")
    bo = load_csv(f"{root}/{tag}/bones.csv") if os.path.exists(f"{root}/{tag}/bones.csv") else None
    fp = f"{root}/{tag}/frame.txt" if os.path.exists(f"{root}/{tag}/frame.txt") else f"{root}/male_Driver/frame.txt"
    meta = open(fp).read().split()
    face, aim = V(meta[1].split("=")[1]), V(meta[2].split("=")[1])
    def L(p): return (p[0] * face[0] + p[2] * face[2], p[1], p[0] * aim[0] + p[2] * aim[2])
    ball0 = V(tr[0]["ball"])
    launch = next(i for i, r in enumerate(tr) if r["ball"] and math.dist(V(r["ball"]), ball0) > 0.02)
    strike = next(i for i, r in enumerate(tr) if r["stage"] == "through")
    top = strike - 1
    head_y = [V(r["clubHead"])[1] if r["clubHead"] else 0 for r in tr]
    def lead_elev(i):
        a, h = L(V(bo[i]["LeftUpperArm"])), L(V(bo[i]["LeftHand"]))
        v = [h[k] - a[k] for k in range(3)]; return math.degrees(math.atan2(v[1], math.hypot(v[0], v[2])))
    take = next(i for i in range(30, top) if head_y[i] > 0.85)                       # shaft about parallel going back
    if bo:
        half = min(range(top, launch), key=lambda i: abs(lead_elev(i)))              # lead arm parallel on the way down
        hand_y = [L(V(b["LeftHand"]))[1] for b in bo]
    else:                                                                            # (no skeleton log: the club head's highest point on the way down, hands at ~1 m)
        half = max(range(top, launch), key=lambda i: head_y[i]); hand_y = [1.0] * len(tr)
    release = next((i for i in range(launch + 1, len(tr)) if head_y[i] >= hand_y[i]), launch + 3)
    follow = max(range(launch, min(len(tr), launch + 60)), key=lambda i: head_y[i])
    ct = [float(r["clipTime"]) for r in tr]
    fin = next(i for i in range(launch, len(tr)) if ct[i] >= max(ct) - 0.02)
    return [30, take, top, half, launch, release, follow, fin], dict(top=top, strike=strike, launch=launch, fin=fin, settled=next(i for i in range(launch, len(tr)) if abs(head_y[i] - head_y[min(len(tr) - 1, i + 6)]) < 0.01 and i > follow))


def img(root, tag, view, f):
    return Image.open(f"{root}/{tag}/{view}_{f:05d}.jpg").convert("RGB")


def crop(view, im):
    if view == "game": return im.crop((400, 120, 900, 720))
    if view == "overhead": return im
    if view in ("broadcast", "dtl"): return im.crop((0, 60, 600, 860))
    if view == "target": return im.crop((120, 60, 720, 860))
    return im.crop((60, 70, 660, 870))


def fit(im, w, h, bg=(16, 16, 20)):
    s = min(w / im.width, h / im.height)
    r = im.resize((max(1, round(im.width * s)), max(1, round(im.height * s))), Image.LANCZOS)
    c = Image.new("RGB", (w, h), bg); c.paste(r, ((w - r.width) // 2, (h - r.height) // 2)); return c


def label(d, xy, text, font, fill=(255, 255, 255), bg=None, pad=6):
    if bg:
        x0, y0, x1, y1 = d.textbbox(xy, text, font=font); d.rectangle([x0 - pad, y0 - pad + 2, x1 + pad, y1 + pad], fill=bg)
    d.text(xy, text, font=font, fill=fill)


def sheet_cells(path, n=8, cols=2, rows=4):
    im = Image.open(path).convert("RGB"); cw, ch = im.width / cols, (im.height - 30) / rows; out = []
    for i in range(n):
        x, y = (i % cols) * cw, 30 + (i // cols) * ch
        c = im.crop((round(x), round(y), round(x + cw), round(y + ch))); px = c.load(); w, h = c.size
        def blank_row(yy): return all(max(abs(v - 17) for v in px[xx, yy]) < 6 for xx in range(110, w, 7))
        def blank_col(xx): return all(max(abs(v - 17) for v in px[xx, yy]) < 6 for yy in range(30, h, 7))
        t = 0
        while t < h - 1 and blank_row(t): t += 1
        b = h - 1
        while b > t and blank_row(b): b -= 1
        l = 0
        while l < w - 1 and blank_col(l): l += 1
        r = w - 1
        while r > l and blank_col(r): r -= 1
        out.append(c.crop((l, max(0, t - 24 if t > 24 else 0), r + 1, b + 1)))
    return out


def grid_sheet(rows, title, sub, out, cw=240, ch=330, lw=230):
    head = 110
    im = Image.new("RGB", (lw + cw * 8, head + 36 + ch * len(rows)), (14, 14, 18)); d = ImageDraw.Draw(im)
    label(d, (16, 14), title, f_big); label(d, (16, 60), sub, f_reg, fill=(200, 200, 210))
    for c, p in enumerate(PHASES):
        x = lw + c * cw; tw = d.textlength(p, font=f_mid); label(d, (x + (cw - tw) / 2, head + 4), p, f_mid, fill=(255, 220, 90))
    for r, (name, subname, colour, cells, caps) in enumerate(rows):
        y = head + 36 + r * ch
        d.rectangle([0, y, lw - 6, y + ch - 6], fill=colour); yy = y + 12
        for line in name.split("\n"): label(d, (12, yy), line, f_mid); yy += 30
        for line in subname.split("\n"): label(d, (12, yy + 4), line, f_tiny, fill=(225, 225, 235)); yy += 22
        for c, cell in enumerate(cells):
            x = lw + c * cw; im.paste(fit(cell, cw - 6, ch - 6), (x, y))
            if caps and caps[c]: label(d, (x + 6, y + ch - 32), caps[c], f_tiny, bg=(0, 0, 0), pad=3)
    im.save(out, quality=90); print(out, im.size)


# ---------------------------------------------------------------- sheets
pb, kb = phases(BEFORE, "male_Driver"); pa, ka = phases(AFTER, "male_Driver")
print("before phases", pb, kb); print("after phases", pa, ka)
cap = lambda fs, k: [f"f{f} {(f - k['launch']) / 60:+.2f}s" for f in fs]
scott = sheet_cells(f"{CMP}/reference/raw/phases_scott.jpg")
grid_sheet([("BEFORE", "in-game, face-on", OLD_C, [crop("faceon", img(BEFORE, "male_Driver", "faceon", f)) for f in pb], cap(pb, kb)),
            ("AFTER", "in-game, face-on", NEW_C, [crop("faceon", img(AFTER, "male_Driver", "faceon", f)) for f in pa], cap(pa, ka)),
            ("Adam Scott", "face-on, driver\nYouTube h-JQsjjsE6Y", PRO_C, scott,
             [f"{t}s {float(t) - 12.95:+.2f}s" for t in "11.90 12.27 12.70 12.85 12.97 13.03 13.13 13.50".split()])],
           "SWING FIX - face-on: before / after / Adam Scott (driver)",
           "Times are seconds from impact. Before: top -0.38 s. After: top -0.25 s. Scott: top -0.21 s.", f"{W}/sheets/01_faceon_before_after_scott.jpg", cw=260, ch=360)
pros = [("Ludvig Aberg", "aberg", "S16"), ("Tom Kim", "kim", "S12"), ("Max Homa", "homa", "S10")]
rows = [("BEFORE", "broadcast tee cam", OLD_C, [crop("broadcast", img(BEFORE, "male_Driver", "broadcast", f)) for f in pb], cap(pb, kb)),
        ("AFTER", "broadcast tee cam", NEW_C, [crop("broadcast", img(AFTER, "male_Driver", "broadcast", f)) for f in pa], cap(pa, ka)),
        ("BEFORE", "down the line", OLD_C, [crop("dtl", img(BEFORE, "male_Driver", "dtl", f)) for f in pb], None),
        ("AFTER", "down the line", NEW_C, [crop("dtl", img(AFTER, "male_Driver", "dtl", f)) for f in pa], None)]
for name, key, clip in pros:
    rows.append((name, f"EndA6Mw2S4w {clip}", PRO_C, sheet_cells(f"{CMP}/reference/raw/phases_{key}.jpg"), None))
grid_sheet(rows, "SWING FIX - behind the golfer: before / after / tour pros (driver)", "Our male driver, filmed in the Golf scene with the game's own swing code.",
           f"{W}/sheets/02_behind_before_after_pros.jpg")
# every angle, after
VIEWS = [("faceon", "Face-on"), ("broadcast", "Broadcast tee cam"), ("dtl", "Down the line"), ("q_front_trail", "3/4 front (trail)"), ("target", "From the target"),
         ("q_front_target", "3/4 front (target)"), ("overhead", "Overhead"), ("game", "Game camera (TV)")]
grid_sheet([(n, "after", NEW_C, [crop(v, img(AFTER, "male_Driver", v, f)) for f in pa], [f"f{f}" for f in pa]) for v, n in VIEWS],
           "SWING FIX - our male driver after the fix, 8 angles", "", f"{W}/sheets/03_after_all_angles.jpg", cw=230, ch=300)
# other takes after (female driver, male iron, male wedge = HalfSwing)
rows = []
for tag, nm in [("female_Driver", "Female driver"), ("male_Iron", "Male iron"), ("male_Wedge", "Male wedge\n(half swing)")]:
    if not os.path.isdir(f"{AFTER}/{tag}"): continue
    pt, kt = phases(AFTER, tag)
    for v in ("faceon", "dtl"):
        before_ok = os.path.isdir(f"{BEFORE}/{tag}")
        if before_ok:
            pbt, kbt = phases(BEFORE, tag)
            rows.append((nm, "BEFORE · " + dict(VIEWS)[v], OLD_C, [crop(v, img(BEFORE, tag, v, f)) for f in pbt], None))
        rows.append((nm, "AFTER · " + dict(VIEWS)[v], NEW_C, [crop(v, img(AFTER, tag, v, f)) for f in pt], None))
grid_sheet(rows, "SWING FIX - the other takes, before / after", "Same clip system: Drive and IronSwing share the full-swing solver; the wedge's full shot is the HalfSwing.",
           f"{W}/sheets/04_other_takes.jpg", cw=220, ch=290)


# ---------------------------------------------------------------- videos
def encode(frames_dir, out, fps, w, h):
    paths = sorted(glob.glob(f"{frames_dir}/*.jpg"))
    subprocess.run([ENC, out, str(fps), str(w), str(h)] + paths, check=True, stdout=subprocess.DEVNULL); print(out, len(paths), "frames")


def fresh(d): shutil.rmtree(d, ignore_errors=True); os.makedirs(d); return d


scott_run = []
for k in "ABCDEF": scott_run += sheet_cells(f"{CMP}/reference/raw/scott_run_{k}.jpg", n=8 if k != "F" else 7)
S0, SI = 11.90, 12.95
def scott_at(tau):
    i = max(0, min(len(scott_run) - 1, round((SI + tau - S0) * 30))); return scott_run[i], S0 + i / 30
for view, vname, name in [("faceon", "face-on", "05_faceon_before_after_scott"), ("broadcast", "broadcast tee cam", "06_behind_before_after")]:
    tmp = fresh(f"{W}/tmp/{name}"); n = 0
    for speed, taus in [("1x  real speed", [k / 30 for k in range(-40, 40)]), ("0.25x", [k / 120 for k in range(-90, 150)])]:
        for tau in taus:
            im = Image.new("RGB", (1920, 900), (14, 14, 18)); d = ImageDraw.Draw(im)
            label(d, (14, 10), f"SWING FIX · {vname} · before / after" + (" / Adam Scott" if view == "faceon" else "") + " · synced at impact", f_mid)
            label(d, (14, 46), f"{speed}    t = {tau:+.2f} s from impact", f_small, fill=(255, 220, 90))
            cols = [("BEFORE (in-game)", OLD_C, crop(view, img(BEFORE, "male_Driver", view, max(0, min(230, round(kb["launch"] + tau * 60)))))),
                    ("AFTER (in-game)", NEW_C, crop(view, img(AFTER, "male_Driver", view, max(0, min(230, round(ka["launch"] + tau * 60))))))]
            if view == "faceon": cols.append(("Adam Scott · YouTube h-JQsjjsE6Y", PRO_C, scott_at(tau)[0]))
            cw = 1920 // len(cols)
            for i, (lab, colr, cell) in enumerate(cols):
                im.paste(fit(cell, cw - 8, 800), (i * cw, 90)); label(d, (i * cw + 10, 98), lab, f_small, bg=colr, pad=5)
            im.save(f"{tmp}/{n:05d}.jpg", quality=88); n += 1
    encode(tmp, f"{W}/film/{name}.mp4", 30, 1920, 900)
shutil.rmtree(f"{W}/tmp", ignore_errors=True)
