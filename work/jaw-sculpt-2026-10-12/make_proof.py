#!/usr/bin/env python3
"""Proof sheets for the jaw sculpt (system python3 + Pillow).
  proof/profile_overlay_<sex>.png  our side profile (game geometry, orthographic) outlined on the target side profile:
                                   green = target, yellow = before, red = now. Lined up by crown and chin height.
  proof/closeups_<sex>.png         TARGET / BEFORE / PASS 1 / NOW, front, three-quarter and side close-ups (Unity turnaround stills, rest pose)
  proof/all.png                    both heroes
"""
import os
from PIL import Image, ImageDraw, ImageFont
HERE = os.path.dirname(os.path.abspath(__file__)); REPO = os.path.dirname(os.path.dirname(HERE))
EVAL = os.path.join(HERE, 'eval'); OUT = os.path.join(HERE, 'proof'); os.makedirs(OUT, exist_ok=True)
TARGETS = os.path.join(REPO, 'ArtDir/hero/targets-2026-10-12/bald-base')
HEAD = {'Male': '02_male_bald_head.jpg', 'Female': '04_female_bald_head.jpg'}
TILES = {'Male': {'front': (0, 170, 340, 510), 'three_quarter': (320, 170, 660, 510), 'side': (610, 170, 950, 510)},
         'Female': {'front': (0, 140, 340, 480), 'three_quarter': (300, 140, 640, 480), 'side': (620, 140, 960, 480)}}
FINAL = {'Male': 'm26', 'Female': 'f16'}
BEFORE = os.path.join(REPO, 'work/bald-base-2026-10-12/jaw_before')   # before any jaw work
PASS1 = os.path.join(HERE, 'unity')                                   # first sculpt pass
NOW = os.path.join(HERE, 'unity_p2')                                  # second pass: jaw corner + crisp jaw normals
BG, INK = (30, 30, 32), (235, 235, 235)


def font(size):
    for p in ('/System/Library/Fonts/Supplemental/Arial Bold.ttf', '/System/Library/Fonts/Helvetica.ttc'):
        try: return ImageFont.truetype(p, size)
        except OSError: pass
    return ImageFont.load_default()


def profile(sex):
    ov = Image.open(os.path.join(EVAL, f'{FINAL[sex]}_ov_side_{sex}.png')).convert('RGB')
    # 1200 px = 0.4 m. Whole head + neck, and the jaw zoomed 2x
    head = ov.crop((150, 40, 1050, 1140)).resize((640, 782), Image.LANCZOS)
    jaw = ov.crop((180, 540, 780, 1040) if sex == 'Male' else (330, 620, 930, 1120)).resize((938, 782), Image.LANCZOS)
    s = Image.new('RGB', (640 + 938 + 30, 782 + 70), BG); d = ImageDraw.Draw(s)
    d.text((12, 10), f'{sex}: side profile (in-game shape) on the target side profile', fill=INK, font=font(24))
    d.text((12, 40), 'green = target   yellow = before   red = now   (lined up by crown and chin height)', fill=(200, 200, 200), font=font(18))
    s.paste(head, (10, 70)); s.paste(jaw, (660, 70))
    s.save(os.path.join(OUT, f'profile_overlay_{sex}.png')); return s


def closeups(sex, size=300):
    hero = f'MatchHero_{sex}'
    cols = [('TARGET', (255, 214, 120)), ('BEFORE', (240, 128, 112)), ('PASS 1', (200, 170, 120)), ('NOW', (130, 220, 130))]
    W = 12 * (size + 6) + 2 * 14; H = 76 + size + 16
    s = Image.new('RGB', (W, H), BG); d = ImageDraw.Draw(s)
    d.text((12, 10), f'{hero}: jaw close-ups vs target (Unity, rest pose)', fill=INK, font=font(24))
    x = 0
    for view, box in TILES[sex].items():
        for name, colour in cols:
            if name == 'TARGET': im = Image.open(os.path.join(TARGETS, HEAD[sex])).convert('RGB').crop(box)
            else:
                p = os.path.join({'BEFORE': BEFORE, 'PASS 1': PASS1, 'NOW': NOW}[name], hero, f'rest_jaw_{view}.png')
                if not os.path.exists(p): x += size + 6; continue
                im = Image.open(p).convert('RGB')
            s.paste(im.resize((size, size), Image.LANCZOS), (x, 76))
            d.text((x + 6, 50), f'{name} {view.replace("_", "-")}', fill=colour, font=font(17))
            x += size + 6
        x += 14
    s.save(os.path.join(OUT, f'closeups_{sex}.png')); return s


if __name__ == '__main__':
    ims = []
    for sex in ('Male', 'Female'):
        ims += [profile(sex)]
        try: ims += [closeups(sex)]
        except FileNotFoundError as e: print('close-ups missing:', e)
    W = max(i.width for i in ims); all_ = Image.new('RGB', (W, sum(i.height for i in ims)), BG); y = 0
    for i in ims: all_.paste(i, (0, y)); y += i.height
    all_.save(os.path.join(OUT, 'all.png')); print('wrote', OUT)


def by_eye(sex, size=380):
    """proof/pass2_<sex>.png: rows front / three-quarter / side, columns TARGET / PASS 1 / NOW (Unity, rest pose)."""
    hero = f'MatchHero_{sex}'
    cols = [('TARGET', (255, 214, 120)), ('PASS 1', (200, 170, 120)), ('NOW', (130, 220, 130))]
    s = Image.new('RGB', (3 * (size + 8) + 8, 3 * (size + 34) + 50), BG); d = ImageDraw.Draw(s)
    d.text((10, 12), f'{hero}: jaw, in game vs target', fill=INK, font=font(24))
    for r, (view, box) in enumerate(TILES[sex].items()):
        for c, (name, colour) in enumerate(cols):
            if name == 'TARGET': im = Image.open(os.path.join(TARGETS, HEAD[sex])).convert('RGB').crop(box)
            else: im = Image.open(os.path.join(PASS1 if name == 'PASS 1' else NOW, hero, f'rest_jaw_{view}.png')).convert('RGB')
            x, y = 8 + c * (size + 8), 50 + r * (size + 34)
            d.text((x + 4, y + 4), f'{name} {view.replace("_", "-")}', fill=colour, font=font(17))
            s.paste(im.resize((size, size), Image.LANCZOS), (x, y + 28))
    s.save(os.path.join(OUT, f'pass2_{sex}.png')); return s




P2 = os.path.join(HERE, 'unity_p2b')     # pass 2 (staged FBX + 5.5 mm normals), same capture setup as pass 3
P3 = os.path.join(HERE, 'unity_p3')      # pass 3: jawline aligned + smoothed along the line, 8 mm normals blended into 12 mm


def pass3(sex, size=380):
    """proof/pass3_<sex>.png: rows front / three-quarter / side, columns TARGET / PASS 2 / NOW (Unity, rest pose)."""
    hero = f'MatchHero_{sex}'
    cols = [('TARGET', (255, 214, 120), None), ('PASS 2', (200, 170, 120), P2), ('NOW', (130, 220, 130), P3)]
    s = Image.new('RGB', (3 * (size + 8) + 8, 3 * (size + 34) + 50), BG); d = ImageDraw.Draw(s)
    d.text((10, 12), f'{hero}: jaw, in game vs target (pass 3)', fill=INK, font=font(24))
    for r, (view, box) in enumerate(TILES[sex].items()):
        for c, (name, colour, src) in enumerate(cols):
            im = Image.open(os.path.join(TARGETS, HEAD[sex])).convert('RGB').crop(box) if src is None else \
                 Image.open(os.path.join(src, hero, f'rest_jaw_{view}.png')).convert('RGB')
            x, y = 8 + c * (size + 8), 50 + r * (size + 34)
            d.text((x + 4, y + 4), f'{name} {view.replace("_", "-")}', fill=colour, font=font(17))
            s.paste(im.resize((size, size), Image.LANCZOS), (x, y + 28))
    s.save(os.path.join(OUT, f'pass3_{sex}.png')); return s


def jawline(size=520):
    """proof/pass3_jawline.png: the jawline alone, three-quarter, PASS 2 vs NOW, both heroes (Unity, rest pose)."""
    s = Image.new('RGB', (2 * (size + 8) + 8, 2 * (size + 34) + 50), BG); d = ImageDraw.Draw(s)
    d.text((10, 12), 'Jawline close-up, three-quarter, in game: PASS 2 vs NOW', fill=INK, font=font(24))
    for r, sex in enumerate(('Male', 'Female')):
        for c, (name, colour, src) in enumerate((('PASS 2', (200, 170, 120), P2), ('NOW', (130, 220, 130), P3))):
            im = Image.open(os.path.join(src, f'MatchHero_{sex}', 'rest_jawline_three_quarter.png')).convert('RGB')
            x, y = 8 + c * (size + 8), 50 + r * (size + 34)
            d.text((x + 4, y + 4), f'{name} · {sex.lower()} · jawline three-quarter', fill=colour, font=font(17))
            s.paste(im.resize((size, size), Image.LANCZOS), (x, y + 28))
    s.save(os.path.join(OUT, 'pass3_jawline.png')); return s


if __name__ == '__main__':
    import sys
    if 'pass3' in sys.argv:
        for sex in ('Male', 'Female'): pass3(sex)
        jawline()
