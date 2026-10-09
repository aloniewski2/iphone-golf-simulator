"""Export editable final golf art to candidates; never touches live Unity Assets.
Run: blender --background --python ArtDir/golf/tools/export-golf-art.py
An optional --asset BotanicalKit exports only the named source.
"""
import bpy,sys,json,hashlib
from pathlib import Path
R=Path(__file__).resolve().parents[3]
SOURCE=R/'ArtDir/golf/source';OUT=R/'work/golf-art-exports';OUT.mkdir(parents=True,exist_ok=True)
assets=['BotanicalKit','MeadowClub','Finish_12','Finish_15','Finish_17','Finish_18','Finish_19','Finish_20','GolfGalleryHero4']
if '--asset' in sys.argv:
 name=sys.argv[sys.argv.index('--asset')+1]
 if name not in assets:raise ValueError('Unknown golf art source: '+name)
 assets=[name]
report=[]
for name in assets:
 source=SOURCE/(name+'.blend');bpy.ops.wm.open_mainfile(filepath=str(source))
 if name.startswith('Finish_'):
  prefixes=['TEMPLE','RUIN','STAIR','LIGHTHOUSE','BRIDGE','BENCH','FENCE','HOUSE','SHACK','GATE','WINDMILL']
  if name in ['Finish_15','Finish_17','Finish_20']:prefixes+=['CABIN','FARMHOUSE','JETTY','BOAT']
  selected=[o for o in bpy.data.objects if o.type=='MESH' and any(o.name.startswith(p) for p in prefixes) and 'SAIL' not in o.name]
 else:selected=[o for o in bpy.data.objects if o.type=='MESH' or (name=='GolfGalleryHero4' and o.type=='EMPTY')]
 if not selected:raise ValueError('No exported source objects: '+name)
 for o in bpy.data.objects:o.select_set(o in selected)
 arguments=dict(filepath=str(OUT/(name+'.fbx')),use_selection=True,object_types={'MESH','EMPTY'} if name=='GolfGalleryHero4' else {'MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_anim=False,add_leaf_bones=False,path_mode='STRIP',use_mesh_modifiers=True)
 if name!='GolfGalleryHero4':arguments.update(apply_scale_options='FBX_SCALE_ALL',bake_space_transform=False)
 if name in ['Finish_12','Finish_18','Finish_19']:arguments['mesh_smooth_type']='OFF'
 bpy.ops.export_scene.fbx(**arguments)
 count=0;objects=[];deps=bpy.context.evaluated_depsgraph_get()
 for o in selected:
  if o.type!='MESH':continue
  ev=o.evaluated_get(deps);mesh=ev.to_mesh();mesh.calc_loop_triangles();n=len(mesh.loop_triangles);count+=n;objects.append({'name':o.name,'vertices':len(mesh.vertices),'triangles':n});ev.to_mesh_clear()
 report.append({'asset':name,'source':str(source.relative_to(R)),'source_sha256':hashlib.sha256(source.read_bytes()).hexdigest(),'candidate':str((OUT/(name+'.fbx')).relative_to(R)),'evaluated_triangles':count,'objects':objects})
(OUT/'export-manifest.json').write_text(json.dumps({'status':'Candidate export only; compare actual Unity rendering before installation.','assets':report},indent=2)+'\n')
print('GOLF_ART_EXPORT_CANDIDATES',json.dumps([(x['asset'],x['evaluated_triangles']) for x in report]))
