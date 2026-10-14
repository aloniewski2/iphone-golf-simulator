#!/bin/zsh
# fast tank loop: build in the warm session + render back / front-left  -> tank/dbg/<tag>.png   (usage: tank_try.sh <tag> [VAR=val ...])
cd ${0:A:h:h}
TAG=$1; shift
ENVS=""
for kv in "$@"; do ENVS="${ENVS}os.environ['${kv%%=*}']='${kv#*=}'\n"; done
N=$(date +%s%N)
printf "import os, importlib, math, builtins, bpy, numpy as np\n${ENVS}import tank2; importlib.reload(tank2)\ntank2.build('MatchHero_Female','female_tank','tank/try_$TAG.blend')\nimport bl; importlib.reload(bl)\nfrom mathutils import Vector\nhero, body = builtins._HCACHE['MatchHero_Female']\nsc = bpy.context.scene\nfor o in list(sc.objects):\n    if o.type in ('LIGHT','CAMERA'): bpy.data.objects.remove(o)\nk = bpy.data.objects['Kit_Top']; k.data.materials.clear(); k.data.materials.append(bl.material('c', bl.srgb('F2F0EA'), .6))\nb = bl.mesh_object('Body', body.pos, body.tri, normals=body.nrm); b.data.materials.append(bl.material('S', bl.srgb('B7744A'), .55))\nroot = bpy.data.objects.new('M', None); sc.collection.objects.link(root); root.scale=(-1,1,1); k.parent = root; b.parent = root\nbl.studio(res=(800,900))\nfor nm, az in (('back',180),('fl',-35)):\n    bl.camera((0,0,1.28), az, 8, 1.3, 70); bl.render('tank/dbg/${TAG}_%%s.png' %% nm)\nprint('TANKTRY done')\n" > scratch2/cmd_$N.py
until grep -q "DONE cmd_$N" logs/server.log; do sleep 1; done
grep -E "Traceback|Error" logs/server.log | tail -2
cd tank/dbg && python3 ../../py/montage.py $TAG.png 2 ${TAG}_back.png ${TAG}_fl.png
