# Intro flyovers, character intros, skip votes, golf pop-in — 2026-10-12

## Round 2 — redesign "D" installed in the game (later the same day)

Brief (Adnan): "i like it, add it in. make sure for intros the character doesnt emote unless someone clicks their emote,
and they are given 3 seconds a piece to emote. also zoom it back a bit so the whole body is visible" + "make the UI a bit
more fun/goofy, it looks good but it feels too corporate".

| Gate | Result | Evidence |
|---|---|---|
| E1 REDESIGN_IN_GAME: golf flyover = opening + 3 highlights (2 picked hazards/features, the green last), tennis drone + intros + stare-down | **PASS** | `films/intro_E_ingame.mp4`; `IntroRulesPlayTests.GolfFlyoverShowsWhatMattersAndEndsOnTheGreen` (3 courses: 3 highlights, green last, camera above ground every frame, ends < 25 m from the pin). Highlights: Cliffside h7 fairway bunker / greenside bunkers / green; Wild Isles fairway bunkers / greenside bunkers / green; Magma h10 fairway bunkers / water carry / green |
| E2 NO_EMOTE_WITHOUT_A_TAP | **PASS** | golf: no `Intro_*`/`Emote_*`/Wave clip on any frame of the intro when nobody picked; tennis: `EmotesPlayed` unchanged on both players (`IntroRulesPlayTests`); film clip 2 (golfer stands) and clip 3 (rival stands) |
| E3 THREE_SECONDS_EACH | **PASS** | golf: 1.2 s over the shoulder + 3.0 s facing the camera (measured 4.20–4.25 s); tennis: rival 3 s, you 3 s, then the 2.5 s stare-down (`timings_E.txt`) |
| E4 A_PICKED_EMOTE_IS_PLAYED_OUT | **PASS** | golf Scuba / Push-ups and tennis Spike / Push-ups play inside the window and the beat runs on to their end (`IntroRulesPlayTests`, `MultiplayerRuntimeTests.EquippedIntroChoicePlaysInTheIntroductionAndIsPlayedOut`); online golf host timing in `MultiplayerRulesTests` (pick at 2 s → emote from 1.2 s into the intro + its length + 0.35 s) |
| E5 WHOLE_BODY_IN_FRAME | **PASS (my reading)** | head to feet with ground under them in every intro frame: `sheets/E_golf_*`, `sheets/E_tennis_full_pick_spike.jpg` |
| E6 FUN_NOT_CORPORATE_UI | **PASS (my reading — Adnan's call)** | sticker plates (white rim, candy gradient, tilt, springy pop + bob), pinned bubble tags with a pulsing dot, VS sticker |
| E7 UI_STAYS_ON_SCREEN_AND_CLEARS | **PASS** | plates were half off the bottom (fixed-size layer laid out before the canvas changed shape) and hidden golf tags froze as ghosts; now anchored to the real corners each frame and faded/cleared — `sheets/E_before_fix_…jpg` vs `sheets/E_*` |

Also changed after the first in-game film: the tennis stare-down was a 7° lens across the court (painted backdrop
smeared, rival tiny behind the net) → eye-to-eye waist-up close-ups on a normal lens, each player to their own name's side.

Tests (clone, final code): EditMode `PresentationDirectorTests` + `MultiplayerRulesTests` 46/52 — the 6 failures are the
same pre-existing golf result-timing tests listed below (another session's 14 s result fallback). PlayMode IntroRules,
PresentationFlow, MultiplayerRuntime, HoleFlow, NativeGolfFlow: 21/22; the one failure is
`EquippedEmotePlaysRequestedClipOnBothBodies` at `jumpJoy` — another session's new standard emotes are not wired on the
heroes in this clone. `GolfPresentationTests.SwingToResult…` still fails as below (the result camera only turns to face
the golfer on a holed big moment since 10-09). Swift was not touched in round 2; no iOS build re-run. Device not run.

## Round 1

Brief (Adnan): flyovers too fast; golf flyover from start to finish, different camera angles, scenery focus; character
intros at least 3 s; tennis flyover can stay short but intros 3 s; let people choose their intro emote (Mario Kart /
Wii Sports presentation); Skip goes to the emote stage, never skips the emotes; online, everyone must press Skip;
courses must look fully rendered the whole time, nothing loading or popping in as the ball/camera gets close.

Editor captures (Unity 6000.3.24f1, APFS clone of the project, 960x540, 20 fps capture clock). Device not run.

## Gates

| Gate | Result | Evidence |
|---|---|---|
| G1 GOLF_FLYOVER_LONGER: full ≥ 9 s (was 4 s, later holes 3 s), repeat ≥ 5 s (was 1.5 s) | **PASS** | measured 9.80 s / 6.00 s on Cliffside, Wild Isles and Magma (`timings_after.txt`) |
| G2 GOLF_TEE_TO_GREEN_ANGLES: 4 angles, visits the tee and the green, camera never under the ground | **PASS** | `IntroRulesPlayTests.GolfTourFliesFromTheTeeToTheGreenAboveTheGround` on all 3 courses (4 shots; tee < 40 yd, pin < 45 yd; above ground every frame) |
| G3 SCENERY_FOCUS: postcard aerial with sea/horizon, long-lens green shot with the backdrop | **PASS (my reading)** | `sheets/after_golf_full_*.jpg`, `films/after_golf_full_*.mp4` |
| G4 CHARACTER_INTRO_MIN_3S | **PASS** | golf intro 3.25 s (all courses); tennis rival + player intros ≥ 3.2 s each (show 13.35 s: drone 4.55 s) |
| G5 SKIP_LANDS_ON_EMOTES | **PASS** | golf skip at 2.0 s → flyover 2.05 s, intro still 3.20 s, a 2nd skip during it ignored; tennis skip at 1.5 s → 8.8 s of intros still play; `IntroRulesPlayTests` (golf + tennis) |
| G6 INTRO_EMOTE_PICK | **PASS (Unity + build)** | golf: phone emote button during the flyover → `Intro_Pushups` plays on the tee; tennis: `RequestEquippedEmote` during the drone → chosen; Swift `canPlayEmote` accepts "intro", golf picker overlay, Unity phone sheet PICK YOUR INTRO; iOS simulator build SUCCEEDED |
| G7 ONLINE_EVERYONE_SKIPS | **PASS (rules) / NOT RUN (2 phones)** | golf host: 5 new `MultiplayerRulesTests` (all must ask; dropped golfer doesn't block; one phone speaks for its pass-the-phone guests; asking after the flyover changes nothing; golfer-up intro pick timed); tennis versus: `IntroRulesPlayTests` (one phone's skip keeps the flyover, the other's makes it skip). No live two-phone run. |
| G8 GOLF_NO_DISTANCE_POP | **PASS (code + editor)** | no LODGroups on resort plants (`GolfCourseStandardPlayTests` updated), no 300 yd cut / 85 yd cone proxy for shrubs, no screen-size LOD/cull on hole 12 plants, grass blades laid at the ball's rest while it flies + soft camera fade, shadows 320 yd: `sheets/shadows_ab.jpg` (near unchanged, distant fairway now shadowed) |
| G9 NO_REGRESSIONS | see below | |

## Tests

- EditMode (full suite): 481 / 489 pass. Failing, not from this change: 6 golf rule tests that predate it
  (`GolfResultGivesThreeSeconds…`, `GolfAllFourTeeOff…`, `GolfTimeoutAddsStroke…`, `OnOneSharedPhone…`,
  `GolfRepeatedEvent…`, `FourGolfersCanFinish…` — the branch's uncommitted 180 s turn limit / 14 s result fallback),
  2 that read files outside Unity/ (absent in the clone).
- PlayMode: `IntroRulesPlayTests` 3/3 and `IntroReviewFilm` (explicit) pass. Regression set (IntroRules, PresentationFlow,
  MultiplayerRuntime, NativeGolfFlow, GolfCourseStandardPlay, GolfPresentation, HoleFlow): 18 / 19 pass. The one failure,
  `GolfPresentationTests.SwingToResultShowsStatsAndLetsBothGolfersEmoteUntilContinue` ("result must face the character",
  0.29 < 0.5), checks the post-shot result camera, which this change does not touch; it was not re-run on a pre-change copy.
- iOS: simulator build SUCCEEDED (scratch xcodegen copy: the shared GolfArcade.xcodeproj does not list another
  session's new `Social/HubRoomTransport.swift`). Swift tests: `MenuFlowRegressionTests` 14/14 (one updated for the intro
  window), `TennisMenuSnapshotTests.testEmoteLoadoutScreens` passes after it sets the playing stage (it failed before on
  both its intro and point asserts). Three other snapshot tests fail on missing SceneKit scene content in the scratch copy
  (`testApprovedIslandScreens`, `testLoadingIdles…`, `testSportLockerPreview…`), unrelated. `sheets/phone_intro_emote_row.png`
  is the phone's intro row as that test renders it.

## Not verified

- Device frame rate with every plant at full detail and 320 yd shadows (4 cascades on the top tier, 2 below).
- A live online match on two phones (votes, phone Skip button with "1/2").
- Tennis venue LODs (tree pockets / crowd / props swap by screen size) were left as they are.

## Files

`films/` before/after mp4s (`before_golf_drive` / `after_golf_drive_cliffside` had different random wind; the controlled
shadow comparison is `drive_shadows_60yd` vs `drive_shadows_320yd`, calm wind), `sheets/` contact sheets, `timings_after.txt`, `tools/` (film + encode scripts).
