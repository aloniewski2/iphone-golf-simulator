#!/bin/bash
# Checks the built app exports what Settings reaches through dlsym, then installs it on Adnan's iPhone (no forced launch).
B=/Users/adnanyonathan/Documents/Codex/2026-09-20/wh/outputs/iphone-golf-simulator/work/phone-build-hitfeel
D=00008150-00041CD92287801C
APP="$B/DerivedData/Build/Products/Release-iphoneos/GolfArcade.app"
F="$APP/Frameworks/UnityFramework.framework/UnityFramework"
for s in GolfHaptics_Configure GolfHaptics_DemoCount GolfHaptics_Demo; do
  nm -gU "$F" | grep -q " T _$s$" && echo "export $s: ok" || { echo "export $s: MISSING"; exit 1; }
done
for i in 1 2 3; do
  xcrun devicectl device install app --device $D "$APP" --json-output "$B/install.json" > "$B/install.log" 2>&1 && { echo "install ok $(date)"; exit 0; }
  echo "install attempt $i failed:"; tail -4 "$B/install.log"; sleep 5
done
exit 1
