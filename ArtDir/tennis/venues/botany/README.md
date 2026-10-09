# Tennis coconut crown source

`build-coconut-palm.py` creates an independent near/far coconut species without changing the shared Golf BotanicalKit. PALM/PALM_FAR keep the seven RESORT semantic materials, first UV map, UV2.x leaf pivot .90, and WindWeight.r tip breeze. Blender Z is up; the importer supplies the usual Y-up prototype transform. Placement preserves crown proportions while scaling trunk height.

Rebuild from the repository:

```sh
/Applications/Blender.app/Contents/MacOS/Blender --background --python ArtDir/tennis/venues/botany/build-coconut-palm.py -- --out work/full-visual-overhaul/tennis/world21/palm
```

The editable saved source is `source/TennisCoconutPalm.blend`; its current generated manifest is beside it. Source proof is `proof/full-visual-overhaul/tennis/palm21-v5`, rendered in CPU Cycles at actual 12.5m trunk / 4.65m crown radius. Both prototypes have closed featherlets and trunks with zero boundary/nonmanifold edges. Thin leaf rim normals are split to avoid smoothing upper and lower surfaces into bulbous pinnae. Near 31,876 triangles; far 14,284.

Status: source-reviewed candidate, installed only for real Unity venue art/cost evaluation. Neither source photos nor closed topology certify reference fidelity or phone performance. Shared golf adoption awaits actual acceptance.

## World22 crown source candidate

`build-resort-canopy.py --out <directory>` now provides a standalone reproducible canopy build using local `botanical_primitives.py`, with no dependency on scratch scripts. Near/far crowns retain the original seven cluster envelopes and leaf pivot (.58), but use smoother crown rings and merge degenerate pole edges. This changes actual crown normals/silhouette rather than placing more trees.

The coconut candidate retains the PALM/PALM_FAR names, seven material-role palette, .90 leaf pivot and trunk wind exclusion. World22 widens overlapping inner featherlets without adding triangles relative to World21. This source is a game-render candidate; source renders show fuller connected arches but do not establish reference fidelity or runtime budget. Actual frame costs must include the current near/far choices and shadow submissions.
