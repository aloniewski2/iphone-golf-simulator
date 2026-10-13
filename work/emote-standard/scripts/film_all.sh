#!/bin/zsh
# film_all.sh <male|female> : tennis, golf and plaza films of the whole set in the clone (frames in film/all_<sport>_<sex>)
W=/Users/adnanyonathan/Documents/Codex/2026-09-20/wh/outputs/iphone-golf-simulator/work/emote-standard; S=$1; F=$([ $S = female ] && echo 1 || echo 0)
for mode in tennisall golfall huball; do
  $W/scripts/urun.sh $W/logs/film_${mode}_$S.log JF_MODE=$mode JF_FEMALE=$F JF_OUT=$W/film/all_${mode}_$S > $W/logs/film_${mode}_$S.out 2>&1
  echo "$mode: $(tail -1 $W/logs/film_${mode}_$S.out)"; grep -E "error CS|Exception" $W/logs/film_${mode}_$S.log | head -4
  cat $W/film/all_${mode}_$S/summary.txt 2>/dev/null
done
