# Architecture

The gameplay pipeline is intentionally layered:

`Camera frame -> player match -> aspect-corrected body space -> virtual club + swept contact -> SwingImpact + chosen target -> ShotRequest -> deterministic flight -> course resolution`

- **Capture** owns AVFoundation input and orientation.
- **Pose** converts every Vision observation into normalized, confidence-scored body landmarks.
- **Calibration** records stable front-facing proportions and the player's expected screen position, then ranks each frame's candidates and rejects poses that do not match.
- **Swing** consumes a time-ordered landmark stream. A comfortable held grip sets a stable body-relative address. `VirtualClubState` owns the grip, head, ball, and confidence used by contact, overlay, avatar, and recorded replay. Full, short-game, and putting thresholds are distinct. Missing impact frames are never bridged by a guessed strike.
- **Shot** separates intended target heading from execution (`SwingImpact`: power, signed start line, explicit curve, contact, source, confidence). `ShotRequest` includes the selected club and shot type. Power maps continuously from zero, whiffs have zero speed, and putt gain does not depend on the pin. `CourseRound` passes intent/execution unchanged into the deterministic model.
- **Game** coordinates a session, club selection, scoring, and presentation.
- **Range** renders the resulting trajectory.

Core algorithms are tested without a camera. Saved player scans remain local; comfortable grip calibration lasts for the current setup and can be reset from course settings. A confidence floor rejects unreliable samples; the accepted contact tolerance widens modestly with uncertainty. Camera start line is an explicitly limited 2D arcade estimate, and draw/fade is an explicit player choice. No random dispersion or pin-directed replacement shot is added.
