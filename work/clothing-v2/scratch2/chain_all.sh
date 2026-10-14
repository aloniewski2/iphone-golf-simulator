#!/bin/zsh
cd ${0:A:h:h}
until grep -q "BUILDALL2 done" logs/server.log; do sleep 2; done
for H in MatchHero_Male MatchHero_Female GolfKitHero_Male GolfKitHero_Female; do scratch2/export_shoes.sh $H > logs/exp_$H.out 2>&1 & done
wait
echo EXPORTS_DONE
# cover + maps for every piece of the four heroes (garments changed for the golf heroes, shoes for all)
for H in MatchHero_Male MatchHero_Female GolfKitHero_Male GolfKitHero_Female; do ( ./run_all_post.sh $H > logs/post_all_$H.out 2>&1 ) & done
wait
echo ALL_POST_DONE
