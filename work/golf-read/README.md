# Golf read pass (2026-10-12): putting line, aim arc, green read, wind, lie

The hunks of this pass are staged on `build-10/12` with `git apply --cached golf-read.patch`. Other sessions' uncommitted edits in the same files are left unstaged. Nothing is committed.

No animation, hero, map, camera, light or Swift file was touched. `before_after.jpg` is the side-by-side; `GATE_RESULTS.md` has the gates.

## What changed

1. **Putting guide line removed.** The putt ribbon (a full simulated roll, break and all) is deleted. The aim `LineRenderer` never gets points again.
   - Files: `Game/GolfGame.cs`, `Game/GolfGame.Network.cs` (one line).
2. **Half arc, then fade.**
   - Full shots: the dots follow the first **half** of the carry. Each dot fades and shrinks from about 45% of the shown length to nothing at the end, and sits on a faint dark halo so it still reads over sand, rock and cloud.
   - Putts: a few fine dots run straight down the aim line, out to half the distance to the hole, fading. They show the start line only, with no break and no pace.
   - All aiming aids are drawn in **still air**: the dots, the landing ring, the 25/50/75/100% markers and the amber load dot. Before, the wind's push was worked into them, which solved the wind for the player.
   - Files: `Game/AimDots.cs`, `Game/GolfGame.cs` (`LayAimLine`, `LayPuttStart`, `PreviewLaunch`, `ShotZone`).
3. **Subtler slope read.** The white/yellow/red contour grid and the beads are replaced by faint pale chevrons ("grain") on the green.
   - Each chevron points down the fall line and creeps that way. Steeper ground shows stronger, longer, faster chevrons; flat ground (< 0.4%) shows none.
   - The chevrons come from the same surface the roll uses.
   - Files: `Game/GreenRead.cs` (rewritten, same API), `Course/Surface.cs` (comment).
4. **Wind.**
   - Each hole already had its own wind from the round seed, the same on every phone.
   - New speed bands: 0–3 mph 25%, 4–9 mph 40%, 10–14 mph 25%, **15–20 mph 10%**.
   - The TV HUD shows a drawn arrow (relative to the aim; up = helping), the mph, and Light/Moderate/Strong, coloured white/yellow/orange.
   - Physics is unchanged: 15 mph across pushes a 7-iron about 21 yd, 20 mph about 29 yd.
   - Putts roll on the ground and get no wind.
   - Aim turns freely, so the player aims into the wind themselves.
   - Files: `Course/Wind.cs`, `UI/GolfShotHud.cs`.
5. **Lie.**
   - Fairway and tee are normal.
   - **Rough** keeps 90% of the distance, for every club. A real swing from it is knocked up to ±2.5° off line and ±4% off pace.
   - **Sand** keeps 75% (85% with the wedges), with up to ±5° and ±7%.
   - The old rough "flyer" is gone. It cut the spin, so wedges from the rough flew about 7% further.
   - The scatter is drawn once at the strike, before the shot goes to the network, so every phone flies the same shot. Planned test shots (`StrikeToward`) stay exact.
   - The landing ring widens from the rough and the sand.
   - A **lie chip** under the shot info shows a swatch (grass blades, sand grains, mown stripes), the name, and the cost for the club in hand.
   - Files: `Course/Hole.cs` (`CourseLies`), `Game/GolfGame.cs` (`OnImpact`, `UpdateAimVisuals`), `UI/Hud.cs` (`SetLie`), `UI/GolfShotHud.cs`.

Tests:
- Updated: `StrikeAndLieTests`, `PostcardHoleTests` (its "rules not to change" lie factors are now 0.9 and 0.75), `WindTests` and `GolfPolishTests` (the markers are still-air).
- New: `Tests/PlayMode/GolfReadCaptureTests.cs`, which renders the frames.

## Proof and how it was made

- `before/` comes from an APFS clone of the project made before any edit (`cp -c`); `after/` from a clone of the edited tree.
- Both are run windowed through `work/postcard-look/heavy.sh`.
- The test plays golf the way the app does (`PrepareNativeAddress`, so there are no editor buttons). It skips each flyover and drops the ball.
- The scenarios are: a short putt and a long putt across the island green's slope (hole 12); an 18 mph crosswind tee shot with an 85% swing (`NativeSwing`, fixed seed); and, on Cliffside hole 7, the rough clear of hazards and a fairway bunker, both aimed at the pin.
- `3z-diagnostic-dots-drawn-last.png` was a check that the dots are not hidden by draw order. They aren't. Near the ball they sit over the pale cliff and behind the PIN label, hence the halo.

Re-run on the live tree (writes `Unity/Library/Captures/golf-read`):

```bash
GOLF_MUTE=1 work/postcard-look/heavy.sh golf-read Unity/Tools/run-playmode-windowed.sh GolfReadCaptureTests
```

## Rollback

`golf-read.patch` holds this pass only.

```bash
git apply -R --cached work/golf-read/golf-read.patch
git apply -R work/golf-read/golf-read.patch
```

The first command unstages it; the second removes it from the working tree.
