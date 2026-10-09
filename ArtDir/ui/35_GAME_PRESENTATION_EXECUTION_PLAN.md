# Party Sports Game Presentation Execution Plan

Implement the loading → venue → players → play → results → exit flow for tennis and golf from `ArtDir/ui/34_GAME_PRESENTATION_RESEARCH.md` revision 2. Start with truthful loading and safe control handoffs, then shorten repeated reactions, then add venue and character presentation. Every phase must leave both sports playable.

**Date:** October 8, 2026. **Status:** Functional implementation pass complete; targeted native/editor/rendered checks and the final unsigned integrated iOS build pass. Visual tuning is held by the failed golf post-processing prerequisite. Physical phone/TV and multiplayer acceptance remain pending. Further recording was waived by the user.

**Project:** `/Users/adnanyonathan/Documents/Codex/2026-09-20/wh/outputs/iphone-golf-simulator`.

## Scope and dependencies

The work begins when a player commits to a match or all competitors are ready, and ends when the destination screen has appeared after leaving. It includes loading cards, camera choreography, walk-ons, match starts, reactions, scorecards, results, audio, and transitions on phone and external display.

Preserve tennis and golf rules, scoring, physics, tracked input, handedness, calibration, saved character choices, campaign progression, and Game Center outcomes. Presentation can temporarily gate input; it must never calculate or award a result. Preserve the accepted gameplay camera as each handoff destination.

Menus, hub, locker, lobby redesign, character rebuilding, garment work, and map geometry are outside this plan. Add only the presentation preferences required by the brief to existing settings. The brief's tennis 2v2 capture is conditional: the current multiplayer configuration supports two active tennis seats and four golf seats. Do not add doubles gameplay to satisfy a proof filename.

The brief requires post-processing, venue lighting, contact shadows, and a passing turf palette before cinematic tuning or visual proof. Treat those as dependencies from the visual workstream. Audit their current state in P0; do not assume the research brief's unverified visual observations are current. If a prerequisite fails, continue loading, state, synchronization, and timing work; hold cinematic tuning and final visual PASS until it is resolved. Do not fold face, lighting, or map reconstruction into this task.

## Current implementation and changes needed

These are source findings from October 8, not measurements of a running device build.

| Existing implementation | Planned use or correction |
|---|---|
| `GolfArcade/Unity/LoadingModel.swift` has a 2 s minimum, a time-driven curve toward 90 percent, and a 0.3 s finish. | Replace estimated percentages with readiness states and measured milestones. Use 1.2 s solo and 1.5 s multiplayer name-read minimums. |
| `GolfArcade/Unity/ArcadeScreens.swift` draws a percentage bar immediately, rotates tips every 4 s, and renders a live equipped character. | Replace the loading body with venue and roster cards; rotate tips after 5 s; cache portraits and retain a silhouette fallback. Keep existing host and controller routing. |
| `NativeSportsSession.cs` waits for the target gameplay camera to render before emitting `ready`. | Reuse this first-frame gate. Add distinct scene-ready and first-render timestamps instead of rebuilding readiness. |
| `TennisPresentation.cs` totals 15.4 s before emote extensions and uses a 1.3 s skip swoop. | Adapt its camera paths, cards, and emote hooks into bounded L2–L4 cuts totaling 7.2 s full or 3.5 s short. |
| `TennisGame.cs` holds ordinary points for approximately 6 real seconds, sets for 7, and can extend holds for taunts. | Make ordinary points serve-ready within 1.5 s. Separate game, set, and match beats, and cap emotes within the applicable beat. |
| `GolfGame.cs` has a 10 s fallback showcase followed by a 2.8 s player introduction and optional 1.9 s gallery beat. `BeginAim` hides the hole intro and snaps the camera. | Reuse the hole data and camera machinery, shorten the flyover and walk-on, preserve a compact hole card at address, and blend to the accepted aim camera. |
| `SportsSession.loadingFinished()` initiates controller setup and calibration. `end()` tears down the session and restores menus. | Keep setup as an independent readiness prerequisite; stage exit under a native cover before invoking existing teardown. |
| `MultiplayerService.swift` already tracks participant `loaded` flags and sends `run` when all competitors load; its 20 s timeout returns everyone to the lobby automatically. | Reuse participant state, add synchronized presentation boundaries, and replace the loading timeout behavior with the brief's Keep waiting / Leave flow. |
| `TennisLook.SetupPost` configures a post profile on supported tiers and disables it on Low. | Verify actual camera/profile activation on the shipping paths. Code presence alone is not a visual PASS. Record Low-tier behavior separately from the post-on acceptance captures. |

Primary integration files are the paths above plus `SportsDisplays.swift`, `SportsRuntime.mm`, `TennisPlayViews.swift`, `TennisHud.cs`, `CameraRig.cs`, `NativeSportsSession.cs`, `MultiplayerModels.swift`, `MultiplayerProtocol.cs`, and `SportsMultiplayer.cs`. New shared presentation classes and test names below are proposed files.

## Timing decisions for implementation

Use the detailed beat definitions where the summary table differs. The following interpretations resolve contradictions before coding.

| Issue in the brief | Execution rule |
|---|---|
| L1 includes a 0.3 s ready pop and a wipe, but G2 allows only 0.35 s from readiness to wipe start. | Start the ready pop immediately when eligible; overlap it with the cover transition. Log wipe start separately from uncover completion. |
| Unity scene-ready and first-render-ready are different events. | Uncover only after the target display has rendered. Measure G2 from first-render-ready after the required read minimum and multiplayer readiness barrier; retain scene-ready and all-player-ready timestamps to expose every source of delay. Never use scene load completion alone to uncover. |
| Tips require 1.5 s readability, but solo identity text has a 1.2 s minimum. | Show a short tip only when it can satisfy its read interval within the load/read window; omit it on fast loads. Never extend a ready load for a newly rotated tip. |
| G4 says point-to-serve ≤1.5 s, while L6 gives game and set boundaries longer beats. | Apply ≤1.5 s to ordinary points and aces/winners that do not end a game, set, or match. Record game/set/match boundaries separately against L6/L7 budgets. |
| L4's table uses tennis durations for both sports. | Golf L4 is 1.0 s full / 0.6 s short, matching the detailed definition and 6.5 s / 2.9 s totals. |
| Intros Off is under 1 s but still includes L4 and a 0.5 s wipe. | Overlap the wipe with the shortened L4 settle; release eligible control within 0.8 s after load handoff. |
| G3 requests skip captures for every beat although L1/L8 are unskippable. | Capture rejected skip attempts for unskippable beats; do not add an unsafe skip to satisfy the matrix. L4 skip input cannot bypass readiness. |
| Full cuts play once, and settings include Full / Short / Off. | Full enables the first-view policy; completed repeats use Short. Short always uses short cuts. Off bypasses L2/L3 and retains the brief's essential transitions. |
| Calibration and tutorial prompts can require user time after loading. | Log these separately. Start L2–L4 only when required setup is satisfied, or return to a covered setup view. Do not claim playable control before calibration. Record both raw load-to-control time and presentation-only time; a calibration wait is explicit, never silently subtracted. |

All cut budgets use elapsed presentation seconds rather than simulation speed. A user pause suspends the cut; telemetry records pause duration separately. A late or duplicated event must not restart a beat or apply a result twice.

| Beat | Tennis full / short | Golf full / short | Input and handoff |
|---|---|---|---|
| L1 Loading | Actual load with 1.2 s solo / 1.5 s multiplayer read minimum | Same | No skip; first rendered frame and required competitors ready |
| L2 Venue | 3.0 / 1.2 s | Hole 1: 4.0 / 1.5 s; later holes: 3.0 / 1.5 s | Solo skip after 0.3 s |
| L3 Walk-on | 3.0 / 1.5 s | 1.5 / 0.8 s | Solo skip after 0.3 s |
| L4 Start | 1.2 / 0.8 s | 1.0 / 0.6 s | Settle into gameplay camera, then release eligible control |
| L5 Reaction | Point 1.2 / 0.8 s; lost point 0.8 s; ace 1.5 / 1.0 s | Bogey 0.6/0.5; par 0.8/0.5; birdie 1.5/1.0; eagle 2.0/1.2; HIO 4.0/3.0 s | Skip on input; HIO after 1.0 s |
| L6 Boundary | Game 2.5/1.5; set 4.0/2.5; changeover 0.6 s | Hole-out 2.5/1.5 s, then next-hole L2 | Solo skip after 0.5 s |
| L7 Match end | 6.0 / 3.0 s | Same | Skip after 1.5 s to results; results wait for input |
| L8 Exit | 1.0 / 0.8 s | Same | Covered teardown; reveal destination only when ready |

## Shared presentation contract

Introduce a shared Unity `PresentationDirector` with sport adapters rather than a second game state machine. Native Swift owns L1, the exit cover, display routing, and platform preferences. Unity owns camera and actor choreography for L2–L7. One director owns presentation camera/input gating at a time; existing tennis and golf presentation paths become adapters and must not run beside it.

Each beat defines its ID, full/short duration, skip threshold, required readiness, safe destination, phone/TV framing, actor cues, overlay cues, and audio lead offsets. Its lifecycle is enter → playing → settle → complete, with cancel/skip resolving through the same cleanup. Every completion restores camera ownership, overlay state, and input gating exactly once. Cancelling a load routes to a covered exit, never to an unready game.

Use a session ID, beat sequence, and presentation revision on bridge events. Reject stale events after retry, rematch, backgrounding, or teardown. Proposed messages include sceneReady, frameReady, beatStarted, beatFinished, requestSkip, presentationPreferences, and exitCovered; map these onto the existing bridge after inspecting its queue and serialization contracts.

Persist seen flags per beat, venue/hole, and presentation content revision. Set a flag only after an uninterrupted full cut completes. A skip, error, or forced short cut does not mark it seen. A presentation content revision invalidates affected entries without replaying every intro after an unrelated app update.

A presentation skip must restore the required score/name/hole information and blend to L4 or the correct next ready state. Consume a tap or swing used to skip so it cannot also strike a ball. Practice swings during Ready are cosmetic; gameplay input becomes valid on the Serve/address release frame. Overview returns to the same ball, aim, club, wind, and turn without advancing play.

For multiplayer, use host-authoritative beat sequences and scheduled boundaries after every required competitor acknowledges readiness. Synchronize L2 and L4 against a shared clock estimate; do not start each client from local packet-arrival time. Ignore individual skips during shared L2/L3, use short hard-capped cuts, and keep local L5 animation cosmetic. Spectators do not block competitors. Retries, duplicate messages, and host/peer loss use existing interruption policy under a cover. Do not add an unbounded post-load wait without showing who is missing and an exit route.

Cache each roster portrait from the current appearance and pose with a key covering identity, sport, outfit/colors, and portrait revision. Use Game Center avatar data only where accessible through the existing identity service; the current participant model does not carry avatar imagery. Reconstruct remote portraits from supported cosmetic data or show a team-color silhouette. Missing imagery never blocks loading or substitutes an incorrect default character.

## Implementation phases

### P0 Establish the baseline and prerequisite gates

1. Record source commit, dirty-file inventory, Unity export identity, native build configuration, devices, render tiers, and existing timings. Reconcile concurrent edits before modifying shared files; preserve all unrelated work.
2. Trace solo tennis/golf, touch/motion, tutorial, rematch, campaign completion, external display, and multiplayer from commit through teardown. Capture existing black-frame risks, calibration boundaries, and timer ownership.
3. Check shipping post-processing, turf palette, hero venue lighting, and contact shadows with the opt-in character visual flags off. Record PASS/FAIL and dependencies; hold cinematic tuning if any required visual gate fails.
4. Create `work/presentation/baseline/`, a scenario manifest, and a gate ledger. Record current behavior before changing timings.

**Exit:** Baseline and ownership map saved. Visual dependencies have explicit status. No existing acceptance proof is treated as evidence for an unverified device export.

### P1 Build L1 loading and the native handoff

1. Change `LoadingModel` to distinguish loading, first-frame-ready, waiting-for-players, transitioning, cancelled, and failed. Remove the simulated curve and visible estimated percent. Use measured stage progress only when the underlying work supports a truthful denominator; otherwise show named stages and an indeterminate indicator even after 10 s.
2. Draw the native venue/VS card on the next frame after commit, before Unity startup. Show sport/mode/venue, local and rival identity or Practice, cached portraits, and a restrained postcard drift. Under 2 s use card motion; at 2–10 s add a corner loop; after 10 s show real progress where available and the waiting player names.
3. Extend the existing rendered-frame readiness event with timing data. Keep the native card over startup, display attachment, and scene load; uncover with a 0.25 s crossfade/iris when eligible. Make the 0.3 s ready pop overlap the transition.
4. Reuse participant loaded ticks, replace the 20 s automatic return with Keep waiting / Leave, and retain recoverable errors. Keep waiting must reset the prompt policy consistently across competitors; Leave uses L8 and existing disconnect handling.
5. Ensure phone controller and external display cards use the same session data. Cache work must not delay commit or launch. Audio ambience and card/ready cues arrive through the shared audio interface introduced here.

**Exit:** G1 loading half and G2 pass on cold/warm loads, fast loads, delayed loads, render failure, and cancellation. Existing controller setup still works. Native tests cover clock-driven completion, honest progress, cancellation, and stale readiness events.

### P2 Add the director with L4 and L8

1. Add the shared contract, beat table, preference storage, seen flags, skip cleanup, and thermal/render-tier policy. Introduce the contract before migrating either sport's cameras.
2. Implement tennis Ready…Serve and golf address. Blend for at least 0.25 s to each sport's current accepted camera; coordinate scoreboard/aim/club visibility and exact input release.
3. Keep controller scan, latency checks, golf grip readiness, and tutorial prompts independent. Do not let presentation completion clear a gameplay or calibration pause.
4. Implement native exit cover → destination preparation → existing session teardown → reveal. Hold the venue still if the destination is late. Make rematch allocate a fresh session, return to L1, and force short L2/L3.
5. Test cancellation during every lifecycle step, rotation/display changes, background/foreground, retry, and rapid repeated Leave/Rematch input.

**Exit:** Both sports launch and leave safely with L2/L3 temporarily bypassed. G1 exit half, G5 handoff behavior, and G6 pass. Off mode stays within the proposed 0.8 s presentation budget when setup is already satisfied.

### P3 Shorten L5 without changing gameplay results

1. Replace tennis's fixed six-second point hold and emote extension with the bounded point/ace cuts. Remove camera ownership conflicts with existing reaction/replay systems. Suppress or trim optional replays whenever they would exceed the point budget.
2. Trigger golf reactions only after the authoritative ball-rest/hole-out result. Scale by score; HIO skips after 1 s. Keep ordinary shot feedback immediate and preserve scoring/next-shot setup.
3. Limit optional big-shot/putt tension presentation to one per hole, expose its off control, and suppress it for remote shots. Peer outcomes appear as toasts.
4. Keep current equipped emotes where their useful section fits; blend out at the hard cap. No selected emote may delay the next serve or input release.

**Exit:** A deterministic 20-point ordinary/ace sample has maximum point-end-to-serve-ready ≤1.5 s, including emote choices and skipped reactions. Golf result variants meet their budgets, award results once, and preserve the next shot.

### P4 Add L2 venue reveals and golf hole cards

Begin visual tuning only after P0's visual dependencies pass.

1. Adapt existing tennis venue camera paths into one eased 3 s / 1.2 s reveal with venue name and a 150 ms audio lead. Supply phone and TV framing for each supported venue.
2. Replace golf's long signature-shot showcase with green → hazards/landmark → tee cuts at 4 s / 1.5 s for Hole 1 and 3 s / 1.5 s later. Reuse hole geometry, wind, par, and yardage data. Retire the automatic showcase shot from this flow.
3. Keep a readable compact hole card through L4, including after skip or Flyover Off. Add Overview at address using the same L2 choreography and a safe return to the unchanged shot state.
4. Use current LOD/impostor systems. Force short cuts for serious thermal state/Low tier; profile the widest shots against the brief's 250 draw-call budget. Short cuts must also pass that budget.

**Exit:** Full/short/skip reveals meet L2 budgets; every supported hole has legible phone card proof. Overview does not change shot state. G5/G6 and reveal performance gates pass.

### P5 Add L3 walk-ons and nameplates

1. Adapt existing tennis emote hooks to two approximately 1.2 s introductions plus a 0.6 s two-shot, or a simultaneous 1.5 s short flourish. Use compatible clip sections and blend-outs; do not accelerate an entire long emote until it looks frantic.
2. Give golf one 1.5 s / 0.8 s tee-up/twirl or equivalent existing flourish. Show a turn strip for multiple golfers, rather than sequential introductions.
3. Use full-body/three-quarter moving views with actual venue lighting and contact shadows. If locomotion is unsuitable, use an authored hop/bounce instead of foot sliding. Do not alter character sources to solve staging.
4. Keep phone heroes ≥35 percent of frame height and face-forward holds ≤0.8 s. Use 5 percent title-safe margins on TV. Preserve names on the scoreboard/turn strip after any skip.

**Exit:** G7 passes for male/female, both handednesses, and representative light/dark/saturated equipped colors. No emote extension can exceed L3 or the total load-handoff-to-control budgets.

### P6 Add L6 boundaries and L7 results

1. Distinguish game, set, match, hole, and round boundaries at the authoritative result event. Play the largest applicable beat once instead of stacking L5, L6, and L7.
2. Add bounded game/set score cards and a 0.6 s covered changeover. Golf hole-out fills the scorecard row, then runs next-hole L2 or the mini L1 loading card if streaming is necessary.
3. Commit final score, stats, XP, and multiplayer/campaign completion once before celebration. Present win, friendly loss/clap, and a two-shot where supported, followed by results. Hold results indefinitely for input.
4. Show exactly Rematch and Leave on the match results card. Adapt those actions to current campaign/round progression so leaving preserves existing rewards and unlocks; do not auto-start another match after a timer.

**Exit:** L6/L7 full/short/skip budgets pass; result state survives bridge-event loss via the existing durable snapshot. Repeated input cannot duplicate awards, advance a hole twice, or launch two rematches.

### P7 Finish audio and display behavior

Audio cues are wired during each earlier phase; this is their integration and mix pass.

1. Mix card, ready, cut, score, win, and exit cues through one stinger interface. Target 100–300 ms leads, approximately 150 ms by default, and a roughly 6 dB music duck for 0.5 s. Attach venue music to in-world sources where supported.
2. Test muted audio, background/resume, overlapping cues, and an emote cancellation. Keep audio/haptics on the presentation clock; no orphan stinger plays after exit.
3. Verify portrait phone, supported phone landscape, TV 16:9, display reconnect, and thermal fallback. Validate readable cards, camera paths, hero size, and safe margins using actual target dimensions.

**Exit:** No audible hard cuts or duplicate cues; all remaining display and thermal variants pass without changes to accepted gameplay framing.

### P8 Verify the integrated build and present proof

1. Run relevant native tests, Unity EditMode tests, and rendered PlayMode tests. Export Unity and build the integrated Release app from the verified source state; record its identity. Editor and SceneKit previews are supporting evidence only.
2. Capture all applicable full/short/skip scenarios on phone and external display. Include first view, repeat, rematch, Off, delayed multiplayer, calibration, interruption, and thermal paths.
3. Generate the timing summaries, frame checks, framing measurements, and PASS/FAIL ledger. Fix failures in the affected phase and repeat only the relevant scenarios plus a short both-sports launch/play/exit regression.
4. Show Adnan the actual integrated-game videos and representative stills. G10 remains pending until Adnan approves the supplied videos; automated PASS does not substitute for that approval.

**Exit:** G1–G9 have attached passing evidence; G10 has explicit approval. All known limitations are recorded with their actual unsupported scenarios.

## Tests and evidence

Extend the existing native `TennisCampaignTests` loading tests, `SportsIntegrationTests`, `MenuFlowRegressionTests`, and `MultiplayerTests`; the existing native `PresentationTests` primarily cover golf scene/audio behavior and do not prove this full flow. Add focused director/timing tests for fake-clock scheduling, seen flags, skip input consumption, pause, cancellation, and session/sequence rejection.

Use existing Unity `NativeSessionTests`, `NativeTennisSetupTests`, `NativeGolfFlowTests`, `TennisGameplayTests`, `TennisTutorialPlayTests`, `GolfPresentationTests`, and `MultiplayerRuntimeTests` where applicable. Add shared presentation PlayMode tests for camera ownership, readiness, state preservation, beat precedence, and result durability. Pure policy tests belong in EditMode; rendered first-frame tests require the windowed runner.

Available repository runners are `Unity/Tools/check.sh`, `Unity/Tools/run-playmode-windowed.sh`, and `Unity/Tools/build-integrated-ios.sh`. Supply narrow relevant PlayMode filters; inspect XML results because the windowed script allows the editor process to return an error while it parses results afterward. The integrated build script requires a fresh Unity iOS export and builds without signing. Select the existing native test scheme and available simulator at execution time; use a properly signed integrated build for physical-device proof.

Store intermediate evidence under `work/presentation/<run>/` and exported review videos/stills under `outputs/presentation/<run>/`. Maintain a manifest connecting each review artifact to its raw capture, source/export/build identity, sport, venue/hole, device, display mode, cut policy, skip time, render tier, flags, and gate results.

Required telemetry includes session ID, beat ID/sequence, elapsed presentation time, scene-ready, first-render-ready, all-competitors-ready, setup-ready, transition start/end, eligible control release, point end, pause intervals, and each client's scheduled/actual L2 and L4 entry. Use monotonic clocks locally; include measured clock offset/uncertainty for cross-device comparisons. Detect any cinematic hitch instead of hiding it by resetting the timeline.

Required summaries are `L1_load_timing.json`, `L5_point_to_serve.json`, `beat_timings.json`, `multiplayer_sync.json`, `frame_checks.json`, and `GATE_RESULTS.md`. Use the brief's L1–L8 filename families. Record nonapplicable combinations explicitly: L1/L8 are unskippable, doubles gameplay is absent, and single-player loads have no multiplayer sync gate.

| Gate | Required acceptance evidence |
|---|---|
| G1 | Frame checks show no exposed black/startup/teardown frame during commit → L1 or L8 → destination, on phone and TV. Intentional calibration flash probes are classified separately. |
| G2 | First-frame-ready to transition start ≤0.35 s after the read minimum and required-player barrier. Raw scene/render/player/setup timings are retained; progress is truthful. |
| G3 | Applicable L1–L8 full/short/skip or rejected-skip captures on phone/TV; unsupported doubles marked explicitly. |
| G4 | Beat budgets ±0.2 s; ordinary point-to-serve ≤1.5 s; eligible presentation totals ≤7.2/3.5 s tennis and ≤6.5/2.9 s golf; Off ≤0.8 s. Calibration waits and pauses visible separately. |
| G5 | Every accepted skip reaches the safe ready state with necessary names/hole data, and its input cannot strike a ball. |
| G6 | Handoff matches the destination camera within the brief's <5 percent position/FOV criterion, or uses a verified eased blend ≥0.25 s with no end snap. Define the position comparison relative to camera-to-subject distance in the checker. |
| G7 | Phone hero shots ≥35 percent frame height; face-forward holds ≤0.8 s; TV overlays inside 5 percent title-safe margin. |
| G8 | Required competitors enter L2/L4 within 300 ms on actual 2-player tennis and 2/4-player golf runs, including a delayed client. Simulated tests supplement real-device proof. |
| G9 | Main visual acceptance captures use post-processing on with `VISUAL_CHARACTER_SKIN_POLISH` and `VISUAL_BLINK_NORMAL_FIELD` off. Low/thermal fallback captures are separately identified. |
| G10 | Adnan approves the actual integrated-game videos. |

## Delivery order and completion policy

Execute sequentially: P0 → P1 → P2 → P3 → P4 → P5 → P6 → P7 → P8. P4/P5 visual tuning waits for the visual prerequisite pass; P1–P3 can progress independently. Multiplayer protocol and readiness changes begin in P1/P2 and are tested there rather than deferred to final integration.

After each phase, update the ledger with `GATE: <name> PASS` or `GATE: <name> FAIL`, evidence paths, and remaining failures. Show actual game proof when a visual phase passes, then continue within the authorized scope. Do not request repeated phase approvals. The final video approval is required by the brief's G10 after the complete review package is ready.

The first implementation slice is P0 plus P1: prove immediate native coverage and truthful loading while preserving the existing first-render and controller setup mechanisms. Completion requires both sports, the integrated build, external-display behavior, and multiplayer proof; passing a solo tennis editor capture alone is insufficient.
