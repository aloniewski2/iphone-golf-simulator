#!/usr/bin/env python3
"""Bald-base pass 2: TARGET / BEFORE / NOW sheets against the "after" rows of ArtDir/hero/targets-2026-10-12/bald-base/.

TARGET = the matching tile cut from the target sheets; BEFORE = work/model-turnaround/before-2026-10-12 (the heroes at the start of
2026-10-12); NOW = the latest work/model-turnaround run. Writes proof/target_<hero>.png and proof/target_all.png.
The targets are slimmer and have hair-free painted faces: compare the listed changes (face, ears, blush, grip, shoes, skirt), not the body size.
"""
import os
from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))
TARGETS = os.path.join(REPO, 'ArtDir/hero/targets-2026-10-12/bald-base')
TURN = os.path.join(REPO, 'work/model-turnaround')
BEFORE = os.path.join(TURN, 'before-2026-10-12')
OUT = os.path.join(HERE, 'proof')
BG, INK = (30, 30, 32), (235, 235, 235)
ROWS = (('TARGET', (255, 214, 120)), ('BEFORE', (240, 128, 112)), ('NOW', (130, 220, 130)))

HEAD = {'front': (182, 542, 582, 942), 'three-quarter': (594, 542, 994, 942), 'side': (1006, 542, 1406, 942)}
BODY = {'front': (182, 622, 542, 1102), 'side': (926, 622, 1286, 1102)}
K = 2534 / 2000   # the details sheet's tile boxes were read at 2000 px wide
DETAIL = {k: tuple(int(v * K) for v in box) for k, box in {
    'male grip': (144, 360, 444, 585), 'female grip': (453, 360, 753, 585), 'relaxed hand': (762, 360, 1062, 585),
    'shoes': (1072, 360, 1372, 585), 'skirt': (1381, 360, 1681, 585), 'skin': (1690, 360, 1990, 585)}.items()}

# per hero: (column label, target sheet, target box, turnaround shot)
COLUMNS = {
    'MatchHero_Male': [('face front', 'male_head_before_after.png', HEAD['front'], 'rest_face_front'),
                       ('face 3/4', 'male_head_before_after.png', HEAD['three-quarter'], 'rest_face_three_quarter'),
                       ('face side / ear', 'male_head_before_after.png', HEAD['side'], 'rest_face_side'),
                       ('grip', 'details_before_after.png', DETAIL['male grip'], 'ready_hand_right_front'),
                       ('relaxed hand', 'details_before_after.png', DETAIL['relaxed hand'], 'rest_hand_left_outside'),
                       ('shoes + socks', 'details_before_after.png', DETAIL['shoes'], 'ready_shoes_left'),
                       ('body', 'male_body_before_after.png', BODY['front'], 'ready_full_front')],
    'MatchHero_Female': [('face front', 'female_head_before_after.png', HEAD['front'], 'rest_face_front'),
                         ('face 3/4', 'female_head_before_after.png', HEAD['three-quarter'], 'rest_face_three_quarter'),
                         ('face side / ear', 'female_head_before_after.png', HEAD['side'], 'rest_face_side'),
                         ('grip', 'details_before_after.png', DETAIL['female grip'], 'ready_hand_right_front'),
                         ('relaxed hand', 'details_before_after.png', DETAIL['relaxed hand'], 'rest_hand_left_outside'),
                         ('shoes + socks', 'details_before_after.png', DETAIL['shoes'], 'ready_shoes_right'),
                         ('body', 'female_body_before_after.png', BODY['front'], 'ready_full_front')],
    'GolfKitHero_Female': [('face front', 'female_head_before_after.png', HEAD['front'], 'rest_face_front'),
                           ('skirt', 'details_before_after.png', DETAIL['skirt'], 'ready_bottom_front'),
                           ('skirt side', 'female_body_before_after.png', BODY['side'], 'ready_full_left'),
                           ('body', 'female_body_before_after.png', BODY['front'], 'ready_full_front')],
}


def font(size):
    for path in ('/System/Library/Fonts/Supplemental/Arial Bold.ttf', '/System/Library/Fonts/Helvetica.ttc'):
        try: return ImageFont.truetype(path, size)
        except OSError: pass
    return ImageFont.load_default()


def fit(im, size):
    im = im.convert('RGB'); im.thumbnail((size, size), Image.LANCZOS)
    t = Image.new('RGB', (size, size), BG); t.paste(im, ((size - im.width) // 2, (size - im.height) // 2)); return t


def sheet(hero, size=300):
    cols = COLUMNS[hero]; label, pad, top = 150, 6, 60
    W = label + len(cols) * (size + pad); H = top + 3 * (size + pad) + 34
    s = Image.new('RGB', (W, H), BG); d = ImageDraw.Draw(s)
    d.text((12, 14), f'{hero}: target vs before vs now (bald-base pass 2, 2026-10-12)', fill=INK, font=font(26))
    for r, (name, colour) in enumerate(ROWS):
        y = top + r * (size + pad); d.text((12, y + size // 2 - 14), name, fill=colour, font=font(28))
        for c, (_, target, box, shot) in enumerate(cols):
            if name == 'TARGET': im = Image.open(os.path.join(TARGETS, target)).crop(box)
            else:
                p = os.path.join(BEFORE if name == 'BEFORE' else TURN, hero, shot + '.png')
                if not os.path.exists(p): continue
                im = Image.open(p)
            s.paste(fit(im, size), (label + c * (size + pad), y))
    for c, (title, *_rest) in enumerate(cols): d.text((label + c * (size + pad) + 4, top + 3 * (size + pad) + 6), title, fill=INK, font=font(16))
    return s


if __name__ == '__main__':
    os.makedirs(OUT, exist_ok=True); rows = []
    for hero in COLUMNS:
        im = sheet(hero); im.save(os.path.join(OUT, f'target_{hero}.png')); rows.append(im)
    W = max(r.width for r in rows); all_ = Image.new('RGB', (W, sum(r.height for r in rows)), BG); y = 0
    for r in rows: all_.paste(r, (0, y)); y += r.height
    all_.save(os.path.join(OUT, 'target_all.png')); print('wrote', OUT)
