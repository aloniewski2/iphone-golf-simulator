#!/bin/zsh
# golf male trousers v2: build -> export (merge manifest) -> cover -> maps   (work/clothing-v2/golf/run_trousers.sh)
set -e
cd ${0:A:h:h}
B=/Applications/Blender.app/Contents/MacOS/Blender
HERO=${1:-GolfKitHero_Male}; DESIGN=${2:-golf_male_trousers}
$B -b --factory-startup --python py/pants2.py -- $HERO $DESIGN shorts/${HERO}_${DESIGN}.blend > logs/pants2-$HERO.log 2>&1; grep -E "CAGE|FIT|PANTS" logs/pants2-$HERO.log
grep -q Traceback logs/pants2-$HERO.log && { grep -A12 Traceback logs/pants2-$HERO.log; exit 1; }
cp shorts/${HERO}_${DESIGN}.blend shorts/${HERO}_${DESIGN}_export.blend
CV2_WSMOOTH=6 $B -b shorts/${HERO}_${DESIGN}_export.blend --python py/export2.py -- $HERO Kit_Bottom trousers2 shorts/${HERO}_${DESIGN}_final.blend > logs/export2-$HERO-bottom.log 2>&1; grep -E "EXPORT2|Traceback" logs/export2-$HERO-bottom.log
