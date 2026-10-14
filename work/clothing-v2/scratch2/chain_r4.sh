#!/bin/zsh
cd ${0:A:h:h}
until grep -q "BUILDALL4 done" logs/server.log; do sleep 2; done
for H in MatchHero_Male MatchHero_Female GolfKitHero_Male GolfKitHero_Female; do scratch2/export_shoes.sh $H > logs/exp_$H.out 2>&1 & done
wait
until true; do sleep 3; done
for H in MatchHero_Male MatchHero_Female GolfKitHero_Male GolfKitHero_Female; do scratch2/post_shoes.sh $H > logs/post_$H.out 2>&1 & done
wait
echo R4_POST_DONE
cd ../..
export TURNAROUND_LOOK=target
work/clothing-v2/unity_iter.sh shoes_2 > work/clothing-v2/logs/unity_shoes_2.out 2>&1
echo UNITY2_DONE >> work/clothing-v2/logs/unity_shoes_2.out
