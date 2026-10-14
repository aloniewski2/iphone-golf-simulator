#!/usr/bin/env python3
"""Before / after sheets for the bald-base pass (2026-10-12).

Reads the turnaround stills saved before the pass (work/model-turnaround/before-2026-10-12/<hero>/) and the fresh ones
(work/model-turnaround/<hero>/, written by work/model-turnaround/run.sh) and writes into work/bald-base-2026-10-12/proof/:
  sheets_<hero>.png      the old and new contact sheets side by side
  compare_<hero>.png     the close-ups that changed (face, ears, grip, hands, shoes, skirt), BEFORE row over AFTER row
  compare_all.png        the four heroes' compare rows stacked
"""
import os
from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))
TURN = os.path.join(REPO, 'work/model-turnaround')
BEFORE = os.path.join(TURN, 'before-2026-10-12')
OUT = os.path.join(HERE, 'proof')
BG, INK, OLD, NEW = (30, 30, 32), (235, 235, 235), (240, 128, 112), (130, 220, 130)

SHOTS = {
    'MatchHero_Male': ['rest_face_front', 'rest_face_three_quarter', 'rest_face_side', 'ready_hand_right_front', 'ready_hand_right_outside',
                       'rest_hand_left_outside', 'ready_shoes_left', 'ready_full_front'],
    'MatchHero_Female': ['rest_face_front', 'rest_face_three_quarter', 'rest_face_side', 'ready_hand_right_front', 'ready_hand_right_outside',
                         'rest_hand_left_outside', 'ready_shoes_right', 'ready_full_front'],
    'GolfKitHero_Male': ['rest_face_front', 'rest_face_three_quarter', 'rest_face_side', 'ready_hand_left_front', 'rest_hand_left_outside',
                         'ready_shoes_left', 'ready_full_front', 'ready_full_left'],
    'GolfKitHero_Female': ['rest_face_front', 'rest_face_three_quarter', 'rest_face_side', 'ready_full_front', 'ready_full_left',
                           'ready_bottom_front', 'rest_full_front', 'ready_shoes_left'],
}


def font(size):
    for path in ('/System/Library/Fonts/Supplemental/Arial Bold.ttf', '/System/Library/Fonts/Helvetica.ttc', '/Library/Fonts/Arial.ttf'):
        try: return ImageFont.truetype(path, size)
        except OSError: pass
    return ImageFont.load_default()


def tile(path, size):
    im = Image.open(path).convert('RGB'); im.thumbnail((size, size))
    t = Image.new('RGB', (size, size), BG); t.paste(im, ((size - im.width) // 2, (size - im.height) // 2)); return t


def compare(hero, size=300):
    shots = SHOTS[hero]; label = 150; pad = 6
    W = label + len(shots) * (size + pad); H = 60 + 2 * (size + pad) + 30
    sheet = Image.new('RGB', (W, H), BG); d = ImageDraw.Draw(sheet)
    d.text((12, 14), f'{hero}: before vs after (bald-base pass 2026-10-12)', fill=INK, font=font(26))
    for row, (name, root, colour) in enumerate((('BEFORE', BEFORE, OLD), ('AFTER', TURN, NEW))):
        y = 60 + row * (size + pad)
        d.text((12, y + size // 2 - 14), name, fill=colour, font=font(28))
        for c, shot in enumerate(shots):
            p = os.path.join(root, hero, shot + '.png')
            if os.path.exists(p): sheet.paste(tile(p, size), (label + c * (size + pad), y))
    for c, shot in enumerate(shots):
        d.text((label + c * (size + pad) + 4, 60 + 2 * (size + pad) + 4), shot.replace('_', ' '), fill=INK, font=font(15))
    return sheet


def sheets(hero, height=1500):
    a = Image.open(os.path.join(BEFORE, hero + '_contact_sheet.png')).convert('RGB')
    b = Image.open(os.path.join(TURN, hero + '_contact_sheet.png')).convert('RGB')
    a = a.resize((int(a.width * height / a.height), height)); b = b.resize((int(b.width * height / b.height), height))
    out = Image.new('RGB', (a.width + b.width + 30, height + 70), BG); d = ImageDraw.Draw(out)
    d.text((14, 18), 'BEFORE', fill=OLD, font=font(34)); d.text((a.width + 44, 18), 'AFTER', fill=NEW, font=font(34))
    out.paste(a, (0, 70)); out.paste(b, (a.width + 30, 70)); return out


def main():
    os.makedirs(OUT, exist_ok=True)
    rows = []
    for hero in SHOTS:
        sheets(hero).save(os.path.join(OUT, f'sheets_{hero}.png'))
        c = compare(hero); c.save(os.path.join(OUT, f'compare_{hero}.png')); rows.append(c)
    W = max(r.width for r in rows); allsheet = Image.new('RGB', (W, sum(r.height for r in rows)), BG); y = 0
    for r in rows: allsheet.paste(r, (0, y)); y += r.height
    allsheet.save(os.path.join(OUT, 'compare_all.png'))
    print('wrote', OUT)


if __name__ == '__main__':
    main()
