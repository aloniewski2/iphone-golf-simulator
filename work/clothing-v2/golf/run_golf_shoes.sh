#!/bin/zsh
# golf shoes v2 (plain, cleated): build -> export (merge manifest)   (work/clothing-v2/golf/run_golf_shoes.sh <hero> <design>)
set -e
cd ${0:A:h:h}
B=/Applications/Blender.app/Contents/MacOS/Blender
HERO=${1:-GolfKitHero_Male}; SHOE=${2:-male_golf}
for sd in L R; do
  $B -b --factory-startup --python py/shoe2.py -- $HERO $SHOE $sd shoes/${HERO}_shoe_$sd.blend > logs/shoe2-$HERO-$sd.log 2>&1; grep -E "SHOE2" logs/shoe2-$HERO-$sd.log
  grep -q Traceback logs/shoe2-$HERO-$sd.log && { grep -A12 Traceback logs/shoe2-$HERO-$sd.log; exit 1; }
  cp shoes/${HERO}_shoe_$sd.blend shoes/${HERO}_shoe_${sd}_export.blend
  $B -b shoes/${HERO}_shoe_${sd}_export.blend --python py/export2.py -- $HERO Kit_Shoe_$sd shoe2 shoes/${HERO}_shoe_${sd}_final.blend res=1024 > logs/export2-$HERO-shoe-$sd.log 2>&1; grep -E "EXPORT2|Traceback" logs/export2-$HERO-shoe-$sd.log
done
