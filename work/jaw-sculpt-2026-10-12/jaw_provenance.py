#!/usr/bin/env python3
"""provenance.json for the jaw-only lobby re-sync (jaw_sync.sh): the same rows as work/hero-sync/provenance.py, but every look-source
hash is taken from the STAGED (index) version of the file and of look_sources.txt itself, with LFS files smudged to their real bytes,
so the staged lobby files and their provenance agree even while other sessions have unstaged edits in the shared tree.
Run AFTER `git add` of the jaw files.   usage: jaw_provenance.py <repo> <golden json dir>
"""
import datetime, glob, hashlib, json, os, subprocess, sys

repo, golden = sys.argv[1], sys.argv[2]
sha = lambda b: hashlib.sha256(b).hexdigest()
def staged(path):
    blob = subprocess.run(['git', '-C', repo, 'show', ':' + path], capture_output=True, check=True).stdout
    if blob.startswith(b'version https://git-lfs.github.com/spec/'):
        blob = subprocess.run(['git', '-C', repo, 'lfs', 'smudge'], input=blob, capture_output=True, check=True).stdout
    return blob
rel = lambda p: os.path.relpath(p, repo)
sources = [l.strip() for l in staged('work/hero-sync/look_sources.txt').decode().splitlines() if l.strip() and not l.startswith('#')]
look = {s: sha(staged(s)) for s in sources}
heads = {s: look[s] for s in sources if s.startswith('Unity/Assets/Resources/Tennis/OriginalSeamBlinkFinish/')}
rows = []
for src in sorted(glob.glob(os.path.join(golden, '*_golden.json'))):
    name = os.path.basename(src)[:-len('_golden.json')]
    packed = os.path.join(repo, 'GolfArcadeTests/VisualGolden', name + '_golden.lzfse')
    manifest = os.path.join(repo, 'GolfArcade/Unity/CharacterAssets', name + '.json')
    rows.append({
        'name': name, 'source': rel(src), 'unitySampleCount': len(json.load(open(src))['samples']),
        'unitySourceSha256': sha(open(src, 'rb').read()), 'packedSha256': sha(open(packed, 'rb').read()), 'packedBytes': os.path.getsize(packed),
        'manifestSha256': sha(open(manifest, 'rb').read()), 'frozenFaceSourceFiles': heads, 'heroLookSourceFiles': look,
        'note': 'HERO_SYNC %s: work/jaw-sculpt-2026-10-12/jaw_sync.sh export of the staged Unity heroes (jaw sculpt pass 3)' % datetime.date.today().isoformat()})
order = ['MatchHero_Male', 'MatchHero_Female', 'MatchHero_Male_Distance', 'MatchHero_Female_Distance', 'GolfKitHero_Male', 'GolfKitHero_Female']
rows.sort(key=lambda r: order.index(r['name']) if r['name'] in order else len(order))
out = os.path.join(repo, 'GolfArcadeTests/VisualGolden/provenance.json')
json.dump(rows, open(out, 'w'), indent=2); open(out, 'a').write('\n')
print('  provenance: %d exports, %d guarded look sources (staged versions)' % (len(rows), len(look)))
