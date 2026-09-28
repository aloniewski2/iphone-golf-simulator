# V4 with the existing tennis gameplay motion

Preview requested by Adnan, 2026-09-26. The locked V4 mesh, wardrobe, materials and lights are retained.

## Video

`V4_existing_tennis_review.mp4`: 30 fps, ready stance; jump serve; forehand; backhand; forward run; right run; left run; forehand volley; overhead smash. Swings first play at the existing gameplay speed, followed by explicitly labelled half-speed replays. Running is held at screen center for review. `chapters.json` contains chapter times.

## Actual source

- `Unity/Assets/Resources/StandardCharacters/standard_male_tennis.fbx` and its imported `Ready_RH`, `Serve_RH`, `Forehand_RH`, `Backhand_RH`, `VolleyForehand_RH`, `Smash_RH` clips.
- Existing `TennisActor.Build/Advance/Pose` evaluates those clips with its gameplay timing, style, procedural gait, arm counter-swing, foot planting and racket alignment. This captures running as it is implemented; the unused RunLeft/RunRight authored clips are not substituted for the procedural gait.
- Source uses the standard male/right-handed actor, default body and Player motion style, no live ball guidance. Serve power .9; groundstrokes/volley .8; smash .9. Running speeds: forward 5 m/s, lateral ±4.5 m/s.

## Preview transfer

Editor-only `HeroLegacyReview.cs` runs a hidden legacy actor and transfers rest-relative torso rotations, anatomical arm/leg segment directions and vertical motion onto the existing V4 bones, retaining V4 bone lengths. Bind matrices are read from the old skin to avoid mistaking the FBX's initial animated pose for rest. Right-hand racket retains its socket and follows the runtime string-bed orientation.

Preview renders the unchanged V4 mesh using its existing four-influence skin matrices in an editor-only CPU skinning copy; this avoids relying on a disabled Humanoid Animator to refresh manually driven skinning. It is a review capture, **not** a newly imported Humanoid clip pack or a live campaign replacement. Original clips, the owned mocap set, production prefab and game logic are not replaced.

## Checks and limitations

Capture completed with shared wardrobe bone checks; vertical hip movement in jump serve is recorded in `../../screenshots/v4_legacy/capture_report.txt` (about 0.53 m). Looked at ready, jump apex, swing follow-through and running samples. The old carry does not fit V4 perfectly: ready hand/torso/racket overlap and fixed open fingers remain visible. No new grip, mesh or animation polish is claimed.

Raw capture: `../../screenshots/v4_legacy/frame_*.png`. Reproduce through `GolfArcade.EditorTools.HeroLegacyReview.Run`, then run `package_review.py` with Pillow, numpy and imageio-ffmpeg.
