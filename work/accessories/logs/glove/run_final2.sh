#!/bin/zsh
cd /Users/adnanyonathan/Documents/Codex/2026-09-20/wh/outputs/iphone-golf-simulator/work/accessories
B=/Applications/Blender.app/Contents/MacOS/Blender
$B -b --factory-startup --python py/glove_run_product.py -- > logs/glove/product_closed.log 2>&1
$B -b --factory-startup --python py/glove_run_product.py -- open > logs/glove/product_open.log 2>&1
for s in Male Female; do
  $B -b --factory-startup --python py/glove_export.py -- $s Left > logs/glove/export_$s.log 2>&1
  $B -b --factory-startup --python py/glove_export.py -- $s Right > logs/glove/export_${s}_R.log 2>&1
done
$B -b --factory-startup --python py/glove_save_blend.py > logs/glove/blend.log 2>&1
echo done > logs/glove/run_final2.done
