"""build_doc.py <audit_dir> <raw_cu_dir> <facesheets_dir> <face_raw_dir>: close-up stills + CLIPPING_AUDIT.md"""
import sys, os, json, shutil
from collections import Counter, defaultdict
sys.argv = [sys.argv[0]] + sys.argv[1:3] + sys.argv[3:]
import assemble as A
from PIL import Image

AUD, RAW = sys.argv[1], sys.argv[2]; FS, FR = sys.argv[3], sys.argv[4]
rows, cov = A.main()
eps = json.load(open(os.path.join(AUD, 'episodes.json')))
cvd = json.load(open(os.path.join(AUD, 'coverage.json')))
SEV = A.SEV; IMG = A.IMG

# face stills
os.makedirs(IMG, exist_ok=True)
for f in os.listdir(FS): shutil.copy(os.path.join(FS, f), os.path.join(IMG, f))
def face_crop(ctx, names, out):
    P = Image.new('RGB', (384 * len(names), 384))
    for i, n in enumerate(names): P.paste(Image.open(os.path.join(FR, f'face_{ctx}', n + '.jpg')), (i * 384, 0))
    P.save(os.path.join(IMG, out), quality=88)
face_crop('tennis_male', ['neutral_front', 'neutral_l45', 'neutral_side'], 'face_male_tennis_eye_specks.jpg')
face_crop('golf_male', ['neutral_front', 'neutral_l45', 'neutral_side'], 'face_male_golf_eye_specks.jpg')
face_crop('tennis_female', ['angry_blink_front', 'angry_blink_l45', 'angry_blink_side'], 'face_female_closed_eye_smear.jpg')

# summary matrix: emote x (hero,kit)
cells = defaultdict(lambda: [0, 0, 0])
for r in rows:
    e = r['e']; cells[(e['emote'], e['hero'], e['kit'])][r['sev']] += 1
emotes = []
for c in cvd['coverage']:
    n = c['emote']
    if n not in emotes: emotes.append(n)
order = ['JumpJoy', 'FistPump', 'Stomp', 'Slump', 'Shock', 'Victory', 'Defeat', 'Scuba', 'Spike', 'Thrust', 'Walk', 'Run', 'Run forward', 'Run left', 'Run right']
emotes = [e for e in order if e in emotes] + [e for e in emotes if e not in order]
old = {'Scuba', 'Spike', 'Thrust'}
cols = [('male', 'tennis'), ('female', 'tennis'), ('male', 'golf'), ('female', 'golf')]
M = ['| Emote | male / tennis kit | female / tennis kit | male / golf kit | female / golf kit |', '|---|---|---|---|---|']
have = {(c['emote'], 'male' if 'female' not in c['ctx'] else 'female', 'tennis' if c['ctx'].startswith('tennis') else 'golf') for c in cvd['coverage']}
for en in emotes:
    cs = []
    for h, k in cols:
        if (en, h, k) not in have: cs.append('-'); continue
        v = cells[(en, h, k)]
        cs.append(f"**{v[2]}** very / {v[1]} noticeable / {v[0]} small" if sum(v) else 'nothing found')
    M.append(f"| {en}{' (old taunt)' if en in old else ''} | " + ' | '.join(cs) + ' |')
tot = Counter(SEV[r['sev']] for r in rows)
byclass = Counter()
for r in rows:
    e = r['e']; k = e['kind']; w = e['what']
    cls = 'hands / fingers into body, head or clothes' if w.startswith(('hand in', 'hand under', 'hands')) else 'racket into body / clothes' if w.startswith('racket') else 'skin or cloth lining showing through the shirt / shorts (tears)' if k.startswith('cloth') else 'shirt and shorts cutting through each other' if 'intersects' in w else 'feet sinking into the ground' if k == 'feet-sink' else 'feet sliding on the ground'
    byclass[(cls, SEV[r['sev']])] += 1
classes = ['hands / fingers into body, head or clothes', 'racket into body / clothes', 'skin or cloth lining showing through the shirt / shorts (tears)', 'shirt and shorts cutting through each other', 'feet sinking into the ground', 'feet sliding on the ground']
C = ['| Kind of problem | very obvious | noticeable | small |', '|---|--:|--:|--:|'] + [f"| {c} | {byclass[(c, 'very obvious')]} | {byclass[(c, 'noticeable')]} | {byclass[(c, 'small')]} |" for c in classes]

head = []
head.append("**Status: audit only. Nothing was fixed, installed or committed.** Everything is staged under `work/emote-standard/` (this file, `CLIPPING_AUDIT_images/`, `CLIPPING_AUDIT.json`, tools and raw data in `audit/`).\n")
head.append(f"## Short version\n\nThe emote set is not clean. **{len(rows)} separate problems** across {len(cvd['coverage'])} clip runs: **{tot['very obvious']} very obvious, {tot['noticeable']} noticeable, {tot['small']} small**. The common ones:\n")
head.append("* **Hands go into the body.** Fingers and wrists sink into the head (Shock), fists press into the thigh (Stomp, JumpJoy), in Slump and Defeat one or both hands vanish inside the shorts or skirt, and in the empty-hand Walk and Run the swinging hands go into the hips and thighs on every stride (the biggest single group).\n* **The shirt tears.** In every emote that lifts the arms or folds the trunk (Scuba, Spike, Thrust, JumpJoy, Victory, Shock), the polo splits at the armpit, shoulder blade, side and back, with skin showing through; golf shirts also tear above the belt.\n* **Shorts and skirt tear and cut through the polo** when the trunk bends (Scuba worst: the seat and hem shred).\n* **The racket goes into the leg, hip or skirt** in the low-hands emotes (Slump, Defeat, Stomp, JumpJoy, FistPump) and the side runs.\n* **Feet:** shoes dip below the rest ground level on landings and heel-strikes (JumpJoy, Victory, Thrust, Walk, Run; worst ~17 mm), and a few emotes slide a planted foot 15-40 mm (Victory in the golf kit worst, then FistPump). Walk and Run do not skate: the stance-foot speeds match the speeds the game plays them at. Nothing sinks by more than 20 mm.\n* **Eyes:** the male hero has small white fragments sticking out of the eyes from 3/4 and side views, in every mood. Female eyes are clean. Details at the bottom.\n")
head.append('\n### Count by kind of problem\n')
head.extend(C)
head.append('\n### Count by emote, hero and kit\n')
head.extend(M)
head.append("""
## How this was checked

* **Clips:** the 7 new emotes (JumpJoy, FistPump, Stomp, Slump, Shock, Victory, Defeat), the 3 old taunts (Scuba, Spike, Thrust), Walk and Run, for the male and female hero.
  * *Tennis kit* (polo, shorts or skirt, socks, shoes): the racket clips the tennis game plays, plus the empty-hand versions the plaza uses.
  * *Golf kit* (polo, trousers or skirt, glove, shoes): the empty-hand clips golf plays, plus the golfer's own Scuba / Spike / Thrust / Walk / Run.
* **Poses:** every clip is played in Unity with all garment components running (arm skinning, grip, pose correctives, polo/KitV2 correctives), sampled at 15 fps and rendered from the front, back, left and right, with an ID pass so torn cloth and skin can be detected. Hands, racket and garments are measured on the posed meshes every frame.
* **Feet:** shoe geometry sampled at 30 fps; ground = the rest-pose sole level.
* **Face:** a sweep of the face rig (4 moods, half and full blink, 4 gaze extremes, 4 camera angles) per hero and kit, because the face mesh does not depend on the body clip.
* **Checked by eye:** the worst rows of every kind were opened and looked at. The detectors had false positives that were removed after looking: background seen between the legs (golf trousers), sock-over-shoe and polo-under-belt overlaps (nothing visible), the inside of the male shorts leg opening. The remaining small rows are only machine-graded.
* **Grades:** cloth tears = size of the visible patch in a full-body render (small < 40 px, very obvious > 120 px for lining, 80 px for skin); hands and racket = how many hand / racket mesh points are inside the body or clothes (small < 60, very obvious > 250 for hands; 40 / 150 for the racket); feet = mm below the rest sole level (small < 8, very obvious > 20) or mm slid while planted (small < 30, very obvious > 80). I looked at the stills of the top rows and moved nothing by hand except where noted in a row.

## Limits (what this does not show)

* Nothing in the actual game scene was run: the tennis driver's own foot-lock and ground lift (it raises the hips when a sole is below rest) are not in these numbers. Golf and the plaza have no such lift. The rest soles themselves sit 31 mm under the root origin in this scene, so "sink" is measured against the rest pose, not the court.
* 15 fps sampling can miss a glitch that lives for a single 1/30 s frame. Feet are 30 fps.
* Sock-through-shoe and the golf polo/belt overlap were flagged by the detector but nothing was visible in the stills, so they are not listed.
* Small fingers/thumb shapes (the thumb-up fist, the claw hands) are not in your list and not repeated here; they are in `work/anim-standard/verdicts.json` from the earlier breakdown.
""")
tail = []
tail.append('\n## Eyes and face\n')
tail.append("The face sweep covers neutral, happy, sad, angry and shock, half and full blink, gaze left / right / up / down, from front, 3/4 left, 3/4 right and side. Full sheets: `face_<hero>_<kit>_moods.jpg` and `_gaze.jpg` in `CLIPPING_AUDIT_images/`.\n")
tail.append('| # | Hero | Kit | Where | What is wrong | How bad | Still |\n|--:|---|---|---|---|---|---|')
tail.append("| F1 | male | tennis | both eyes, seen from 3/4 or the side, every mood and every clip | Small white fragments (broken eye-white shards) stick out of the eye socket, outside the eyelids. Obvious in side view. | **noticeable** up close (locker, emote close-ups), small at match distance | ![](CLIPPING_AUDIT_images/face_male_tennis_eye_specks.jpg) |")
tail.append("| F2 | male | golf | same | Same fragments, and a ragged white edge at the eye corners even from the front. | **noticeable** up close, small at golf camera distance | ![](CLIPPING_AUDIT_images/face_male_golf_eye_specks.jpg) |")
tail.append("| F3 | female | tennis + golf | closed eye (full blink, angry squint) in 3/4 view | A dark smear of the lash line stretches across the closed eye. Open eyes, brows, gaze extremes: clean. | **small** | ![](CLIPPING_AUDIT_images/face_female_closed_eye_smear.jpg) |")
tail.append("\nNot found: eyelids through the eyeballs, brows through the skull, pupils leaving the eye at any gaze extreme, or any face glitch tied to a specific clip. Also worth knowing, not a glitch: the happy, sad and shock moods differ from neutral only by a small brow shift, so the face barely reads as different between emotes (angry squint is the only clear one).\n")
tail.append('## Feet and locomotion\n')
tail.append('Walk and Run, stance-foot speed measured from the baked clips against the speed the game plays the clip at:\n')
tail.append('| Clip | Hero / kit | Measured stance speed | Game speed | Result |\n|---|---|--:|--:|---|')
for l in cvd['loco']:
    golfer = l['variant'] == 'golfer clip'
    exp = (1.2 if l['emote'] == 'Walk' else 3.5) if l['variant'].startswith('free') and l['ctx'].startswith('golf') or l['variant'].startswith('free') and l['ctx'].startswith('tennis') else (1.25 if l['emote'] == 'Walk' else 3.25)
    if golfer: tail.append(f"| {l['emote']} ({l['variant']}) | {l['ctx'].replace('_', ' / ')} | {l['median']:.2f} m/s | no game speed yet (golf flow does not move the golfer) | steady, no skating inside the clip |"); continue
    dev = (l['median'] - exp) / exp * 100
    tail.append(f"| {l['emote']} ({l['variant']}) | {l['ctx'].replace('_', ' / ')} | {l['median']:.2f} m/s | {exp:.2f} m/s | {'no skating' if abs(dev) < 8 else f'{dev:+.0f} % off'} |")
tail.append('\n## Clips with nothing found\n')
seen = {(r['e']['ctx'], r['e']['emote']) for r in rows}
clean = sorted({f"{c['emote']} ({c['variant']}, {c['ctx'].replace('_', ' / ')})" for c in cvd['coverage'] if (c['ctx'], c['emote']) not in seen})
tail.append(', '.join(clean) if clean else 'None: every clip has at least one problem.')
A.write_md(rows, cov, eps, dict(head=head, tail=tail))
print(len(rows), tot)
