#!/bin/zsh
# The hub's PlayMode suites (plaza, bays, party, phone-only) in a scratch clone, windowed and muted; metrics to <out dir>.
#   work/hub-country-club/tools/suite.sh <clone dir> <out dir>
REPO="$(cd "$(dirname "$0")/../../.." && pwd)"
CLONE="$1"; OUT="$2"
UNITY=/Applications/Unity/Hub/Editor/$(sed -n 's/^m_EditorVersion: //p' "$REPO/Unity/ProjectSettings/ProjectVersion.txt")/Unity.app/Contents/MacOS/Unity
rm -rf "$CLONE/Temp"; mkdir -p "$OUT"
GOLF_MUTE=1 HUB_PROOF_DIR="$OUT" "$REPO/work/postcard-look/heavy.sh" hub-country-club-suite "$UNITY" -projectPath "$CLONE" -runTests -testPlatform PlayMode \
  -testFilter 'GolfArcade\.Tests\.Hub(Plaza|Bay|Party|PhoneOnly)Tests\.' -testResults "$OUT/results.xml" -logFile "$OUT/unity.log" || true
python3 - "$OUT/results.xml" <<'PY'
import sys, xml.etree.ElementTree as ET
r = ET.parse(sys.argv[1]).getroot()
for tc in r.iter('test-case'):
    print(f"{tc.get('result'):8} {float(tc.get('duration') or 0):7.1f}s  {tc.get('fullname')}")
print(r.get('result'), 'passed', r.get('passed'), 'failed', r.get('failed'), 'skipped', r.get('skipped'))
PY
