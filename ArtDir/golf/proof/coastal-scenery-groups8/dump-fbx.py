import bpy,json,hashlib,sys,argparse
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--repo',required=True);p.add_argument('--out',required=True);args=p.parse_args(sys.argv[sys.argv.index('--')+1:]);R=Path(args.repo);O=Path(args.out);O.mkdir(parents=True,exist_ok=True)
paths={'original':R/'Unity/Assets/Resources/Course/hole_12.fbx','candidate':R/'Unity/Assets/Resources/Course/Resort/ReferenceHole12.fbx'}
for label,path in paths.items():
 bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=str(path),use_anim=False)
 meshes=[];markers=[]
 for o in bpy.context.scene.objects:
  if o.name.startswith(('PLANT_','MARKER_')):markers.append({'name':o.name,'position':list(o.matrix_world.translation),'scale':list(o.matrix_world.to_scale())})
  if o.type!='MESH' or not o.name.upper().startswith(('TREE','SHRUB','BUSH','ROCK','LIGHTHOUSE','TOWER','CLUBHOUSE')):continue
  m=o.data;meshes.append({'name':o.name,'localVertices':[list(v.co) for v in m.vertices],'worldVertices':[list(o.matrix_world@v.co) for v in m.vertices],'faces':[list(p.vertices) for p in m.polygons],'matrixWorld':[list(row) for row in o.matrix_world]})
 (O/(label+'.json')).write_text(json.dumps({'path':str(path),'sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'meshes':meshes,'markers':markers},separators=(',',':')))
 print('INDEPENDENT_FBX_DUMP',label,'meshes',len(meshes),'markers',len(markers),flush=True)
