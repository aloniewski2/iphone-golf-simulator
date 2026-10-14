#!/bin/zsh
# ACCESSORIES: sync the repo's Unity sources into MY APFS clone, then run an editor method there (never the shared project).
#   work/accessories/unity_iter.sh install                      install the items from out/ (AccessoriesInstall.Run)
#   work/accessories/unity_iter.sh proof <outdir> [heroes]      photograph heroes (AccessoriesProof; env ACC_PROOF_SHOTS / ACC_PROOF_POSE / ACC_PROOF_ALONE / ACC_PROOF_BAG)
#   work/accessories/unity_iter.sh run <Class.Method>           any other editor method
# Needs graphics for `proof` (no -nographics). Always GOLF_MUTE=1.
set -e
REPO=${0:A:h:h:h}; CL=${ACC_CLONE:-$TMPDIR/acc-UnityClone}; U=/Applications/Unity/Hub/Editor/6000.3.24f1/Unity.app/Contents/MacOS/Unity
LOG=$REPO/work/accessories/logs
mode=$1; shift
for d in Assets Packages ProjectSettings; do rsync -a --exclude 'Resources/Accessories/' $REPO/Unity/$d/ $CL/$d/; done
export GOLF_MUTE=1
case $mode in
  install) METHOD=GolfArcade.EditorTools.AccessoriesInstall.Run; GFX=-nographics; export ACC_SRC=$REPO/work/accessories/out ;;
  proof) METHOD=GolfArcade.EditorTools.AccessoriesProof.RunBatch; GFX=; export ACC_PROOF_OUT=${1:A}; [[ -z "$1" ]] && export ACC_PROOF_OUT=$REPO/work/accessories/proof/unity; [[ -n "$2" ]] && export ACC_PROOF_HEROES=$2 ;;
  run) METHOD=$1; GFX=-nographics ;;
esac
$U -batchmode $GFX -quit -projectPath $CL -executeMethod $METHOD -logFile $LOG/unity_$mode.log || { grep -E "error CS|Exception|ERROR" $LOG/unity_$mode.log | head -20; exit 1; }
grep -E "error CS|Exception" $LOG/unity_$mode.log | head -5 || true
echo "ok: $mode"
