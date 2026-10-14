#!/usr/bin/env python3
"""Fails fast, with a readable message, when the netsim stub of HeroTennisDriver.Clip drifts from the game.

NetSim links TennisEmotes.cs (and friends) from Unity/Assets but cannot compile HeroTennisDriver.cs (it is a
MonoBehaviour), so Shim/UnityShim.cs carries a stub enum with the clip names the linked sources use. Add a clip to
the game and forget the stub, and `dotnet test` dies with a wall of CS0117 errors. This runs first and says what to add.

    python3 Tools/netsim/check_shim_enums.py
"""
import glob
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
DRIVER = os.path.join(ROOT, "Unity/Assets/Scripts/Tennis/HeroTennisDriver.cs")
SHIM = os.path.join(HERE, "Shim/UnityShim.cs")
CSPROJ = os.path.join(HERE, "NetSim.csproj")
VARS = {"Scripts": "Unity/Assets/Scripts/", "EditModeTests": "Unity/Assets/Tests/EditMode/"}


def read(path):
    with open(path, encoding="utf-8") as f:
        return f.read()


def enum_names(text, pattern, what):
    m = re.search(pattern, text, re.S)
    if not m:
        sys.exit(f"check_shim_enums: could not find {what}")
    body = re.sub(r"//[^\n]*", "", m.group(1))
    return [n.strip() for n in body.split(",") if n.strip()]


def linked_sources():
    paths = set()
    for var, rel in re.findall(r'Compile Include="\$\((\w+)\)([^"]+)"', read(CSPROJ)):
        if var in VARS:
            paths.update(glob.glob(os.path.join(ROOT, VARS[var], rel)))
    return sorted(paths)


real = set(enum_names(read(DRIVER), r"enum\s+Clip\s*\{(.*?)\}", "'enum Clip' in HeroTennisDriver.cs"))
stub = set(enum_names(read(SHIM), r"class\s+HeroTennisDriver\s*\{\s*public\s+enum\s+Clip\s*\{(.*?)\}",
                      "the HeroTennisDriver.Clip stub in Shim/UnityShim.cs"))

used = {}
for path in linked_sources():
    for name in re.findall(r"HeroTennisDriver\.Clip\.(\w+)", read(path)):
        used.setdefault(name, os.path.relpath(path, ROOT))

problems = []
missing = sorted(n for n in used if n not in stub)
if missing:
    problems.append("The linked sources use clips the netsim stub does not have. Add to the enum in "
                    "Tools/netsim/Shim/UnityShim.cs:\n  " + ", ".join(f"{n} (used in {used[n]})" for n in missing))
bogus = sorted(n for n in stub if n not in real)
if bogus:
    problems.append("The netsim stub has clips that no longer exist in HeroTennisDriver.Clip (renamed or removed). "
                    "Remove from Tools/netsim/Shim/UnityShim.cs:\n  " + ", ".join(bogus))

if problems:
    sys.exit("check_shim_enums: FAIL\n" + "\n".join(problems))
print(f"check_shim_enums: OK ({len(used)} clip names used by linked sources, {len(stub)} in stub, {len(real)} in game)")
