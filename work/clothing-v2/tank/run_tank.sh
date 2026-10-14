#!/bin/zsh
# TANK v2 (female tennis): build -> export (merges the manifest) -> skin masks -> maps   (work/clothing-v2/tank/run_tank.sh)
set -e
cd ${0:A:h:h}
B=/Applications/Blender.app/Contents/MacOS/Blender
HERO=${1:-MatchHero_Female}; DESIGN=${2:-female_tank}
$B -b --factory-startup --python py/tank2.py -- $HERO $DESIGN tank/${DESIGN}.blend > logs/tank2-$DESIGN.log 2>&1; grep -E "FIT|SNAP|TANK" logs/tank2-$DESIGN.log
grep -q Traceback logs/tank2-$DESIGN.log && { grep -A12 Traceback logs/tank2-$DESIGN.log; exit 1; }
cp tank/${DESIGN}.blend tank/${DESIGN}_export.blend
CV2_WSMOOTH=3 $B -b tank/${DESIGN}_export.blend --python py/export2.py -- $HERO Kit_Top tank2 tank/${DESIGN}_final.blend > logs/export2-$DESIGN.log 2>&1; grep -E "EXPORT2|Traceback" logs/export2-$DESIGN.log
$B -b --factory-startup --python py/cover.py -- $HERO > logs/cover-$HERO.log 2>&1; grep -E "Kit_Top|Traceback" logs/cover-$HERO.log | head -3 || true
CV2_PIECES=Kit_Top $B -b --factory-startup --python py/maps.py -- $HERO > logs/maps-$HERO.log 2>&1; grep -E "Kit_|Traceback" logs/maps-$HERO.log
