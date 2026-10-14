#!/bin/zsh
# export_shoes.sh <hero>: export L+R shoes (merges manifest), then skin masks + maps for the shoe pieces
cd ${0:A:h:h}
B=/Applications/Blender.app/Contents/MacOS/Blender
H=$1
for sd in L R; do
  $B -b shoes/${H}_shoe_${sd}_export.blend --python py/export2.py -- $H Kit_Shoe_$sd shoe2 shoes/${H}_shoe_${sd}_final.blend res=1024 > logs/export2-$H-shoe-$sd.log 2>&1; grep -E "EXPORT2|Traceback" logs/export2-$H-shoe-$sd.log
done
echo "$H EXPORTED"
