import bpy,json,hashlib
from pathlib import Path
P=Path('/Users/adnanyonathan/Documents/Codex/2026-09-20/wh/outputs/iphone-golf-simulator')
O=P/'ArtDir/review/unimate-tennis'; source=P/'ArtDir/hero/v5/Hero_01_V5.blend'
bpy.ops.wm.open_mainfile(filepath=str(source));rig=bpy.data.objects['Hero_01_Rig'];rig.animation_data_clear()
for pb in rig.pose.bones:pb.matrix_basis.identity()
keep={'Hero_01_Rig','Body_Skin','Body_EyeSphere_L','Body_EyeSphere_R','Hair_Default','Hat_Visor','Shirt_Default','Shorts_Default','Shoes_Default'}
for ob in list(bpy.data.objects):
 if ob.type=='MESH' and ob.name not in keep:bpy.data.objects.remove(ob,do_unlink=True)
names=[b.name for b in rig.data.bones if b.name!='Root']; bones=[]
for name in names:
 b=rig.data.bones[name];parent=b.parent.name if b.parent and b.parent.name in names else None
 wm=rig.matrix_world@b.matrix_local
 bones.append({'name':name,'parent':parent,'world_rest':[list(row) for row in wm]})
(O/'skeleton-rest.json').write_text(json.dumps({'bones':bones,'source':str(source),'source_sha256':hashlib.sha256(source.read_bytes()).hexdigest()},indent=2))
bpy.ops.wm.save_as_mainfile(filepath=str(O/'HeroV5_UniMate_Preview.blend'))
print('EXPORTED_SKELETON',len(bones))
