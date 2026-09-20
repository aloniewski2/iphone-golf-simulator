#!/bin/zsh
# Sign the Unity-generated Xcode project (Builds/iOS, from Golf Arcade → Build iOS Xcode Project)
# for a phone and install + launch it. Team, bundle id and device are overrides so nothing about
# signing is committed; defaults are Andrew's phone and the one profile on his Mac.
#
#   Unity/Tools/install-phone.sh
#   TEAM=XXXXXXXXXX BUNDLE=com.example.golf DEVICE=<CoreDevice id> Unity/Tools/install-phone.sh
set -e
cd "$(dirname "$0")/../Builds/iOS"
TEAM=${TEAM:-R45WRD83AP}
BUNDLE=${BUNDLE:-com.aloniewski.iphonegolfsim}
DEVICE=${DEVICE:-B94032C7-207E-5C11-BD77-8AFE448605A4}
OUT=${OUT:-$(mktemp -d /tmp/golfarcade-unity-XXXXXX)}

# Only the app target may take the bundle id: an xcodebuild-wide override would give
# UnityFramework the same id and the phone refuses the install (DuplicateIdentifier).
sed -i '' "s/PRODUCT_BUNDLE_IDENTIFIER = com\.aloniewski\.golfarcade\.unity;/PRODUCT_BUNDLE_IDENTIFIER = $BUNDLE;/" Unity-iPhone.xcodeproj/project.pbxproj

xcodebuild -project Unity-iPhone.xcodeproj -target Unity-iPhone -configuration Debug -sdk iphoneos \
  SYMROOT="$OUT/sym" OBJROOT="$OUT/obj" DEVELOPMENT_TEAM="$TEAM" CODE_SIGN_STYLE=Automatic build \
  | grep -E "error:|BUILD (SUCCEEDED|FAILED)" || true
APP="$OUT/sym/Debug-iphoneos/GolfArcade.app"
[ -d "$APP" ] || { echo "no app built"; exit 1; }
xcrun devicectl device install app --device "$DEVICE" "$APP" | grep -E "installed|bundleID|ERROR" || true
xcrun devicectl device process launch --terminate-existing --device "$DEVICE" "$BUNDLE" | grep -iE "launched|error"
