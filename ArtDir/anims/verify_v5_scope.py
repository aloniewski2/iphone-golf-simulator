import bpy,json
from mathutils import Vector
from pathlib import Path
P=Path(__file__).resolve().parent
bpy.ops.wm.open_mainfile(filepath=str(P/'v5_before/Hero_01_Mixamo_QA.blend'))
old={o.name:[tuple(v.co) for v in o.data.vertices] for o in bpy.data.collections['Hero_01'].objects if o.type=='MESH'}
bones=[(b.name,tuple(b.head_local),tuple(b.tail_local)) for b in bpy.data.objects['Hero_01_Rig'].data.bones]
bpy.ops.wm.open_mainfile(filepath=str(P/'Hero_01_Mixamo_QA.blend'))
report={'unchanged_meshes':{},'body_coordinates_changed_outside_leg_region':0,'body_leg_vertices_changed':0,'skeleton_unchanged':bones==[(b.name,tuple(b.head_local),tuple(b.tail_local)) for b in bpy.data.objects['Hero_01_Rig'].data.bones]}
for n in ['Shirt_Default','Shoes_Default','Body_EyeSphere_L','Body_EyeSphere_R']:report['unchanged_meshes'][n]=old[n]==[tuple(v.co) for v in bpy.data.objects[n].data.vertices]
body=bpy.data.objects['Body_Skin']
for i,v in enumerate(body.data.vertices):
 if (v.co-Vector(old['Body_Skin'][i])).length>1e-7:
  if .235<old['Body_Skin'][i][2]<.64:report['body_leg_vertices_changed']+=1
  else:report['body_coordinates_changed_outside_leg_region']+=1
report['passed']=report['skeleton_unchanged'] and all(report['unchanged_meshes'].values()) and report['body_coordinates_changed_outside_leg_region']==0
(P/'v5-scope-verification.json').write_text(json.dumps(report,indent=2));print(report)
assert report['passed']
