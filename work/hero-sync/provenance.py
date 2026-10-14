#!/usr/bin/env python3
"""Hero sync step 4: rewrite GolfArcadeTests/VisualGolden/provenance.json for freshly packed goldens.

usage: provenance.py <repo> <golden json dir>
Per hero export: the Unity golden it was packed from, the packed file, the installed manifest, the head FBX hashes (checked by
testNativeHeroesAreExportedFromTheCurrentUnityHeads) and the hashes of every file in look_sources.txt (checked by
testNativeHeroesMatchTheCurrentUnityHeroLook), so a hero-look change without a re-sync fails the native tests.
"""
import datetime, glob, hashlib, json, os, sys

repo, golden = sys.argv[1], sys.argv[2]
here = os.path.dirname(os.path.abspath(__file__))
sha = lambda p: hashlib.sha256(open(p, 'rb').read()).hexdigest()
rel = lambda p: os.path.relpath(p, repo)
sources = [l.strip() for l in open(os.path.join(here, 'look_sources.txt')) if l.strip() and not l.startswith('#')]
look = {s: sha(os.path.join(repo, s)) for s in sources}
heads = {s: look[s] for s in sources if s.startswith('Unity/Assets/Resources/Tennis/OriginalSeamBlinkFinish/')}
rows = []
for src in sorted(glob.glob(os.path.join(golden, '*_golden.json'))):
    name = os.path.basename(src)[:-len('_golden.json')]
    packed = os.path.join(repo, 'GolfArcadeTests/VisualGolden', name + '_golden.lzfse')
    manifest = os.path.join(repo, 'GolfArcade/Unity/CharacterAssets', name + '.json')
    rows.append({
        'name': name, 'source': rel(src), 'unitySampleCount': len(json.load(open(src))['samples']),
        'unitySourceSha256': sha(src), 'packedSha256': sha(packed), 'packedBytes': os.path.getsize(packed), 'manifestSha256': sha(manifest),
        'frozenFaceSourceFiles': heads, 'heroLookSourceFiles': look,
        'note': 'HERO_SYNC %s: work/hero-sync/sync_heroes.sh export of the current Unity heroes' % datetime.date.today().isoformat()})
order = ['MatchHero_Male', 'MatchHero_Female', 'MatchHero_Male_Distance', 'MatchHero_Female_Distance', 'GolfKitHero_Male', 'GolfKitHero_Female']
rows.sort(key=lambda r: order.index(r['name']) if r['name'] in order else len(order))
out = os.path.join(repo, 'GolfArcadeTests/VisualGolden/provenance.json')
json.dump(rows, open(out, 'w'), indent=2); open(out, 'a').write('\n')
print('  provenance: %d exports, %d guarded look sources' % (len(rows), len(look)))
