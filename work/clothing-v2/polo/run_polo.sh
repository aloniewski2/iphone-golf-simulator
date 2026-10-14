#!/bin/zsh
# POLO v2 (male tennis): build -> export -> skin masks -> maps   (work/clothing-v2/polo/run_polo.sh)
set -e
cd ${0:A:h:h}
B=/Applications/Blender.app/Contents/MacOS/Blender
$B -b --factory-startup --python py/polo2.py -- MatchHero_Male polo/polo_v2.blend > logs/polo2.log 2>&1; grep -E "POLO|FIT" logs/polo2.log
grep -q Traceback logs/polo2.log && { grep -A12 Traceback logs/polo2.log; exit 1; }
cp polo/polo_v2.blend polo/polo_v2_export.blend
CV2_WSMOOTH=8 $B -b polo/polo_v2_export.blend --python py/polo2_export.py -- MatchHero_Male > logs/polo2_export.log 2>&1; grep POLO2 logs/polo2_export.log
rm -f out/MatchHero_Male/unity/correctives.npz out/MatchHero_Male/unity/Kit_Top_shapes.bin out/MatchHero_Male/unity/Kit_Top_correctives.json
$B -b --factory-startup --python py/cover.py -- MatchHero_Male > logs/cover-MatchHero_Male.log 2>&1
CV2_CORR_MODE=lift $B -b --factory-startup --python py/correctives.py -- MatchHero_Male 24 > logs/correctives-MatchHero_Male.log 2>&1; grep shapes logs/correctives-MatchHero_Male.log
$B -b --factory-startup --python py/cover.py -- MatchHero_Male > logs/cover-MatchHero_Male.log 2>&1; grep -E "Kit_|uncovers" logs/cover-MatchHero_Male.log | head -4
$B -b --factory-startup --python py/maps.py -- MatchHero_Male > logs/maps-MatchHero_Male.log 2>&1; grep Kit_ logs/maps-MatchHero_Male.log
