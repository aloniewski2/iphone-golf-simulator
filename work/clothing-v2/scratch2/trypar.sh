#!/bin/zsh
# trypar.sh <tag> <hero> <design> <side> VAR=val ... : build with env overrides in the persistent session and render
cd ${0:A:h:h}
TAG=$1; HERO=$2; DES=$3; SIDE=$4; shift 4
ENVS=""
for kv in "$@"; do ENVS="${ENVS}os.environ['${kv%%=*}']='${kv#*=}'
"; done
N=$(date +%s%N)
cat > scratch2/cmd_$N.py <<PY
import os, importlib, subprocess
$ENVS
import shoe2; importlib.reload(shoe2)
shoe2.build('$HERO', '$DES', '$SIDE', 'shoes/try_$TAG.blend')
for k in list(os.environ):
    if k.startswith('SHOE_'): del os.environ[k]
env = dict(os.environ, SHOE_VIEWS='side,3/4,top,bottom,back,front', SHOE_FAST='1', SHOE_SOLE='DCDDD2')
subprocess.run(['/Applications/Blender.app/Contents/MacOS/Blender','-b','shoes/try_$TAG.blend','--python','py/shoe_sheet.py','--','shoes/sheet/$TAG.png'], env=env, capture_output=True)
PY
until grep -q "DONE cmd_$N" logs/server.log; do sleep 1; done
grep -E "Traceback|Error" logs/server.log | tail -2
cd shoes/sheet && python3 ../../py/montage.py $TAG.png 3 ${TAG}_side.png ${TAG}_34.png ${TAG}_top.png ${TAG}_bottom.png ${TAG}_back.png ${TAG}_front.png
