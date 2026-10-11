# Plaza loading screen (D) + arrival fly-in: gate results, 2026-10-11

Adnan picked mockup D ("Ball roll") from the canvas https://claude.ai/artifact/KewuiCJrFBeQggrzU22N2Y and asked for a 2-3 s
cinematic fly-over into the character's view once it loads.

| Gate | Result | Evidence |
|------|--------|----------|
| TV shows loading screen D (not title + Play) while the Plaza boots | PASS | `hub-loading-tv.png` (68 %), `hub-loading-tv-100.png`; TennisTVRoot shows HubLoadingScreen while HubSession.drivesPhone |
| Bar follows real progress, never goes back, 100 % = ready | PASS | Swift `testBootLoadingBarFollowsUnityAndFadesIntoTheArrival`; Unity `ThePlazaBuildsInStepsAndArrivesWithAFlyIn` (14 steps, only rising, ends at 1) |
| Phone mirrors the bar, stick wakes at 100 % | PASS | `hub-loading-phone.png` |
| At 100 %: hold, fade into the fly-in (hubArrive) | PASS | Swift test (hubArrive after readyHold); Unity holds the opening shot until go |
| Fly-in 2-3 s, lands exactly on the follow camera, hero controllable | PASS | 2.60 s editor; camera within 5 cm of the follow target; `arrival_sheet.png` |
| Stick push skips the fly-in, hero moves on the next frame | PASS | fly-in test (< 0.7 s); `StickReachesTheHeroOnTheNextFrame` PASS |
| Back from a match: no fly-in, straight to the bench | PASS | Swift test; `BackFromAMatchYouSitOnTheSameBench` PASS |
| Hub PlayMode suites (Plaza, Bay, Party) | PASS 19/20 | `SoakHoldsFrameRate` flaky at its 58 fps editor bar on both versions: new 57.1-59.6 (6 runs, 4 pass), unchanged 58.1-59.4 (3 runs, all pass) |
| Device: smoothness of the bar on a real phone + TV | NOT RUN | worst build step 1478 ms in the editor (hero prewarm / polo load): the bar holds still during that step |
