# EMOTE STANDARD — gate results (2026-10-12)

Each gate is PASS / FAIL against the line under it. The evidence is in `logs/`, `film/` and `preview/`. Eye lines are Adnan's call; I mark them OPEN.

| # | Gate | Line | Result | Evidence |
|---|---|---|---|---|
| G1 | CLIPS_BUILT | All 32 actions build: 7 emotes × (tennis + free) × 2 heroes, plus Walk/Run × 2. The written clip replays the solved pose to ≤ 0.01 mm. | **PASS** (max 0.004 mm) | `logs/build_male.txt`, `logs/build_female.txt` |
| G2 | FEET_ON_COURT | Lowest body vertex in every frame is ≥ −6 mm. Unity's `Ground()` absorbs a few mm. | **PASS** (worst −4.8 mm: male Defeat, frame 15, rolling onto the toes) | build logs, MIN_BODY_Z |
| G3 | RACKET_ABOVE_COURT | Lowest racket vertex in the tennis versions is ≥ 0 in every frame. | **PASS** (lowest +27 mm: male Defeat, racket head resting by the knee) | build logs, MIN_RACKET_Z |
| G4 | LEG_REACH | Locomotion legs stay at ≤ 98.5 % of leg length. | **PASS** (0.985 by the reach guard) | build logs, INFO |
| G5 | LOOPS_SEAMLESS | In Walk/Run, frame P equals frame 0. | **PASS** (by construction: phase functions with period P) | `scripts/gait.py` |
| G6 | TENNIS_PLAYS | Both heroes: all 7 play through `HeroTennisDriver.PlayEmote`. The 6 automatic reactions pick the new clips. | **PASS** (13/13 male, 13/13 female) | `film/all_tennisall_*/summary.txt`, `film/InGame_TENNIS_male.mp4` |
| G7 | GOLF_PLAYS | Both heroes: all 7 play through `GolfGame.PlayResultEmote`. Walk/Run are golfer moves. | **PASS** (9/9 each) | `film/all_golfall_*/summary.txt`, `film/InGame_GOLF_male.mp4` |
| G8 | PLAZA_PLAYS | Both heroes: the stick drives walk/run/sprint on the new cycles. All 7 play through `HubPlayer.PlayEmote`, empty-handed. | **PASS** | `film/all_huball_*/summary.txt`, `film/InGame_MENU_male.mp4` |
| G9 | GOLF_AIM_FOOTWORK | Same scripted aim input, before/after. The shoes stay planted between steps and the steps alternate. | **PASS** (male: 21 steps; before = whole-stance slide) | `film/GolfAimFootwork_male_beforeafter.mp4` |
| G10 | INSTALL_EXACT | Prefabs differ from the pre-install files by exactly the 7 slots (28 lines) each. Installed files match the clone that was filmed. | **PASS** | `INSTALLED_SHA256SUMS.txt`, `install/BASE_PREFAB_SHA256.txt` |
| G11 | LOBBY_SYNC | Hero sync re-exports the lobby heroes with the 7 new clip ids and rewrites provenance. | **PASS** (8 rig/JSON files updated, meshes unchanged; jumpJoy … defeat exported, loop seams ≤ 0.002 mm; hitPerfect now = the new Fist Pump, matching the iPhone label) | `logs/hero_sync.txt`, `work/hero-sync/out/logs/Run-report.txt` |
| G12 | NATIVE_BUILD | The app compiles with the new `EmoteCatalog` and locker focus rows. | **BLOCKED** (not by this change). The full build fails in `GolfArcade/Social/ClubHub.swift`: `HubRoomTransport` is not found. That is another session's uncommitted file, not in the Xcode target. Both edits compile on their own with `swiftc`: 13 emotes give 5 three-wide focus rows, and normalize/valid/name accept the new ids. | `logs/xcode_build.txt` |
| G13 | UNITY_TESTS | The EditMode suite on the installed state (clone = real project) has no new failures. | **PASS** (481/489). The 8 failures are the 6 golf `MultiplayerRulesTests` that failed before this job, plus 2 that only fail because the clone has no `GolfArcade/` and `proof/` beside it. The legacy-name lint was run by hand on every changed file: clean. | `logs/editmode_results.xml` |
| E1 | LOOK (eye) | Each emote reads as its emotion from the game camera. | OPEN (Adnan) | reels in `film/`, `preview/StandardSet_male_reel.mp4` |
| E2 | FACE (eye) | The face moods (happy / sad / angry / shock) are only lid, brow and gaze. The painted mouth does not change. | OPEN, known limit | |

Not done / limits:
- Golf has no flow that moves the golfer, so Walk/Run are loaded but unused in golf. It needs a decision on where to use them: walk-on to the tee, walking to the ball, or something else.
- The mouth is painted: no smile/frown change yet. A mouth-shape expression layer in `HeroFaceSkin` would be the next big emotion win, but it touches the lobby-parity look sources.
- `golfaim` was filmed on the male hero only.
