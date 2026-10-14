"""Studio renders of a .blend garment: alone (no body) or on the hero.  Blender -b <file.blend> --python look.py -- <hero> <outdir> <prefix> [body=0/1] [views]
views: front,side,back,fl  (+ crops: collar, hem, cuff)"""
import sys, os, math
sys.path.insert(0, os.path.dirname(__file__))
import numpy as np, bpy
from mathutils import Vector
import cv2io, bl

a = sys.argv[sys.argv.index('--') + 1:]
hero_id, outdir, prefix = a[0], a[1], a[2]
with_body = len(a) > 3 and a[3] == '1'
views = (a[4] if len(a) > 4 else 'front,side,back,fl').split(',')
os.makedirs(outdir, exist_ok=True)
sc = bpy.context.scene
for o in list(sc.objects):
    if o.type in ('LIGHT', 'CAMERA'): bpy.data.objects.remove(o)
# hide cages / helpers, keep garment objects
garment = [o for o in sc.objects if o.type == 'MESH' and not o.name.endswith('_cage')]
for o in sc.objects:
    if o.name.endswith('_cage'): o.hide_render = True
root = bpy.data.objects.new('Mirror', None); sc.collection.objects.link(root); root.scale = (-1, 1, 1)
for o in garment: o.parent = root
cloth = bl.material('Cloth', bl.srgb(os.environ.get('CV2_CLOTH', 'ECE8DE')), .85, .5)
trim = bl.material('Trim', bl.srgb('22305A'), .7)
for o in garment:
    if not o.data.materials: o.data.materials.append(cloth)
    if 'trim' in o.data.attributes and o.data.attributes['trim'].domain == 'POINT':
        # vertex trim mask -> mix cloth / navy
        m = bpy.data.materials.new(o.name + '_m'); m.use_nodes = True; nt = m.node_tree
        bsdf = nt.nodes['Principled BSDF']; bsdf.inputs['Roughness'].default_value = .85
        at = nt.nodes.new('ShaderNodeAttribute'); at.attribute_name = 'trim'
        mix = nt.nodes.new('ShaderNodeMix'); mix.data_type = 'RGBA'
        mix.inputs[6].default_value = (*bl.srgb(os.environ.get('CV2_CLOTH', 'ECE8DE')), 1); mix.inputs[7].default_value = (*bl.srgb('22305A'), 1)
        nt.links.new(at.outputs['Fac'], mix.inputs['Factor']); nt.links.new(mix.outputs[2], bsdf.inputs['Base Color'])
        o.data.materials.clear(); o.data.materials.append(m)
MAPS = os.environ.get('CV2_MAPS')
if MAPS:
    for o in garment:
        if not o.data.uv_layers: continue
        m = bpy.data.materials.new(o.name + '_maps'); m.use_nodes = True; nt = m.node_tree
        bsdf = nt.nodes['Principled BSDF']; bsdf.inputs['Roughness'].default_value = .85
        try: bsdf.inputs['Sheen Weight'].default_value = .4
        except KeyError: pass
        imN = bpy.data.images.load(os.path.join(MAPS, os.environ.get('CV2_TAG', 'Top') + '_N.png')); imN.colorspace_settings.name = 'Non-Color'
        imM = bpy.data.images.load(os.path.join(MAPS, os.environ.get('CV2_TAG', 'Top') + '_M.png')); imM.colorspace_settings.name = 'Non-Color'
        tN = nt.nodes.new('ShaderNodeTexImage'); tN.image = imN
        tM = nt.nodes.new('ShaderNodeTexImage'); tM.image = imM
        nmap = nt.nodes.new('ShaderNodeNormalMap'); nt.links.new(tN.outputs['Color'], nmap.inputs['Color']); nt.links.new(nmap.outputs['Normal'], bsdf.inputs['Normal'])
        sep = nt.nodes.new('ShaderNodeSeparateColor'); nt.links.new(tM.outputs['Color'], sep.inputs['Color'])
        mix = nt.nodes.new('ShaderNodeMix'); mix.data_type = 'RGBA'
        mix.inputs[6].default_value = (*bl.srgb(os.environ.get('CV2_CLOTH', 'ECE8DE')), 1); mix.inputs[7].default_value = (*bl.srgb('22305A'), 1)
        nt.links.new(sep.outputs['Blue'], mix.inputs['Factor'])
        cav = nt.nodes.new('ShaderNodeMath'); cav.operation = 'MULTIPLY_ADD'; cav.inputs[1].default_value = .6; cav.inputs[2].default_value = .4
        nt.links.new(sep.outputs['Green'], cav.inputs[0])
        mul = nt.nodes.new('ShaderNodeMix'); mul.data_type = 'RGBA'; mul.blend_type = 'MULTIPLY'; mul.inputs['Factor'].default_value = 1
        nt.links.new(mix.outputs[2], mul.inputs[6]); nt.links.new(cav.outputs[0], mul.inputs[7])
        nt.links.new(mul.outputs[2], bsdf.inputs['Base Color'])
        o.data.materials.clear(); o.data.materials.append(m)
if with_body:
    hero = cv2io.Hero(hero_id)
    b = bl.mesh_object('Body', hero.body.pos, hero.body.tri, normals=hero.body.nrm); b.parent = root
    b.data.materials.append(bl.material('Skin', bl.srgb('B7744A'), .55))
bl.studio()
H = {'front': 0, 'fl': -35, 'side': -90, 'back': 180, 'fr': 35, 'bl': -145}
for v in views:
    name, _, crop = v.partition(':')
    if crop == 'collar': tgt, dist, el = (0, -.06, 1.35), .62, 14
    elif crop == 'hem': tgt, dist, el = (.08, -.06, .90), .62, 6
    elif crop == 'cuff': tgt, dist, el = (.30, -.05, 1.20), .75, 5
    elif crop == 'waist': tgt, dist, el = (0, -.12, .84), .75, 8
    elif crop == 'leg': tgt, dist, el = (.12, -.05, .68), 1.0, 6
    elif crop == 'crotch': tgt, dist, el = (0, -.05, .70), .9, -18
    elif crop == 'shoe': tgt, dist, el = (.17, -.06, .06), .9, 12
    elif os.environ.get('CV2_TGT'): tgt = tuple(float(v) for v in os.environ['CV2_TGT'].split(',')); dist = float(os.environ.get('CV2_DIST', '2.6')); el = 4
    elif with_body: tgt, dist, el = (0, -.02, .9), 4.6, 4
    else: tgt, dist, el = (0, -.04, 1.15), 2.6, 4
    bl.camera(tgt, H[name], el, dist, 70)
    bl.render(os.path.join(outdir, f'{prefix}_{v.replace(":", "_")}.png'))
