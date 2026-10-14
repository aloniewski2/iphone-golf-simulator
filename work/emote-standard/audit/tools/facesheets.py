"""facesheets.py <raw_face_dir> <out_dir>: one sheet per context: rows = face states, columns = front / 3-4 left / 3-4 right / side (192 px tiles)"""
import os, sys, glob
from PIL import Image, ImageDraw, ImageFont
raw, out = sys.argv[1], sys.argv[2]; os.makedirs(out, exist_ok=True)
F = ImageFont.truetype('/System/Library/Fonts/Supplemental/Arial Bold.ttf', 16)
states = ['neutral', 'happy', 'sad', 'angry', 'shock', 'blinkhalf', 'blinkfull', 'happy_blink', 'sad_blink', 'angry_blink', 'shock_browmax', 'gaze_left', 'gaze_right', 'gaze_up', 'gaze_down', 'angry_gaze_down', 'shock_gaze_up']
views = ['front', 'l45', 'r45', 'side']; T = 224
for d in sorted(glob.glob(os.path.join(raw, 'face_*'))):
    ctx = os.path.basename(d)[5:]
    # two sheets per context: moods (rows 0-10), gaze (rows 11-16)
    for name, rows in (('moods', states[:11]), ('gaze', states[11:])):
        P = Image.new('RGB', (130 + 4 * T, 28 + len(rows) * T), (18, 18, 22)); dr = ImageDraw.Draw(P)
        for vi, v in enumerate(views): dr.text((130 + vi * T + 8, 6), v, fill=(255, 225, 140), font=F)
        for ri, s in enumerate(rows):
            dr.text((6, 28 + ri * T + T // 2 - 8), s, fill=(255, 255, 255), font=F)
            for vi, v in enumerate(views):
                p = os.path.join(d, f'{s}_{v}.jpg')
                if os.path.exists(p): P.paste(Image.open(p).resize((T, T), Image.LANCZOS), (130 + vi * T, 28 + ri * T))
        P.save(os.path.join(out, f'face_{ctx}_{name}.jpg'), quality=88)
