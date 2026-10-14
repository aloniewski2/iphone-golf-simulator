# Country-club hub — gate results (2026-10-12)

Brief: `ArtDir/ui/hub-targets-2026-10-12/country-club/COUNTRY_CLUB_HUB_BRIEF.md`. Code: see `../README.md`.
BEFORE = the project as it was when the task started (original `HubArt.cs` / `HubRooms.cs` / `HubIcons.cs`), AFTER = the
restyle. Both rendered and tested in scratch APFS clones of the same `Unity/` (windowed editor, `GOLF_MUTE=1`).

| Gate | Result | Evidence |
|---|---|---|
| SAME_7_SHOTS (same cameras) | **PASS** | `before/`, `after/`, `*_before_after.png`, `all_before_after.png`; cameras = `HubPlazaTests.LookStills` (`../tools/HubCountryClubProof.cs`) |
| LAYOUT_UNCHANGED | **PASS** | `HubLayout.cs`, `HubWorld.cs`, `HubPlayer.cs`, `HubBayScreen.cs`, `HubRemotes.cs`, `HubKit.cs`, `HubShapes.cs`, `HubHeroAnimator.cs` byte-identical to task start; door, spot, spawn and camera code untouched |
| MENU_FLOW (hub PlayMode suites) | **PASS** | `suite_before.txt` / `suite_after.txt`: identical, 22 passed, 2 failed, 1 skipped in both. The 2 failures exist before the change: `FourHeroesStaySmooth…` (editor fps with 4 heroes, 34.8 / 34.5 vs 55) and `OnlineTennisWearsTheLobbyLooks` (shirt colour, character code) |
| EDITMODE | **PASS** | `editmode_failures_before.txt` = `editmode_failures_after.txt` (the same 20 failures from other areas, none in the hub; 378 passed) |
| LABELS_AND_BUTTONS | **PASS** | LOCKER, CLUBHOUSE, PLAY, MOTION CLUB, TENNIS, GOLF, TRAINING, EXHIBITION, CAMPAIGN, ONLINE, ROUND, PASS THE PHONE, exits, station plates all present (navy board, gold text); spot ids, stations, bays unchanged |
| LIVE_SCREENS | **PASS** | the 7 bay screens keep `HubBayScreen` (same video clips, glitch shader); only a gold trim behind the frame and the navy/gold plate were added (`after/06`, `after/07`, `extra/x_bay_seated.png`) |
| CHARACTERS_ANIMATION_UNTOUCHED | **PASS** | no file outside `Scripts/Hub` art, `Resources/Hub` shaders and `work/hub-country-club` changed |
| PLAZA_SMOOTH_EDITOR | **PASS** | soak 45 s: 59.7 fps before / 59.7 after; p99 21.7 / 21.6 ms; doors worst 0.419 / 0.417 s; stick to motion 1 / 1 frame (`before/hub_metrics.json`, `after/hub_metrics.json`) |
| RENDER_COST (gameplay views, editor counters) | **PASS** for the plaza; **NOTE** for tennis/golf | table below |
| DEVICE_FPS (iPhone) | **NOT RUN** | needs an iOS export installed on a phone; not done (see below) |
| LOOK (Adnan, by eye) | **OPEN** | `*_before_after_target.png` put our AFTER next to the brief's target |

## Render cost (average of 90–120 live frames, the hub camera's own view)

| View | SetPass | Draw calls | Triangles | Vertices | Shadow casters |
|---|---|---|---|---|---|
| Plaza spawn, before → after | 32 → 36 | 272 → 251 | 869k → 784k | 732k → 658k | 187 → 131 |
| Plaza walking | 32 → 36 | 262 → 244 | 862k → 783k | 724k → 656k | 184 → 131 |
| Tennis ground | 35 → 36 | 98 → 138 | 637k → 681k | 487k → 517k | 65 → 75 |
| Golf range | 36 → 38 | 99 → 162 | 631k → 690k | 483k → 525k | 58 → 89 |

(Most of each view's triangles are the hero and its garments, the same before and after: the old tennis room was four
walls and a floor and still drew 637k.)
Tennis and golf were two walls and a floor; they are now outdoor grounds with trees, fences, walls and the clubhouse, so they
draw +40 / +63 calls, but in the same shaders (SetPass +1 / +2). Both stay below the plaza's own budget. Lighting is
cheaper there: the four room point lights are gone (only the sun lights outdoors). Everything is static-batched as before.
New shaders: `HubSurface` (one main light + SH + wrap, a two-octave value noise) and `HubWater` (sines + one highlight),
with no extra textures beyond one small base map, and no depth or opaque-texture reads. No new real-time lights; the lanterns
are emissive plus bloom.

Build: the plaza builds in the same 14 steps; editor worst step 1381 ms before, 911 ms after; boot 3.50 s before, 1.73 s
after. Editor times vary from run to run; the 7 shots' boot-to-ready was 13.9 s for both. The painted textures (stone,
slate, gravel, cobbles, lawn, the lit rooms, the clock and the sky) are made once per app run and kept, about 7 MB, so
coming back to the plaza after a match does not paint them again.

## Not done / open

- **DEVICE_FPS NOT RUN.** A paired iPhone 17 Pro Max is visible, but measuring needs a Unity iOS export and installing a
  build over the app on Adnan's phone (both before and after for a fair comparison). Ask before doing this.
- **LOOK OPEN.** Approval by eye is Adnan's. Known differences from the targets: the signs keep our Rubik font (the targets
  use a serif); the PLAY sign is on the portico's entablature, not a vertical banner on the tower (target 01); the golf
  view's pond and bridge are behind the screens from this camera (they are modelled, at the course's left); the interiors
  (Locker, Clubhouse, PLAY Hall) only took the palette (navy/gold signs, navy panelling), since they aren't in the brief's sheets.
- `HubPlazaTests.LookStills` predates the arrival fly-in: its first ("spawn") still now shows the fly-in's opening shot.
  The proof test here skips the fly-in first. `LookStills` itself was not changed.

## Pass 2 — interiors, PLAY on the tower, golf pond (later 2026-10-12)

| Gate | Result | Evidence |
|---|---|---|
| SAME_CAMERAS (rooms + changed outdoor shots) | **PASS** | `pass2/*_before_after.png`; room shots = `look_room_play/_locker/_clubhouse` cameras; before = pass 1 |
| LAYOUT_UNCHANGED | **PASS** | `HubLayout.cs`, `HubWorld.cs` untouched; every room wall, floor, bench, locker, sofa, desk, shelf and rail collider at the same place and size; the PLAY Hall's north wall keeps its full-width collider (the arches are drawn, the scenery behind them is never reached) |
| MENU_FLOW (hub PlayMode suites) | **PASS** | `pass2/suite_after.txt` identical to the original baseline (22 passed, the same 2 pre-existing party failures, 1 skipped); all 10 doors 0.40–0.42 s |
| PLAZA_SMOOTH_EDITOR | **PASS** | soak 59.5 fps (original 59.7), p99 24.0 ms (21.7), stick 1 frame (`pass2/after/hub_metrics.json`) |
| RENDER_COST | **PASS** | PLAY Hall view: SetPass 34, draw calls 141, 640k tris (`pass2/after/render_cost.txt`; the old hall wasn't measured, the tennis ground — a similar room — was 98 calls / 637k before) |
| DEVICE_FPS | **NOT RUN** | as pass 1 |
| LOOK (Adnan) | **OPEN** | `pass2/08_play_hall_before_after_target.png` |

Notes: the PLAY HALL title plate (hidden behind the old middle arch) is now on the hall's west wall above the TENNIS ◂ ▸ GOLF
board. From the range camera the middle of the course is always behind the three screens, so the pond and bridge sit right
of the range, in front of the clubhouse, on a terrace 1.2 m up: visible, but small at that distance.
