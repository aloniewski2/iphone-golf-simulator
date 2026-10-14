#!/bin/zsh
# ACCESSORIES: the Unity proof renders for the review sheets (game shaders). Run after `unity_iter.sh install`.
#   final_proof.sh   -> proof/final/{tennis,golf,alone}/
set -e
H=${0:A:h}; O=$H/proof/final; mkdir -p $O
echo "== tennis heroes (tennis cap), bust + head"
ACC_PROOF_POSE=rest ACC_PROOF_SHOTS="front:bust:0,q34:bust:35,side:bust:90,head_q34:head:38" $H/unity_iter.sh proof $O/tennis MatchHero_Male,MatchHero_Female
echo "== golf heroes (golf cap, glove, bag carried)"
ACC_PROOF_BAG=1 ACC_PROOF_POSE=idle ACC_PROOF_SHOTS="front:full:0,q34:full:35,side:full:90,back34:full:150,bust_front:bust:0,bust_q34:bust:35,bust_side:bust:90,head_q34:head:38,head_side:head:90,hand_in:hand:35,hand_out:hand:-60,bag_back:back:150:8" $H/unity_iter.sh proof $O/golf GolfKitHero_Male,GolfKitHero_Female
for item in tennis_cap golf_cap golf_glove golf_bag; do
  echo "== alone: $item"
  hero=$([[ $item == tennis_cap ]] && echo MatchHero_Male || echo GolfKitHero_Male)
  ACC_ITEMS=$item ACC_PROOF_ALONE=1 ACC_PROOF_BAG=$([[ $item == golf_bag ]] && echo 1 || echo 0) ACC_PROOF_POSE=idle \
    ACC_PROOF_SHOTS="front:acc:0:8,side:acc:90:6,back:acc:180:8,q34:acc:38:20,top:acc:0:80,under:acc:0:-70" $H/unity_iter.sh proof $O/alone_$item $hero
done
echo done
