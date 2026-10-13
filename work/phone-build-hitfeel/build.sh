#!/bin/bash
# Hit-feel device build from the snapshot in root/: Unity export -> xcodegen -> Xcode Release.
# Same recipe as work/phone-build-20261012 (serialised with other chats through heavy.sh). Install is separate: install.sh.
R=/Users/adnanyonathan/Documents/Codex/2026-09-20/wh/outputs/iphone-golf-simulator
B=$R/work/phone-build-hitfeel
cd "$B" || exit 1
GOLF_MUTE=1 "$R/work/postcard-look/heavy.sh" hitfeel-export /Applications/Unity/Hub/Editor/6000.3.24f1/Unity.app/Contents/MacOS/Unity -batchmode -quit -projectPath "$B/root/Unity" -buildTarget iOS -job-worker-count 1 --burst-disable-compilation -executeMethod GolfArcade.EditorTools.BuildMenu.BuildIOS -logFile "$B/unity-export.log"
echo "unity export exit $?"
cat "$B/root/Unity/Library/BuildResults/ios.txt" 2>/dev/null
grep -E "error CS[0-9]+" "$B/unity-export.log" | head -20
(cd "$B/root" && xcodegen generate --spec project-unity.yml > "$B/xcodegen.log" 2>&1) || { echo "xcodegen failed"; exit 1; }
"$R/work/postcard-look/heavy.sh" hitfeel-xcode xcodebuild -project "$B/root/GolfArcadeUnity.xcodeproj" -scheme GolfArcade -configuration Release -sdk iphoneos -destination generic/platform=iOS -derivedDataPath "$B/DerivedData" DEVELOPMENT_TEAM=9VP5B3Q5VQ CODE_SIGN_STYLE=Automatic -allowProvisioningUpdates build > "$B/xcode-build.log" 2>&1
echo "xcodebuild exit $?"
grep -E "error:|BUILD (SUCCEEDED|FAILED)" "$B/xcode-build.log" | tail -15
