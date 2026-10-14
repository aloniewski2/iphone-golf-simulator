"""Shoe alone, 6 views (side, 3/4, top, bottom, back, front) -> contact sheet PNG.  Blender -b <shoe.blend> --python shoe_sheet.py -- <out.png> [maps_dir tag]
Role attribute (face) picks the material: 0 upper (white), 1 sole (navy-grey), 2 hardware.  If maps are given the M/N maps are used on the upper like in game."""
import sys, os, math
sys.path.insert(0, os.path.dirname(__file__))
import numpy as np, bpy
from mathutils import Vector
import bl
a = sys.argv[sys.argv.index('--') + 1:]
out = a[0]; TRIM = os.environ.get('SHOE_TRIM', '22305A'); SOLE = os.environ.get('SHOE_SOLE', 'E8E8E8')
sc = bpy.context.scene
for o in list(sc.objects):
    if o.type in ('LIGHT', 'CAMERA'): bpy.data.objects.remove(o)
ob = [o for o in sc.objects if o.type == 'MESH' and o.name.startswith('Kit_Shoe')][0]
me = ob.data
me.materials.clear()
mats = [bl.material('upper', bl.srgb('F2F2F0'), .55), bl.material('sole', bl.srgb(SOLE), .6), bl.material('hw', bl.srgb('9AA0A8'), .35)]
for m in mats: me.materials.append(m)
role = me.attributes['role'].data; ri = np.zeros(len(me.polygons), np.int32); role.foreach_get('value', ri)
for p, r in zip(me.polygons, ri): p.material_index = int(min(r, 2))
if os.environ.get('SHOE_DEBUG'):
    pa = np.zeros(len(me.polygons), np.int32); me.attributes['panel'].data.foreach_get('value', pa)
    cols = ['F2F2F0', 'FF3030', '30A0FF', '30FF30', 'FFD000', 'FF30FF', '00FFFF', 'FF8800']
    me.materials.clear()
    for c in cols: me.materials.append(bl.material('d' + c, bl.srgb(c), .6))
    for p, q in zip(me.polygons, pa): p.material_index = int(q) % 8
MAPS = os.environ.get('SHOE_MAPS')            # dir with <tag>_M.png / <tag>_N.png (Unity out dir) + SHOE_TAG (ShoeL / ShoeR)
if MAPS:
    tag = os.environ['SHOE_TAG']
    imN = bpy.data.images.load(os.path.join(MAPS, tag + '_N.png')); imN.colorspace_settings.name = 'Non-Color'
    imM = bpy.data.images.load(os.path.join(MAPS, tag + '_M.png')); imM.colorspace_settings.name = 'Non-Color'
    def mapmat(name, base, trim, rough):
        m = bpy.data.materials.new(name); m.use_nodes = True; nt = m.node_tree; bsdf = nt.nodes['Principled BSDF']; bsdf.inputs['Roughness'].default_value = rough
        tN = nt.nodes.new('ShaderNodeTexImage'); tN.image = imN; tM = nt.nodes.new('ShaderNodeTexImage'); tM.image = imM
        nm = nt.nodes.new('ShaderNodeNormalMap'); nm.inputs['Strength'].default_value = 1.0; nt.links.new(tN.outputs['Color'], nm.inputs['Color']); nt.links.new(nm.outputs['Normal'], bsdf.inputs['Normal'])
        sep = nt.nodes.new('ShaderNodeSeparateColor'); nt.links.new(tM.outputs['Color'], sep.inputs['Color'])
        mix = nt.nodes.new('ShaderNodeMix'); mix.data_type = 'RGBA'; mix.inputs[6].default_value = (*bl.srgb(base), 1); mix.inputs[7].default_value = (*bl.srgb(trim), 1)
        nt.links.new(sep.outputs['Blue'], mix.inputs['Factor'])
        cav = nt.nodes.new('ShaderNodeMath'); cav.operation = 'MULTIPLY_ADD'; cav.inputs[1].default_value = .6; cav.inputs[2].default_value = .4; nt.links.new(sep.outputs['Green'], cav.inputs[0])
        ao = nt.nodes.new('ShaderNodeMath'); ao.operation = 'MULTIPLY_ADD'; ao.inputs[1].default_value = .55; ao.inputs[2].default_value = .45; nt.links.new(sep.outputs['Red'], ao.inputs[0])
        m1 = nt.nodes.new('ShaderNodeMix'); m1.data_type = 'RGBA'; m1.blend_type = 'MULTIPLY'; m1.inputs['Factor'].default_value = 1; nt.links.new(mix.outputs[2], m1.inputs[6]); nt.links.new(cav.outputs[0], m1.inputs[7])
        m2 = nt.nodes.new('ShaderNodeMix'); m2.data_type = 'RGBA'; m2.blend_type = 'MULTIPLY'; m2.inputs['Factor'].default_value = 1; nt.links.new(m1.outputs[2], m2.inputs[6]); nt.links.new(ao.outputs[0], m2.inputs[7])
        nt.links.new(m2.outputs[2], bsdf.inputs['Base Color'])
        return m
    me.materials.clear()
    me.materials.append(mapmat('up', os.environ.get('SHOE_UPPER', 'F4F4F2'), TRIM, .55))
    me.materials.append(mapmat('so', SOLE, SOLE, .6))
    me.materials.append(mapmat('hw', '9AA0A8', '9AA0A8', .35))
    for p_, r in zip(me.polygons, ri): p_.material_index = int(min(r, 2))
if os.environ.get('SHOE_FAST'):                  # no baked maps: trim stripes / chevron / outsole from the vertex fields, so a preview costs seconds
    def fastmat(name, base, trim, rough, use_trim):
        m = bpy.data.materials.new(name); m.use_nodes = True; nt = m.node_tree; bsdf = nt.nodes['Principled BSDF']; bsdf.inputs['Roughness'].default_value = rough
        def attr(n_):
            a_ = nt.nodes.new('ShaderNodeAttribute'); a_.attribute_name = n_; return a_.outputs['Fac']
        def lt(a_, th, w=.0006):
            n1 = nt.nodes.new('ShaderNodeMath'); n1.operation = 'LESS_THAN'; nt.links.new(a_, n1.inputs[0]); n1.inputs[1].default_value = th; return n1.outputs[0]
        def mul(a_, b_):
            n1 = nt.nodes.new('ShaderNodeMath'); n1.operation = 'MULTIPLY'; nt.links.new(a_, n1.inputs[0]); nt.links.new(b_, n1.inputs[1]); return n1.outputs[0]
        def mx(a_, b_):
            n1 = nt.nodes.new('ShaderNodeMath'); n1.operation = 'MAXIMUM'; nt.links.new(a_, n1.inputs[0]); nt.links.new(b_, n1.inputs[1]); return n1.outputs[0]
        fac = mx(mul(lt(attr('f_stripe'), .0070), attr('tr_stripe')), mul(lt(attr('f_chev'), .0034), attr('tr_chev'))) if use_trim else None
        mix = nt.nodes.new('ShaderNodeMix'); mix.data_type = 'RGBA'; mix.inputs[6].default_value = (*bl.srgb(base), 1); mix.inputs[7].default_value = (*bl.srgb(trim), 1)
        if fac is not None: nt.links.new(fac, mix.inputs['Factor'])
        nt.links.new(mix.outputs[2], bsdf.inputs['Base Color']); return m
    me.materials.clear()
    me.materials.append(fastmat('up', os.environ.get('SHOE_UPPER', 'F4F4F2'), TRIM, .55, True)); me.materials.append(fastmat('so', SOLE, SOLE, .6, False)); me.materials.append(fastmat('hw', '9AA0A8', '9AA0A8', .35, False))
    for p_, r in zip(me.polygons, ri): p_.material_index = int(min(r, 2))
bl.studio(res=(640, 480))
sc.render.resolution_x, sc.render.resolution_y = 640, 440
bb = np.array([ob.matrix_world @ Vector(c) for c in ob.bound_box]); ctr = Vector(bb.mean(0)); ctr.z = .05
side = 1 if ctr.x > 0 else -1
if os.environ.get('SHOE_CTR'): ctr = Vector(tuple(float(v) for v in os.environ['SHOE_CTR'].split(',')))
fwd = Vector((0, -1, 0))   # hero faces -Y
views = {'side': (Vector((side, 0, .12)), 'lateral'), '3/4': (Vector((side * .7, -.7, .45)), ''), 'top': (Vector((0, -.05, 1)), ''), 'bottom': (Vector((0, -.05, -1)), ''),
         'back': (Vector((0, 1, .25)), ''), 'front': (Vector((side * .25, -1, .3)), '')}
if os.environ.get('SHOE_VIEWS'): views = {k: v for k, v in views.items() if k in os.environ['SHOE_VIEWS'].split(',')}
cam = None; files = []
for name, (d, _) in views.items():
    d = d.normalized(); dist = float(os.environ.get('SHOE_DIST', '1.05'))
    if cam is None:
        cd = bpy.data.cameras.new('Cam'); cam = bpy.data.objects.new('Cam', cd); sc.collection.objects.link(cam); sc.camera = cam
    cam.data.lens = 85
    cam.location = ctr + d * dist
    up_hint = 'Y' if name in ('top', 'bottom') else 'Z'
    cam.rotation_euler = (ctr - cam.location).to_track_quat('-Z', 'Y' if name in ('top', 'bottom') else 'Y').to_euler()
    if name == 'top': cam.rotation_euler = (math.radians(0), 0, 0); cam.location = ctr + Vector((0, -.02, dist))
    if name == 'bottom': cam.rotation_euler = (math.radians(180), 0, 0); cam.location = ctr + Vector((0, -.02, -dist))
    fp = os.path.splitext(out)[0] + '_' + name.replace('/', '') + '.png'; bl.render(fp); files.append(fp)
print('SHEET', ' '.join(files))
