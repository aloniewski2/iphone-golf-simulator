# RealityKit rebuild: implementation checkpoint

This is an **incomplete rebuild**, not the full-game handoff. The standard `GolfArcade`
scheme now runs, profiles and archives the native `Release` product. Release excludes nine
SceneKit renderer source files and requires no renderer flag. `GolfArcadeRelease` tests that
shipping configuration through ordinary menu/solo/multiplayer/practice navigation.

The older renderer remains development-only in `Debug` and the optimized `Comparison`
configuration (`GolfArcadeVisualAudit`); `-realityKit` still selects native rendering in those
comparison builds. The main scheme's regression tests explicitly use Debug so existing
comparison/physics fixtures remain available. `GolfArcadeNativeOnly` is retained as an alias
configuration for the earlier measurements. This cutover does not certify outstanding
visual, physical-swing, real-display or sustained-performance acceptance.

## Implemented

Current user priority: this is a Wii Sports-style arcade game, not a precision golf simulator.
Prioritize readable visuals, responsive and forgiving swings, quick rounds, multiplayer flow,
and completing the native replacement. Preserve existing scoring/shot behavior. Fine-grained
contact tuning is deferred, not silently certified or removed from the remaining-work record.

User-requested arcade ball: `GolfBallVisual` gives both renderers a 6.5 cm presentation radius
(about 3× the previous diameter). Native center clearance and comparison tee/resting offsets
keep the enlarged sphere above the authored surface. The solver's 2.135 cm ball radius and
5.4 cm cup radius are unchanged. Trails retain the authoritative path, independent of this
visual offset. No collider or second physics authority was added.

- User-directed course-menu restriction: only Sunward Resort · Nine is selectable for solo
  and multiplayer. Other definitions/assets remain for practice and migration comparisons;
  no map files were deleted. The native solo/multiplayer selection-and-load UI regression
  passed at 00:58:21 UTC; comparison-renderer resort navigation also passed at 00:56:11.

- Swift 6 / iOS 26 minimum in the generated project and project.yml.
- Live phone motion now uses one CoreMotion manager and a serial user-interactive operation
  queue, 100 Hz requested sampling, fused orientation, acceleration converted to m/s², and
  timestamp-aware 12 Hz low-pass filtering. Synthetic replay covers 60 and 100 Hz.
- A Ready tap freezes club, handedness, sensitivity and aim. The new recognizer separates
  reversal from impact and requires a swept crossing through address. Deceleration or elapsed
  time never generates an impact. Follow-through runs for up to four seconds without rearming.
- Bounded raw capture (1,000 samples), bounded event handoff (16 events), generation fencing,
  explicit overflow cancellation, at-most-30-Hz continuous status, and unthrottled discrete
  events. No asynchronous task is created per sensor sample.
- Phone directional assistance populates SwingImpact.startLineDegrees, capped at 8 degrees
  for full shots and 2 degrees for putts, and retains the existing speed/sensitivity calibration.
- A MainActor Observable GameSession, single persistent non-AR ARView, DisplayCoordinator,
  UIViewController reparenting, and native ControllerView. External scene windows do not become
  key. The application delegate preserves SwiftUI ownership and the existing iOS 27 accessory
  availability guard.
- Ordered input, gameplay, ball, golfer, camera and effects Systems. The native gameplay clock
  uses monotonic elapsed time and does not use the comparison screen's 30 Hz SwiftUI timer.
- Immutable ShotPreparation values construct RangeShot on a detached worker. Generation and
  course/hole/player/origin checks discard stale results. Accepted shots re-enter CourseRound's
  scoring, replay and turn progression; no second ball physics engine is introduced.
- Explicit yards-to-metres / Y-up / -Z down-course boundary. Async USDZ loading caches current
  and next holes, validates tee/pin markers against the terrain, checks skeleton joint names
  and required embedded clips, and rejects missing assets visibly.
- All 21 existing holes are converted and packaged as metres-based USDZ, including authored
  vegetation and clubhouse props. Runtime loading and tee/pin alignment tests passed for all
  21. Editable exports and provenance are in art/Native; no new assets were bought/generated.
- A single 24-joint skinned golfer contains 14 authored clips, with cap/visor geometry, tint
  roles, explicit impact markers and hand/foot contact curves. A 112-pose audit (14 clips,
  both handednesses, four times) passed solved hand position/orientation and foot-height limits
  with the corrected club. A rigid shaft-tip joint and blended shaft/grip skin weights avoid
  IK discarding longitudinal scale. Both FK and final solved shaft/head endpoint checks pass.
- The appearance editor now renders the actual RealityKit golfer. Course cards use 21 actual
  RealityKit renders. Visual inspection caught aliased terrain materials and missing accessory
  tint labels; exports now split material regions explicitly and preserve semantic tint roles.
- Native shot planning, a four-player scorecard sheet, green-read text, settings and a 12-second
  flyover are implemented. Immutable, cancellable worker predictions feed the map and 3D guide.
  Prediction parity/stale-result tests, shot/replay, target planning, four-player scorecard,
  flyover and native TV settings/comparison-flash regressions pass.
- Native practice records bounded feedback directly without entering the legacy charge/release
  solver path. Repeated swings leave authoritative and rendered ball transforms, strokes and
  scores unchanged; invalid power/confidence is rejected. Practice and missed strikes no longer
  produce the cosmetic ball pulse. The practice menu retains its original Meadow training venue.
- Native automatic-putt line/pace searches now run inside the preview worker, not from
  `combinedAim`/renderer reads. The round accepts validated, origin/hole-matched cache results;
  input mode/readiness, preview revision and round generation reject obsolete publication.
  Solver loops cooperate with cancellation. Swing waits for a matching plan, while club/target
  editing remains available. Publication leaves the task key stable, and accepted phone input
  retains its frozen heading even if a later planning task or direct domain edit occurs.
- Native controls now show lie, wind relative to selected aim, phone-swing safety guidance and
  fairway targeting. Shape/trajectory availability matches the existing shot rules. Landed-shot
  lie/distance/contact/penalty feedback, hole score, round totals/winner and best score use the
  authoritative round rather than a second scoring model. Practice-to-scored-play, ordinary
  shot/replay, resort putt/hole-result and next-hole UI flows pass.
- Native round completion has a scrollable totals/scorecard area, a course-summary header and
  pinned Menu/Play Again actions. Solo best, four-player winner and tie fixtures pass through
  a real final stroke, final scores and restart in the UI. Portrait and landscape captures
  were reviewed. Fixture prior scores and defaults are isolated from user records; these UI
  checks do not claim nine holes were played through screen gestures.
- Full nine-hole solo/four-player prepared-shot scoring tests cover turn order, stroke caps,
  replay isolation, 81-stroke/+45 totals, persisted/reloaded solo best, improvement and restart.
  Multiplayer preserves an existing solo record. Final completion now keeps the scored ball
  and its locator, pin beacon and green-reading overlay hidden until gameplay restarts.
- Waiting players share the skinned model resources with individual appearance, idle and
  recovery playback, terrain foot correction, and original cosmetic club-contact capsules.
  Four-player roster/turn changes, mirrored placement, score isolation and teardown pass.
  End-to-end animated contact/recovery visual verification remains outstanding.
- Owned driver/wood, iron/wedge and putter head silhouettes are embedded in the same skinned
  asset; selection changes material visibility without replacing the skeleton or grip.
  Tests cover every club's visibility and hide all club materials for waiting players.
- The phone keeps the viewport and shot action pinned while controls scroll. A stable
  adaptive layout places them side-by-side in landscape. UI tests verify viewport geometry,
  control reachability, actual rotation and preserved turn state. Native display preferences
  now handle string-backed Boolean values consistently with UserDefaults.bool(forKey:).
- Native cameras now reuse the shot-camera director. Landing/cup/result audio is driven by
  the authoritative trajectory timeline; tests check one-shot delivery and explicit replay reset.
- Native cosmetic effects retain the comparison renderer's bounded 18-piece turf/sand burst,
  60-point flight trail, resting-ball ring and distance-faded hole beacon. Burst paths/fades,
  trajectory positions, camera/ball exclusion distances and replay pool reuse are tested.
  The scored ball now disappears on holing out and returns during replay, with the same
  completion tolerance as CourseRound. None of these entities has scored physics authority.
- Decorative trees and the pin flag use component-backed native motion. The monotonic session
  clock freezes it when paused; Reduce Motion restores the authored rest transforms. Rebinding
  and teardown remove components and references without modifying collision metadata.
- A single-mesh native Metal green-read overlay uses the shared renderer-independent surface
  samples. GPU motion preserves the existing quadratic terrain-following path, speed and slope
  colors. All 21 holes pass downhill/green containment and 1 cm surface-height checks. Runtime
  captures and a pixel-level regression verify moving colored dots rather than black geometry.
  Shader time is supplied by the session; Reduced Motion disables travel without hiding the read.
- Native water shading restores world-anchored wave gradients and shallow/deep color using
  the authoritative lake outlines. A bounded shoreline texture is prepared on a cancellable
  worker and cached with the course; per-frame work updates only the session-time uniform.
  Both ordinary and resort lagoon materials are covered. The scored water plane never moves.
  Resort water now also consumes the six existing hole-local reflection captures using worker
  atlas assembly, native custom uniforms and box-projected sampling. Reflected lighting affects
  water only, with no dynamic capture/global environment change. A rendered comparison verifies
  visible water changes and unchanged sky/terrain. Lake-by-lake review and any re-baking needed
  for final converted scenery/lighting remain part of visual acceptance.
- Native grass uses the existing USDZ blades and albedo, restoring breeze, distance retirement,
  slope-based lighting and clearance around the authoritative ball. Exported V encodes flipped
  blade height; the shader decodes it and supplies a stable normal basis. Runtime close-ups
  caught and corrected black backfaces. Reduced Motion freezes breeze while retaining clearance.
- Explicit viewport teardown removes scene/IK bindings. Six repeated stop/restart cycles and
  the complete touch-shot/replay UI flow pass on iOS 26.5.
- Release-compatible signposts for sensor callbacks, filtering, impact acceptance and trajectory
  construction. Bounded session counters record update intervals and callback-to-impact
  application latency. Update interval statistics are not GPU-presented frame measurements.

## Remaining implementation and validation

The 12 pre-resort holes now use the existing terrain-conforming playable-surface exporter
instead of flat hazard overlays. All 12 were re-exported and packaged, all 21 runtime assets
loaded successfully, and all 21 previews were regenerated. Cliff hole 3 and representative
Meadow/Cliff/resort cards were visually checked; full per-hole visual review remains. The standard
SceneKit comparison remains unchanged because this path is enabled only by offline export.

The former missing-USDZ gate is resolved using the owned source assets. The native game is now
playable through touch input. This is not yet full visual/gameplay parity. In particular, the
corrected course packages total about 1.35 GB and still need content/performance optimization. Material
conversion removes invalid exported default emission/normals and adds native shadows, but
final reflection/scenery visual acceptance, broader foliage coverage and all-course visual review remain unfinished.
The native water/grass shaders now exist, but their physical-device cost is not yet measured.

Still required before the full handoff:

1. Finish native material/effect parity and verify visual/physics alignment across each hole.
2. Extend sampled-pose coverage to terrain/club variants and finish
   appearance/headwear visual review. The USD skeleton uses a bent address rest pose separate
   from its T-pose skin bind to avoid singular straight-limb IK initialization.
3. Complete animated bystander contact/recovery verification, all camera modes, course cards,
   trajectory previews, target-map interactions and remaining practice/presentation parity.
4. Expand full-playthrough verification beyond the tested prepared-round and UI flows.
   Shipping Release now excludes SceneKit rendering sources and direct framework linkage;
   retain Debug/Comparison only as development evidence while acceptance is completed.
5. Validate real external-display scene callbacks and iOS 26 hardware, not only host-controller
   reparenting tests. Run the required 20-minute device/display sessions and measure AirPlay
   transport delay separately.
6. Capture GPU frame pacing, memory/thermal behavior and measured sensor latency in Release;
   collect real sensor recordings and validate swing quality. Capture the running native game
   screenshot and clip for the final accepted implementation. Development screenshots exist,
   but are not the full handoff.

The known synchronous club-reference/calibration work has now been removed. The seven exact
24-step calibration results and 20 lie-specific reference reaches are immutable source values.
Actual trajectories still use the unchanged solver; only fixed reference computations moved
offline. `testClubCalibrationMatchesSolver` regenerates all seven speeds and requires exact
equality. `testClubSelectionReferenceMatchesSolver` regenerates every reach, checks all applicable
lies, and checks each threshold at the exact value and adjacent representable Doubles. Their
`CLUB_SPEED`/`CLUB_REFERENCE` output supplies replacement values when physics/calibration changes.
This removes 168 first-use integrations and up to five integrations per club recommendation,
without introducing a temporary club choice, async readiness state, or stale worker result.
Automatic-putt, native preview and accepted-shot calculations remain on workers. Further
performance work is still required for rendering, asset loading, frame pacing and real hardware.

Native sessions now enable the existing three-second next-shot/five-second next-hole progression.
Static status hints describe the pending transition without a per-frame SwiftUI countdown. Replay
cancels the deadline; explicit pause, panels and background time do not consume it. Manual-result
UI fixtures explicitly opt out with the DEBUG-only `-manualProgression` launch argument.

## Asset contract

Bundle `GolfNativeAssets.json` plus local USDZ files. The JSON has `version: 1`,
`metresPerUnit: 1`, `upAxis: "Y"`, and `holes`, where each entry contains `courseID`, `hole`
and `file` (a local .usdz filename). Preserve Course.all IDs and Hole.number exactly.
Each hole contains `tee` and `pin` entities in the root course coordinate space within 2 cm
of the authoritative metadata coordinates. Additional terrain/hazard checks remain required.

The `golfer` object contains `file`, `skeletonEntity`, `requiredJoints`, and `impactMarkers`.
The skeleton entity is a bound ModelEntity. Embedded clips must include driver, iron, chip,
pitch, bunker, putt, celebration and recovery. `impactMarkers` maps the six shot clips to
positive seconds within their clips. Keep shirt/cuff/trousers/skin/hair/ivory material names
and cap/visor entity names stable. This contract defines the loader boundary, not a claim that
an arbitrary package meeting these checks has passed visual or anatomical review.

## Verification notes

Debug and Release Simulator builds pass. Across the final focused runs, 57 distinct tests pass:
56 native/motion/mechanics/scoring tests, followed by 11 native architecture tests that include
one added recessed-surface check (10 overlap with the 56-test run). Trajectory equality is checked against the existing solver
for every Course.all hole using driver, iron, wedge and putter, including sampled playback.

A broader suite run passed 131 tests before interruption. It reported the existing
CompleteGolfTests.testNineHoleCourseKeepsLegacyIdentityAndSharedBunkerFloor mismatch: the test
expects terrain minus 0.8 yards, while the current course definitions use cached recessed
bunker floors. Course.swift and BallFlight.swift were not changed by this migration checkpoint.
The next legacy test, testBeaconIsVisualOnlyAndDoesNotDuplicateAcrossHoles, spent several minutes
building all SceneKit courses; that run was stopped. The entire suite is **not certified green**.

Additional runtime verification: iPhone 17 Pro / iOS 26.5 passed the live pose, clip seeking,
worker/replay and six stop/restart checks. iOS 27.0 passed gameplay assertions but twice produced
an EXC_BAD_ACCESS in CoreRealityIO's live-scene-update queue immediately after unit-test process
teardown. Explicit scene cleanup did not eliminate it; the equivalent 26.5 run had no crash
attachment in that earlier run. A later combined all-course/pose/preview run on iOS 26.5 also
produced a post-unit-test-process SIGABRT (`__cxa_pure_virtual`) in the live-scene-update queue's
USDZ resolver cache. It was attached to the following UI test, but its PID belonged to the
preceding unit-test host. This remains unresolved, not certified as an OS-only issue.

No physical-device FPS, sensor p95, memory, thermals, AirPlay delay or full-game parity is certified.

Latest focused evidence (iPhone 17 Pro, iOS 26.5 Simulator, September 19–20 UTC):
- 23:47:52: 112-pose audit and native four-player waiting roster passed.
- 23:49:49: all 21 runtime loads and all 21 actual-course preview exports passed.
- 23:52:10: planning, scorecard, flyover and TV comparison-flash UI flow passed.
- 23:53:10: touch shot/replay/next and six teardown/restart cycles passed.
- 23:56:26: regenerated three-head golfer passed the 112-pose audit and appearance checks.
- 23:57:46: all-club visibility, waiting roster, four-player completion and all-course worker
  trajectory parity passed (four tests).
- 00:00:02: pinned viewport scrolling and genuine portrait-to-landscape layout passed.
- 00:20:33: native burst, breeze, trail, holing/replay and touch-shot UI regressions passed;
  the comparison green-read terrain test also passed (six tests).
- 00:22:19: native GPU green-read runtime capture plus colored-pixel regression passed.
- 00:23:02: all-21-hole green-read surface/time checks, visible camera-facing navigation badge
  and six teardown/restart cycles passed (three tests).
- 00:23:32: Release build passed with the green-read Metal shader (before water/grass changes).
- 00:25:07: native architecture and motion groups passed 37 tests, with five opt-in export/load
  fixtures skipped and no failures.
- 00:36:00: all-21-course water binding coverage, shoreline-field samples, session pause/reduced
  motion/cleanup and rendered moving-water checks passed (three tests).
- 00:41:02: all-21-course water/grass binding coverage passed. The grass capture initially
  targeted empty fairway inside a grass chunk's bounds; it now targets an actual mesh vertex.
- 00:43:45: close-up grass clearance and session controls passed after the double-sided
  lighting correction; the capture was visually inspected.
- 00:44:50: expanded native architecture/motion run passed 41 tests with four opt-in fixtures
  skipped. All 21 course previews were regenerated. Grass breeze/clearance and a black-backface
  pixel regression passed, but runtime pipeline warnings prevent calling the renderer certified.
- 00:47:53: grass capture passed with the gameplay directional-shadow configuration. The grass
  material now retains only albedo, does not cast dynamic shadows (matching the source), and
  has a 0.2 model-space bounds margin for GPU displacement.
- 00:50:03 and 00:50:35: isolated lit-material controls distinguish CustomMaterial resource-limit
  warnings from game assets. A plain solid-color custom sphere with no textures, custom uniforms,
  or geometry modifier reproduces constant-buffer limits on iOS 26.5. A standard-material sphere
  does not produce that error, but logs unsupported render-target reads in a shadow pipeline.
- 00:50:57: water, grass and minimal custom-material rendering checks passed on iOS 27; its log
  reports texture binding index 31 exceeding 30 in some pipelines. The captures/behavior passing
  do not prove these pipeline failures harmless or establish physical-device behavior.
- 00:51:33: Release build passed with the native water and grass shaders.
  No claim that these focused checks certify the complete project suite or device performance.
- 01:07:13: five practice/shot-result tests passed, covering direct validation, legacy practice
  compatibility, native ball-transform/score isolation, menu practice-to-scored-play and replay.
  Practice and landed-result screenshots were inspected.
- 01:08:30: native planning/scorecard/flyover/TV-settings and pinned-viewport rotation UI tests
  passed with the expanded controls; the landscape capture was inspected.
- 01:09:46: full native architecture/motion groups passed 42 tests, with five opt-in export/load
  fixtures skipped. Existing custom-material pipeline errors remain in the runtime log; this
  passing suite does not resolve renderer certification or replace physical validation.
- 01:17:12: six automatic-putt/native-planning/comparison-parity tests passed. Native aim reads
  remain cache-only, planned and actual 60 Hz trajectory samples match, and stale/cancelled work
  is rejected. Initial compile errors in value copying/test visibility were corrected first.
- 01:22:20: four native putt-worker tests plus the Sunward putting UI flow passed, including
  preservation of an already armed phone request. The first UI attempt used the editable map's
  identifier; automatic aim correctly uses its read-only `holeOverviewMap` identifier.
- 01:23:45: expanded Sunward UI test passed through planning, a holed eight-yard fixture putt,
  hole-result presentation, Continue and loading hole 2 with stroke 1. The fixture starts on
  the green with no prior strokes; its hole-in-one label is not a tee-to-cup demonstration.
- 01:24:10: Release simulator build passed after the practice/result and automatic-putt worker
  changes. Session defaults were restored to Debug. This is compile evidence, not device timing.
- 01:32:49: full nine-hole prepared-shot scoring/persistence and final-holed-ball/restart checks
  passed. The UI fixture originally required a rounded-power slider gesture to hole a short putt;
  it missed, correctly remaining in play. Final-score UI verification now uses the existing stroke
  cap for a deterministic final stroke; a separate test covers an actually holed final putt.
- 01:34:33: solo/winner/tie final-score UI cases and Play Again passed. The winner case includes
  a landscape reachability check. Solo and landscape winner captures were visually inspected.
- 01:36:05: full native architecture/motion groups plus practice and layout UI regressions passed
  50 tests, with five opt-in export/load fixtures skipped. Runtime material/mesh-builder warnings
  remain; this is not renderer certification.
- 01:38:50: final-hole visibility/restart check also passed explicit assertions that the ball
  locator, hole beacon and green-reading grid are disabled on the final-results screen.
- 01:39:10: Release simulator build passed with the final-results changes; defaults restored
  to Debug. Physical-device performance and remaining renderer warnings are still unresolved.
- 01:44:20: three native automatic-progression tests passed, covering exact session-clock
  boundaries, pause/background/panel time, replay cancellation, player/hole transitions and
  final-score persistence.
- 01:49:02: two UI tests passed without the manual-progression override. The running game
  pauses the next-shot deadline, resumes to stroke 2, preserves replay until manual continuation,
  and automatically opens final scores with the isolated fixture's correct total and best score.
- 01:50:09: native architecture/motion groups and putting, final-results and shot/replay UI
  regressions passed 54 tests, with five opt-in export/load fixtures skipped. Existing native
  material-pipeline warnings remain in the log; passing assertions do not certify rendering.
- 01:53:51: Release simulator build passed with automatic progression enabled.
- 02:02:30: six focused tests passed on iPhone 18 Pro / iOS 27, using the new Debug test products:
  exact solver-calibration and reference-table equality, threshold edges, 204 recommendation
  queries against direct simulation, paused flight/next-shot behavior and all-course worker
  trajectory parity. Diagnostic timings: calibration recomputation 294 ms; 204 direct queries
  1.218 s versus 0.081 ms for lookup. These are Debug simulator microbenchmarks, not device FPS.
- 02:03:23: Release simulator compilation passed with offline calibration/reference values.
- 02:04:43: all three calibration/reference checks also passed under Release optimization on
  iOS 27. The test build explicitly enabled testability and DEBUG-only fixture compilation;
  the preceding plain Release build remains the shipping-configuration compile check. The
  optimized microbenchmark measured 20.37 ms to regenerate the old first-use calibration and
  81.62 ms for 204 direct recommendations versus 0.182 ms for lookup. This is a single diagnostic
  simulator sample under concurrent test load, not an application latency or frame-rate claim.

- 02:13–02:20: all-21-course native material loading, lagoon capture metadata/typed-uniform
  layout, water timing and depth checks passed on iOS 27. The first reflection render assertion
  included mostly unchanged terrain; after inspecting both captures, its ROI was moved inside
  the lake, with separate unchanged-sky and unchanged-terrain assertions. The corrected test
  passed (sky difference 0; water mean RGB difference 23.31), as did the subsequent four-test
  water/final-results regression. An opt-in capture run rendered all six resort lakes.
- 02:21:04: Release simulator compilation passed with local lagoon reflections.
- 02:21:57: the broader CourseTests run completed successfully: **41 tests, zero failures**,
  1,366.767 seconds. Its earlier 300-second MCP observation timeout did not stop the backend;
  the run was neither cancelled nor restarted. Terminal evidence is in
  `test_sim_2026-09-20T01-58-59-810Z_pid29927_7694fa2b.log` and its matching result bundle.
- 02:23:07: three focused lagoon contract/reflection/wave tests passed on iOS 26.5 too.
- 02:29:08: an isolated actual-USDZ shoreline render test passed on iOS 26.5: the imported white
  PBR control produced zero green/ochre pixels, while the corrected per-vertex material produced
  134,447. Both captures were inspected. Geometry, scoring and collision were unchanged.
- 02:29:46: all 21 course loads, grass motion/clearance, and six lake review captures passed
  with the corrected shoreline material. All six revised lake images were inspected.
- 02:32:57: the same vertex-color restoration was applied to the authored bunker soil collar,
  retaining its roughness and dark-root-to-warm-earth gradient. All-21-course material checks
  and both isolated color render regressions passed on iOS 26.5. The bunker before/after
  captures were inspected; the white rim is now the authored green/brown gradient.
- 02:34:27: shoreline, bunker-collar and localized-reflection render tests passed on iOS 27.
- 02:34:54: Release simulator compilation passed with both vertex-color materials.
- 02:37–02:45: a new all-14-clip, both-handedness, two-heading audit at an actual 11.8% resort
  slope exposed foot-height errors up to about 10 cm. Explicit camera framing reproduced the
  issue. Increasing leg-chain influence corrected foot reach in a putt diagnostic but degraded
  hand contact, so that experimental production change was reverted. The original validated
  rig remains. `GOLF_SLOPE_CONTACT_AUDIT=1` runs the known-failing diagnostic (optionally narrow
  with `GOLF_CONTACT_CLIP=putt`). It is opt-in because the user explicitly deprioritized precise
  golf/contact tuning in favor of Wii Sports-style gameplay completion. This is unresolved
  evidence, not a passing slope-contact claim.
  The new horizontal foot-slide assertion also found about 4 cm of recovery-entry slip on the
  older fixture; it remains part of the opt-in fine-contact audit. The standard animation test
  retains its existing hand/orientation/shaft and vertical foot-contact checks.
- 02:46:59: the standard 112-pose animation audit passed after reverting the experimental rig;
  the separate strict slope/slide diagnostic remains opt-in and known to fail.
- 02:51:18: four focused arcade-ball tests passed: sphere dimensions versus unchanged scoring
  radius, bunker/water clearance, authoritative trail positions, and a Sunward touch-shot UI
  flow through address, flight, rest and the next stroke. All three UI captures were inspected.
- 02:53:16: three follow-up checks passed with the larger ball: all-course worker trajectory
  parity, the green-start planning/holed-putt UI flow and touch-shot/replay progression.

### Native-only optimized build boundary — 2026-09-20 UTC

Historical candidate milestone; the later shipping Release cutover below supersedes its
statements about which configuration is the default.

- `NativeOnly` is Release-optimized and does not define DEBUG. `GolfArcadeNativeOnly` builds
  the app plus only `NativeReleaseTests`; it does not compile the old renderer unit fixtures.
  Standard Debug/Release retain their previous test suite. The UI-only Release visual-audit
  scheme is now declared in project.yml so project regeneration no longer removes it.
- Shared `SwingInput` moved to Domain/SwingMetrics. Scene configuration and native accessory
  observation no longer depend on the legacy TV singleton. Compile-time routing always chooses
  native gameplay/settings in the candidate; `-realityKit` is not required.
- 03:00:57 optimized simulator build passed. The actual compiler source list excludes all nine
  legacy renderer files. `otool` shows RealityKit but no direct SceneKit framework link; `nm`
  finds no SceneKit rendering API references. `scripts/verify-native-boundary.sh` passes for
  this product and deliberately fails for the standard comparison Release product.
- The weak `libswiftSceneKit` compatibility overlay remains. A separate object containing
  only `import SwiftUI`, `import RealityKit`, and `Entity()` reproduces the SDK-generated
  `__swift_FORCE_LOAD_$_swiftSceneKit` symbol; this is distinguished from direct rendering use.
- 03:02:47 iOS 27: all three ordinary-menu tests passed (78.5 s including build/run overhead).
  They cover solo shot/replay/manual continuation/automatic next shot, multiplayer scored
  shot, score-free practice, menu return and the single selectable resort. No seeded players,
  DEBUG shortcuts or renderer launch flag are used. Captured address/flight/result views were
  inspected; this is not a full nine-hole UI playthrough or physical-display test.
- 03:04:33 iOS 26.5: the same three tests passed (67.9 s including overhead).
- 03:06:24: all six comparison TVDisplayTests passed. 03:06:56: both native repeated-handover
  and external-host/settings-toggle tests passed. These are still host-controller tests,
  not actual device external-scene callback certification.
- The default shipping configuration has not yet been changed; outstanding acceptance,
  performance and physical-device gates listed above remain open.

### Native performance instrumentation — 2026-09-20 UTC

`-nativePerformanceAudit` enables a bounded, non-observable recorder in optimized or Debug
builds. It emits app-private `Documents/native-performance-<session>.json` reports every five
seconds and at lifecycle flushes, retaining up to 720 windows. Each metric uses at most 4,096
recent samples for nearest-rank p95; whole-window count/mean/max remain exact. JSON encoding,
sorting, memory sampling and atomic writes happen on a serial utility queue. No raw motion,
camera imagery, roster names or external-display identifiers are included. The recorder
does not collect counters or write files when its opt-in flag is absent.

Recorded metrics: game-update intervals, elapsed span across ordered app systems, sensor
sample intervals/delivery, filter-and-recognizer work, callback-to-impact-presentation latency,
trajectory preparation and foreground asset loading. Context counts distinguish phone/external,
hole number, flyover and gameplay phase. Reports include OS/model, native-only/optimization
flags, process physical-footprint samples and thermal state. Paused/loading frame gaps are
excluded; no sensor data means unavailable, never a fabricated 0 ms result.

These are **not** exclusive CPU samples, GPU completion timestamps, completed display frames,
peak-memory captures, physical swing certification or AirPlay measurements. Instruments/device
evidence remains required. See Apple's [RealityKit performance guidance](https://developer.apple.com/documentation/RealityKit/improving-the-performance-of-a-realitykit-app)
and [task VM information](https://developer.apple.com/documentation/kernel/task_vm_info_data_t).

- 03:12:55: instrumented NativeOnly simulator build passed.
- 03:14:33: optimized iOS 27 normal-menu run passed three complete moving flyovers, one
  accepted touch shot, pause and menu return. The 57.23-second report has 14 windows. Warm
  five-second game-update p95 values were generally about 16.8–17.7 ms; the early maximum
  interval was 628.87 ms. Sampled physical footprint reached 765.60 MiB, and the single
  trajectory preparation took 19.87 ms. The app-system elapsed p95 was about 2.0–2.4 ms in
  most windows. Physical sensor counts were zero. This is a baseline with a startup hitch
  and significant memory use, **not a 60-FPS/device performance pass**.
- 03:17:11: six diagnostic-statistics tests plus eleven native-motion regressions passed.
  Checks cover unavailable values, nearest-rank p95, bounded retention with exact totals,
  pause exclusion, sensor reset, disabled recording and concurrent updates.
- 03:17:44: all four NativeReleaseTests passed on iOS 26.5, including the opt-in performance
  run. Its report spans 55.36 seconds/14 windows, with a 449.51 ms early update gap,
  777.67 MiB maximum sampled footprint and one 22.08 ms trajectory preparation. Both OS
  runs expose an early stall outside the short app-system span; profiling asset/preload work
  is the next investigation, not a proven root cause. The native binary-boundary audit still
  passes. Reports are preserved in outputs as native-performance-ios26-baseline.json and
  native-performance-ios27-baseline.json.

### Local CPU trace and grass preparation — 2026-09-20 UTC

Apple Instruments Time Profiler captured the optimized native-only app on the iOS 26.5
simulator, including initial course entry and next-hole preparation. The app dSYM UUID was
verified before resolving optimized frames. Of 23,714 ms of main-thread sampling weights,
2,240 ms included grass installation, 4,350 ms included per-frame grass updates and 4,353 ms
included RealityKit's material-parameter system. These inclusive categories overlap and are
not exact wall-clock costs. Next-hole installation clustered in two adjacent one-second bins
(937 and 630 ms of asset-loader samples). This establishes grass material setup as a loading
hotspot; it does not attribute every previously observed frame gap to that code. The trace
reported a missing input source for one table; the CPU sample table was populated and only
40 ms of samples retained unresolved app frames after symbol resolution.

`scripts/analyze-native-profile.py` analyzes local exported time-profile XML and optionally
resolves unknown app addresses against a UUID-matched dSYM. No profile data is uploaded.
ETTrace's external-viewer workflow was blocked during the investigation; its temporary app
integration was removed. The product has no ETTrace dependency.

Grass installation now copies one shader prototype per preparation, preserves each material's
authored albedo, and yields between approximately four-millisecond batches with cancellation
checks. A single entity/material operation can exceed that budget; this is cooperative
batching, not a hard frame-time guarantee. Prepared cached clones remain idempotent.
Actual asset coverage across all 21 bundled holes, the close-grass rendering/time/ball-clearance
regression, cancellation, and slot-preservation/idempotence checks passed on iOS 26.5.
The first same-flow optimized iOS 26.5 run passed: maximum game-update gap 234.07 ms
(baseline 449.51), sampled footprint 653.05 MiB (baseline 777.67), warm-window update p95
16.88–17.58 ms and app-system span p95 1.66–1.80 ms. This is one run per version, not a
repeatability or device-performance claim. An early stall remains; post-change CPU
attribution confirms the former installation hotspot is largely absent: 13 ms inclusive
grass-installation sample weight versus 2,240 ms in the earlier trace, covering initial
and next-hole preparation in each. This is not an exact wall-time ratio. Trace lengths
and menu/ready proportions differ; do not compare whole-trace percentages as throughput.
Per-frame grass updates/material-parameter work remain prominent. Only 17 of 19,855 ms
of post-change main samples retained unknown app frames. Three focused grass tests also
passed on iOS 27; the optimized loading UI test and native binary audit passed.
Raw reports: native-performance-ios26-grass-batching.json, native-grass-baseline-cpu-profile.json
and native-grass-batching-cpu-profile.json in the user-facing outputs folder.

### Distance-gated grass uniforms — 2026-09-20 UTC

The remaining per-frame grass cost is addressed with bindings cached when a static course
is attached. Each binding retains its world-space visual AABB (expanded by 0.2 m) and the
last uniform value. It remains active if any part of that box is within the shader's existing
52-yard camera cutoff, or its horizontal footprint is within the existing 0.85-yard ball
clearance. Other bindings receive the neutral distant value once and then avoid repeated
RealityKit material/component accesses. Entering range uses the current session phase
immediately; there is no delayed refresh or lower-rate visible animation. Missing/invalid
bounds or camera/ball values fail open. Terrain must be rebound if its world transform is
changed; normal viewport handover does not transform the course world.

No geometry, fade distances, shader code, physics or scoring changed. Close-grass render
comparisons against all-material updates pass for breeze and ball clearance on iOS 27,
with a sub-byte mean-pixel-error threshold. Tests also cover both distance boundaries,
large bounds, vertical ball separation, invalid data, clearing stale clearance and immediate
camera reentry. These checks and the viewport-handover/in-flight-shot regression pass on
both iOS 26.5 and 27. Four optimized native UI checks pass on iOS 26.5, plus a second
same-flow performance capture. Warm-window app-system p95 spans were 0.76–1.04 ms and
0.73–1.00 ms (setup-only baseline 1.66–1.80 ms). Early update gaps remain 229.81/233.46 ms.
Warm update p95 ranges were 16.99–25.26 ms and 16.89–18.10 ms; the first run's flight/landing
outlier prevents a uniformly smoother frame-pacing claim. Sampled footprints were
654.97/642.35 MiB. The native binary audit passes. Address/flight screenshots were reviewed.
Reports: native-performance-ios26-grass-distance.json and native-performance-ios26-grass-distance-repeat.json.
Existing simulator pipeline warnings persist and are not certified harmless.

### Shipping Release cutover — 2026-09-20 UTC

`Release` now defines `NATIVE_ONLY` and excludes the same nine legacy renderer sources as
the verified candidate. The normal `GolfArcade` scheme uses Release for Run, Profile and
Archive, with no renderer launch flag. Its Test action explicitly remains Debug for existing
mixed native/comparison fixtures. `GolfArcadeRelease` runs the ordinary-menu native UI suite
against actual Release; it excludes the old fixture UI tests. `GolfArcadeVisualAudit` now
uses the separately named optimized Comparison configuration for Run/Test/Profile. The
original sources/assets remain available for development; none were deleted in this cutover.

- 04:01:20 iOS 26.5 and 04:03:08 iOS 27: all three Release UI tests passed in each run.
  Coverage: solo shot/replay/automatic next shot, multiplayer scored shot, score-free practice,
  single-course selection and menu return. No renderer flag, seeded roster or DEBUG shortcut.
- The normal GolfArcade scheme built Release for Simulator after regeneration.
- An unsigned `generic/platform=iOS` Release build of the normal scheme succeeded with
  iPhoneOS SDK 27.0. Mach-O records platform IOS and minimum OS 26.0. This is a compile/link
  check, not installation, physical play, signing, App Store submission or device acceptance.
- The actual simulator and iPhone Release binaries pass `verify-native-boundary.sh`:
  nine legacy sources excluded, RealityKit linked, no direct SceneKit framework/render API
  references. The SDK-generated weak Swift SceneKit compatibility overlay remains.
- Comparison builds successfully, includes the old files, directly links SceneKit, and is
  correctly rejected by the native-boundary verifier as a negative control.
- The actual Release address screenshot was reviewed and preserved as
  native-shipping-release-checkpoint.png. README now describes current controls and build
  configurations instead of the obsolete camera/body-scan prototype.

This completes the shipping renderer configuration, not the full rebuild. Outstanding
visual/camera/bystander checks, early loading stalls, pipeline-warning/teardown investigations,
physical motion recordings and 20-minute phone/TV acceptance still apply.

Build environment: Xcode's previously missing Metal Toolchain 27A266a was installed with
`xcodebuild -downloadComponent MetalToolchain` (838.9 MB) to compile the native shaders.

Open renderer investigation: `testNativeMinimalLitMaterialPipelineControl` renders a standalone
solid-color custom sphere; set `GOLF_STANDARD_MATERIAL_CONTROL=1` for its standard-material
control. Inspect runtime logs, not just XCTest status. Both control and gameplay captures can
pass while RealityKit logs failed pipelines. This is a reproducible baseline, not a claim that
all failures are simulator-only or safe to ignore. Resort local-reflection visual acceptance and
device validation are still required. Shader implementation follows Apple's
[CustomMaterial guidance](https://developer.apple.com/documentation/realitykit/modifying-realitykit-rendering-using-custom-materials).
The native lagoon's projection constants use Apple's
[typed custom-uniform interface](https://developer.apple.com/documentation/realitykit/custommaterial/withmutableuniforms(oftype:stage:_:)).
