# Plan 2C — CameraUX_PlayableSpectacle (2026-09-27)

**Status:** review candidate. Stopped for Adnan's approval. Next up is environment quality (not started).

**Rule, per Adnan:** cinematics can run up until the ball is hit. From then on, the normal play camera owns every live ball.

## Changes
Files: `Unity/Assets/Scripts/Tennis/TennisJuice.cs`, `Unity/Assets/Scripts/Tennis/TennisGame.cs`.

- **Serve**
  - Kept: the low cinematic toss cam (camera flip) on a good toss. It now holds until contact.
  - Removed: the perfect-serve contact cut and the ball-track shot.
  - At contact the camera snaps to the play cam. The perfect serve still gets its title, a light flash, an FOV punch and the FX and sting.
- **Ultimate (both sides): the cinematic moves BEFORE contact.**
  - When a full-meter hitter is 0.14 s from contact, the world freezes. The camera orbits them for 1.8 s, then cuts straight to the play cam.
  - The real contact then happens on the play cam, with a "SUPERNOVA!" title, a light flash and the hyper ball.
  - The rival's ultimate therefore cuts to your receive view before the rival hits.
  - No release POV any more, and no slow-mo on the live ball.
- **Other cuts**
  - Perfect cut: 0.4 s. Smash cut: 0.45 s, and its ball track is removed.
  - Any hit cut or toss cam is force-ended the moment the rival strikes.
  - Every shot hands back to the play cam on the same frame it ends.
  - Whiff crash-zoom and the reaction cams are kept (dead ball, dead time).
- **Contact flashes:** turned down (0.8 → 0.22 for the ultimate, 0.45 → 0.18 for the perfect serve) so the ball stays readable as it goes live.

## Proof (from a 3300-frame match)
| Moment | Frames |
|---|---|
| Serve | toss cam from f125 → play cam at contact f169 → flight readable (stills 08a–c) |
| Your ultimate | charge f921–975 (frozen) → play cam → real contact f977 → rival returns at f1006 (stills 09a–c) |
| Rival ultimate | charge f3167–3221 → your receive view → rival contact f3225 → you return it at f3256 (stills 10a–d) |

**Capture:** `ArtDir/anims/captures/Plan2C_CameraUX.mp4` (14.6 s).
