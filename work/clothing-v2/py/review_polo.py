"""Review sheets for the polo stage."""
from PIL import Image, ImageDraw, ImageFont
import os
O = '../../ArtDir/hero/clothing-v2-review'
def font(sz):
    for f in ['/System/Library/Fonts/Supplemental/Arial.ttf', '/System/Library/Fonts/Helvetica.ttc']:
        if os.path.exists(f): return ImageFont.truetype(f, sz)
    return ImageFont.load_default()
F = font(30); Fs = font(22)
def label(im, text, sub=None):
    d = ImageDraw.Draw(im); d.rectangle((0, 0, im.width, 44), fill=(30, 30, 32)); d.text((14, 7), text, fill=(255, 225, 140), font=F)
    if sub: d.text((14, im.height - 32), sub, fill=(230, 230, 230), font=Fs)
    return im
def row(paths, h, crops=None):
    ims = []
    for i, p in enumerate(paths):
        im = Image.open(p).convert('RGB')
        if crops and crops[i]: im = im.crop(crops[i])
        ims.append(im.resize((int(im.width * h / im.height), h)))
    W = sum(i.width for i in ims); r = Image.new('RGB', (W, h), (118, 118, 120)); x = 0
    for i in ims: r.paste(i, (x, 0)); x += i.width
    return r
def stack(rows, titles, subs=None):
    W = max(r.width for r in rows); H = sum(r.height + 44 for r in rows)
    s = Image.new('RGB', (W, H), (40, 40, 42)); y = 0
    for k, r in enumerate(rows):
        band = Image.new('RGB', (W, 44), (30, 30, 32)); d = ImageDraw.Draw(band); d.text((14, 7), titles[k], fill=(255, 225, 140), font=F)
        s.paste(band, (0, y)); s.paste(r, (0, y + 44)); y += r.height + 44
    return s
# 1. the garment alone
a = row(['polo/final/alone_front.png', 'polo/final/alone_side.png', 'polo/final/alone_back.png'], 700, [(90, 270, 810, 900)] * 3)
c = row(['polo/final/alone_fl_collar.png', 'polo/final/alone_front_hem.png'], 700)
stack([a, c], ['Polo alone, off the body: front / side / back (Blender, its own baked maps)', 'Close-ups: collar (stand, roll, fall, tipping), placket + buttons; turned hem with twin-needle stitching']).save(O + '/polo_1_alone.png')
# 2. on the character (Unity, game shaders), next to the target
T = '../../ArtDir/hero/targets-2026-10-12/clothing-v2/01_tennis_male_target.jpg'
t = Image.open(T).convert('RGB'); t = t.resize((int(t.width * 760 / t.height), 760))
u = row(['polo_final/MatchHero_Male/rest_full_front.png', 'polo_final/MatchHero_Male/rest_full_front_left.png', 'polo_final/MatchHero_Male/rest_full_left.png', 'polo_final/MatchHero_Male/rest_full_back.png'], 760, [(160, 40, 920, 1100)] * 4)
cl = row(['polo_final/MatchHero_Male/rest_top_front.png', 'polo_final/MatchHero_Male/rest_top_back.png', 'polo_final/MatchHero_Male/rest_bottom_front.png', 'polo_final/MatchHero_Male/ready_top_front.png'], 560,
         [(140, 0, 940, 520), (140, 0, 940, 520), (100, 0, 980, 560), None])
stack([t, u, cl], ['TARGET 01 (tennis male)', 'OURS in Unity (game shaders): front / 3-4 / side / back; shorts, socks, shoes are still the old ones', 'Close-ups in Unity: collar + placket, back collar + cuffs, hem + vents, ready stance']).save(O + '/polo_2_on_character_vs_target.png')
# 3. poses (Blender, offline skinning == Unity): red = skin through the shirt
p = row(['polo/pose_Male_ReadyIdle.png', 'polo/pose_Male_ForehandArmCorrected.png', 'polo/pose_Male_BackhandArmCorrected.png', 'polo/pose_Male_Serve.png'], 760, [(200, 60, 700, 900)] * 4)
stack([p], ['Poses (ReadyIdle, Forehand, Backhand, Serve) - weights copied from the body; red = skin through the shirt']).save(O + '/polo_3_poses.png')
print('ok')
