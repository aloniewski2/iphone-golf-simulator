# Golf Arcade — Unity

## Permanent character asset library

**Current runtime:** golf now uses our permanent V4 male/female characters, Golf Kit 1, held driver and V4 drive animation. See [runtime details](../SportsLibrary/GOLF-RUNTIME-INTEGRATION.md). Other sport clips and club-specific swing switching remain pending.

The latest game and source library are combined on `feature/standard-characters-integration`. See the [integration baseline](../SportsLibrary/INTEGRATION-BASELINE.md): the current rigged golfer and its single baked swing still need migration to the permanent V4 character assets.

The [Sports Library](../SportsLibrary/README.md) contains the permanent male/female character standards and all current sports-art deliverables. Read the [character standard](../SportsLibrary/CHARACTER-STANDARD.md) and [integration handoff](../SportsLibrary/UNITY-HANDOFF.md) before importing or replacing golfer visuals. This source-library commit does not yet replace the current procedural golfer or modify runtime gameplay.

A Unity 6.3 LTS port of the phone-swing golf game, played the way Wii Sports Golf is played:
the iPhone is the club. It runs on the phone, so the same device shows the hole, takes the aim
buttons, and measures the swing.

## How it plays

1. **Aim** — hold ◀ ▶ to sweep the aim line (a tap nudges it 1.5°). The club is picked for you by
   lie and distance; ▲ ▼ changes it. Each hole has a **wind** (up to 20 mph), shown as an arrow
   relative to your aim with what it does in words (*12 mph left to right*). The yellow ring marks
   where the club's full swing actually carries in that wind, so aim off it the way the Wii teaches.
2. **Address** — hold the phone still for a moment (like a golfer settling over the ball). The HUD
   says *Ready — swing!*
3. **Backswing** — as you draw the phone back, the power meter on the left fills with the size of
   the backswing, exactly like the Wii's meter. The tension builds with it: the phone's haptics
   buzz harder and sharper in your hand (CoreHaptics, `Plugins/iOS/GolfHaptics.mm`), a creaking
   wind-up climbs in pitch, and the meter warms from green to orange and starts to tremble.
4. **Downswing** — the swing's **peak rotation speed** is the power (each club has its own full
   speed: driver 16 rad/s, 7-iron 13, wedge 9, putter 3). The **roll of your wrist** between address
   and impact is the club face: open slices, closed hooks, a few degrees are forgiven. Swing
   much harder than the club's full speed and the shot goes wild.
5. The ball flies through the same physical model as the iOS app (drag, Magnus lift, bounce,
   lie-dependent roll, a cup that captures or lips out) — with the air itself moving, so a
   headwind balloons and shortens a shot and a crosswind pushes it — the camera chases it, and the
   result card reads carry, total and lie. Water and out of bounds cost a stroke.
6. After the last hole the **scorecard** comes up (hole by hole against par, total, over/under)
   with *Play again*.

Sounds are synthesised at start-up (a strike per club, the downswing whoosh, the cup rattle, a
splash, a chime when the swing is armed, a fanfare for a holed ball, button ticks), so there are no
audio assets to manage. Haptics: a tick when the swing arms and on every button, a thump at impact
scaled by power, success for a holed ball, a buzz for water or out of bounds. The HUD lays out inside the phone's safe area.

## Working in the editor

Open `Unity/` with Unity `6000.3.24f1`. Everything is built at runtime from the one **Golf Game**
object in `Assets/Scenes/Golf.unity`; there is nothing to wire by hand. Play mode without a gyro
uses the synthetic swing: **hold Space** (or the on-screen *Hold to swing* button) to draw back,
release to swing; hold **Shift** on release for an over-swing, **A** / **D** to close or open the
face; ← → aim, ↑ ↓ change club.

To tune against a real swing without building, install **Unity Remote 5** on the iPhone, plug it
in, set *Edit → Project Settings → Editor → Device* to the phone, and the editor receives the
phone's gyro live.

Tests: *Window → General → Test Runner*, or headless:

```bash
/Applications/Unity/Hub/Editor/6000.3.24f1/Unity.app/Contents/MacOS/Unity -batchmode -nographics -projectPath Unity -runTests -testPlatform EditMode -testResults /tmp/editmode.xml -logFile /tmp/tests.log
```

The **Golf Arcade** menu has the same runs (*Run EditMode Tests*, *Run PlayMode Tests*) writing a
plain-text report to `Library/TestResults/<Mode>.txt`, plus *Play Golf Scene*, *Capture Game View*
(a phone-resolution PNG of what the player sees, to `Library/Captures/`) and *Debug Swing*. The
PlayMode suite includes `ReviewCaptureTests`, which plays a stroke at real speed and saves frames
(flyover, address, backswing, flight, result, next shot) to `Library/Captures/review/` — the
quickest way to look at camera or HUD work without a device. These menu items can be clicked from
a script (`osascript` → System Events) so the editor can be driven with no mouse.

Headless Unity refuses to start while the editor GUI has the project open. For that case
`Tools/check.sh` compiles all three assemblies with the compiler the editor ships and runs the
EditMode tests on its bundled .NET runtime — no Unity process needed (the PlayMode smoke test
still needs the editor):

```bash
Unity/Tools/check.sh
```

### MCP for Unity

The project includes [MCP for Unity](https://github.com/CoplayDev/unity-mcp) so Claude Code can
drive the editor (scene edits, play mode, tests, console). Register the server once:

```bash
claude mcp add --scope local --transport stdio UnityMCP --env MCP_TOOL_TIMEOUT=720000 -- ~/.local/bin/uvx --from "mcpforunityserver==10.2.0" mcp-for-unity
```

It needs `uv` (`curl -LsSf https://astral.sh/uv/install.sh | sh`) and the editor open on this
project; check the status under *Window → MCP for Unity*.

## Building for the iPhone

*File → Build Profiles → iOS → Build* (or *Golf Arcade → Build iOS Xcode Project*, which writes to
`Builds/iOS` and a verdict to `Library/BuildResults/ios.txt`) produces an Xcode project; open it
and run on the phone with your own team. Bundle id `com.aloniewski.golfarcade.unity`, portrait
only, iOS 17+. `Tools/install-phone.sh` signs that project from the command line (team, bundle id
and device as env overrides, nothing committed), installs it with `devicectl` and launches it.

## Layout

- `Assets/Scripts/Swing` — `MotionSwingDetector` (pure C#, the recognizer) and motion sources
  (phone gyro, synthetic).
- `Assets/Scripts/Shot` — clubs, calibration, and the `BallFlight` solver.
- `Assets/Scripts/Course` — hole geometry and lies, `Wind`, `CourseShot` (flight on the course,
  roll, cup), `Scorecard`, `HoleView` (runtime meshes).
- `Assets/Scripts/Game` — `GolfGame` state machine, camera, golfer, swing wiring, `GolfSounds`,
  `GameCapture` (render what the phone shows to a PNG).
- `Assets/Editor` — project setup, the test and play menus.
- `Assets/Scripts/UI` — the HUD.
- `Assets/Tests` — EditMode tests for the detector, shot model, wind and scorecard; a PlayMode
  smoke test.
- `Tools` — `check.sh`, the editor-free compile + EditMode test run.
