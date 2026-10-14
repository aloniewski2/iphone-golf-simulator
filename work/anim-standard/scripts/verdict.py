"""Record my review of one clip in verdicts.json (work/anim-standard/verdicts.json).
  verdict.py acc <ctx>/<clipdir> <kind> <what|*> <f0-f1> "<label>" [views]   accept automatic candidates (kind lining/skin/hole/hand;
                                                                        what = substring; views = comma list of front,back,left,right,handL,handR)
  verdict.py man <ctx>/<clipdir> <page> <scale> "x,y,r,label" ...       manual circle in DISPLAY coords of review page <page> (as read)
  verdict.py man2 <ctx>/<clipdir> <scale> "x,y,r,label" ...              manual circle in DISPLAY coords of the review2 sheet (A overview or B hands)
  verdict.py hand <ctx>/<clipdir> <L|R> <f0-f1> "<label>"                 circle the whole hand in its close-up (the hand is centred there)
  verdict.py sum <ctx>/<clipdir> "<one line>" ...                        summary lines (what is wrong in this clip)
  verdict.py ok  <ctx>/<clipdir>                                          reviewed, nothing wrong
  verdict.py clear <ctx>/<clipdir>"""
import json, os, sys
V = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'verdicts.json')
TW, TH, HEAD, LAB, HW, HH, ROWS = 400, 560, 46, 22, 240, 280, 4
VIEWS = ['front', 'back', 'left', 'right']
vd = json.load(open(V)) if os.path.exists(V) else {}
op, key = sys.argv[1], sys.argv[2]
e = vd.setdefault(key, {'accept': [], 'manual': [], 'summary': [], 'reviewed': False})
if op == 'acc':
    kind, what, fr, label = sys.argv[3:7]; f0, f1 = (int(x) for x in fr.split('-')) if '-' in fr else (int(fr), int(fr))
    acc = {'kind': kind, 'what': what, 'f0': f0, 'f1': f1, 'label': label}
    if len(sys.argv) > 7: acc['views'] = sys.argv[7].split(',')
    e['accept'].append(acc)
elif op == 'man':
    page, k = int(sys.argv[3]), float(sys.argv[4])
    for a in sys.argv[5:]:
        x, y, r, label = a.split(',', 3); x, y, r = float(x) * k, float(y) * k, float(r) * k
        row = int((y - HEAD) // (TH + LAB)); frame = (page - 1) * ROWS + row
        ty = y - HEAD - row * (TH + LAB) - LAB
        if x < 4 * TW: img, tx = VIEWS[int(x // TW)], x - int(x // TW) * TW
        else: img, tx = ('handL' if ty < HH else 'handR'), x - 4 * TW; ty = ty if ty < HH else ty - HH
        e['manual'].append({'frame': frame, 'img': img, 'x': round(tx), 'y': round(ty), 'r': round(r), 'label': label.strip()})
elif op == 'man2':
    k = float(sys.argv[3]); CW, CH2 = 120, 280
    yB = 40 + 4 * (CH2 + 18) + 30
    for a in sys.argv[4:]:
        x, y, r, label = a.split(',', 3); x, y, r = float(x) * k, float(y) * k, float(r) * k
        if y < yB - 30:
            vi = int((y - 40) // (CH2 + 18)); kf = int(x // CW)
            tx, ty, tr = (x - kf * CW) * 2 + 80, (y - 40 - vi * (CH2 + 18) - 18) * 2, r * 2
            e['manual'].append({'frame': kf, 'img': VIEWS[vi], 'x': round(tx), 'y': round(ty), 'r': round(tr), 'label': label.strip()})
        else:
            rr = int((y - yB) // (HH + 18)); j = int(x // HW); kf = (rr // 2) * 8 + j
            tx, ty = x - j * HW, y - yB - rr * (HH + 18) - 18
            e['manual'].append({'frame': kf, 'img': 'handL' if rr % 2 == 0 else 'handR', 'x': round(tx), 'y': round(ty), 'r': round(r), 'label': label.strip()})
elif op == 'hand':
    side, fr, label = sys.argv[3], sys.argv[4], sys.argv[5]; f0, f1 = (int(x) for x in fr.split('-')) if '-' in fr else (int(fr), int(fr))
    for kf in range(f0, f1 + 1):
        e['manual'].append({'frame': kf, 'img': 'handL' if side == 'L' else 'handR', 'x': 120, 'y': 150, 'r': 80, 'label': label})
elif op == 'sum':
    e['summary'] += sys.argv[3:]
elif op == 'ok':
    pass
elif op == 'clear':
    vd[key] = {'accept': [], 'manual': [], 'summary': [], 'reviewed': False}; e = vd[key]
if op != 'clear': e['reviewed'] = True
json.dump(vd, open(V, 'w'), indent=1)
print(key, f"accept {len(e['accept'])} manual {len(e['manual'])} summary {len(e['summary'])}")
