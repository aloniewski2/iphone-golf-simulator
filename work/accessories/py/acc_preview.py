"""ACCESSORIES preview renders (Blender, N frame): a fitted piece (from its _final.npz + maps) alone or on the hero.
Blender -b --factory-startup --python acc_preview.py -- <Male|Female> <item> <outdir> <views> [alone]
views: comma list of name or name:crop (front, 34, side, back, top, under; crops head, hand, back)"""
import sys, os, json, math
sys.path.insert(0, os.path.dirname(__file__))
import numpy as np, bpy
import acc_common as A, acc_geo as G

OUT = os.path.join(A.ROOT, 'out')
COLOURS = {'tennis_cap': ('F3F2EE', '1F2B57'), 'golf_cap': ('1F2B57', 'F3F2EE'), 'golf_glove': ('F1F0EC', '1F2B57'), 'golf_bag': ('F1F0EC', '1F2B57')}


def _object_from_npz(g, name, mapdir, tag, item, use_maps=True, base_hex=None, trim_hex=None):
    pos = g['pos']; tri = g['tri']; sub = g['sub']
    ob = A.mesh_object(name, pos, tri)
    me = ob.data
    uv = me.uv_layers.new(name='UVMap')
    uvs = g['uv'][tri].reshape(-1, 2)
    uv.data.foreach_set('uv', uvs.astype(np.float32).ravel())
    bh, th = COLOURS.get(item, ('E8E6E0', '22305A'))
    bh = base_hex or bh; th = trim_hex or th
    cloth = bpy.data.materials.new(name + '_cloth'); cloth.use_nodes = True; nt = cloth.node_tree
    bsdf = nt.nodes['Principled BSDF']; bsdf.inputs['Roughness'].default_value = .62
    try: bsdf.inputs['Sheen Weight'].default_value = .12
    except KeyError: pass
    nfile = os.path.join(mapdir, f'{tag}_N.png'); mfile = os.path.join(mapdir, f'{tag}_M.png')
    if use_maps and os.path.exists(nfile) and os.path.exists(mfile):
        imN = bpy.data.images.load(nfile); imN.colorspace_settings.name = 'Non-Color'
        imM = bpy.data.images.load(mfile); imM.colorspace_settings.name = 'Non-Color'
        tN = nt.nodes.new('ShaderNodeTexImage'); tN.image = imN
        tM = nt.nodes.new('ShaderNodeTexImage'); tM.image = imM
        nm = nt.nodes.new('ShaderNodeNormalMap'); nm.inputs['Strength'].default_value = 1.0
        nt.links.new(tN.outputs['Color'], nm.inputs['Color']); nt.links.new(nm.outputs['Normal'], bsdf.inputs['Normal'])
        sep = nt.nodes.new('ShaderNodeSeparateColor'); nt.links.new(tM.outputs['Color'], sep.inputs['Color'])
        mix = nt.nodes.new('ShaderNodeMix'); mix.data_type = 'RGBA'
        mix.inputs[6].default_value = (*A.srgb(bh), 1); mix.inputs[7].default_value = (*A.srgb(th), 1)
        nt.links.new(sep.outputs['Blue'], mix.inputs['Factor'])
        cav = nt.nodes.new('ShaderNodeMath'); cav.operation = 'MULTIPLY_ADD'; cav.inputs[1].default_value = .62; cav.inputs[2].default_value = .38
        nt.links.new(sep.outputs['Green'], cav.inputs[0])
        ao = nt.nodes.new('ShaderNodeMath'); ao.operation = 'MULTIPLY'
        nt.links.new(cav.outputs[0], ao.inputs[0]); nt.links.new(sep.outputs['Red'], ao.inputs[1])
        mul = nt.nodes.new('ShaderNodeMix'); mul.data_type = 'RGBA'; mul.blend_type = 'MULTIPLY'; mul.inputs['Factor'].default_value = 1
        nt.links.new(mix.outputs[2], mul.inputs[6]); nt.links.new(ao.outputs[0], mul.inputs[7])
        nt.links.new(mul.outputs[2], bsdf.inputs['Base Color'])
    else:
        bsdf.inputs['Base Color'].default_value = (*A.srgb(bh), 1)
    metal = A.material(name + '_metal', A.srgb('C9CCD1'), .28, 0, metallic=.95)
    me.materials.append(cloth); me.materials.append(metal)
    mi = np.asarray(sub, np.int32).clip(0, 1)
    me.polygons.foreach_set('material_index', mi)
    me.update()
    return ob


def render_views(sex, objs, outdir, prefix, views, with_body=True):
    if sex == 'Product':
        A.studio(res=(900, 900), bg=(.34, .35, .37))
        for v in views:
            name, _, crop = v.partition(':')
            az, el = {'front': (0, 10), '34': (38, 20), 'side': (90, 8), 'back': (180, 10), 'back34': (150, 18), 'top': (0, 88), 'under': (0, -75), 'sider': (-90, 8)}.get(name, (0, 10))
            tgt, dist, lens = ((0, -0.015, 0.048), .74, 75) if crop in ('', None) else ((0, -0.02, 0.04), .5, 85)
            if crop == 'brim': tgt, dist = (0, -0.09, 0.0), .32
            if crop == 'strap': tgt, dist = (0, 0.105, 0.01), .30
            if crop == 'crown': tgt, dist = (0, -0.03, 0.07), .38
            A.camera(tgt, az, el, dist, lens)
            A.render(os.path.join(outdir, f'{prefix}_{name}{("_" + crop) if crop else ""}.png'))
        return
    hd = A.HeroData(sex)
    if with_body: A.load_body(hd)
    A.studio(res=(900, 900), bg=(.33, .34, .36))
    head = hd.rest_pos['Head']; hand = hd.rest_pos['LeftHand']; chest = hd.rest_pos['UpperChest']
    for v in views:
        name, _, crop = v.partition(':')
        az, el = {'front': (0, 8), '34': (38, 14), 'side': (90, 6), 'back': (180, 8), 'back34': (150, 14), 'top': (0, 70), 'under': (0, -60), 'sider': (-90, 6),
                  'half': (62, 10)}.get(name, (0, 8))
        if crop == 'head': tgt, dist, lens = (0, head[1] - 0.01, head[2] + 0.115), .85, 85
        elif crop == 'closehead': tgt, dist, lens = (0, head[1] - 0.01, head[2] + 0.10), .55, 85
        elif crop == 'hand': tgt, dist, lens = tuple(hand + np.array([.04, 0, -.03])), .55, 85
        elif crop == 'back': tgt, dist, lens = (0, chest[1], chest[2] - 0.15), 2.0, 60
        elif crop == 'body': tgt, dist, lens = (0, -0.02, .90), 4.6, 70
        else: tgt, dist, lens = (0, -0.02, 1.2), 3.2, 70
        A.camera(tgt, az, el, dist, lens)
        A.render(os.path.join(outdir, f'{prefix}_{name}{("_" + crop) if crop else ""}.png'))


if __name__ == '__main__':
    a = A.args_after_dashes()
    sex, item, outdir, views = a[0], a[1], a[2], a[3].split(',')
    alone = len(a) > 4 and a[4] == 'alone'
    d = os.path.join(OUT, sex, item)
    meta = json.load(open(os.path.join(d, 'piece.json')))
    A.reset()
    objs = []
    for pc in (meta.get('pieces') or ['Kit_Cap']):
        g = np.load(os.path.join(d, pc + '_final.npz'))
        objs.append(_object_from_npz(g, item + '_' + pc, d, meta.get('tags', {}).get(pc, meta.get('tag', 'Cap')), item))
    render_views(sex, objs, outdir, f'{sex}_{item}', views, with_body=not alone)
