# Tennis rally prototype and product standards

## Required gameplay standards

| User standard | Initial implementation |
| --- | --- |
| Swing timing matters | Contact window around the stroke's sweet time; early/late hits lose quality, outside-window swings miss. |
| Ball near racket center is better | Relative ball/racket sweep intersects the actual animated string-bed plane; elliptical distance from its exported sweet-spot marker grades center contact. |
| Position affects power and accuracy | Running balance and reach quality contribute to output speed and directional error. Moving into position matters; there is no automatic remote hit. |
| Live hit-quality feedback | Immediate timing, center and positioning grades; shot speed; outcome label; ball color response; result text sent over the existing phone ACK channel. |
| Stamina depends on lateral speed | Drain is proportional to normalized speed squared; rest recovers it. Low stamina limits run speed and reduces shot performance. |
| Swing power controls ball speed | Gyro angular-speed input scales power; keyboard hold/release supplies an explicit test proxy. Quality/stamina modify, rather than replace, that power. |

These are initial tunable game-design values, not measured tennis biomechanics. Physics uses metres and a 120 Hz fixed step. Tests distinguish correct center hits, off-center hits, misses, timing penalties, positioning penalties, swing-power differences and speed-dependent stamina drain.

## Open and play

Unity 6000.3.24f1: open `Assets/Scenes/Tennis.unity`, or menu **Golf Arcade → Tennis → Open Rally Lab**, then Play. Press **T** during golf to open tennis; **G** returns to golf. Golf remains the first build scene.

- A/D: lateral movement. Shift: sprint.
- Hold/release Space: keyboard swing-power proxy. Release early enough for the racket to meet the ball during the stroke.
- Left/right arrows: aim. R: new feed. F1: switch permanent male/female player.
- Phone: existing LAN controller or local gyro. C calibrates neutral tilt. Tilt or existing left/right controller buttons steer; angular speed triggers a stroke and its power. Tilt steering pauses during the stroke to avoid treating swing rotation as a step.

**Physical-position tracking is not implemented.** The existing protocol provides attitude, gyro speed and buttons, not world-space phone position. `SetLateralInput` is the integration boundary for a future tracked/calibrated movement source. The keyboard and tilt controls are honest prototype controls, not inertial position tracking. No new phone UI or protocol version has been shipped.

## Assets and scope

Uses the existing `coastal-tennis-resort-v1.blend`, not a replacement court. The export groups its visible geometry into spatial tiles and converts the visual materials to the game's supported shader. Procedural material detail is not fully baked. Source Blender files remain unchanged. `blender/scripts/export_tennis_runtime.py` regenerates the resort and tennis character exports. Racket contact markers identify the sweet spot and string-bed basis independently of FBX axis conversion.

Both permanent identities use floating hands with no visible shoulders/arms. Existing rigs still animate torso, legs and hands. The player has ready, lateral-running, forehand and backhand clips. The racket does not automatically switch hands on a backhand. Character skin uses the existing style palette.

This is a **singles rally lab**, not a finished tennis game: the far character is a training feeder, not competitive AI. Serving, scoring/deuce/tiebreaks, lets, doubles, advanced spin, left-handed selection, authoritative multiplayer, audio/haptics, animation transition polish, accessibility controls and device profiling remain future work. Net and ground checks use analytic collision planes; full swept angular racket collision and exact sub-step bounce integration remain improvements. The first resort export is not a mobile-optimized art build: foliage instancing, LOD, material baking and draw-call/memory profiling are still needed.

## Verification

EditMode checks the six required gameplay behaviors. PlayMode loads the actual arena and both standard characters, exercises movement/stamina and injects controlled contact fixtures through the real racket collision path. Review captures from those tests are deterministic keyboard/controller-boundary simulations, not recordings of a physical phone session.

Verified in Unity 6000.3.24f1: **60 EditMode and 4 PlayMode tests passed**. Both tennis identities individually returned a controlled racket-center shot. Golf gameplay and the other-sport standard-rig checks still pass. [Review clips](Previews/UnityTennis/README.md) are 6-second, 360-frame, 60 fps offline captures, not device performance benchmarks.
