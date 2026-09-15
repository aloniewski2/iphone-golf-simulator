# Architecture

The gameplay pipeline is intentionally layered:

`Camera frame -> observed pose -> swing state and metrics -> mode forgiveness -> impact estimate -> shot result -> ball flight`

- **Capture** owns AVFoundation input and orientation.
- **Pose** converts Vision observations into normalized, confidence-scored body landmarks.
- **Swing** consumes a time-ordered landmark stream and emits phases and observed metrics.
- **Shot** estimates golf metrics and computes a deterministic outcome.
- **Game** coordinates a session, club selection, scoring, and presentation.
- **Range** renders the resulting trajectory.

Domain models avoid Apple framework types where practical so the core engine can be unit tested without a camera. Every derived measurement carries or contributes to a confidence score. Arcade Mode can compensate when confidence is low; future Simulation Mode can warn or require recalibration using the same inputs.

