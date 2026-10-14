#!/bin/zsh
# run.sh <sex> <clips> [extra args] : measure the swings authored by gs_new/golf_swing.py on the shipped FBX
cd "$(dirname "$0")/.."
sex=$1; clips=$2; shift 2
/Applications/Blender.app/Contents/MacOS/Blender -b --factory-startup --python scripts/splice_swings.py -- --sex $sex --in rollback/golfer_${${sex:0:1}:l}.fbx --gs gs_new --only $clips --measure "$@" 2>&1 | grep -E "HALF|CHIP|FULL|ADDRESS|worst hand|authored|VERIFY|MEASURE|^  |Error|Traceback|line [0-9]|exported|moved"
