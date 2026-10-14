#!/bin/zsh
# SHOES + SOCKS v2 (tennis male): build -> export (merges the manifest) -> skin masks -> maps   (work/clothing-v2/shoes/run_shoes.sh <hero> <shoe design> <sock design>)
set -e
cd ${0:A:h:h}
B=/Applications/Blender.app/Contents/MacOS/Blender
HERO=${1:-MatchHero_Male}; SHOE=${2:-male_tennis}; SOCK=${3:-male_crew}
for sd in L R; do
  $B -b --factory-startup --python py/shoe2.py -- $HERO $SHOE $sd shoes/${HERO}_shoe_$sd.blend > logs/shoe2-$HERO-$sd.log 2>&1; grep -E "SHOE2" logs/shoe2-$HERO-$sd.log
  grep -q Traceback logs/shoe2-$HERO-$sd.log && { grep -A12 Traceback logs/shoe2-$HERO-$sd.log; exit 1; }
  cp shoes/${HERO}_shoe_$sd.blend shoes/${HERO}_shoe_${sd}_export.blend
  $B -b shoes/${HERO}_shoe_${sd}_export.blend --python py/export2.py -- $HERO Kit_Shoe_$sd shoe2 shoes/${HERO}_shoe_${sd}_final.blend res=1024 > logs/export2-$HERO-shoe-$sd.log 2>&1; grep -E "EXPORT2|Traceback" logs/export2-$HERO-shoe-$sd.log
  $B -b --factory-startup --python py/sock2.py -- $HERO $SOCK $sd shoes/${HERO}_sock_$sd.blend > logs/sock2-$HERO-$sd.log 2>&1; grep -E "SOCK2" logs/sock2-$HERO-$sd.log
  grep -q Traceback logs/sock2-$HERO-$sd.log && { grep -A12 Traceback logs/sock2-$HERO-$sd.log; exit 1; }
  cp shoes/${HERO}_sock_$sd.blend shoes/${HERO}_sock_${sd}_export.blend
  $B -b shoes/${HERO}_sock_${sd}_export.blend --python py/export2.py -- $HERO Kit_Sock_$sd sock2 shoes/${HERO}_sock_${sd}_final.blend res=1024 wsmooth=4 > logs/export2-$HERO-sock-$sd.log 2>&1; grep -E "EXPORT2|Traceback" logs/export2-$HERO-sock-$sd.log
done
$B -b --factory-startup --python py/cover.py -- $HERO > logs/cover-$HERO.log 2>&1; grep -E "Kit_Sock|Kit_Shoe|Traceback" logs/cover-$HERO.log | head -8
CV2_PIECES=Kit_Shoe_L,Kit_Shoe_R,Kit_Sock_L,Kit_Sock_R $B -b --factory-startup --python py/maps.py -- $HERO > logs/maps-$HERO.log 2>&1; grep -E "Kit_|Traceback" logs/maps-$HERO.log
