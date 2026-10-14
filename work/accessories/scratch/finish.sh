#!/bin/sh
cd /Users/adnanyonathan/Documents/Codex/2026-09-20/wh/outputs/iphone-golf-simulator/work/accessories
BL=/Applications/Blender.app/Contents/MacOS/Blender
$BL -b --factory-startup --python py/bag_fit.py -- Female > logs/fit_female.log 2>&1
$BL -b --factory-startup --python py/bag_pose.py -- Female > logs/pose_female.log 2>&1
sh py/bag_proof_hero.sh
python3 py/bag_sheet.py hero
python3 py/bag_pose_images.py Female
python3 py/bag_sheet.py pose Female
python3 py/bag_validate.py > logs/validate.log 2>&1
echo done > logs/finish.done
