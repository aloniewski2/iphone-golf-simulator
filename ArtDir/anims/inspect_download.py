import bpy,json
from pathlib import Path
P=Path(__file__).resolve().parent
bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=str(P/'source/Mixamo_Body_Bind.fbx'))
out={}
for o in bpy.data.objects:
 if o.type=='ARMATURE':out[o.name]={'matrix':[list(x) for x in o.matrix_world],'bones':{b.name:{'parent':b.parent.name if b.parent else None,'head':list(b.head_local),'tail':list(b.tail_local)} for b in o.data.bones}}
 elif o.type=='MESH':out[o.name]={'verts':len(o.data.vertices),'dims':list(o.dimensions)}
out['actions']=[{'name':a.name,'range':list(a.frame_range)} for a in bpy.data.actions]
(P/'source/bind-inspection.json').write_text(json.dumps(out,indent=2));print(json.dumps(out))
