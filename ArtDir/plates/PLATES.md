# Party Sports — six reference plates, review set 01

**PHASE: Plates · Status: PENDING ADNAN APPROVAL — NOT LOCKED**

Created 2026-09-25. Six 1920×1080 PNGs; `review-sheet.jpg` is a numbered contact sheet. These are concept composites, not Unity captures, playable UI, or evidence of mobile rendering performance. Approval locks visual direction, not generated micro-detail or anatomy errors.

## Authority and scope

- Art bible: `/Users/adnanyonathan/Downloads/adnan-party-sports-art-bible.md` (v1.0).
- Instructions: `/Users/adnanyonathan/Downloads/AGENTS.md`.
- Repository: `/Users/adnanyonathan/Documents/Codex/2026-09-20/wh/outputs/iphone-golf-simulator`.
- Paths below are repository-relative unless marked otherwise.
- Existing targets A/B and six base-avatar concepts were reviewed; sampled video frames were rejected as pose sources because their bald avatar differs from the chosen identity. Textures/frames were not counted as plates.
- Exactly **3 Higgsfield still generations** used: hero refinement, miss, win. No video generation, Tripo, Mixamo, Blender rebuilding, Unity lighting changes or game-code edits.
- `compose_plates.py` reproduces the six composites from saved sources. `sources/refine_hero.py` records the local hero proportion composite. Originals remain unchanged.

## Shared hero and production targets

**Male prototype direction:** blond swept sculpted hair; white visor with navy underside; warm skin; simple large eyes; white polo with navy/orange edging; relaxed navy shorts; white socks and chunky shoes. This derives from the right-hand athlete in `SportsLibrary/ArtDirection/Tennis/target-characters-b.png`. Hair and visor are future attachment meshes, not permission to regenerate bodies.

Plate 01 is the proportion authority. Its original generated head was still too large, so the head layer was scaled to 58% around the neck attachment in a 2D composite. Approximate hair-top-to-chin extent is 23–24% of the resulting standing figure. This is a visual ratio, not a measured rig. Target engine height: **1.75 m**, unchanged gameplay reach across cosmetic variants. The two action poses were referenced to that corrected figure; pose foreshortening and minor generated facial/shoe variation must not become separate body designs.

Palette targets: sky `#7EC8E3`; floor `#F4F0E6`; lines `#2B2B2B`; CTA `#FF6B3D`; secondary `#5B8CFF`; success `#3DDC97`; whiff `#FF5A7A`; warm skin albedo `#FFE0C2`; white cloth; cool shadow `#4A5A78` at roughly 24%; panel white; text `#1A1A1A`. The composited shapes use these exact hexes. Shaded character pixels do **not** prove those material albedos.

Lighting target for later implementation: soft warm directional key at 35–45° elevation, cool fill at 30–40% of key, readable face, contact shadow. No required bloom, grain, vignette or motion blur. Generated highlights are mood evidence only; no physical lighting values can be extracted from these pictures.

## 01 — Hero (`01_hero.png`)

- **Source:** `SportsLibrary/ArtDirection/Tennis/target-characters-b.png` → Higgsfield refinement → `ArtDir/plates/sources/hero-refined.png` → `hero-proportion-composite.png`.
- **Why:** clearest existing male identity; keeps the visor/polo/shorts language while moving toward the bible's athlete proportions. Full-body silhouette and gear remain visible.
- **Palette:** exact sky/floor/accent chips; white/navy outfit, orange edging, warm skin. Navy is retained from the existing candidate as a cosmetic choice.
- **Proportions:** corrected ~23–24% head, thick limbs, oversized shoes and racket; use this standing plate for the shared-body reference.
- **Lighting:** warm upper-left key appearance; flat clear backdrop and soft cool contact shadow; generator halo removed.
- **Camera:** front three-quarter, full figure, eye-level character presentation; later menu target FOV 40–50°, not a measured camera calibration.
- **Gaps:** visible neck/collar composite seam at full resolution; cloth, laces and hands remain more detailed than the bible. Treat soft-plastic simplification and mitt-like hand topology as production requirements. Do not copy pixels as final UV texture.

## 02 — Gameplay (`02_gameplay.png`)

- **Source:** upper resort horizon from `SportsLibrary/ArtDirection/Tennis/target-gameplay-b.png`; same `hero-proportion-composite.png` on both sides; locally drawn court, net, ball and minimal score.
- **Why:** retain the familiar coastal landmark while making the stage quiet and the two athletes/ball readable. Original blue court, tiny near/far player disparity and busy surroundings were not accepted wholesale.
- **Palette:** floor `#F4F0E6`, dark court lines, desaturated horizon, cool muted apron, blue sparse ball trail. Yellow ball gets maximum local contrast.
- **Proportions:** same hero cutout at two perspective scales; no second hero design. Racket and ball deliberately enlarged for couch readability.
- **Lighting:** source horizon still has warmer/brighter sunlight than the simplified foreground; soft cool shadows approximate the common direction.
- **Camera:** elevated court composition, two players and ball in frame; later gameplay FOV 50–60°. Central net/ball hierarchy is the reference.
- **Gaps:** this is a **composition/color blockout**, not a polished game screenshot. Both cutouts face the viewer; actual opponent-facing poses must replace them. Court/net perspective is illustrative, not a dimensions specification. Upper horizon remains busier than the bible. Near player uses a ready pose, not a contact pose; no animation or ball-contact validation implied. Approval should address readability and palette, not freeze this pose arrangement.

## 03 — Miss (`03_miss.png`)

- **Source:** `ArtDir/plates/sources/miss.png`, generated with the corrected hero as image reference; shared local sky/floor and pink skid strokes.
- **Why:** visible ball beyond the racket, surprised face, crossed follow-through and lifted balancing leg communicate a harmless whiff even without the caption.
- **Palette:** shared base palette; `#FF5A7A` marks the skid, never injury or punishment. Yellow ball separated from hands/racket.
- **Proportions:** same hair, visor, kit, body mass and shoes; action foreshortening must resolve to plate 01's standing body.
- **Lighting:** soft warm upper-left appearance with cool contact shadow, no dramatic lighting shift on failure.
- **Camera:** full action silhouette in medium-wide front three-quarter framing. Keep racket, loose leg and missed ball visible together; avoid a face-only crop.
- **Gaps:** no timing demonstrated by a still; hand overlap, shoulder rotation and grounded foot contact require later rig review. Pink skid is an illustrated effect cue. Fine cloth/footwear detail remains above the desired mobile simplicity. The eventual motion needs anticipation → overshoot → settle.

## 04 — Win (`04_win.png`)

- **Source:** `ArtDir/plates/sources/win.png`, generated from the same corrected hero; local restrained confetti and shared background.
- **Why:** raised fist, open smile, asymmetrical hop and retained racket form a readable celebration without power rewards.
- **Palette:** mint `#3DDC97` plus orange/blue confetti; identical white/navy outfit and sky/floor values.
- **Proportions:** same identity and nominal body; knees are bent in a hop, so image bounding-box head ratio is not standing height. Plate 01 controls production geometry.
- **Lighting:** warm soft key and cool floor shadow, matching the shared presentation rather than a victory-only spotlight.
- **Camera:** full-body three-quarter celebration with hand, racket and shoes inside frame. Preserve empty margin for overshoot.
- **Gaps:** minor generated face and shoe differences should be ignored in production; no new character version. Jump arc, landing and cosmetic clearance remain untested. Confetti is sparse concept decoration, not an approved particle budget.

## 05 — Home (`05_home.png`)

- **Source:** exact `hero-proportion-composite.png`; original locally composited UI, typography, backdrop and shadow. No copied third-party interface assets.
- **Why:** the equipped hero remains prominent on the left; one dominant Start Party action on the right; clear casual, solo and identity choices below it.
- **Palette:** exact orange primary button with dark text, white panel, warm neutral secondary rows, sky backdrop. No monetization banners or ranked pillar.
- **Proportions:** exact standing hero asset; no independent menu body, face or outfit.
- **Lighting:** same hero lighting and subtle contact shadow as plate 01; no extra cinematic rim required.
- **Camera:** landscape/AirPlay mood mock; full-body hero and generously spaced controls. Phone portrait and safe-area layouts are not specified by this still.
- **Gaps:** mocked future party path—online party implementation remains deferred; this does not claim working Create/Join. Flat mock cannot prove Dynamic Type, focus states, a11y or touch behavior. Panel is opaque white instead of 90% white to preserve contrast in this review. UI title is working copy, not a finalized brand mark.

## 06 — Loading (`06_loading.png`)

- **Source:** exact `hero-proportion-composite.png`; locally drawn falling ball, progress, status and copy.
- **Why:** same athlete tries one more practice bounce while the player sees preparation progress, a short control/etiquette tip and soft social nudge.
- **Palette:** shared sky/floor, orange progress, white tip panel and dark text; bright ball isolated above visor.
- **Proportions:** identical hero cutout; no separate loading mascot or alternate body.
- **Lighting:** same warm key/cool shadow appearance; no exposure shift between home and loading.
- **Camera:** medium-wide full-body composition with room above visor for the ball gag and clear text space to the right.
- **Gaps:** static **idle-beat concept**, not an animated loop. It suggests the ball about to drop, but a later glance/reaction would make the gag stronger. 65% is illustrative, not live telemetry; production must bind actual progress and handle stalls. Both displayed lines are short: one etiquette tip plus one social nudge. No generation/video is intended as runtime animation.

## Source generation ledger (3 of 3 used; no more submitted)

| Saved source | Higgsfield job | Reference |
|---|---|---|
| `sources/hero-refined.png` | `129c436d-5ece-4ebb-8b19-9417cd3880c3` | Existing target-characters-b, male on right |
| `sources/miss.png` | `acef4a80-b7be-42c1-a8fb-1938f7970f48` | Locally corrected hero |
| `sources/win.png` | `63679c67-df97-4325-bceb-aab369eceed2` | Locally corrected hero |

All generated with Higgsfield GPT Image 2.5, 3:4, 2K, high quality, transparent background. Final artboards are 16:9 local composites. Original generator outputs are retained for provenance, not extra approved plates.

## Approval gate

Adnan: mark **Approve** or **Reject + correction** for **each** item below. A file existing does not make it locked. No next phase begins from this deliverable alone.

| Number | Plate | Decision |
|---|---|---|
| 01 | Hero identity + proportions | Pending |
| 02 | Gameplay palette + composition | Pending |
| 03 | Funny miss beat | Pending |
| 04 | Celebration beat | Pending |
| 05 | Home / party CTA direction | Pending |
| 06 | Loading / idle direction | Pending |

Before these become production law, explicitly accept or reject the recorded gaps, particularly plate 02's pose/perspective placeholder and the hero's collar seam. This phase ends at review, not at implementation.
