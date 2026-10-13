# Swing fix — gate results (2026-10-12)

GATE 1: The splice set-up reproduces the shipped swings (original golf_swing.py vs. the shipped Drive, every bone, every frame): 0.00 mm. **PASS**

GATE 2: Everything outside the four re-authored takes is unchanged after the splice. **PASS**
- 13 other takes ≤ 5 µm, bones ≤ 15 µm.
- Vertices ≤ 0.2 µm; UVs, weights, vertex groups and material names identical.
- Normals ≤ 0.0006.

GATE 3: The hands stay on the grip in every re-authored take (worst slip, male and female): ≤ 0.24 cm. **PASS**

GATE 4: Downswing in-game (SwingFilm trace, top → ball launch): driver 0.25 s (was 0.38), HalfSwing 0.26 s (was 0.34); the pros measure 0.19–0.23 s. **PASS**

GATE 5: Lead arm straight. **PASS for the male driver; FAIL for the female address only.**

Lead elbow bend, address / top / impact:

| take | before | after |
|---|---|---|
| male driver, in-game | 68 / 38 / 93° | 33 / 15 / 20° |
| female driver, in-game | 66 / 57 / 87° | 45 / 24 / 27° |

- Top and impact pass for both.
- The female address stays at 45° (target ≤ 35°). Her shorter arms can't reach the straight-arm target without the hands leaving the grip, so keeping the hands on the grip won.

GATE 6: Lag held: wrist hinge with the lead arm parallel in the downswing (P5), Blender, male / female driver: 25/26° → 81/81°. **PASS (≥ 70°)**

GATE 7: No dive at impact (male driver, in-game). **PASS**
- Spine bend change: +18° → +1°.
- Head toward the ball: 13.3 → −1.3 cm.
- Head drop: 14.5 → 7.5 cm (female 13.8 → 4.1).

GATE 8: Iron / HalfSwing / Chip improved on the same lines (Blender, male; female similar): **PASS**

| take | lead elbow at impact | head drop | head toward ball |
|---|---|---|---|
| IronSwing | 72 → 43° | 11 → 8 cm | 6 → 4 cm |
| HalfSwing | 70 → 50° | 8 → 8 cm | 5 → 5 cm |
| Chip | 70 → 31° | 7 → 4 cm | 8 → 3 cm |

The HalfSwing's top lead elbow went 60 → 22° and its P5 hinge 21 → 61°.

GATE 9: Unity PlayMode tests (clone, final build). **PASS**
- GolferClipTests, SwingSmoothnessTests ×3, GolfMotionReadyTests ×3, HeroGolferTests.TheHeroSwingsAndMeetsTheBall and EveryClubSwings: 9/9.
- On the original assets, AFullDriveIsFluid fails (pre-existing settle pop, 1.18 m); it is fixed here.

GATE 10: Trail knee at impact ≤ 45°: 62 → 60° (male), 62 → 55° (female). **FAIL**

Raising the trail heel made it worse: a raised heel shortens the leg. Fixing it needs the pelvis to stay higher through impact (a change to the stance and leg solve), not attempted in this pass.

Not run:
- A phone/device build.
- Unity on the shared project. Everything ran in my APFS clone: the project as it was at 15:49 today, plus exactly these changes. The shared `GolferView.cs` also carries another session's newer footwork and mood edits, which were not in the clone. My edits there are separate blocks, but that combination has not been compiled or tested by me.
