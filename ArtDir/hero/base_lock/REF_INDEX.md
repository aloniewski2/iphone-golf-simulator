# Base lock reference index (use these — don’t invent angles)

## Authority look
Prefer bald plates for customize-ready head:
- `male_body_bald.jpg` / `female_body_bald.jpg` (front+back)
- Haired: `male_body_plain.jpg` / `female_body_plain.jpg` (legacy)

## Extra angles for modeling agents
| File | Use |
|------|-----|
| `male_multiangle_body.jpg` | 8 body angles M |
| `female_multiangle_body.jpg` | 8 body angles F |
| `male_head_detail.jpg` | Face + cranial dome M |
| `female_head_detail.jpg` | Face + cranial dome F |

## How to load in Blender
Put each panel (or full sheet) as Background Image / Image Empty on dedicated cameras: Front, Back, Left, Right, 3/4s. Lock camera; never freehand silhouette. Overlay proof every phase.

## Micro-goals (avoid Goal Mode thrash)
1. Male silhouette only vs multiangle + bald
2. Female silhouette only
3. Male head lock vs head_detail
4. Female head lock
5. Unity import look check
Do **not** ask for full M+F+Unity+GATE in one Goal if output keeps restarting.

## Face lock pack (Micro1g authority + Micro1h densify)
Dense male face authority for agents — **use this, not `preview/face_example/`**:
- Folder: `face_lock/`
- Spec: `face_lock/FACE_SPEC.md` (includes Micro1h densify note)
- Process: `face_lock/PROCESS_FaceMatch.md` **(fixed pipeline — densify / welded blocks)**
- Sheets: `face_lock/SHEET_head_detail_6.png`, `SHEET_multiangle_heads.png`, `SHEET_all_face_angles.png`, `SHEET_plate_vs_bad_example.png`
- Crops: `face_lock/01_*.png` … `16_*.png` (from head_detail / body_bald / multiangle)
- Paste prompt (active): `face_lock/PASTE_PROMPT_MaleFace_Micro1h.txt`
- Legacy Micro1g paste: `face_lock/PASTE_PROMPT_MaleFace.txt`
- Goal (active): `GOAL_HeroBase_Micro1h_MaleFaceDensify.md` (project root)
- Prior: `GOAL_HeroBase_Micro1g_MaleFaceLock.md` (FAIL — displacement/coarse topo; start from its blend SHA `c6b66ede…`)


## Female face lock pack (MicroF1 authority) — added by chat 5
Dense female face authority for agents — mirrors `face_lock/` (male). **Docs/plates only; no mesh.**
- Folder: `face_lock_f/`
- Spec: `face_lock_f/FACE_SPEC.md` (binary lines; female anatomy — no Adam's apple, soft arch brow, narrow rounded chin, standing-out ears)
- Process: `face_lock_f/PROCESS_FaceMatch.md` (densify / welded blocks, mandatory loops, no sticker volume)
- Sheets: `face_lock_f/SHEET_head_detail_6.png`, `SHEET_multiangle_heads.png`, `SHEET_all_face_angles.png`, `SHEET_female_vs_male_contrast.png`
- Crops: `face_lock_f/01_*.png` … `16_*.png` (from female_head_detail / female_body_bald / female_multiangle_body; boxes in `crop_boxes.json`, rebuild with `build_pack.py`)
- Paste prompt (chat 6): `face_lock_f/PASTE_PROMPT_FemaleFace_MicroF1.txt`
- Start blend SHA (verify): `blender/HeroBase_Female.blend` = `200dc3b9…5cd`
- Note: `female_body_plain.jpg` is byte-identical to `female_body_bald.jpg` (both bald).
