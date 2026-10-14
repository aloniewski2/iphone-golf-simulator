#!/bin/zsh
# usage: run_try.sh <hero> <design> <side> <tag> [views]   -> builds in the persistent session, renders sheet to shoes/sheet/<tag>.png
cd ${0:A:h:h}
N=$(date +%s%N 2>/dev/null || date +%s)
cat > scratch2/cmd_$N.py <<PY
import importlib, sys, subprocess, os
import shoe2; importlib.reload(shoe2)
shoe2.build('$1', '$2', '$3', 'shoes/try_$4.blend')
env = dict(os.environ, SHOE_VIEWS='${5:-side,3/4,top,bottom,back,front}')
subprocess.run(['/Applications/Blender.app/Contents/MacOS/Blender','-b','shoes/try_$4.blend','--python','py/shoe_sheet.py','--','shoes/sheet/$4.png'], env=env, capture_output=True)
PY
# wait for the server to pick it up and finish
until [[ -f scratch2/cmd_$N.py.done ]]; do sleep 1; done
until grep -q "DONE cmd_$N" logs/server.log; do sleep 1; done
grep -E "SHOE2|Traceback|Error" logs/server.log | tail -3
