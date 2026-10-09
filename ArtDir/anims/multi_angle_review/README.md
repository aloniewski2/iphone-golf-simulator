# Current gameplay animation review

Review footage only. No animation, rig, weight, mesh, camera behavior or gameplay code was changed to make these captures. The review uses the current runtime hero, wardrobe, PlayableGraph, 1.2× visual tempo and procedural corrections.

Master: `Gameplay_AllAnimations_12Angles_60fps.mp4` — 1:25.5, 2560×1440, 60 fps, with embedded chapters. Individual animation-library, movement-blend and live-point reels are also retained.

## Camera grid

All twelve panels render the same simulation instant at 60 fps. The 2560×1440 master contains 640×480 panels; use fullscreen to inspect them.

| Row | Column 1 | Column 2 | Column 3 | Column 4 |
|---|---|---|---|---|
| Top | Front / net side | Front right | Right side | Back right |
| Middle | Back / playing side | Back left | Left side | Front left |
| Bottom | Overhead | Low front | Hands + racket | Knees + feet |

Directions are court-relative. Cameras follow the player so moving clips remain visible. HUD is hidden for inspection. Close-up cameras track their target; this movement is not animation jitter.

## Sections

The animation library uses controlled inputs in the gameplay scene. Swing/recovery, run blend, feet, hands and clearance processing remain active. Most chapters are three seconds, including a brief lead-in and recovery. They are diagnostic exercises without a live incoming ball, except the two dives. The separate live-point section uses autoplay and real gameplay physics/contact, from serve to point result. Neither is a raw FBX preview.

| Time | Animation |
|---|---|
| 0:00 | Ready |
| 0:03 | Idle |
| 0:06 | Forehand |
| 0:09 | Backhand |
| 0:12 | Jump serve |
| 0:15 | Forehand volley |
| 0:18 | Overhead smash |
| 0:21 | Run forward |
| 0:24 | Run right |
| 0:27 | Run left |
| 0:30 | Run backward |
| 0:33 | Perfect reaction |
| 0:36 | Whiff |
| 0:39 | Point celebration |
| 0:42 | Point loss |
| 0:45 | Match win |
| 0:48 | Match loss |
| 0:51 | Dive right |
| 0:54 | Dive left |
| 0:57 | Walk to position |
| 1:00 | Forehand while running |
| 1:03 | Backhand while running |
| 1:06 | Complete live point (13-shot rally) |

`active-clips.txt` is the inventory read from the running hero. Backward running uses the existing reversed forward cycle. Dives use the current swing + procedural dive/recovery. Slice, topspin, lob and low pickup do not have separate HeroTennisDriver slots: they route through the existing forehand/backhand stroke path (or overhead as selected by gameplay). Their different ball trajectories should not be mistaken for additional body clips.

Look for elbow/wrist flips, hand-to-handle separation, knees changing hinge direction, foot sliding, clipping during blends, and differences between the isolated chapters and the live point. No defects have been edited out. Silent capture; no claim of on-device rendering performance.
