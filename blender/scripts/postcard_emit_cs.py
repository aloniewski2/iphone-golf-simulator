#!/usr/bin/env python3
"""Print the C# `Course.Postcards()` block for Hole.cs from the three design modules.

    python3 blender/scripts/postcard_emit_cs.py > /tmp/postcards_block.cs
    python3 blender/scripts/postcard_emit_cs.py --check Unity/Assets/Scripts/Course/Hole.cs

The design modules (hole08/09/10_design.py) are the single source of truth: the Blender build reads
the same numbers, rounded to 0.1 yd, so the mesh and the scoring cannot drift. `--check FILE` parses
the numbers back out of Hole.cs and compares them with the design modules (exit 1 on any drift).
"""
import importlib
import math
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
MODULES = ["hole08_design", "hole09_design", "hole10_design"]


def n(v):
    s = ("%.1f" % v).rstrip("0").rstrip(".")
    return "0" if s in ("-0", "") else s


def pts(points, per_line=6, indent=" " * 24):
    items = [f"P({n(x)}, {n(d)})" for x, d in points]
    rows = [", ".join(items[i:i + per_line]) + "," for i in range(0, len(items), per_line)]
    return ("\n" + indent).join(rows)


def hole_block(m):
    cl = ", ".join(f"P({n(x)}, {n(d)})" for x, d in m.CENTERLINE)
    hz = []
    for h in m.HAZARDS:
        fn = "Water" if h["kind"] == "water" else "Bunker"
        hz.append(f"{fn}({n(h['x'])}, {n(h['d'])}, {n(h['width'])}, {n(h['length'])})")
    tags = "  // " + "; ".join(f"{h['tag']}" for h in m.HAZARDS if h.get("tag")) if any(h.get("tag") for h in m.HAZARDS) else ""
    haz_lines = ("\n" + " " * 24).join(hz[i] + "," for i in range(len(hz)))
    return f"""                new Hole
                {{
                    // {m.NAME}: {len(m.SHORE)}-point shore, centerline {sum(math.dist(m.CENTERLINE[i-1], m.CENTERLINE[i]) for i in range(1, len(m.CENTERLINE))):.1f} yd, play surface flat at {m.PLAY_Z:g} m in blender/hole_{m.NUMBER:02d}.blend
                    Number = {m.NUMBER}, Par = {m.PAR},
                    Centerline = new[] {{ {cl} }},
                    FairwayWidth = {n(m.FAIRWAY_WIDTH)}, GreenRadius = {n(m.GREEN_RADIUS)}, RoughWidth = {n(m.ROUGH_WIDTH)},
                    Hazards = new[]
                    {{
                        {haz_lines}
                    }},
                    Shore = new[]
                    {{
                        {pts(m.SHORE, indent=" " * 24)}
                    }},
                }},"""


def emit():
    mods = [importlib.import_module(x) for x in MODULES]
    body = "\n".join(hole_block(m) for m in mods)
    return f"""        /// Postcards: three scenic one-height holes (8 Needle, 9 Split, 10 Crater) on the same scoring
        /// rules as Cliffside. Every number below is printed by blender/scripts/postcard_emit_cs.py from
        /// blender/scripts/hole08/09/10_design.py, the same data the Blender meshes were built from.
        /// The play surface is one height; cliffs, lava and the sea sit below it as scenery.
        public static Course Postcards() => new()
        {{
            Name = "Postcards",
            Holes = new[]
            {{
{body}
            }},
        }};

        static CourseHazard Water(double x, double d, double w, double l) => new(HazardKind.Water, x, d, w, l);
"""


def parse_holecs(path):
    """Pull (Number, centerline, shore, hazards) back out of Course.Postcards() in Hole.cs."""
    txt = open(path).read()
    i = txt.index("public static Course Postcards()")
    j = txt.index("static CourseHazard Water(", i)
    block = txt[i:j]
    holes = []
    for part in block.split("new Hole")[1:]:
        num = int(re.search(r"Number = (\d+)", part).group(1))
        pair = r"P\((-?[\d.]+), (-?[\d.]+)\)"
        cl = re.search(r"Centerline = new\[\] \{(.*?)\}", part, re.S).group(1)
        shore = re.search(r"Shore = new\[\]\s*\{(.*?)\}\s*,\s*\}", part, re.S).group(1)
        hz = re.findall(r"(Water|Bunker)\((-?[\d.]+), (-?[\d.]+), (-?[\d.]+), (-?[\d.]+)\)", part)
        holes.append(dict(
            number=num,
            center=[(float(a), float(b)) for a, b in re.findall(pair, cl)],
            shore=[(float(a), float(b)) for a, b in re.findall(pair, shore)],
            hazards=[(k.lower(), float(a), float(b), float(c), float(d)) for k, a, b, c, d in hz],
            fw=float(re.search(r"FairwayWidth = ([\d.]+)", part).group(1)),
            gr=float(re.search(r"GreenRadius = ([\d.]+)", part).group(1)),
            rough=float(re.search(r"RoughWidth = ([\d.]+)", part).group(1)),
            par=int(re.search(r"Par = (\d+)", part).group(1)),
        ))
    return holes


def check(path):
    cs = {h["number"]: h for h in parse_holecs(path)}
    bad = []
    for name in MODULES:
        m = importlib.import_module(name)
        h = cs.get(m.NUMBER)
        if h is None:
            bad.append(f"hole {m.NUMBER} missing from Hole.cs")
            continue
        want = dict(center=[tuple(p) for p in m.CENTERLINE], shore=[tuple(p) for p in m.SHORE],
                    hazards=[(x["kind"], x["x"], x["d"], x["width"], x["length"]) for x in m.HAZARDS],
                    fw=m.FAIRWAY_WIDTH, gr=m.GREEN_RADIUS, rough=m.ROUGH_WIDTH, par=m.PAR)
        for k, v in want.items():
            got = h[k]
            if isinstance(v, list):
                same = len(v) == len(got) and all(all(abs(a - b) < 1e-6 if not isinstance(a, str) else a == b for a, b in zip(p, q)) for p, q in zip(v, got))
            else:
                same = abs(v - got) < 1e-6
            if not same:
                bad.append(f"hole {m.NUMBER}: {k} differs between {name}.py and Hole.cs")
    print("HOLE_CS_MATCHES_DESIGN", "FAIL: " + "; ".join(bad) if bad else "PASS")
    return 1 if bad else 0


if __name__ == "__main__":
    if len(sys.argv) >= 3 and sys.argv[1] == "--check":
        sys.exit(check(sys.argv[2]))
    sys.stdout.write(emit())
