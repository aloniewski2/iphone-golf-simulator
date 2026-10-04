"""Verify frozen plate authority without overwriting user-owned acceptance or scope."""
from pathlib import Path
import hashlib,json
R=Path(__file__).resolve().parents[2];D=R/'ArtDir/hero/base_lock'
frozen=json.loads((D/'plate-measurements.json').read_text())
for sex,values in frozen.items():
 p=D/(sex+'_body_plain.jpg');actual=hashlib.sha256(p.read_bytes()).hexdigest()
 if actual!=values['sha256']:raise RuntimeError('Authority plate hash changed: '+str(p))
for folder in ['proof','blender','unity_import']:(D/folder).mkdir(exist_ok=True)
for p in ['ACCEPTANCE.md','METRICS.md','SCALE.md']:
 if not (D/p).exists():raise RuntimeError('Missing frozen contract '+p)
print('PHASE0_VERIFIED: unchanged authority plates; static character scope, no rig or animator.')
