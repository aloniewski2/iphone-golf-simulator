#!/bin/zsh
# usage: versus_pair.sh <label> [env...]   seat 0 windowed in U (records), seat 1 batch in U2
SP=/private/tmp/claude-501/-Users-adnanyonathan-Documents-Codex-2026-09-20-wh-outputs-iphone-golf-simulator/d0a599a3-5168-48ea-a405-d9dbd314ba83/scratchpad
UNITY=/Applications/Unity/Hub/Editor/6000.3.24f1/Unity.app/Contents/MacOS/Unity
label=$1; shift
OUT=$SP/runs/$label; mkdir -p $OUT
F="GolfArcade.PlayTests.VersusPeerCapture.TennisVersusPeer"
W=${WSEAT:-0}; B=$((1-W))
( env GOLF_MUTE=1 MP_VERSUS=1 MP_SEAT=$B MP_OUT=$OUT "$@" $UNITY -batchmode -projectPath $SP/U2 -runTests -testPlatform PlayMode -testFilter $F -testResults $OUT/seat$B.xml -logFile $OUT/seat$B.log ) &
P1=$!
env GOLF_MUTE=1 MP_VERSUS=1 MP_SEAT=$W MP_RECORD=1 MP_OUT=$OUT "$@" $UNITY -projectPath $SP/U -runTests -testPlatform PlayMode -testFilter $F -testResults $OUT/seat$W.xml -logFile $OUT/seat$W.log
wait $P1
for s in 0 1; do python3 - $OUT/seat$s.xml <<'PY'
import sys, xml.etree.ElementTree as E
try: r=E.parse(sys.argv[1]).getroot()
except Exception as e: print(sys.argv[1], "no results", e); sys.exit()
for t in r.iter('test-case'):
    m=t.find('.//message'); print(sys.argv[1].split('/')[-1], t.get('result'), t.get('duration'), (m.text or '').strip()[:300] if m is not None else '')
PY
done
