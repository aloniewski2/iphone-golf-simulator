#!/bin/zsh
cd /Users/adnanyonathan/Documents/Codex/2026-09-20/wh/outputs/iphone-golf-simulator/work/accessories
B=/Applications/Blender.app/Contents/MacOS/Blender
for s in Male Female; do
  $B -b --factory-startup --python py/glove_export.py -- $s Left > logs/glove/export_$s.log 2>&1
  $B -b --factory-startup --python py/glove_export.py -- $s Right > logs/glove/export_${s}_R.log 2>&1
done
for s in Male Female; do
  $B -b --factory-startup --python py/glove_pose.py -- $s Left > logs/glove/pose_$s.log 2>&1
  $B -b --factory-startup --python py/glove_pose.py -- $s Right > logs/glove/pose_${s}_R.log 2>&1
done
echo done > logs/glove/run_final3.done
