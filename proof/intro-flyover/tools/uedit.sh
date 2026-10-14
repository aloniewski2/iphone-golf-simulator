#!/bin/zsh
# uedit.sh <log> <filter>: EditMode tests in my clone (batchmode).
SP=/private/tmp/claude-501/-Users-adnanyonathan-Documents-Codex-2026-09-20-wh-outputs-iphone-golf-simulator/8b0d94ae-fef3-43f2-9146-5c0e7ce4d79b/scratchpad
UNITY=/Applications/Unity/Hub/Editor/6000.3.24f1/Unity.app/Contents/MacOS/Unity
while pgrep -f "^/Applications/Xcode[^ ]*/Contents/Developer/usr/bin/xcodebuild |^xcodebuild " > /dev/null 2>&1; do sleep 15; done
rm -f $SP/films/edit.xml
env GOLF_MUTE=1 $UNITY -batchmode -projectPath $SP/UnityClone -runTests -testPlatform EditMode -testFilter "$2" -testResults $SP/films/edit.xml -logFile $1
echo "unity exit $?"
grep -E "error CS[0-9]+" $1 | sort -u | head -30
python3 - $SP/films/edit.xml <<'PY'
import sys, xml.etree.ElementTree as E
try: r = E.parse(sys.argv[1]).getroot()
except Exception as e: print("no results", e); sys.exit()
print(f"EDITMODE total={r.get('total')} passed={r.get('passed')} failed={r.get('failed')}")
for t in r.iter('test-case'):
    if t.get('result') != 'Passed':
        m = t.find('.//message'); print('  FAIL', t.get('name'), (m.text or '').strip()[:400] if m is not None else '')
PY
