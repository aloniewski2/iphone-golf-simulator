#!/bin/sh
# BAG: full rebuild. Run from work/accessories:  sh py/bag_build_all.sh [fast]
BL=/Applications/Blender.app/Contents/MacOS/Blender
set -e
mkdir -p logs proof/bag
# 1. the product alone (maps baked) in both leg poses
$BL -b --factory-startup --python py/bag_export.py -- product folded > logs/product_folded.log 2>&1
$BL -b --factory-startup --python py/bag_export.py -- product open   > logs/product_open.log 2>&1
# 1b. the strap route per hero from the clearance map over the critical clips (start th, web offset)
$BL -b --factory-startup --python py/bag_route.py -- Male 12 1.335 44 0.010   > logs/route_male.log 2>&1
$BL -b --factory-startup --python py/bag_route.py -- Female 12 1.335 34 0.012 > logs/route_female.log 2>&1
# 2. fit + maps per hero (placement is optimised against the Idle/Walk/Run/Wave/Cheer/FistPump clips, so this takes ~1 min each)
$BL -b --factory-startup --python py/bag_fit.py -- Male   > logs/fit_male.log 2>&1
$BL -b --factory-startup --python py/bag_fit.py -- Female > logs/fit_female.log 2>&1
# 3. pose / clearance check over every dumped clip (golf heroes + tennis match heroes)
if [ "$1" != "fast" ]; then
  $BL -b --factory-startup --python py/bag_pose.py -- Male   > logs/pose_male.log 2>&1
  $BL -b --factory-startup --python py/bag_pose.py -- Female > logs/pose_female.log 2>&1
fi
# 4. review images
sh py/bag_proof_product.sh
python3 py/bag_sheet.py product
python3 py/bag_sheet.py closeups
sh py/bag_proof_hero.sh
python3 py/bag_sheet.py hero
for S in Male Female; do python3 py/bag_pose_images.py $S; python3 py/bag_sheet.py pose $S; done
python3 py/bag_validate.py
