#!/bin/zsh
cd ${0:A:h:h}
until grep -q "Kit_Top {" logs/chain_tank.out 2>/dev/null; do sleep 4; done
cp scratch2/b7.py.txt scratch2/cmd_buildall7.py
until grep -q "BUILDALL7 done" logs/server.log; do sleep 3; done
for H in MatchHero_Male MatchHero_Female GolfKitHero_Male GolfKitHero_Female; do scratch2/export_shoes.sh $H > logs/exp_$H.out 2>&1 & done
wait
echo EXPORTS7_DONE
for H in MatchHero_Male GolfKitHero_Male GolfKitHero_Female; do ./run_all_post.sh $H > logs/post_all_$H.out 2>&1 & done
( polo/run_polo_post.sh MatchHero_Female > logs/post_polo_female.out 2>&1; scratch2/post_shoes.sh MatchHero_Female > logs/post_MatchHero_Female.out 2>&1 ) &
wait
echo POST7_DONE
cd ../..
export TURNAROUND_LOOK=target
work/clothing-v2/unity_iter.sh shoes_3 > work/clothing-v2/logs/unity_shoes_3.out 2>&1
echo UNITY3_DONE
