from pathlib import Path
import re,json,hashlib
p=Path('Unity/Assets/ArtDirection/Hero01');pref=(p/'Prefabs/Hero_01_BackhandRun.prefab').read_text();lookup={}
for f in p.rglob('*.fbx.meta'):
 m=re.search(r'^guid: (.+)$',f.read_text(),re.M)
 if m:lookup[m[1]]=str(f)[:-5]
r={}
for key in ['idle','serve','forehand','backhand','runForward','runRight','runLeft','volley','smash']:
 m=re.search(r'^  '+key+r':.*guid: ([a-f0-9]+)',pref,re.M);r[key]=lookup.get(m[1]) if m else None
assert all(r.values());assert len(set(r.values()))==9
out=Path('ArtDir/anims/anim_repair');(out/'slot_audit_before.json').write_text(json.dumps(r,indent=2))
print(json.dumps(r,indent=2))
# Check actual preceding recorded frames, not a selected pose.
from PIL import Image
import numpy as np
frames=Path('ArtDir/screenshots/backhand_run_playmode');motion={}
for name,i in [('JumpServe',1),('Forehand',2),('Volley',7),('Smash',8)]:
 a=np.asarray(Image.open(frames/f'frame_{i*120:04}.png'),dtype=float);b=np.asarray(Image.open(frames/f'frame_{i*120+20:04}.png'),dtype=float)
 motion[name]=float(np.mean(abs(a-b)))
(out/'previous_render_motion.json').write_text(json.dumps(motion,indent=2));print(motion)
