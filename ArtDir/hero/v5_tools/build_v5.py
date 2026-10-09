"""Hero POLISH V5 build. Source: the live V4 assembly (restore_motion/Hero_01_FingerRig.blend, 42-bone rig).
Writes ArtDir/hero/v5/Hero_01_V5.blend. The V4 source is never modified (archived copy in v5/archive_V4/).
Usage: Blender -b --python build_v5.py -- [steps=hair,legs,shorts,visor,socks,armpit]"""
import bpy, sys, json, shutil, importlib.util
from pathlib import Path
from mathutils import Matrix
T = Path(__file__).resolve().parent; HERO = T.parent; ROOT = HERO.parent.parent
SRC = HERO.parent / 'anims/restore_motion/Hero_01_FingerRig.blend'
V5 = HERO / 'v5'; (V5 / 'archive_V4').mkdir(parents=True, exist_ok=True)
if not (V5 / 'archive_V4/Hero_01_FingerRig_V4.blend').exists(): shutil.copy2(SRC, V5 / 'archive_V4/Hero_01_FingerRig_V4.blend')
argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
steps = (argv[0] if argv else 'hair,legs,shorts,visor,socks,armpit').split(',')
def load(name):
    spec = importlib.util.spec_from_file_location(name, T / (name + '.py')); m = importlib.util.module_from_spec(spec); spec.loader.exec_module(m); return m
bpy.ops.wm.open_mainfile(filepath=str(SRC))
rig = bpy.data.objects['Hero_01_Rig']
if rig.animation_data: rig.animation_data.action = None
for pb in rig.pose.bones: pb.matrix_basis = Matrix.Identity(4)
bpy.context.view_layer.update()
report = {'steps': steps}
def free_copy(o, name):
    """No-hat variant: the sculpt as Tripo completed it (hair rebuilt under its band), before the visor press."""
    old = bpy.data.objects.get(name)
    if old: bpy.data.objects.remove(old)
    f = o.copy(); f.data = o.data.copy(); f.name = name; f.data.name = name
    for c in o.users_collection: c.objects.link(f)
    return f
body = bpy.data.objects['Body_Skin']
if 'head' in steps:
    # one real head (face + skull) before anything is fitted to it
    load('head_rebuild').build(bpy.context, report)
if 'tripo' in steps:
    # V5 cap only (the gap-free underlayer), then the Tripo-sculpted hair grafted on top
    load('build_hair_v5').build(bpy.context, bpy.data.objects['Hair_Default'], body, n_locks=0, report=report, use_v4_shell=False, cap_off=.008)
    glb = next((HERO / 'v5_tripo/complete/tripo-out').glob('*/model.glb'))
    load('tripo_hair').graft(bpy.context, bpy.data.objects['Hair_Default'], body, glb, report)
    free_copy(bpy.data.objects['Hair_Default'], 'Hair_Default_Free')
elif 'hair' in steps:
    load('build_hair_v5').build(bpy.context, bpy.data.objects['Hair_Default'], body, report=report)
if 'tripo' in steps or 'hair' in steps:
    hm = bpy.data.objects['Hair_Default'].data.materials[0]   # review colour only (Unity recolours hair)
    if hm and hm.use_nodes:
        bs = hm.node_tree.nodes.get('Principled BSDF')
        if bs: bs.inputs['Base Color'].default_value = (.69, .45, .15, 1); bs.inputs['Roughness'].default_value = .6
if any(s in steps for s in ('legs', 'shorts', 'visor', 'socks', 'armpit', 'arms')):
    load('build_body_v5').run(bpy.context, steps, report)
if 'styles' in steps:
    # extra Tripo-sculpted haircuts, each its own Hair_<Style> (cap + sculpted hair) with its own band fill
    th = load('tripo_hair'); bb = load('build_body_v5'); bh = load('build_hair_v5')
    base = bpy.data.objects['Hair_Default']; hat = bpy.data.objects['Hat_Visor']
    for style in ['Ponytail', 'Bob', 'Long', 'Curly']:
        d = HERO / 'v5_styles' / style.lower()
        seg = next(d.glob('seg/tripo-out/*/model.glb'), None); comp = next(d.glob('comp/tripo-out/*/model.glb'), None)
        if not seg or not comp: report['style_' + style] = 'missing'; continue
        old = bpy.data.objects.get('Hair_' + style)
        if old: bpy.data.objects.remove(old)
        o = base.copy(); o.data = base.data.copy(); o.name = 'Hair_' + style
        for c in base.users_collection: c.objects.link(o)
        rs = {}
        bh.build(bpy.context, o, body, n_locks=0, report=rs, use_v4_shell=False, cap_off=.008)
        cfg = json.loads((d / 'parts.json').read_text()) if (d / 'parts.json').exists() else {}
        anchor = th.anchor_from(bpy.context, seg, cfg.get('seg_visor'))
        th.graft(bpy.context, o, body, comp, rs, anchor=anchor, auto=not cfg, drape=style in ('Ponytail', 'Long', 'Curly'),
                 hair_names=cfg.get('hair'), visor_name=cfg.get('visor'))
        free_copy(o, 'Hair_' + style + '_Free')
        bb.band_fill(hat, o, rs, name='Hair_BandFill_' + style)
        bb.hair_under_band(hat, o, rs, falloff=.004)
        report['style_' + style] = rs
if 'scalp' in steps:
    load('scalp_styles').build(bpy.context, report)
if 'female' in steps:
    load('female_shape').add(bpy.context, report)
bpy.ops.wm.save_as_mainfile(filepath=str(V5 / 'Hero_01_V5.blend'))
(V5 / 'build_report.json').write_text(json.dumps(report, indent=1))
print('BUILD_V5_DONE', json.dumps(report))
