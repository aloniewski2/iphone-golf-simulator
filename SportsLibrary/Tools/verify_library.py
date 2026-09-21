"""Read-only asset handoff checks. Run after git lfs pull; Python 3 standard library only."""
from pathlib import Path
import hashlib
import json
import re
import sys
from urllib.parse import unquote

root = Path(__file__).resolve().parents[1]
errors = []
inventory = json.loads((root / 'asset-inventory.json').read_text())['files']
for entry in inventory:
    path = root / entry['path']
    if not path.is_file():
        errors.append(f'Missing: {entry["path"]}')
        continue
    digest = hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b''):
            digest.update(block)
    if path.stat().st_size != entry['bytes'] or digest.hexdigest() != entry['sha256']:
        errors.append(f'Size/checksum mismatch (run git lfs pull if pointer): {entry["path"]}')

actions = json.loads((root / 'Animations/V4/Reports/action-manifest.json').read_text())
if len(actions) != 236 or len({a['action'] for a in actions}) != 236:
    errors.append('Expected 236 unique V4 actions')
families = {(a['sport'], a['clip']) for a in actions}
if len(families) != 59:
    errors.append('Expected 59 motion families')
for sport, clip in families:
    variants = {(a['gender'], a['hand']) for a in actions if a['sport'] == sport and a['clip'] == clip}
    if variants != {('Male','RH'),('Male','LH'),('Female','RH'),('Female','LH')}:
        errors.append(f'Incomplete standard-character/hand coverage: {sport}/{clip}')
for sport in ['Golf','Tennis','Bowling','Boxing']:
    split = json.loads((root / f'Animations/V4/{sport}/actions.json').read_text())
    if split != [a for a in actions if a['sport'] == sport]:
        errors.append(f'Incorrect sport index: {sport}')

registry = json.loads((root / 'Characters/standard-characters.json').read_text())
if registry['status'] != 'permanent_visual_standard':
    errors.append('Permanent character standard status changed')
for character in registry['characters']:
    if not (root / 'Characters' / character['source']).is_file():
        errors.append(f'Missing original identity: {character["id"]}')

# Current navigation only: original archived notes may contain former author-machine paths.
docs = [root/'README.md',root/'CHARACTER-STANDARD.md',root/'UNITY-HANDOFF.md',root/'Previews/README.md']
docs += list((root/'Animations/V4').glob('*/README.md'))
for doc in docs:
    for target in re.findall(r'\]\(([^)]+)\)', doc.read_text()):
        if '://' in target or target.startswith('#'):
            continue
        if not (doc.parent / unquote(target.split('#')[0])).exists():
            errors.append(f'Broken navigation: {doc.relative_to(root)} -> {target}')

report = {'passed': not errors, 'inventoried_files': len(inventory),
          'inventoried_bytes': sum(x['bytes'] for x in inventory),
          'action_variants': len(actions), 'motion_families': len(families),
          'unity_runtime_tested': False, 'errors': errors}
print(json.dumps(report, indent=2))
sys.exit(1 if errors else 0)
