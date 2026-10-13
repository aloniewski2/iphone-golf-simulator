"""Tennis vs golf emote comparison videos from the EmoteCompareFilm frames.
  python3 compose.py <sex>      (frames/tennis_<sex>, frames/golf_<sex> -> film/<Move>_<sex>.mp4, proof/<Move>_<sex>_sheet.jpg)

Each video is a 2x2 grid at 30 fps, every cell starting at the moment its game was told to play the emote:
  top    : TENNIS front camera | GOLF front camera   (the game camera moved to the same spot in front of the body)
  bottom : TENNIS game camera  | GOLF game camera on the hole-out card (what the TV shows)
A shorter clip holds its last frame. A second video <Move>_<sex>_result.mp4 shows golf's shot-result panel view next to the tennis game camera."""
import glob, os, subprocess, sys
from PIL import Image, ImageDraw, ImageFont

W = os.path.dirname(os.path.abspath(__file__))
ENC = os.path.join(W, "..", "emotes", "scripts", "enc")
MOVES = ["Intro_Wave", "Intro_BringIt", "Intro_Pushups", "Emote_Scuba", "Emote_Thrust", "Emote_Spike"]
NAMES = {"Intro_Wave": "Wave", "Intro_BringIt": "Bring It", "Intro_Pushups": "Push-ups", "Emote_Scuba": "Scuba", "Emote_Thrust": "Thrust", "Emote_Spike": "Spike"}
sex = sys.argv[1] if len(sys.argv) > 1 else "male"
font = ImageFont.truetype("/System/Library/Fonts/Supplemental/Arial Bold.ttf", 26)
small = ImageFont.truetype("/System/Library/Fonts/Supplemental/Arial Bold.ttf", 20)
os.makedirs(f"{W}/film", exist_ok=True); os.makedirs(f"{W}/proof", exist_ok=True)


def frames(d, kind):
    return sorted(glob.glob(f"{d}/{kind}_*.jpg"))


def cell(path, label, sub, colour):
    im = Image.open(path).convert("RGB")
    if im.size != (960, 540): im = im.resize((960, 540), Image.LANCZOS)
    dr = ImageDraw.Draw(im)
    w = dr.textlength(label, font=font) + dr.textlength("  " + sub, font=small) + 28
    dr.rectangle([0, 0, w, 40], fill=colour)
    dr.text((12, 6), label, font=font, fill="white")
    dr.text((12 + dr.textlength(label, font=font), 11), "  " + sub, font=small, fill="white")
    return im


def grid(cells, out_dir, tag):
    """cells: [(frames, label, colour)] in reading order (2 per row). Writes the PNG frames and returns their paths."""
    n = max(len(c[0]) for c in cells)
    rows = (len(cells) + 1) // 2
    os.makedirs(out_dir, exist_ok=True)
    for f in glob.glob(f"{out_dir}/*.png"): os.remove(f)
    paths = []
    for i in range(n):
        im = Image.new("RGB", (1920, 540 * rows), (20, 20, 24))
        for k, (fr, label, colour) in enumerate(cells):
            j = min(i, len(fr) - 1)
            sub = f"{j / 30:4.1f}s / {len(fr) / 30:.1f}s" + ("  (ended)" if i >= len(fr) else "")
            im.paste(cell(fr[j], label, sub, colour), ((k % 2) * 960, (k // 2) * 540))
        p = f"{out_dir}/{tag}_{i:05d}.png"; im.save(p); paths.append(p)
    return paths


T, G = (40, 110, 200), (30, 140, 70)
for mv in MOVES:
    td, gr, gh = f"{W}/frames/tennis_{sex}/{mv}", f"{W}/frames/golf_{sex}/result/{mv}", f"{W}/frames/golf_{sex}/holeout/{mv}"
    if not (os.path.isdir(td) and os.path.isdir(gh)): print("skip", mv); continue
    nm = NAMES[mv]
    cells = [(frames(td, "front"), f"TENNIS · {nm} · front", T), (frames(gh, "front"), f"GOLF · {nm} · front", G),
             (frames(td, "game"), f"TENNIS · {nm} · game camera", T), (frames(gh, "game"), f"GOLF · {nm} · game camera (hole-out card)", G)]
    tmp = f"{W}/tmp/{sex}_{mv}"
    paths = grid(cells, tmp, "g")
    out = f"{W}/film/{mv}_{sex}.mp4"
    subprocess.run([ENC, out, "30", "1920", "1080"] + paths, check=True, stdout=subprocess.DEVNULL)
    # a contact sheet: 6 moments across the longer clip
    n = len(paths); picks = [int(n * t) for t in (0.1, 0.25, 0.4, 0.55, 0.7, 0.85)]
    sheet = Image.new("RGB", (3 * 960, 2 * 540))
    for q, p in enumerate(picks):
        sheet.paste(Image.open(paths[p]).resize((960, 540), Image.LANCZOS), ((q % 3) * 960, (q // 3) * 540))
    sheet.save(f"{W}/proof/{mv}_{sex}_sheet.jpg", quality=88)
    if os.path.isdir(gr):
        cells = [(frames(td, "game"), f"TENNIS · {nm} · game camera", T), (frames(gr, "game"), f"GOLF · {nm} · game camera (shot result)", G)]
        paths = grid(cells, tmp + "_r", "r")
        subprocess.run([ENC, f"{W}/film/{mv}_{sex}_result.mp4", "30", "1920", "540"] + paths, check=True, stdout=subprocess.DEVNULL)
    print("wrote", out)
