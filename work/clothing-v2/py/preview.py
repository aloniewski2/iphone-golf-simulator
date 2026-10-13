"""Preview renders: body (+ garments from out/<hero>/*.npz) in Blender. Usage:
Blender -b --factory-startup --python preview.py -- <hero> <outdir> [views=front,left,back,fl] [kit=old|new|none] [engine]"""
import sys, os, glob, math
sys.path.insert(0, os.path.dirname(__file__))
import numpy as np
import bpy
import cv2io, bl

argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
hero_id = argv[0] if argv else 'MatchHero_Male'
outdir = argv[1] if len(argv) > 1 else os.path.join(cv2io.ROOT, 'proof', 'preview')
views = (argv[2] if len(argv) > 2 else 'front,fl,left,back').split(',')
kitmode = argv[3] if len(argv) > 3 else 'new'
engine = argv[4] if len(argv) > 4 else 'BLENDER_EEVEE'
os.makedirs(outdir, exist_ok=True)

bl.reset()
hero = cv2io.Hero(hero_id)
root = bpy.data.objects.new('Mirror', None); bpy.context.scene.collection.objects.link(root); root.scale = (-1, 1, 1)
skin = bl.material('Skin', bl.srgb('B7744A'), .55)
b = bl.mesh_object('Body', hero.body.pos, hero.body.tri, normals=hero.body.nrm); b.parent = root; b.data.materials.append(skin)
if os.environ.get('CV2_NOBODY'): b.hide_render = True

PALETTE = {}
def mat_for(role):
    if role not in PALETTE:
        col = {'Kit_Shirt': 'F4F0E6', 'Kit_ShirtTrim': '1D2B4F', 'Kit_Shorts': '22315A', 'Kit_ShortsBand': '22315A', 'Kit_Shoe': 'F6F6F4', 'Kit_Sole': 'E8E8E4', 'Kit_Sock': 'F7F7F5',
               'Kit_GolfGlove': 'F7F7F5', 'Kit_GolfHardware': 'A0A3A8', 'Kit_Belt': '6B4429'}.get(role.replace(' (hero)', ''), 'FF00FF')
        m = bl.material(role, bl.srgb(col), .75, .4)
        nt = m.node_tree; attr = nt.nodes.new('ShaderNodeAttribute'); attr.attribute_name = 'C'
        mix = nt.nodes.new('ShaderNodeMix'); mix.data_type = 'RGBA'; mix.blend_type = 'MULTIPLY'; mix.inputs['Factor'].default_value = 1
        mix.inputs[6].default_value = (*bl.srgb(col), 1)
        nt.links.new(attr.outputs['Color'], mix.inputs[7]); nt.links.new(mix.outputs[2], nt.nodes['Principled BSDF'].inputs['Base Color'])
        PALETTE[role] = m
    return PALETTE[role]

if kitmode == 'old':
    for name, p in hero.kit.items():
        o = bl.mesh_object(name, p.pos, p.tri, normals=p.nrm); o.parent = root
        for s in p.subs: o.data.materials.append(mat_for(s['material']))
        mi = np.zeros(len(p.tri), np.int32)
        for k, s in enumerate(p.subs): mi[s['start']:s['start'] + s['count']] = k
        o.data.polygons.foreach_set('material_index', mi)
elif kitmode == 'new':
    for f in sorted(glob.glob(os.path.join(cv2io.ROOT, 'out', hero_id, 'Kit_*.npz'))):
        g = np.load(f, allow_pickle=True)
        o = bl.mesh_object(os.path.basename(f)[:-4], g['pos'], g['tri'], normals=g['nrm'] if 'nrm' in g else None); o.parent = root
        roles = list(g['roles']); mi = g['mat'] if 'mat' in g else np.zeros(len(g['tri']), np.int32)
        for r in roles: o.data.materials.append(mat_for(str(r)))
        o.data.polygons.foreach_set('material_index', mi.astype(np.int32))
        if os.environ.get('CV2_FABRIC') and 'fabric' in g:
            pal = {7: (1, 1, 1), 9: (.6, .8, 1), 6: (1, 1, .6), 8: (1, .7, .5), 2: (.5, 1, .5), 3: (.2, .2, .2), 4: (.9, .9, .7), 5: (1, .2, .2)}
            fc = np.array([pal.get(int(f), (1, 0, 1)) for f in g['fabric']])
            vc = np.ones((len(g['pos']), 3)); vc[g['tri'][:, 0]] = fc; vc[g['tri'][:, 1]] = fc; vc[g['tri'][:, 2]] = fc
            g = dict(g); g['col'] = vc
        if 'col' in g:   # per-vertex shade (AO / stripes) for previews
            a = o.data.color_attributes.new('C', 'FLOAT_COLOR', 'POINT'); c = np.concatenate([g['col'], np.ones((len(g['col']), 1))], 1)
            a.data.foreach_set('color', c.astype(np.float32).ravel())

bl.studio(engine)
H = {'front': 0, 'fl': -35, 'left': -90, 'back': 180, 'fr': 35, 'right': 90, 'bl': -145}
for v in views:
    name, _, crop = v.partition(':')
    if crop == 'top': tgt, dist, lens = (0, -.02, 1.15), 1.6, 70
    elif crop == 'bottom': tgt, dist, lens = (0, -.02, .7), 1.8, 70
    elif crop == 'feet': tgt, dist, lens = (0, -.05, .1), 1.1, 70
    elif crop == 'feettop': tgt, dist, lens = (.2, -.07, .05), .9, 70
    elif crop == 'shoe': tgt, dist, lens = (.21, -.07, .05), .55, 70
    elif crop == 'shoulders': tgt, dist, lens = (0, .0, 1.28), 1.1, 70
    elif crop == 'heel': tgt, dist, lens = (.19, .0, .05), .5, 70
    else: tgt, dist, lens = (0, -.02, .86), 4.6, 70
    bl.camera(tgt, H[name], {'feet': 18, 'feettop': 70, 'shoe': 35, 'heel': 25, 'shoulders': 55}.get(crop, 4), dist, lens)
    bl.render(os.path.join(outdir, f'{hero_id}_{kitmode}_{v.replace(":", "_")}.png'))
