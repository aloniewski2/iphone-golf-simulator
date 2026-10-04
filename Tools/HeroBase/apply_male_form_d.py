"""Continue the saved Body_M by assigning coordinates; preserve locked setup."""
import bpy,numpy as np,json,hashlib,math,shutil
from pathlib import Path
from mathutils import Vector
R=Path(__file__).resolve().parents[2];D=R/'ArtDir/hero/base_lock';W=R/'work/male-form-d';P=D/'proof';S=P/'male_silhouette';F=P/'male_form_d'
blend=D/'blender/HeroBase_Male_Silhouette.blend';bpy.ops.wm.open_mainfile(filepath=str(blend))
obj=bpy.data.objects['Body_M'];me=obj.data;scene=bpy.context.scene;inv=json.loads((W/'baseline/invariants.json').read_text())
top=lambda:hashlib.sha256(json.dumps([list(f.vertices) for f in me.polygons],separators=(',',':')).encode()).hexdigest()
matrix=lambda o:list(sum((list(row) for row in o.matrix_world),[]))
assert top()==inv['connectivity_sha256'] and matrix(obj)==inv['body_matrix']
assert {o.name:matrix(o) for o in bpy.data.objects if o.type!='MESH'}==inv['nonbody_matrix']
co=np.load(W/'revised_coordinates.npy');assert co.shape==(len(me.vertices),3)
for v,p in zip(me.vertices,co):v.co=tuple(p)
me.update();me.calc_loop_triangles();active=scene.camera;filepath=scene.render.filepath
for ref in json.loads((R/'work/male-silhouette/reference-cameras.json').read_text())['views']:
    scene.camera=bpy.data.objects['Locked_'+ref['name']];scene.render.filepath=str(S/(ref['name']+'.png'));bpy.ops.render.render(write_still=True)
scene.camera=active;scene.render.filepath=filepath
assert top()==inv['connectivity_sha256'];bpy.ops.wm.save_as_mainfile(filepath=str(blend))
sha=lambda f:hashlib.sha256(f.read_bytes()).hexdigest()
audit={'source_sha256':sha(blend),'continuation_baseline_sha256':inv['source_sha256'],'head_stage_baseline_sha256':json.loads((R/'work/male-jaw-5/baseline/invariants.json').read_text())['source_sha256'],'connectivity_sha256':top(),
 'height_metres':max(v.co.z for v in me.vertices)-min(v.co.z for v in me.vertices),'vertices':len(me.vertices),'triangles':len(me.loop_triangles),
 'body_origin_unchanged':True,'cameras_unchanged':True,'remesh':False,'female':False,'hair':False,'edits':json.loads((W/'coordinate-edit.json').read_text())}
(P/'male-form-d-source-audit.json').write_text(json.dumps(audit,indent=2))
for name,locked in [('front','bald_front'),('back','bald_back')]:shutil.copy2(S/(locked+'.png'),F/(name+'.png'))
cam=bpy.data.objects.new('Transient_FormD_Proof',active.data.copy());scene.collection.objects.link(cam);cam.data.background_images.clear()
def shot(name,angle,h,scale,width=1280,height=720):
    a=math.radians(angle);target=Vector((0,0,h));cam.location=target+Vector((math.sin(a)*5,-math.cos(a)*5,0))
    cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=scale
    scene.camera=cam;scene.render.resolution_x=width;scene.render.resolution_y=height;scene.render.filepath=str(F/(name+'.png'));bpy.ops.render.render(write_still=True)
for name,angle in [('left',90),('right',-90),('threequarter',45)]:shot(name,angle,.84,1280*1.7/593)
shot('head_side',90,1.49,.52,900,900);shot('head_front',0,1.49,.52,900,900);shot('foot_side',90,.12,.52,900,900)
H=F/'head_all_around';H.mkdir(exist_ok=True)
for name,angle,elev in [('front',0,0),('front_left',45,0),('left',90,0),('back_left',135,0),('back',180,0),('back_right',225,0),('right',270,0),('front_right',315,0),('top',0,80)]:
 a=math.radians(angle);e=math.radians(elev);target=Vector((0,-.045,1.505));cam.location=target+Vector((math.sin(a)*4*math.cos(e),-math.cos(a)*4*math.cos(e),4*math.sin(e)))
 cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=.45;scene.render.resolution_x=800;scene.render.resolution_y=800;scene.render.filepath=str(H/(name+'.png'));bpy.ops.render.render(write_still=True)
print('FORM_D_SAVED',json.dumps({k:audit[k] for k in ['source_sha256','height_metres','vertices','triangles']}))
