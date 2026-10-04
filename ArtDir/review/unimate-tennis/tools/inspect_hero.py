import bpy,json,sys
from pathlib import Path
P=Path('/Users/adnanyonathan/Documents/Codex/2026-09-20/wh/outputs/iphone-golf-simulator')
bpy.ops.wm.open_mainfile(filepath=str(P/'ArtDir/hero/v5/Hero_01_V5.blend'))
d={'version':bpy.app.version_string,'python':sys.version,'objects':[],'actions':list(bpy.data.actions.keys())}
for o in bpy.data.objects:
 r={'name':o.name,'type':o.type,'hidden':o.hide_render,'location':list(o.location),'scale':list(o.scale),'dimensions':list(o.dimensions)}
 if o.type=='ARMATURE':r['bones']=[{'name':b.name,'parent':b.parent.name if b.parent else None,'head':list(b.head_local),'tail':list(b.tail_local),'deform':b.use_deform} for b in o.data.bones]
 if o.type=='MESH':r['materials']=[m.name for m in o.data.materials if m];r['vertices']=len(o.data.vertices);r['modifiers']=[(m.name,m.type) for m in o.modifiers]
 d['objects'].append(r)
(P/'ArtDir/review/unimate-tennis/hero-inventory.json').write_text(json.dumps(d,indent=2))
print('HERO_INVENTORY_DONE',bpy.app.version_string)
