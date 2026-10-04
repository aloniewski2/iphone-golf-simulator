# MICRO GOAL 1g — Male FACE LOCK (Body_M bald head only)

Prereq: start from Micro1f blend  
`ArtDir/hero/base_lock/blender/HeroBase_Male_Silhouette.blend`  
SHA-256 `57a08123bd714e58d744e2a59780e6aacb81dd71879f3ffeb14b31725eee6d73`

Read first (in order):
1. This file
2. `ArtDir/hero/base_lock/face_lock/FACE_SPEC.md` **(binary gates — obey every PASS/FAIL line)**
3. `AGENTS.md`, `art-bible.md`
4. Contact sheets + crops in `ArtDir/hero/base_lock/face_lock/`
5. Source plates (authority JPGs only):
   - `ArtDir/hero/base_lock/male_head_detail.jpg`
   - `ArtDir/hero/base_lock/male_body_bald.jpg`
   - `ArtDir/hero/base_lock/male_multiangle_body.jpg`

**REJECT:** `ArtDir/hero/base_lock/preview/face_example/` — wrong likeness (dough, hair, pink shirt). Use only as FAIL contrast via `face_lock/SHEET_plate_vs_bad_example.png`.

## Assessment (do not soft-fail)

Recent face_example attempt is **WAY OFF** the authority plates. Micro1f locked hands + Unity skin; face features were out of scope and remain wrong (low nose, dough cheeks, lump ears, sticker eyes when features appear). This micro is the face lock.

## Scope (ONLY)

**FACE ONLY** on `Body_M` bald head:
- Cranial dome, forehead/brow, brows, eyes, nose (**raise nose height to plate**), cheeks, ears, mouth, jaw/chin, stubble room, neck blend into existing chin lock
- Prefer sculpting primary forms into `Body_M` head; thin feature layers OK if clean
- No hair mesh this pass
- Material: keep/extend warm soft-plastic skin read on head

## Do not touch / do not regress

- Hands (Micro1f PASS) — bit-identical preferred
- Body below neck (shoulders, waist, arms, torso, feet)
- Silhouette ≤ **5.0%** bald front/back (report numbers) — do not invent tighter gate
- Chin plane from Micro1e — keep unless a documented micro-fix is required for nose/mouth; prove no melt
- No clothes, no female, no Mixamo/Animator, no full-body remesh
- Grey crew shirt on plates is **not** mesh scope

Authority = `ArtDir/hero/base_lock/face_lock/` + the three JPG plates above.

## Gates (binary — see FACE_SPEC.md for full lines)

Minimum reply lines (expand with every FACE_SPEC region that fails):

| Line | PASS when |
|------|-----------|
| OVERALL_LIKENESS | Matches plate male at arm’s length (front/3q/profile) — not face_example |
| CRANIAL | Bald dome front/profile/top match plates |
| BROW / EYES | Soft shelf; large brown socketed eyes + specular; thick arched brows |
| NOSE | Straight bridge; tip height matches plate (NOT low); alae/nostrils in 3/4 |
| CHEEKS / EARS / MOUTH / JAW | Athletic cheeks; readable ears; slight smile; U-chin + under-chin plane; no dough jowls |
| NECK / CHIN_LOCK | Thick neck; Micro1e chin lock held |
| NO_HAIR / NO_FACE_EXAMPLE_FAILS | No hair; none of FAIL_* tags in FACE_SPEC §14 |
| HANDS / BODY_BELOW_NECK | Unchanged PASS |
| SIL_% | bald_front + bald_back each ≤ **5.0%** |

## Deliver

- Updated blend (ArtDir) + FBX → Unity `HeroBase_Male`
- `ArtDir/hero/base_lock/proof/02_match_m_head.png` — multi-angle overlays (plate vs mesh heads)
- `ArtDir/hero/base_lock/proof/03_unity_m.png` — refreshed Unity **head close-ups** (+ body context ok)
- `ArtDir/hero/base_lock/proof/MALE_HEAD_RESULTS.md` — overwrite/create with Micro1g result + every GATE line
- Optional: `proof/male-micro1g-checks.json`

## Reply format

```
GATE: MALE_HEAD PASS
```
or
```
GATE: MALE_HEAD FAIL
<failed lines only>
```
