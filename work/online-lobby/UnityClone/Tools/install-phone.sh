#!/bin/zsh
# Explicit local signing only; no developer account or device defaults.
set -euo pipefail
cd "$(dirname "$0")/../.."
: "${SPORTS_TEAM:?Set SPORTS_TEAM to your Apple development team}"
: "${SPORTS_DEVICE:?Set SPORTS_DEVICE to your connected phone identifier}"
SPORTS_OUT=${SPORTS_DERIVED_DATA:-$(mktemp -d /tmp/golfarcade-ios-XXXXXX)}
xcodegen generate --spec project-unity.yml
xcodebuild -project GolfArcadeUnity.xcodeproj -scheme GolfArcade -configuration Release \
  -sdk iphoneos -destination "id=$SPORTS_DEVICE" -derivedDataPath "$SPORTS_OUT" \
  DEVELOPMENT_TEAM="$SPORTS_TEAM" CODE_SIGN_STYLE=Automatic -allowProvisioningUpdates build
SPORTS_APP="$SPORTS_OUT/Build/Products/Release-iphoneos/GolfArcade.app"
[[ -d "$SPORTS_APP" ]] || { print -u2 'No integrated app produced'; exit 1; }
xcrun devicectl device install app --device "$SPORTS_DEVICE" "$SPORTS_APP"
xcrun devicectl device process launch --device "$SPORTS_DEVICE" com.aloniewski.GolfArcade
