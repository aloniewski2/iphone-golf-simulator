# Reference-led Unity golf grip

Reference: https://www.youtube.com/shorts/i1flY8dg5Kw (NyxOne9999). Inspected actual playback and paused browser frames of the 5.321-second clip. Initial page poster frames were not used as motion evidence. The reference shows a joined grip, extended lead arm, folding trail elbow, body turn and balanced finish. Single-view footage cannot establish exact 3D joint rotations or concealed finger placement; this is a stylized adaptation, not extracted motion capture.

## Runtime changes

`StandardGolfGrip` is installed by `GolferView` for both permanent standard golfers. Evaluation order is authored clip → shared golf grip → existing sport-independent `StandardCharacterArms`.

- Fixed gripping hands rotate around the shaft so the wrists approach from the body side, instead of opposite radial directions. The original curled-finger geometry is retained.
- Both hands and the club move as one assembly. The lead wrist is given more reach through the backswing/delivery, within both arm reach limits; the finish allows folding.
- At address and impact the assembly pivots about the club-head contact. This retains the ball-contact position without lifting the character to compensate for a lowered club head.
- The golf FBXs are rebuilt with hemisphere-consistent quaternion keys and linear club-key interpolation before sub-frame export. This addresses the imported follow-through club jump exposed by the new 120 Hz continuity test.
- Male/female identities, shared arm lengths, source Blender masters, physical phone input and other sports are unchanged.

The current game uses the right-handed driver clip. This correction is golf-specific, not a universal two-hand grip for tennis or boxing. Left-handed golf, separately articulated fingers, collision-aware shoulder/clothing deformation and alternate clubs require further work. The original clip still supplies hips, torso, feet and swing timing.

## Validation

The EditMode test samples both actual golf FBXs at 120 Hz for hand-to-handle stability, finite continuous arms, segment lengths, club continuity and preserved address/impact contact. PlayMode runs both characters through the real synthetic-phone-driven golf game and captures 60 fps gameplay plus closer alternate-angle diagnostic frames. Offline sampling is not an iPhone performance benchmark.

Unity 6000.3.24f1: all 54 EditMode tests passed, including both new 361-pose golf checks and the six existing other-sport arm fixtures. The continuity test previously caught a roughly 40 cm club-contact movement in one 1/120-second step during the imported follow-through; correcting the sparse source quaternion keys before sampling resolved that failure.

All 3 PlayMode tests also passed. See [gameplay recordings and closer grip views](Previews/UnityGrip/README.md). The closer views expose the grip and elbow shape; shoulder/sleeve seams and the fixed-finger mesh remain stylized rather than production anatomical detail.
