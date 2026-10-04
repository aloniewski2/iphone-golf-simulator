# Rally Pop — implemented HUD

## Runtime
`Unity/Assets/Scripts/Tennis/TennisHud.cs` builds native uGUI score, feedback and call panels. No generated badge images are needed. Existing ShowGrade/ShowCall and MatchVisible APIs remain intact.

- Navy #0E1F36, cream #FAF6E6, lime #CCF04D; Rubik Bold native text.
- Two cream name rows, tennis-ball server indicator, games cells and lime point cells.
- Multi-set scores have a separately labeled footer instead of cramming sets and games into one cell. Deuce, advantage and numeric tiebreak scores retain existing scoring rules.
- Quality and existing shot/speed details appear briefly at lower left; point/fault/ace/game/set calls use a queued upper-center panel.
- Subtle score pulse and short fade/settle replace bouncing names, giant word art and periodic gloss sweeps.
- New widgets obey match visibility and safe-area offsets. Serve meter layout, code, palette, labels and drawing helpers are untouched; SHA-256 source-block checks are in serve-meter-preservation.json.

## Verification
Unity 6000.3.24f1 compiled and entered Play Mode successfully. Editor command `GolfArcade.EditorTools.RallyPopHudCapture.Run` captures the actual tennis scene with deterministic review scores. These are review fixtures, not an automatically played rally. Captures cover multi-set 30/15, deuce 40/40 with a state call, and the original serve meter. Intro letterbox is dismissed; camera post-processing is disabled for HUD review only. No runtime camera or lighting settings were changed.

## Scope
This pass covers the scorebug, shot feedback and point/state calls. Existing contact map, swing timing cue, calibration and phone controller remain as implemented. Native tracking/replay notices retain their current styling. No character, motion, game rules, menus or phone deployment changes.
