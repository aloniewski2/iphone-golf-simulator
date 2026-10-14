"""assemble.py <audit_dir> <raw_dir> - stage 2: close-up stills + CLIPPING_AUDIT.md
 audit_dir = work/emote-standard/audit (episodes.json, overrides.json, areas_*.json after the Unity close-up pass)
 raw_dir   = the scratchpad folder that holds cu/<id>.jpg (Unity close-ups)"""
import glob, json, math, os, sys
from collections import defaultdict, Counter
from PIL import Image, ImageDraw, ImageFont

AUD, RAW = sys.argv[1], sys.argv[2]
ROOT = os.path.abspath(os.path.join(AUD, '..'))
IMG = os.path.join(ROOT, 'CLIPPING_AUDIT_images')
SEV = ['small', 'noticeable', 'very obvious']
BOLD = ImageFont.truetype('/System/Library/Fonts/Supplemental/Arial Bold.ttf', 22)
SM = ImageFont.truetype('/System/Library/Fonts/Supplemental/Arial.ttf', 16)

AREA = {'hips': 'pelvis / waist', 'waist': 'lower torso', 'chest': 'chest / upper torso', 'neckhead': 'neck / head',
        'LShoulder': 'left shoulder', 'RShoulder': 'right shoulder', 'LUpperArm': 'left upper arm / armpit', 'RUpperArm': 'right upper arm / armpit',
        'LForearm': 'left forearm / hand', 'RForearm': 'right forearm / hand', 'Lpelvis': 'left hip / groin', 'Rpelvis': 'right hip / groin',
        'LThigh': 'left thigh', 'RThigh': 'right thigh', 'LShin': 'left shin / knee', 'RShin': 'right shin / knee', 'LFoot': 'left foot / shoe', 'RFoot': 'right foot / shoe'}


def load_areas():
    out = {}
    for p in glob.glob(os.path.join(RAW, 'areas_*.json')):
        for a in json.load(open(p)): out[a['out']] = a['area'].split(':')[0]
    return out


def key_of(e, area):
    v = e['variant'] if e['kind'] == 'hint' and e['what'].startswith(('hand', 'racket')) else ''
    return f"{e['ctx']}|{e['emote']}|{v}|{e['kind']}|{e['what']}|{area}"


def main():
    eps = json.load(open(os.path.join(AUD, 'episodes.json')))
    cov = json.load(open(os.path.join(AUD, 'coverage.json')))
    ov = json.load(open(os.path.join(AUD, 'overrides.json'))) if os.path.exists(os.path.join(AUD, 'overrides.json')) else {}
    areas = load_areas()
    os.makedirs(IMG, exist_ok=True)
    # --- merge episodes of one clip + problem + body area
    groups = defaultdict(list)
    for e in eps:
        cu = f"cu/{e['id']:04d}.jpg"
        e['area'] = areas.get(cu, '?')
        if e['kind'].startswith('feet'): e['area'] = 'LFoot' if e['side'] == 'L' else 'RFoot'
        if not os.path.exists(os.path.join(RAW, cu)): e['noimg'] = True
        groups[key_of(e, e['area'])].append(e)
    rows = []
    for k, es in groups.items():
        o = ov.get(k, {})
        if o.get('drop'): continue
        worst = max(es, key=lambda e: (e['sev'], e['metric'] / HI(e)))
        sev = o.get('sev', worst['sev'])
        rows.append(dict(key=k, e=worst, all=es, sev=sev, score=sev * 100 + min(99, 50 * worst['metric'] / HI(worst)), note=o.get('note', ''), issue=o.get('issue', worst['issue'])))
    rows.sort(key=lambda r: -r['score'])
    for i, r in enumerate(rows): r['n'] = i + 1
    # --- stills
    for r in rows:
        e = r['e']; src = os.path.join(RAW, f"cu/{e['id']:04d}.jpg"); dst = os.path.join(IMG, f"{r['n']:03d}_{e['emote'].replace(' ', '')}_{e['hero']}_{e['kit']}.jpg"); r['img'] = os.path.basename(dst)
        if not os.path.exists(src): continue
        im = Image.open(src).convert('RGB').resize((640, 640), Image.LANCZOS); d = ImageDraw.Draw(im, 'RGBA')
        rr = 140 if e['what'].endswith(('Bottom', 'Top')) and 'intersects' in e['what'] else 78
        c = 320; d.ellipse((c - rr, c - rr, c + rr, c + rr), outline=(255, 40, 40, 255), width=5)
        d.rectangle((0, 0, 640, 34), fill=(0, 0, 0, 190))
        d.text((8, 6), f"#{r['n']:03d}  {e['emote']} - {e['hero']} - {e['kit']} kit - f{e['fw']}  [{SEV[r['sev']]}]", fill=(255, 235, 150, 255), font=SM)
        im.save(dst, quality=86)
    json.dump([dict(n=r['n'], key=r['key'], sev=SEV[r['sev']], emote=r['e']['emote'], variant=r['e']['variant'], hero=r['e']['hero'], kit=r['e']['kit'], kind=r['e']['kind'], what=r['e']['what'],
                    area=AREA.get(r['e']['area'], r['e']['area']), issue=r['issue'], metric=round(r['e']['metric'], 1), unit=r['e']['unit'],
                    frames=[(x['f0'], x['f1']) for x in sorted(r['all'], key=lambda x: x['f0'])], worst_frame=r['e']['fw'], img=r['img']) for r in rows], open(os.path.join(ROOT, 'CLIPPING_AUDIT.json'), 'w'), indent=1)
    return rows, cov


def HI(e):
    k = e['kind']; w = e['what']
    if e['unit'] == 'pts': return 250 if not w.startswith('racket') else 150
    if k.startswith('cloth'): return 120 if 'lining' in k else 80
    if k == 'feet-sink': return 20
    if k == 'feet-slide': return 80
    if w.startswith('hand in'): return 35
    if w.startswith('hand under'): return 38
    if w.startswith('hands'): return 28
    if w.startswith('racket'): return 32
    return 22


if __name__ == '__main__':
    rows, cov = main()
    print(len(rows), Counter(SEV[r['sev']] for r in rows))


def frames_txt(r):
    parts = []
    for x in sorted(r['all'], key=lambda x: x['f0']):
        parts.append(f"f{x['f0']}" if x['f0'] == x['f1'] else f"f{x['f0']}-{x['f1']}")
    s = ', '.join(parts[:4]) + (f" (+{len(parts) - 4} more)" if len(parts) > 4 else '')
    return f"**f{r['e']['fw']}** ({r['e']['tw']:.2f} s) in {s}"


def metric_txt(e):
    if e['unit'] == 'px': return f"{e['metric']:.0f} px patch"
    if e['unit'] == 'pts': return f"{e['metric']:.0f} {'racket' if e['what'].startswith('racket') else 'hand'} mesh points inside, up to {e.get('depth', 0):.0f} mm deep"
    return f"{e['metric']:.0f} mm"


def write_md(rows, cov, eps, extra_sections):
    L = []
    L.append('# Emote clipping audit\n')
    L.extend(extra_sections['head'])
    L.append('\n## Problem list, worst first\n')
    L.append('Frames are clip frames at 30 fps. "Frame" = the worst frame, then every range where it happens. Metric in brackets: cloth = size of the visible patch in a full-body render; hands / racket = number of hand or racket mesh points inside (capped at 40 mm depth by the test); shirt vs shorts = mm; feet = mm below the rest sole level or mm slid while planted.\n')
    L.append('| # | Emote | Hero | Kit | Frame | Body area | What is wrong | How bad | Close-up |')
    L.append('|--:|---|---|---|---|---|---|---|---|')
    for r in rows:
        e = r['e']; area = AREA.get(e['area'], e['area'])
        if e['kind'] == 'hint' and e['what'].startswith(('hand', 'racket')): pass
        tag = ' (old taunt)' if e['old'] else ''
        vs = sorted({x['variant'] for x in r['all']}); vtxt = ' + '.join(vs) if len(vs) > 1 else e['variant']
        note = f" - {r['note']}" if r['note'] else ''
        L.append(f"| {r['n']} | {e['emote']}{tag}<br>{vtxt} | {e['hero']} | {e['kit']} | {frames_txt(r)} | {area} | {r['issue']} ({metric_txt(e)}){note} | **{SEV[r['sev']]}** | ![](CLIPPING_AUDIT_images/{r['img']}) |")
    L.extend(extra_sections['tail'])
    open(os.path.join(ROOT, 'CLIPPING_AUDIT.md'), 'w').write('\n'.join(L) + '\n')
