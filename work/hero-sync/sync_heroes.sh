#!/bin/zsh
# HERO SYNC: one step that keeps the lobby / locker / menu heroes (SwiftUI + SceneKit, GolfArcade/Unity/CharacterAssets) identical to the
# in-game Unity heroes. Run it after ANY change to how a hero looks (the files in look_sources.txt): mesh shaping, grip, skin, kit, shaders.
#
#   work/hero-sync/sync_heroes.sh              export + install + goldens/provenance + lobby-vs-game sheets
#   work/hero-sync/sync_heroes.sh --no-compare export + install + goldens/provenance only (no graphics needed)
#   work/hero-sync/sync_heroes.sh --compare-only  just the lobby-vs-game sheets of what is installed (after a MatchHeroSurfaces.swift change)
#
# 1. refreshes an APFS clone of Unity/ (never runs batch Unity on the shared project), 2. exports MatchHero_* (tennis + Distance) and GolfKitHero_*
# with MatchHeroLockerExport (muted, batch, no graphics), 3. installs every changed file into GolfArcade/Unity/CharacterAssets, 4. packs the
# Unity goldens into GolfArcadeTests/VisualGolden and rewrites provenance.json (with the look_sources.txt hashes the native freshness test
# checks), 5. renders each hero in Unity and in the app's own SceneKit code from the same cameras and writes side-by-side sheets + a colour
# report to work/hero-sync/sheets/. SceneKit cannot run Unity's shaders: when a hero SHADER changes, port it in MatchHeroSurfaces.swift too
# (the sheets show where they differ). Nothing is committed.
set -e
setopt null_glob
REPO=$(cd "$(dirname "$0")/../.." && pwd)
SYNC=$REPO/work/hero-sync
OUT=$SYNC/out
U=${UNITY:-/Applications/Unity/Hub/Editor/6000.3.24f1/Unity.app/Contents/MacOS/Unity}
CLONE=${HERO_SYNC_CLONE:-${TMPDIR:-/tmp}/hero-sync-UnityClone}
ASSETS=$REPO/GolfArcade/Unity/CharacterAssets
GOLDEN=$REPO/GolfArcadeTests/VisualGolden
export GOLF_MUTE=1          # Unity runs stay silent
COMPARE=1; EXPORT=1; [[ "$1" == "--no-compare" ]] && COMPARE=0; [[ "$1" == "--compare-only" ]] && EXPORT=0
mkdir -p $OUT/logs

echo "== 1/5 Unity clone: $CLONE"
if [[ ! -d $CLONE/Assets ]]; then cp -c -R $REPO/Unity $CLONE
else for d in Assets Packages ProjectSettings; do rsync -a --delete $REPO/Unity/$d/ $CLONE/$d/; done; fi

if (( EXPORT )); then
echo "== 2/5 export (MatchHeroLockerExport: Run, RunDistance, RunGolf)"
rm -rf $OUT/export $OUT/golden; mkdir -p $OUT/export $OUT/golden
for job in Run RunDistance RunGolf; do
  MH_LOCKER_FACE_PARITY=0 MH_LOCKER_OUT=$OUT/export MH_LOCKER_GOLDEN=$OUT/golden MH_LOCKER_REPORT=$OUT/logs/$job-report.txt \
    $U -batchmode -nographics -quit -projectPath $CLONE -executeMethod GolfArcade.EditorTools.MatchHeroLockerExport.$job -logFile $OUT/logs/$job.log \
    || { echo "export $job FAILED: see $OUT/logs/$job.log"; grep -m5 -E "error CS|Exception" $OUT/logs/$job.log; exit 1; }
done

echo "== 3/5 install into GolfArcade/Unity/CharacterAssets"
changed=0
for f in $OUT/export/*.*; do
  n=${f:t}; if ! cmp -s $f $ASSETS/$n; then cp $f $ASSETS/$n; echo "  updated $n"; changed=$((changed+1))
    [[ $n == *.png && -f $ASSETS/${n:r}.ktx ]] && echo "  WARNING: ${n:r}.png changed but its .ktx (what the garment shader samples) was not regenerated"; fi
done
echo "  $changed file(s) changed"

echo "== 4/5 goldens + provenance"
[[ -x $OUT/PackGolden ]] || swiftc -O -parse-as-library $REPO/work/reference-rebuild/original-native-adoption29/PackGolden.swift -o $OUT/PackGolden
for g in $OUT/golden/*_golden.json; do $OUT/PackGolden $g $GOLDEN/${${g:t}:r}.lzfse; done
python3 $SYNC/provenance.py $REPO $OUT/golden
fi

if (( COMPARE )); then
  echo "== 5/5 lobby vs game sheets"
  rm -rf $OUT/unity $OUT/lobby; mkdir -p $OUT/unity/tennis $OUT/unity/golf $OUT/lobby/tennis $OUT/lobby/golf
  for sport in tennis golf; do
    LP_OUT=$OUT/unity/$sport LP_GOLF=$([[ $sport == golf ]] && echo 1 || echo 0) \
      $U -batchmode -quit -projectPath $CLONE -executeMethod GolfArcade.EditorTools.MatchHeroLockerExport.CaptureLobbyParity -logFile $OUT/logs/capture-$sport.log \
      || { echo "capture $sport FAILED: see $OUT/logs/capture-$sport.log"; exit 1; }
  done
  swiftc -swift-version 6 -O -parse-as-library $SYNC/lobby_render.swift $REPO/GolfArcade/Unity/MatchHeroData.swift $REPO/GolfArcade/Unity/MatchHeroSurfaces.swift \
    $REPO/GolfArcade/Unity/MatchHeroMotion.swift -framework SceneKit -framework AppKit -framework Metal -o $OUT/lobby_render
  LP_ASSETS=$ASSETS LP_DISTANCE=1 $OUT/lobby_render $OUT/lobby/tennis 2>&1 | grep -v "^$" | grep -iv "warning" || true
  LP_ASSETS=$ASSETS LP_GOLF=1 $OUT/lobby_render $OUT/lobby/golf default 2>&1 | grep -v "^$" | grep -iv "warning" || true
  python3 $SYNC/compare.py $OUT $SYNC/sheets
fi
echo "== done. Review: git diff --stat -- GolfArcade/Unity/CharacterAssets GolfArcadeTests/VisualGolden; sheets in work/hero-sync/sheets/"
