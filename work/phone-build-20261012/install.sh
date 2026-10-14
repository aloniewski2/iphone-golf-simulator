#!/bin/bash
# wait for the iPhone, then install + launch the build
D=00008150-00041CD92287801C
APP="/Users/adnanyonathan/Documents/Codex/2026-09-20/wh/outputs/iphone-golf-simulator/work/phone-build-20261012/DerivedData/Build/Products/Release-iphoneos/GolfArcade.app"
until xcrun devicectl list devices 2>/dev/null | grep $D | grep -qv unavailable; do sleep 5; done
echo "phone available: $(date)"
for i in 1 2 3; do
  xcrun devicectl device install app --device $D "$APP" --json-output "/Users/adnanyonathan/Documents/Codex/2026-09-20/wh/outputs/iphone-golf-simulator/work/phone-build-20261012/install.json" > "/Users/adnanyonathan/Documents/Codex/2026-09-20/wh/outputs/iphone-golf-simulator/work/phone-build-20261012/install.log" 2>&1 && { echo "install ok"; break; }
  echo "install attempt $i failed:"; tail -4 "/Users/adnanyonathan/Documents/Codex/2026-09-20/wh/outputs/iphone-golf-simulator/work/phone-build-20261012/install.log"; sleep 5
done
xcrun devicectl device process launch --device $D --terminate-existing com.aloniewski.GolfArcade --json-output "/Users/adnanyonathan/Documents/Codex/2026-09-20/wh/outputs/iphone-golf-simulator/work/phone-build-20261012/launch.json" > "/Users/adnanyonathan/Documents/Codex/2026-09-20/wh/outputs/iphone-golf-simulator/work/phone-build-20261012/launch.log" 2>&1 && echo "launch ok" || { echo "launch failed:"; tail -6 "/Users/adnanyonathan/Documents/Codex/2026-09-20/wh/outputs/iphone-golf-simulator/work/phone-build-20261012/launch.log"; }
