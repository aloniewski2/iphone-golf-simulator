#!/bin/zsh
set -euo pipefail
cd "$(dirname "$0")/../.."
if [[ ! -d Unity/Builds/iOS/Unity-iPhone.xcodeproj ]]; then
  print -u2 'Export from Unity: Golf Arcade > Build iOS Xcode Project, then rerun.'
  exit 1
fi
xcodegen generate --spec project-unity.yml
# GolfArcadeUnity.xcworkspace is checked in; XcodeGen regenerates its native project.
xcodebuild -project GolfArcadeUnity.xcodeproj -scheme GolfArcade -configuration Release \
  -sdk iphoneos -derivedDataPath "${SPORTS_DERIVED_DATA:-/tmp/golfarcade-integrated}" \
  CODE_SIGNING_ALLOWED=NO build
