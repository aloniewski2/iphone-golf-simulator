#!/bin/bash
# Runs the REAL SportsSessionRecorder.mm on macOS (Metal + AVFoundation) with synthetic frames/audio.
# ~45 s, needs a Mac with a Metal GPU. Uses a tiny UIKit shim (uikit_shim/) for the 3 UIKit symbols it touches.
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$HERE/../../.." && pwd)"
PLUG="$ROOT/Unity/Assets/Plugins/iOS"
OUT="${TMPDIR:-/tmp}/sports-session-harness.$$"; mkdir -p "$OUT"; trap 'rm -rf "$OUT"' EXIT
clang++ -std=c++17 -fobjc-arc -O1 -g ${SANITIZE:+-fsanitize=$SANITIZE} -Wall -Wno-unused-parameter -I "$HERE/uikit_shim" -I "$PLUG" \
  "$PLUG/SportsSessionRecorder.mm" "$HERE/session_recorder_harness.mm" -o "$OUT/harness" \
  -framework Foundation -framework AVFoundation -framework CoreMedia -framework CoreVideo -framework CoreAudio -framework Metal -framework QuartzCore
"$OUT/harness"
