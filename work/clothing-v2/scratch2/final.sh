#!/bin/zsh
# FINAL pass: everything in parallel per hero (manifest merges are per hero, so each hero's pieces run in order)
cd ${0:A:h:h}
B=/Applications/Blender.app/Contents/MacOS/Blender
cp scratch2/b8.py.txt scratch2/cmd_buildall8.py
until grep -q "BUILDALL8 done" logs/server.log; do sleep 2; done
echo BUILT8
shoes_and_socks() {   # <hero> <sockdesign|none>
  H=$1
  for sd in L R; do
    cp shoes/${H}_shoe_$sd.blend shoes/${H}_shoe_${sd}_export.blend
    $B -b shoes/${H}_shoe_${sd}_export.blend --python py/export2.py -- $H Kit_Shoe_$sd shoe2 shoes/${H}_shoe_${sd}_final.blend res=1024 > logs/export2-$H-shoe-$sd.log 2>&1
    if [[ "$2" != none ]]; then
      $B -b --factory-startup --python py/sock2.py -- $H $2 $sd shoes/${H}_sock_$sd.blend > logs/sock2-$H-$sd.log 2>&1
      cp shoes/${H}_sock_$sd.blend shoes/${H}_sock_${sd}_export.blend
      $B -b shoes/${H}_sock_${sd}_export.blend --python py/export2.py -- $H Kit_Sock_$sd sock2 shoes/${H}_sock_${sd}_final.blend res=1024 wsmooth=4 > logs/export2-$H-sock-$sd.log 2>&1
    fi
  done
}
( shoes_and_socks MatchHero_Male male_crew; ./run_all_post.sh MatchHero_Male > logs/post_all_MatchHero_Male.out 2>&1; echo M_DONE ) &
( tank/run_tank.sh > logs/chain_tank.out 2>&1; shoes_and_socks MatchHero_Female female_ankle; polo/run_polo_post.sh MatchHero_Female > logs/post_polo_female.out 2>&1; scratch2/post_shoes.sh MatchHero_Female > logs/post_MatchHero_Female.out 2>&1; echo F_DONE ) &
( golf/run_trousers.sh GolfKitHero_Male golf_male_trousers > logs/chain_trousers.out 2>&1; shoes_and_socks GolfKitHero_Male none; ./run_all_post.sh GolfKitHero_Male > logs/post_all_GolfKitHero_Male.out 2>&1; echo GM_DONE ) &
( polo/run_polo_any.sh GolfKitHero_Female golf_female 1 > logs/chain_polo_gf2.out 2>&1; shoes_and_socks GolfKitHero_Female none; polo/run_polo_post.sh GolfKitHero_Female > logs/post_polo_gf.out 2>&1; ./run_all_post.sh GolfKitHero_Female > logs/post_all_GolfKitHero_Female.out 2>&1; echo GF_DONE ) &
wait
echo ALL8_DONE
cd ../..
export TURNAROUND_LOOK=target
work/clothing-v2/unity_iter.sh shoes_4 > work/clothing-v2/logs/unity_shoes_4.out 2>&1
echo UNITY4_DONE
