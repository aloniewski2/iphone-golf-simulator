import bpy,bmesh,sys,math,json
from pathlib import Path
from mathutils import Matrix
root=Path(sys.argv[sys.argv.index('--')+1]);out=root/'ArtDir/hero/integrity_v1/export';out.mkdir(exist_ok=True,parents=True);rig=bpy.data.objects['Hero_01_Rig']
rig.animation_data_clear()
for b in rig.pose.bones:b.matrix_basis=Matrix.Identity(4)
bpy.context.view_layer.update()
# A small skinned skin join closes the rear neck opening without touching the face.
old=bpy.data.objects.get('Body_NeckSeal')
if old:bpy.data.objects.remove(old,do_unlink=True)
verts=[];faces=[];N=32;rows=8
for row in range(rows):
 t=row/(rows-1);z=1.18+.15*t;r=.055+.025*t
 for k in range(N):
  a=k*math.tau/N;verts.append((r*math.cos(a),.058+r*.90*math.sin(a),z))
for row in range(rows-1):
 for k in range(N):
  a=row*N+k;b=row*N+(k+1)%N;faces.append((a,b,b+N,a+N))
faces.append(tuple(reversed(range(N))));faces.append(tuple((rows-1)*N+k for k in range(N)))
m=bpy.data.meshes.new('Body_NeckSeal');m.from_pydata(verts,[],faces);m.update();o=bpy.data.objects.new('Body_NeckSeal',m);bpy.context.collection.objects.link(o)
mat=bpy.data.materials.new('Hero neck skin');mat.use_nodes=True;bs=mat.node_tree.nodes.get('Principled BSDF');bs.inputs['Base Color'].default_value=(.73,.48,.30,1);bs.inputs['Roughness'].default_value=.64;m.materials.append(mat)
for p in m.polygons:p.use_smooth=True
for name in ['Chest','Neck','Head']:o.vertex_groups.new(name=name)
for v in m.vertices:
 t=(v.co.z-1.18)/.15;head=max(0,(t-.45)/.55)*.65;chest=(1-t)*.40
 o.vertex_groups['Chest'].add([v.index],chest,'REPLACE');o.vertex_groups['Head'].add([v.index],head,'REPLACE');o.vertex_groups['Neck'].add([v.index],1-chest-head,'REPLACE')
arm=o.modifiers.new('Shared skeleton','ARMATURE');arm.object=rig;o.parent=rig
# Neck join is constant skin, not a projection target; a simple UV exists for later material work.
m.uv_layers.new(name='NeckUV')
for p in m.polygons:
 for li in p.loop_indices:
  v=m.vertices[m.loops[li].vertex_index].co;m.uv_layers.active.data[li].uv=((math.atan2(v.y-.058,v.x)/math.tau)%1,(v.z-1.18)/.15)
# Export body coverage mask into a temporary copy while retaining female shape keys.
def masked(source):
 masks=[x for x in source.modifiers if x.type=='MASK']
 if not masks:return source,None
 c=source.copy();c.data=source.data.copy();bpy.context.collection.objects.link(c)
 for mod in masks:
  gi=c.vertex_groups[mod.vertex_group].index;bm=bmesh.new();bm.from_mesh(c.data);dl=bm.verts.layers.deform.active
  kill=[v for v in bm.verts if ((v[dl].get(gi,0) if dl else 0)>mod.threshold)==mod.invert_vertex_group]
  bmesh.ops.delete(bm,geom=kill,context='VERTS');bm.to_mesh(c.data);bm.free();c.modifiers.remove(c.modifiers[mod.name])
 source.name+='__source';c.name='Body_Skin';return c,source
def export(name,parts):
 bpy.ops.object.select_all(action='DESELECT');rig.select_set(True)
 for ob in parts:ob.select_set(True)
 bpy.context.view_layer.objects.active=rig;bpy.ops.export_scene.fbx(filepath=str(out/name),use_selection=True,object_types={'MESH','ARMATURE'},add_leaf_bones=False,bake_anim=False,axis_forward='-Z',axis_up='Y',use_mesh_modifiers=True)
body,original=masked(bpy.data.objects['Body_Skin']);export('Hero_01_FingerBody.fbx',[body,o])
if original:bpy.data.objects.remove(body,do_unlink=True);original.name='Body_Skin'
for name in ['Hair_Default','Hat_Visor','Shirt_Default','Shorts_Default','Shoes_Default']:
 parts=[bpy.data.objects[name]]
 if name=='Hair_Default':parts+=[x for x in bpy.data.objects if x.type=='MESH' and x.name.startswith('Hair_') and x.name!=name]
 export('Hero_01_'+name+'.fbx',parts)
bpy.ops.wm.save_as_mainfile(filepath=str(root/'ArtDir/hero/integrity_v1/Hero_Integrity_v1.blend'));print('EXPORT_INTEGRITY_DONE')
