#!/usr/bin/env python3
"""Summarize local xctrace time-profile XML; never uploads profiling data."""
import argparse
import collections
import json
from pathlib import Path
import subprocess
import xml.etree.ElementTree as ET

parser = argparse.ArgumentParser()
parser.add_argument("xml")
parser.add_argument("--dsym", help="UUID-matched app DWARF file for unresolved optimized frames")
parser.add_argument("--output", help="Preserve the local analysis JSON at this path")
args = parser.parse_args()
root = ET.parse(args.xml).getroot()
refs = {node.get("id"): node for node in root.iter() if node.get("id")}


def resolve(node):
    return refs[node.get("ref")] if node.get("ref") else node


symbol_names = {}
if args.dsym:
    app = next(node for node in root.iter("binary") if node.get("name") == "GolfArcade")
    uuids = subprocess.check_output(["dwarfdump", "--uuid", args.dsym], text=True)
    if app.get("UUID") not in uuids:
        raise ValueError("App dSYM UUID does not match this trace")
    addresses = set()
    for frame in root.iter("frame"):
        binary = frame.find("binary")
        if binary is not None and resolve(binary).get("name") == "GolfArcade":
            if not frame.get("name") or frame.get("name", "").startswith("0x"):
                addresses.add(frame.get("addr"))
    addresses = sorted(addresses)
    for offset in range(0, len(addresses), 128):
        chunk = addresses[offset:offset + 128]
        names = subprocess.check_output([
            "atos", "-arch", app.get("arch"), "-o", args.dsym, "-l", app.get("load-addr"), *chunk
        ], text=True).splitlines()
        if len(names) != len(chunk):
            raise ValueError("Unexpected atos response length")
        symbol_names.update(zip(chunk, names))


def frame_name(frame):
    return symbol_names.get(frame.get("addr"), frame.get("name", frame.get("addr", "unknown")))


leaves = collections.Counter()
inclusive = collections.Counter()
app_inclusive = collections.Counter()
bins = collections.defaultdict(collections.Counter)
main_ms = 0
rows = 0
unknown_app_ms = 0
for row in root.iter("row"):
    values = [resolve(node) for node in row]
    if len(values) != 7:
        raise ValueError("Expected the time-profile table's seven columns")
    timestamp, thread, process, core, state, weight, backtrace = values
    if "Main Thread" not in thread.get("fmt", ""):
        continue
    if "GolfArcade" not in process.get("fmt", ""):
        continue
    duration = int(weight.text) / 1_000_000
    second = int(int(timestamp.text) / 1_000_000_000)
    frames = [resolve(node) for node in backtrace if node.tag == "frame"]
    if not frames:
        continue
    rows += 1
    main_ms += duration
    names = [frame_name(frame) for frame in frames]
    leaves[names[0]] += duration
    for name in set(names):
        inclusive[name] += duration
    app_names = set()
    for frame in frames:
        binary = frame.find("binary")
        if binary is not None and resolve(binary).get("name") == "GolfArcade":
            name = frame_name(frame)
            app_names.add(name)
    if any(name.startswith("0x") or name == "unknown" for name in app_names):
        unknown_app_ms += duration
    for name in app_names:
        app_inclusive[name] += duration
    bins[second]["mainSampleMS"] += duration
    for label, fragment in [
        ("assetLoaderMS", "NativeAssetLoader"),
        ("grassInstallMS", "NativeGrassSurface.install"),
        ("grassUpdateMS", "NativeGrassSurface.update"),
        ("materialParameterMS", "MaterialParameterBlock"),
        ("entityCloneMS", "clone"),
        ("metalSimulatorMS", "MTLSim"),
        ("nativeSystemsMS", "GolfArcade."),
    ]:
        if any(fragment in name for name in names):
            bins[second][label] += duration


def top(counter, count=25):
    return [{"symbol": name, "sampleMS": value,
             "percentOfMainSamples": round(100 * value / main_ms, 2)}
            for name, value in counter.most_common(count)]


summary = json.dumps({
    "source": args.xml,
    "note": "Sampling weights, not exact durations; inclusive symbols overlap. Main thread only.",
    "mainRows": rows,
    "mainSampleMS": main_ms,
    "unknownAppSampleMS": unknown_app_ms,
    "categories": {key: sum(bucket[key] for bucket in bins.values())
                   for key in sorted({key for bucket in bins.values() for key in bucket})},
    "leaves": top(leaves),
    "inclusive": top(inclusive, 40),
    "appInclusive": top(app_inclusive, 30),
    "seconds": [{"second": second, **counts} for second, counts in sorted(bins.items())],
}, indent=2)
if args.output:
    Path(args.output).write_text(summary + "\n")
print(summary)
