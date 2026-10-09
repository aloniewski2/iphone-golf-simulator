"""HeroFromPlate: NEW Tripo source only. Never opens rejected assets."""
import bpy,bmesh,math,json,numpy as np
from pathlib import Path
from mathutils import Vector,Matrix
P=Path(__file__).resolve().parent
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(P.parent/'blockout/Hero_01_tripo.glb'))
body=next(o for o in bpy.context.scene.objects if o.type=='MESH');body.name='Hero_01_Body'
bpy.context.view_layer.objects.active=body;body.select_set(True);bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
# Tripo +X forward -> Blender -Y forward, metric 1.70m, origin at soles.
vs=[v.co for v in body.data.vertices];lo=Vector(tuple(min(v[i] for v in vs) for i in range(3)));hi=Vector(tuple(max(v[i] for v in vs) for i in range(3)));center=Vector(((hi.x+lo.x)/2,(hi.y+lo.y)/2,lo.z));R=Matrix.Rotation(-math.pi/2,4,'Z')
for v in body.data.vertices:v.co=R@((v.co-center)*(1.7/(hi.z-lo.z)))
# Remove duplicate UV-split geometry vertices while preserving per-loop UVs; remove loose verts.
bm=bmesh.new();bm.from_mesh(body.data);before=len(bm.verts);bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.000015)
loose=[v for v in bm.verts if not v.link_faces]
if loose:bmesh.ops.delete(bm,geom=loose,context='VERTS')
bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(body.data);bm.free()
for p in body.data.polygons:p.use_smooth=True
body.data.update();bpy.context.view_layer.update()
# Save 2K color / 1K normal; ORM is replaced by deliberate matte material values.
color=next(i for i in bpy.data.images if i.name.startswith('Color'));color.name='Hero_01_BaseColor';color.scale(2048,2048);color.filepath_raw=str(P/'Hero_01_BaseColor.png');color.file_format='PNG';color.save();color.name='RawColor_unused';color=bpy.data.images.load(str(P/'Hero_01_BaseColor.png'),check_existing=False);color.name='Hero_01_BaseColor';color.pack()
normal=next((i for i in bpy.data.images if i.name.startswith('Normal')),None)
if normal:normal.name='Hero_01_Normal';normal.scale(1024,1024);normal.filepath_raw=str(P/'Hero_01_Normal.png');normal.file_format='PNG';normal.save();normal.name='RawNormal_unused';normal=bpy.data.images.load(str(P/'Hero_01_Normal.png'),check_existing=False);normal.name='Hero_01_Normal';normal.colorspace_settings.name='Non-Color';normal.pack()
a=np.asarray(color.pixels[:]).reshape((2048,2048,4));uv=body.data.uv_layers.active
old=body.data.materials[0]
def mat(name,rough):
 m=bpy.data.materials.new(name);m.use_nodes=True;bs=m.node_tree.nodes.get('Principled BSDF');bs.inputs['Roughness'].default_value=rough;bs.inputs['Metallic'].default_value=0;bs.inputs['Specular IOR Level'].default_value=.28
 t=m.node_tree.nodes.new('ShaderNodeTexImage');t.image=color;m.node_tree.links.new(t.outputs['Color'],bs.inputs['Base Color'])
 if normal:
  t=m.node_tree.nodes.new('ShaderNodeTexImage');t.image=normal;n=m.node_tree.nodes.new('ShaderNodeNormalMap');n.inputs['Strength'].default_value=.20;m.node_tree.links.new(t.outputs['Color'],n.inputs['Color']);m.node_tree.links.new(n.outputs['Normal'],bs.inputs['Normal'])
 return m
mats=[mat('Hero_01_MatteCloth',.74),mat('Hero_01_WarmSkin',.62),mat('Hero_01_BlondHair',.57),mat('Hero_01_Visor',.43)]
body.data.materials.clear()
for m in mats:body.data.materials.append(m)
def sample(poly):
 u=sum((uv.data[i].uv for i in poly.loop_indices),Vector((0,0)))/len(poly.loop_indices);return a[min(2047,int(u.y*2048)),min(2047,int(u.x*2048)),:3]
for p in body.data.polygons:
 c=p.center;r,g,b=sample(p);warm=r>g*1.08 and g>b*1.12
 # Classify existing volumes; no replacement body or cap primitives.
 if c.z>1.50 and not warm:p.material_index=3
 elif warm and (c.z>1.49 or (c.y>.02 and c.z>1.275) or (abs(c.x)>.15 and c.z>1.43)):p.material_index=2
 elif warm and (c.z>1.25 or (.21<c.z<.57) or (abs(c.x)>.26 and .55<c.z<1.08)):p.material_index=1
 else:p.material_index=0
# Add shallow curved eye surfaces over the original eye locations, matching their outline.
# Brown iris + smaller pupil and tiny highlights in one 256px eye texture.
size=256;pix=[]
for y in range(size):
 for x in range(size):
  u=(x/(size-1)-.5)*2;v=(y/(size-1)-.5)*2;r=math.hypot(u,v);ang=math.atan2(v,u)
  if r<.61:rgb=(.024,.012,.010)
  else:
   f=max(0,1-abs(r-.76)/.25);st=.018*math.sin(ang*37);rgb=(.105+.13*f+st,.042+.055*f+st*.3,.019+.02*f)
  if r>.94:rgb=(.04,.018,.012)
  # Only the main small graphic glint; real specular is retained as camera moves.
  if ((u+.33)/.17)**2+((v-.40)/.17)**2<1:rgb=(.98,.98,.97)
  if ((u-.28)/.065)**2+((v+.36)/.065)**2<1:rgb=(.8,.82,.82)
  pix.extend((*rgb,1))
eyeim=bpy.data.images.new('Hero_01_Iris',width=size,height=size);eyeim.pixels[:]=pix;eyeim.filepath_raw=str(P/'Hero_01_Iris.png');eyeim.file_format='PNG';eyeim.save();eyeim.pack()
eyemat=bpy.data.materials.new('Hero_01_EyeCornea');eyemat.use_nodes=True;eb=eyemat.node_tree.nodes.get('Principled BSDF');eb.inputs['Roughness'].default_value=.23;eb.inputs['Specular IOR Level'].default_value=.4;t=eyemat.node_tree.nodes.new('ShaderNodeTexImage');t.image=eyeim;eyemat.node_tree.links.new(t.outputs['Color'],eb.inputs['Base Color'])
eyes=[]
for sign,s in [(1,'L'),(-1,'R')]:
 ex=sign*.084;ez=1.399;rx=.030;rz=.041;verts=[];uvs=[];faces=[];N=40;K=10
 # Closed shallow spherical cap conforms to underlying sculpt and preserves eyelids/sclera.
 for k in range(K+1):
  r=max(.02,k/K)
  for j in range(N):
   ang=math.tau*j/N;x=ex+rx*r*math.cos(ang);z=ez+rz*r*math.sin(ang);hit,loc,n,idx=body.ray_cast(Vector((x,-1,z)),Vector((0,1,0)))
   y=loc.y-.0010-.0015*math.sqrt(max(0,1-r*r)) if hit else -.125
   verts.append((x,y,z));uvs.append(((math.cos(ang)*r+1)/2,(math.sin(ang)*r+1)/2))
 for k in range(K):
  for j in range(N):q=k*N+j;qn=k*N+(j+1)%N;faces.append((q+N,qn+N,qn,q))
 faces.append(tuple(range(N)))
 me=bpy.data.meshes.new('EyeSphere_'+s);me.from_pydata(verts,[],faces);me.update();o=bpy.data.objects.new('Hero_01_EyeSphere_'+s,me);bpy.context.collection.objects.link(o);me.materials.append(eyemat);layer=me.uv_layers.new(name='IrisUV')
 for p in me.polygons:
  p.use_smooth=True
  for li in p.loop_indices:layer.data[li].uv=uvs[me.loops[li].vertex_index]
 solid=o.modifiers.new('Thin cornea shell','SOLIDIFY');solid.thickness=.001;solid.offset=-1;bpy.context.view_layer.objects.active=o;bpy.ops.object.modifier_apply(modifier=solid.name)
 eyes.append(o)
# Head correction only, kept seamless at neck; metric height restored uniformly.
# Hair crown -> chin approximately 24.5% instead of the blockout's ~26%.
for o in [body]+eyes:
 for v in o.data.vertices:
  if v.co.z>1.25:
   t=min(1,(v.co.z-1.25)/.075);v.co.x*=1-.045*t;v.co.y*=1-.045*t;v.co.z=1.25+(v.co.z-1.25)*.90
  v.co*=1.7/1.655
# Separate existing authored-by-Tripo hair and visor surfaces for attachment editing.
# Each retains original UVs; surfaces are not regenerated.
bpy.ops.object.select_all(action='DESELECT');body.select_set(True);bpy.context.view_layer.objects.active=body
for idx,name in [(2,'Hero_01_Hair'),(3,'Hero_01_Visor')]:
 bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='DESELECT');bpy.ops.object.mode_set(mode='OBJECT')
 for p in body.data.polygons:p.select=p.material_index==idx
 bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.separate(type='SELECTED');bpy.ops.object.mode_set(mode='OBJECT')
 new=[o for o in bpy.context.selected_objects if o!=body]
 for o in new:o.name=name;o.select_set(False)
 body.select_set(True);bpy.context.view_layer.objects.active=body
# Single provisional humanoid rest armature. Mixamo binding is deliberately NOT run.
C=bpy.data.collections.new('Hero_01');bpy.context.scene.collection.children.link(C)
asset=[o for o in bpy.context.scene.objects if o.type=='MESH']
for o in asset:
 for c in list(o.users_collection):c.objects.unlink(o)
 C.objects.link(o)
ad=bpy.data.armatures.new('Hero_01_Skeleton');rig=bpy.data.objects.new('Hero_01_Rig',ad);C.objects.link(rig);bpy.context.view_layer.objects.active=rig;rig.select_set(True);body.select_set(False);bpy.ops.object.mode_set(mode='EDIT');bones={}
def bone(n,h,t,parent=None):
 b=ad.edit_bones.new(n);b.head=h;b.tail=t
 if parent:b.parent=ad.edit_bones[parent]
 bones[n]=(Vector(h),Vector(t))
bone('Root',(0,0,0),(0,0,.12));bone('Hips',(0,0,.73),(0,0,.87),'Root');bone('Spine',(0,0,.87),(0,0,1.03),'Hips');bone('Chest',(0,0,1.03),(0,0,1.19),'Spine');bone('Neck',(0,0,1.19),(0,0,1.30),'Chest');bone('Head',(0,0,1.30),(0,0,1.62),'Neck')
for sign,s in [(1,'L'),(-1,'R')]:
 bone('Shoulder.'+s,(0,0,1.18),(sign*.22,0,1.17),'Chest');bone('UpperArm.'+s,(sign*.22,0,1.17),(sign*.36,-.005,.965),'Shoulder.'+s);bone('LowerArm.'+s,(sign*.36,-.005,.965),(sign*.458,-.013,.778),'UpperArm.'+s);bone('Hand.'+s,(sign*.458,-.013,.778),(sign*.475,-.014,.655),'LowerArm.'+s)
 bone('UpperLeg.'+s,(sign*.12,0,.77),(sign*.162,0,.44),'Hips');bone('LowerLeg.'+s,(sign*.162,0,.44),(sign*.195,0,.165),'UpperLeg.'+s);bone('Foot.'+s,(sign*.195,0,.165),(sign*.195,-.16,.066),'LowerLeg.'+s);bone('Toes.'+s,(sign*.195,-.16,.066),(sign*.195,-.24,.055),'Foot.'+s)
bpy.ops.object.mode_set(mode='OBJECT');rig.show_in_front=True
# Rest-pose weights; no unverified animation claim.
def weights(v,name):
 x,y,z=v;side='L' if x>0 else 'R';ax=abs(x)
 if name.startswith(('Hero_01_Eye','Hero_01_Hair','Hero_01_Visor')) or z>1.285:return {'Head':1}
 if ax>.275 and .59<z<1.19:
  if z<.795:return {'Hand.'+side:1}
  t=max(0,min(1,(z-.925)/.085));return {'LowerArm.'+side:1-t,'UpperArm.'+side:t}
 if z<.77:
  if z<.17:return {'Foot.'+side:1}
  t=max(0,min(1,(z-.40)/.10));return {'UpperLeg.'+side:t,'LowerLeg.'+side:1-t}
 if z<.91:
  t=max(0,min(1,(z-.77)/.14));return {'Hips':1-t,'Spine':t}
 t=max(0,min(1,(z-1.01)/.14));return {'Spine':1-t,'Chest':t}
for o in asset:
 bm=bmesh.new();bm.from_mesh(o.data);bmesh.ops.triangulate(bm,faces=list(bm.faces));bad=[f for f in bm.faces if f.calc_area()<5e-13]
 if bad:bmesh.ops.delete(bm,geom=bad,context='FACES_ONLY')
 bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(o.data);bm.free()
 groups={n:o.vertex_groups.new(name=n) for n in bones}
 for v in o.data.vertices:
  for n,w in weights(v.co,o.name).items():
   if w>0:groups[n].add([v.index],w,'REPLACE')
 mod=o.modifiers.new('Provisional rest skin','ARMATURE');mod.object=rig;o.parent=rig
for n,loc,parent in [('Hat',(0,0,1.665),'Head'),('Hand_R',(-.477,-.023,.695),'Hand.R'),('Hand_L',(.477,-.023,.695),'Hand.L'),('Back',(0,.15,1.06),'Chest'),('FaceExtra',(0,-.15,1.405),'Head')]:
 o=bpy.data.objects.new(n,None);C.objects.link(o);o.empty_display_size=.035;o.parent=rig;o.parent_type='BONE';o.parent_bone=parent;bpy.context.view_layer.update();o.matrix_world.translation=loc
# Discard unused source materials/images (including 4K ORM) and export asset only.
for m in list(bpy.data.materials):
 if m.users==0:bpy.data.materials.remove(m)
for im in list(bpy.data.images):
 if im.users==0:bpy.data.images.remove(im)
bpy.ops.object.select_all(action='DESELECT')
for o in C.objects:o.select_set(True)
bpy.context.view_layer.objects.active=rig
scene=bpy.context.scene;scene.unit_settings.system='METRIC';scene.unit_settings.scale_length=1
bpy.ops.export_scene.fbx(filepath=str(P/'Hero_01.fbx'),use_selection=True,object_types={'MESH','ARMATURE','EMPTY'},add_leaf_bones=False,bake_anim=False,axis_forward='-Z',axis_up='Y',path_mode='COPY',embed_textures=True)
# Review studio: warm off-white backdrop, broad key, fill and rim. No export lights.
review=bpy.data.collections.new('REVIEW_ONLY');scene.collection.children.link(review)
def reviewobj(o):
 for c in list(o.users_collection):c.objects.unlink(o)
 review.objects.link(o)
bpy.ops.mesh.primitive_plane_add(size=2000);floor=bpy.context.object;reviewobj(floor);floor.name='Studio floor';floor.location.z=-.003
m=bpy.data.materials.new('Studio warm neutral');m.diffuse_color=(.70,.73,.71,1);floor.data.materials.append(m)
scene.world=bpy.data.worlds.new('Soft studio ambient');scene.world.use_nodes=True;scene.world.node_tree.nodes.get('Background').inputs[0].default_value=(.8,.85,.92,1);scene.world.node_tree.nodes.get('Background').inputs[1].default_value=.22
for name,pos,power,size,col in [('Key',(-3,-4,5),550,4,(1,.91,.82)),('Fill',(3,-2,3),230,3,(.85,.92,1)),('Rim',(1,3,4),450,3,(1,.94,.85))]:
 bpy.ops.object.light_add(type='AREA',location=pos);o=bpy.context.object;o.name=name;reviewobj(o);o.data.energy=power;o.data.shape='DISK';o.data.size=size;o.data.color=col;o.rotation_euler=(Vector((0,0,.9))-o.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add();cam=bpy.context.object;cam.name='Review camera';reviewobj(cam);cam.data.type='ORTHO';cam.data.ortho_scale=1.94;scene.camera=cam
scene.render.engine='CYCLES';scene.cycles.samples=64;scene.render.resolution_x=1100;scene.render.resolution_y=1300;scene.render.resolution_percentage=100;scene.view_settings.view_transform='AgX';scene.view_settings.look='AgX - Medium High Contrast';scene.view_settings.exposure=.1
for name,pos in [('front',(0,-5,.88)),('side',(5,0,.88)),('threequarter',(2.3,-5,1.07))]:
 cam.location=pos;cam.rotation_euler=(Vector((0,0,.86))-cam.location).to_track_quat('-Z','Y').to_euler();scene.render.filepath=str(P/f'Hero_01_{name}.png');bpy.ops.render.render(write_still=True)
stats={'source':'ArtDir/blockout/Hero_01_tripo.glb','tripo_task':'2286dbbb-2ddf-4f3d-8fff-03c30680a07d','raw_vertices_before_merge':before,'meshes':{},'height':max(v.co.z for o in asset for v in o.data.vertices),'head_chin_z':1.25*1.7/1.655,'armatures':1,'bones':len(ad.bones)}
for o in asset:o.data.calc_loop_triangles();stats['meshes'][o.name]={'vertices':len(o.data.vertices),'triangles':len(o.data.loop_triangles)}
(P/'validation.json').write_text(json.dumps(stats,indent=2))
bpy.ops.object.select_all(action='DESELECT');rig.select_set(True);bpy.context.view_layer.objects.active=rig
bpy.ops.wm.save_as_mainfile(filepath=str(P/'Hero_01.blend'));print('HERO_DONE',json.dumps(stats),flush=True)
