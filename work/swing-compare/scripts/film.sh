#!/bin/zsh
# film.sh <log> [ENV=val ...]: SwingFilm in MY APFS clone (never the shared project); waits while xcodebuild runs (Burst crash).
UNITY=/Applications/Unity/Hub/Editor/6000.3.24f1/Unity.app/Contents/MacOS/Unity
CLONE=${SF_CLONE:-/private/tmp/claude-501/-Users-adnanyonathan-Documents-Codex-2026-09-20-wh-outputs-iphone-golf-simulator/7dab0438-556f-4f2a-93fa-076d9203f1a0/scratchpad/UnityClone}
log=$1; shift
while pgrep -f "^/Applications/Xcode[^ ]*/Contents/Developer/usr/bin/xcodebuild |^xcodebuild " > /dev/null 2>&1; do sleep 15; done
env GOLF_MUTE=1 "$@" $UNITY -batchmode -projectPath $CLONE -executeMethod GolfArcade.EditorTools.SwingFilm.Run -logFile $log
echo "unity exit $?"
