"""Snapshot and inspect the existing saved Body_M; no source mutation."""
import bpy,json,hashlib,shutil,numpy as np,math
from pathlib import Path
from mathutils import Vector
R=Path(__file__).resolve().parents[2];D=R/'ArtDir/hero/base_lock';W=R/'work/male-form-d';P=D/'proof/male_form_d'
W.mkdir(exist_ok=True);(W/'baseline').mkdir(exist_ok=True);P.mkdir(exist_ok=True)
source=D/'blender/HeroBase_Male_Silhouette.blend'
baseline=W/'baseline/HeroBase_Male_Silhouette.blend'
assert not baseline.exists(),'Snapshot already exists; do not replace the continuation baseline'
shutil.copy2(source,baseline);bpy.ops.wm.open_mainfile(filepath=str(source))
obj=bpy.data.objects['Body_M'];me=obj.data;me.calc_loop_triangles()
co=np.array([tuple(v.co) for v in me.vertices],float);edges=np.array([tuple(e.vertices) for e in me.edges],int)
faces=np.array([tuple(t.vertices) for t in me.loop_triangles],int)
np.savez(W/'baseline/mesh.npz',co=co,edges=edges,faces=faces);np.save(W/'revised_coordinates.npy',co)
matrix=lambda o:list(sum((list(row) for row in o.matrix_world),[]))
inv={'source_sha256':hashlib.sha256(source.read_bytes()).hexdigest(),'vertices':len(co),'triangles':len(faces),
 'connectivity_sha256':hashlib.sha256(json.dumps([list(f.vertices) for f in me.polygons],separators=(',',':')).encode()).hexdigest(),
 'body_matrix':matrix(obj),'nonbody_matrix':{o.name:matrix(o) for o in bpy.data.objects if o.type!='MESH'},
 'cameras':{o.name:{'scale':o.data.ortho_scale,'props':dict(o.items())} for o in bpy.data.objects if o.type=='CAMERA'}}
(W/'baseline/invariants.json').write_text(json.dumps(inv,indent=2))
scene=bpy.context.scene;old=scene.camera
cam=bpy.data.objects.new('Transient_FormD_Inspection',old.data.copy());scene.collection.objects.link(cam)
def shot(name,angle,height,scale,width=1280,height_px=720):
    a=math.radians(angle);target=Vector((0,0,height));cam.location=target+Vector((math.sin(a)*5,-math.cos(a)*5,0))
    cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=scale;cam.data.background_images.clear()
    scene.camera=cam;scene.render.resolution_x=width;scene.render.resolution_y=height_px;scene.render.filepath=str(P/('baseline_'+name+'.png'));bpy.ops.render.render(write_still=True)
for name,angle in [('front',0),('back',180),('left',90),('right',-90),('threequarter',45)]:shot(name,angle,.84,1280*1.7/593)
shot('head_side',90,1.49,.52,900,900);shot('head_front',0,1.49,.52,900,900);shot('foot_side',90,.12,.52,900,900)
print('FORM_D_BASELINE',json.dumps(inv))
