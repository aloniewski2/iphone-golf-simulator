# Actual-game UI mockups / v3

24 editable browser mockups: three styles × eight states. No game code modified.

## Review
- `index.html`: select a theme and screen independently. Switching theme preserves the selected state for direct comparison.
- `all.html`: all 24 screen images.
- `sunshine.html`, `festival.html`, `toybox.html`: each eight-screen collection.
- `comparison.png`: gameplay + home in all three styles.
- `screens/`: individual 1280×720 PNGs.

The eight states are Home, Sport selection, Loading, Flyover, Rally, Serve, Pause, Results.

## What is actual versus proposed
The court, environment, character and menu illustrations are **unchanged existing repository images**, not freshly generated substitutes. The UI is newly composed as HTML/CSS with live text, then rendered to PNG. These are not captures of a shipped Unity implementation, nor a fresh capture of the latest checkout. Scores, speed and loading progress are sample values. Serve uses the same receiving-view backdrop to compare HUD layout, not to propose a new serving pose/camera.

Source imagery:
- Gameplay: `ArtDir/screenshots/venues/resort_1_gameplay.png`
- Aerial: `ArtDir/screenshots/plan2d_env_postcard/10_aerial_flyover_after.png`
- V4 portrait: `ArtDir/screenshots/tennis_complete/Default_V4__Idle.png`
- Menu room: `GolfArcade/Unity/MenuArt/scene-home.jpg`
- Sport illustrations: `GolfArcade/Unity/MenuArt/club-tennis.png`, `club-golf.png`

All copied into `assets/`; originals untouched. A framed equipped-avatar card uses the actual V4 render rather than a generated face. In the game this would be a live avatar viewport in the same card.

## Theme translation
1. Sunshine: warm ivory enamel-like panels, orange raised actions, navy headline extrusion, rounded athletic type.
2. Festival: condensed italic display type, hard-edged ticket layers, cobalt/coral/yellow, graphic offset shadows.
3. Toybox: rounded molded casings, recessed blue number tiles, lime button tops, soft geometric display type.

Fonts: existing repo Rubik and Bricolage, plus Roboto. Festival uses the locally installed Impact for this mockup (Arial Narrow/Rubik fallback). Do not redistribute the system font; select/license an equivalent runtime font before shipping. Existing repo font licenses remain applicable.

## Behavior boundaries
Party/online remains deferred. Home offers Play, Campaign and Locker. Sport selection offers available Tennis/Golf. Controller gameplay retains Dive; no ultimate UI added. Match completion keeps real Next/Replay/Exit actions; Next only appears for eligible campaign wins. Paused/results layouts are visual examples, not new session state code. Mock buttons show preview feedback; sport cards route to the loading preview only.

No point camera cuts, altered hit timing, new animation, hero look revision or phone deployment in this task.

## Rebuild / verify
`node render.cjs` uses the bundled Playwright on this machine. It renders each route, waits for fonts and a real `.frame`, and outputs 24 PNGs plus comparison sheets. Run the directory with a local HTTP server for the interactive gallery.

Before game integration: verify TV couch readability, live scoreboard formats, controller mirrors of result actions, large text and safe-area layouts. Actual art fidelity is deliberately visible in these mockups so the UI can be judged honestly against the existing game.
