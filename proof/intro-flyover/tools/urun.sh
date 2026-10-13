#!/bin/zsh
# urun.sh <log> <testFilter> [ENV=val ...]: windowed PlayMode test in MY APFS clone (never the shared project);
# waits while another Unity/xcodebuild runs heavy work is not required for a separate project, only xcodebuild (Burst crash).
SP=/private/tmp/claude-501/-Users-adnanyonathan-Documents-Codex-2026-09-20-wh-outputs-iphone-golf-simulator/8b0d94ae-fef3-43f2-9146-5c0e7ce4d79b/scratchpad
UNITY=/Applications/Unity/Hub/Editor/6000.3.24f1/Unity.app/Contents/MacOS/Unity
log=$1; filter=$2; shift 2
while pgrep -f "^/Applications/Xcode[^ ]*/Contents/Developer/usr/bin/xcodebuild |^xcodebuild " > /dev/null 2>&1; do sleep 15; done
env GOLF_MUTE=1 "$@" $UNITY -projectPath $SP/UnityClone -runTests -testPlatform PlayMode -testFilter "$filter" -testResults $SP/films/results.xml -logFile $log
echo "unity exit $?"
python3 - $SP/films/results.xml <<'PY'
import sys, xml.etree.ElementTree as E
try:
    r = E.parse(sys.argv[1]).getroot()
except Exception as e:
    print("no results", e); sys.exit()
print(f"PLAYMODE total={r.get('total')} passed={r.get('passed')} failed={r.get('failed')}")
for t in r.iter('test-case'):
    if t.get('result') != 'Passed':
        m = t.find('.//message'); print('  FAIL', t.get('name'), (m.text or '').strip()[:400] if m is not None else '')
PY
