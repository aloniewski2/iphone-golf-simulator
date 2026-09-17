# Practice Lab — measurement milestone

Open **Practice Lab** from the main menu. It does not require the roster/body-scan onboarding and never changes course scores. This is the first measurement milestone, not a claim that camera accuracy gates have been achieved.

## Pilot workflow

1. Use an actual iPhone, placed securely with a clear view of shoulders and hands. Use empty hands and one player in frame. The current lab uses the front camera and the existing 2D Vision detector; it does not validate player identity.
2. Choose an anonymous participant code, handedness, challenge, and conditions (lighting, approximate distance, portrait/landscape, camera height). Do not enter names or personal details.
3. Tap **Start camera**, grant permission, and wait for usable tracking. No trial may start on stale or absent tracking. Camera permission and pose-recording consent are separate.
4. Optionally enable **Record pose trace for replay**. It records joint coordinates, confidences, aspect and timestamps, including missing poses. This toggle does not record images. A separate, default-off source-video toggle can record the oriented camera frames for a trial; obtain consent from everyone visible before enabling it. Source clips include the room and people, never audio. Trials without pose consent retain numeric metrics and outcomes but no joint coordinates.
5. Start the fixed **12-second trial**. The detector resets; hold a comfortable address, perform exactly one requested swing, then wait for completion. In a non-swing trial, perform ordinary non-golf movements for the whole window. All detections within the window are counted; the trial does not stop at the first hit.
6. Review Results. Missing detections remain in the denominator. Multiple detections and whiffs fail target challenges. Aborted/background-interrupted/zero-frame trials are explicitly excluded, not silently marked successful.
7. Open **Replay trace** to scrub poses and rerun a fresh detector. Recorded camera results and synthetic fixtures are distinguished. Synthetic fixture results never contribute to camera accuracy or timing statistics.
8. **Export** JSON before leaving. Trial-session contents otherwise live only in memory. Leaving/clearing asks for confirmation; exported files are not deleted. Source clips and their sidecars persist separately in the app's local library until explicitly deleted or the app is uninstalled, and are excluded from device backup. Share each clip only with appropriate participant consent; nothing is automatically uploaded.

The session is bounded at 50 trials and 1,800 retained frames/timing samples per trial. A truncated pose recording cannot be reported as a matching replay. Up to 50 source clips can be saved, each limited to 12 seconds at a target 4 Mbps. Export and delete source clips explicitly when necessary. Do not mix pilot tuning data with held-out validation data.

## Metric definitions (protocol `practice-lab-v3-capture-evidence`)

| Metric | Definition |
| --- | --- |
| First-try single outcome | Intentional completed camera trials with exactly one detected stroke, at most one detected attempt and zero tracking/contact retries / intentional completed camera trials. Duplicates fail. A virtual whiff is an outcome, not target success. Aborted takeaways are negative trials. |
| Direction target | Exactly one non-whiff, confidence ≥0.45, with simulated start line in requested bin. Straight: −5° through +5°; left: −45° through <−5°; right: >+5° through +45°. Missing/duplicate detections fail. |
| Power band | Exactly one non-whiff, confidence ≥0.45, in requested band: gentle >0 to <0.35, medium ≥0.35 to <0.70, firm ≥0.70. These are mapping thresholds, not measured physical effort or power-ordering validation. |
| Putting target | Exactly one non-whiff, confidence ≥0.45, with simulated total distance within ±25% of 3 or 9 feet. This measures distance control, not aim or holing. |
| Accidental strokes | Every detection during completed 12-second non-swing windows. Report window count and exposure seconds. **A window is not one independently labeled action**, so this cannot certify the proposed 1-per-1,000-actions gate. |
| Usable poses | Detector sample can be formed and its confidence is ≥0.45. Missing poses remain in frame count. Multi-person frame count flags identity ambiguity. |
| Pose FPS | Rolling rate of delivered Vision results, including missing-player results. Not camera hardware FPS, render FPS or proof that no frames were dropped. |
| Vision work | Capture callback entry through Vision processing and pose selection. Includes preprocessing; not a pure model-inference microbenchmark. |
| Pipeline latency | Capture callback entry through detector-state update, including main-thread delivery. **Excludes sensor capture delay, preview/display presentation and human-motion timing.** |

Trial-level Wilson 95% intervals are descriptive. Repeated trials from one person are correlated; do not interpret those intervals as participant-level/general-population certainty. No target percentages are automatically certified in the UI. Empty data says “Not measured.”

## Export and deterministic replay

`BenchmarkReport` contains schema/protocol/detector versions, device hardware identifier, OS, app version, anonymous trial metadata, timing samples, outcomes and optional pose traces. Capture timestamps are rebased to the first retained camera sample; gaps and missing poses remain present. Bump detector/protocol versions when behavior or scoring thresholds change.

`BenchmarkReplay.run(trial)` feeds the original aspect-corrected poses through `ArmSwingDetector.Sample` and a fresh detector with the recorded club and handedness. `matches` compares event count, timing, power, start line, strike, confidence and simulated distance with a small floating-point tolerance. It does not compare against external physical ground truth. Exported JSON can be decoded in regression tests (use ISO-8601 date decoding for UI exports).

## What this milestone does NOT measure

- No independently annotated swing occurrence, true wrist/club trajectory, physical clubface, ball speed, spin or launch-angle ground truth.
- No automatic end-to-end motion-to-photon measurement, GPU/render frame pacing or crash-free session certification. Manual external-video latency annotations and sampled thermal states are available, but are not independently verified by the app.
- No automatic setup-time, tracking-recovery-time, power-ordering or novice hole-completion certification.
- Native Vision 3D visual-depth prototype is opt-in; no measured 2D-versus-3D model comparison yet.
- No collected human dataset. Simulator fixtures are explicitly synthetic.

## Next physical-device block

Run 5–10 consenting pilot participants across supported phone tiers. Give practice attempts first, then randomize labeled direction, power, putting and non-swing trials. Independently note whether each requested action actually occurred; prompt labels alone are not ground truth. Include tracking loss, overlapping hands, lighting changes and both handednesses. Export per participant/condition. Target roughly 500 labeled movements across the pilot before deciding detector changes.

Then collect separate held-out participants and compare candidate 2D/3D pipelines against independently annotated synchronized video. Freeze the evaluation protocol before tuning against validation results. Measure motion-to-display latency with high-speed external video. The release-gate values discussed in planning remain proposed targets until this evidence exists.

## September 17 temporal-controller iteration

- Add **Pause at the top**, **Take back, then cancel**, and **Briefly hidden grip** trials. The paused trial asks for a four-second hold; an aborted takeaway must produce zero strokes. Natural occlusion is not permission to hide the entire player.
- Each trial freezes its requested capture rate and tracking mode. Diagnostics include attempts, cancellations, retries, maximum delivered-frame gap, capture drops, phase counts and depth-bearing frames. The camera's actual delivered rate remains the observed Pose FPS, not the requested rate.
- **Standard tracking** remains the default. **3D depth experiment** runs native Vision 3D selectively and augments non-grip avatar depth only when fresh, bounded and matched to the 2D torso. It does not replace the contact detector or provide a room/floor anchor. Optional 60-fps capture retains the widest supported format and falls back to 30 where necessary.
- Consented pose traces include optional relative-time depth estimates. This earlier iteration used schema 3 and did not record source video; the capture-evidence iteration below supersedes that limitation. Missing diagnostic fields in older trials remain readable.
- Ordinary course play now performs a non-scoring two-sided swing check before the existing five-second ground-ball review, followed by a ready tone. Practice Lab bypasses that rehearsal/review so its prompted detection trials retain a repeatable fresh-detector protocol.
- Contact uses a fixed 0.24-shoulder-unit arcade radius with a bounded confidence uncertainty margin. Clear contact is a hit, clear separation is a miss, and an uncertain boundary crossing is a free retry. These are initial tunable values, not empirically certified accuracy.

Still required: consented source-video collection with reference labels, comparative model evaluation, true motion-to-display measurements, phone-movement/room-anchor validation, and sustained normal-launch physical-device trials. Synthetic tests cannot satisfy those gates.

## Capture-evidence implementation — September 17

- **Current camera baseline** stays the default. **Wider front camera (experimental)** discovers a front ultra-wide camera and uses a square dynamic aspect ratio when supported, with an explicit wide-camera fallback. Newer APIs are availability-gated; deployment remains iOS 17.
- Both modes disable automatic Center Stage reframing. No depth stream is enabled. Inventory includes front/back devices, format dimensions, nominal field of view, capture-rate ranges, dynamic ratios, Center Stage support and depth-format counts. Capability presence is not proof that combinations work together.
- Select 30, 60 or 120 capture fps between attempts. The experimental selector prioritizes coverage, then supported rate and a bounded processing resolution. Requested/configured capture rates and delivered pose rate are distinct. Higher-rate capture does not guarantee faster Vision processing.
- Trials retain actual delivered dimensions, rotation, mirroring and configuration generation. Coordinate changes interrupt a trial and reset the controller's address without awarding a stroke. This does **not** detect a physically translated phone with otherwise unchanged frame geometry.
- Source recording is separately opt-in, local, silent and bounded. The recorder encodes the same oriented buffers sent to Vision, with writer-drop counts and geometry metadata. Encoder work can affect performance; compare video-off and video-on runs separately. Interrupted clips remain marked with their ending reason.
- A source clip exports as `UUID.mov`, `UUID.json` (video timing/geometry) and, when available, `UUID.trial.json` (frozen trial). The report schema is 4; older optional fields remain decodable. Trial-relative timestamp `t` maps to movie time as `trial.firstCapturePTS + t - clip.firstCapturePTS`. Preserve frame gaps; do not realign by frame count.
- Add externally filmed latency observations using reference clip, recording rate, physical-motion frame, first-response frame, destination and conditions. Duration is `(responseFrame - motionFrame) / recordingFPS`; a one-frame uncertainty is displayed. Separate phone, AirPlay and wired samples. This is evidence entry, not an implemented external-display game scene or automatic latency test.
- Leaving Practice Lab restores baseline 2D/30-fps settings. Diagnostic and recording views observe the tracker directly, separate from the parent view's session state.

Verified: 171 unit tests and the Practice Lab navigation/replay/export UI test pass on the existing iOS 27 simulator (172 total). A signed generic-iPhone Release build and strict code-signature verification pass. These checks include synthetic source-video encoding/reopening with no audio track, consent rejection, fallback selection, coordinate invalidation, timestamp mapping and backward-compatible report decoding. No live-camera comparison, participant trial, display-latency certification or thermal endurance run was performed for this build.
