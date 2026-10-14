import sys, os
sys.path.insert(0, '/Users/adnanyonathan/Documents/Codex/2026-09-20/wh/outputs/iphone-golf-simulator/work/accessories/py')
import numpy as np, bpy
import acc_common as A
sex = A.args_after_dashes()[0]
A.reset()
hd = A.HeroData(sex)
A.load_body(hd)
A.studio(res=(700, 900))
for name, az, el in (('front', 0, 6), ('side', 90, 6), ('back', 180, 6), ('back34', 150, 12)):
    A.camera((0, 0.0, 1.15), az, el, 3.0, 60)
    A.render(f'/Users/adnanyonathan/Documents/Codex/2026-09-20/wh/outputs/iphone-golf-simulator/work/accessories/scratch/body_{sex}_{name}.png')
