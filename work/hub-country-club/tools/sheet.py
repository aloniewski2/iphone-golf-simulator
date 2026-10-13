#!/usr/bin/env python3
"""Country-club hub proof sheets: BEFORE | AFTER for each of the 7 brief shots (same cameras), plus one contact sheet.

    python3 work/hub-country-club/tools/sheet.py <before dir> <after dir> <out dir> [target dir] [shot,shot,...]

Writes <out>/<shot>_before_after.png (1920 x 600 + labels) and <out>/all_before_after.png. With a target dir (the brief's
sheets), also <out>/<shot>_before_after_target.png with the AFTER (target) image from the brief as a third panel.
"""
import os, sys
from PIL import Image, ImageDraw, ImageFont

SHOTS = ["01_island_overview", "02_plaza_spawn", "03_locker_door", "04_play_door", "05_clubhouse_door", "06_tennis_court", "07_golf_range"]
TARGETS = {"01_island_overview": "01_island_overview_before_after.png", "02_plaza_spawn": "02_plaza_spawn_before_after.png",
           "03_locker_door": "03_locker_door_before_after.png", "04_play_door": "04_play_door_before_after.png",
           "05_clubhouse_door": "05_clubhouse_door_before_after.png", "06_tennis_court": "06_tennis_court_before_after.png",
           "07_golf_range": "07_golf_range_before_after.png", "08_play_hall": "08_play_hall_target.jpg"}


def font(size):
    for path in ["/System/Library/Fonts/Supplemental/Arial Bold.ttf", "/System/Library/Fonts/Helvetica.ttc", "/Library/Fonts/Arial Bold.ttf"]:
        if os.path.exists(path):
            try:
                return ImageFont.truetype(path, size)
            except OSError:
                pass
    return ImageFont.load_default()


def target_panel(path):
    """The AFTER (right) half of a brief sheet: the image area between the title and the captions (a .jpg is the target alone)."""
    im = Image.open(path).convert("RGB")
    if path.endswith(".jpg"):
        return im
    w, h = im.size
    # the sheets are 2260 x 769: two 1100-wide panels from y ~70 to ~690 (measured on the sheets)
    k = w / 2260
    return im.crop((int(1140 * k), int(70 * k), int(2240 * k), int(690 * k)))


def main():
    before, after, out = sys.argv[1], sys.argv[2], sys.argv[3]
    targets = sys.argv[4] if len(sys.argv) > 4 else None
    shots = sys.argv[5].split(",") if len(sys.argv) > 5 else SHOTS
    os.makedirs(out, exist_ok=True)
    W = 960; H = 540; pad = 12; band = 46
    f = font(28); fs = font(20)
    rows = []
    for s in shots:
        b = Image.open(os.path.join(before, s + ".png")).convert("RGB").resize((W, H), Image.LANCZOS)
        a = Image.open(os.path.join(after, s + ".png")).convert("RGB").resize((W, H), Image.LANCZOS)
        sheet = Image.new("RGB", (W * 2 + pad * 3, H + band + pad * 2), (28, 28, 30))
        sheet.paste(b, (pad, band + pad)); sheet.paste(a, (W + pad * 2, band + pad))
        d = ImageDraw.Draw(sheet)
        d.text((pad, 10), "BEFORE  " + s[3:].replace("_", " "), font=f, fill=(250, 140, 120))
        d.text((W + pad * 2, 10), "AFTER  (same camera)", font=f, fill=(140, 225, 140))
        sheet.save(os.path.join(out, s + "_before_after.png"))
        rows.append(sheet)
        if targets and s in TARGETS and os.path.exists(os.path.join(targets, TARGETS[s])):
            t = target_panel(os.path.join(targets, TARGETS[s])).resize((W, H), Image.LANCZOS)
            three = Image.new("RGB", (W * 3 + pad * 4, H + band + pad * 2), (28, 28, 30))
            three.paste(b, (pad, band + pad)); three.paste(a, (W + pad * 2, band + pad)); three.paste(t, (W * 2 + pad * 3, band + pad))
            d = ImageDraw.Draw(three)
            d.text((pad, 10), "BEFORE (ours)", font=f, fill=(250, 140, 120))
            d.text((W + pad * 2, 10), "AFTER (ours, same camera)", font=f, fill=(140, 225, 140))
            d.text((W * 2 + pad * 3, 10), "TARGET (brief sheet)", font=f, fill=(150, 190, 250))
            three.save(os.path.join(out, s + "_before_after_target.png"))
    allw = rows[0].width; allh = sum(r.height for r in rows)
    big = Image.new("RGB", (allw, allh), (28, 28, 30)); y = 0
    for r in rows:
        big.paste(r, (0, y)); y += r.height
    big = big.resize((allw // 2, allh // 2), Image.LANCZOS)
    big.save(os.path.join(out, "all_before_after.png"))


if __name__ == "__main__":
    main()
