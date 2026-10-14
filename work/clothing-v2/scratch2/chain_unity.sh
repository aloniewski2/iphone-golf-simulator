#!/bin/zsh
cd ${0:A:h:h}/../..
until grep -q ALL_POST_DONE work/clothing-v2/logs/chain_all.out; do sleep 3; done
export TURNAROUND_LOOK=target
work/clothing-v2/unity_iter.sh shoes_2 > work/clothing-v2/logs/unity_shoes_2.out 2>&1
echo UNITY2_DONE >> work/clothing-v2/logs/unity_shoes_2.out
