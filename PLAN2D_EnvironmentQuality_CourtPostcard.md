# PHASE: EnvironmentQuality_CourtPostcard (Plan 2D)

Paste into Claude Opus. One PHASE only. Stop when done — then Adnan reviews. Do not start Plan 3 (anims/cosmetics) in this phase.

---

## Project
`/Users/adnanyonathan/Documents/Codex/2026-09-20/wh/outputs/iphone-golf-simulator`
Read `AGENTS.md` + `art-bible.md` first.

**Locks (do not reopen):** Hero POLISH V4 · Plan 1 fluid play · Plan 2 juice / cameras / ultimate (2C playability rules stay). This phase is **world art only**.

---

## Why (Adnan)

Cinematic cams + reaction cams now look at the background. Left-side trees/foliage read as **untextured low-poly blobs**. Beach house / postcard backdrop feels weak vs detailed characters. Goal: **ship-quality postcard court** that still holds up in flyovers, perfect-serve angles, ultimate cuts, and after-point react cams — without hurting play readability or iPhone perf.

Art bible §6: clean toy courts; **one postcard landmark**; no noise behind the ball; warm daylight; characters stay the pop.

---

## Hard rules

1. **Play first** — ball + players stay readable. No dense foliage / props in the play corridor or ball flight path.
2. **Postcard, not open world** — one tennis court set + one strong landmark (beach house / island vibe). No cluttered gacha lobby.
3. **Match plate energy** — soft toy, warm daylight, Switch Sports / Wii clarity. Not hyperreal, not muddy low-poly gray.
4. **Mobile-honest** — URP-friendly; LODs / batching; 60fps feel on iPhone. No film-only megascans as requirements.
5. **Do not** regen hero, touch anims, rewrite cameras, or change gameplay/ultimate systems.

---

## Do (priority order)

### P0 — Fix what’s already on camera
1. **Audit** every mesh visible in: gameplay cam, serve toss, ultimate charge/release, reaction cams, any intro/flyover. Screenshot or list offenders.
2. **Left-side trees / foliage** — replace blob / untextured / gray low-poly with styled, textured, art-bible-friendly trees (chunky toy foliage OK; muddy gray blobs not). Match saturation: world slightly quieter than characters.
3. **Beach house / landmark** — bring to the same quality bar as hero (readable silhouette, clean materials, warm palette). One hero building, not a city.
4. **Ground / court skirt / sky / water (if present)** — kill obvious unfinished seams, missing materials, and z-fight that show in cinematic angles.
5. **Lighting pass** — one warm key, soft shadows, fill; keep bloom low. Characters still pop when cams cut wide.

### P1 — Cinematic readiness (same set, not new levels)
6. Make sure **flyover / island intro angles** (if already in project or easy to add as a short camera path) don’t reveal unfinished backsides or empty skybox holes.
7. Add cheap polish only where cams look: trim props, fence, umbrellas, path — big shapes, few pieces. No prop spam.

### Perf / tech
- Prefer Atlas / shared materials; reuse tree variants with scale/tint.
- Cull / LOD for distant trees; never drop material to pink/magenta or unlit gray in shipping path.
- Keep court collision + playable bounds unchanged unless a visual mesh was wrong (fix visual only).

---

## Don’t
- New sports / new courts / open island traversal.
- Rewrite Plan 2C camera UX or ultimate timing.
- Hero regen, Mixamo retarget, cosmetics system (Plan 3).
- Photoreal bark / 8K textures that tank mobile.

---

## Done when
1. Side-by-side or short capture: same angles that looked bad before (react cam, ultimate, serve) now show **finished** trees + beach house.
2. Gameplay cam still clean — no new clutter in ball path.
3. Brief note: what assets swapped/added, material/LOD notes, any remaining debt.

Reply paths + what you changed. **Stop for Adnan.** Next after approval = Plan 3 (gap clips + first cosmetics) unless Adnan asks for more env.

