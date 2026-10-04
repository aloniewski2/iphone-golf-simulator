"""Export only the current saved male continuation, with scale and normals intact."""
import bpy,json,hashlib
from pathlib import Path
R=Path(__file__).resolve().parents[2];D=R/'ArtDir/hero/base_lock';W=R/'work/male-form';P=D/'proof'
blend=D/'blender/HeroBase_Male_Silhouette.blend';bpy.ops.wm.open_mainfile(filepath=str(blend))
obj=bpy.data.objects['Body_M'];bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
me=obj.data;me.calc_loop_triangles();assert len(me.loop_triangles)==49176 and bpy.context.scene.unit_settings.scale_length==1
dst=D/'unity_import/HeroBase_Male_Body.fbx'
bpy.ops.export_scene.fbx(filepath=str(dst),use_selection=True,object_types={'MESH'},global_scale=1,apply_unit_scale=True,
 apply_scale_options='FBX_SCALE_ALL',axis_forward='-Z',axis_up='Y',use_mesh_modifiers=False,mesh_smooth_type='OFF',add_leaf_bones=False,bake_anim=False)
report={'source':str(blend),'source_sha256':hashlib.sha256(blend.read_bytes()).hexdigest(),'fbx':str(dst),'fbx_sha256':hashlib.sha256(dst.read_bytes()).hexdigest(),
 'mesh':'Body_M','vertices':len(me.vertices),'triangles':len(me.loop_triangles),'global_scale':1,'forward':'-Z','up':'Y','hair':False,'armature':False,'normal_export':'Saved smooth mesh normals; no modifier or topology conversion'}
(P/'male-form-export.json').write_text(json.dumps(report,indent=2));print(json.dumps(report,indent=2))
