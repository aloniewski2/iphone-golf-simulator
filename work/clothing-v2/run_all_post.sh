#!/bin/zsh
# after every piece is exported: skin masks (cover) + maps for the four heroes   (work/clothing-v2/run_all_post.sh [hero...])
cd ${0:A:h}
B=/Applications/Blender.app/Contents/MacOS/Blender
HEROES=(MatchHero_Male MatchHero_Female GolfKitHero_Male GolfKitHero_Female)
[[ $# -gt 0 ]] && HEROES=($@)
for H in $HEROES; do
  $B -b --factory-startup --python py/cover.py -- $H > logs/cover-$H.log 2>&1; grep -E "Traceback" logs/cover-$H.log
  $B -b --factory-startup --python py/maps.py -- $H > logs/maps-$H.log 2>&1; grep -E "Traceback" logs/maps-$H.log
  echo "$H post done"
done
