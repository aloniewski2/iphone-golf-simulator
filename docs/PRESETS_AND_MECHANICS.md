# Presets and golf mechanics checkpoint

September 18, 2026. Follow-up to `COMPLETE_GOLF_CHECKPOINT.md`, not full release qualification.

## Implemented

- Four selectable, saved cosmetic presets: Cove, Dune, Orchard and Sunset. Skin tone, hair color, outfit color and cap/visor are customizable. Player setup → Golfer opens a live SceneKit preview. Cancel discards edits; Save updates the existing player roster. Old rosters without appearance decode with a stable color-index fallback. Cosmetics do not affect handedness, tracking or shot physics.
- Both existing and imported development rigs consume the same appearance; the shared phone/TV course scene and spectators use each player's saved selection. The default remains the continuous procedural body. These are four looks on one rig, not four separately sculpted models.
- Simulation version 4 on the nine-hole resort: swept tree-trunk collisions, shared bunker floor/lip geometry, distinct chip/pitch/bunker launch profiles, deterministic pace/edge-sensitive cup entry and lip-outs, immediate water-ground stopping and validated safe drops. OB returns to the previous spot with a penalty. Wedge selection in sand defaults to the bunker shot type.
- Contact classification influences launch/spin/start line; result feedback names supported causes or course interactions. This does not claim a measured physical club face.
- Prediction and actual play use the same versioned solver. Route strength search penalizes trunk/lip strikes. Version 4 score keys keep earlier best scores separate without deleting them. Legacy courses and recorded paths are preserved.

## Verification

- `zsh scripts/check-core.sh`: 2,217 host production-logic checks pass; free asset provenance hashes, 65-bone mesh and normalized weights pass.
- Synthetic four-player nine-hole progression: 124 solver-driven shots, zero capped hole-turns; replay did not add strokes. This is not four-person motion validation.
- Checked short/long putts over 27 distance/cross-slope/rise combinations, low versus lofted bunker escape, trunk tunneling/height exclusion, deterministic cup/rim behavior, water/OB recovery, preview/release agreement and legacy player decoding.
- Debug physical-iPhone-target app plus XCTest/UI-test bundles compile successfully. New tests cover saved appearance, UI selection/save/reopen, mechanics and replay. **Compilation is not XCTest execution.**
- Release physical-iPhone-target build succeeds; Apple Development signature and sealed resources verified. Prepared app: `/private/tmp/golf-presets-release/Build/Products/Release-iphoneos/GolfArcade.app`. Debug test products: `/private/tmp/golf-presets-mechanics/Build/Products/`.
- Offline SceneKit render review of four presets on both rigs, in a right-handed address pose. Not a phone screenshot or full-motion deformation review.
- No phone control, installation, simulator testing, source-camera recording or upload this turn: the user requested build preparation only.

## Remaining gates and limitations

- Imported rig promotion remains blocked on clothing/head finish, deformation and complete animation-family review. Preset selection is available on the established default while this proof continues.
- Trunk collision uses finite-height cylindrical sides. Crowns/distant scenery are decorative; foliage collision is not simulated. Bunker art samples the continuous surface and still needs edge/lip visual review on device.
- Cup/material/contact models are deterministic, tunable game approximations, not validated real-world golf measurements.
- Putting/legacy planning still requires broader off-main-worker migration and device profiling. No 60 fps, thermal or latency certification is claimed.
- Physical XCTest/UI walkthrough, both-handed full swings, TV reconnect, real solo/party rounds and release cohort/latency/performance gates remain pending. No tracking algorithm was certified by host tests.
- Full authored animation library, additional character sculpting, environmental art and remaining audiovisual presentation are not completed by this checkpoint.

## Later device test

Run `GolfArcadeTests`, then `RangePlayTests/testChooseSaveAndReopenGolferPreset`, the imported comparison and TV setup tests on the actual iPhone. Check all preset looks during idle, backswing, impact and follow-through in both handednesses. Complete a resort round and test sand, rough, water, OB, tree strikes and short/long sloped putts. Source-camera recording remains separately opt-in.

The SwiftUI UI-patterns skill guided a local, cancellable editor sheet with native pickers and saved roster ownership; the preview rebuilds only when appearance/handedness changes, not on tracking frames.
