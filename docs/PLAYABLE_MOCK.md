# Playable course mock

Launch `GolfArcade` in Xcode on a physical iPhone. The simulator runs the menus, courses, and touch play, but it cannot supply a live camera body pose. Automated tests use launch-only fixture players for that reason.

## Menus

The app opens on a main menu:

- **Solo** — shows your saved player. Scan (or **Rescan**) your body, pick left- or right-handed, then choose a course.
- **Multiplayer** — 2–4 players. Each player gets a name, a color, a handedness, and their own body scan. Continue unlocks when every player is scanned.
- **Settings** — swing input (Camera, Touch, Phone), gesture controls, sound, haptics.

Scans are saved on the device. A single-player scan from earlier builds is migrated into the first player.

## Body scan

Set the phone where it will stay and keep your head, hands, and feet visible. Face forward with your arms slightly away from your sides and hold still until the scan fills. The scan records body proportions for identifying the active player. Once on the course, hold a comfortable golf grip briefly to set the virtual club's address and reach. That address is stored relative to your shoulders and follows body translation and scale. Use **Reset comfortable grip** to change your setup, or **Rescan** if player matching is lost.

## Courses

Each course has three holes that you play out, counting strokes against par:

| Course | Difficulty | Holes (par) | Character |
| --- | --- | --- | --- |
| Meadow Run | Easy | 3 · 4 · 3 | Wide fairways, a few bunkers |
| Pine Bend | Medium | 4 · 3 · 4 | Doglegs, narrower, 2–3 bunkers per hole |
| Cliffwater | Hard | 4 · 5 · 3 | Narrow, water carries, greenside bunkers |

- The first aim is toward the pin. Use the rail's target button to pick any course position, including recovery directions. Your chosen target and aim adjustment persist after each shot. ◀ ▶ adjust by 2°, or 1° with the putter. In Camera mode you can also aim from the ball: hold one arm straight out to the side at shoulder height, like signalling a turn, and the line steps that way — once after a moment, then every half second while the arm stays out (the side you see in the mirror is the side the line moves). The shot controls also expose full/pitch/chip ranges and explicit draw/fade.
- The next shot is played from where the ball stops. The suggested club is selected for you (putter on the green, wedge in a bunker).
- **Rough** takes 15% off the next shot and a **bunker** 40%.
- **Water**: +1 stroke, dropped short of the hazard on the line of play. **Out of bounds** (past the tree line): +1 stroke, replayed from the same spot.
- Putting power has a fixed meaning independent of pin distance, continuous from zero. From the camera a putt is read from the hands' arc with a long meter (a full lag-putt sweep of about 55° fills it; a 20° rock is a mid-length putt), so a small stroke stays a small putt. A slow ball passing within the 0.12-yard arcade cup drops: the cup is drawn that size, cut into the green with a pale rim, and the ball rolls to the middle and falls out of sight down it rather than blinking off.
- After par + 5 strokes the ball is picked up so the round keeps moving.
- In multiplayer, each player plays the whole hole in turn, then everyone moves to the next hole. The scorecard shows strokes per hole, totals, and ± par. Solo best scores are saved per course.

Hole shapes and hazards are data in `GolfArcade/Game/Course.swift`; the 3D scene (`CourseScene`) and lie checks both read them.

## Layout

The hole fills the screen. A small chip in the top-left shows hole, par, yards to the pin, and shots; under it, while you set up, a large readout gives the distance to the hole (feet on the green), the rise and what it plays like, and the aim line, big enough to read with the phone propped up across the room. The club rail floats on the right (menu, clubs, aim). Result, hole, and scorecard panels appear only between shots.

## Lining up (Camera mode)

The camera starts in the bottom-left corner. Tap it to enlarge or shrink it; enlargement never blocks play. **Use touch** is available if the camera cannot track you.

- Hold your comfortable golf grip briefly. The virtual ball and club reach are established from that setup. Return to the same body-relative grip between strokes.
- The skeleton only shows faintly while the camera is finding you; it disappears during a swing.
- The readiness indicator requires continuous stillness. It is feedback, not a mandatory pre-shot countdown.
- Gentle putts use separate thresholds from full swings. Aborted swings time out; unreliable or missing impact frames cannot fabricate contact.

## Your avatar

The golfer **copies your body** using the existing body-pose reconstruction. During camera play, its grip and clubhead are projected from the exact virtual-club state used for contact and the camera overlay. Replays retain those endpoints. The inferred club is a gameplay model, not a measured physical club. Touch and Phone modes use a canned avatar swing.

## Contact

Contact is judged by the swept virtual clubhead passing through the ball between camera frames:

- **center**: full power
- **thin** (head high): less power
- **fat** (head low): much less power
- **heel / toe** (head off center): less power
- **miss**: a counted stroke with no ball movement and no impact flash/sound

A dot in the corner camera view shows the swept contact location. Power combines backswing length and downswing speed without a minimum-speed floor. Signed motion affects the start line; overswing and contact labels no longer inject preset hooks. Choose draw/fade explicitly because front-camera pose cannot observe a real club face.

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

Both hands together (a golf grip) never count, and gestures are ignored during a swing and for a second after impact. The picture-in-picture flashes each recognized gesture. Once you are set at the ball, club changes and selection wait for the shot, but a **swipe** left or right still moves the line, and the **arm signal** does the same without a fist: one arm out to the side, the other hand down, steps the line that way (`AimSignalRecognizer`, body joints only, so it needs no hand-pose reading). The recognizer is `HandGestureRecognizer`, unit-tested with synthetic motion; the hand-shape reading uses Vision hand pose, which runs only while gestures are enabled.

## Other inputs

**Touch**: pull down and release; 80 points is full swing power within the 100-point pad. Slide sideways to steer. Putting has a dedicated strength slider, an estimate in feet, and a **Putt** button; its 0.5% increments use the same fixed gain everywhere. Fine direction is available on the club rail and in shot controls. **Phone**: hold still, take it back, and swing through (physical iPhone only); putter thresholds accept smaller, slower strokes. Both use explicit center contact rather than pretending to measure it. All modes can choose aim and draw/fade in the shot controls.

## Ball flight

Shots are simulated, not scripted: gravity, drag, Magnus lift from backspin, then bounce and roll. The drag and lift curves are fitted to launch-monitor flights, and backspin grips the turf on the first bounce, so each club behaves like its real one: at full power the driver (106 mph, 12.5°, 2600 rpm) carries 250 and rolls 20 with a 33-yard apex; the 7-iron (88 mph, 17°, 6800 rpm) carries 160, hops 8 and comes down at about 48°; the sand wedge (70 mph, 29°, 9800 rpm) carries 90 and checks up within 3. From the camera, a full swing gets shorter with the club — 120° of hands arc for the driver, 110° for the iron, 100° for the wedge — so the committed swing you naturally make with each club fills its meter. The meter is not locked at the top: ease your hands back down slowly and it follows them, and the shot is played from wherever your real downswing starts. See `GolfArcade/Mock/BallFlight.swift`.

## What to test on a device

- Scan two players; confirm turns alternate and the camera only follows the active player.
- Set a comfortable grip, then make deliberately high, low, and wide swings; the virtual club, contact marker, and resulting shot should agree.
- The avatar copies arm raises, crouches, leans, and a full swing without stretching or jitter; the drawn club head sits on the ball when lined up.
- Verify the camera stays compact through tracking loss, touch remains available, and pure, fat, and whiffed swings trigger different reactions.
- In multiplayer, swing into the friend on your lead side and knock them over.
- Fist swipes and punch in every menu; the raised-hand fallback from about 8 feet; full swings never navigate.
- Hit into water and out of bounds on Cliffwater; check the penalty and drop.
- Background the app mid-flight: time pauses and resumes.

## Verification

The `GolfArcade` scheme runs unit tests for detector input, contact, short-shot distances, scoring, turn rotation, persistence, replay, and pause behavior. UI tests exercise menu → course, compact camera with touch fallback, target controls, touch play, and multiplayer. See `AUDIT_EXECUTION.md` for the mapping to the September 16 audit and physical-device validation still required.
