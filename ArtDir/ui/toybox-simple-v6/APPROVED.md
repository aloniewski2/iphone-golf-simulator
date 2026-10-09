# Approved visual target — 2026-09-29

Adnan approved this set: “these look good, make sure everything looks refined and almost exactly like these screenshots”. This locks the simple V6 direction for implementation. This records visual approval, not completed runtime implementation.

## Authoritative references
- Intro and home: `intro-home-character.png` (character on BOTH screens).
- Sport selection and Tennis hub: `02-sport-menus.png`.
- Quick Match and Campaign: `03-match-campaign.png`.
- Training and guide: `04-training-guide.png`.
- Locker: ONLY `05-locker-rows.png`. Preserve existing long horizontal selection rows, label/value/chevrons and right-side character. Do not use rejected `05-locker.png` grid.
- Settings and TV connection: `06-settings-connect.png`.
- Pause and results: `07-pause-results.png`.
- Loading: `08-loading.png`.

## Fidelity acceptance for implementation
Compare each actual runtime capture beside its reference before calling the screen finished. Match relative layout, title size/weight, line spacing, navy text, lime emphasis, off-white surfaces, subtle shadows, control proportions, scenery composition, and character placement. Keep restrained styling: no oversized extruded titles, heavy blue casings, extra banners, slogans, or new card grids.

Boards are concept compositions; fit individual screens to actual TV aspect and safe areas without stretching text or characters. Keep all controls usable and readable from the couch. Verify default, focus, pressed, disabled and scroll states. Use real controls and live equipped character preview rather than flattening a whole screenshot into a nonfunctional screen.

Generated hero/world imagery is a visual target, not evidence that existing runtime assets match it. Report any remaining character/rendering fidelity gap explicitly rather than claiming pixel parity.

## Functional invariants
- Serve meter remains exactly unchanged.
- Loading accepts repeated practice swings with equipped character, shows honest loading progress, and transitions once ready.
- Postmatch phone presents functional Next Round, Rematch and Main Menu actions; never requires a swing to advance.
- Dive remains the sole special gameplay action. No ultimate or ranked additions.
- Keep existing locker selection structure and customization behavior; update visual treatment only.

Preserve concurrent agent edits. Approval does not authorize unrelated character rebuilding or gameplay changes.
