#!/bin/zsh
# polo v2 for any hero: build -> export (merges manifest) -> cover -> correctives -> cover -> maps -> poke   (work/clothing-v2/polo/run_polo_any.sh <hero> <design> [plain 0/1])
set -e
cd ${0:A:h:h}
B=/Applications/Blender.app/Contents/MacOS/Blender
HERO=$1; DESIGN=$2; PLAIN=${3:-0}
mkdir -p polo/$HERO
$B -b --factory-startup --python py/polo2.py -- $HERO polo/$HERO/polo_v2.blend $DESIGN > logs/polo2-$HERO.log 2>&1; grep -E "POLO|FIT" logs/polo2-$HERO.log
grep -q Traceback logs/polo2-$HERO.log && { grep -A12 Traceback logs/polo2-$HERO.log; exit 1; }
cp polo/$HERO/polo_v2.blend polo/$HERO/polo_v2_export.blend
if [[ "$PLAIN" == "1" ]]; then export CV2_PLAIN=1; fi
CV2_WSMOOTH=8 $B -b polo/$HERO/polo_v2_export.blend --python py/polo2_export.py -- $HERO > logs/polo2_export-$HERO.log 2>&1; grep -E "POLO2|Traceback" logs/polo2_export-$HERO.log
