#!/bin/zsh
# after polo2_export: skin masks -> pose correctives -> skin masks -> maps (Kit_Top only)   (work/clothing-v2/polo/run_polo_post.sh <hero>)
set -e
cd ${0:A:h:h}
B=/Applications/Blender.app/Contents/MacOS/Blender
HERO=${1:-MatchHero_Male}
rm -f out/$HERO/unity/correctives.npz out/$HERO/unity/Kit_Top_shapes.bin out/$HERO/unity/Kit_Top_correctives.json
$B -b --factory-startup --python py/cover.py -- $HERO > logs/cover-$HERO.log 2>&1
CV2_CORR_MODE=lift $B -b --factory-startup --python py/correctives.py -- $HERO 24 > logs/correctives-$HERO.log 2>&1; grep shapes logs/correctives-$HERO.log || true
$B -b --factory-startup --python py/cover.py -- $HERO > logs/cover-$HERO.log 2>&1; grep -E "Kit_Top" logs/cover-$HERO.log | head -3 || true
CV2_PIECES=Kit_Top $B -b --factory-startup --python py/maps.py -- $HERO > logs/maps-$HERO.log 2>&1; grep Kit_Top logs/maps-$HERO.log || true
CV2_PIECES=Kit_Top $B -b --factory-startup --python py/poke.py -- $HERO > logs/poke-top.log 2>&1; python3 py/pokesum.py out/$HERO/poke.json
