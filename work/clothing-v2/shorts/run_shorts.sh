#!/bin/zsh
# SHORTS v2 (male tennis): build -> export (merges the manifest) -> skin masks -> maps   (work/clothing-v2/shorts/run_shorts.sh)
set -e
cd ${0:A:h:h}
B=/Applications/Blender.app/Contents/MacOS/Blender
HERO=${1:-MatchHero_Male}; DESIGN=${2:-male_shorts}
$B -b --factory-startup --python py/pants2.py -- $HERO $DESIGN shorts/${DESIGN}.blend > logs/pants2-$DESIGN.log 2>&1; grep -E "CAGE|FIT|PANTS" logs/pants2-$DESIGN.log
grep -q Traceback logs/pants2-$DESIGN.log && { grep -A12 Traceback logs/pants2-$DESIGN.log; exit 1; }
cp shorts/${DESIGN}.blend shorts/${DESIGN}_export.blend
CV2_WSMOOTH=6 $B -b shorts/${DESIGN}_export.blend --python py/export2.py -- $HERO Kit_Bottom shorts2 shorts/${DESIGN}_final.blend > logs/export2-$DESIGN.log 2>&1; grep -E "EXPORT2|Traceback" logs/export2-$DESIGN.log
$B -b --factory-startup --python py/cover.py -- $HERO > logs/cover-$HERO.log 2>&1; grep -E "Kit_Bottom|Traceback" logs/cover-$HERO.log | head -3
CV2_PIECES=Kit_Bottom $B -b --factory-startup --python py/maps.py -- $HERO > logs/maps-$HERO.log 2>&1; grep -E "Kit_|Traceback" logs/maps-$HERO.log
