#!/bin/zsh
cd /Users/adnanyonathan/Documents/Codex/2026-09-20/wh/outputs/iphone-golf-simulator/work/accessories
B=/Applications/Blender.app/Contents/MacOS/Blender
$B -b --factory-startup --python py/glove_relax_caches.py -- Male Male_R Female Female_R > logs/glove/relax.log 2>&1
for s in Male Female; do
  $B -b --factory-startup --python py/glove_export.py -- $s Left > logs/glove/export_$s.log 2>&1
  $B -b --factory-startup --python py/glove_export.py -- $s Right > logs/glove/export_${s}_R.log 2>&1
  $B -b --factory-startup --python py/glove_pose.py -- $s Left > logs/glove/pose_$s.log 2>&1
  $B -b --factory-startup --python py/glove_pose.py -- $s Right > logs/glove/pose_${s}_R.log 2>&1
done
echo done > logs/glove/run_relax.done
