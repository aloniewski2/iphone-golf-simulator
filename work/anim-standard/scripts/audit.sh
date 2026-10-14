#!/bin/zsh
# audit.sh <out_dir> [ENV=val ...] : AnimationAudit in the APFS clone (graphics on). Waits while an xcodebuild runs.
UNITY=/Applications/Unity/Hub/Editor/6000.3.24f1/Unity.app/Contents/MacOS/Unity
out=$1; shift
while pgrep -f "^/Applications/Xcode[^ ]*/Contents/Developer/usr/bin/xcodebuild |^xcodebuild " > /dev/null 2>&1; do sleep 15; done
mkdir -p $out
env GOLF_MUTE=1 AA_OUT=$out "$@" $UNITY -batchmode -projectPath /private/tmp/claude-501/-Users-adnanyonathan-Documents-Codex-2026-09-20-wh-outputs-iphone-golf-simulator/43f4b36a-f11c-4db6-90e0-e9995fa0db4f/scratchpad/UnityClone -executeMethod GolfArcade.EditorTools.AnimationAudit.Run -logFile $out/unity.log
echo "unity exit $?"
