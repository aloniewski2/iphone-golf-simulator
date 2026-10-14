#!/bin/zsh
# utest.sh <log> <testFilter>: windowed PlayMode tests in MY APFS clone (never the shared project), muted; waits while xcodebuild runs (Burst crash).
UNITY=/Applications/Unity/Hub/Editor/6000.3.24f1/Unity.app/Contents/MacOS/Unity
CLONE=/private/tmp/claude-501/-Users-adnanyonathan-Documents-Codex-2026-09-20-wh-outputs-iphone-golf-simulator/7dab0438-556f-4f2a-93fa-076d9203f1a0/scratchpad/UnityClone
log=${1:A}; filter=$2
while pgrep -f "^/Applications/Xcode[^ ]*/Contents/Developer/usr/bin/xcodebuild |^xcodebuild " > /dev/null 2>&1; do sleep 15; done
rm -f $log.xml
env GOLF_MUTE=1 $UNITY -projectPath $CLONE -runTests -testPlatform PlayMode -testFilter "$filter" -testResults $log.xml -logFile $log
echo "unity exit $?"
python3 - $log.xml <<'PY'
import sys, xml.etree.ElementTree as E
try: r = E.parse(sys.argv[1]).getroot()
except Exception as e: print("no results", e); sys.exit()
print(f"PLAYMODE total={r.get('total')} passed={r.get('passed')} failed={r.get('failed')}")
for t in r.iter('test-case'):
    m = t.find('.//message'); print(' ', t.get('result'), t.get('fullname'), ((m.text or '').strip()[:300] if m is not None else ''))
PY
