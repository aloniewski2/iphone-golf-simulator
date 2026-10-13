#!/usr/bin/env python3
"""Draws every feel-test entry from patterns.json as small multiples (SVG, then PNG via Quick Look).

Each panel is what the hand receives over time: a bar for a tap (transient), a filled envelope for a buzz
(continuous, times its intensity curve). Height is intensity; colour is sharpness on one blue ramp,
light = round and dull, dark = crisp. Strikes share a 0-350 ms axis, the longer moments a 0-1600 ms one.
usage: charts.py patterns.json outdir
"""
import json, os, subprocess, sys

RAMP = ["#86b6ef", "#6da7ec", "#5598e7", "#3987e5", "#2a78d6", "#256abf", "#1c5cab", "#184f95", "#104281", "#0d366b"]
SURFACE, INK, INK2, GRID = "#fcfcfb", "#0b0b0b", "#52514e", "#e4e3df"


def shade(sharpness):
    return RAMP[min(len(RAMP) - 1, max(0, int(round(sharpness * (len(RAMP) - 1)))))]


def curve_at(curve, t):
    if not curve:
        return 1.0
    if t <= curve[0][0]:
        return curve[0][1]
    for (t0, v0), (t1, v1) in zip(curve, curve[1:]):
        if t <= t1:
            return v1 if t1 <= t0 else v0 + (v1 - v0) * (t - t0) / (t1 - t0)
    return curve[-1][1]


def panel(demo, x0, y0, w, h, span):
    out = []
    px = lambda t: x0 + 34 + (w - 44) * min(t, span) / span
    py = lambda i: y0 + h - 22 - (h - 52) * i
    out.append(f'<text x="{x0 + 34}" y="{y0 + 16}" font-size="13" font-weight="600" fill="{INK}">{demo["name"]}</text>')
    for i in (0, .5, 1):
        out.append(f'<line x1="{x0 + 34}" x2="{x0 + w - 10}" y1="{py(i):.1f}" y2="{py(i):.1f}" stroke="{GRID}" stroke-width="1"/>')
        out.append(f'<text x="{x0 + 28}" y="{py(i) + 4:.1f}" font-size="10" text-anchor="end" fill="{INK2}">{i:g}</text>')
    ticks = (0, 100, 200, 300) if span < 1 else (0, 400, 800, 1200, 1600)
    for ms in ticks:
        out.append(f'<text x="{px(ms / 1000):.1f}" y="{y0 + h - 6}" font-size="10" text-anchor="middle" fill="{INK2}">{ms}</text>')
    for layer in demo["layers"]:
        for e in layer["events"]:
            if not e["c"]:
                continue
            pts, t, step = [], e["t"], 0.002
            while t <= e["t"] + e["d"] + 1e-6:
                pts.append((px(t), py(e["i"] * curve_at(layer["curve"], t))))
                t += step
            poly = [(px(e["t"]), py(0))] + pts + [(px(e["t"] + e["d"]), py(0))]
            d = " ".join(f"{a:.1f},{b:.1f}" for a, b in poly)
            out.append(f'<polygon points="{d}" fill="{shade(e["s"])}" fill-opacity=".55" stroke="{shade(e["s"])}" stroke-width="1"/>')
    for layer in demo["layers"]:
        for e in layer["events"]:
            if e["c"]:
                continue
            i = e["i"] * curve_at(layer["curve"], e["t"])
            x = px(e["t"])
            out.append(f'<rect x="{x - 2:.1f}" y="{py(i):.1f}" width="4" height="{py(0) - py(i):.1f}" rx="2" fill="{shade(e["s"])}" stroke="{SURFACE}" stroke-width="1"/>')
    return out


def main():
    data = json.load(open(sys.argv[1]))
    outdir = sys.argv[2]
    demos = data["demos"]
    cols, w, h = 4, 330, 200
    rows = (len(demos) + cols - 1) // cols
    W = cols * w + 40
    H = max(W, rows * h + 150)   # square: Quick Look renders the PNG square and would crop a wide page
    svg = [f'<svg xmlns="http://www.w3.org/2000/svg" width="{W}" height="{H}" viewBox="0 0 {W} {H}" font-family="-apple-system, Helvetica, Arial, sans-serif">',
           f'<rect width="{W}" height="{H}" fill="{SURFACE}"/>',
           f'<text x="20" y="34" font-size="22" font-weight="700" fill="{INK}">Hit feel: every pattern in the Settings feel test</text>',
           f'<text x="20" y="58" font-size="13" fill="{INK2}">Height = haptic intensity (0-1) over time in ms. Bar = tap (transient); filled shape = buzz (continuous, shaped by its curve).</text>',
           f'<text x="20" y="76" font-size="13" fill="{INK2}">Colour = sharpness: light = round and dull, dark = crisp. Strikes share 0-350 ms; game, match and gallery moments 0-1600 ms.</text>']
    for k, s in enumerate((0, .33, .66, 1)):
        x = 20 + k * 120
        svg.append(f'<rect x="{x}" y="88" width="14" height="14" rx="3" fill="{shade(s)}"/>')
        svg.append(f'<text x="{x + 20}" y="100" font-size="12" fill="{INK2}">sharpness {s:g}</text>')
    for n, demo in enumerate(demos):
        long = any(e["t"] + (e["d"] if e["c"] else 0) > .36 for l in demo["layers"] for e in l["events"])
        svg += panel(demo, 20 + (n % cols) * w, 120 + (n // cols) * h, w, h, 1.6 if long else .35)
    svg.append("</svg>")
    path = os.path.join(outdir, "hit-feel-patterns.svg")
    open(path, "w").write("\n".join(svg))
    subprocess.run(["qlmanage", "-t", "-s", str(W), "-o", outdir, path], capture_output=True)
    png = path + ".png"
    if os.path.exists(png):
        os.replace(png, os.path.join(outdir, "hit-feel-patterns.png"))
    print("wrote", path)


if __name__ == "__main__":
    main()
