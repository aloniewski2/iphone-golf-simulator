"""report.py <breakdown_dir> <out_dir> - the browsable index of the deformity breakdown (one HTML page + small circled thumbnails).
Per context and clip: what is wrong (my summary lines from verdicts.json), issue badges, and up to 4 circled tiles (the frames / views
with the most circles). The full image-by-image pages stay in <breakdown_dir>/pages/final/<context>/."""
import html, json, os, re, sys
from collections import Counter, defaultdict
from PIL import Image, ImageDraw, ImageFont

bd, out = sys.argv[1], sys.argv[2]
os.makedirs(os.path.join(out, 'img'), exist_ok=True)
V = json.load(open(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'verdicts.json')))
sm = ImageFont.truetype('/System/Library/Fonts/Supplemental/Arial Bold.ttf', 15)
CTX = [('tennis_male', 'Tennis - male'), ('tennis_female', 'Tennis - female'), ('golf_male', 'Golf - male'), ('golf_female', 'Golf - female')]
CATS = [('cloth', 'Clothes tear or clip', []), ('into', 'Hand or arm through body / clothes', []),
        ('grip', 'Hand holds nothing', []), ('hand', 'Awkward hand shape', [])]


def cats_of(label):
    l = label.lower()
    if 'invisible' in l: return ['grip']
    if re.search(r'\b(sink|sinks|buried|overlap)\b', l) or re.search(r'(?<!stretches )(?<!shreds )(?<!pulled )\binto\b', l) \
            or 'through the face' in l or 'through the other arm' in l or 'misses the head' in l:
        return ['into']
    if re.search(r'polo|shirt|sleeve|torn|tear|seam|hole|skin|skirt|shorts|trouser|waistband|slit|seat|hem|crease|bunch|shred|cuff|streak|split|gap', l):
        return ['cloth']
    if re.search(r'claw|splay|flat|paddle|stiff|thumb|fist|palm|open|finger|starfish|pinch|loose|cup|hand', l):
        return ['hand']
    return ['other']


def circles(ctx, dn, auto, ver):
    """{(frame, img): [(x, y, r, label)]} - exactly what sheets.py --final draws"""
    out = defaultdict(list)
    for a in auto:
        if a['kind'] == 'lining' and a['what'].startswith('Kit_Shoe') and a['area'] < 80: continue
        k, img = int(a['img'][1:3]), a['img'][4:]
        for acc in ver['accept']:
            if a['kind'] == acc['kind'] and acc['f0'] <= k <= acc['f1'] and (acc['what'] == '*' or acc['what'] in a['what']) and img in acc.get('views', [img]):
                out[(k, img)].append((a['x'], a['y'], a['r'], acc['label'])); break
    for m in ver['manual']:
        out[(m['frame'], m['img'])].append((m['x'], m['y'], m['r'], m['label']))
    return out


def tile(ctx, dn, k, img, items, scale):
    im = Image.open(os.path.join(bd, ctx, dn, f'f{k:02d}_{img}.jpg')).convert('RGB')
    dr = ImageDraw.Draw(im, 'RGBA')
    for x, y, r, _ in items: dr.ellipse([x - r, y - r, x + r, y + r], outline=(255, 30, 30), width=4)
    said = set(); ly = 4
    for x, y, r, label in items:
        if label in said: continue
        said.add(label); tw = dr.textlength(label, font=sm)
        dr.rectangle([4, ly, 8 + tw, ly + 18], fill=(220, 20, 20, 225)); dr.text((6, ly + 1), label, fill=(255, 255, 255), font=sm); ly += 20
    w, h = im.size
    im = im.resize((int(w * scale), int(h * scale)), Image.LANCZOS)
    name = f'{ctx}__{dn}__f{k:02d}_{img}.jpg'
    im.save(os.path.join(out, 'img', name), quality=80)
    return name


totals = Counter(); clip_cats = Counter(); sections = []; nclips = 0
for ctx, title in CTX:
    ip = os.path.join(bd, ctx, 'index.json')
    if not os.path.exists(ip): continue
    rows = []
    for c in json.load(open(ip)):
        dn = c['dir']; key = f'{ctx}/{dn}'
        ver = V.get(key)
        if not ver or not ver.get('reviewed'): continue
        nclips += 1
        auto = json.load(open(os.path.join(bd, ctx, dn, 'auto.json')))
        circ = circles(ctx, dn, auto, ver)
        labels = Counter(l for items in circ.values() for *_, l in items)
        cs = sorted({k for l in labels for k in cats_of(l)})
        for k in cs: clip_cats[k] += 1
        n = sum(len(v) for v in circ.values()); totals['circles'] += n
        frames = sorted({k for k, _ in circ})
        # thumbnails: the busiest frame/view per distinct label, at most 4
        picks, covered = [], set()
        for (k, img), items in sorted(circ.items(), key=lambda kv: -len(kv[1])):
            new = {l for *_, l in items} - covered
            if not new: continue
            picks.append((k, img, items)); covered |= {l for *_, l in items}
            if len(picks) == 4: break
        thumbs = [(tile(ctx, dn, k, img, items, .58 if 'hand' not in img else .82), k, img) for k, img, items in picks]
        npages = (c['frames'] + 3) // 4
        rows.append(dict(clip=c['clip'], dir=dn, frames=c['frames'], length=c['length'], n=n, cats=cs, summary=ver.get('summary', []),
                         bad=len(frames), thumbs=thumbs, pages=[os.path.relpath(os.path.join(bd, 'pages', 'final', ctx, f'{dn}_p{p:02d}.jpg'), out) for p in range(1, npages + 1)]))
    sections.append((ctx, title, rows))

catname = {k: t for k, t, _ in CATS}; catname['other'] = 'Other'
def badge(k): return f'<span class="b b-{k}">{html.escape(catname[k])}</span>'
parts = []
for ctx, title, rows in sections:
    bad = sum(1 for r in rows if r['n'])
    parts.append(f'<section id="{ctx}"><h2>{html.escape(title)} <small>{len(rows)} clips · {bad} with problems</small></h2>')
    for r in rows:
        nm = r['clip'].replace('free:', 'plaza / golf (empty hands): ')
        summ = ''.join(f'<li>{html.escape(s)}</li>' for s in r['summary']) or '<li class="ok">No problems found.</li>'
        th = ''.join(f'<figure><a href="img/{t}"><img loading="lazy" src="img/{t}" alt=""></a><figcaption>frame {k} · {img}</figcaption></figure>' for t, k, img in r['thumbs'])
        pages = ' '.join(f'<a href="{html.escape(p)}">page {i + 1}</a>' for i, p in enumerate(r['pages']))
        parts.append(f'<article class="{"clean" if not r["n"] else ""}"><header><h3>{html.escape(nm)}</h3><span class="meta">{r["frames"]} frames · {r["length"]} s · '
                     f'{r["bad"]} frames circled</span>{"".join(badge(k) for k in r["cats"])}</header><ul>{summ}</ul>'
                     f'<div class="thumbs">{th}</div><p class="pages">Every frame, circled: {pages}</p></article>')
    parts.append('</section>')
stats = ''.join(f'<div class="stat"><b>{clip_cats.get(k, 0)}</b><span>{html.escape(t)}</span></div>' for k, t, _ in CATS)
nav = ' · '.join(f'<a href="#{c}">{html.escape(t)}</a>' for c, t, _ in sections)
page = f'''<!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>Animation Deformity Breakdown</title><style>
:root{{--bg:#f6f5f2;--fg:#1d1d1f;--mut:#6b6b70;--card:#fff;--line:#e3e1dc;--red:#d42b2b;--c1:#c0392b;--c2:#8e44ad;--c3:#d35400;--c4:#2471a3}}
@media (prefers-color-scheme: dark){{:root:not([data-theme="light"]){{--bg:#151517;--fg:#ececee;--mut:#9a9aa2;--card:#1f1f23;--line:#2e2e34}}}}
:root[data-theme="dark"]{{--bg:#151517;--fg:#ececee;--mut:#9a9aa2;--card:#1f1f23;--line:#2e2e34}}
body{{margin:0;background:var(--bg);color:var(--fg);font:15px/1.45 -apple-system,system-ui,Segoe UI,Roboto,sans-serif}}
main{{max-width:1180px;margin:0 auto;padding:24px 16px 64px}} h1{{margin:0 0 4px;font-size:26px}} .lead{{color:var(--mut);margin:0 0 18px}}
nav{{margin:10px 0 22px}} nav a{{color:var(--c4)}} h2{{margin:34px 0 10px;font-size:21px}} h2 small{{color:var(--mut);font-weight:500;font-size:14px}}
.stats{{display:grid;grid-template-columns:repeat(auto-fit,minmax(170px,1fr));gap:10px;margin:14px 0}}
.stat{{background:var(--card);border:1px solid var(--line);border-radius:10px;padding:12px}} .stat b{{font-size:24px;display:block}} .stat span{{color:var(--mut);font-size:13px}}
article{{background:var(--card);border:1px solid var(--line);border-radius:12px;padding:14px;margin:12px 0}} article.clean{{opacity:.75}}
article header{{display:flex;flex-wrap:wrap;gap:6px 10px;align-items:baseline}} h3{{margin:0;font-size:16px}} .meta{{color:var(--mut);font-size:13px}}
.b{{font-size:12px;padding:2px 8px;border-radius:99px;color:#fff}} .b-cloth{{background:var(--c1)}} .b-into{{background:var(--c2)}} .b-grip{{background:var(--c3)}} .b-hand{{background:var(--c4)}} .b-other{{background:#777}}
ul{{margin:8px 0 10px 18px;padding:0}} li.ok{{color:var(--mut)}} .thumbs{{display:flex;flex-wrap:wrap;gap:8px}}
figure{{margin:0}} figure img{{display:block;height:220px;width:auto;max-width:100%;border-radius:6px;border:1px solid var(--line)}} figcaption{{font-size:12px;color:var(--mut)}}
.patterns{{background:var(--card);border:1px solid var(--line);border-radius:12px;padding:6px 16px 10px}} .patterns li{{margin:6px 0}} .pages a{{color:var(--c4);margin-right:6px}}
.pages{{font-size:12px;color:var(--mut);margin:8px 0 0;overflow-wrap:anywhere}} code{{font-size:12px}}
</style></head><body><main>
<h1>Animation deformity breakdown</h1>
<p class="lead">Every tennis, golf and plaza clip, both heroes, sampled about 6 times a second from front, back, left and right, with close-ups of both hands. Red circles mark what is wrong. {nclips} clips reviewed, {totals["circles"]} circles in total.</p>
<div class="stats">{stats}</div>
<section class="patterns"><h2>What keeps going wrong <small>the targets for the standard</small></h2><ol>
<li><b>Shirts tear whenever the arms go up or cross the body.</b> Male tennis polo: holes at the chest, armpit, shoulder blade and side seam (serve, smash, every forehand, Jump for Joy, Victory, Shock, Thrust, push-ups). Female tennis polo: the sleeve opens at the armpit in almost every swing. Golf shirts: the hem rides up so a hole or skin shows above the belt, and the sleeve stretches into streaks with the arms overhead.</li>
<li><b>Hands go into the body and clothes.</b> Any time the hands are low they sink into the shorts, skirt or thighs: Slump hides both hands completely, Stomp and Defeat bury the fists or fingers, and the plaza walk and run swing the hands through the hips on every step. Two-handed grips overlap (backhand, every golf swing). Fingers go into the skull (Shock), and the racket handle or arm passes through the face at the end of the female forehands and return.</li>
<li><b>Empty hands still hold a racket.</b> Every empty-handed emote (plaza and golf) starts and ends in the tennis grip shape, holding nothing. The old golf emotes (Spike, Scuba, Thrust, intros) start and end in a two-hand club grip with no club.</li>
<li><b>Hand shapes look wrong.</b> In tennis swings the free hand is a stiff claw or a flat splayed paddle. Fists have the thumb sticking straight up, and the bare right hand folds wrong. The plaza walk uses pinched OK-sign hands. The golf idle, walk and old celebration clips hang stiff, flat, open hands. The serve toss hand is a flat palm instead of cupping the ball. Racket fingers lie open on the handle in the side runs and some finishes.</li>
<li><b>Trousers and skirts split.</b> The male golf trousers have a see-through slit at the crotch and tear holes in the seat when bending (old golf emotes, push-ups, the old fist pump). Skirts split open in squats and knee lifts. The female golf glove cuff shreds into spikes on the putt and chip.</li>
</ol><p class="lead">Clean, nothing circled: Ready (both heroes) and tennis Idle (both). Male golf Idle has no cloth problems, only stiff open hands.</p></section>
<p class="lead">Numbers above count clips that show each kind of problem. The thumbnails show the worst frames; every frame of every clip is in the full pages listed under each clip.</p>
<nav>{nav}</nav>
{"".join(parts)}
</main></body></html>'''
open(os.path.join(out, 'index.html'), 'w').write(page)
print('clips', nclips, 'circles', totals['circles'], 'cats', dict(clip_cats), 'imgs', len(os.listdir(os.path.join(out, 'img'))))

# ---------------------------------------------------------------- the publishable page: one file, thumbnails embedded
import base64
def b64(name): return base64.b64encode(open(os.path.join(out, 'img', name), 'rb').read()).decode()
ctx_short = {'tennis_male': 'Tennis ♂', 'tennis_female': 'Tennis ♀', 'golf_male': 'Golf ♂', 'golf_female': 'Golf ♀'}
arts = []
for ctx, title, rows in sections:
    arts.append(f'<h2 class="ctxh" data-ctx="{ctx}" id="{ctx}">{html.escape(title)} <span>{len(rows)} clips · {sum(1 for r in rows if r["n"])} with problems</span></h2>')
    for r in rows:
        nm = r['clip'].split(':', 1)
        kind = 'Plaza / golf, empty hands' if nm[0] == 'free' else ('Golf' if ctx.startswith('golf') and nm[0] == 'golf' else nm[0])
        name = nm[1] if len(nm) > 1 else nm[0]
        summ = ''.join(f'<li>{html.escape(s)}</li>' for s in r['summary']) or '<li class="ok">Nothing wrong found.</li>'
        th = ''.join(f'<figure><button type="button" class="shot" aria-label="Enlarge frame {k} {img}"><img loading="lazy" src="data:image/jpeg;base64,{b64(t)}" alt="{html.escape(name)} frame {k}, {img}"></button><figcaption>f{k} · {img.replace("hand", "hand ")}</figcaption></figure>' for t, k, img in r['thumbs'])
        npg = len(r['pages'])
        path = f'work/anim-standard/breakdown/pages/final/{ctx}/{r["dir"]}_p01' + (f'…p{npg:02d}' if npg > 1 else '') + '.jpg'
        arts.append(f'<article data-ctx="{ctx}" data-cats="{" ".join(r["cats"])}" data-clean="{0 if r["n"] else 1}">'
                    f'<header><h3>{html.escape(name)}</h3><span class="kind">{html.escape(kind)}</span></header>'
                    f'<p class="meta">{ctx_short[ctx]} · {r["frames"]} frames · {r["length"]} s · {r["bad"]} of {r["frames"]} frames circled</p>'
                    f'<div class="badges">{"".join(badge(k) for k in r["cats"])}</div><ul>{summ}</ul>'
                    + (f'<div class="strip">{th}</div>' if th else '') +
                    f'<p class="path">All {r["frames"]} frames, 4 views + hands: <span>{html.escape(path)}</span></p></article>')
chips = ''.join(f'<button type="button" class="chip" data-cat="{k}" aria-pressed="false"><i class="dot d-{k}"></i>{html.escape(t)} <b>{clip_cats.get(k, 0)}</b></button>' for k, t, _ in CATS)
tabs = '<button type="button" class="tab" data-ctx="all" aria-pressed="true">All</button>' + ''.join(f'<button type="button" class="tab" data-ctx="{c}" aria-pressed="false">{html.escape(t)}</button>' for c, t, _ in sections)
patterns = page[page.index('<section class="patterns">'):page.index('</section>', page.index('<section class="patterns">')) + 10]
patterns = patterns.replace('<small>the targets for the standard</small>', '')
art = f'''<title>Animation Deformity Breakdown</title>
<link rel="preconnect" href="https://fonts.googleapis.com"><link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
<link rel="stylesheet" href="https://fonts.googleapis.com/css2?family=IBM+Plex+Mono:wght@400;500&family=IBM+Plex+Sans+Condensed:wght@600;700&family=IBM+Plex+Sans:wght@400;500;600&display=swap">
<style>
/* layout: a QA review sheet. Findings first, then a filter bar, then one card per clip with its worst circled frames */
:root{{--bg:#eceff3;--fg:#151a21;--mut:#5a6372;--card:#ffffff;--line:#d5dae2;--mark:#d4272b;--c-cloth:#b5294e;--c-into:#6a3fc2;--c-grip:#a35d06;--c-hand:#1d6aad;--c-other:#6b7280;--chip:#e2e6ec;
--display:"IBM Plex Sans Condensed","Arial Narrow",system-ui,sans-serif;--body:"IBM Plex Sans",system-ui,-apple-system,"Segoe UI",sans-serif;--mono:"IBM Plex Mono",ui-monospace,Menlo,monospace}}
@media (prefers-color-scheme: dark){{:root:not([data-theme="light"]){{--bg:#111419;--fg:#e6e9ee;--mut:#9ba3b0;--card:#1a1e25;--line:#2c323c;--mark:#ff5a5a;--c-cloth:#d04a6c;--c-into:#8c66e0;--c-grip:#c17a1c;--c-hand:#3a86cc;--c-other:#7d8592;--chip:#232831;color-scheme:dark}}}}
:root[data-theme="dark"]{{--bg:#111419;--fg:#e6e9ee;--mut:#9ba3b0;--card:#1a1e25;--line:#2c323c;--mark:#ff5a5a;--c-cloth:#d04a6c;--c-into:#8c66e0;--c-grip:#c17a1c;--c-hand:#3a86cc;--c-other:#7d8592;--chip:#232831;color-scheme:dark}}
body{{background:var(--bg);color:var(--fg);font:15px/1.5 var(--body)}}
.wrap{{max-width:1120px;margin:0 auto;padding-inline:16px;padding-block:28px 72px;display:flex;flex-direction:column;gap:18px}}
h1{{font:700 clamp(28px,4.5vw,40px)/1.05 var(--display);margin:0;text-wrap:balance;letter-spacing:-.01em}}
h1 em{{font-style:normal;color:var(--mark)}}
.lead{{color:var(--mut);margin:0;max-width:68ch}}
.counts{{display:flex;flex-wrap:wrap;gap:8px 22px;font:500 13px var(--mono);color:var(--mut)}} .counts b{{color:var(--fg);font-size:15px}}
.patterns{{background:var(--card);border:1px solid var(--line);border-radius:10px;padding-inline:20px;padding-block:6px 14px}}
.patterns h2{{font:700 20px var(--display);margin:12px 0 4px}} .patterns ol{{margin:0;padding-left:20px;display:flex;flex-direction:column;gap:8px;max-width:80ch}} .patterns .lead{{margin-top:10px;font-size:14px}}
.bar{{position:sticky;top:env(safe-area-inset-top,0px);z-index:5;background:var(--bg);padding-block:10px;border-bottom:1px solid var(--line);display:flex;flex-direction:column;gap:8px}}
.row{{display:flex;flex-wrap:wrap;gap:6px;align-items:center}}
button{{font:inherit;color:inherit;cursor:pointer}}
.tab,.chip,.toggle{{border:1px solid var(--line);background:var(--chip);border-radius:999px;padding:4px 12px;font-size:13px;font-weight:500}}
.tab[aria-pressed="true"]{{background:var(--fg);color:var(--bg);border-color:var(--fg)}}
.chip[aria-pressed="true"],.toggle[aria-pressed="true"]{{border-color:var(--fg);box-shadow:inset 0 0 0 1px var(--fg)}}
.chip b{{font:500 12px var(--mono);color:var(--mut);margin-left:2px}}
.dot{{display:inline-block;width:9px;height:9px;border-radius:50%;margin-right:6px;vertical-align:0}}
.d-cloth{{background:var(--c-cloth)}} .d-into{{background:var(--c-into)}} .d-grip{{background:var(--c-grip)}} .d-hand{{background:var(--c-hand)}} .d-other{{background:var(--c-other)}}
button:focus-visible{{outline:2px solid var(--mark);outline-offset:2px}}
.shown{{font:12px var(--mono);color:var(--mut);margin-left:auto}}
.list{{display:flex;flex-direction:column;gap:12px}}
.ctxh{{font:700 22px var(--display);margin:22px 0 0}} .ctxh span{{font:500 13px var(--mono);color:var(--mut);margin-left:8px}}
article{{background:var(--card);border:1px solid var(--line);border-radius:10px;padding:14px 16px;display:flex;flex-direction:column;gap:8px;min-width:0}}
article header{{display:flex;flex-wrap:wrap;gap:4px 12px;align-items:baseline}}
h3{{font:600 17px var(--body);margin:0}} .kind{{font:500 12px var(--mono);color:var(--mut);text-transform:uppercase;letter-spacing:.06em}}
.meta{{margin:0;font:12px var(--mono);color:var(--mut);font-variant-numeric:tabular-nums}}
.badges{{display:flex;flex-wrap:wrap;gap:6px}}
.b{{font-size:12px;font-weight:500;padding:1px 9px;border-radius:999px;color:#fff}}
.b-cloth{{background:var(--c-cloth)}} .b-into{{background:var(--c-into)}} .b-grip{{background:var(--c-grip)}} .b-hand{{background:var(--c-hand)}} .b-other{{background:var(--c-other)}}
article ul{{margin:0;padding-left:18px;display:flex;flex-direction:column;gap:3px;max-width:90ch}} li.ok{{color:var(--mut);list-style:none;margin-left:-18px}}
.strip{{display:flex;gap:10px;overflow-x:auto;padding-bottom:4px}}
figure{{margin:0;flex:none}} .shot{{display:block;padding:0;border:1px solid var(--line);border-radius:6px;overflow:hidden;background:#4f5866}}
.shot img{{display:block;height:230px;width:auto;max-width:none}}
figcaption{{font:11px var(--mono);color:var(--mut);margin-top:3px}}
.path{{margin:0;font-size:12px;color:var(--mut);overflow-wrap:anywhere}} .path span{{font-family:var(--mono)}}
dialog{{border:0;padding:0;background:transparent;max-width:96vw;max-height:94vh}} dialog::backdrop{{background:rgba(8,10,14,.82)}}
dialog img{{display:block;max-width:96vw;max-height:94vh;width:auto;height:auto;border-radius:6px}}
@media (max-width:520px){{.shot img{{height:180px}} .shown{{margin-left:0;width:100%}}}}
@media (prefers-reduced-motion:no-preference){{article{{transition:opacity .15s}}}}
</style>
<div class="wrap">
<h1>Animation deformity breakdown: <em>{totals["circles"]:,} circles</em> across {nclips} clips</h1>
<p class="lead">Every tennis, golf and plaza clip on both heroes, sampled about six times a second from the front, back, left and right, plus a close-up of each hand. Red circles mark the problem. The strip under each clip shows its worst frames; tap one to enlarge. The full frame-by-frame pages are in the project folder.</p>
<div class="counts"><span><b>{sum(len(r) for _, _, r in sections)}</b> clips</span><span><b>{sum(1 for _, _, rows in sections for r in rows if r["n"])}</b> with problems</span><span><b>{sum(r["frames"] for _, _, rows in sections for r in rows)}</b> frames × 6 images</span></div>
{patterns}
<div class="bar" role="toolbar" aria-label="Filter clips">
<div class="row">{tabs}</div>
<div class="row">{chips}<button type="button" class="toggle" id="hideclean" aria-pressed="false">Hide clean clips</button><span class="shown" id="shown"></span></div>
</div>
<div class="list">{"".join(arts)}</div>
</div>
<dialog id="zoom"><img alt=""></dialog>
<script>
(function(){{
  var ctx='all', cats=new Set(), hideClean=false;
  var arts=[].slice.call(document.querySelectorAll('article')), heads=[].slice.call(document.querySelectorAll('.ctxh'));
  function apply(){{
    var n=0;
    arts.forEach(function(a){{
      var ok=(ctx==='all'||a.dataset.ctx===ctx);
      if(ok&&cats.size){{var cs=a.dataset.cats.split(' ');ok=[].some.call(cs,function(c){{return cats.has(c);}});}}
      if(ok&&hideClean&&a.dataset.clean==='1')ok=false;
      a.hidden=!ok; if(ok)n++;
    }});
    heads.forEach(function(h){{h.hidden=!(ctx==='all'||h.dataset.ctx===ctx);}});
    document.getElementById('shown').textContent=n+' of '+arts.length+' clips shown';
  }}
  document.querySelectorAll('.tab').forEach(function(b){{b.addEventListener('click',function(){{
    ctx=b.dataset.ctx; document.querySelectorAll('.tab').forEach(function(t){{t.setAttribute('aria-pressed',t===b?'true':'false');}}); apply();}});}});
  document.querySelectorAll('.chip').forEach(function(b){{b.addEventListener('click',function(){{
    var c=b.dataset.cat; if(cats.has(c))cats.delete(c); else cats.add(c); b.setAttribute('aria-pressed',cats.has(c)?'true':'false'); apply();}});}});
  var hc=document.getElementById('hideclean'); hc.addEventListener('click',function(){{hideClean=!hideClean; hc.setAttribute('aria-pressed',hideClean?'true':'false'); apply();}});
  var dlg=document.getElementById('zoom'), big=dlg.querySelector('img');
  document.querySelectorAll('.shot').forEach(function(s){{s.addEventListener('click',function(){{var i=s.querySelector('img'); big.src=i.src; big.alt=i.alt; if(dlg.showModal)dlg.showModal();}});}});
  dlg.addEventListener('click',function(){{dlg.close();}});
  apply();
}})();
</script>
'''
open(os.path.join(out, 'artifact.html'), 'w').write(art)
print('artifact', round(len(art) / 1e6, 2), 'MB')
