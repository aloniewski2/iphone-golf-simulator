#!/bin/bash
# Inspect an actual optimized product, not just source imports or build settings.
# Usage: bash scripts/verify-native-boundary.sh <GolfArcade executable> <GolfArcade.SwiftFileList>
set -euo pipefail
if [[ $# != 2 || ! -f "$1" || ! -f "$2" ]]; then
  echo "Usage: $0 <built GolfArcade executable> <compiled GolfArcade.SwiftFileList>" >&2
  exit 2
fi
executable="$1"
source_list="$2"
legacy='/(AvatarRig|GolferSkin|ResortGolferSkin|SunwardAsset|CourseScene|CourseSurfaceDetail|GolfImpactEffects|GolfRenderAudit|CourseScreen)\.swift$'
if rg -n "$legacy" "$source_list"; then
  echo "FAIL: legacy renderer source compiled into this product" >&2
  exit 1
fi
libraries=$(xcrun otool -L "$executable")
if printf '%s\n' "$libraries" | rg 'SceneKit\.framework/SceneKit'; then
  echo "FAIL: direct SceneKit framework link" >&2
  exit 1
fi
symbols=$(xcrun nm -u "$executable")
if printf '%s\n' "$symbols" | rg '(_OBJC_(CLASS|METACLASS)_\$_SCN|\$s8SceneKit)'; then
  echo "FAIL: unresolved SceneKit rendering API references" >&2
  exit 1
fi
if ! printf '%s\n' "$libraries" | rg -q 'RealityKit\.framework/RealityKit'; then
  echo "FAIL: expected RealityKit framework link is absent" >&2
  exit 1
fi
echo "PASS: nine legacy files excluded; RealityKit linked; no direct SceneKit framework or rendering API references."
if printf '%s\n' "$libraries" | rg -q 'libswiftSceneKit'; then
  echo "NOTE: SDK-generated weak Swift SceneKit compatibility overlay remains; this is not a SceneKit renderer."
fi
