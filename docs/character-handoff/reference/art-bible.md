# Art Bible — Friends-First iPhone Party Sports Game

**For:** Adnan Yonathan  
**Stack:** Blender → Unity (URP) · iPhone · F2P cosmetics-only  
**Role of this doc:** Single source of truth for look. Higgsfield plates are mood reference; this bible is what production builds to.  
**North star feeling:** Wii Sports nostalgia + Switch Sports clarity + Fall Guys laugh-on-fail — readable from the couch and on TikTok with sound off.

---

## 1) Fantasy (one sentence)

You are a goofy, oversized athlete in a bright living-room sports arena, trying to look cool and failing in a way your friends will replay.

If a visual choice doesn’t serve **readable play**, **funny failure**, or **invite-a-friend energy**, cut it.

---

## 2) Pillars (priority order)

1. **Couch-readable** — silhouettes and scores readable from 2–3 meters / tiny phone preview.
2. **Filmable** — every big moment works as a mute vertical clip (clear pose, big motion, punchy color).
3. **Friendly, not sweaty** — toys and sport, not sim or esports grit.
4. **Failure is beautiful** — misses get more juice than routine hits.
5. **Cosmetics = identity** — skins change who you *are*, never how well you play online.
6. **Mobile-honest** — must hit 60fps feel on iPhone; no film-only tricks as requirements.

---

## 3) What we are / aren’t

| We are | We are not |
|--------|------------|
| Soft toy-athlete, big head, chunky limbs | Hyperreal humans, pores, uncanny faces |
| Clean courts, big shapes, playful props | Cluttered gacha lobbies, neon cyber cities |
| Warm daylight + soft shadows | Moody cinematic noir, heavy fog |
| Exaggerated squash/stretch on impacts | Stiff sports broadcast realism |
| Switch Sports / Jackbox clarity | Valorant / Rocket League density |

**Comps to steal patterns from (never assets):** Nintendo Switch Sports, Wii Sports, Fall Guys (silhouette + fail), Jackbox (UI clarity), light Splatoon energy (bold color blocks — not ink mechanics).

---

## 4) Character design

### Proportions (lock these)
- **Head:** ~20–25% of total height (readable face/emote)
- **Body:** short torso, **thick limbs**, big hands/feet (physics jokes read)
- **Height:** ~1.6–1.9 m in engine (pick one hero height and keep all archetypes in band)
- **Neck:** short; avoid stick necks
- **Face:** simple planes, big eyes or strong brow — readable at 64px on UI
- **Fingers:** chunky mitts or 3–4 stylized fingers max (mobile deformation)

### Silhouette rules
- Must read as “athlete with gear” in 0.3s as pure black shape
- Equipment (racket, ball, bat) oversized ~10–20% vs real life
- Hair/hats as **attach meshes**, not baked into body (cosmetics)

### Archetypes (cosmetics identities — same hitboxes / same moves)
Ship 4–6 base bodies max early; rest is skins/gear:
1. Classic Sporty  
2. Soft Round Buddy  
3. Tall Flex  
4. Compact Dynamo  
(Optional later: mascot-leaning, retro jersey, etc.)

**Non-negotiable:** archetype swap never changes online power, reach, or timing windows.

### Materials (character)
- **Primary:** URP Lit, mostly **matte / soft plastic** (smoothness 0.25–0.45)
- Skin: slight warmth, very light SSS only if cheap; else warm albedo
- Fabric: low smoothness, subtle fabric tint — no heavy weave detail
- Accents (shoes, logos): higher saturation, still matte
- **No:** clearcoat cars, mirror chrome whole body, 4K skin pores

### Texture budget (mobile)
- Body atlas: 1× 1K or 2K max for hero; 512–1K for distant LODs
- Bold painted shapes > noisy detail
- AO baked soft; avoid tiny text on jerseys

---

## 5) Color palette (production hexes)

Use these as defaults; cosmetics may shift accents but keep value contrast.

| Role | Hex | Notes |
|------|-----|--------|
| Sky / ambient cool | `#7EC8E3` | Soft sports-day sky |
| Court primary | `#F4F0E6` | Warm off-white floor |
| Court lines | `#2B2B2B` | Strong readable lines |
| Grass / soft field | `#6BBF6B` | If outdoor sports |
| Brand / CTA accent | `#FF6B3D` | Energy orange — UI primary |
| Secondary accent | `#5B8CFF` | Friendly blue |
| Success / perfect | `#3DDC97` | Hit juice |
| Fail / whiff | `#FF5A7A` | Comic fail, not punishing red-black |
| Character base A | `#FFE0C2` | Warm skin default |
| Character cloth default | `#FFFFFF` + accent trim | Cosmetics carry color |
| Shadow tint | `#4A5A78` @ 20–35% | Cool soft shadows |
| UI panel | `#FFFFFF` @ 90% + soft dark text `#1A1A1A` | Jackbox-clear |

**Saturation rule:** world slightly desaturated vs characters so players pop. Characters = candy; court = clean stage.

---

## 6) Environments

### Goals
- Empty enough for physics comedy and camera
- One “postcard” landmark per sport (big hoop, striped wall, silly statue) for TikTok recognition
- No visual noise behind ball / trajectories

### Scale
- Play space slightly toy-scaled (props a bit big)
- Strong horizon / back wall color block for vertical video

### Materials
- Flat-ish colors, soft gradients OK
- Contact shadows under characters
- Avoid reflective floors that kill readability

---

## 7) Camera & framing

| Context | Guidance |
|---------|----------|
| Gameplay | 3/4 side or elevated 3/4; FOV ~50–60°; keep both players + ball readable |
| Fail / perfect | Short punch-in or camera shake; optional 0.15–0.25s slow-mo on whiff |
| Menu / home | Hero character mid-frame, FOV cozy ~40–50°, soft rim |
| Vertical clip safe | Keep action in center 60% (TikTok UI overlays) |

**Never:** cinema ultra-wide that miniaturizes characters; shaky handheld as default.

---

## 8) Lighting & post (Unity URP)

### Lighting recipe
- **1 key** directional, slightly warm, ~35–45° elevation, soft shadows on
- **1 fill** (ambient or second light) so faces don’t go black — fill ~30–40% of key
- Optional **rim** from behind opposite key for menu heroes only
- Baked or mixed GI light if cheap; prefer simple over beautiful darkness

### Post (keep subtle)
- Bloom: low, only on emissive accents / perfect-hit sparks
- Color grading: slight warm lift in mids; crush blacks gently — match Switch Sports daylight, not Netflix
- Motion blur: **off** or tiny for mobile clarity
- Film grain / heavy vignette: **off**

**Acceptance test:** T-pose character on court screenshot should feel ~80% of locked Higgsfield *palette + light* without matching film detail.

---

## 9) Animation & “fun juice” (visual)

Motion style: **snappy anticipation → overshoot → settle**. Misses overshoot more than hits.

### Required visual beats per sport point
1. Ready pose (readable intent)  
2. Swing / throw with smear or slight stretch  
3. Impact or whiff (hit-stop 2–4 frames on perfect; longer comic freeze on airball)  
4. Recovery / stumble / celebration  

### Fail spectacle (priority)
- Exaggerated follow-through, off-balance lean, prop fly (safe, non-painful comedy)
- Face/emote swap on miss
- Soft landing dust — never gore or mean-spirited

### Cosmetics in motion
- Hats/gear skinned or socketed; must not clip on big fails (test fail anims first)

---

## 10) UI / menu / loading (matches product decisions)

### Home
- Biggest CTA: **Play with Friends / Start Party**
- Secondary: Quick Play, Campaign (side), Cosmetics
- No ranked as home pillar
- Center: avatar with equipped cosmetics
- Style: chunky buttons, Switch Sports friendliness, Jackbox one-job clarity

### Loading
- Funny sport idle / mild fail loop
- One-line tip + soft invite nudge if solo
- Progress always visible
- Palette and character must match in-game (no bait-and-switch trailer look)

### Type
- Rounded sans, heavy weight for CTAs
- High contrast; Dynamic Type–friendly sizes
- Avoid thin fashion typography

---

## 11) Higgsfield → production bridge

1. Lock **6–12 still plates** (hero, play, fail, win, home, loading mood). Stop regenerating the dream.  
2. Tag each plate: palette / proportion / light / camera notes.  
3. Rebuild in Blender to proportions here; textures to palette.  
4. Match Unity light/post to §8.  
5. Diff screenshots vs plates; fix only concrete gaps (“head +10%”, “saturation −10% court”).  

Higgsfield = mood board + marketing. **This bible = ship target.**

---

## 12) Pipeline notes (Blender / Unity)

- Single Mixamo-compatible humanoid; all clips share Avatar  
- Attach points: `Hat`, `Hand_R`, `Hand_L`, `Back`, `FaceExtra`  
- Export FBX from Blender; Unity Animation Type **Humanoid**  
- LODs: hero / mid / far (far can drop gear detail)  
- One shared character shader variant; cosmetics = maps + meshes, not unique shaders each

---

## 13) Do / Don’t

**Do**
- Big silhouettes, warm daylight, funny fails, friend CTA energy  
- Test every skin in mute vertical recording  
- Keep campaign/menu art consistent with play  

**Don’t**
- Chase photoreal or Higgsfield film grain in-engine  
- Dark competitive esports UI  
- Tiny ornaments that vanish on iPhone  
- Power glow that implies P2W  
- New full character regen per cosmetic (use sockets)

---

## 14) Definition of done (look)

A stranger watching a 5-second mute clip can tell:
1. It’s a party sports game  
2. Who whiffed  
3. They’d want to play with friends  

If not, art isn’t done — even if the mesh is “pretty.”

---

## 15) First production pack (ship this before expanding)

1. 1 hero body + 1 Mixamo humanoid Avatar  
2. 4 cosmetics (2 hats, 1 outfit tint, 1 celebration prop)  
3. 1 court for the first sport  
4. Anim set: Idle, Ready, Swing, Miss, Perfect, Celebrate, Sad  
5. Home + loading matching this bible  
6. 6 locked reference plates in `/ArtDir/plates/`  

---

*Version 1.0 — drafted for Adnan’s friends-first iPhone party sports game. Update palette/proportions only via explicit art bible revision, not one-off Higgsfield regenerations.*
