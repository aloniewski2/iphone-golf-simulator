#!/bin/zsh
# FEMALE tennis bottom v2 = tight under-shorts + pleated skirt + rib waistband, one Kit_Bottom piece   (work/clothing-v2/tank/run_female_bottom.sh)
set -e
cd ${0:A:h:h}
B=/Applications/Blender.app/Contents/MacOS/Blender
HERO=${1:-MatchHero_Female}
$B -b --factory-startup --python py/pants2.py -- $HERO female_undershorts shorts/female_undershorts.blend > logs/pants2-female_undershorts.log 2>&1; grep -E "CAGE|FIT|PANTS" logs/pants2-female_undershorts.log
grep -q Traceback logs/pants2-female_undershorts.log && { grep -A12 Traceback logs/pants2-female_undershorts.log; exit 1; }
$B -b --factory-startup --python py/skirt2.py -- $HERO female_tennis_skirt shorts/female_undershorts.blend shorts/female_bottom.blend > logs/skirt2.log 2>&1; grep -E "FIT|SKIRT" logs/skirt2.log
grep -q Traceback logs/skirt2.log && { grep -A12 Traceback logs/skirt2.log; exit 1; }
cp shorts/female_bottom.blend shorts/female_bottom_export.blend
CV2_WSMOOTH=6 $B -b shorts/female_bottom_export.blend --python py/export2.py -- $HERO Kit_Bottom skirt2 shorts/female_bottom_final.blend > logs/export2-female_bottom.log 2>&1; grep -E "EXPORT2|Traceback" logs/export2-female_bottom.log
