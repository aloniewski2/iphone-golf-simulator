# UI joy / depth explorations — v2

Three intentionally different alternatives to the rejected flat v1. Each board compares the same four situations: loading, drone venue arrival, gameplay score/feedback, and match results. These are **AI-generated art-direction concepts**, not installed UI or approved new world/hero art.

## 01 — Sunshine Club

**Feeling:** an inviting summer sports holiday.

**Typography:** oversized athletic rounded grotesk with dimensional cream/orange headline lettering and navy structural depth. The large, optically tight headline is the main identity; body/score text stays clean and flat enough to read.

**Materials:** warm sculpted plaques, chamfered action buttons, recessed numeric wells, fine warm edge highlights and grounded shadows. Deep loading/results composition; small lightweight scorebug during play.

**Motion translation:** the progress ball travels in a recessed rail, button top depresses into its orange sidewall, score numerals settle into the inset cell. Reuse the existing V4 hero for the expressive idle and result pose.

## 02 — Rally Festival

**Feeling:** a colorful, social sports event with punch and swagger.

**Typography:** large condensed italic headlines, strong tabular scores, directional layouts, disciplined spacing. Contrast comes from scale and shape rather than adding outlines to every line.

**Materials:** die-cut event tickets, folded backing tabs, coated-paper layers and rubberized action slabs. Warm coral and marigold offset electric cobalt.

**Motion translation:** ticket enters with a short slide/settle, score strip rolls once, winning ribbon opens briefly. No continuous confetti or pulsing HUD.

## 03 — Toybox Arena

**Feeling:** the tactile pleasure of a beautifully made toy.

**Typography:** buoyant, wide-counter geometric display; clear deep-blue text on white/lime. Softness comes from letter shape, not blur.

**Materials:** molded casings, dimensional blue sidewalls, recessed number tiles, raised button tops, restrained highlights. Mango/lime pops over sky/lilac.

**Motion translation:** physical press/rebound, ball rolling down a molded loading rail, one gentle podium/star beat on results. Keep active-play feedback brief.

## Common production rules

- Treat the boards as visual direction. Do not extract their raster text for runtime score labels, player names or buttons. Rebuild type with licensed live fonts or an approved custom display alphabet plus live supporting text.
- Generated hero poses, scenery, equipment and decorative text are illustrative. Keep the locked V4 character and existing game assets; no automatic hero/environment replacement is authorized by these boards.
- Preserve the current game name **Island Sports Club**. Direction names are not renames.
- The four UI situations have the same function in all three concepts; choose the visual family, not new mechanics.
- No ultimates, no ranked pillar, no power cosmetics. Swing remains the actual control; controller has Dive and Pause during play.
- Next / Replay / Exit remain real phone buttons after a completed match. Next appears only when progression allows it; no swing-to-advance.
- Point feedback does not change the gameplay camera. Existing serve intro is retained.
- TV-safe margins, native phone safe areas, accessible labels, Dynamic Type for explanations, and explicit text/icons for color-coded states are required in the implementation pass.
- Depth should come from 2–3 well-lit UI layers, 9-slice artwork, edge highlights, shadows and a small number of scene objects. Do not build the HUD as many costly real-time 3D transparent surfaces.
- During play, preserve the ball path and athlete silhouettes. Dramatic typography belongs on loading/results, while scores and quality labels prioritize instant recognition.
- Visible scores, progress and speed are sample data. Color contrast and smallest labels still require implementation QA. The generated boards do not certify accessibility.
- Original v1 pack remains intact; all new work is isolated in `ArtDir/ui/joy-directions-v2/`.

## Decision to make

Choose the overall family first. Then bring that family to the existing 25-state inventory, tune the typography on real game captures, and export runtime-ready assets. Avoid mixing three competing button/type vocabularies into one game.
