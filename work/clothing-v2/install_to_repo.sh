#!/bin/zsh
# copy what the Unity clone installed (meshes, maps, role materials, prefabs) back into the repo (stage only: no commit)   (work/clothing-v2/install_to_repo.sh)
set -e
REPO=${0:A:h:h:h}; CL=${CV2_CLONE:-$TMPDIR/cv2-UnityClone}
for d in Assets/Characters/KitsV2 Assets/Resources/Tennis/KitsV2 Assets/Resources/Golf/KitsV2 Assets/Characters/MatchHeroes/Materials/Kit Assets/Characters/GolfHeroKit/Materials; do
  mkdir -p $REPO/Unity/$d; rsync -a $CL/$d/ $REPO/Unity/$d/
done
for p in PlayerMale PlayerFemale PlayerMaleGolfKit PlayerFemaleGolfKit; do
  cp $CL/Assets/Resources/Tennis/Customization/$p.prefab $REPO/Unity/Assets/Resources/Tennis/Customization/$p.prefab
done
echo "copied; review with: git status --short Unity/Assets/Characters Unity/Assets/Resources"
