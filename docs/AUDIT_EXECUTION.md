# September 16 audit implementation

## Scope and baseline

Implemented against the reviewed `most-current-update` commit `735544439083430f5cd07c8c3b55ed20c960b1eb`, on local branch `fix/reliable-shot-control` in the `golf-simulator-calibration` checkout. The separately modified `main` checkout was not changed. The supplied attachment ends after F03; no later, unseen plan sections are claimed complete. This implementation therefore uses the audited branch as its base rather than the older `testing` branch described in CONTRIBUTING. Any eventual PR still needs the repository's integration/release policy.

## Implemented

| Audit | Changes | Regression coverage |
| --- | --- | --- |
| F01: player agency | `ShotRequest` separates chosen target heading/club/shot type from `SwingImpact` power, signed start line, explicit curve, strike, input source, and confidence. The target map, full-circle aim, and 0.25° putter adjustments permit recovery directions. Target/aim survive next-shot transitions. Camera motion retains its sign; no preset overswing or toe/heel hook is added. | Mirrored/left-handed traces; target and execution separation; recovery direction; persistent target and fine aim; explicit chip/pitch/full ranges. |
| F02: putting and short shots | Club-specific camera and phone putting thresholds. Fixed address reference accepts slow strokes. All launch maps are continuous from zero. Removed pin-distance power scaling and minimum release power. Added touch putting strength slider with fixed-gain feet estimate and explicit commit, plus pitch/chip ranges. | Gentle 8° and slow 30° strokes; small waggles rejected; phone putt; distances from 0.01–8 yd; zero-power driver/wedge/putter; fixed gain at different pin distances; tee-to-cup in two strokes. |
| F03: visible/scored contact | A held comfortable grip defines virtual reach in aspect-corrected shoulder-relative units. The virtual club state supplies camera endpoints, avatar endpoints, replay endpoints, swept contact, and feedback. Sweeps integrate inferred arm/shaft rotation plus observed grip translation between frames. Low confidence and missing impact samples cannot create contact. Whiffs count a stroke with zero movement. | Shared avatar endpoints; between-frame contact and near misses; frame rates 15/24/30/60; translated/scaled body; confidence and tracking gaps; whiff scoring/replay. |
| Timing/readiness | Aborted swings time out; readiness resets on movement/lost tracking; repeated pause is idempotent; flight skip tolerates date rounding. | Timeout/recovery, continuous stillness, radial misalignment, repeated pause, paused input, skip. |
| Cup consistency | Reduced capture to an explicit 0.12-yard arcade tolerance and a slow-speed threshold; resolve swept segments; rendered rest equals the logical cup. | Precise putt endpoint equality, short-of-cup rejection, successful slow capture. |
| Play flow | Compact camera by default; optional enlargement never blocks a swing; touch fallback available in play; shot planning cannot spend a stroke. | Camera → touch → target controls → putting UI, course touch loop, multiplayer turn presentation. |

## What the model does and does not measure

The camera measures 2D body landmarks, not a physical clubhead, club face, ball speed, or 3D swing path. The club is a deterministic virtual extension of a learned grip. Its signed projected tangent steers an arcade start-line estimate; a reversed projected stroke reverses direction. Depth remains unobserved. Draw/fade is chosen explicitly. Phone and touch use explicit center contact because those inputs cannot observe club-ball contact. No random dispersion or automatic pin-directed replacement is used.

Putting has one fixed user/club gain. The touch estimate is based on the current flat roll model; there is no hidden novice distance scaling. Pitch and chip are explicit lower speed ranges of the existing launch model, not a claim of measured wedge delivery or course slope simulation.

The original player identity/body scans, course-first scene, negative avatar heading transform, deterministic flight, local profiles, reactions, and multiplayer bystanders remain. Comfortable grip calibration is transient for the current setup; resetting it does not overwrite a player's saved body scan.

## Verification and remaining device work

Executed September 16, 2026: simulator build/run succeeded; full suite passed 98/98 (95 unit + 3 UI). After adding off-center-grip neutral-direction normalization and its regression, the final unit run passed 96/96. No UI code changed after the passing UI run. `git diff --check` is clean. The UI runner reported an internal QoS priority-inversion warning during touch play; device profiling is still needed, and this result does not establish runtime performance.

Build and run with the `GolfArcade` scheme. Run the scheme's unit and UI tests on iPhone 17 Pro / iOS 26.5. The regression tests compile and execute the actual gameplay code. UI tests preserve screenshots in the Xcode result bundle. The UI hole-loop test verifies controls, replay, scoring and eventual completion; it can use the stroke cap. A separate deterministic test explicitly verifies a two-stroke tee-to-cup completion without the cap.

Physical-device validation has not been completed by this implementation. Before release:

1. Test comfortable-grip setup, normal strokes, 8° putts, slow 30° putts, intentional waggles, aborted swings, and tracking loss on several supported iPhones. Include portrait/landscape, different distances, indoor lighting, and both handednesses. Log attempts, detected contacts, misses, false launches, tracking confidence, and response latency.
2. Repeat straight, visibly off-line, high/low, and deliberate miss strokes. Check the same clubhead/ball relationship in the camera, avatar, impact marker, and replay. Tune the virtual geometry/tolerance from recorded traces rather than altering scores to rescue shots.
3. With adult supervision, have a young beginner learn by watching a demonstration. Record whether they can set up, choose a club, make a short putt, and finish the practice hole without written instructions or repeated adult intervention. This usability criterion is not established by synthetic tests.
4. Test camera denial, interrupted drags, app interruptions, phone rotation, restarting holes, and multiplayer handoffs. Verify the selected player is tracked and bystander motion cannot spend their stroke.
5. Measure device frame rate, sustained performance, battery/thermal behavior, and haptic/camera interaction. Simulator success does not establish these outcomes.

Broader course expansion, animation/content polish, realistic slopes/spin measurement, and new multiplayer interactions are not part of the supplied F01–F03 implementation scope.
