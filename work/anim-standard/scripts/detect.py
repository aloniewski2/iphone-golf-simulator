"""detect.py <breakdown_dir> <context> [clipdir ...] - automatic candidates per clip -> <clip>/auto.json
  lining : the inside of a garment showing (ID pass: lining-side front faces or outer-side back faces), as a blob that its own
           garment's outside surrounds (>= FRAC of its border) - a tear / fold-through, not an opening (sleeve, hem, collar)
  body   : the inside of the body showing (body back faces, head excluded) - hidden skin removed under a garment that pulled away
  hands  : from hands.json (C#): hand in body / under cloth / hands overlap"""
import glob, json, os, sys
from collections import deque
import numpy as np
from PIL import Image

MIN_AREA, FRAC, BODY_MIN = 10, 0.6, 12
VIEWS = ['front', 'back', 'left', 'right']


def comps(mask):
    """8-connected components of a boolean mask: list of (ys, xs)"""
    seen = np.zeros_like(mask); H, W = mask.shape; out = []
    for y0, x0 in zip(*np.nonzero(mask)):
        if seen[y0, x0]: continue
        q = deque([(y0, x0)]); seen[y0, x0] = True; ys, xs = [], []
        while q:
            y, x = q.popleft(); ys.append(y); xs.append(x)
            for dy in (-1, 0, 1):
                for dx in (-1, 0, 1):
                    yy, xx = y + dy, x + dx
                    if 0 <= yy < H and 0 <= xx < W and mask[yy, xx] and not seen[yy, xx]:
                        seen[yy, xx] = True; q.append((yy, xx))
        out.append((np.array(ys), np.array(xs)))
    return out


def ring(ys, xs, shape):
    m = np.zeros(shape, bool); m[ys, xs] = True
    d = m.copy()
    d[1:] |= m[:-1]; d[:-1] |= m[1:]; d[:, 1:] |= m[:, :-1]; d[:, :-1] |= m[:, 1:]
    d[1:, 1:] |= m[:-1, :-1]; d[:-1, :-1] |= m[1:, 1:]; d[1:, :-1] |= m[:-1, 1:]; d[:-1, 1:] |= m[1:, :-1]
    return d & ~m


def analyse(png, legend):
    a = np.array(Image.open(png).convert('RGB')).astype(int)
    R, G, B = a[..., 0], a[..., 1], a[..., 2]
    idm = np.rint(R / 20).astype(int); front = G > 200; back = (G > 100) & (G < 165)
    found = []
    for gid, name in legend.items():
        gid = int(gid)
        if not name.startswith('kit:'): continue
        g = idm == gid
        inside = g & ((front & (B > 192)) | (back & (B < 64)))        # B 64..192 = natural in the reference pose (ignored)
        outside = g & ~inside & (front | back)
        shoe = 'Shoe' in name
        for ys, xs in comps(inside):
            if len(ys) < (80 if shoe else MIN_AREA): continue
            rg = ring(ys, xs, inside.shape); n = rg.sum()
            frac = (outside & rg).sum() / max(1, n)
            skin_border = ((idm == 1) & rg).sum() / max(1, n)     # an opening (hem, cuff, collar, leg hole) meets skin
            # the collar zone (head / neck skin within 14 px) is ambiguous for the wall test: skip it
            y0, y1, x0, x1 = max(0, ys.min() - 14), ys.max() + 15, max(0, xs.min() - 14), xs.max() + 15
            near_head = ((idm[y0:y1, x0:x1] == 1) & (B[y0:y1, x0:x1] > 192)).any()
            if frac >= FRAC and skin_border < .15 and not near_head:
                found.append({'kind': 'lining', 'what': name[4:], 'x': float(xs.mean()), 'y': float(ys.mean()),
                              'r': float(max(10, 1.3 * max(np.ptp(xs), np.ptp(ys)) / 2 + 6)), 'area': int(len(ys)), 'frac': round(float(frac), 2)})
    # torso / pelvis skin visible inside the garment's outline: the body coming through the clothes (bordered mostly by garment)
    garm = (idm >= 2) & (idm <= 8)
    skin = (idm == 1) & front & (B > 64) & (B < 192)
    for ys, xs in comps(skin):
        if len(ys) < BODY_MIN: continue
        rg = ring(ys, xs, skin.shape); frac = (garm & rg).sum() / max(1, rg.sum())
        if frac >= .75:
            found.append({'kind': 'skin', 'what': 'skin through clothes', 'x': float(xs.mean()), 'y': float(ys.mean()),
                          'r': float(max(10, 1.3 * max(np.ptp(xs), np.ptp(ys)) / 2 + 6)), 'area': int(len(ys)), 'frac': round(float(frac), 2)})
    # see-through holes: background enclosed by the character and bordered by one garment
    bg = (G < 60) & (R < 10)
    H, W = bg.shape
    for ys, xs in comps(bg):
        if len(ys) < MIN_AREA or len(ys) > 4000 or ys.min() == 0 or xs.min() == 0 or ys.max() == H - 1 or xs.max() == W - 1: continue
        rg = ring(ys, xs, bg.shape); ids = idm[rg]; n = len(ids)
        vals, cnt = np.unique(ids, return_counts=True); top = vals[np.argmax(cnt)]
        if 2 <= top <= 8 and cnt.max() / n >= .8:
            found.append({'kind': 'hole', 'what': 'see-through ' + legend.get(str(int(top)), '?').replace('kit:', ''), 'x': float(xs.mean()), 'y': float(ys.mean()),
                          'r': float(max(10, 1.3 * max(np.ptp(xs), np.ptp(ys)) / 2 + 6)), 'area': int(len(ys))})
    return found


def one(bd, ctx, dn, legend):
    from collections import Counter
    d = os.path.join(bd, ctx, dn); res = []
    K = len(glob.glob(os.path.join(d, 'f*_front.jpg')))
    for k in range(K):
        for v in VIEWS:
            p = os.path.join(d, f'f{k:02d}_{v}_id.png')
            if not os.path.exists(p): continue
            for f in analyse(p, legend): f['img'] = f'f{k:02d}_{v}'; res.append(f)
    hp = os.path.join(d, 'hands.json')
    if os.path.exists(hp):
        for h in json.load(open(hp)):
            if h['depth'] < 6: continue      # contact, not a visible pass-through
            res.append({'kind': 'hand', 'what': f"{h['kind']} {h['side']}", 'x': h['x'], 'y': h['y'], 'r': 22 if 'hand' in h['img'] else 18,
                        'depth': h['depth'], 'n': h['n'], 'img': h['img']})
    json.dump(res, open(os.path.join(d, 'auto.json'), 'w'), indent=0)
    return dn + ' ' + str(dict(Counter((r['kind'], r['what']) for r in res)))


if __name__ == '__main__':
    bd, ctx = sys.argv[1], sys.argv[2]
    legend = json.load(open(os.path.join(bd, ctx, 'legend.json'))) if os.path.exists(os.path.join(bd, ctx, 'legend.json')) else \
        {"1": "body", "2": "kit:Kit_Top", "3": "kit:Kit_Bottom", "4": "kit:Kit_Sock_L", "5": "kit:Kit_Sock_R", "6": "kit:Kit_Shoe_L", "7": "kit:Kit_Shoe_R"}
    dirs = sys.argv[3:] or sorted(os.path.basename(os.path.dirname(p)) for p in glob.glob(os.path.join(bd, ctx, '*', 'hands.json')))
    from multiprocessing import Pool
    with Pool(8) as pool:
        for line in pool.starmap(one, [(bd, ctx, dn, legend) for dn in dirs]): print(line)


