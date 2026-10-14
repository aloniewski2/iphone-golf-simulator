"""Summarise AccessoriesClipCheck CSVs into clipcheck/CLIPCHECK.md.  python3 py/clip_summary.py out.md csv [csv ...]
Per piece: clips checked, clips with any contact >= 1.5 mm (skin in accessory / cloth in accessory / accessory in body), worst rows."""
import csv, sys, collections
out, files = sys.argv[1], sys.argv[2:]
rows = []; seen = set()
for f in files:                      # first file wins for a (hero, clip, piece) that appears in several runs
    for r in csv.DictReader(open(f)):
        k = (r['hero'], r['clip'], r['piece'])
        if k in seen: continue
        seen.add(k); rows.append(r)
def v(r, k): return float(r[k])
def worst(r): return max(v(r, 'body_in_acc_mm'), v(r, 'cloth_in_acc_mm'), v(r, 'acc_in_body_mm'))
MOVES = ('Walk', 'Run')
def kind(c):
    c0 = c.replace('_', '')
    if c0.startswith('Emote') or c0.startswith('Intro'): return 'emote / intro'
    if c0.startswith(MOVES): return 'walk / run'
    if any(k in c0 for k in ('Swing', 'Drive', 'Putt', 'Chip', 'Forehand', 'Backhand', 'Serve', 'Volley', 'Overhead', 'Return', 'Slice', 'Split', 'Recover', 'HitPerfect', 'MissWhiff')): return 'swing / stroke'
    return 'idle / celebrate / other'
L = ['# Accessories clip check', '', 'Poses of every emote / intro / Walk / Run / swing clip (10 samples a second) with the accessories on; Unity skinning (BakeMesh). Depths in mm, search radius 20 mm.',
     'Columns: SKIN = skin vertices inside the accessory wall; CLOTH = garment vertices inside it; ACC-IN-BODY = accessory vertices inside the body (a glove\'s own hand excluded).', '']
by = collections.defaultdict(list)
for r in rows: by[(r['piece'])].append(r)
for piece, rs in sorted(by.items()):
    L.append(f'## {piece}'); L.append('')
    L.append('| group | clips (hero x clip) | with contact >= 1.5 mm | worst |'); L.append('|---|---|---|---|')
    groups = collections.defaultdict(list)
    for r in rs: groups[kind(r['clip'])].append(r)
    for g, gr in sorted(groups.items()):
        bad = [r for r in gr if worst(r) >= 1.5]
        w = max(gr, key=worst)
        L.append(f"| {g} | {len(gr)} | {len(bad)} | {worst(w):.1f} mm ({w['hero']} {w['clip']}) |")
    L.append('')
    bad = sorted([r for r in rs if worst(r) >= 1.5], key=lambda r: -worst(r))
    if bad:
        L.append('| hero | clip | skin in acc (n / mm) | cloth in acc (n / mm) | acc in body (n / mm) | worst t |'); L.append('|---|---|---|---|---|---|')
        for r in bad[:40]:
            L.append(f"| {r['hero']} | {r['clip']} | {r['body_in_acc_n']} / {r['body_in_acc_mm']} | {r['cloth_in_acc_n']} / {r['cloth_in_acc_mm']} | {r['acc_in_body_n']} / {r['acc_in_body_mm']} | {r['acc_t']} |")
        L.append('')
open(out, 'w').write('\n'.join(L)); print(out, len(rows), 'rows')
