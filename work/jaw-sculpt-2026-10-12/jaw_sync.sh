#!/bin/zsh
# Lobby re-sync for the jaw sculpt ONLY: the same steps as work/hero-sync/sync_heroes.sh, but the Unity clone is the STAGED state of
# Unity/ (other sessions' unstaged edits and untracked files left out) plus this pass's files (head FBXs + HeroSkinNormals.cs).
# Why: on 2026-10-12 a parallel session's unreviewed clothing-V2 polo sat unstaged in the shared tree, and sync_heroes.sh exports the
# working tree, so it would have baked that polo into the jaw's lobby files. Nothing is committed; the shared tree is not modified.
#   work/jaw-sculpt-2026-10-12/jaw_sync.sh
set -e
setopt null_glob
REPO=$(cd "$(dirname "$0")/../.." && pwd)
SYNC=$REPO/work/hero-sync
HERE=$REPO/work/jaw-sculpt-2026-10-12
OUT=$HERE/sync_out
U=${UNITY:-/Applications/Unity/Hub/Editor/6000.3.24f1/Unity.app/Contents/MacOS/Unity}
CLONE=${JAW_SYNC_CLONE:-${TMPDIR:-/tmp}/jaw-sync-UnityClone}
ASSETS=$REPO/GolfArcade/Unity/CharacterAssets
GOLDEN=$REPO/GolfArcadeTests/VisualGolden
MINE=(Unity/Assets/Resources/Tennis/OriginalSeamBlinkFinish/Male.fbx Unity/Assets/Resources/Tennis/OriginalSeamBlinkFinish/Female.fbx Unity/Assets/Scripts/Tennis/HeroSkinNormals.cs)
export GOLF_MUTE=1
cd $REPO
mkdir -p $OUT/logs

echo "== 1/5 staged-state clone: $CLONE"
if [[ ! -d $CLONE/Assets ]]; then cp -c -R $REPO/Unity $CLONE
else for d in Assets Packages ProjectSettings; do rsync -a --delete $REPO/Unity/$d/ $CLONE/$d/; done; fi
n=0
for f in $(git diff --name-only -- Unity/Assets Unity/Packages Unity/ProjectSettings); do
  (( ${MINE[(Ie)$f]} )) && continue
  if git cat-file -e ":$f" 2>/dev/null; then git show ":$f" > $CLONE/${f#Unity/}; else rm -f $CLONE/${f#Unity/}; fi; n=$((n+1))
done
m=0
for f in $(git ls-files --others --exclude-standard -- Unity/Assets Unity/Packages Unity/ProjectSettings); do rm -f $CLONE/${f#Unity/}; m=$((m+1)); done
for d in $(git ls-files --others --exclude-standard --directory -- Unity/Assets); do rm -rf $CLONE/${d#Unity/}; done
for f in $MINE; do cp $REPO/$f $CLONE/${f#Unity/}; done
echo "  $n file(s) back to their staged version, $m untracked file(s) left out, ${#MINE} jaw file(s) on top"

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
  n=${f:t}; if ! cmp -s $f $ASSETS/$n; then cp $f $ASSETS/$n; changed=$((changed+1)); fi
done
echo "  $changed file(s) changed"

echo "== 4/5 goldens"
[[ -x $SYNC/out/PackGolden ]] || swiftc -O -parse-as-library $REPO/work/reference-rebuild/original-native-adoption29/PackGolden.swift -o $SYNC/out/PackGolden
for g in $OUT/golden/*_golden.json; do $SYNC/out/PackGolden $g $GOLDEN/${${g:t}:r}.lzfse; done
echo "  provenance: run jaw_provenance.py after staging (hashes the STAGED look sources)"

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
echo "== done"
