"""Save the product (closed + velcro-open states, maps wired up) as products/golf_glove_v1.blend. Blender -b --python glove_save_blend.py"""
import sys, os
sys.path.insert(0, os.path.dirname(__file__))
import numpy as np, bpy
import acc_common as A, glove_render_product as GRP

A.reset()
ob_c = GRP.load_piece(os.path.join(GRP.PROD, 'closed')); ob_c.name = 'Kit_Glove_L_closed'
ob_o = GRP.load_piece(os.path.join(GRP.PROD, 'open')); ob_o.name = 'Kit_Glove_L_tab_open'
ob_o.location = (0.0, 0.0, 0.0); ob_o.hide_viewport = True; ob_o.hide_render = True
for ob in (ob_c, ob_o):
    ob.data.name = ob.name
# pack the maps into the file so it opens standalone
for img in bpy.data.images:
    try: img.pack()
    except Exception: pass
out = os.path.join(A.ROOT, 'products', 'golf_glove_v1.blend')
bpy.ops.wm.save_as_mainfile(filepath=out)
print('saved', out)
