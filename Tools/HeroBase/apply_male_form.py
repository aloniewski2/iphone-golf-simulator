"""Apply the form revision to the saved mesh, retaining cameras and connectivity."""
import bpy,numpy as np,json,hashlib,math
from pathlib import Path
from mathutils import Vector
R=Path(__file__).resolve().parents[2];D=R/'ArtDir/hero/base_lock';W=R/'work/male-form';P=D/'proof';B=P/'male_silhouette'
blend=D/'blender/HeroBase_Male_Silhouette.blend';bpy.ops.wm.open_mainfile(filepath=str(blend))
obj=bpy.data.objects['Body_M'];me=obj.data;scene=bpy.context.scene;inv=json.loads((W/'baseline/invariants.json').read_text())
top=lambda:hashlib.sha256(json.dumps([list(f.vertices) for f in me.polygons],separators=(',',':')).encode()).hexdigest()
matrix=lambda o:list(sum((list(row) for row in o.matrix_world),[]))
assert top()==inv['connectivity_sha256'] and matrix(obj)==inv['body_matrix']
assert {o.name:matrix(o) for o in bpy.data.objects if o.type!='MESH'}==inv['nonbody_matrix']
new=np.load(W/'revised_coordinates.npy');assert new.shape==(len(me.vertices),3)
base=np.load(W/'baseline/mesh.npz')['co']
for v,co,original in zip(me.vertices,new,base):v.co=tuple(original if original[2]>1.43 else co)
me.update();me.calc_loop_triangles()
active=scene.camera;filepath=scene.render.filepath
for ref in json.loads((R/'work/male-silhouette/reference-cameras.json').read_text())['views']:
    scene.camera=bpy.data.objects['Locked_'+ref['name']]
    scene.render.filepath=str(B/(ref['name']+'.png'));bpy.ops.render.render(write_still=True)
scene.camera=active;scene.render.filepath=filepath
assert top()==inv['connectivity_sha256']
bpy.ops.wm.save_as_mainfile(filepath=str(blend))
audit=json.loads((B/'source-audit.json').read_text())
audit.update({'stage':'Male shoulder and waist form continuation','height_metres':max(v.co.z for v in me.vertices)-min(v.co.z for v in me.vertices),
 'blend_sha256':hashlib.sha256(blend.read_bytes()).hexdigest(),'form_continuation_source_sha256':inv['source_sha256'],
 'form_edits':json.loads((W/'coordinate-edit.json').read_text()),'depth_coordinates_unchanged':False})
(B/'source-audit.json').write_text(json.dumps(audit,indent=2))
# Three-quarter is a transient proof camera; never save it into the locked source.
form=P/'male_form';form.mkdir(exist_ok=True)
for name,source in [('front','bald_front'),('back','bald_back')]:
    cam=bpy.data.objects['Locked_'+source];scene.camera=cam
    # Native authority-framed renders, with only empty margins cropped in proof.
    scene.render.filepath=str(form/(name+'.png'));bpy.ops.render.render(write_still=True)
data=active.data.copy();cam=bpy.data.objects.new('Transient_Form_3q',data);scene.collection.objects.link(cam)
h=(654-360)*1.7/593;angle=math.pi/4
cam.location=(math.sin(angle)*5,-math.cos(angle)*5,h);cam.rotation_euler=(Vector((0,0,h))-cam.location).to_track_quat('-Z','Y').to_euler()
scene.camera=cam;scene.render.filepath=str(form/'threequarter.png');bpy.ops.render.render(write_still=True)
print('MALE_FORM_COORDINATE_REVISION_SAVED',audit['height_metres'])
