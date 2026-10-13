#!/bin/zsh
# CLOTHING V2 pipeline: garments for the four heroes -> Unity (in the APFS clone) -> turnaround.
#   work/clothing-v2/run.sh [heroes] [turnaround-out]      e.g. run.sh all after_5 ; run.sh MatchHero_Male - (no Unity)
set -e
HERE=${0:A:h}; cd $HERE
B=/Applications/Blender.app/Contents/MacOS/Blender
HEROES=${1:-all}; [[ $HEROES == all ]] && HEROES=MatchHero_Male,MatchHero_Female,GolfKitHero_Male,GolfKitHero_Female
PY=/Applications/Blender.app/Contents/Resources/5.2/python/bin/python3.13
[[ -f out/KitV2_FabricAtlas.png ]] || $PY py/atlas.py out/KitV2_FabricAtlas.png
for h in ${(s:,:)HEROES}; do
  for step in build finalize cover correctives cover maps; do
    args=($h); [[ $step == build ]] && args=($h all)
    $B -b --factory-startup --python py/$step.py -- $args > logs/$step-$h.log 2>&1 || { echo "$step $h FAILED"; tail -20 logs/$step-$h.log; exit 1; }
    grep -q "Traceback" logs/$step-$h.log && { echo "$step $h FAILED"; grep -A12 Traceback logs/$step-$h.log | head -20; exit 1; }
  done
  echo "built $h: $(grep -c 'Kit_' logs/build-$h.log) pieces"
done
[[ "$2" == "-" ]] && exit 0
./unity_iter.sh ${2:-after}
