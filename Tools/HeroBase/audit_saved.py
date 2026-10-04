import bpy,sys,json,math,hashlib
from pathlib import Path
from mathutils import Vector
R=Path(__file__).resolve().parents[2];D=R/'ArtDir/hero/base_lock';out=[]
sys.path.insert(0,str(Path(__file__).parent))
from plate_profiles import MALE,FEMALE
for sex,suf,pr in [('Male','M',MALE),('Female','F',FEMALE)]:
 p=D/'blender'/('HeroBase_'+sex+'.blend');bpy.ops.wm.open_mainfile(filepath=str(p));parts=[]
 scalp_floor=(pr['sole']-pr['eye_y']+24)*(1.7/pr['height'])
 for o in [bpy.data.objects['Body_'+suf],bpy.data.objects['Hair_'+suf]]:
  me=o.data;me.calc_loop_triangles();edge={tuple(sorted(e.vertices)):0 for e in me.edges}
  for f in me.polygons:
   for e in f.edge_keys:edge[tuple(sorted(e))]+=1
  above=lambda e:min(me.vertices[i].co.z for i in e)>scalp_floor
  row={'name':o.name,'triangles':len(me.loop_triangles),'scale':list(o.scale),'rotation_radians':list(o.rotation_euler),'origin':list(o.location),'nonfinite_vertices':sum(not all(math.isfinite(x) for x in v.co) for v in me.vertices),'boundary_edges':sum(n==1 for n in edge.values()),'nonmanifold_edges_more_than_two_faces':sum(n>2 for n in edge.values()),'alpha_materials':[(m.name,m.diffuse_color[3]) for m in me.materials]}
  if o.name.startswith('Body_'):
   row.update({'scalp_region_floor_metres':scalp_floor,'scalp_boundary_edges':sum(n==1 and above(e) for e,n in edge.items()),'scalp_nonmanifold_edges':sum(n>2 and above(e) for e,n in edge.items()),'hair_materials':sum(m.name.startswith('Hair_') for m in me.materials),'height_metres':max(v.co.z for v in me.vertices)-min(v.co.z for v in me.vertices)})
   assert row['scalp_boundary_edges']==row['scalp_nonmanifold_edges']==row['hair_materials']==0
  else:assert row['boundary_edges']==row['nonmanifold_edges_more_than_two_faces']==0
  parts.append(row)
 attachment=json.loads((D/'unity_import'/('HeroBase_'+sex+'_Attachments.json')).read_text())
 record={'sex':sex,'meshes':[o.name for o in bpy.data.objects if o.type=='MESH'],'armatures':sum(o.type=='ARMATURE' for o in bpy.data.objects),'parts':parts,'source_sha256':hashlib.sha256(p.read_bytes()).hexdigest(),'plate_hash_matches':hashlib.sha256((D/(sex.lower()+'_body_plain.jpg')).read_bytes()).hexdigest()==json.loads((D/'plate-measurements.json').read_text())[sex.lower()]['sha256'],'attachment':attachment,'exports':[]}
 assert set(record['meshes'])=={'Body_'+suf,'Hair_'+suf},'Unexpected source mesh'
 record['source_images']=[{'name':im.name,'packed':bool(im.packed_file)} for im in bpy.data.images if im.source=='FILE']
 assert all(im['packed'] for im in record['source_images']),'Unpacked authoring image'
 # Independently re-import each export. A body file must not contain a hairstyle mesh or material.
 for is_hair,filename,expected in [(False,attachment['body_fbx'],parts[0]),(True,attachment['default_hair_fbx'],parts[1])]:
  fbx=D/'unity_import'/filename;bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=str(fbx))
  meshes=[o for o in bpy.data.objects if o.type=='MESH'];assert len(meshes)==1
  obj=meshes[0];me=obj.data;me.calc_loop_triangles();mats=[m.name for m in me.materials];assert len(me.loop_triangles)==expected['triangles'];assert all(m.startswith('Hair_') for m in mats) if is_hair else not any(m.startswith('Hair_') for m in mats)
  world=[obj.matrix_world@v.co for v in me.vertices];zmin=min(v.z for v in world);zmax=max(v.z for v in world)
  record['exports'].append({'path':filename,'sha256':hashlib.sha256(fbx.read_bytes()).hexdigest(),'mesh_names':[o.name for o in meshes],'triangles':len(me.loop_triangles),'materials':mats,'armatures':sum(o.type=='ARMATURE' for o in bpy.data.objects),'z_min_metres':zmin,'z_max_metres':zmax,'head_local_origin':is_hair})
 out.append(record)
(D/'proof/saved-source-audit.json').write_text(json.dumps(out,indent=2));print('SAVED_SOURCE_AUDIT',json.dumps(out))
