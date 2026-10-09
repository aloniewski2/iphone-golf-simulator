import bpy,json
from pathlib import Path
from mathutils import Vector
R=Path(__file__).resolve().parents[4]
W=R/'ArtDir/tennis/audience/staging6';P=R/'proof/full-visual-overhaul/tennis/audience6'
def is_pants(c):return max(abs(c[i]-v) for i,v in enumerate((.055,.10,.16,1)))<1e-5

def snapshot(ob,pants_only=False,exclude_pants=False):
 mesh=ob.data;palette=mesh.color_attributes.active_color;polys=[];colours=[]
 for polygon in mesh.polygons:
  colour=tuple(palette.data[polygon.loop_start].color)
  pants=is_pants(colour)
  if pants_only and not pants:continue
  if exclude_pants and pants:continue
  polys.append(list(polygon.vertices));colours.append([tuple(palette.data[li].color) for li in polygon.loop_indices])
 used=sorted({i for poly in polys for i in poly});remap={v:i for i,v in enumerate(used)}
 return {'points':[tuple(mesh.vertices[i].co) for i in used],'faces':[[remap[i] for i in f] for f in polys],'colours':colours,'weights':[{ob.vertex_groups[g.group].name:g.weight for g in mesh.vertices[i].groups} for i in used]}

def candidate_meshes(asset):
 out={}
 for ob in bpy.data.objects:
  if ob.type!='MESH':continue
  root=ob
  while root.parent:root=root.parent
  if not root.name.startswith('FAN_'):continue
  index=int(root.name[4:])
  target=ob.name.startswith('VISITOR_MESH_FAR') if 'Promenade' in asset else ob.name.startswith('BODY_FAR')
  if index%2==0 and target:out[index]=ob
 return out

report=[]
for asset in ['TennisPromenadeHero3','TennisSeatedHero3']:
 candidate=W/(asset+'.blend');bpy.ops.wm.open_mainfile(filepath=str(candidate))
 pants={i:snapshot(ob,pants_only=True) for i,ob in candidate_meshes(asset).items()}
 bpy.ops.wm.open_mainfile(filepath=str(R/'ArtDir/tennis/audience/inputs'/('Accepted_'+asset.replace('Hero3','Hero5')+'.blend')))
 for index,ob in candidate_meshes(asset).items():
  before=ob.data;before.calc_loop_triangles();old_count=len(before.loop_triangles);base=snapshot(ob,exclude_pants=True);new=pants[index]
  if not new['faces']:raise RuntimeError('No separated male pants candidate '+str(index))
  offset=len(base['points']);points=base['points']+new['points'];faces=base['faces']+[[i+offset for i in f] for f in new['faces']];colours=base['colours']+new['colours'];weights=base['weights']+new['weights']
  mesh=bpy.data.meshes.new(before.name+'_separated_male_shorts');mesh.from_pydata(points,[],faces);mesh.update()
  for material in before.materials:mesh.materials.append(material)
  palette=mesh.color_attributes.new(name='SpectatorPalette',type='FLOAT_COLOR',domain='CORNER')
  for polygon,cs in zip(mesh.polygons,colours):
   polygon.use_smooth=True
   for loop,colour in zip(polygon.loop_indices,cs):palette.data[loop].color=colour
  ob.data=mesh
  for i,groups in enumerate(weights):
   for name,value in groups.items():
    group=ob.vertex_groups.get(name) or ob.vertex_groups.new(name=name);group.add([i],value,'REPLACE')
  mesh.calc_loop_triangles();report.append({'asset':asset,'variant':index,'old_mesh_triangles':old_count,'new_mesh_triangles':len(mesh.loop_triangles),'scope':'Only pants-coloured polygons of the far male mesh are replaced; all original non-pants polygon geometry/palette/weights and all other source mesh datablocks are retained.'})
 # The editable candidate retains original accepted source5 parts and bones.
 bpy.ops.object.select_all(action='SELECT')
 bpy.ops.export_scene.fbx(filepath=str(W/(asset+'.fbx')),use_selection=True,object_types={'MESH','EMPTY','ARMATURE'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_anim=False,add_leaf_bones=False,path_mode='STRIP')
 bpy.ops.wm.save_as_mainfile(filepath=str(candidate))
(P/'far-shorts-only-patch.json').write_text(json.dumps(report,indent=2)+'\n');print('FAR_SHORTS_ONLY_PATCH',json.dumps(report))
