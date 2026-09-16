# Playable course mock

Launch `GolfArcade` in Xcode on a physical iPhone. The simulator runs the menus, courses, and touch play, but it cannot supply a live camera body pose. Automated tests use launch-only fixture players for that reason.

## Menus

The app opens on a main menu:

- **Solo** — shows your saved player. Scan (or **Rescan**) your body, pick left- or right-handed, then choose a course.
- **Multiplayer** — 2–4 players. Each player gets a name, a color, a handedness, and their own body scan. Continue unlocks when every player is scanned.
- **Settings** — swing input (Camera, Touch, Phone), gesture controls, sound, haptics.

Scans are saved on the device. A single-player scan from earlier builds is migrated into the first player.

## Body scan

Set the phone where it will stay, stand 7–12 feet away, and keep your head, hands, and feet visible. Face forward with your arms slightly away from your sides and hold still until the scan fills. The scan records body proportions plus where you stood. That position is also where the **virtual ball** sits, so scan from where you plan to swing. Rescan from the player screen, or from the in-game `…` menu, if the phone moves.

## Courses

Each course has three holes that you play out, counting strokes against par:

| Course | Difficulty | Holes (par) | Character |
| --- | --- | --- | --- |
| Meadow Run | Easy | 3 · 4 · 3 | Wide fairways, a few bunkers |
| Pine Bend | Medium | 4 · 3 · 4 | Doglegs, narrower, 2–3 bunkers per hole |
| Cliffwater | Hard | 4 · 5 · 3 | Narrow, water carries, greenside bunkers |

- Aim starts at the pin every shot; the rail's ◀ ▶ adjust it by 2°.
- The next shot is played from where the ball stops. The suggested club is selected for you (putter on the green, wedge in a bunker).
- **Rough** takes 15% off the next shot and a **bunker** 40%.
- **Water**: +1 stroke, dropped short of the hazard on the line of play. **Out of bounds** (past the tree line): +1 stroke, replayed from the same spot.
- On the green, putts scale to the distance: a full stroke rolls about 1.6× the distance to the cup. A slow ball crossing the cup drops.
- After par + 5 strokes the ball is picked up so the round keeps moving.
- In multiplayer, each player plays the whole hole in turn, then everyone moves to the next hole. The scorecard shows strokes per hole, totals, and ± par. Solo best scores are saved per course.

Hole shapes and hazards are data in `GolfArcade/Game/Course.swift`; the 3D scene (`CourseScene`) and lie checks both read them.

## Layout

The hole fills the screen. A small chip in the top-left shows hole, par, yards to the pin, and shots. The club rail floats on the right (menu, clubs, aim). Result, hole, and scorecard panels appear only between shots.

## Lining up (Camera mode)

At the start of each turn, and whenever the active player has been out of view for two seconds, the camera opens as a **big window** over the course.

- A golf ball is drawn at your feet, a ring shows where your hands go, and a **club is drawn in your hands**. At address the club head points at the ball, so putting the club head on the ball is lining up.
- The skeleton only shows faintly while the camera is finding you; it disappears during a swing.
- Hold still with your hands in the ring. The ready ring fills over 1.5 s, then the window shrinks to the bottom-left corner and the course takes over.
- Swings made while the big window is open are practice swings and never cost a stroke.

## Your avatar

The golfer in the middle of the screen **copies your body**. Your 2D camera pose is rebuilt into a 3D pose with fixed limb lengths: when a limb looks shorter to the camera than it really is, the difference becomes reach toward the ball. The body scan sets the scale, so the avatar is the same size wherever you stand. The avatar holds a 3D club that follows your arms (same rule as the drawn club) over a ball on a tee. In Touch and Phone modes it plays the canned swing instead. See `GolfArcade/Avatar/`.

## Contact

Contact is judged by where your hands pass the ring during the downswing:

- **center**: full power
- **thin** (hands high): less power, lower
- **fat** (hands low): much less power
- **heel / toe** (hands to either side): less power and a curve
- **miss**: the ball barely moves

A dot in the corner camera view shows where you crossed, and the shot result names the contact. Power still comes from backswing length and downswing speed (Wii-style arm arc).

## Shot cameras

| When | Camera |
| --- | --- |
| Before the shot | Behind the golfer and ball, looking down the hole |
| On the green / putter | Eye level just behind the ball, looking at the cup |
| Impact | Hero shot in front of the golfer (1–1.8 s, longer for better swings; skipped for putts) |
| Ball in the air | Close behind the ball, following it |
| Ball coming down | 3/4 view as it lands and rolls out |

Replays run the same sequence with your recorded follow-through. Punch, swipe right, or tap the replay pill to skip. The director is `ShotCameraDirector`.

## Reactions

After the follow-through your avatar reacts to the result:

| Reaction | Shot |
| --- | --- |
| Club twirl, held finish | Center strike on the fairway or green, full distance |
| Held finish, nod | Solid result |
| Hands drop, head shake | Rough, or a mishit that came up short |
| Club dropped, hands on hips | Fat strike or bunker |
| Stagger, hands on head | Whiff, water, or out of bounds |
| Arms up, jump | Holed |

## Friends on the tee (multiplayer)

The other players' avatars stand around you in their colors while you play. One waits on your lead side, close enough to reach: swing into them and they get knocked over, lie there for a moment, and get back up. It's just for fun and never affects the score.

**Planned:** throwing your club, or slamming it on the ground after a bad shot.

## Gesture controls (Camera mode)

Move through menus without touching the phone:

- **Arm a hand:** make a fist, held apart from your other hand, for a moment. If you are too far away for the camera to read your fingers, raise your hand to shoulder height instead.
- **Swipe** up, down, left, or right about a shoulder-width, quickly.
- **Punch** toward the camera to select.

| Where | Up / Down | Left | Right | Punch |
| --- | --- | --- | --- | --- |
| Menus | Move focus | Move focus / flip handedness | Move focus / flip handedness | Select |
| Before a shot | Change club | Aim left | Aim right | — |
| Ball in flight or replay | — | — | Skip | Skip |
| Shot result / hole done | — | Replay | Next | Next |
| Round complete | — | Menu | Play again | Play again |

Both hands together (a golf grip) never count, and gestures are ignored during a swing and for a second after impact. The picture-in-picture flashes each recognized gesture. The recognizer is `HandGestureRecognizer`, unit-tested with synthetic motion; the hand-shape reading uses Vision hand pose, which runs only while gestures are enabled.

## Other inputs

**Touch**: pull down on the swing pad and release; a 100-point drag is full power. **Phone**: grip the phone like a club, hold still, take it back, and swing through (physical iPhone only). Both always count as center contact.

## Ball flight

Shots are simulated, not scripted: gravity, drag, Magnus lift from backspin, then bounce and roll. At full power the model gives roughly driver 254 + 23 yd, iron 148 + 15, wedge 82 + 10. See `GolfArcade/Mock/BallFlight.swift`.

## What to test on a device

- Scan two players; confirm turns alternate and the camera only follows the active player.
- Stand with your hands off the ball: the swing must not arm. Line up, then make deliberately high, low, and wide swings; contact and flight should change.
- The avatar copies arm raises, crouches, leans, and a full swing without stretching or jitter; the drawn club head sits on the ball when lined up.
- Hold address 1.5 s: the camera window shrinks. Pure, fat, and whiffed swings trigger different reactions.
- In multiplayer, swing into the friend on your lead side and knock them over.
- Fist swipes and punch in every menu; the raised-hand fallback from about 8 feet; full swings never navigate.
- Hit into water and out of bounds on Cliffwater; check the penalty and drop.
- Background the app mid-flight: time pauses and resumes.

## Verification

The `GolfArcade` scheme runs unit tests (courses, lies, penalties, holing out, turn rotation, stroke cap, contact vs. the virtual ball, the ready timer, pose copying, club geometry, reactions, shot cameras, club contact, gestures, roster migration) and UI tests that walk menu → course (big camera window, then minimized), play a hole out with screenshots of each camera, and show friends standing around the golfer.
