#!/bin/zsh
# usage: run.sh <label> <test filter> [env assignments...]
label=$1; filter=$2; shift 2
cd /private/tmp/claude-501/-Users-adnanyonathan-Documents-Codex-2026-09-20-wh-outputs-iphone-golf-simulator/d0a599a3-5168-48ea-a405-d9dbd314ba83/scratchpad/U
env GOLF_MUTE=1 MP_EXPLORE=1 MP_EXPLORE_DIR=${MP_EXPLORE_DIR:-/private/tmp/claude-501/-Users-adnanyonathan-Documents-Codex-2026-09-20-wh-outputs-iphone-golf-simulator/d0a599a3-5168-48ea-a405-d9dbd314ba83/scratchpad/runs/$label} "$@"   /Users/adnanyonathan/Documents/Codex/2026-09-20/wh/outputs/iphone-golf-simulator/work/postcard-look/heavy.sh "mp-explore $label"   /Applications/Unity/Hub/Editor/6000.3.24f1/Unity.app/Contents/MacOS/Unity -projectPath /private/tmp/claude-501/-Users-adnanyonathan-Documents-Codex-2026-09-20-wh-outputs-iphone-golf-simulator/d0a599a3-5168-48ea-a405-d9dbd314ba83/scratchpad/U -runTests -testPlatform PlayMode   -testFilter "$filter" -testResults /private/tmp/claude-501/-Users-adnanyonathan-Documents-Codex-2026-09-20-wh-outputs-iphone-golf-simulator/d0a599a3-5168-48ea-a405-d9dbd314ba83/scratchpad/runs/$label.xml -logFile /private/tmp/claude-501/-Users-adnanyonathan-Documents-Codex-2026-09-20-wh-outputs-iphone-golf-simulator/d0a599a3-5168-48ea-a405-d9dbd314ba83/scratchpad/runs/$label.log
python3 - /private/tmp/claude-501/-Users-adnanyonathan-Documents-Codex-2026-09-20-wh-outputs-iphone-golf-simulator/d0a599a3-5168-48ea-a405-d9dbd314ba83/scratchpad/runs/$label.xml <<'PY'
import sys, xml.etree.ElementTree as E
try: r = E.parse(sys.argv[1]).getroot()
except Exception as e: print("no results:", e); sys.exit()
print(f"total={r.get('total')} passed={r.get('passed')} failed={r.get('failed')} skipped={r.get('skipped')}")
for t in r.iter('test-case'):
    m = t.find('.//message'); print(' ', t.get('name'), t.get('result'), t.get('duration'), (m.text or '').strip()[:300] if m is not None else '')
PY
