# MICRO GOAL 1f — Male hand plate fidelity + Unity soft-plastic skin

Prereq: start from Micro1e lock  
`ArtDir/hero/base_lock/blender/HeroBase_Male_Silhouette.blend`  
SHA-256 `35766aea1c493bab0ce1bddd6bc7639c4b304630c500c1d654b4336c2e113d90`  
FBX `ArtDir/hero/base_lock/unity_import/HeroBase_Male_Body.fbx` SHA `3268e665…`

Read first: this file, `AGENTS.md`, `art-bible.md`, then plates + current Unity proof:
- `ArtDir/hero/base_lock/male_body_bald.jpg`
- `ArtDir/hero/base_lock/male_multiangle_body.jpg`
- `ArtDir/hero/base_lock/male_head_detail.jpg`
- `ArtDir/hero/base_lock/proof/03_unity_m.png` (current)
- `ArtDir/hero/base_lock/proof/01h_hand_form_m.png`
- `ArtDir/hero/base_lock/proof/MALE_FORM_RESULTS.md` (honest leftovers: tube fingers, thin thenar)

## Assessment (do not soft-fail this)

Micro1e got CHIN + separated fingers + silhouette ≤5% and left body regions bit-identical. That is progress, **not ship quality**.

Eye vs `03_unity_m.png` + plates:
- **CHIN** — keep; do not reopen unless you regress it.
- **HANDS** — still FAIL quality: straight tapered tubes, weak knuckles, thin thumb/thenar vs plate hand mass. Separated digits ≠ plate soft-plastic hands.
- **VIBRANCY** — Unity `Blockout_Grey` reads dead clay. Plates are warm soft-plastic skin. Fix material/lighting read on the bald body — **not clothes**.

## Scope (ONLY)

1. **HANDS** — both hands only (wrist weld ring and distal). Match plate: thenar bulk, knuckle soft pads, finger taper + slight natural curl, readable gaps, thumb opposition. Soft-plastic toy anatomy, not realist tendons. Prefer sculpt/coordinate edit on current rebuilt topo; local remesh of hand only if tubes cannot hit plate (document why). Do not change palm cut above the existing weld.
2. **UNITY_SKIN** — URP Lit warm soft-plastic skin on `HeroBase_Male` (bald body). Roughness ~0.55–0.70 per `art-bible.md`. Warm tan matching plate skin (not grey, not yellow plastic, not metallic). Same material both Blender preview (optional) and Unity proof. Neutral studio + soft key/fill; optional subtle rim so form pops. **No textures required** if solid color + roughness lands the look.

## Do not touch / do not regress

- Chin/jaw/neck (z band already PASS), shoulders, waist, arms above wrist, feet, torso, scalp.
- No clothes, hair, face features (eyes/mouth still out of scope).
- No female. No Mixamo / Animator. No full-body remesh. No inventing ≤2% silhouette gate (keep ≤ **5.0%** bald front/back).
- Do not “PASS” hands because digits are separated — Adnan eye-checks vs plate.

## Gates (binary)

| Line | PASS when |
|---|---|
| HANDS | Front + 3/4 + side of **both** hands overlay-match plate hand mass: thenar, knuckles, taper, gaps. Not tubes/mitt/claw. |
| UNITY_SKIN | `03_unity_m.png` bald body reads warm soft-plastic like plates; not grey clay, not muddy, not oversaturated candy. |
| SIL_% | bald_front + bald_back each ≤ **5.0%** (report numbers). |
| CHIN / SHOULDERS / WAIST / ARMS_SIDE / FEET_SIDE | unchanged PASS (bit-identical or prove ≤0.5 mm max in those regions). |

## Deliver

- Updated blend (ArtDir) + FBX → Unity `HeroBase_Male`
- `ArtDir/hero/base_lock/proof/01f_hands_m.png` — plate vs mesh hands (L+R, front/3q/side) + before/after
- `ArtDir/hero/base_lock/proof/03_unity_m.png` — refreshed Unity body + hand close-ups + skin read
- `ArtDir/hero/base_lock/proof/MALE_FORM_RESULTS.md` — overwrite with Micro1f result
- Machine checks JSON optional under `proof/male-micro1f-checks.json`

## Reply format

```
GATE: MALE_FORM PASS
```
or
```
GATE: MALE_FORM FAIL
HANDS / UNITY_SKIN / SIL_% / CHIN / SHOULDERS / WAIST / ARMS_SIDE / FEET_SIDE
```
(list **only** failed lines — no soft language)
