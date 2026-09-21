import bpy, os, io, contextlib
out = FBX
os.makedirs(os.path.dirname(out), exist_ok=True)
sc = bpy.context.scene
for o in bpy.data.objects: o.select_set(False)
rig = bpy.data.objects["GOLFER_RIG"]; body = bpy.data.objects["GOLFER_BODY"]
rig.select_set(True); body.select_set(True)
bpy.context.view_layer.objects.active = rig
sc.frame_set(0)
buf = io.StringIO()
with contextlib.redirect_stdout(buf), bpy.context.temp_override(selected_objects=[rig, body], active_object=rig, object=rig):
    bpy.ops.export_scene.fbx(filepath=out, use_selection=True, object_types={'ARMATURE', 'MESH'},
        apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL', global_scale=1.0,
        axis_forward='-Z', axis_up='Y', bake_space_transform=False,
        use_mesh_modifiers=True, mesh_smooth_type='OFF', add_leaf_bones=False,
        use_armature_deform_only=False, armature_nodetype='NULL',
        bake_anim=True, bake_anim_use_all_bones=True, bake_anim_use_nla_strips=False,
        bake_anim_use_all_actions=False, bake_anim_force_startend_keying=True,
        bake_anim_step=1.0, bake_anim_simplify_factor=0.0, path_mode='AUTO', embed_textures=False)
bpy.ops.wm.save_mainfile()
result = {"fbx": out, "size": os.path.getsize(out)}
