#!/bin/zsh
cd ${0:A:h:h}
B=/Applications/Blender.app/Contents/MacOS/Blender
H=$1
$B -b --factory-startup --python py/cover.py -- $H > logs/cover-$H.log 2>&1; grep -E "Traceback" logs/cover-$H.log
CV2_PIECES=Kit_Shoe_L,Kit_Shoe_R $B -b --factory-startup --python py/maps.py -- $H > logs/maps-$H.log 2>&1; grep -E "Kit_Shoe|Traceback" logs/maps-$H.log
echo "$H POST done"
