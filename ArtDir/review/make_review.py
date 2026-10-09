"""Assemble a Remotion review reel from the latest captures and audit renders, then render it.
Usage: python make_review.py <tag> "<change 1>" "<change 2>" ...
Inputs: ArtDir/review/intro<tag>/intro.mp4 (IntroCaptureTests), ArtDir/hero/v5_proof/hairfit_unity (HairFitAuditTests),
ArtDir/hero/v5_proof/hairfit (locker testHairFitAudit), optional extra PNGs in ArtDir/review/extra/.
Output: ArtDir/review/review_<tag>.mp4 (+ manifest.json beside it)."""
import json, shutil, subprocess, sys, datetime
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
REMOTION = ROOT.parent / 'remotion'
tag = sys.argv[1] if len(sys.argv) > 1 else ''
changes = sys.argv[2:]
build = f"{datetime.date.today():%Y-%m-%d} {tag.strip('_') or 'review'}"
pub = REMOTION / 'public' / 'review' / (tag.strip('_') or 'latest')
if pub.exists(): shutil.rmtree(pub)
pub.mkdir(parents=True)
rel = lambda p: str(p.relative_to(REMOTION / 'public'))

def put(src, name=None):
    src = Path(src); dst = pub / (name or src.name); shutil.copy2(src, dst); return rel(dst)

segs = [{"type": "title", "title": f"Review · {build}", "lines": changes or ["latest build"], "seconds": 4}]
intro = ROOT / f'ArtDir/review/intro{tag}/intro.mp4'
if intro.exists():
    v = put(intro)
    segs += [
        {"type": "video", "src": v, "title": "Match intro (as played)", "note": "drone → rival → you → umpire → swoop", "from": 0, "seconds": 15.4},
        {"type": "video", "src": v, "title": "Rival intro, close", "note": "slow ×0.5", "from": 5.4, "seconds": 7, "rate": .5, "zoom": {"x": .5, "y": .45, "scale": 1.9}},
        {"type": "video", "src": v, "title": "Player intro, close", "note": "slow ×0.5", "from": 9.2, "seconds": 7, "rate": .5, "zoom": {"x": .5, "y": .45, "scale": 1.9}},
        {"type": "video", "src": v, "title": "Rally", "note": "gameplay camera", "from": 16.5, "seconds": 20},
        {"type": "video", "src": v, "title": "Rally, near player", "note": "slow ×0.5 zoom", "from": 20, "seconds": 10, "rate": .5, "zoom": {"x": .5, "y": .75, "scale": 1.8}},
    ]
hf = ROOT / 'ArtDir/hero/v5_proof/hairfit_unity'
cuts = ["Swept", "Ponytail", "Bob", "Long", "Curly", "Bald", "Buzz", "Waves"]
hats = ["None", "Visor", "Cap", "Sweatband"]
def grid(title, note, files, cols, seconds=4):
    fs = [f for f in files if f.exists()]
    if fs: segs.append({"type": "grid", "title": title, "note": note, "images": [put(f, f"{f.parent.name}_{f.name}") for f in fs],
                        "labels": [f.stem for f in fs], "cols": cols, "seconds": seconds})
for body in ("boy", "girl"):
    grid(f"In game: every haircut, {body}, no hat / visor", "Ready, 3/4", [hf / f"{body}_{c}_{h}_ready_34.png" for h in ("None", "Visor") for c in cuts], 8)
grid("In game: new cuts × every headwear, mid-swing side", "forehand contact", [hf / f"boy_{c}_{h}_fh_side.png" for c in ("Bald", "Buzz", "Waves") for h in hats], 4)
grid("In game: new cuts, back", "Ready", [hf / f"boy_{c}_{h}_ready_back.png" for c in ("Bald", "Buzz", "Waves") for h in hats], 4)
lk = ROOT / 'ArtDir/hero/v5_proof/hairfit'
grid("Locker: new cuts × every headwear", "3/4", [lk / f"{c}_{h}_34.png" for c in ("Bald", "Buzz", "Waves") for h in hats], 4)
grid("Locker: new cuts, side / back", "", [lk / f"{c}_{h}_{v}.png" for c in ("Bald", "Buzz", "Waves") for h in ("None", "Visor") for v in ("side", "back")], 4)
extra = ROOT / 'ArtDir/review/extra'
if extra.exists():
    for f in sorted(extra.glob('*.png')): grid(f.stem.replace('_', ' '), "", [f], 1, 4)

man = {"build": build, "segments": segs}
out = ROOT / f'ArtDir/review/review{tag}.mp4'
(out.with_suffix('.json')).write_text(json.dumps(man, indent=1))
props = pub / 'manifest.json'; props.write_text(json.dumps(man))
subprocess.run(['npx', 'remotion', 'render', 'Review', str(out), f'--props={props}', '--log=error'], cwd=REMOTION, check=True)
print('REVIEW', out, sum(s['seconds'] for s in segs), 's')
