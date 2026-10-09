# Crafted tennis clubhouse

`build-clubhouse.py --out <directory>` builds `TennisClubhouse.blend`, FBX and a source manifest outside Unity. It reads the canonical EnvV4 architectural helpers, retains the original two-storey/lookout footprint and role-atlas coordinates, and replaces intersecting glass discs with closed unified arches. Shaped overlapping terracotta tiles and eave joinery are real mesh geometry. The original architectural corner bevel is tagged before tile generation, so fine tile geometry does not inherit an uncontrolled bevel subdivision.

`render-clubhouse.py --source <blend> --out <directory>` renders actual source geometry from front, roof and three-quarter views. CPU source proof is not Unity appearance certification. The live environment assigns the existing palette atlas per role; glazing alone opts into the bounded sky-reflection shader path.

World22 source candidate: `source/TennisClubhouse.blend`. Runtime resource identifiers and importer meta are stable. Court, net, line, actor and collision geometry are not part of this source. Actual captures 17/18 frame the complete live clubhouse bounds from the court-facing side and above the roof. Art and frame-counter acceptance require root's game renders.
