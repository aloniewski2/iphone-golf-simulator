#!/bin/zsh
# golf gloves v2 (L and R): build -> export (merge manifest)   (work/clothing-v2/golf/run_gloves.sh <hero>)
set -e
cd ${0:A:h:h}
B=/Applications/Blender.app/Contents/MacOS/Blender
HERO=${1:-GolfKitHero_Male}
for sd in L R; do
  $B -b --factory-startup --python py/glove2.py -- $HERO $sd golf/${HERO}_glove_$sd.blend > logs/glove2-$HERO-$sd.log 2>&1; grep -E "GLOVE" logs/glove2-$HERO-$sd.log
  grep -q Traceback logs/glove2-$HERO-$sd.log && { grep -A12 Traceback logs/glove2-$HERO-$sd.log; exit 1; }
  cp golf/${HERO}_glove_$sd.blend golf/${HERO}_glove_${sd}_export.blend
  CV2_WSMOOTH=2 $B -b golf/${HERO}_glove_${sd}_export.blend --python py/export2.py -- $HERO Kit_Glove_$sd glove2 golf/${HERO}_glove_${sd}_final.blend res=1024 margin=.004 > logs/export2-$HERO-glove-$sd.log 2>&1; grep -E "EXPORT2|Traceback" logs/export2-$HERO-glove-$sd.log
done
