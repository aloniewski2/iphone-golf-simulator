#!/bin/bash
# Unity iOS export inside the APFS clone (root/Unity -> root/Unity/Builds/iOS); serialised with other chats via heavy.sh
cd "/Users/adnanyonathan/Documents/Codex/2026-09-20/wh/outputs/iphone-golf-simulator/work/phone-build-20261012"
GOLF_MUTE=1 "/Users/adnanyonathan/Documents/Codex/2026-09-20/wh/outputs/iphone-golf-simulator/work/postcard-look/heavy.sh" phone-build-20261012 /Applications/Unity/Hub/Editor/6000.3.24f1/Unity.app/Contents/MacOS/Unity -batchmode -quit -projectPath "/Users/adnanyonathan/Documents/Codex/2026-09-20/wh/outputs/iphone-golf-simulator/work/phone-build-20261012/root/Unity" -buildTarget iOS -job-worker-count 1 --burst-disable-compilation -executeMethod GolfArcade.EditorTools.BuildMenu.BuildIOS -logFile "/Users/adnanyonathan/Documents/Codex/2026-09-20/wh/outputs/iphone-golf-simulator/work/phone-build-20261012/unity-export.log"
echo "Unity exit: $?"
cat "/Users/adnanyonathan/Documents/Codex/2026-09-20/wh/outputs/iphone-golf-simulator/work/phone-build-20261012/root/Unity/Library/BuildResults/ios.txt" 2>/dev/null
