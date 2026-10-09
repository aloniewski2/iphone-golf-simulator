import json,hashlib
from pathlib import Path
import numpy as np
from PIL import Image
P=Path(__file__).resolve().parents[1];A=P.parent;root=A.parent.parent
old=json.loads((A/'anim_repair/gameplay_pose_bake.json').read_text());new=json.loads((P/'gameplay_pose_bake.json').read_text());checks={}
for a,b in zip(old['clips'],new['clips']):
 if a['name'].startswith('Run'):
  assert a==b;checks[a['name']]={'all_pose_frames_exact':True}
 elif a['name'] in ['Forehand','JumpServe','Smash']:
  allowed=['UpperArm.R','LowerArm.R','Hand.R'];others=[i for i,n in enumerate(old['bones']) if n not in allowed]
  assert all(all(f[key][i]==g[key][i] for i in others) for f,g in zip(a['frames'],b['frames']) for key in ['positions','rotations']);checks[a['name']]={'all_non_right_arm_curves_exact':True}
for idx,name in [(4,'RunForward'),(5,'RunRight'),(6,'RunLeft')]:
 maximum=0;worst=0
 for frame in range(120):
  f=f'frame_{idx*120+frame:04}.png';a=np.asarray(Image.open(A.parent/'screenshots/anim_repair_playmode'/f)).astype(int);b=np.asarray(Image.open(A.parent/'screenshots/fullstroke_grip_playmode'/f)).astype(int);maximum=max(maximum,int(abs(a-b).max()));worst=max(worst,int(np.count_nonzero(np.any(a!=b,axis=2))))
 checks[name].update(max_capture_channel_delta=maximum,max_changed_pixels_per_960x960_frame=worst)
(P/'scope_verification.json').write_text(json.dumps(checks,indent=2));print(checks)
