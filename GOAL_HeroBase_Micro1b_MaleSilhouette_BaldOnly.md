# MICRO GOAL 1b — Male silhouette bald front/back only

Prereq: Micro1 MALE_SIL FAIL (~5.6–5.9% on bald). Continue from existing blockout — do not restart from scratch.

Read AGENTS.md, art-bible.md, ArtDir/hero/base_lock/REF_INDEX.md, proof/MALE_SIL_RESULTS.md.

**Authority for this gate:** only `male_body_bald.jpg` front + back.
**Ignore for PASS:** multiangle sheet views (sheet_front/back/left/right). Keep those cameras packed for later, but do not fail this goal on them.

Open: `ArtDir/hero/base_lock/blender/HeroBase_Male_Silhouette.blend`

ONLY: edit Body_M silhouette so bald_front and bald_back each have symmetric-difference/union **≤ 5.0%**. Keep metres, A-pose, height ~1.70m, locked cameras/origins from Micro1. Fix pink (too wide shoulders/arms) and green (crown / heels / inner thighs) from `proof/01_silhouette_overlay_m.png`. No female, no Unity, no face texture polish, no hair.

Re-run the same native-mask metric. Write:
- updated `proof/01_silhouette_overlay_m.png`
- `proof/MALE_SIL_RESULTS.md` with GATE line
- update STATUS.md if present

Reply exactly:
GATE: MALE_SIL PASS
or
GATE: MALE_SIL FAIL
+ bald_front / bald_back % + proof path.
