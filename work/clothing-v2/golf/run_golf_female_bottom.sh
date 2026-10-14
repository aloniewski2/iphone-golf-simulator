#!/bin/zsh
# golf female skort = tight under-shorts + pleated skort + flat waistband with belt loops (one Kit_Bottom piece)   (work/clothing-v2/golf/run_golf_female_bottom.sh)
set -e
cd ${0:A:h:h}
B=/Applications/Blender.app/Contents/MacOS/Blender
HERO=GolfKitHero_Female
$B -b --factory-startup --python py/pants2.py -- $HERO female_undershorts shorts/${HERO}_undershorts.blend > logs/pants2-$HERO-under.log 2>&1; grep -E "PANTS" logs/pants2-$HERO-under.log
grep -q Traceback logs/pants2-$HERO-under.log && { grep -A12 Traceback logs/pants2-$HERO-under.log; exit 1; }
$B -b --factory-startup --python py/skirt2.py -- $HERO female_golf_skort shorts/${HERO}_undershorts.blend shorts/${HERO}_skort.blend > logs/skirt2-$HERO.log 2>&1; grep -E "FIT|SKIRT" logs/skirt2-$HERO.log
grep -q Traceback logs/skirt2-$HERO.log && { grep -A12 Traceback logs/skirt2-$HERO.log; exit 1; }
cp shorts/${HERO}_skort.blend shorts/${HERO}_skort_export.blend
CV2_WSMOOTH=6 $B -b shorts/${HERO}_skort_export.blend --python py/export2.py -- $HERO Kit_Bottom skirt2 shorts/${HERO}_skort_final.blend > logs/export2-$HERO-bottom.log 2>&1; grep -E "EXPORT2|Traceback" logs/export2-$HERO-bottom.log
