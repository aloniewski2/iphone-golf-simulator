"""Before / after contact sheets of the locker close-ups (reads proof/*.png written by LockerMirrorProofTests, writes proof/SHEET_*.png)."""
import sys, os
from PIL import Image, ImageDraw, ImageFont
P = os.path.join(os.path.dirname(__file__), "..", "proof")
def label(im, text):
    d = ImageDraw.Draw(im); d.rectangle([0, 0, im.width, 34], fill=(16, 18, 24)); d.text((10, 8), text, fill=(255, 255, 255))
    return im
def sheet(names, title, out, w=520):
    ims = []
    for n in names:
        for suffix, tag in (("_BEFORE", "BEFORE"), ("", "AFTER")):
            f = os.path.join(P, n + suffix + ".png")
            if not os.path.exists(f): continue
            im = Image.open(f).convert("RGB"); im = im.resize((w, int(im.height * w / im.width)))
            ims.append(label(im, f"{tag}  {n.replace('_', ' ')}"))
    if not ims: return
    h = max(i.height for i in ims); rows = 2
    cols = (len(ims) + rows - 1) // rows
    S = Image.new("RGB", (cols * w, rows * h), (16, 18, 24))
    for k, im in enumerate(ims):
        # pair BEFORE above AFTER in a column
        c, r = k // 2, k % 2
        S.paste(im, (c * w, r * h))
    S.save(os.path.join(P, out)); print("wrote", out, S.size)
sheet(["male_torso", "female_skort"], "torso and skort", "SHEET_torso_skort.png")
sheet(["male_shoe", "female_shoe"], "shoe", "SHEET_shoe.png")
sheet(["male_face", "female_face"], "face", "SHEET_face.png")
sheet(["male_body", "female_body"], "body", "SHEET_body.png")
