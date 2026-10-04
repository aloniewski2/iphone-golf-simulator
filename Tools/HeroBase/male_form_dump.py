"""Snapshot the existing blockout's vertex/edge arrays for coordinate-only repairs."""
import bpy,numpy as np,json,hashlib
from pathlib import Path
R=Path(__file__).resolve().parents[2];D=R/'ArtDir/hero/base_lock';W=R/'work/male-form'
bpy.ops.wm.open_mainfile(filepath=str(D/'blender/HeroBase_Male_Silhouette.blend'))
obj=bpy.data.objects['Body_M'];me=obj.data
co=np.array([tuple(v.co) for v in me.vertices],float)
edges=np.array([tuple(e.vertices) for e in me.edges],int)
faces=np.array([tuple(t.vertices) for t in me.loop_triangles],int)
np.savez(W/'baseline/mesh.npz',co=co,edges=edges)
report={'vertices':len(me.vertices),'polygons':len(me.polygons),'connectivity_sha256':hashlib.sha256(json.dumps([list(f.vertices) for f in me.polygons],separators=(',',':')).encode()).hexdigest(),
 'body_matrix':list(sum((list(row) for row in obj.matrix_world),[])),
 'nonbody_matrix':{o.name:list(sum((list(row) for row in o.matrix_world),[])) for o in bpy.data.objects if o.type!='MESH'},
 'cameras':{o.name:{'matrix':list(sum((list(row) for row in o.matrix_world),[])),'scale':o.data.ortho_scale,'props':dict(o.items())} for o in bpy.data.objects if o.type=='CAMERA'},
 'source_sha256':hashlib.sha256((W/'baseline/HeroBase_Male_Silhouette.blend').read_bytes()).hexdigest()}
(W/'baseline/invariants.json').write_text(json.dumps(report,indent=2))
print(json.dumps({'vertices':len(me.vertices),'edges':len(me.edges),'zrange':co[:,2].min().item(),'height':np.ptp(co[:,2]).item()},indent=2))
