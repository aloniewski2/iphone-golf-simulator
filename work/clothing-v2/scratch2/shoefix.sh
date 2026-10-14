#!/bin/zsh
cd ${0:A:h:h}
B=/Applications/Blender.app/Contents/MacOS/Blender
cp scratch2/b9.py.txt scratch2/cmd_buildall9.py
until grep -q "BUILDALL9 done" logs/server.log; do sleep 2; done
one() { H=$1
  for sd in L R; do cp shoes/${H}_shoe_$sd.blend shoes/${H}_shoe_${sd}_export.blend
    $B -b shoes/${H}_shoe_${sd}_export.blend --python py/export2.py -- $H Kit_Shoe_$sd shoe2 shoes/${H}_shoe_${sd}_final.blend res=1024 > logs/export2-$H-shoe-$sd.log 2>&1; done
  scratch2/post_shoes.sh $H > logs/post_$H.out 2>&1 }
for H in MatchHero_Male MatchHero_Female GolfKitHero_Male GolfKitHero_Female; do one $H & done
wait
echo SHOEFIX_DONE
cd ../..
export TURNAROUND_LOOK=target
work/clothing-v2/unity_iter.sh shoes_5 > work/clothing-v2/logs/unity_shoes_5.out 2>&1
echo UNITY5_DONE
