#!/bin/bash
# Regenerate the app project in the clone (picks up new Swift files) and build it for the iPhone

cd "/Users/adnanyonathan/Documents/Codex/2026-09-20/wh/outputs/iphone-golf-simulator/work/phone-build-20261012/root"
xcodegen generate --spec project-unity.yml > "/Users/adnanyonathan/Documents/Codex/2026-09-20/wh/outputs/iphone-golf-simulator/work/phone-build-20261012/xcodegen.log" 2>&1 || { echo "xcodegen failed"; exit 1; }
"/Users/adnanyonathan/Documents/Codex/2026-09-20/wh/outputs/iphone-golf-simulator/work/postcard-look/heavy.sh" phone-build-20261012-xcode xcodebuild -project GolfArcadeUnity.xcodeproj -scheme GolfArcade -configuration Release -sdk iphoneos -destination generic/platform=iOS -derivedDataPath "/Users/adnanyonathan/Documents/Codex/2026-09-20/wh/outputs/iphone-golf-simulator/work/phone-build-20261012/DerivedData" DEVELOPMENT_TEAM=9VP5B3Q5VQ CODE_SIGN_STYLE=Automatic -allowProvisioningUpdates build > "/Users/adnanyonathan/Documents/Codex/2026-09-20/wh/outputs/iphone-golf-simulator/work/phone-build-20261012/xcode-build.log" 2>&1
echo "xcodebuild exit: $?"
