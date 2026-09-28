import bpy
bpy.ops.wm.open_mainfile(filepath='/Users/adnanyonathan/Documents/Codex/2026-09-20/wh/outputs/iphone-golf-simulator/ArtDir/anims/restore_motion/Hero_01_FingerRig.blend')
r=bpy.data.objects['Hero_01_Rig']
for b in r.data.bones:
 if any(k in b.name for k in ['Index','Middle','Ring','Pinky','Thumb']):continue
 print('B',b.name,tuple(round(x,3) for x in b.head_local),tuple(round(x,3) for x in b.tail_local),round(b.length,3),b.parent.name if b.parent else None, tuple(round(x,2) for x in b.matrix_local.to_3x3().col[0]),tuple(round(x,2) for x in b.matrix_local.to_3x3().col[2]))
