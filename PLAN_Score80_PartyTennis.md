# MASTER PLAN: Score 64 → 80+ (Party Tennis Feel)

Paste into Claude Opus as the **controlling brief**. Execute **one PHASE at a time** in order. Stop after each phase for Adnan approval + a short capture before starting the next. Do not skip ahead. Do not reopen locked work unless a later phase explicitly requires a surgical fix.

---

## Project
`/Users/adnanyonathan/Documents/Codex/2026-09-20/wh/outputs/iphone-golf-simulator`
Read first: `AGENTS.md`, `art-bible.md`, and any existing `PLAN3*_*.md` / `PLAN2*_*.md` in root (do not contradict locks).

**Product:** Friends-first iPhone party tennis (Wii Sports nostalgia + Switch Sports clarity + Fall Guys laugh-on-fail). Cosmetics = identity only (no P2W).

**Stack:** Unity URP · locked Hero POLISH V4 (proportions + Mixamo bind) · Eyes Japan–derived `Hero_*` clips · F2P cosmetics later.

**Current honest score vs comps (Switch Sports tennis, Wii Sports tennis, Fall Guys feel, Mario Tennis / just-volleyball presentation): ~64/100.**
Target this plan: **≥80 overall** with no category below **72**.

| Category (baseline) | Now | Target |
|---------------------|----:|-------:|
| Animation fluidity / body | 60 | ≥78 |
| Hit feel / sync / weight | 65 | ≥80 |
| Camera & spectacle (playable) | 68 | ≥78 |
| Character / art in frame | 62 | ≥76 |
| Juice / fun density | 65 | ≥80 |
| **Overall** | **64** | **≥80** |

---

## Locks (never casually reopen)

1. Hero V4 **body proportions** — no full Tripo regen. Surgical mesh/material/socket OK.
2. Plan 1 truths: feet on court, honest hit sync, no fake input hits, body clearance / serve stance fixes stay.
3. Plan 2C soft law: **spectacle only while ball frozen / dead; live ball = readable play cam.** Keep serve toss flip; no perfect-serve contact cut that hides trajectory; ultimate snaps to play cam at/before contact.
4. Ultimate = equal meter access, not shop-gated power.
5. Don’t start online / PVP shell / sport 2 in this plan.

---

## North star motion law (every anim phase)

Snappy **anticipation → contact → overshoot → settle**.  
**Hips lead** → torso → shoulder → arm → racket.  
Misses overshoot more than hits.  
Racket **face** to ball at contact; hands hold grip (no implant).  
Full arcs — no stub / 2-frame twitch.  
Comps to steal **feel** (not IP): Wii Sports tennis, Switch Sports tennis, Fall Guys body bounce, just volleyball continuous juice, Mario Tennis hit punctuation.

**Anti-goal:** advanced Roblox humanoid — arm flicks, locked spine, skate into swing, juice hiding empty body.

---

## Success criteria for the whole plan

After final phase, Adnan captures a ~15–20s rally (serve → multi-hit → big shot → point). Independent re-score should land **≥80** because:

1. Run → plant → swing reads weighty; no frictionless skate.
2. Swings are hips-first with clear followthrough; closeups show living torso (+ face effort on ultimates).
3. Perfect / Supercharged / Ultimate have unmistakable hit-stop + body reaction; juice supports body, doesn’t replace it.
4. Cams stay playable (2C rules) but mid-rally has subtle life.
5. Customize + gameplay: solid hair (no see-through gaps); court/env holds up in react/ultimate cams without looking unfinished.
6. Fun density: every 2–3 seconds a readable beat (quality callout, plant dust as garnish, impact punch) without spam.

---

# PHASE ORDER (execute in sequence)

---

## PHASE A — AnimationFluidity_Core (biggest score jump: ~60→78 body)

**Goal:** Kill Roblox stiffness. Body sells the sport.

### Do
1. **Audit** all play clips: idle/ready, run, stop, plant, FH, BH (1H + 2H if present), serve (toss+strike), volley, smash, whiff, ultimate strike, auto emotes. List stubby / arm-only / locked-spine offenders.
2. **Kinetic chain pass** on every swing + serve + ultimate strike: hips rotate first; unlock spine; clavicle/chest follow arm (no T-pose shoulder residue).
3. **Full arcs**: anticipation → swing → contact → follow → recover. Contact frame stays honest with ball (Plan 1).
4. **Locomotion plants**: dedicated plant/pivot and stop-into-swing blends; root/blend so feet meet court; dust is optional garnish not the only contact cue.
5. **Upper/lower body masks**: run legs + swing arms without spine snap; tune blend times.
6. **Curves**: slow-in wind-up, fast-out through contact, overshoot + settle. No linear robotic lerp feel.
7. **Emote lead-ins**: short blend out of play pose into victory/fail — no hard pop.
8. Prefer clean/retime/polish existing `Hero_*` Eyes Japan clips; author gap clips only where arcs are stubby. **Do not** replace with Mixamo stock as final.

### Don’t
Regen hero · rewrite cameras · rebuild ultimate power · env art · cosmetics shop.

### Done when
Capture shows hips-first swings, plant-into-swing (no skate), living torso on serve/ultimate, blends without pops. Reply: clips touched, blend tree changes, remaining anim debt. **STOP for Adnan.**

---

## PHASE B — HitWeight_AndJuice (hit feel ~65→80, juice ~65→80)

**Goal:** Physical punctuation so Perfect/Supercharged/Ultimate feel heavy without fake contacts.

### Do
1. **Hit-stop** 2–3 frames on Perfect / Smash / Supercharged / Ultimate (body + ball + racket), then followthrough. Scale lightly for Great; none/minimal for normal.
2. **Body reaction** on heavy contact: short recoil / squash on spine or shoulders — readable, not ragdoll.
3. **Impact VFX density** one notch up on heavy hits only (sparks/shockwave/trail burst) — still readable; don’t wallpaper the screen every touch.
4. **Screen shake** subtle, intensity by quality tier; never during precision aim windows if it breaks timing readability.
5. Sync SFX attack with contact frame (if SFX hooks exist); no desync trails that lead the body.
6. Whiff / net / fail get **comic** overshoot (Fall Guys energy) — failure more juicy than routine.

### Don’t
Fake hits for spectacle · break 2C camera timing · change ball physics into unfair AI.

### Done when
Same rally: heavy hits have unmistakable punch; normal hits stay clean; juice supports body. **STOP for Adnan.**

---

## PHASE C — CameraLife_Playable (camera ~68→78)

**Goal:** Mid-rally life without hurting return readability.

### Do
1. Reaffirm **2C soft law** in code/timeline: live ball → play cam; frozen charge / dead ball → spectacle OK.
2. Keep serve **toss flip**; no contact cinematic that hides serve trajectory.
3. Ultimate: charge cinematic OK; **snap to play/receive cam at or before contact** (player + rival).
4. Add **subtle mid-rally camera drift / breath** (small positional noise or soft follow lag) — Switch Sports–light, not film handheld chaos.
5. Optional: tiny FOV pulse on Perfect contact only (≤1–2°), settle fast.
6. Reaction cams / after-point stay in dead time only.

### Don’t
Rival POV during inbound flight · lingering release cams · new cutspam mid-rally.

### Done when
Capture: toss flip cool; serve trajectory fully readable; after ultimates player can return; mid-rally feels alive not bolted. **STOP for Adnan.**

---

## PHASE D — HeroV4_HairAndCustomizeSafe (art character ~62→76 contribution)

**Goal:** Solid hero in customize + closeups; wardrobe swaps safe.

### Do
1. Fix hair **horizontal gap / transparency**: reproduce in customize UI; weld/rebuild lock volumes OR fix cull/normals OR move solid locks to opaque/alpha-clip (verify root cause).
2. Verify gameplay + customize + ultimate closeup: **no background through hair**.
3. Customize-safe slots: Hair · Headwear · Top · Bottom · Racket (± shoes). Swap A↔B: no poke-through, no bald hole without scalp/cap, racket sockets + 2HBH still valid.
4. Skinning near ears/nape so run/swing doesn’t reopen gaps.
5. Accidental shirt dither/shader noise → clean matte URP Lit per art-bible.
6. Micro armpit/shoulder pinch only if cheap — **no proportion reopen**.

### Don’t
Full body regen · Mixamo proxy final · P2W cosmetics stats · shop economy.

### Done when
Close-up solid hair; slot swaps clean; note root cause + assets. **STOP for Adnan.**

---

## PHASE E — EnvironmentPostcard_Quality (art env → pull overall over 80)

**Goal:** Court holds up when cams go wide (react, ultimate, serve).

### Do
1. Audit meshes visible in gameplay / serve / ultimate / react / any flyover.
2. Replace left-side **blob / untextured trees** with styled toy foliage (art-bible; world slightly quieter than characters).
3. Beach house / postcard landmark to hero quality bar — one landmark, not a city.
4. Court skirt / ground / sky / water: kill missing mats, seams, z-fight.
5. Lighting: warm key, soft shadows, fill; bloom low; characters still pop.
6. Cheap prop polish only where cams look (fence, umbrella, path) — big shapes, few pieces.
7. Mobile: LODs, shared mats, no pink/magenta, playable bounds unchanged (visual only).

### Don’t
New sports / open island traversal · block on cosmetics · photoreal 8K bark.

### Done when
Same bad angles as before look finished; gameplay cam uncluttered. **STOP for Adnan.**

---

## PHASE F — IntegrationPass_ScoreGate (prove ≥80)

**Goal:** No new systems — only fix regressions from A–E and ship the proof clip.

### Do
1. Play 5 rallies: serve perfect + miss, lateral plant FH/BH, Supercharged, Ultimate both sides, whiff, net cord if possible.
2. Checklist gate (all must pass):
   - [ ] No skate into swing
   - [ ] Hips-first readable on FH/BH/serve
   - [ ] Heavy hit-stop felt on Perfect/Ultimate
   - [ ] Live-ball never stuck on cinematic cam
   - [ ] Hair solid in customize + closeup
   - [ ] Wide cams: trees/house not blob/unfinished
   - [ ] No fake contacts; Plan 1 sync still true
   - [ ] 60fps feel on target iPhone path (no new hitch from VFX)
3. Export one **hero capture** (~15–20s) + short notes: remaining debt only.
4. Self-score each category; if any category **<72** or overall **<80**, list the single smallest fix and do **one** hotfix loop — then stop again for Adnan.

### Don’t
Start Plan cosmetics catalog, online, or sport 2.

### Done when
Adnan has hero capture + checklist. Plan complete pending his re-score.

---

## How to run each phase (Claude operating rules)

1. Read locks + art-bible animation law before touching assets.
2. Prefer surgical edits; log every path changed.
3. After implementation: **record proof clip** matching Adnan’s test shape (serve + rally + big moment).
4. Reply format every stop:
   - What changed (bullets + paths)
   - Proof clip notes
   - Risks / debt
   - **STOP — awaiting Adnan**
5. If blocked (missing mocap, broken import): say exactly what’s missing; don’t invent placeholder anims that reintroduce Roblox feel.
6. Never “improve” by adding more UI spam instead of body mechanics.

---

## Suggested score path (approximate)

| After phase | Expected overall |
|-------------|-----------------:|
| A Fluidity | ~70–72 |
| B Hit weight | ~74–76 |
| C Camera life | ~76–77 |
| D Hero hair/customize | ~77–78 |
| E Env postcard | ~79–81 |
| F Gate | **≥80** confirmed |

If A under-delivers (body still <72), **do not proceed to E** — re-run a tight A2 (spine + plant only) first. Motion is the ceiling.

---

## Out of scope until after 80

- Full cosmetics catalog / economy (3B shop content)
- Custom emote set beyond auto placeholders
- Online Lobby+Relay
- Sport 2
- Hero proportion redesign

---

**Start now with PHASE A only.** When PHASE A is done, stop for Adnan.
