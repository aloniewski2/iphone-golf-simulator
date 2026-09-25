# Golf Arcade — Unity

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

## Profiles, two players on one phone, and online

The home screen has **SOLO · 2 PLAYERS · ONLINE** over the dock and a **profile** button at the
top left. PLAY goes the chosen way, on the holes COURSE picked (one hole, or the round):

- **Solo**: tees off as the phone's profile, straight away.
- **2 players** (up to four): each player is a profile with their own name and golfer (the male or
  female golfer, kit and shirt, picked on the golfer select screen). They take turns hole by hole:
  one plays the hole out, the next tees off as their own golfer with the same wind (the hole's
  intro says whose turn it is), and then the card comes up. It has a row per player and the
  standings in its tiles, and names the hole's winner, then the round's.
- **Online**: two to four phones play the same holes at the same time, with the same wind from the
  room's seed. Pick *Quick match*, *Create a room* (share the 4-letter code) or *Join*. Scores
  arrive as others hole out, and the last card waits until everyone is in. It needs the game server
  in `server/` (see its README), with its address set in the Online screen or in
  `BackendConfig.DefaultServerUrl`.
- **Profile**: the name, the golfer, and a record: rounds, best round, average against par,
  birdies, holes in one, and matches won, tied and lost. *Sign in online* creates the profile's
  server account, which keeps the record across devices and puts it on the leaderboard. The other
  golfers on this phone can be added, switched to and deleted there.

The golfer the phone plays as is the active profile's. The GOLFER button edits it as before.

**Formats for 2 players** (picked on the 2 PLAYERS screen): *stroke play*; *match play* (win
holes, not strokes: "2 UP"); *closest to the pin* (one tee shot each on the par 3s, feet from the
pin, a hole in one is 0 ft); and *longest drive* (one drive each on the par 4s and 5s, counted only
if it finishes on the fairway). The round card shows each player's shot and the standings.

**The Open** (OPEN on the home screen): four rounds of the chosen course against eleven tour pros,
with a clubhouse leaderboard after each round and on the OPEN screen. It is saved on the phone,
so the event can be played a round at a time. The pros' rounds are played out hole by hole from
the event's seed and their skill (`Championship.cs`).

**Courses**: `Course.All()` lists them; COURSE browses every course's holes and FULL ROUND picks
the round of the course on show. New holes are built from design modules with
`blender/scripts/course_builder.py` (an optional `PALETTE` recolours them, matched in the game
by a theme in `HoleView.Themes`).

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
- `Assets/Scripts/UI` — the HUD, the home, golfer and course screens, and `Lobby` (profile,
  2 players, online).
- `Assets/Scripts/Profile` — `PlayerProfile`, `ProfileBook` (pure C#) and `ProfileStore` (PlayerPrefs).
- `Assets/Scripts/Course/Match.cs` — turns, the shared wind and the standings for any number of players.
- `Assets/Scripts/Net/Online` — the server's message shapes, `OnlineRoom` (pure C#), the socket
  session and the REST client.
- `Assets/Tests` — EditMode tests for the detector, shot model, wind and scorecard; a PlayMode
  smoke test.
- `Tools` — `check.sh`, the editor-free compile + EditMode test run.
