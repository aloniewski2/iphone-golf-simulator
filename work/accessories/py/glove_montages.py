"""PIL montages (system python3): proof/glove/glove_on_<sex>_vs_reference.png"""
import os, sys
from PIL import Image, ImageDraw, ImageFont
R = '/Users/adnanyonathan/Documents/Codex/2026-09-20/wh/outputs/iphone-golf-simulator/'
P = R + 'work/accessories/proof/glove/'
ref = Image.open(R + 'ArtDir/hero/targets-2026-10-13/accessories/06_fit_golf_cap_glove_bag.jpg').convert('RGB')
f = ImageFont.truetype('/System/Library/Fonts/Helvetica.ttc', 22)


def tile(path, label, W=640, crop=None):
    im = Image.open(path).convert('RGB')
    if crop: im = im.crop(crop)
    im = im.resize((W, W))
    d = ImageDraw.Draw(im); d.rectangle((0, 0, 8 + 13 * len(label), 34), fill=(31, 43, 87)); d.text((8, 5), label, fill=(255, 255, 255), font=f)
    return im


for sex in ('Male', 'Female'):
    H = P + 'hero/'
    rows = [
        [(H + f'{sex}_glove_front.png', 'FRONT (both gloves)', None), (H + f'{sex}_glove_q34.png', '3/4', None), (H + f'{sex}_glove_back.png', 'BACK', None)],
        [(H + f'{sex}_glove_hand_back.png', 'LEFT HAND: BACK', None), (H + f'{sex}_glove_hand_q34.png', 'LEFT HAND: 3/4', None), (H + f'{sex}_glove_hand_palm.png', 'LEFT HAND: PALM', None)],
        [(H + f'{sex}R_glove_hand_back.png', 'RIGHT HAND: BACK', None), (H + f'{sex}R_glove_hand_q34.png', 'RIGHT HAND: 3/4', None), (H + f'{sex}R_glove_hand_palm.png', 'RIGHT HAND: PALM', None)],
    ]
    W = 640
    refw = W * 3
    ref2 = ref.resize((refw, int(ref.height * refw / ref.width)))
    sheet = Image.new('RGB', (refw, ref2.height + W * len(rows)), (40, 40, 44))
    sheet.paste(ref2, (0, 0))
    for r, row in enumerate(rows):
        for c, (pth, lab, crop) in enumerate(row):
            sheet.paste(tile(pth, lab, W, crop), (c * W, ref2.height + r * W))
    out = P + f'glove_on_{sex}_vs_reference.png'
    sheet.save(out); print(out, sheet.size)
