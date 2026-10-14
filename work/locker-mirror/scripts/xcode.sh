#!/bin/zsh
# xcode.sh build|test [xcodebuild args...]  - private DerivedData under the session scratchpad; waits while a Unity batch runs (Burst SIGSEGV when both compile); logs to work/locker-mirror/logs/xcode_<mode>.log
# `test` runs test-without-building and, because `xcodebuild test` can hang at teardown holding a shared lock, kills xcodebuild a few seconds after the verdict line is printed.
cd "$(dirname "$0")/../../.." || exit 1
SCR=${LM_SCRATCH:-/private/tmp/claude-501/-Users-adnanyonathan-Documents-Codex-2026-09-20-wh-outputs-iphone-golf-simulator/b8c5974c-0658-48af-9507-14588d72a05b/scratchpad}
DD=$SCR/DerivedData
DEST=${LM_DEST:-id=88A5F840-AF09-4E3F-AF23-089D85AF4F25}
mode=$1; shift
LOG=work/locker-mirror/logs/xcode_${mode}.log
while pgrep -f "Unity.app/Contents/MacOS/Unity" > /dev/null 2>&1; do echo "waiting for a Unity batch..."; sleep 15; done
if [ "$mode" = build ]; then
  xcodebuild build-for-testing -project GolfArcade.xcodeproj -scheme GolfArcade -destination "$DEST" -derivedDataPath "$DD" "$@" > $LOG 2>&1
  echo "xcodebuild exit $?"; tail -5 $LOG
else
  xcodebuild test-without-building -project GolfArcade.xcodeproj -scheme GolfArcade -destination "$DEST" -derivedDataPath "$DD" "$@" > $LOG 2>&1 &
  pid=$!
  while kill -0 $pid 2>/dev/null; do
    if grep -qE "^\*\* TEST (SUCCEEDED|FAILED) \*\*|^Test Suite '(Selected tests|All tests)' (passed|failed)" $LOG; then sleep 6; kill $pid 2>/dev/null; pkill -f "xcodebuild test-without-building" 2>/dev/null; break; fi
    sleep 3
  done
  grep -E "^\*\* TEST|Test Suite .* (passed|failed)|Executed [0-9]+ tests" $LOG | tail -8
fi
