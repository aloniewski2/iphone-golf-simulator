"""Build face_lock_f crops, debug grids and contact sheets from the three female plates.
Run from anywhere:  python3 ArtDir/hero/base_lock/face_lock_f/build_pack.py
Writes ONLY inside face_lock_f/. No .blend/.fbx access."""
import json, os
from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
BASE = os.path.dirname(HERE)
SRC = {k: os.path.join(BASE, v) for k, v in {
    "head": "female_head_detail.jpg",
    "bald": "female_body_bald.jpg",
    "multi": "female_multiangle_body.jpg"}.items()}

def mbox(cx, top):  # multiangle head+shoulders box
    return [cx - 46, top, cx + 46, top + (136 - top if top < 100 else 96)]

CROPS = [  # name, source key, box
    ("01_front_face.png",                "head",  [85, 55, 430, 342]),
    ("02_left_profile.png",              "head",  [440, 55, 800, 342]),
    ("03_right_profile.png",             "head",  [830, 55, 1195, 342]),
    ("04_threequarter_front_left.png",   "head",  [85, 385, 430, 656]),
    ("05_threequarter_front_right.png",  "head",  [440, 385, 800, 656]),
    ("06_top_cranial.png",               "head",  [830, 385, 1195, 656]),
    ("07_body_front_head.png",           "bald",  [350, 15, 530, 200]),
    ("08_body_back_head.png",            "bald",  [757, 15, 937, 200]),
    ("09_ma_front.png",                  "multi", mbox(171, 46)),
    ("10_ma_back.png",                   "multi", mbox(487, 46)),
    ("11_ma_left_profile.png",           "multi", mbox(790, 46)),
    ("12_ma_right_profile.png",          "multi", mbox(1093, 46)),
    ("13_ma_threequarter_front_left.png","multi", mbox(170, 372)),
    ("14_ma_threequarter_front_right.png","multi",mbox(482, 372)),
    ("15_ma_threequarter_back_left.png", "multi", mbox(787, 372)),
    ("16_ma_threequarter_back_right.png","multi", mbox(1088, 372)),
]
LABELS = ["01 FRONT","02 LEFT PROFILE","03 RIGHT PROFILE","04 3/4 FRONT L","05 3/4 FRONT R",
          "06 TOP CRANIAL","07 BODY FRONT HEAD","08 BODY BACK HEAD","09 MA FRONT","10 MA BACK",
          "11 MA LEFT","12 MA RIGHT","13 MA 3/4 FL","14 MA 3/4 FR","15 MA 3/4 BL","16 MA 3/4 BR"]
BG = (27, 27, 30)
def font(sz):
    for p in ("/System/Library/Fonts/Supplemental/Arial Bold.ttf", "/System/Library/Fonts/Helvetica.ttc"):
        try: return ImageFont.truetype(p, sz)
        except Exception: pass
    return ImageFont.load_default()

imgs = {k: Image.open(v).convert("RGB") for k, v in SRC.items()}
meta = {}
crops = []
for (name, key, box), lab in zip(CROPS, LABELS):
    c = imgs[key].crop(tuple(box)); c.save(os.path.join(HERE, name))
    crops.append((lab, c)); meta[name] = {"box": box, "source": os.path.basename(SRC[key])}
json.dump(meta, open(os.path.join(HERE, "crop_boxes.json"), "w"), indent=2)

# debug grids (boxes drawn on the full plate)
for key, fn in (("head", "debug_grid.png"), ("bald", "debug_grid_body_bald.png"), ("multi", "debug_grid_multiangle.png")):
    im = imgs[key].copy(); d = ImageDraw.Draw(im)
    for name, k, box in CROPS:
        if k == key:
            d.rectangle(box, outline=(0, 255, 90), width=2); d.text((box[0] + 4, box[1] + 3), name[:2], fill=(0, 255, 90), font=font(14))
    im.save(os.path.join(HERE, fn))

def sheet(items, cols, cell, title, out, upscale=True):
    cw, ch = cell; pad = 12; lab_h = 30; top = 56
    rows = (len(items) + cols - 1) // cols
    W = pad + cols * (cw + pad); H = top + rows * (ch + lab_h + pad) + pad
    S = Image.new("RGB", (W, H), (24, 24, 27)); d = ImageDraw.Draw(S)
    d.text((W // 2, 28), title, fill=(255, 255, 255), font=font(30), anchor="mm")
    for i, (lab, im) in enumerate(items):
        r, c = divmod(i, cols); x = pad + c * (cw + pad); y = top + r * (ch + lab_h + pad)
        d.rectangle([x, y, x + cw, y + ch + lab_h], fill=(18, 18, 20), outline=(60, 60, 64))
        s = min(cw / im.width, ch / im.height)
        if not upscale: s = min(s, 1.0)
        t = im.resize((max(1, int(im.width * s)), max(1, int(im.height * s))), Image.LANCZOS)
        S.paste(t, (x + (cw - t.width) // 2, y + (ch - t.height) // 2))
        d.text((x + cw // 2, y + ch + lab_h // 2), lab, fill=(235, 235, 235), font=font(16), anchor="mm")
    S.save(os.path.join(HERE, out))

sheet(crops[0:6], 3, (420, 300), "FEMALE FACE LOCK — Head Detail (6 panels, authority)", "SHEET_head_detail_6.png")
sheet(crops[8:16], 4, (330, 340), "FEMALE FACE LOCK — Multi-angle Heads (8)", "SHEET_multiangle_heads.png")
sheet(crops, 4, (340, 360), "FEMALE FACE LOCK — All Face Angles (authority)", "SHEET_all_face_angles.png")

# female vs male anatomy contrast (keeps male face from leaking into female pass)
mdir = os.path.join(BASE, "face_lock")
pairs = []
for f, lab in (("01_front_face.png", "FRONT"), ("02_left_profile.png", "LEFT PROFILE"), ("04_threequarter_front_left.png", "3/4 FRONT L")):
    pairs.append(("FEMALE " + lab, Image.open(os.path.join(HERE, f)).convert("RGB")))
    pairs.append(("MALE (do NOT copy) " + lab, Image.open(os.path.join(mdir, f)).convert("RGB")))
sheet(pairs, 2, (420, 300), "FEMALE (authority) vs MALE (contrast only)", "SHEET_female_vs_male_contrast.png")
print("ok", len(crops), "crops")
