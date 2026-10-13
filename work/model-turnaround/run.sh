#!/bin/zsh
# Hero turnaround: renders every hero from every angle into work/model-turnaround/<hero>/ (+ one contact sheet and a 10 s spin per hero).
# Usage:   work/model-turnaround/run.sh                       all four heroes
#          work/model-turnaround/run.sh MatchHero_Female      one or more heroes (comma separated)
#          TURNAROUND_VIDEO=0 work/model-turnaround/run.sh    skip the spin videos
#          UNITY_PROJECT=<clone> work/model-turnaround/run.sh render from another copy of the project (e.g. an APFS clone)
# Close the Unity editor on the project first (or use the menu there instead: Tools > Hero Turnaround > Capture All Heroes).
# Needs graphics: never add -nographics.
set -e
HERE=${0:A:h}
REPO=${HERE:h:h}
PROJECT=${UNITY_PROJECT:-$REPO/Unity}
VERSION=$(sed -n 's/^m_EditorVersion: //p' $PROJECT/ProjectSettings/ProjectVersion.txt)
UNITY=${UNITY_APP:-/Applications/Unity/Hub/Editor/$VERSION/Unity.app/Contents/MacOS/Unity}
[[ -n "$1" ]] && export TURNAROUND_HEROES=$1
export TURNAROUND_OUT=${TURNAROUND_OUT:-$HERE}
export GOLF_MUTE=1
unset VISUAL_CHARACTER_SKIN_POLISH VISUAL_BLINK_NORMAL_FIELD
mkdir -p $HERE/logs
echo "Unity $VERSION  project $PROJECT  ->  $TURNAROUND_OUT"
$UNITY -batchmode -projectPath $PROJECT -executeMethod GolfArcade.EditorTools.HeroTurnaround.RunBatch -logFile $HERE/logs/turnaround.log || { echo "FAILED, see $HERE/logs/turnaround.log"; grep -n "error CS\|Exception" $HERE/logs/turnaround.log | head -20; exit 1; }
echo "Done. Contact sheets:"; ls -1 $TURNAROUND_OUT/*_contact_sheet.png
