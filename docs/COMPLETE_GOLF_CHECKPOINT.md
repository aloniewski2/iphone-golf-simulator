# Complete Golf Experience — implementation checkpoint

2026-09-17. **Work in progress; not completion of the supplied release plan.**

## Implemented in this checkpoint

- Seven-club bag with solver-calibrated full-shot references. Legacy `iron` and `wedge` identifiers remain 7-iron and sand wedge.
- Explicit straight/draw/fade and low/normal/high choices, normalized for incompatible shot types. Chosen shape reverses for left-handed golfers; it is not reported as measured physical club-face angle.
- Wind snapshots in shot requests and air-relative aerodynamic flight. Preview and release use the same request/solver. The new surface model is identified as simulation version 3.
- `Sunward Resort · Nine`: original par-36 nine-hole layout; the three-hole `sunward` identity remains a separate legacy course with separate score keys.
- Polygon fairways/greens and a shared surface query for height, slope and lie. The renderer samples that query, including recessed bunker bowls; new-course flight bounces against surface normals and rolls with lie-specific resistance. Fringe and deep rough are distinct lies.
- Background bounded strength search for the new course; complete immutable planning conditions discard stale results. Suggested strength freezes while charging. Existing legacy/putting planning still needs broader background-worker migration and device profiling.
- Practice/play control. Practice stores strength feedback but cannot release a scored shot or move the round ball.
- Shorter follow-through camera beat, landing precedence, damped camera transitions, 60 fps presentation target, Reduced Motion handling for camera/effect/reaction/green-read motion.
- Predicted flight is mint; predicted roll is orange; the recommended map route remains yellow. Physical scene-ball radius is unchanged by the locator halo.
- Shared timestamped `GolferAnimationState` for the existing phone/TV scene, per-joint presentation provenance, a bounded recording queue, brief visual occlusion hold and observed foot-lift allowance. The visual hold is not submitted to contact detection.
- Offline CC0 Standard-pack import: 65-bone skinned golfer, original golf palette/accessories, anatomical left-handed pose mapping, shared-grip arm constraints and authored finger-curl fallback. License text, source record, checksums and verification script are included.

## Character gate: NOT PASSED

The imported golfer is a **development comparison**, not the default. Debug Settings → Development comparison → Preview imported golfer, then open a new course view. Active rounds are not switched in place.

Offline SceneKit inspection found oversized cap, left-handed axial-twist and open-hand problems; corresponding corrections were implemented. Clothing boundaries, shoulder deformation, silhouette, head/face finish and every swing-family review still require work. The source free pack offers superhero topology; it is not a finished golf character or an authored golf animation library. Do not advertise four finished character presets.

The offline asset inspection used a mechanically adapted macOS SceneKit renderer, **not an iPhone gameplay test or simulator substitute for tracking validation**.

## Evidence

- Physical-iPhone-target Debug app and XCTest/UI-test bundles: build succeeded.
- Physical-iPhone-target Release app: build succeeded; Apple Development signature and sealed resources verified.
- `zsh scripts/check-core.sh`: **2,168 host-side production-logic assertions passed**, plus asset provenance/rig validation.
- Synthetic four-player nine-hole progression: **124 shot inputs, zero capped hole-turns**. Every player completed nine score entries. Replaying each shot did not spend another stroke. This uses ideal synthetic inputs, not four people or camera tracking.
- New test coverage includes course identity/par, shared bunker floor, wind determinism, seven-club reference carry, handedness, shape/loft, practice no-score, preview/release consistency, short-shot camera behavior and imported-rig screenshots.
- Existing three-hole-only and immediate-club-disappearance fixtures were explicitly updated for the new layout and 180 ms visual-hold contract.
- Source whitespace check passed. The supplied vendor license is deliberately retained byte-for-byte (including its original CRLF/trailing spaces) and excluded from that formatting check.

**Not run in this turn:** complete physical-device XCTest execution, the previously interrupted UI walkthrough, a human swing, a physical nine-hole round, live TV reconnect/latency tests or the 30-minute performance run. Test-bundle compilation is not test execution. Prior-turn device results do not certify this checkpoint.

No source-camera recording or upload was performed. No paid assets were purchased. No runtime cloud inference was introduced.

## Remaining required work, in order

1. Finish the single-golfer deformation/wardrobe proof, validate it on the actual iPhone in both handednesses, then approve its default promotion. Add the complete authored golf stance/phase/short-medium-full library and four selectable presets only after that gate.
2. Finish the showcase hole's authored art: refined continuous boundaries, bunker lips, water/shorelines, paths/landmarks, vegetation LOD and lighting. Current sampled polygon terrain is a foundation, not final resort art. Validate mesh/lie edge tolerances.
3. Complete swept obstacle interaction including shared tree-trunk collision and robust bunker-lip cases; pace-sensitive cup behavior with deterministic lip-outs. Current cup/recovery logic remains a simpler existing model.
4. Finish coordinate-space/session identity and tracking/impact provenance contracts, animation/replay versioned persistence and complete condition-keyed asynchronous planning. Measure prediction/render cost on the phone; no 60 fps claim yet.
5. Finish full motion-family animation, character customization, audio/ambience, skippable introductions and phone/TV HUD visual review. Re-test source changes, replays, pauses and reconnects for duplicate scoring.
6. Qualify all nine holes and actual 2–4-player rounds, accessibility, storage migration and interruptions. Run the stated 20-person / 1,200-swing, 30-hour stillness/non-swing and external latency gates. Report samples and uncertainty, not inferred success.

## Reproduce / device handoff

Use Xcode 27 with the existing iOS 17 deployment floor. Generate the project with `xcodegen generate`.

```
zsh scripts/check-core.sh
xcodebuild build-for-testing -project GolfArcade.xcodeproj -scheme GolfArcade \
  -configuration Debug -destination 'generic/platform=iOS' \
  -derivedDataPath /private/tmp/golf-complete-device
```

Prepared Release app: `/private/tmp/golf-complete-release/Build/Products/Release-iphoneos/GolfArcade.app`.
Prepared Debug app/tests: `/private/tmp/golf-complete-device/Build/Products/`.

Next physical checks: run all `GolfArcadeTests`, then `RangePlayTests/testResortNineHoleAndImportedGolferComparison` and the existing Sunward/TV setup tests on the **physical iPhone**. Keep the phone in the app during these checks. Source-camera capture stays off unless separately opted in.

## Reference and source boundary

Reference: https://www.youtube.com/watch?v=bnpeDsCLeJo. Prior review used the full transcript and sampled decoded frames, not uninterrupted viewing. Observed: readable oblique setup, map/wind/power HUD, flight/landing cameras, compact putting and distinct terrain. Those observations informed an original implementation; they do not establish Nintendo's internal algorithms. No video or Nintendo assets are bundled.

Free geometry: https://quaternius.com/packs/universalbasecharacters.html and https://quaternius.itch.io/universal-base-characters. Only Standard/free contents were imported; see `GolfArcade/Resources/Golfer/README.md` and supplied CC0 license.

The SwiftUI UI-patterns skill guided compact controls and stable shared round ownership rather than per-joint UI state. The existing local TV/startup and earlier Sunward changes are preserved as the integration baseline.
