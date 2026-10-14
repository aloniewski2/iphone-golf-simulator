#!/bin/zsh
# PlayMode tests that need a real rendered frame (NativeLaunchSupportsBothSportsIdentitiesAndHands
# waits for the gameplay camera to finish rendering) cannot pass under -batchmode, which never
# renders. Running the editor windowed renders normally; it opens, runs, and quits by itself.
#
#   Unity/Tools/run-playmode-windowed.sh [test filter]   (default: the tennis tests only)
set -e
cd "$(dirname "$0")/.."
UNITY=${UNITY:-/Applications/Unity/Hub/Editor/$(sed -n 's/^m_EditorVersion: //p' ProjectSettings/ProjectVersion.txt)/Unity.app/Contents/MacOS/Unity}
OUT=${OUT:-Library/TestResults}; mkdir -p "$OUT"
FILTER=${1:-"TennisGameplayTests|TennisPolishPlayTests|NativeLaunchSupportsBothSportsIdentitiesAndHands"}
"$UNITY" -projectPath . -runTests -testPlatform PlayMode -testFilter "$FILTER" -testResults "$OUT/playmode.xml" -logFile "$OUT/playmode.log" || true
python3 - "$OUT/playmode.xml" <<'PY'
import sys, xml.etree.ElementTree as E
r = E.parse(sys.argv[1]).getroot()
print(f"PLAYMODE total={r.get('total')} passed={r.get('passed')} failed={r.get('failed')}")
for t in r.iter('test-case'):
    if t.get('result') != 'Passed':
        m = t.find('.//message'); print('  FAIL', t.get('name'), (m.text or '').strip()[:200] if m is not None else '')
PY
