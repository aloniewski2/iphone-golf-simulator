# Architecture

The gameplay pipeline is intentionally layered:

`Camera frame -> pose candidates -> calibrated player match -> swing state and metrics -> mode forgiveness -> impact estimate -> shot result -> ball flight`

- **Capture** owns AVFoundation input and orientation.
- **Pose** converts every Vision observation into normalized, confidence-scored body landmarks.
- **Calibration** records stable front-facing proportions and the player's expected screen position, then ranks each frame's candidates and rejects poses that do not match.
- **Swing** consumes a time-ordered landmark stream and emits phases and observed metrics.
- **Shot** estimates golf metrics and computes a deterministic outcome.
- **Game** coordinates a session, club selection, scoring, and presentation.
- **Range** renders the resulting trajectory.

Domain models avoid Apple framework types where practical so the core engine can be unit tested without a camera. Every derived measurement carries or contributes to a confidence score. The saved calibration is local to the device and can be replaced from range settings. Arcade Mode can compensate when confidence is low; future Simulation Mode can warn when the current match degrades using the same inputs.
