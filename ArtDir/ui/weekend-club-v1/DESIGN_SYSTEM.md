# Weekend Sports Club — UI direction 01

**Product:** Island Sports Club. “Weekend Sports Club” is the design direction’s name, not a proposed product rename.
**Status:** review-only assets and mockups, September 29, 2026. No live game, character, camera or controller code changed for this UI task.

## Review

- `index.html`: selectable 25-screen review. Buttons inside mockups demonstrate pressed feedback only, not game navigation.
- `all-screens.html` / `all-screens.png`: every example.
- `review-sheet.png`: six selected editable studies.
- `concept-board.png`: AI-generated aspirational visual board. Its world, character poses, lettering and microcopy are **not** production assets. In particular, the generated “tap” serve caption is wrong for our swing-controlled game; the editable study uses **Swing**. Generated “Seaside Club” is illustrative; current venue is Tropical Open.
- `screens/`: individual 1280×720 PNG studies.
- `assets/`: 13 original transparent SVG primitives plus PNG exports, existing licensed Rubik/Roboto fonts, and one unchanged court screenshot from the repo.
- `tokens.json`: reusable palette/type/space/motion source.
- `build_gallery.py`: editable screen construction and SVG source. Run it, then `node render.cjs` to rebuild the previews. Renderer uses this machine’s bundled Playwright/Sharp paths; adapt those paths on another machine.

## Why this direction fits

Bright toy athletes need an equally confident, simple UI. Court-line graphics, rounded match tickets and a single orange action establish a recognizable sports-club vocabulary. Warm paper surfaces keep dark text readable against the busy court. The interface uses the art bible’s orange, blue, sky, mint and pink rather than a new neon palette.

Big type does the work. No gradient bevel stacks, shiny currency walls, multiple text outlines, or inconsistent generated word-art. During a rally, the UI retreats to the edges. Loading, arrival and results have space for personality. A short whiff message is playful, not humiliating.

Existing character remains V4. In production, loading/results use the actual equipped avatar rendered by the existing system. The editable layouts use an original racket illustration to mark that art slot, rather than pretending a generated hero has shipped. The concept board shows the intended character energy.

## Foundations

| Token | Value | Role |
|---|---|---|
| Paper | #F4F0E6 | Loading/results surfaces |
| White | #FFFFFF | HUD cards |
| Ink | #203047 | Text, headers, focus contrast |
| Orange | #FF6B3D | Primary CTA / player 1 |
| Blue | #5B8CFF | Player 2 / information |
| Sky | #7EC8E3 | Quiet illustration fields |
| Mint | #3DDC97 | Best timing / successful check |
| Pink | #FF5A7A | Miss/call accent; never punishment |
| Muted | #56657A | Secondary text on light surfaces |

Ink is a small UI extension to the bible, reflecting the hero’s navy clothing. Pink/blue/orange use **ink**, not small white text. All state meanings also have text or icons.

- **Type:** existing Rubik Bold for headings/scores/actions; Roboto for explanation. Licenses copied with fonts. No new font service.
- **TV size:** 1280×720 review coordinate system; 82–88px hero title, 42–56px major call, 36–40px point score, 24–28px instructions, 18–22px secondary labels. Production CanvasScaler 1920×1080 with equivalent scaling. Truly necessary couch text must stay at least 24px at this reference size; some small labels in component sheets are review annotations.
- **Phone:** 28–38pt title, 17–19pt body, 14pt metadata; 48pt minimum target, primary actions ≥56pt. Respect Dynamic Type and native safe areas.
- **Spacing:** 4, 8, 12, 16, 24, 32, 48, 64. TV 5% horizontal safe zone; top margin at least 5%. Phone native insets +20pt.
- **Shape:** 24px cards, 18px buttons, 12px chips. A venue ticket has an orange binding edge. One shallow downward shadow only.
- **Icon:** 24/32/48px grid, rounded strokes, optical centering. Pause, Next, Replay, Dive, Exit, Ball, Check, Spark included.
- **Numbers:** tabular figures, stable score cell width; names ellipsize before scores shrink. Two-line fallback for longer translated CTA labels.

## Scope / inventory mapping

| Example | Existing implementation to adapt | Proposed treatment |
|---|---|---|
| 01 Loading | `GolfArcade/Unity/LoadingModel.swift`, `ClubScreens.swift` | Honest milestone progress, idle/art slot, one tip |
| 02 Drone flyover | Tennis intro/camera presentation and native intro overlay | Lower-left venue ticket, Skip; no permanent logo wall |
| 03 Match intro | Existing match/campaign introduction | Player cards + actual match format |
| 04 Calibration | `TennisHud.BuildTimingCheck`, calibration state | Real recorded count, warm-up distinction, retry |
| 05 Return ready | `TennisHud`, receiving phase | One brief ready line |
| 06 Serve | `TennisHud.BuildServeMeter`, `TennisTossMeter` | Clear target zone and early/late labels; swing input |
| 07 Scorebug | `TennisHud.Build/SetRow` | Names, games, points, serve dot; quiet rally milestone |
| 08 Shot feedback | `TennisHud` grades + badge caption | Same badge shape for every quality; speed below |
| 09 Match calls | `TennisHud` call queue | Unified calls replacing mixed word-art |
| 10 Point | Point flow + score HUD | Upper-edge call; retain gameplay POV |
| 11 Set | Match phase + phone result controls | Continue / Exit, real phone buttons |
| 12 Pause | Session pause/settings routing | Resume, Sound & controls, Exit confirmation |
| 13 Connection | `SportsSession`, external display/controller status | Pause on input loss; clear recovery state |
| 14–15 Results | `SportsSession`, `ClubResultsScreen`, `MatchFinishControls` | Persistent Next/Replay/Exit; win/loss eligibility |
| 16 Rewards | `PostMatchScreen.swift` | Optional details, cosmetic progression only |
| 17–18 Controller | Native racket view + `MatchFinishControls` | Dive while playing; real result buttons after finish |
| 19 Load failure | `LoadingModel.isStalled/statusText` | Retry/Menu; no fake completion |
| 21 In-play feedback | `TennisHud` grade slot | Badge in actual gameplay context |
| 22 Screen check | External display onboarding | Manual confirmation fallback |
| 23 Tutorial | Playable tutorial state | One instruction + action completion |
| 24 Settings | Existing sound/control preferences | Shared modal and control treatment |
| 25 Exit confirmation | Active-match exit routing | Safe primary: Keep playing |
| 20 Shared kit / golf | Shared UI primitives; golf `Hud.cs`/`Scorecard.cs` | Sport-specific labels on common surfaces |

The audited code currently has distinct Ok/Good/Great/Excellent/Perfect grades, generated call sprites, gradient panels and outlined fallback text. This design replaces their visual treatment, not their scoring or timing rules. Existing ultimate/super UI is excluded by the current product decision. Home/store/locker navigation is not redesigned in this pack.

## Layering / visibility contract

1. World and ball: always most readable during active play.
2. Scorebug top-left, pause top-right: persistent during play.
3. Serve/receive instructions: only in that phase; dismiss at rally start.
4. Shot badge: one slot near a lower outer edge, away from the player/ball. Never stack five grades. Move if the player occupies that quadrant.
5. Point call: preempts shot badge; queued calls collapse to the decisive one.
6. Blocking pause/reconnect/result: hide serve/swing prompts and disable gameplay input. Modal takes focus.

**No camera cut for point feedback.** The existing serve scene cut remains. The mock court is an unchanged old capture, so the old in-world serve bar can still be seen in its backdrop; integration replaces that bar, never layers a second meter on top.

**Phone result contract:** match/set terminal state swaps the racket view to action controls. A swing is never required. Eligible campaign win → Next round, Replay, Exit. Exhibition → Replay, Exit. Loss → Replay, Exit. Between sets → Continue to set N, Exit. Actions stay visible until acknowledged. Disable repeat taps while loading, then restore on failure with an error line. Reward animation never gates those controls. Native and Unity derive this from the same durable result state, including reconnect/resume.

## Components and asset production

`panel-ticket`, `button-primary`, `button-secondary`, `feedback-clean`, `feedback-whiff` are transparent, text-free primitives. Eight icons complete the 13-asset set. PNGs are review/prototype exports; SVG is editable master. Use native rounded rectangles or 9-sliced sprites at integration, not stretched full-screen screenshots. Suggested slice inset: button 24px, ticket 32px, feedback 24px; preserve alpha and verify corners in Unity. Icons exported at 256px can be downsampled to target resolution. UI texture import: Sprite, sRGB, no mipmaps, clamp; atlas by display context. Do not bake player names, scores, labels or numbers into bitmaps.

Unity: adapt existing `TennisHud` builder and its event queue, preserving the gameplay model and existing PlayableGraph. SwiftUI: share the color/type vocabulary via a small theme layer within existing Club components. Do not replace working result routing or build another parallel controller state machine. No need to rewrite GolferModelImporter, character assembly or animation.

## Motion spec

- Primary press: 90ms, 3px down, shadow disappears; release 120ms.
- Panel enter: 180ms, 12px travel + opacity; no overshoot for modals.
- Score tick: 240ms, numeric crossfade with ≤1.08 scale on changed cell only.
- Shot feedback: 140ms enter, 700ms hold, 180ms exit. Perfect gets one small spark; Ok gets no flourish.
- Point call: 1200ms hold maximum; scorebug remains the durable score.
- Flyover ticket: enter after camera establishes venue, ~250ms slide; skip always available on controller.
- Loading: show actual readiness, use existing 2s minimum/0.3s finish. Tips change at most every 5s; a normal quick load shows one tip. Existing 8s delay status and 20s recovery state retained.
- Reduced motion: fades only; no pulse/scale; preserve all feedback text. Haptics/sounds supplement, never replace labels.

## Loading tip copy (10)

1. Clean timing sends the ball faster.
2. A late return can still keep the rally alive.
3. Your swing guides your aim.
4. Watch the toss. A well-timed serve pays off.
5. A high lob is a chance to go overhead.
6. Save your dive for the ball just out of reach.
7. Leave yourself room to swing comfortably.
8. Keep your eyes on the court, not your phone.
9. Missed one? The next point is a fresh start.
10. Pick a look you love. Cosmetics don’t change your power.

When party mode actually ships, add invite/social tips backed by a working invite path. Don’t advertise nonexistent online rooms today.

## Accessibility and adaptation

- Ink-on-light text; labels/icons accompany color. Check final colors against WCAG AA at implementation; this pack does not claim a completed accessibility audit.
- No rapid flash or endless shimmer. Focus ring 4px blue plus a white separation ring over blue surfaces.
- Accessible CTA names: Pause match, Resume match, Dive, Continue to next set, Continue to next round, Replay match, Exit to main menu, Retry loading. Announce score changes and the current server without reading every rally badge.
- TV examples prioritize couch play. On handheld landscape, collapse scorebug metadata before reducing point size; use OS safe areas. Phone portrait examples are a separate native controller layout, not the TV screen scaled down.
- Long names, large text, deuce/advantage, tiebreak scores, multiple set scores, loading error and failed Next acknowledgement need runtime QA.
- Golf adapts metrics only: Hole/Par/Strokes, distance/lie/wind from actual gameplay. Shot quality uses the same family; avoid tennis-specific “point” copy in golf.

## Review decisions

Approve/reject the overall direction first: cream/navy/orange, softer flat tickets, bold rounded type. Then review (1) scorebug size, (2) feedback footprint, (3) loading/results avatar slot, (4) phone finished state. This is ready for visual review, not a claim that the live game has adopted the designs.

## Artifact verification

25 screens rendered through Chromium with no page errors. Inspected overview, serve and call sheets; moved the serve panel away from the near player. Raster exports are 1280×720; 13 SVG primitives have transparent PNG companions. All work lives under this review directory.
