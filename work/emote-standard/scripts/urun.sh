#!/bin/zsh
# urun.sh <log> [ENV=val ...] : JoyFilm in the APFS clone (never the shared project); waits while xcodebuild runs (Burst crash).
UNITY=/Applications/Unity/Hub/Editor/6000.3.24f1/Unity.app/Contents/MacOS/Unity
log=$1; shift
while pgrep -f "^/Applications/Xcode[^ ]*/Contents/Developer/usr/bin/xcodebuild |^xcodebuild " > /dev/null 2>&1; do sleep 15; done
env GOLF_MUTE=1 "$@" $UNITY -batchmode -projectPath /private/tmp/claude-501/-Users-adnanyonathan-Documents-Codex-2026-09-20-wh-outputs-iphone-golf-simulator/43f4b36a-f11c-4db6-90e0-e9995fa0db4f/scratchpad/UnityClone -executeMethod GolfArcade.EditorTools.JoyFilm.Run -logFile $log
echo "unity exit $?"
