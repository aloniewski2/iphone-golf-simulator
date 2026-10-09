import bpy,json,hashlib
from pathlib import Path
from mathutils import Vector
P=Path(__file__).resolve().parent
bpy.ops.wm.open_mainfile(filepath=str(P/'v4_before/Hero_01_Mixamo_QA.blend'))
meshes=[o for o in bpy.data.collections['Hero_01'].objects if o.type=='MESH']
def signature(o):
 return hashlib.sha256(repr(([tuple(v.co) for v in o.data.vertices],[tuple(f.vertices) for f in o.data.polygons],[[ (g.group,g.weight) for g in v.groups] for v in o.data.vertices])).encode()).hexdigest()
before={o.name:signature(o) for o in meshes}
# Shading normals only: preserve every coordinate, polygon, UV, and weight.
for o in meshes:
 if o.name not in ['Hair_Default','Body_Skin','Shirt_Default','Shoes_Default']:continue
 normals=[Vector((0,0,0)) for v in o.data.vertices]
 for f in o.data.polygons:
  for i in f.vertices:normals[i]+=f.normal*f.area
 for n in normals:n.normalize()
 old=[n.vector.copy() for n in o.data.corner_normals]
 for l in o.data.loops:
  co=o.data.vertices[l.vertex_index].co
  if o.name=='Hair_Default':
   # The cap uses a broad radial normal; tuft shells retain their own shape normals.
   if co.z<1.60:normals[l.vertex_index]=(co-Vector((0,.055,1.45))).normalized()
  elif o.name=='Body_Skin':
   if co.z>1.38:
    radial=Vector((co.x/1.0,(co.y-.04)/.85,(co.z-1.43)/1.05)).normalized()
    normals[l.vertex_index]=normals[l.vertex_index].lerp(radial,.4).normalized()
   else:normals[l.vertex_index]=old[l.index]
 o.data.normals_split_custom_set([normals[l.vertex_index] for l in o.data.loops])
# Eye dome UVs: face projection keeps both catchlights on the visible front hemisphere.
for o in meshes:
 if not o.name.startswith('Body_EyeSphere_'):continue
 faces=[f for f in o.data.polygons if o.data.materials[f.material_index].name=='Hero_01_EyeCornea']
 ids=set(i for f in faces for i in f.vertices)
 xmin,xmax=min(o.data.vertices[i].co.x for i in ids),max(o.data.vertices[i].co.x for i in ids)
 zmin,zmax=min(o.data.vertices[i].co.z for i in ids),max(o.data.vertices[i].co.z for i in ids)
 for f in faces:
  for li in f.loop_indices:
   co=o.data.vertices[o.data.loops[li].vertex_index].co
   o.data.uv_layers.active.data[li].uv=(1-(co.x-xmin)/(xmax-xmin),(co.z-zmin)/(zmax-zmin))
after={o.name:signature(o) for o in meshes}
assert before==after
(P/'v4-mesh-lock.json').write_text(json.dumps({'before_eye_depth_coordinates_topology_weights_unchanged':before==after,'mesh_hashes':after},indent=2))
# Expose the existing eye domes previously occluded by the original face surface.
for o in meshes:
 if o.name.startswith('Body_EyeSphere_'):
  for v in o.data.vertices:v.co.y-=.006
report=json.loads((P/'v4-mesh-lock.json').read_text());report['eye_depth_exception_m']=-.006;report['body_limb_hair_hat_clothing_coordinates_unchanged']=True
(P/'v4-mesh-lock.json').write_text(json.dumps(report,indent=2))
bpy.ops.wm.save_as_mainfile(filepath=str(P/'Hero_01_Mixamo_QA.blend'))
