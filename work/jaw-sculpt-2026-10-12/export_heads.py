"""Export the sculpted heads (<Sex>_JawSculpt.blend next to this script) exactly like work/reference-rebuild/face-finish30/composed-ears/compose.py (Body + ReferenceFeatures +
rig, rest pose, shape keys at 0, same FBX options) into export/<Sex>.fbx.
Blender -b --factory-startup --python export_heads.py
"""
import bpy, pathlib, hashlib, json
W = pathlib.Path(__file__).resolve().parent
out = {}
for sex in ('Male', 'Female'):
    bpy.ops.wm.open_mainfile(filepath=str(W / f'{sex}_JawSculpt.blend'))
    body = bpy.data.objects['Body']
    rig = next(o for o in bpy.data.objects if o.type == 'ARMATURE' and o.name.startswith('Rig_'))
    rig.data.pose_position = 'REST'; rig.animation_data_clear()
    for c in [c for c in bpy.data.collections if c.name.startswith('KitProbe')]: bpy.data.collections.remove(c)
    blend = W / f'{sex}_JawSculpt.blend'
    bpy.ops.wm.save_as_mainfile(filepath=str(blend))
    for ob in (body, bpy.data.objects['ReferenceFeatures'], rig):        # the live session hid them behind the game preview
        ob.hide_set(False); ob.hide_viewport = False; ob.hide_select = False
    for ob in bpy.context.scene.objects:
        if ob.name.startswith(('GamePreview', 'NormTest')): ob.hide_set(True)
    bpy.ops.wm.save_as_mainfile(filepath=str(blend))
    for ob in bpy.context.scene.objects:
        ob.select_set(ob.name in ('Body', 'ReferenceFeatures') or ob is rig)
        if ob.type == 'MESH' and ob.data.shape_keys:
            for key in ob.data.shape_keys.key_blocks: key.value = 0
    bpy.context.view_layer.objects.active = body
    assert sorted(o.name for o in bpy.context.selected_objects) == sorted(['Body', 'ReferenceFeatures', rig.name])
    fbx = W / 'export' / f'{sex}.fbx'; fbx.parent.mkdir(exist_ok=True)
    bpy.ops.export_scene.fbx(filepath=str(fbx), use_selection=True, object_types={'ARMATURE', 'MESH'}, global_scale=1.0, apply_unit_scale=True,
                             apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y', use_mesh_modifiers=False, mesh_smooth_type='OFF',
                             add_leaf_bones=False, bake_anim=False, path_mode='STRIP', use_armature_deform_only=False)
    out[sex] = {'blend': str(blend), 'blendSha256': hashlib.sha256(blend.read_bytes()).hexdigest(), 'fbx': str(fbx),
                'fbxSha256': hashlib.sha256(fbx.read_bytes()).hexdigest(), 'bodyVertices': len(body.data.vertices),
                'selected': sorted(o.name for o in bpy.context.selected_objects)}
    print(out[sex], flush=True)
(W / 'export' / 'EXPORT.json').write_text(json.dumps(out, indent=2) + '\n')
