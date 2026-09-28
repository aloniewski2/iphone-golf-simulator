# PHASE: AnimationFluidity_PartySports (Plan 3A — motion quality)

Paste into Claude Opus. One PHASE only. Stop when done — then Adnan reviews. Cosmetics (skins/sockets) are Plan 3B later; do not start cosmetics here.

---

## Project
`/Users/adnanyonathan/Documents/Codex/2026-09-20/wh/outputs/iphone-golf-simulator`
Read `AGENTS.md` + `art-bible.md` first (esp. animation law §7).

**Locks:** Hero POLISH V4 mesh stays. Do not regen body / Tripo / Mixamo proxy replace. Prefer clean + retarget + curve polish on existing `Hero_*` Eyes Japan–derived clips; add gap clips only where arcs are stubby. Plan 1 grounding / hit-sync truth stays. Plan 2 juice/cams stay (2C playability rules). Env (2D) is separate — don’t block on it.

---

## Why (Adnan — hard)

Play still feels **unfluid** — more advanced Roblox than fast party sports. Wii Sports (2006) had weighty fluid swings; Fall Guys / Switch Sports feel alive. Juice (VFX, speed lines, ultimates) is carrying moments the **body** should sell. Cinematic zooms expose locked torsos and dead faces.

**North star feel:** snappy anticipation → contact → overshoot → settle. Hips lead. Failures overshoot more than hits. Readable from the couch.

**Comps (steal motion law, not IP):** Wii Sports tennis, Nintendo Switch Sports, Fall Guys body bounce, *just volleyball* continuous over-animated juice. Not Roblox default humanoid.

---

## Audit (this clip)

- Serve: torso too vertical / rigid on jump serve
- Rally: foot-slide skate; dust masks plant; swings arm-local, weak kinetic chain
- Supercharged / ultimate closeups: stiff spine, blank face
- Emote pop: abrupt into victory, no lead-in
- Overall: linear timing, missing wind-up / overshoot / settle; VFX hides missing hit weight

---

## Do (priority)

### P0 — Core feel (must ship this phase)
1. **Kinetic chain on all swings (FH / BH / serve / volley / ultimate strike)**  
   Hips rotate first → torso → shoulder → arm → racket. Kill arm-only flicks with squared frozen hips. Unlock spine (no locked vertical torso on serve jump or ultimate spin).
2. **Full arcs** — anticipation → swing → contact → follow → recover. No stub / 2-frame twitch. Contact frame honest with ball (Plan 1 truth).
3. **Locomotion plants** — kill frictionless skate into swing. Dedicated plant/pivot / stop-into-swing blends; feet meet court; root/blend so dust is optional garnish not the only “contact.”
4. **Upper/lower blends** — run legs + swing arms without spine snap. Mask times tuned so torso doesn’t pop.

### P1 — Timing & life
5. **Curves** — slow-in wind-up, fast-out through contact, overshoot + settle back to ready. No linear robotic lerp feel.
6. **Hit weight** — brief 2–3 frame hit-stop on perfect / smash / ultimate contact (body + ball), then followthrough. Don’t fake contacts.
7. **Cinematic faces** — if cam cuts close (ultimate / rival cam), fire effort blendshapes (squint, grit, blink). Blank mannequin face = FAIL on closeup.
8. **Emote / point transitions** — short lead-in out of play pose into auto victory/fail emote; no hard pop.

### P2 — Appeal (if time)
9. Subtle squash/stretch on root/spine for jump serve + ultimate spin (toy bounce, not cartoon melt).
10. Idle / ready breathe so ready stance isn’t statue.

---

## Don’t
- Regen Hero V4 mesh or swap to Mixamo stock as final.
- Rebuild ultimate power / shop / cosmetics system.
- Rewrite Plan 2C camera UX (keep playable spectacle rules).
- Fake hits for spectacle; don’t add clutter props.
- Photoreal mocap stiffness — stylize toward party, not broadcast tennis.

---

## Done when
Short capture (same rally length as Adnan’s clip) shows:
1. Swings read hips-first with clear followthrough
2. Run → plant → swing without skate
3. Serve / ultimate closeups: living torso + face effort
4. Hit-stop readable on big hits; juice supports body, doesn’t replace it
5. Feel closer to Switch Sports / Fall Guys weight than Roblox default

Reply: paths changed, which clips retimed/replaced, remaining debt. **Stop for Adnan.** Next = Plan 3B cosmetics (or env 2D if still open) per Adnan.

