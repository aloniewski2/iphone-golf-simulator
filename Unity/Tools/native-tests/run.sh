#!/bin/bash
# Builds and runs the SportsAudioMixer unit tests on macOS: plain, ThreadSanitizer, AddressSanitizer.
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$HERE/../../.." && pwd)"
INC="$ROOT/Unity/Assets/Plugins/iOS"
OUT="${TMPDIR:-/tmp}/sports-audio-mixer-test.$$"
mkdir -p "$OUT"; trap 'rm -rf "$OUT"' EXIT
CXX="${CXX:-clang++}"
common=(-std=c++17 -O1 -g -Wall -Wextra -I "$INC" "$HERE/audio_mixer_test.cpp" -pthread)
echo "=== plain"
"$CXX" "${common[@]}" -o "$OUT/plain" && "$OUT/plain"
for san in thread address; do
  echo "=== -fsanitize=$san"
  if "$CXX" "${common[@]}" -fsanitize=$san -o "$OUT/$san"; then "$OUT/$san"; else echo "sanitizer $san not available, skipped"; fi
done
echo "ALL AUDIO MIXER TESTS PASSED"
