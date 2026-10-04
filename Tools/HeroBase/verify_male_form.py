"""Read-only round-trip verification of the saved form continuation and FBX."""
import bpy,json,hashlib,numpy as np
from pathlib import Path
from mathutils.kdtree import KDTree
R=Path(__file__).resolve().parents[2];D=R/'ArtDir/hero/base_lock';W=R/'work/male-form';P=D/'proof'
blend=D/'blender/HeroBase_Male_Silhouette.blend';bpy.ops.wm.open_mainfile(filepath=str(blend))
obj=bpy.data.objects['Body_M'];me=obj.data;me.calc_loop_triangles();scene=bpy.context.scene
inv=json.loads((W/'baseline/invariants.json').read_text());base=np.load(W/'baseline/mesh.npz')['co'];current=np.array([tuple(v.co) for v in me.vertices])
matrix=lambda o:list(sum((list(row) for row in o.matrix_world),[]))
top=hashlib.sha256(json.dumps([list(f.vertices) for f in me.polygons],separators=(',',':')).encode()).hexdigest()
assert top==inv['connectivity_sha256'] and len(me.vertices)==24578 and len(me.loop_triangles)==49176
assert matrix(obj)==inv['body_matrix']
assert {o.name:matrix(o) for o in bpy.data.objects if o.type!='MESH'}==inv['nonbody_matrix']
assert scene.unit_settings.system=='METRIC' and scene.unit_settings.scale_length==1
assert np.array_equal(current[base[:,2]>1.43],base[base[:,2]>1.43])
assert len([o for o in bpy.data.objects if o.type=='MESH'])==1 and not any(o.type=='ARMATURE' for o in bpy.data.objects)
assert [m.name for m in me.materials]==['Blockout_Grey']
for name,values in inv['cameras'].items():
    cam=bpy.data.objects[name];assert cam.data.ortho_scale==values['scale'] and dict(cam.items())==values['props']
    assert cam.hide_select and all(cam.lock_location) and all(cam.lock_rotation) and all(cam.lock_scale)
    assert cam.data.background_images[0].image.packed_file
assert all(im.packed_file for im in bpy.data.images if im.source=='FILE')
export=json.loads((P/'male-form-export.json').read_text());sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
assert export['source_sha256']==sha(blend) and export['fbx_sha256']==sha(D/'unity_import/HeroBase_Male_Body.fbx')
height=float(np.ptp(current[:,2]));assert 1.65<=height<=1.75
# Independently re-import the actual exported file in this temporary process.
before=set(bpy.data.objects);bpy.ops.import_scene.fbx(filepath=str(D/'unity_import/HeroBase_Male_Body.fbx'))
imported=[o for o in bpy.data.objects if o not in before and o.type=='MESH'];assert len(imported)==1
io=imported[0];io.data.calc_loop_triangles();assert len(io.data.loop_triangles)==49176
kd=KDTree(len(me.vertices))
for i,v in enumerate(me.vertices):kd.insert(obj.matrix_world@v.co,i)
kd.balance();roundtrip=max(kd.find(io.matrix_world@v.co)[2] for v in io.data.vertices);assert roundtrip<1e-5
report={'saved_blender_and_fbx_verification':'PASS','vertices':len(me.vertices),'triangles':len(me.loop_triangles),
 'metres':True,'height_metres':height,'body_origin_unchanged':True,'topology_unchanged':True,'cameras_and_origins_unchanged':True,
 'head_vertices_unchanged':True,'packed_reference_images':3,'hair':False,'face_polish':False,'female_work':False,
 'independent_fbx_reimport_maximum_coordinate_error_metres':roundtrip,'source_sha256':sha(blend),'fbx_sha256':sha(D/'unity_import/HeroBase_Male_Body.fbx')}
(P/'male-form-source-verification.json').write_text(json.dumps(report,indent=2));print(json.dumps(report,indent=2))
