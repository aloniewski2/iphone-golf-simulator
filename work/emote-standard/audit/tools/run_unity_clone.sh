#!/bin/zsh
# run.sh <outdir> ENV=val ... : Unity batch in the APFS clone
UNITY=/Applications/Unity/Hub/Editor/6000.3.24f1/Unity.app/Contents/MacOS/Unity
C=/private/tmp/claude-501/-Users-adnanyonathan-Documents-Codex-2026-09-20-wh-outputs-iphone-golf-simulator/43f4b36a-f11c-4db6-90e0-e9995fa0db4f/scratchpad/UnityClone
out=$1; shift
mkdir -p $out
env GOLF_MUTE=1 AA_OUT=$out "$@" $UNITY -batchmode -projectPath $C -executeMethod GolfArcade.EditorTools.AnimationAudit.Run -logFile $out/unity.log
echo "unity exit $?" >> $out/unity.log
