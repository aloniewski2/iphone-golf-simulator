#!/usr/bin/env python3
"""Cross-check the two independent gate implementations: blender/scripts/postcard_check.py (Python mirror) and `run.sh gates` (C#, real Hole.cs).

    python3 Tools/PostcardCheck/gates_diff.py --course Postcards hole08_design hole09_design hole10_design
    python3 Tools/PostcardCheck/gates_diff.py --course Cliffside SCRATCH/hole07_equiv_design.py

Each design module is run through postcard_check.run() (its own LIMITS) and the matching hole (by NUMBER) through the C# gates
(brief limits built into the harness). Compared per gate name: PASS/FAIL must agree; the detail text is compared after dropping
cosmetic differences (140 vs 140.0, hazard tags vs indexes) and any other difference is printed as a note.
`GATES_MATCH PASS|FAIL`; exit 0 only when every shared gate agrees. C#-only gates are listed, not compared.
"""
import argparse
import os
import re
import subprocess
import sys

sys.dont_write_bytecode = True      # never write __pycache__ into blender/scripts (the lead's directory)
HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", ".."))
sys.path.insert(0, os.path.join(REPO, "blender", "scripts"))
import postcard_check as pc  # noqa: E402

RUN = os.path.join(HERE, "run.sh")


def norm(s):
    s = re.sub(r"(\d)\.0\b", r"\1", s)
    s = re.sub(r"[A-Za-z_]+ (\d+\.\d) yd touches the shore", "", s)
    return s.strip()


def cs_gates(course, hole, extra):
    p = subprocess.run([RUN, "gates", "--course", course, "--hole", str(hole)] + extra, capture_output=True, text=True)
    if p.returncode not in (0, 1):
        sys.stderr.write(p.stderr + p.stdout)
        raise SystemExit(f"run.sh gates failed: exit {p.returncode}")
    out = {}
    for line in p.stdout.splitlines():
        m = re.match(r"GATE: (\S+) (PASS|FAIL) - (.*)", line)
        if m:
            out[m.group(1)] = (m.group(2) == "PASS", m.group(3))
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--course", required=True)
    ap.add_argument("designs", nargs="+")
    ap.add_argument("--set", action="append", default=[], help="forwarded to run.sh gates as --set key=value")
    a = ap.parse_args()
    extra = [x for kv in a.set for x in ("--set", kv)]
    bad = 0
    compared = 0
    for d in a.designs:
        h, lines, _ = pc.run(d, fast=False)
        theirs = cs_gates(a.course, h.number, extra)
        print(f"=== hole {h.number} {h.name}: python {len(lines)} gate lines, C# {len(theirs)} gate lines")
        for name, ok, detail in lines:
            if name not in theirs:
                print(f"  MISSING in C#: {name}")
                bad += 1
                continue
            cok, cdetail = theirs[name]
            compared += 1
            if ok != cok:
                bad += 1
                print(f"  DISAGREE {name}: python {'PASS' if ok else 'FAIL'}, C# {'PASS' if cok else 'FAIL'}\n    python: {detail}\n    C#:     {cdetail}")
            elif norm(detail) != norm(cdetail):
                print(f"  same verdict ({'PASS' if ok else 'FAIL'}), detail text differs: {name}\n    python: {detail}\n    C#:     {cdetail}")
            else:
                print(f"  agree {name} {'PASS' if ok else 'FAIL'}")
        only = sorted(set(theirs) - {n for n, _, _ in lines})
        if only:
            print("  C#-only gates (not in the Python): " + ", ".join(only))
    print(f"GATES_MATCH {'PASS' if bad == 0 else 'FAIL'} - {compared} shared gate lines compared, {bad} disagreements")
    return 0 if bad == 0 else 1


if __name__ == "__main__":
    sys.exit(main())
