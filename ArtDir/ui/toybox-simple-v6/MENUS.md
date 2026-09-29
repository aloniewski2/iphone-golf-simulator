# Menu expansion of approved intro/home

Generated visual mockups using the built-in imagegen tool with `intro-home-character.png` as the strict reference. These images are review targets, not implemented runtime screens.

## Screens
Each paired board reads top, then bottom.

- `02-sport-menus.png`: Select a sport; Tennis hub.
- `03-match-campaign.png`: Quick Match setup; Campaign.
- `04-training-guide.png`: Training; How to Play.
- `05-locker-rows.png`: approved-format locker revision: long horizontal label/value selectors on left, equipped character on right. Supersedes the rejected thumbnail-grid `05-locker.png` concept.
- `06-settings-connect.png`: Settings; Connect to TV.
- `07-pause-results.png`: Pause; Match Won.
- `08-loading.png`: full-screen loading with repeatable practice swing concept.

## Shared direction
Flat navy rounded sans-serif type, open spacing, restrained lime primary action, simple unboxed secondary actions, subtle surfaces and shadows. Island scenery and equipped character provide depth. No thick molded frames, extruded headings, oversized card stacks, slogans, monetization banners, ranked or ultimates.

## Loading behavior for implementation
Reuse equipped character and existing practice swing clips. Accept repeated swing input throughout loading. Show real loading progress. Transition once ready, without forcing additional practice or artificial delay. No practice grading required. This image shows the interaction concept only.

## Existing behavior to preserve
Serve meter unchanged; none is drawn here. Controller has Dive during play and actual Next Round/Rematch/Main Menu buttons after match completion. Online/party work remains deferred. Existing character proportions and game mechanics are not modified by this visual exploration.

## Copy variants
Loss results reuse the results layout with `Match Lost`, actual score, `Try Again`, `Main Menu`. Settings links and quick-match values must reflect actual supported options during implementation; mockup values are illustrative.

Locker format follows the existing TV `ClubCharacterScreen` in `GolfArcade/Unity/ClubScreens.swift` and `ClubRow` in `ClubDesign.swift`: label, optional swatch, previous/value/next controls. Retain its existing rows and actions. The phone-specific `LockerStudio` has a different layout; it is not the target for this TV mockup. User explicitly prefers the existing long horizontal rectangles over a thumbnail grid.

Full prompts: `menus-prompts.json`; locker revision: `locker-rows-prompt.txt`.
