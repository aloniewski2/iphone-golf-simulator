# Standard characters in golf gameplay

The golf game now loads `standard_male_golf.fbx` and `standard_female_golf.fbx` from `Unity/Assets/Resources/StandardCharacters`. These are exported from the actual permanent V4 Blender character rigs with Golf Kit 1 and the held driver; they are not the alternate golfers from the earlier testing branch.

The existing male/female selector and skin-tone controls use these models. The existing phone/network/synthetic swing controller still drives the game. Backswing load scrubs the V4 drive, and the existing strike/ball-flight pipeline uses its updated impact time. Current integration is right-handed and driver-animation based, matching the initial game's single-swing workflow. The other V4 clips, sports, kits, and LH controls are not yet wired into gameplay.

## Implementation

- Generic FBX rigs; explicit `StandardGolfDrive` clip selection instead of first-clip selection.
- V4 source frames 1–91 at 30 fps; export samples every half frame (60 samples/second), duration 3 seconds.
- Top at 1.77 seconds; impact at 2.4 seconds; finish at 3 seconds.
- Source stance rotation compensated on the visual model; source meters converted to game yards once.
- An exported `StandardClubContact` marker aligns the authored impact position to the game ball.
- Both models retain separate garment meshes and imported color regions. Skin materials use the existing skin-tone selector.
- Closer third-person address framing; the existing flight camera and game rules remain intact.
- Original V4 Blender masters and original supplied GLBs remain unchanged. Earlier runtime golfer FBXs remain in the repository as historical assets, not selected by default.

## Re-export

From the repository root, run Blender in background mode with `--python blender/scripts/export_standard_golf.py`. This reads `SportsLibrary/Blender/sports-animation-studio-v4.blend` and exports the two runtime FBXs without saving changes to the source master. Retrieve LFS files first. Unity importer settings live in `GolferModelImporter.cs`.

## Review and limitations

`StandardCharacterGameplayTests` loads the actual Golf scene, selects each standard, verifies a skinned model and contact marker, sends synthetic input through the game's normal swing controller, asserts a ball-flight transition, and captures 60 fps frame sequences under `Unity/Library/Captures/standard-male` and `standard-female`.

These captures show actual Unity gameplay driven by simulated phone input, not physical-iPhone measurements. Offline deterministic frame capture is not a real-time performance benchmark. Rig/cloth polish and partial-swing transitions still need further animation review. A drive is not an appropriate final animation for every club: putter/iron/wedge clip switching remains future work.

This document supersedes the earlier source-only status for these two golf models and their drive clip only. It does not claim the entire sports animation library is integrated.
