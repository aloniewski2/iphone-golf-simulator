#!/usr/bin/env python3
"""Skin pass (2026-10-12, after pass 2): face close-ups next to the target heads.

TARGET = the front / three-quarter / side tiles of ArtDir/hero/targets-2026-10-12/bald-base/0{2,4}_*_bald_head.jpg
BEFORE = skin_before/<hero>/ (the turnaround before the skin pass), NOW = work/model-turnaround/<hero>/ (re-run run.sh first).
Writes proof/skin_<hero>.png and proof/skin_all.png. The turnaround's studio skin tone is darker than the target's; compare the finish
(matte, form shading, golden warmth), not the tone.
"""
import os
from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))
TARGETS = os.path.join(REPO, 'ArtDir/hero/targets-2026-10-12/bald-base')
TURN = os.path.join(REPO, 'work/model-turnaround')
BEFORE = os.path.join(HERE, 'skin_before')
OUT = os.path.join(HERE, 'proof')
BG, INK = (30, 30, 32), (235, 235, 235)
HEAD = {'male': '02_male_bald_head.jpg', 'female': '04_female_bald_head.jpg'}
TILES = {'front': (20, 150, 300, 430), 'three_quarter': (320, 150, 600, 430), 'side': (630, 150, 910, 430)}
HEROES = [('MatchHero_Male', 'male'), ('MatchHero_Female', 'female'), ('GolfKitHero_Male', 'male'), ('GolfKitHero_Female', 'female')]


def font(size):
    for path in ('/System/Library/Fonts/Supplemental/Arial Bold.ttf', '/System/Library/Fonts/Helvetica.ttc'):
        try: return ImageFont.truetype(path, size)
        except OSError: pass
    return ImageFont.load_default()


def sheet(hero, sex, size=330):
    cols = [('TARGET', (255, 214, 120)), ('BEFORE', (240, 128, 112)), ('NOW', (130, 220, 130))]
    pad, top = 6, 76
    W = 3 * 3 * (size + pad) + 2 * 14; H = top + size + 36
    s = Image.new('RGB', (W, H), BG); d = ImageDraw.Draw(s)
    d.text((12, 10), f'{hero}: skin finish vs target (matte, warm form, golden tan)', fill=INK, font=font(24))
    x = 0
    for view, box in TILES.items():
        for k, (name, colour) in enumerate(cols):
            if name == 'TARGET': im = Image.open(os.path.join(TARGETS, HEAD[sex])).convert('RGB').crop(box)
            else:
                p = os.path.join(BEFORE if name == 'BEFORE' else TURN, hero, f'rest_face_{view}.png')
                if not os.path.exists(p): x += size + pad; continue
                im = Image.open(p).convert('RGB')
            s.paste(im.resize((size, size), Image.LANCZOS), (x, top))
            d.text((x + 6, top - 28), f'{name} {view.replace("_", "-")}', fill=colour, font=font(18))
            x += size + pad
        x += 14
    return s


if __name__ == '__main__':
    os.makedirs(OUT, exist_ok=True); rows = []
    for hero, sex in HEROES:
        im = sheet(hero, sex); im.save(os.path.join(OUT, f'skin_{hero}.png')); rows.append(im)
    W = max(r.width for r in rows); all_ = Image.new('RGB', (W, sum(r.height for r in rows)), BG); y = 0
    for r in rows: all_.paste(r, (0, y)); y += r.height
    all_.save(os.path.join(OUT, 'skin_all.png')); print('wrote', OUT)
