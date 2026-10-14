"""target (top) vs ours (bottom: rest-pose front, front-right, side, back + close-ups) for one hero.  python3 compare.py <after-dir> <out-dir>"""
import sys, os
from PIL import Image, ImageDraw
A, O = sys.argv[1], sys.argv[2]; os.makedirs(O, exist_ok=True)
T = '../../ArtDir/hero/targets-2026-10-12/clothing-v2/'
heroes = [('MatchHero_Male', '01_tennis_male_target.jpg'), ('MatchHero_Female', '02_tennis_female_target.jpg'),
          ('GolfKitHero_Male', '03_golf_male_target.jpg'), ('GolfKitHero_Female', '04_golf_female_target.jpg')]
for h, t in heroes:
    tgt = Image.open(T + t).convert('RGB'); W = 1400
    tgt = tgt.resize((W, int(tgt.height * W / tgt.width)))
    views = ['rest_full_front', 'rest_full_front_left', 'rest_full_left', 'rest_full_back']
    ims = [Image.open(f'{A}/{h}/{v}.png').convert('RGB') for v in views]
    ims = [i.crop((90, 60, 990, 1440)).resize((350, 537)) for i in ims]
    close = ['rest_top_front', 'rest_top_back', 'rest_bottom_front', 'rest_bottom_back', 'rest_shoes_left']
    cl = [Image.open(f'{A}/{h}/{v}.png').convert('RGB').resize((280, 280)) for v in close]
    S = Image.new('RGB', (W, tgt.height + 537 + 280 + 40), (40, 40, 42))
    S.paste(tgt, (0, 0))
    for k, i in enumerate(ims): S.paste(i, (k * 350, tgt.height + 20))
    for k, i in enumerate(cl): S.paste(i, (k * 280, tgt.height + 557 + 20))
    d = ImageDraw.Draw(S); d.text((8, tgt.height + 4), 'OURS (Unity turnaround, rest pose, target hues)', fill=(255, 220, 120))
    S.save(f'{O}/{h}_vs_target.png')
print('ok')
