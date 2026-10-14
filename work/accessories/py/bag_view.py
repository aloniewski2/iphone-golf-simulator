"""BAG: quick preview renders of product parts with flat materials by kind (before maps exist). Blender python."""
import sys, os, math
sys.path.insert(0, os.path.dirname(__file__))
import numpy as np, bpy
import acc_common as A, acc_geo as G
import bag as B

NAVY = {B.K['LINING'], B.K['BASE'], B.K['POCKET'], B.K['ZIP'], B.K['HUB'], B.K['BACKPOCKET'], B.K['GRIP'], B.K['PIPING'], B.K['FOOT'], B.K['FLOOR']}

def kind_of(ob):
    me = ob.data
    if 'kind' not in me.attributes or not len(me.polygons): return 0
    v = np.zeros(len(me.polygons), np.int32); me.attributes['kind'].data.foreach_get('value', v); return int(v[0])

def flat_materials(parts):
    white = A.material('PV_white', A.srgb('EEEDE8'), .6, .3)
    navy = A.material('PV_navy', A.srgb('1F2B57'), .55, .25)
    metal = A.material('PV_metal', A.srgb('B8BCC4'), .3, 0, metallic=.9)
    dark = A.material('PV_dark', A.srgb('15171C'), .45, 0)
    for ob in parts:
        k = kind_of(ob)
        ob.data.materials.clear()
        ob.data.materials.append(metal if k in B.METAL_KINDS else navy if k in NAVY else white)
        if ob.data.polygons and 'kind' in ob.data.attributes and False: pass

def views_render(outdir, prefix, views, target=(0, 0.0, 0.43), dist=2.2, lens=70, res=(800, 900)):
    A.studio(res=res, bg=(.34, .35, .37))
    for name, (az, el) in views.items():
        A.camera(target, az, el, dist, lens)
        A.render(os.path.join(outdir, f'{prefix}_{name}.png'))
