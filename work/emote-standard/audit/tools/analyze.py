"""analyze.py <raw_dir_prefix> <out_dir>  - stage 1 of the clipping audit.
Reads the AnimationAudit AA_MODE=emote captures (<raw>/c_<ctx>/<ctx>/<clip>/...), finds every problem episode, grades it, and writes
  <out>/episodes.json   one entry per (clip, hero, kit, problem, area-free key) with frame range, worst frame, metric, severity
  <out>/jobs.json       the close-up renders Unity has to make (worst frame of each episode)
Problems: cloth lining / skin through clothes / see-through holes (ID-pass detector, filtered by the earlier human review),
hand in body / under cloth / hands overlap, racket in body / under cloth, garment through garment, feet below the ground, planted feet sliding."""
import glob, json, math, os, sys
from collections import defaultdict
from multiprocessing import Pool

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, '..', '..', '..', 'anim-standard', 'scripts'))
import detect  # noqa: E402

RAW, OUT = sys.argv[1], sys.argv[2]
OLD = json.load(open(os.path.join(HERE, '..', '..', '..', 'anim-standard', 'verdicts.json')))
OLDBD = os.path.join(HERE, '..', '..', '..', 'anim-standard', 'breakdown')
CTXS = ['tennis_male', 'tennis_female', 'golf_male', 'golf_female']
VIEWS = ['front', 'back', 'left', 'right']
FEET_FPS = 30.0


def names(ctx, clip):
    """emote, variant, old-taunt flag from the clip name"""
    kit = 'tennis' if ctx.startswith('tennis') else 'golf'
    free = clip.startswith('free:') or clip.startswith('golf:')
    bare = clip.split(':', 1)[1]
    if clip.startswith('free:'): variant = 'free hands'
    elif clip.startswith('golf:'): variant = 'golfer clip'
    else: variant = 'racket'
    n = bare.replace('Male_', '').replace('Female_', '').replace('Emote_', '')
    if n.startswith('Run') and n != 'Run' and clip.startswith('free:') is False:
        n = {'RunForward': 'Run forward', 'RunLeft': 'Run left', 'RunRight': 'Run right'}.get(n, n)
    old = n in ('Scuba', 'Spike', 'Thrust')
    return n, variant, old


def sev(rank):
    return ['small', 'noticeable', 'very obvious'][rank]


# ------------------------------------------------------------------ ID-pass detection (cached)
def id_one(args):
    path, legend = args
    return detect.analyse(path, legend)


def id_detect(ctx, base):
    cache = os.path.join(base, 'idcache.json')
    if os.path.exists(cache): return json.load(open(cache))
    legend = json.load(open(os.path.join(base, ctx, 'legend.json')))
    index = json.load(open(os.path.join(base, ctx, 'index.json')))
    jobs = []; keys = []
    for c in index:
        for k in range(c['frames']):
            for v in VIEWS:
                p = os.path.join(base, ctx, c['dir'], f'f{k:02d}_{v}_id.png')
                if os.path.exists(p): jobs.append((p, legend)); keys.append((c['dir'], k, v))
    with Pool(8) as pool: res = pool.map(id_one, jobs, chunksize=8)
    out = defaultdict(list)
    for (d, k, v), r in zip(keys, res):
        for f in r: f['frame'] = k; f['view'] = v; out[d].append(f)
    json.dump(out, open(cache, 'w'))
    return out


def old_range_ok(ctx, d, length, t, kind, what):
    """was this (kind, what) at time t accepted by the earlier human review of the clip?"""
    v = OLD.get(f'{ctx}/{d}')
    if not v: return False
    try: idx = json.load(open(os.path.join(OLDBD, ctx, 'index.json')))
    except Exception: return False
    e = next((x for x in idx if x['dir'] == d), None)
    if not e: return False
    ki = round(t / max(1e-6, e['length']) * (e['frames'] - 1))
    for a in v.get('accept', []):
        if a['kind'] != kind: continue
        if a['what'] not in ('*', what, what.replace('Kit_', '')): continue
        if a['f0'] - 1 <= ki <= a['f1'] + 1: return True
    return False


# ------------------------------------------------------------------ episodes
def episodes(frames, gap=2):
    """frames: {k: (metric, payload)} -> list of (k0, k1, kworst, metric, payload)"""
    ks = sorted(frames); out = []; cur = []
    for k in ks:
        if cur and k - cur[-1] > gap + 0: out.append(cur); cur = []
        cur.append(k)
    if cur: out.append(cur)
    res = []
    for e in out:
        kw = max(e, key=lambda k: frames[k][0])
        res.append((e[0], e[-1], kw, frames[kw][0], frames[kw][1]))
    return res


def grade_cloth(kind, area):
    if kind == 'hole': return 0 if area < 25 else 1 if area < 60 else 2
    if kind == 'lining': return 0 if area < 40 else 1 if area < 120 else 2
    return 0 if area < 25 else 1 if area < 80 else 2           # skin


def grade_depth(kind, depth, n):
    if kind.startswith('hand in body'): lo, hi = 15, 35
    elif kind.startswith('hand under cloth'): lo, hi = 18, 38
    elif kind.startswith('hands overlap'): lo, hi = 12, 28
    elif kind.startswith('racket'): lo, hi = 14, 32
    else: lo, hi = 10, 22                                           # garment intersects garment
    return 0 if depth < lo else 1 if depth < hi else 2


def analyse_clip(ctx, base, c, idres):
    d = c['dir']; length = c['length']; times = c['times']; K = c['frames']
    clip = c['clip']; emote, variant, old = names(ctx, clip)
    hero = 'female' if 'female' in ctx else 'male'; kit = 'tennis' if ctx.startswith('tennis') else 'golf'
    ev = []

    def add(**kw):
        kw.update(ctx=ctx, dir=d, clip=clip, emote=emote, variant=variant, old=old, hero=hero, kit=kit, length=length, mood=c['mood'])
        ev.append(kw)

    # --- cloth (ID pass), filtered by the earlier review
    byk = defaultdict(dict)
    for f in idres.get(d, []):
        t = times[f['frame']]
        kind = f['kind']; what = f['what']
        okold = old_range_ok(ctx, d, length, t, kind if kind != 'hole' else 'hole', what)
        big = f['area'] >= 150
        if kind == 'hole': continue            # every hole seen was background between the legs / nothing visible (checked by eye)
        if kind == 'lining' and what == 'Kit_Bottom' and ctx == 'tennis_male': continue   # the inside of the shorts leg opening (natural)
        if not (okold or big): continue
        key = (kind, what)
        cur = byk[key].get(f['frame'])
        if cur is None or f['area'] > cur[0]: byk[key][f['frame']] = (f['area'], dict(view=f['view'], x=f['x'], y=f['y'], r=f['r'], area=f['area'], confirmed=okold))
    for (kind, what), fr in byk.items():
        for k0, k1, kw, m, pay in episodes(fr):
            issue = {'lining': f'{what} torn open / inside of the cloth shows',
                     'skin': 'skin shows through the clothes',
                     'hole': f'see-through hole in {what.replace("see-through ", "")}'}[kind]
            add(kind='cloth-' + kind, what=what, issue=issue, k0=k0, k1=k1, kw=kw, metric=m, unit='px', sev=grade_cloth(kind, m),
                job=dict(mode='px', view=pay['view'], x=pay['x'], y=pay['y'], r=pay['r'], t=times[kw], skip=''), confirmed=pay['confirmed'])
    # --- hints: hands, racket, garment pairs
    hp = os.path.join(base, ctx, d, 'hands.json')
    hints = json.load(open(hp)) if os.path.exists(hp) else []
    grp = defaultdict(dict)
    for h in hints:
        k = int(h['img'][1:3]); kind = h['kind']; side = h['side']
        if h['depth'] < 8 or h['n'] < 6: continue
        if 'Shoe_' in h['kind'] or 'Sock_' in h['kind']: continue      # sock/shoe overlaps: nothing visible in the stills (checked)
        if 'intersects' in h['kind'] and ctx.startswith('golf'): continue   # tucked polo under the belt: natural overlap (checked)
        # one entry per frame/kind/side: the deepest, remembering the main view it came from
        key = (kind, side)
        cur = grp[key].get(k)
        main = h['img'][4:] in VIEWS
        score = h['n'] if kind.startswith(('hand in', 'hand under', 'racket')) else h['depth']
        if cur is None or score > cur[0]:
            grp[key][k] = (score, dict(n=h['n'], depth=h['depth'], w=(h['wx'], h['wy'], h['wz']), img=h['img']))
    for (kind, side), fr in grp.items():
        for k0, k1, kw, m, pay in episodes(fr):
            if kind.startswith('hand in body'): issue = f'{"left" if side == "L" else "right"} hand pushes into the body'
            elif kind.startswith('hand under cloth'): issue = f'{"left" if side == "L" else "right"} hand disappears under the clothes'
            elif kind.startswith('hands overlap'): issue = 'the two hands go through each other'
            elif kind == 'racket in body': issue = 'racket goes into the body'
            elif kind == 'racket under cloth': issue = 'racket goes through the clothes'
            else: issue = kind.replace('intersects', 'cuts through').replace('Shoe_', 'shoe ').replace('Sock_', 'sock ').replace('Top', 'polo').replace('Bottom', 'shorts/skirt')
            if kind == 'Top intersects Bottom' or kind == 'Bottom intersects Top': issue = 'polo and shorts/skirt cut through each other'
            skip = 'LR' if kind.startswith('racket') else ('L' if side == 'L' else 'R') if kind.startswith('hand') else ''
            wx, wy, wz = pay['w']
            byn = kind.startswith(('hand in', 'hand under', 'racket'))
            sv = (0 if m < (40 if kind.startswith('racket') else 60) else 1 if m < (150 if kind.startswith('racket') else 250) else 2) if byn else grade_depth(kind, m, pay['n'])
            add(kind='hint', what=kind, side=side, issue=issue, k0=k0, k1=k1, kw=kw, metric=m, unit='pts' if byn else 'mm', n=pay['n'], depth=pay['depth'], sev=sv,
                job=dict(mode='world', wx=wx, wy=wy, wz=wz, t=times[kw], skip=skip, view=None, fov=45 if 'intersects' in kind else 0))
    # --- feet
    fp = os.path.join(base, ctx, d, 'feet.json')
    if os.path.exists(fp):
        feet = json.load(open(fp)); dt = length / max(1, len(feet) - 1)
        loco = emote in ('Walk', 'Run', 'Run forward', 'Run left', 'Run right')
        for side in 'LR':
            seq = [f[side] for f in feet]
            # sinking: sole points below the rest ground level
            sink = {i: (-s['ymin'], dict(c=(s['cx'], s['cy'], s['cz']), below=s['below'])) for i, s in enumerate(seq) if s and s['ymin'] < -3 and s['below'] >= 8}
            for i0, i1, iw, m, pay in episodes(sink, gap=2):
                add(kind='feet-sink', what=side, side=side, issue=f'{"left" if side == "L" else "right"} shoe sinks into the ground', k0=i0, k1=i1, kw=iw, metric=m, unit='mm', sev=0 if m < 8 else 1 if m < 20 else 2,
                    n=pay['below'], fps=FEET_FPS, job=dict(mode='world', wx=pay['c'][0], wy=pay['c'][1], wz=pay['c'][2], t=iw * dt, skip='', view=None, fov=14))
            # sliding while planted
            planted = lambda s: s and s['nc'] >= 40 and s['ymin'] < 10
            speeds = [math.hypot(s['dfwd'], s['dright']) / 1000 / dt if planted(s) and s['mag'] >= 0 else None for s in seq]
            if not loco:
                run = []
                segs = []
                for i, sp in enumerate(speeds):
                    if sp is not None: run.append(i)
                    else:
                        if len(run) >= 3: segs.append(run)
                        run = []
                if len(run) >= 3: segs.append(run)
                for sgm in segs:
                    path = math.hypot(sum(seq[i]['dfwd'] for i in sgm), sum(seq[i]['dright'] for i in sgm))       # mm of net contact-point travel
                    peak = max(speeds[i] for i in sgm)
                    if path >= 12:
                        iw = max(sgm, key=lambda i: speeds[i]); s = seq[iw]
                        add(kind='feet-slide', what=side, side=side, issue=f'{"left" if side == "L" else "right"} foot slides {path:.0f} mm on the ground while planted', k0=sgm[0], k1=sgm[-1], kw=iw, metric=path, unit='mm', peak=peak,
                            sev=0 if path < 30 else 1 if path < 80 else 2, fps=FEET_FPS,
                            job=dict(mode='world', wx=s['cx'], wy=s['cy'], wz=s['cz'], t=iw * dt, skip='', view=None, fov=14))
    return ev


def locomotion_report(ctx, base, c):
    """median stance speed of a walk / run clip against the speed the game plays it at"""
    feet = json.load(open(os.path.join(base, ctx, c['dir'], 'feet.json'))); dt = c['length'] / max(1, len(feet) - 1)
    vs = []
    for f in feet:
        for side in 'LR':
            s = f[side]
            if s and s['nc'] >= 40 and s['ymin'] < 10 and s['mag'] >= 0: vs.append(math.hypot(s['dfwd'], s['dright']) / 1000 / dt)
    if not vs: return None
    vs.sort(); med = vs[len(vs) // 2]
    # stance runs: a planted foot's speed should be constant over the stance
    spread = (vs[int(len(vs) * .9)] - vs[int(len(vs) * .1)]) / max(1e-6, med)
    return dict(median=med, spread=spread, n=len(vs))


def main():
    os.makedirs(OUT, exist_ok=True)
    allev = []; loco = []; coverage = []
    for ctx in CTXS:
        base = f'{RAW}{ctx}'
        if not os.path.exists(os.path.join(base, ctx, 'index.json')): print('missing', ctx); continue
        idres = id_detect(ctx, base)
        for c in json.load(open(os.path.join(base, ctx, 'index.json'))):
            ev = analyse_clip(ctx, base, c, idres)
            allev += ev
            emote, variant, old = names(ctx, c['clip'])
            coverage.append(dict(ctx=ctx, clip=c['clip'], dir=c['dir'], emote=emote, variant=variant, frames=c['frames'], length=c['length'], mood=c['mood']))
            if emote in ('Walk', 'Run', 'Run forward', 'Run left', 'Run right'):
                r = locomotion_report(ctx, base, c)
                if r: r.update(ctx=ctx, clip=c['clip'], emote=emote, variant=variant); loco.append(r)
    # ids + jobs
    allev.sort(key=lambda e: (-e['sev'], -e['metric']))
    jobs = []
    for i, e in enumerate(allev): e['id'] = i + 1
    # times from the index (image frames) / feet samples
    idx = {}
    for ctx in CTXS:
        p = os.path.join(f'{RAW}{ctx}', ctx, 'index.json')
        if os.path.exists(p):
            for c in json.load(open(p)): idx[(ctx, c['dir'])] = c
    for e in allev:
        c = idx[(e['ctx'], e['dir'])]
        if e.get('fps'):
            dt = c['length'] / max(1, round(c['length'] * e['fps']))
            e['t0'], e['t1'], e['tw'] = e['k0'] * dt, e['k1'] * dt, e['kw'] * dt
        else:
            e['t0'], e['t1'], e['tw'] = c['times'][e['k0']], c['times'][e['k1']], c['times'][e['kw']]
        e['f0'], e['f1'], e['fw'] = round(e['t0'] * 30), round(e['t1'] * 30), round(e['tw'] * 30)
        j = e['job']
        if j.get('view') is None:        # world-mode: the camera on the side of the body the point is on (hero faces +z, right = +x)
            vx, vz = j['wx'], j['wz']
            if e['kind'].startswith('feet'): j['view'] = 'front' if abs(vz) >= abs(vx) else ('right' if vx > 0 else 'left')
            else: j['view'] = ('front' if vz >= 0 else 'back') if abs(vz) >= abs(vx) else ('right' if vx > 0 else 'left')
        j.update(ctx=e['ctx'], clip=e['clip'], out=f'cu/{e["id"]:04d}.jpg')
        jobs.append(j)
    json.dump(allev, open(os.path.join(OUT, 'episodes.json'), 'w'), indent=1)
    json.dump(dict(jobs=[{k: v for k, v in j.items() if k in ('ctx', 'clip', 'view', 'mode', 'out', 'skip', 't', 'x', 'y', 'wx', 'wy', 'wz', 'fov')} for j in jobs]), open(os.path.join(OUT, 'jobs.json'), 'w'))
    json.dump(dict(coverage=coverage, loco=loco), open(os.path.join(OUT, 'coverage.json'), 'w'), indent=1)
    from collections import Counter
    print(len(allev), Counter(sev(e['sev']) for e in allev), Counter(e['kind'] + ':' + e['what'] for e in allev).most_common(12))


if __name__ == '__main__':
    main()
