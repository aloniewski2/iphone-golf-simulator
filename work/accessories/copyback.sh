#!/bin/zsh
# ACCESSORIES: copy what the editor produced in my clone back into the repo (installed items + the .meta files of the new scripts). Stage only.
set -e
REPO=${0:A:h:h:h}; CL=${ACC_CLONE:-$TMPDIR/acc-UnityClone}
mkdir -p $REPO/Unity/Assets/Resources/Accessories
rsync -a --delete $CL/Assets/Resources/Accessories/ $REPO/Unity/Assets/Resources/Accessories/
[[ -f $CL/Assets/Resources/Accessories.meta ]] && cp -n $CL/Assets/Resources/Accessories.meta $REPO/Unity/Assets/Resources/Accessories.meta || true
for f in Scripts/Tennis/HeroAccessories.cs Editor/AccessoriesInstall.cs Editor/AccessoriesProof.cs Editor/AccessoriesClipCheck.cs Tests/EditMode/HeroAccessoriesTests.cs; do
  [[ -f $CL/Assets/$f.meta && ! -f $REPO/Unity/Assets/$f.meta ]] && cp $CL/Assets/$f.meta $REPO/Unity/Assets/$f.meta && echo "meta $f"
done
echo copied; du -sh $REPO/Unity/Assets/Resources/Accessories
