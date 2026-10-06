#!/usr/bin/env python3
"""The swing showcase's frames into an MP4 (Golf Arcade -> Record Swing Showcase writes Library/Captures/swing/f_NNNNN.jpg and takes.json).

    python3 Unity/Tools/swing_video.py [out.mp4] [frames dir]

H.264 30 fps at the frames' size (1920x1080), a label on each take (the club, REAL TIME or SLOW MOTION), a short title, fades, and sound made here, timed to each take's strike
(takes.json says which frame the ball is struck on): a whoosh that builds into the ball and a strike on it (real time: a bright whoosh and a crack; slow motion: a long, low
rush and a deep thud), over a quiet bed of sea and breeze. The game itself plays no sound for a swing off the course, so there is nothing else to mix in.
"""
import json, os, subprocess, sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, ".."))
frames = sys.argv[2] if len(sys.argv) > 2 else os.path.join(ROOT, "Library", "Captures", "swing")
out = sys.argv[1] if len(sys.argv) > 1 else os.path.join(frames, "golf-arcade-swing.mp4")
FONT = "/System/Library/Fonts/Helvetica.ttc"
meta = json.load(open(os.path.join(frames, "takes.json")))
fps = meta["fps"]
n_frames = len([f for f in os.listdir(frames) if f.startswith("f_") and f.endswith(".jpg")])
secs = n_frames / fps
end = secs - 0.8

# ---- picture: labels (this ffmpeg has no text filter: each label is an image, drawn here and laid over the frames for its take)
from PIL import Image, ImageDraw, ImageFont
def label_png(path, text, size):
    font = ImageFont.truetype(FONT, size)
    l, tp, r, b = ImageDraw.Draw(Image.new("RGB", (4, 4))).textbbox((0, 0), text, font=font)
    pad = 22
    im = Image.new("RGBA", (r - l + 2 * pad, b - tp + 2 * pad), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    d.rounded_rectangle((0, 0, im.width - 1, im.height - 1), 18, fill=(0, 0, 0, 120))
    d.text((pad - l, pad - tp), text, fill=(255, 255, 255, 255), font=font)
    im.save(path); return im.size
labels = []          # (png, x expression, y expression, first frame, last frame)
tmp = os.path.join(frames, "labels"); os.makedirs(tmp, exist_ok=True)
label_png(os.path.join(tmp, "title.png"), "GOLF ARCADE    THE SWING", 66)
labels.append((os.path.join(tmp, "title.png"), "(W-w)/2", "H*0.10", 0, int(fps * 2.2)))
for i, tk in enumerate(meta["takes"]):
    slow = tk["slow"] < 1
    label_png(os.path.join(tmp, f"take{i}.png"), f"{tk['label']}    " + ("SLOW MOTION   1/4 SPEED" if slow else "REAL TIME"), 50)
    labels.append((os.path.join(tmp, f"take{i}.png"), "60", "H-h-70", tk["start"] + 6, tk["end"]))
inputs = []
for png, *_ in labels: inputs.extend(["-loop", "1", "-framerate", str(fps), "-i", png])
vchain = ["[0:v]fade=t=in:d=0.5,fade=t=out:st=%.3f:d=0.8[v0]" % end]
for i, (png, x, y, a, b) in enumerate(labels):
    vchain.append(f"[v{i}][{i + 1}:v]overlay=x={x}:y={y}:enable='between(n,{a},{b})'[v{i + 1}]")
vlast = f"[v{len(labels)}]"
n_inputs_video = 1 + len(labels)

# ---- sound
chains, mix = [], []
def add(spec, filt, delay_s, vol):
    k = n_inputs_video + sum(1 for w in inputs if w == "lavfi")        # (the frames and the labels come first; then each sound source)
    inputs.extend(["-f", "lavfi", "-i", spec])
    tag = f"s{k}"
    chains.append(f"[{k}:a]{filt},volume={vol},adelay={max(0, int(delay_s * 1000))}:all=1,aformat=sample_rates=48000:channel_layouts=stereo[{tag}]")
    mix.append(f"[{tag}]")
for tk in meta["takes"]:
    t_imp = tk["impact"] / fps
    if tk["slow"] >= 1:
        add("anoisesrc=d=0.6:c=pink:a=0.9:r=48000", "highpass=f=600,lowpass=f=6500,afade=t=in:st=0:d=0.4,afade=t=out:st=0.4:d=0.2", t_imp - 0.4, 0.9)       # the whoosh into the ball
        add("anoisesrc=d=0.12:c=white:a=1:r=48000", "lowpass=f=3200,afade=t=out:st=0:d=0.12", t_imp, 1.0)                                                       # the crack
        add("sine=f=180:d=0.18:r=48000", "afade=t=out:st=0:d=0.18", t_imp, 0.55)
    else:
        add("anoisesrc=d=2.4:c=brown:a=1:r=48000", "lowpass=f=1100,afade=t=in:st=0:d=1.7,afade=t=out:st=1.7:d=0.7", t_imp - 1.7, 1.1)                       # a long low rush
        add("anoisesrc=d=0.5:c=white:a=1:r=48000", "lowpass=f=600,afade=t=out:st=0:d=0.5", t_imp, 1.0)                                                           # the thud
        add("sine=f=70:d=0.6:r=48000", "afade=t=out:st=0:d=0.6", t_imp, 0.8)
# the bed: sea and breeze, as in demo_video.sh
add("anoisesrc=color=brown:amplitude=0.6:sample_rate=48000:seed=12", f"lowpass=f=420,tremolo=f=0.12:d=0.6,atrim=0:{secs:.3f}", 0, 0.5)
add("anoisesrc=color=pink:amplitude=0.25:sample_rate=48000:seed=7", f"highpass=f=900,lowpass=f=4000,tremolo=f=0.1:d=0.5,atrim=0:{secs:.3f}", 0, 0.05)
fc = ";".join(vchain) + ";" + ";".join(chains) + f";{''.join(mix)}amix=inputs={len(mix)}:duration=longest:normalize=0,atrim=0:{secs:.3f},afade=t=in:d=0.4,afade=t=out:st={end:.3f}:d=0.8[a]"
cmd = ["ffmpeg", "-v", "error", "-stats", "-y", "-framerate", str(fps), "-i", os.path.join(frames, "f_%05d.jpg")] + inputs + [
    "-filter_complex", fc, "-map", vlast, "-map", "[a]", "-c:v", "libx264", "-preset", "medium", "-crf", "20", "-pix_fmt", "yuv420p",
    "-c:a", "aac", "-b:a", "160k", "-movflags", "+faststart", "-t", f"{secs:.3f}", out]
r = subprocess.run(cmd)
if r.returncode: sys.exit(r.returncode)
print(out, f"{os.path.getsize(out) / 1e6:.1f} MB, {secs:.1f} s")
