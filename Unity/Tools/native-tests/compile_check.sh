#!/bin/bash
# Syntax/type check of the Unity iOS plugin sources and the Swift-app bridge against the
# real iOS SDK and the exported Unity headers (Unity/Builds/iOS is an older export; fine for this).
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../../.." && pwd)"
SDK="$(xcrun --sdk iphoneos --show-sdk-path)"
CLASSES="$ROOT/Unity/Builds/iOS/Classes"
SHIM="$(mktemp -d)"; trap 'rm -rf "$SHIM"' EXIT
# The exported UnityAppController.h imports <UnityFramework/RenderPluginDelegate.h>; in the
# framework build those headers are public, in the plain export they sit in Classes/PluginBase.
mkdir -p "$SHIM/UnityFramework"
for f in "$CLASSES"/PluginBase/*.h "$CLASSES"/*.h "$ROOT"/Unity/Builds/iOS/UnityFramework/*.h; do ln -sf "$f" "$SHIM/UnityFramework/"; done
check() {
  echo "== $1"
  xcrun --sdk iphoneos clang++ -fsyntax-only -std=c++17 -fobjc-arc -Wall -Wextra -Wno-unused-parameter \
    -target arm64-apple-ios17.0 -isysroot "$SDK" \
    -DUNITY_VERSION_VER=6000 -DUNITY_VERSION_MAJ=3 -DUNITY_VERSION_MIN=24 \
    -include "$CLASSES/Prefix.pch" \
    -I "$SHIM" -I "$CLASSES" -I "$CLASSES/Unity" -I "$ROOT/Unity/Builds/iOS" \
    -I "$ROOT/Unity/Assets/Plugins/iOS" -I "$ROOT/GolfArcade/Unity" \
    "$ROOT/$1"
}
for f in "$@"; do check "$f"; done
if [ $# -eq 0 ]; then
  check Unity/Assets/Plugins/iOS/SportsSessionRecorder.mm
  check Unity/Assets/Plugins/iOS/SportsSessionCapture.mm
  check Unity/Assets/Plugins/iOS/SportsPointRecorder.mm
  check Unity/Assets/Plugins/iOS/SportsPointCapture.mm
  check GolfArcade/Unity/SportsRuntime.mm
fi
echo "compile check OK"
