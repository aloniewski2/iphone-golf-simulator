#!/bin/zsh
# usage: shoe_look.sh <blend> <outdir> <prefix> <body 0/1> [tgt x,y,z (mesh space, right foot positive x)] [dist] [views]
B=/Applications/Blender.app/Contents/MacOS/Blender
cd ${0:A:h:h}
X=${5:-.21,-.06,.06}; MX=$(python3 -c "print(-float('${X%%,*}'))"); REST=${X#*,}
CV2_TGT="$MX,$REST" CV2_DIST=${6:-.9} $B -b $1 --python py/look.py -- ${CV2_HERO:-MatchHero_Male} $2 $3 $4 ${7:-fl,side,back,front} 2>&1 | tail -1
python3 - <<PY
from PIL import Image
v='${7:-fl,side,back,front}'.split(','); ims=[Image.open('$2/$3_%s.png'%n.split(':')[0].replace(':','_')).convert('RGB') for n in v]
w,h=ims[0].size
s=Image.new('RGB',(w*len(ims)//2,h//2))
for i,im in enumerate(ims): s.paste(im.resize((w//2,h//2)),(i*w//2,0))
s.save('$2/$3_sheet.png')
PY
