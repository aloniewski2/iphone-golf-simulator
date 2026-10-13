#!/bin/zsh
# sync Unity sources into the clone, install V2 garments there, render the turnaround into work/clothing-v2/$1
set -e
REPO=${0:A:h:h:h}; CL=${CV2_CLONE:-$TMPDIR/cv2-UnityClone}; U=/Applications/Unity/Hub/Editor/6000.3.24f1/Unity.app/Contents/MacOS/Unity
OUT=$REPO/work/clothing-v2/${1:-after}
for d in Assets Packages ProjectSettings; do rsync -a --exclude 'Editor/ClothingV2/' --exclude 'Characters/KitsV2/' --exclude 'Resources/Tennis/KitsV2/' --exclude 'Resources/Golf/KitsV2/' --exclude 'Resources/Tennis/Customization/' $REPO/Unity/$d/ $CL/$d/; done
# the clone's prefabs are the installed ones: refresh them from the repo only when asked
[[ -n "$CV2_RESET_PREFABS" ]] && rsync -a $REPO/Unity/Assets/Resources/Tennis/Customization/ $CL/Assets/Resources/Tennis/Customization/
GOLF_MUTE=1 CV2_SRC=$REPO/work/clothing-v2/out $U -batchmode -nographics -quit -projectPath $CL -executeMethod GolfArcade.EditorTools.ClothingV2Install.Run -logFile $REPO/work/clothing-v2/logs/install.log || { grep -E "error CS|Exception" $REPO/work/clothing-v2/logs/install.log | head; exit 1; }
grep -E "Exception" $REPO/work/clothing-v2/logs/install.log | head -3
UNITY_PROJECT=$CL TURNAROUND_OUT=$OUT TURNAROUND_VIDEO=${TURNAROUND_VIDEO:-0} TURNAROUND_HEROES=${TURNAROUND_HEROES:-} $REPO/work/model-turnaround/run.sh | tail -1
