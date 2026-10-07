# Tennis contact and timing tuning — 2026-10-06

GATE: CONTACT PASS — in the same 130-input sweep on each build, successful returns increased from 22 to 26. The negative-x wing expanded from 12 to 13 successful timings; the positive-x wing from 10 to 13. Inputs were sampled every 20 ms. This is a controlled fixture, not a general player success-rate estimate.

GATE: TIMING REWARD PASS — timing supplies 80% of contact quality. Perfect/great timing windows retain their original thresholds; the scrappy timing tail extends to 340 ms. Perfect targets follow the chosen direction/depth closely, while mistimed shots drift toward the centre with bounded scatter. Actual perfect returns in the sweep landed within 25 cm of the chosen target.

GATE: RETURNABLE PACE PASS — regular speed tuning is 13–30 m/s before small effort/stamina modifiers. Streak boost is 1.15x, capped at 34 m/s, replacing 1.7x. Observed maximum rated shot speed in the fixture dropped from 49.207 to 31.088 m/s. At the maximum tuning, the interception model finds reachable replies from a ready central position for left, centre and right targets, with more than 650 ms to react and travel. Good placement can still win a point.

GATE: VISIBLE CONTACT PASS — assist reach grows from 1.6 to 1.8 m, the stroke becomes eligible after 20 ms, and slightly late balls remain eligible 0.5 m behind the player. Both hidden gameplay rig and visible hero can lunge further. The hero follows the scheduled guide envelope. The existing 0.22 m visible-string contact limit stays enforced; maximum observed gap among accepted returns was 0.1766 m.

GATE: ONLINE PARITY PASS — both seats use the shared timing, pace and placement rules. Confirmed swings preserve their onset time; compensated packet delay preserves the timing grade. Provisional swings and stale point/contact inputs remain rejected.

GATE: RULES PASS — 90 EditMode tests passed, covering timing, reach, aiming, maximum pace, reachable replies, match rules, both online seats and packet delay. See rules.xml.

GATE: GAMEPLAY PASS — the 130-input updated sweep passed speed, timing-grade, contact and actual landing checks. See gameplay.xml, before/sweep.csv and after/sweep.csv.

Source parity: all ten changed source/test files match the isolated verification project; SHA-256 values are in source-verification.json.

Verification used Unity 6000.3.24f1 in an isolated copy with the original external tennis/crowd animation manifests. Three existing incompatible golf test files (NativeSessionTests, PlayerBaseTests and StandardCharacterGameplayTests) were excluded only from that copy. Their original files were preserved. These are targeted checks; the complete legacy suite and a physical phone/TV session were not run. No iOS app rebuild or device installation was performed.

GATE: RENDERED PROOF PASS — a live simulated perfect return passed and was rendered with a fixed review camera. See [gameplay contact](gameplay-contact.png), capture.xml and capture.txt. The camera placement belongs only to the test capture.
