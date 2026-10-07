#!/usr/bin/env python3
"""Equivalence proof: blender/scripts/postcard_check.py `Hole.lie_at` (the Python mirror) against the REAL C# `Hole.LieAt`.

    python3 Tools/PostcardCheck/equiv.py --design SCRATCH/hole07_equiv_design.py --course Cliffside --hole 7 --step 2 [--random 200000]
    python3 Tools/PostcardCheck/equiv.py --design hole08_design --course Postcards --hole 8 --step 1 --random 100000

`--design` is a design module (a .py path, or a module name importable from blender/scripts, PYTHONPATH or the cwd) in the
schema of blender/scripts/POSTCARDS_README.md (course yards). The C# side is `run.sh lies` (every grid cell) and, with
`--random N`, `run.sh lies --points FILE` (N seeded random points, a third of them within 0.3 yd of an exact boundary:
hazard ellipses, fairway/rough offsets, green and tee radii, so the `<=` comparisons are exercised).
Prints `EQUIV PASS|FAIL cells=.. mismatches=..`; every mismatch is diagnosed down to the sub-expression that differs
(hazard ellipse / OnLand / green / tee / fairway / rough) using `run.sh probe` on the C# side. Exit code 0 only when there are 0 mismatches.
Pure stdlib python3, no numpy.
"""
import argparse
import importlib
import importlib.util
import math
import os
import random
import re
import subprocess
import sys

sys.dont_write_bytecode = True      # never write __pycache__ into blender/scripts (the lead's directory)
HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", ".."))
sys.path.insert(0, os.path.join(REPO, "blender", "scripts"))
import postcard_check as pc  # noqa: E402  (the lead's Python mirror; read-only here)

RUN = os.path.join(HERE, "run.sh")
LETTER = {pc.TEE: "T", pc.FAIRWAY: "F", pc.ROUGH: "R", pc.BUNKER: "B", pc.GREEN: "G", pc.WATER: "W", pc.OOB: "O"}


def load_design(spec):
    if spec.endswith(".py") or os.sep in spec:
        s = importlib.util.spec_from_file_location(os.path.basename(spec)[:-3], spec)
        mod = importlib.util.module_from_spec(s)
        s.loader.exec_module(mod)
        return mod
    sys.path.insert(0, os.getcwd())
    return importlib.import_module(spec)


def run_cs(args, env_extra=None):
    env = dict(os.environ)
    if env_extra:
        env.update(env_extra)
    p = subprocess.run([RUN] + args, capture_output=True, text=True, env=env)
    if p.returncode not in (0, 1):
        sys.stderr.write(p.stderr)
        sys.stderr.write(p.stdout)
        raise SystemExit(f"run.sh {' '.join(args)} failed with exit code {p.returncode}")
    return p.stdout


def py_probe(h, p):
    """Every boolean sub-expression of Hole.LieAt, evaluated by the Python mirror."""
    out = {"hazards": [Hole_in(h, k, p) for k in h.hazards], "onland": h.on_land(p)}
    out["green"] = math.dist(p, h.pin) <= h.gr
    out["tee"] = math.dist(p, h.tee) <= pc.CUP_TEE_RADIUS
    off = h.dist_center(p)
    out["offset"] = off
    out["fairway"] = off <= h.fw / 2
    out["rough"] = off <= h.fw / 2 + h.rough
    return out


def Hole_in(h, k, p):
    return h.in_ellipse(k, p)


def cs_probe(course, hole, p):
    txt = run_cs(["probe", "--course", course, "--hole", str(hole), "--at", f"{p[0]!r},{p[1]!r}"])
    d = {"hazards": [], "txt": txt}
    for m in re.finditer(r"hazard\[\d+\] \w+: .* -> Contains (True|False)", txt):
        d["hazards"].append(m.group(1) == "True")
    d["onland"] = re.search(r"OnLand \(even-odd shore test\) = (True|False)", txt).group(1) == "True"
    d["green"] = re.search(r"green = (True|False)", txt).group(1) == "True"
    d["tee"] = re.search(r"tee = (True|False)", txt).group(1) == "True"
    m = re.search(r"centerline offset = (\S+); fairway <= \S+: (True|False); rough <= \S+: (True|False)", txt)
    d["offset"], d["fairway"], d["rough"] = float(m.group(1)), m.group(2) == "True", m.group(3) == "True"
    return d


def diagnose(h, course, hole, p, cs_letter, py_letter):
    a, b = py_probe(h, p), cs_probe(course, hole, p)
    diffs = []
    for i, (x, y) in enumerate(zip(a["hazards"], b["hazards"])):
        if x != y:
            diffs.append(f"hazard[{i}] ellipse test: python {x}, C# {y}")
    if a["onland"] != b["onland"]:
        diffs.append(f"on-land (even-odd polygon) test: python {a['onland']}, C# {b['onland']}")
    for k in ("green", "tee", "fairway", "rough"):
        if a[k] != b[k]:
            diffs.append(f"{k} test: python {a[k]}, C# {b[k]}")
    if abs(a["offset"] - b["offset"]) > 1e-9:
        diffs.append(f"centerline offset: python {a['offset']!r}, C# {b['offset']!r}")
    return f"({p[0]!r}, {p[1]!r}) C#={cs_letter} python={py_letter}; differing sub-expression: " + ("; ".join(diffs) if diffs else "none found (a floating point tie?)")


def boundary_points(h, rng, n):
    """Points within 0.3 yd of an exact decision boundary of LieAt."""
    pts = []
    xs = [p[0] for p in h.shore]
    ds = [p[1] for p in h.shore]
    for _ in range(n):
        kind = rng.randrange(6)
        if kind == 0 and h.hazards:
            k = rng.choice(h.hazards)
            t = rng.uniform(0, 2 * math.pi)
            s = 1 + rng.uniform(-0.3, 0.3) / max(k[3] / 2, 1)
            pts.append((k[1] + k[3] / 2 * math.cos(t) * s, k[2] + k[4] / 2 * math.sin(t) * s))
        elif kind == 1:
            t = rng.uniform(0, 2 * math.pi)
            r = h.gr + rng.uniform(-0.3, 0.3)
            pts.append((h.pin[0] + r * math.cos(t), h.pin[1] + r * math.sin(t)))
        elif kind == 2:
            t = rng.uniform(0, 2 * math.pi)
            r = pc.CUP_TEE_RADIUS + rng.uniform(-0.3, 0.3)
            pts.append((h.tee[0] + r * math.cos(t), h.tee[1] + r * math.sin(t)))
        elif kind == 3:
            i = rng.randrange(len(h.shore))
            a, b = h.shore[i], h.shore[(i + 1) % len(h.shore)]
            u = rng.random()
            pts.append((a[0] + (b[0] - a[0]) * u + rng.uniform(-0.3, 0.3), a[1] + (b[1] - a[1]) * u + rng.uniform(-0.3, 0.3)))
        elif kind == 4:
            i = rng.randrange(1, len(h.center))
            a, b = h.center[i - 1], h.center[i]
            u = rng.random()
            c = (a[0] + (b[0] - a[0]) * u, a[1] + (b[1] - a[1]) * u)
            ln = max(math.dist(a, b), 1e-9)
            nx, nd = -(b[1] - a[1]) / ln, (b[0] - a[0]) / ln
            side = h.fw / 2 + (h.rough if rng.random() < 0.5 else 0) + rng.uniform(-0.3, 0.3)
            side *= rng.choice((-1, 1))
            pts.append((c[0] + nx * side, c[1] + nd * side))
        else:
            pts.append((rng.uniform(min(xs) - 40, max(xs) + 40), rng.uniform(min(ds) - 40, max(ds) + 40)))
    return pts


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--design", required=True)
    ap.add_argument("--course", required=True)
    ap.add_argument("--hole", type=int, required=True)
    ap.add_argument("--step", type=float, default=2.0)
    ap.add_argument("--random", type=int, default=0)
    ap.add_argument("--seed", type=int, default=20261003)
    ap.add_argument("--show", type=int, default=6, help="mismatches to diagnose")
    ap.add_argument("--dump-dir", help="write both full grids (cs_lies.txt from the real C#, py_lies.txt from the Python mirror) and run `diff` on them")
    a = ap.parse_args()

    mod = load_design(a.design)
    h = pc.Hole(mod)
    if h.number != a.hole:
        print(f"note: design NUMBER {h.number} != --hole {a.hole}")
    total = bad = 0
    bad_list = []

    # ---- 1. every grid cell
    out = run_cs(["lies", "--course", a.course, "--hole", str(a.hole), "--step", f"{a.step:g}"])
    lines = out.splitlines()
    hdr = re.match(r"# HOLE (\d+) step (\S+) x0 (\S+) d0 (\S+) nx (\d+) nd (\d+)", lines[0])
    if not hdr:
        raise SystemExit("unexpected lies output: " + lines[0])
    step, x0, d0, nx, nd = float(hdr.group(2)), float(hdr.group(3)), float(hdr.group(4)), int(hdr.group(5)), int(hdr.group(6))
    rows = lines[1:]
    assert len(rows) == nd and all(len(r) == nx for r in rows), f"grid shape {len(rows)}x{len(rows[0])} != {nd}x{nx}"
    counts = {}
    py_rows = []
    for j, row in enumerate(rows):
        py_row = []
        for i, ch in enumerate(row):
            p = (x0 + i * step, d0 + j * step)
            mine = LETTER[h.lie_at(p)]
            py_row.append(mine)
            counts[ch] = counts.get(ch, 0) + 1
            total += 1
            if mine != ch:
                bad += 1
                bad_list.append((p, ch, mine))
        py_rows.append("".join(py_row))
    if a.dump_dir:
        os.makedirs(a.dump_dir, exist_ok=True)
        head = lines[0]
        cs_path, py_path = os.path.join(a.dump_dir, "cs_lies.txt"), os.path.join(a.dump_dir, "py_lies.txt")
        with open(cs_path, "w") as f:
            f.write("\n".join(lines) + "\n")
        with open(py_path, "w") as f:
            f.write("\n".join([head] + py_rows) + "\n")
        d = subprocess.run(["diff", cs_path, py_path], capture_output=True, text=True)
        print(f"EQUIV dump: wrote {cs_path} and {py_path} ({os.path.getsize(cs_path)} bytes each); `diff` exit code {d.returncode} ({'identical' if d.returncode == 0 else 'DIFFERENT'})")
    print(f"EQUIV grid: hole {a.hole} course {a.course} step {step:g}: {nx} x {nd} = {nx * nd} cells compared, {bad} mismatches; lies seen {dict(sorted(counts.items()))}")

    # ---- 2. random + boundary points
    if a.random:
        rng = random.Random(a.seed)
        xs = [p[0] for p in h.shore]
        ds = [p[1] for p in h.shore]
        n_b = a.random // 3
        pts = boundary_points(h, rng, n_b)
        pts += [(rng.uniform(min(xs) - 60, max(xs) + 60), rng.uniform(min(ds) - 60, max(ds) + 60)) for _ in range(a.random - n_b)]
        os.makedirs(os.path.join(HERE, ".build"), exist_ok=True)
        pf = os.path.join(HERE, ".build", f"equiv_points_{a.hole}.txt")
        with open(pf, "w") as f:
            for p in pts:
                f.write(f"{p[0]!r} {p[1]!r}\n")
        out = run_cs(["lies", "--course", a.course, "--hole", str(a.hole), "--points", pf]).splitlines()
        letters = [l for l in out[1:] if l]
        assert len(letters) == len(pts), f"{len(letters)} letters for {len(pts)} points"
        rbad = 0
        rcounts = {}
        for p, ch in zip(pts, letters):
            mine = LETTER[h.lie_at(p)]
            rcounts[ch] = rcounts.get(ch, 0) + 1
            if mine != ch:
                rbad += 1
                bad_list.append((p, ch, mine))
        total += len(pts)
        bad += rbad
        print(f"EQUIV random: {len(pts)} points (seed {a.seed}, {n_b} within 0.3 yd of an exact boundary), {rbad} mismatches; lies seen {dict(sorted(rcounts.items()))}")

    for p, ch, mine in bad_list[: a.show]:
        print("  MISMATCH " + diagnose(h, a.course, a.hole, p, ch, mine))
    print(f"EQUIV {'PASS' if bad == 0 else 'FAIL'} cells={total} mismatches={bad}")
    return 0 if bad == 0 else 1


if __name__ == "__main__":
    sys.exit(main())
