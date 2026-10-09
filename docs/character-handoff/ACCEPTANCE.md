# Handoff acceptance and reproducibility

## Baseline to protect

- Preserve active tennis clip files and their hashes in `active-prefab-manifest.json`.
- Preserve timing/contact behavior, current swing feel, run clips and right-hand racket grip.
- Do not run old regeneration tools against the live prefab without a before/after dependency diff.
- Archive experimental work distinctly; do not replace current slots with similarly named historical assets.

## Golf vertical-slice gate

- One Humanoid Avatar valid in the receiving project; every wardrobe piece shares its skeleton.
- Address→load→top→downswing→impact→finish→recover visibly plays on the full character.
- Scrubbing backward/abort does not jump to unrelated poses; repeated shots reset correctly.
- Both hands remain on the club; no inverted wrist/elbow, shoulder collapse or body penetration.
- Clubhead meets the ball at the actual authoritative hit time. Report maximum world-space grip/contact error across sampled frames and define a scale-appropriate tolerance with the receiving game.
- Feet stay planted intentionally; no duplicated root travel or metre/yard mismatch.
- RH and LH pass, and different club lengths use correct contact metadata.

## Customization matrix

Test boy/girl × size 0/0.5/1 × every offered haircut × None/Visor/Cap/Sweatband; at least pale/mid/deep skin, light/dark hair, contrasting garment colors. Pairwise sampling may reduce the motion matrix only after static combinations pass. Do not enable gated bald/buzz/waves until scalp tests pass.

Capture front, side, back, overhead and low angle. Include address/top/impact/finish and the full motion. Check cuffs, neck, ankles, hat/forehead, hair interiors, scalp, pupil/catchlights, knee silhouette, fingers and off-hand. Verify edits persist through save/load and game restart and appear identically in native preview and both sports. Identity choices must not change power, collision volume or competitive reach.

## Existing tests to reuse (not newly run for this audit)

- `Unity/Assets/Tests/PlayMode/NativeLaunchHeroTests.cs`: native launch/current Hero integration.
- `HairFitAuditTests.cs`, `Score80HairABTests.cs`, `Score80WardrobeAuditTests.cs`: targeted visual probes.
- `PlayerBaseTests.cs`: older bases, not certification of the newer Hero.
- `Unity/Assets/Tests/EditMode/StandardGolfGripTests.cs`: legacy golf grip contract.
- `TennisMultiAngleCaptureTests.cs`: current motion review harness; machine-specific encoder path needs adjustment.
- `GolfArcadeTests/TennisMenuSnapshotTests.swift`: native UI/locker snapshots and TV review capture.
- `HeroV6RetargetTests.cs`: experimental V6; not the active shipping character test.

Use pinned Unity, `-projectPath Unity -runTests -testPlatform PlayMode` or EditMode with a selected `-testFilter` and explicit `-testResults`/`-logFile`. Do not start a second editor against a project already open. Read each capture test's output paths and environment requirements first. Avoid rerunning every historical art generator as a “test.”

## Mobile / lifecycle gate

Profile on target iPhone in an actual match: CPU/GPU frame time, skinning cost, draw calls/material instances, texture memory, repeated wardrobe changes and scene reloads. Test bounds/culling from every camera. Do not call a 60-fps offline video a device performance measurement. No specific mobile frame budget was measured by this audit.

## Evidence delivered by this audit

Source inspection, active prefab GUID/hash manifest, review of existing hair evidence and current TV locker capture. The preceding TV capture test passed and produced a 39-second 1280×720 video; its captured frame rate is about 10 fps, not a gameplay benchmark. Golf import/retarget and character repairs remain work for the receiving agent.

Prior serve/movement tuning validation: 49/51 selected EditMode tests passed; `HardStrokesHaveSmallerContactWindowAndReach` and `ReachAssistAcceptsNearbySwingsButNotFarOrUntimedBalls` failed in the prior run. Not rerun here; do not describe the whole branch as green. See `docs/serve-and-court-pace-tuning.md`.

## Checkout / asset recovery

Use Git LFS: clone the handoff branch, run `git lfs install` and `git lfs pull` before Unity imports. Preserve `.meta` files and Unity/package versions. ArtDir includes sizeable source/review history; it is not all runtime content. Verify binary assets are real files rather than small LFS pointer text. Start from the runtime prefab manifest rather than importing the whole archive into a receiving game's Resources folder.
