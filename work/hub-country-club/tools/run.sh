#!/bin/zsh
# Country-club hub proof: run HubCountryClubProof.SevenShots in a scratch APFS clone of Unity/ and write the shots + render
# costs to <out dir>. Windowed (batchmode never renders), muted, serialised with other chats' Unity/xcodebuild runs.
#   work/hub-country-club/tools/run.sh <clone dir> <out dir> [sync]
# `sync` first copies the repo's hub scripts / shaders / Hub resources into the clone (the AFTER run); without it the clone is
# measured as it is (the BEFORE run, made from the unchanged project).
set -e
REPO="$(cd "$(dirname "$0")/../../.." && pwd)"
CLONE="$1"; OUT="$2"; MODE="$3"
UNITY=/Applications/Unity/Hub/Editor/$(sed -n 's/^m_EditorVersion: //p' "$REPO/Unity/ProjectSettings/ProjectVersion.txt")/Unity.app/Contents/MacOS/Unity
[ -d "$CLONE" ] || cp -c -R "$REPO/Unity" "$CLONE"
if [ "$MODE" = sync ]; then
  rsync -a --delete "$REPO/Unity/Assets/Scripts/Hub/" "$CLONE/Assets/Scripts/Hub/"
  rsync -a "$REPO/Unity/Assets/Resources/Hub/" "$CLONE/Assets/Resources/Hub/"
fi
cp "$REPO/work/hub-country-club/tools/HubCountryClubProof.cs" "$CLONE/Assets/Tests/PlayMode/"
rm -rf "$CLONE/Temp"; mkdir -p "$OUT"
GOLF_MUTE=1 HUB_CC_DIR="$OUT" "$REPO/work/postcard-look/heavy.sh" hub-country-club "$UNITY" -projectPath "$CLONE" -runTests -testPlatform PlayMode \
  -testFilter GolfArcade.Tests.HubCountryClubProof.SevenShots -testResults "$OUT/results.xml" -logFile "$OUT/unity.log" || true
grep -o 'result="[A-Za-z]*"' "$OUT/results.xml" | head -3 || echo "no results (see $OUT/unity.log)"
