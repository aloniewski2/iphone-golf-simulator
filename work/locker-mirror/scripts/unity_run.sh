#!/bin/zsh
# unity_run.sh <method> <log> [ENV=val ...]
# Runs a Unity batch method in an APFS clone of the project (never in the shared project) and waits while an xcodebuild runs (Burst SIGSEGV when both compile).
# The clone lives next to this session's scratch: set LM_CLONE to override. Refresh the clone's script with sync_clone.sh after editing Unity/Assets/Editor.
CLONE=${LM_CLONE:-/private/tmp/claude-501/-Users-adnanyonathan-Documents-Codex-2026-09-20-wh-outputs-iphone-golf-simulator/b8c5974c-0658-48af-9507-14588d72a05b/scratchpad/UnityClone}
UNITY=/Applications/Unity/Hub/Editor/6000.3.24f1/Unity.app/Contents/MacOS/Unity
method=$1; log=$2; shift 2
while pgrep -f "xcodebuild" > /dev/null 2>&1; do echo "waiting for xcodebuild..."; sleep 15; done
env "$@" $UNITY -batchmode -nographics -projectPath $CLONE -executeMethod $method -logFile $log
echo "unity exit $?"
