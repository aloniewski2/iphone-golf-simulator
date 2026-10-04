# Motion Club — resort menus and hinge navigation

Implemented 2026-09-29 on `newmapsandmenus`. Visual target: `01-radial-home.png` (separated cards along a half-circle). The connected-panel proposal is not used.

## Delivered

- Motion Club title and app display name.
- Home: large Play / Locker / Store / Settings cards along a curved rail. Hover or phone-remote focus selects an activity; touch opens the destination. Store shows a lock and Coming soon and refuses navigation. Campaign remains reachable under Tennis; it is removed from the home menu.
- Play preview repeats the existing equipped hero's gameplay forehand. Locker preview spins and tries hat/shirt combinations on a temporary copy; it never writes equipment to the player profile. Settings uses a newly baked menu-only seated pose and a striped deck chair. Store uses a locked equipment case.
- Four original scenery plates derived from the approved concepts: clubhouse entrance, terrace, locker interior, courtside warmup. These have no baked UI or foreground avatar. Native controls and the live customizable SceneKit hero sit over them.
- Shared resort styling across sport selection, Tennis/Golf hubs, Quick Match, court selection, training, guides, connection, Settings, campaign story, results and pause. Campaign's existing island map remains its route-specific backdrop.
- Locker preserves all ten long horizontal customization rows and live mesh/tint changes.
- Both loading screens use the courtside setting and a substantial lime progress bar, real readiness/progress, rotating tips, back/retry recovery. Tennis keeps repeated physical/tap practice swings. Golf keeps its existing golf idle; a new golf swing animation is not authored in this menu pass.
- Literal 800 ms vertical-edge hinge swing between menu routes, including pause on TV. Forward pivots on the left edge; Back pivots on the right. The destination remains stationary behind the outgoing screen, which rotates through 90 degrees and darkens slightly as it turns. No lateral slide. Reduce Motion uses a 160 ms dissolve.

## Architecture and limits

`MotionClubMenus.swift` owns the curved menu, preview selection, shared room routing and `ClubCameraHost`. The transition rotates the outgoing native screen about a fixed hinge edge; the resort is illustrated scenery, not a continuous navigable 3D clubhouse. `ClubDesign.swift` and `ClubScreens.swift` keep the shared native screen family. `TennisMenu.swift` keeps existing remote/input navigation, adds validated focus and a locked Store action. Invalid touch IDs no longer invoke a previously focused action.

`CharacterModelPreview.swift` uses existing hero meshes and the existing forehand morph frames. `HeroLockerExport.RunClubLoungePreview` bakes a seated pose with the same 38-part order as `HeroMenu`, on a temporary runtime instance. No gameplay rig, clip or material is rewritten. `HeroLounge.lzfse` is the new packaged pose. The chair/case are lightweight SceneKit props. Equipped character rendering still has the project's existing hair/face/neck/leg fidelity differences from the generated concepts; this is not a character rebuild.

Preview lifecycle stops timers when hidden/dismantled; reduced motion freezes preview activity. Screen transitions cancel cleanly on a new destination. The main cards have accessibility labels and selected traits. Phone layouts preserve large controls and scrolling locker rows; TV uses its existing 1280×720 canvas and overscan setting.

## Verification and review

- Simulator build succeeded; both Xcode projects regenerated to include new source/assets.
- 25 navigation/loading/preview tests passed (23 TennisCampaignTests + 2 MotionClubReviewTests).
- Checks include locked Store refusal, no profile mutation from preview selection, seated pose applied to multiple modular meshes, return to racket preview, invalid action refusal, existing loading readiness/stalls, campaign/quick-match/replay/progression.
- `runtime/`: actual native TV-root screenshots and phone layouts. Tour fixtures visit routes without starting a match or altering production progress.
- `Motion_Club_Menus_and_Transitions.mp4`: real simulator screen recording cropped to the TV canvas; not generated video. No interpolation of the character animation. Native recording avoids per-frame screenshot overhead. The complete tour includes 24 stable menu states, loading practice and route changes.
- `runtime/index.html`: zoomable gallery and recording. Generated concepts stay outside runtime/.
- No physical iPhone install or AirPlay hardware session performed in this turn.

## Scope preserved

No scorebug, serve meter, shot feedback, in-match camera, gameplay controller, ball/shot mechanics, abilities, network, purchases or gameplay cosmetics-power changes. No new golf motion pack. No change to the saved equipped loadout from home previews.

## Art provenance / prompt

Built-in image generation edited the approved resort mockups into clean background plates. Prompt: remove all text, UI, logos, character/profile and reflections; preserve the approved warm stylized wood/stucco/brass/ocean setting; leave the left side clear for native controls and the right foreground empty for the equipped character. Final layers live in `GolfArcade/Unity/MenuArt/club-*.png`.

Rebuild seated pose: run Unity's `GolfArcade.EditorTools.HeroLockerExport.RunClubLoungePreview`, then `swift ArtDir/ui/motion-club-radial-v1/tools/package-lounge.swift` from repo root. Raw intermediates remain in the existing ignored preview-raw directory.


## Hinge + victory follow-up (2026-09-29)

- Replaced the perspective pan with a fixed-edge 90-degree Y-axis rotation. No destination translation. 800 ms eased rotation, edge shade; Reduce Motion uses 160 ms dissolve.
- Shared `ClubVictorySummary` renders real already-awarded `PostMatchSummary` totals, level crossings, score and aces/winners/best-rally stats. It never awards XP itself or creates cosmetic rewards.
- TV results emphasize VICTORY with a trophy, cream information panel, navy progression card and equipped avatar. Losses read MATCH COMPLETE.
- Actual phone `MatchFinishControls` uses the same progression component with a persistent action footer. Next Round, Rematch and Main Menu use their original session callbacks/identifiers and are usable during XP counting. No separate rewards detour is required.
- 28 selected simulator tests passed, including campaign next/replay, actual phone finish root, and progression tests. Native short tour separately verifies final layout and hinge capture.
- `runtime/Motion_Club_Hinge_and_Victory.mp4` supersedes the older pan recording. `victory-tv.png` and `victory-phone.png` are runtime captures (review fixture on TV, simulator match-complete event on phone).
- No physical-device install/AirPlay hardware validation in this pass. In-match serve meter and HUD unchanged.

### Hinge rendering verification

The hinge uses explicit perspective rotation keyframes outside SwiftUI's update transaction, with completion driven by `CAAnimationDelegate` rather than a wall-clock timeout. `testHingeActuallyRotatesAroundEdge` passed: the displayed layer had m11=0.824 and m13=-0.566 mid-transition, the left anchor remained fixed, and the outgoing snapshot was removed with input restored. The simulator native recording has variable frame cadence; no 60 fps playback guarantee is claimed.
