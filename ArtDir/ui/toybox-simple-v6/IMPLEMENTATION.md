# Island Sports Club — menu implementation

Implemented 2026-09-29. Visual target: `APPROVED.md` and the V6 boards. Runtime captures are in `runtime/`. These are rendered SwiftUI/SceneKit screens, not flattened mockup images.

## Delivered

- Intro with the equipped character and white Play control; Home with Play, Campaign, Locker, Settings.
- Playable sport selection, Tennis/Golf hubs, court selection, Quick Match setup, campaign map, training, guide, golf lesson, connection, settings, pause, results and loading.
- Quick Match uses real opponent, difficulty, and short-set / one-set / best-of-three settings. Replay preserves those launch settings. Campaign retains its existing ten opponents, unlock gates and scoring.
- Locker keeps all ten long horizontal rows and real mesh/tint swaps. All ten fit on the TV; phone rows scroll. The preview rotates by dragging.
- Shared navy / lime / off-white components, original vector palm mark, restrained focus outline, large touch targets and main-action accessibility labels. Compact CTA type scales with Dynamic Type, capped at 150% to fit; the TV remains a fixed 1280×720 canvas respecting overscan.
- Native pause and postmatch buttons. Main Menu exits directly home. Next Round / Rematch retain existing match progression and duplicate-completion protections.
- Tennis loading accepts repeated gyro-triggered practice swings or taps. It renders the real gameplay forehand as vertex morph targets on the equipped character; it returns to the menu idle after each swing. Ten short tips, actual readiness gate, estimated progress below 90%, retry/back on stalled loads. Golf retains its existing idle warmup; a golf-specific interactive swing is not supplied in this pass.
- No serve-meter changes. No gameplay animation, hit mechanics, ultimate, ranked, network or monetization changes.

## Runtime entry points

| File | Responsibility |
| --- | --- |
| `GolfArcade/Unity/ClubDesign.swift` | `IslandUI`, backdrop, wordmark, button, horizontal selector, shared shell and player preview |
| `GolfArcade/Unity/ClubScreens.swift` | `Island*Screen` implementations |
| `GolfArcade/Unity/TennisMenuViews.swift` | Existing route → new screen mappings |
| `GolfArcade/Unity/TennisMenu.swift` | Remote focus grids, Quick Match settings, campaign selection, postmatch navigation |
| `GolfArcade/Unity/LoadingModel.swift` | Readiness/progress and practice input lifecycle |
| `GolfArcade/Unity/CharacterModelPreview.swift` | Existing equipped meshes/tints plus menu idle and repeated forehand playback |
| `GolfArcade/Unity/SportsSession.swift` | Match settings, pause/display state, phone loading transition |
| `GolfArcade/Unity/SportsHome.swift`, `SportsDisplays.swift`, `TennisPlayViews.swift` | Actual phone/TV presentation, pause and finish controls |

Both Xcode projects include the assets. Existing legacy screen definitions remain for compatibility; party/online entry is deferred. Existing concurrent gameplay/character work was preserved.

## Assets and rebuilding the preview

Backgrounds: `GolfArcade/Unity/MenuArt/island-intro.png`, `island-terrace.png`, `island-campaign.png`. They are original generated scenery based on the approved boards. Buttons, typography, progress, map markers and navigation are live native controls.

Menu pose uses the existing gameplay hero Idle at 1.5 seconds. Loading uses eight sampled Forehand targets, interpolated on a 60 Hz timer for a 1.5-second practice swing. All wardrobe variants share the bake order; original `HeroV4.bin` remains unchanged. Compressed menu pose and frames add approximately 46 MiB. Raw intermediates are excluded from the app and ignored by Git.

From repository root:

```sh
/Applications/Unity/Hub/Editor/6000.3.24f1/Unity.app/Contents/MacOS/Unity -batchmode -projectPath "$PWD/Unity" -executeMethod GolfArcade.EditorTools.HeroLockerExport.RunMenuPreview -logFile /tmp/island-preview-export.log
swift ArtDir/ui/toybox-simple-v6/tools/package-preview.swift
```

Raw output: `source/preview-raw/`. Packaged output: `GolfArcade/Unity/CharacterAssets/HeroMenu.json`, `HeroMenu.lzfse`, `HeroSwing_*.lzfse`. Do not package raw `.bin` intermediates.

## Verification

- Simulator build succeeded.
- `TennisCampaignTests`: 23 tests passed. Includes navigation, tutorial gates, Quick Match launch settings, real tint adjustment, readiness/stalls, replay, next round and duplicate completion protection.
- `testApprovedIslandScreens`: passed. Captures TV routes plus phone home, locker, settings, campaign, Quick Match and loading. Asserts active morph weights on multiple character meshes after practice, then triggers another swing.
- Unity-integrated `GolfArcadeUnity.xcodeproj` generic iOS build succeeded, signing disabled. Not installed on the physical phone in this task.
- Physical gyro threshold and AirPlay pause/resume still need a device session; simulator captures verify presentation and triggered playback, not hardware motion or display transport.

## Honest fidelity gaps

The UI layout, copy, palette and selection structure follow V6. The live hero is the project's current customizable mesh, not the generated illustration. Face/hair surfaces, neck shading and bent-leg idle still differ visibly; this pass does not rebuild or hide that model. SceneKit shading also differs from the Unity court renderer. Source gallery uses a blonde test loadout; production follows the user's saved cosmetics.

Campaign maps the real ten rounds (the concept board illustrated six). Training keeps the real rally-practice mode and pace control, rather than presenting unimplemented drill types. Results use the equipped idle pose rather than a newly authored victory animation. Golf does not yet have an interactive loading swing.
