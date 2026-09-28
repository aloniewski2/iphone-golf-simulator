import bpy,json,shutil
from pathlib import Path
from mathutils import Matrix
P=Path(__file__).resolve().parent;W=P/'wardrobe';W.mkdir(exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(P/'Hero_01_Mixamo_QA.blend'))
rig=bpy.data.objects['Hero_01_Rig'];C=bpy.data.collections['Hero_01'];meshes=[o for o in C.objects if o.type=='MESH'];sockets=[bpy.data.objects[n] for n in ['Hat','Hand_R','Hand_L','Back','FaceExtra']]
body=bpy.data.objects['Body_Skin'];coverage=body.modifiers['Default outfit coverage'];scene=bpy.context.scene
# FBX supports the original base atlas, not the Blender tint node-group graph.
restore=[]
for m in body.data.materials:
 bs=m.node_tree.nodes.get('Principled BSDF')
 if not bs:continue
 if bs.inputs['Base Color'].links:restore.append((m,bs,bs.inputs['Base Color'].links[0].from_socket))
 im=next((n for n in m.node_tree.nodes if n.type=='TEX_IMAGE' and n.image and 'BaseColor' in n.image.name),None)
 for l in list(bs.inputs['Base Color'].links):m.node_tree.links.remove(l)
 if im:m.node_tree.links.new(im.outputs['Color'],bs.inputs['Base Color'])
 else:bs.inputs['Base Color'].default_value=(.69,.43,.22,1)
def select(objs):
 bpy.ops.object.select_all(action='DESELECT')
 for o in objs:o.select_set(True)
 bpy.context.view_layer.objects.active=rig

def export(path,objs,anim=False):
 select([rig]+objs)
 bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types={'MESH','ARMATURE','EMPTY'},add_leaf_bones=False,bake_anim=anim,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0,axis_forward='-Z',axis_up='Y',path_mode='COPY',embed_textures=True)
rig.animation_data.action=None
for pb in rig.pose.bones:pb.matrix_basis=Matrix.Identity(4)
scene.frame_set(1);bpy.context.view_layer.update()
export(P/'Hero_01_Mixamo_Bind.fbx',meshes+sockets)
for name in ['Body','Body_DefaultCoverage','Hair_Default','Hat_Visor','Shirt_Default','Shorts_Default','Shoes_Default']:
 ms=[o for o in meshes if o.name.startswith('Body_')] if name.startswith('Body') else [bpy.data.objects[name]]
 coverage.show_viewport=name!='Body';coverage.show_render=coverage.show_viewport
 export(W/('Hero_01_'+name+'.fbx'),ms+sockets if name.startswith('Body') else ms)
coverage.show_viewport=True;coverage.show_render=True
for name in ['Idle','Ready']:
 act=bpy.data.actions[name];rig.animation_data.action=act;scene.frame_start=int(act.frame_range[0]);scene.frame_end=int(act.frame_range[1]);scene.frame_set(scene.frame_start);export(P/(name+'.fbx'),[body],True)
for m,bs,socket in restore:m.node_tree.links.new(socket,bs.inputs['Base Color'])
# Keep the editable QA scene with both clips and all cosmetic meshes.
rig.animation_data.action=bpy.data.actions['Idle'];scene.frame_start=1;scene.frame_end=299;scene.frame_set(1);scene.render.resolution_percentage=50;scene.cycles.samples=16
bpy.ops.wm.save_as_mainfile(filepath=str(P/'Hero_01_Mixamo_QA.blend'))
for name in ['Hero_01_BaseColor.png','Hero_01_Normal.png','Hero_01_Iris.png','Hero_01_SkinMask.png','Hero_01_SkinMask.json']:
 shutil.copy2(P.parent/'hero/modular'/name,W/name)
print('EXPORT_DONE')
