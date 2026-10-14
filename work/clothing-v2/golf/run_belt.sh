#!/bin/zsh
# belt v2 (golf): build (follows the fitted trousers/skort waistband) -> export -> cover -> maps   (work/clothing-v2/golf/run_belt.sh <hero> <design>)
set -e
cd ${0:A:h:h}
B=/Applications/Blender.app/Contents/MacOS/Blender
HERO=${1:-GolfKitHero_Male}; DESIGN=${2:-male_golf}
$B -b --factory-startup --python py/belt2.py -- $HERO $DESIGN golf/${HERO}_belt.blend > logs/belt2-$HERO.log 2>&1; grep -E "BELT" logs/belt2-$HERO.log
grep -q Traceback logs/belt2-$HERO.log && { grep -A12 Traceback logs/belt2-$HERO.log; exit 1; }
cp golf/${HERO}_belt.blend golf/${HERO}_belt_export.blend
CV2_WSMOOTH=3 $B -b golf/${HERO}_belt_export.blend --python py/export2.py -- $HERO Kit_Belt belt2 golf/${HERO}_belt_final.blend res=1024 margin=.0015 rotate=1 > logs/export2-$HERO-belt.log 2>&1; grep -E "EXPORT2|Traceback" logs/export2-$HERO-belt.log
