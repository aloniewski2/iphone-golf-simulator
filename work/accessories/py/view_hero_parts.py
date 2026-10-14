"""Look at a hero's head / left hand / back in N (no accessories). Blender -b --python view_hero_parts.py -- <Male|Female> <outdir>"""
import sys, os
sys.path.insert(0, os.path.dirname(__file__))
import numpy as np, bpy
import acc_common as A

sex, outdir = A.args_after_dashes()[:2]
A.reset()
hd = A.HeroData(sex)
A.load_body(hd)
A.studio(res=(700, 700))
head = hd.rest_pos['Head']; hand = hd.rest_pos['LeftHand']; chest = hd.rest_pos['UpperChest']
print('N positions: head', head, 'hand', hand, 'upperchest', chest)
for name, tgt, dist, shots in (('head', (0, -.02, 1.60), 1.0, ((0, 5), (90, 5), (45, 15), (180, 5), (0, 60))),
                               ('hand', tuple(hand), .45, ((0, 20), (90, 20), (180, 20), (0, 70)))):
    for az, el in shots:
        A.camera(tgt, az, el, dist, 85)
        A.render(os.path.join(outdir, f'{sex}_{name}_{az}_{el}.png'))
