"""Hero_01 authored production geometry. Outputs confined to ArtDir/hero."""
import bpy,bmesh,math,json
from pathlib import Path
from mathutils import Vector
P=Path(__file__).resolve().parent
bpy.ops.wm.read_factory_settings(use_empty=True)
C=bpy.data.collections.new('Hero_01');bpy.context.scene.collection.children.link(C)
colors=['FFE0C2','FFFFFF','283952','FF6B3D','E8B349','B88230','241D25','FFFFFF','C98F77','D9E1E8','674628','F0CA6E']
def lin(x):return x/12.92 if x<.04045 else ((x+.055)/1.055)**2.4
# One 1K atlas: 4 x 4 broad swatches plus painted face and iris islands.
im=bpy.data.images.load(str(P/'Hero_01_Atlas.png'));im.name='Hero_01_Atlas';im.pack()
mat=bpy.data.materials.new('Hero_01 • matte atlas');mat.use_nodes=True
bs=mat.node_tree.nodes.get('Principled BSDF');bs.inputs['Roughness'].default_value=.68;bs.inputs['Specular IOR Level'].default_value=.25
tex=mat.node_tree.nodes.new('ShaderNodeTexImage');tex.image=im;tex.interpolation='Closest';mat.node_tree.links.new(tex.outputs['Color'],bs.inputs['Base Color'])
materials={}
for name,rough,spec in [('Skin',.52,.3),('Cloth',.72,.24),('Hair',.53,.32),('Visor',.43,.35),('Eyes',.20,.45)]:
 m=mat.copy();m.name='Hero_01_'+name;b=m.node_tree.nodes.get('Principled BSDF');b.inputs['Roughness'].default_value=rough;b.inputs['Specular IOR Level'].default_value=spec;materials[name]=m
parts=[];socket_parents={};weights={}
def move(o):
 for c in list(o.users_collection):c.objects.unlink(o)
 C.objects.link(o)
def finish(o,name,tile,bone):
 o.name=name;move(o);o.data.materials.clear();kind='Eyes' if name.startswith(('Eye','Iris','Pupil','Glint')) else 'Hair' if name.startswith('Hair') else 'Visor' if name.startswith('Visor') else 'Skin' if tile in [0,8,12] else 'Cloth';o.data.materials.append(materials[kind])
 if not o.data.uv_layers:o.data.uv_layers.new(name='PaletteUV')
 uv=o.data.uv_layers.active;uv.name='PaletteUV'
 for l in uv.data:l.uv=((tile%4+.5)/4,(tile//4+.5)/4)
 for p in o.data.polygons:p.use_smooth=True
 parts.append(o);weights[o.name]=bone
 return o

def mesh(name,vs,fs,tile,bone):
 m=bpy.data.meshes.new(name);m.from_pydata(vs,[],fs);m.update();o=bpy.data.objects.new(name,m);C.objects.link(o)
 bm=bmesh.new();bm.from_mesh(m);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(m);bm.free()
 return finish(o,name,tile,bone)
def ell(name,loc,sc,tile,bone,seg=24,rings=16,rot=None):
 bpy.ops.mesh.primitive_uv_sphere_add(segments=seg,ring_count=rings,location=loc);o=bpy.context.object;o.scale=sc
 if rot:o.rotation_euler=rot
 bpy.ops.object.transform_apply(location=True,rotation=True,scale=True);return finish(o,name,tile,bone)
def loft(name,rows,tile,bone,n=32,caps=True):
 vs=[]
 for z,x,y,rx,ry in rows:
  for j in range(n):a=2*math.pi*j/n;vs.append((x+rx*math.cos(a),y+ry*math.sin(a),z))
 fs=[]
 for i in range(len(rows)-1):
  for j in range(n):a=i*n+j;b=i*n+(j+1)%n;fs.append((a,b,b+n,a+n))
 if caps:fs+=[tuple(reversed(range(n))),tuple((len(rows)-1)*n+j for j in range(n))]
 return mesh(name,vs,fs,tile,bone)
def tube(name,points,radii,tile,bone,n=20):
 vs=[]
 for i,p in enumerate(points):
  p=Vector(p);t=Vector(points[min(len(points)-1,i+1)])-Vector(points[max(0,i-1)]);t.normalize();u=Vector((0,-1,0));v=t.cross(u).normalized()
  for j in range(n):a=j*2*math.pi/n;vs.append(p+radii[i]*(u*math.cos(a)+v*math.sin(a)))
 fs=[]
 for i in range(len(points)-1):
  for j in range(n):a=i*n+j;b=i*n+(j+1)%n;fs.append((a,b,b+n,a+n))
 fs.extend([tuple(reversed(range(n))),tuple((len(points)-1)*n+j for j in range(n))]);return mesh(name,vs,fs,tile,bone)
def curve(name,pts,r,tile,bone):
 cv=bpy.data.curves.new(name,'CURVE');cv.dimensions='3D';cv.bevel_depth=r;cv.bevel_resolution=1;cv.resolution_u=2;s=cv.splines.new('BEZIER');s.bezier_points.add(len(pts)-1)
 for q,p in zip(s.bezier_points,pts):q.co=p;q.handle_left_type='AUTO';q.handle_right_type='AUTO'
 o=bpy.data.objects.new(name,cv);C.objects.link(o);bpy.context.view_layer.objects.active=o;o.select_set(True);bpy.ops.object.convert(target='MESH');o.select_set(False);return finish(o,name,tile,bone)
# One generic rest rig; no Mixamo bind or Humanoid mapping is performed here.
ad=bpy.data.armatures.new('Hero_01_Skeleton');rig=bpy.data.objects.new('Hero_01_Rig',ad);C.objects.link(rig);bpy.context.view_layer.objects.active=rig;rig.select_set(True);bpy.ops.object.mode_set(mode='EDIT')
bones={}
def bone(n,h,t,parent=None):
 b=ad.edit_bones.new(n);b.head=h;b.tail=t
 if parent:b.parent=ad.edit_bones[parent]
 bones[n]=(Vector(h),Vector(t));return b
bone('Root',(0,0,0),(0,0,.15));bone('Hips',(0,0,.79),(0,0,.94),'Root');bone('Spine',(0,0,.94),(0,0,1.10),'Hips');bone('Chest',(0,0,1.10),(0,0,1.28),'Spine');bone('Neck',(0,0,1.28),(0,0,1.365),'Chest');bone('Head',(0,0,1.365),(0,0,1.68),'Neck')
for sign,s in [(1,'L'),(-1,'R')]:
 bone('Shoulder.'+s,(0,0,1.26),(sign*.24,0,1.255),'Chest');bone('UpperArm.'+s,(sign*.24,0,1.255),(sign*.445,0,1.03),'Shoulder.'+s);bone('LowerArm.'+s,(sign*.445,0,1.03),(sign*.61,0,.845),'UpperArm.'+s);bone('Hand.'+s,(sign*.61,0,.845),(sign*.715,0,.735),'LowerArm.'+s)
 bone('UpperLeg.'+s,(sign*.113,0,.79),(sign*.13,0,.44),'Hips');bone('LowerLeg.'+s,(sign*.13,0,.44),(sign*.14,0,.145),'UpperLeg.'+s);bone('Foot.'+s,(sign*.14,0,.145),(sign*.14,-.125,.065),'LowerLeg.'+s);bone('Toes.'+s,(sign*.14,-.125,.065),(sign*.14,-.22,.055),'Foot.'+s)
bpy.ops.object.mode_set(mode='OBJECT');rig.select_set(False);rig.show_in_front=True
# Short relaxed polo torso.
loft('Polo_Torso',[(.855,0,0,.192,.121),(.87,0,0,.201,.129),(.91,0,0,.200,.131),(1.02,0,0,.202,.137),(1.14,0,0,.224,.140),(1.225,0,0,.237,.124),(1.26,0,0,.230,.108),(1.29,0,0,.155,.082),(1.305,0,0,.07,.061)],1,'torso')
loft('Neck',[(1.26,0,0,.071,.061),(1.32,0,0,.071,.061),(1.395,0,0,.080,.063)],0,'Neck')
# Shared connected waist with crotch strip and two leg loops.
vs=[]
for z in [.875,.84,.785]:
 for j in range(32):a=2*math.pi*j/32;vs.append((.203*math.cos(a),.135*math.sin(a),z))
for j in range(1,8):vs.append((0,.135*math.cos(math.pi*j/8),.695))
for sign in [1,-1]:
 for z in [.665,.607,.595]:
  for j in range(24):a=2*math.pi*j/24;vs.append((sign*.113+.098*math.cos(a),.115*math.sin(a),z))
fs=[]
for i in range(2):
 for j in range(32):fs.append((i*32+j,i*32+(j+1)%32,(i+1)*32+(j+1)%32,(i+1)*32+j))
for idx,top in enumerate([[64+j%32 for j in range(24,41)]+[96+j for j in range(7)],[64+j for j in range(8,25)]+[96+j for j in reversed(range(7))]]):
 off=103+idx*72;shift=18 if idx==0 else 6
 for j in range(24):fs.append((top[j],top[(j+1)%24],off+(j+1+shift)%24,off+(j+shift)%24))
 for k in range(2):
  for j in range(24):fs.append((off+k*24+j,off+k*24+(j+1)%24,off+(k+1)*24+(j+1)%24,off+(k+1)*24+j))
mesh('Shorts',vs,fs,2,'legs')
for sign,s in [(1,'L'),(-1,'R')]:
 # Rounded knees with extra deformation rings.
 loft('Leg_'+s,[(z,sign*x,0,rx,ry) for z,x,rx,ry in [(.135,.14,.047,.051),(.23,.139,.057,.058),(.34,.135,.071,.068),(.405,.132,.073,.071),(.44,.13,.073,.073),(.475,.128,.075,.075),(.55,.122,.082,.080),(.65,.113,.082,.080)]],0,'leg'+s)
 loft('Sock_'+s,[(.115,sign*.14,0,.052,.058),(.215,sign*.14,0,.061,.064),(.225,sign*.14,0,.061,.064)],1,'LowerLeg.'+s)
 loft('Shoe_'+s,[(.007,sign*.14,-.068,.079,.158),(.018,sign*.14,-.068,.084,.162),(.052,sign*.14,-.068,.084,.162),(.078,sign*.14,-.06,.079,.15),(.115,sign*.14,-.036,.069,.119),(.152,sign*.14,-.005,.055,.069)],1,'Foot.'+s)
 ell('ShoeToe_'+s,(sign*.14,-.118,.066),(.080,.105,.057),1,'Foot.'+s,24,16)
 loft('Sole_'+s,[(0,sign*.14,-.068,.080,.158),(.013,sign*.14,-.068,.085,.163),(.035,sign*.14,-.068,.085,.163)],9,'Foot.'+s)
 for i in range(3):curve('Lace_'+s+str(i),[(sign*.14-.039,-.12+i*.025,.122+i*.01),(sign*.14,-.125+i*.025,.129+i*.01),(sign*.14+.039,-.12+i*.025,.122+i*.01)],.005,1,'Foot.'+s)
 ell('ShoeAccent_'+s,(sign*.214,-.055,.080),(.012,.059,.029),3,'Foot.'+s,20,12)
 curve('ShoeAccentFront_'+s,[(sign*.14-.05,-.185,.057),(sign*.14,-.193,.059),(sign*.14+.05,-.185,.057)],.009,3,'Foot.'+s)
 shoulder=Vector((sign*.215,0,1.27));elbow=Vector((sign*.445,0,1.03));wrist=Vector((sign*.61,0,.845))
 pts=[shoulder.lerp(elbow,t) for t in [.45,.6,.8,1]]+[elbow.lerp(wrist,t) for t in [.15,.3,.5,.7,1]]
 tube('Arm_'+s,pts,[.075,.073,.071,.07,.069,.068,.065,.059,.049],0,'arm'+s)
 pts=[Vector((sign*.13,0,1.222)),Vector((sign*.215,0,1.228))]+[shoulder.lerp(elbow,t) for t in [.2,.4,.56,.6]];tube('Sleeve_'+s,pts,[.06,.084,.087,.086,.084,.083],1,'arm'+s)
 tube('SleeveNavy_'+s,[shoulder.lerp(elbow,t) for t in [.53,.60]],[.086,.085],2,'arm'+s)
 tube('SleeveOrange_'+s,[shoulder.lerp(elbow,t) for t in [.60,.625]],[.085,.084],3,'arm'+s)
 # Palm plus three individually readable rounded fingers and a thumb.
 axis=Vector((sign*.65,0,-.76));across=Vector((sign*.76,0,.65));center=Vector((sign*.65,-.005,.807))
 hand=ell('Palm_'+s,center,(.052,.032,.065),0,'Hand.'+s,24,16,rot=(0,sign*.65,0));handparts=[hand];bridge=ell('Wrist_'+s,(sign*.615,-.001,.845),(.045,.036,.052),0,'Hand.'+s,20,12,rot=(0,sign*.65,0));handparts.append(bridge)
 for j,(off,length) in enumerate([(-.033,.065),(0,.077),(.033,.064)]):
  start=center+axis*.024+across*off
  ps=[start+axis*(length*t)+Vector((0,-.013*t*t,0)) for t in [0,.25,.55,.8,1]]
  finger=tube('Finger_'+s+str(j),ps,[.021,.023,.022,.017,.007],0,'Hand.'+s,12);handparts.append(finger)
  tip=ell('FingerTip_'+s+str(j),ps[-1],(.009,.012,.012),0,'Hand.'+s,12,8);handparts.append(tip)
 thumb=ell('Thumb_'+s,center-across*.057+axis*.005+Vector((0,-.015,0)),(.027,.029,.048),0,'Hand.'+s,20,12,rot=(0,sign*1.1,0));handparts.append(thumb)
 bpy.ops.object.select_all(action='DESELECT')
 for o in handparts:o.select_set(True)
 bpy.context.view_layer.objects.active=hand;bpy.ops.object.join()
 for o in handparts[1:]:parts.remove(o)
 rem=hand.modifiers.new('Joined fingers palm','REMESH');rem.mode='VOXEL';rem.voxel_size=.0045;rem.use_smooth_shade=True;bpy.ops.object.modifier_apply(modifier=rem.name)
 smooth=hand.modifiers.new('Soft knuckles','SMOOTH');smooth.factor=.55;smooth.iterations=3;bpy.ops.object.modifier_apply(modifier=smooth.name)
 dec=hand.modifiers.new('Finger retopology budget','DECIMATE');dec.ratio=.28;bpy.ops.object.modifier_apply(modifier=dec.name)
 finish(hand,'Palm_'+s,0,'Hand.'+s);parts.remove(hand)
 for v in hand.data.vertices:v.co=Vector((sign*.61,0,.845))+(v.co-Vector((sign*.61,0,.845)))*1.12
# Sculpted head from regular rings: wider cheeks, softly squared jaw and recessed eyes.
head=ell('Head',(0,0,1.515),(.196,.166,.202),12,'Head',48,32)
for v in head.data.vertices:
 x,y,z=v.co
 # Smooth local deformation preserves the locked crown/chin extent.
 if y<0:
  cheek=math.exp(-((abs(x)-.108)/.068)**2-((z-1.478)/.052)**2)
  eye=math.exp(-((abs(x)-.078)/.05)**2-((z-1.555)/.055)**2)
  v.co.y-=.017*cheek;v.co.y+=.004*eye
 if z<1.47:
  v.co.x*=1+.16*math.exp(-((z-1.40)/.065)**2)
# Head front projection into its own painted face tile, with peach cheeks and soft eye shading.
for poly in head.data.polygons:
 for li in poly.loop_indices:
  v=head.data.vertices[head.data.loops[li].vertex_index].co
  head.data.uv_layers.active.data[li].uv=((.04+.92*(v.x/.43+.5))/4,(3+.04+.92*((v.z-1.313)/.404))/4)
for sign in [1,-1]:
 ell('Ear',(sign*.193,.006,1.492),(.038,.028,.050),0,'Head',20,12)
 ell('EarInset',(sign*.214,-.018,1.492),(.015,.006,.027),8,'Head',16,10)
 # Sclera sits in the socket; iris area stays brown around a smaller pupil.
 ex=sign*.077;ez=1.548
 eye=ell('EyeSclera',(ex,-.148,ez),(.046,.014,.055),7,'Head',32,20)
 for v in eye.data.vertices:
  # lift outer corner slightly for a friendly almond silhouette
  v.co.z+=.09*sign*(v.co.x-ex)
 iris=ell('Iris',(ex+sign*.003,-.162,ez),(.032,.009,.043),13,'Head',32,20)
 for poly in iris.data.polygons:
  for li in poly.loop_indices:
   v=iris.data.vertices[iris.data.loops[li].vertex_index].co
   iris.data.uv_layers.active.data[li].uv=((1+.1+.8*((v.x-ex-sign*.003)/.064+.5))/4,(3+.1+.8*((v.z-ez)/.086+.5))/4)
 ell('Pupil',(ex+sign*.003,-.1705,ez+.003),(.018,.005,.027),6,'Head',24,16)
 ell('Glint',(ex-.010,-.177,ez+.022),(.009,.003,.012),7,'Head',16,10)
 ell('GlintSmall',(ex+.011,-.175,ez-.015),(.003,.002,.004),7,'Head',12,8)
 curve('UpperLid',[(ex-.048,-.154,ez+.016),(ex-.032,-.163,ez+.047),(ex,-.17,ez+.055),(ex+.034,-.16,ez+.044),(ex+.049,-.151,ez+.012)],.004,10,'Head')
 curve('Brow',[(sign*.04,-.155,1.617),(sign*.074,-.165,1.629),(sign*.111,-.145,1.622)],.008,5,'Head')
# Integrated soft nose volume and shaped smile, rather than a stuck-on ball.
ell('NoseBridge',(0,-.151,1.516),(.020,.017,.033),0,'Head',24,16)
ell('NoseTip',(0,-.174,1.497),(.025,.026,.019),0,'Head',24,16)
curve('Smile',[(-.040,-.157,1.455),(-.023,-.157,1.443),(0,-.1555,1.439),(.023,-.157,1.443),(.040,-.157,1.455)],.0032,10,'Head')
# Polo collar folded front flaps and placket.
for sign in [1,-1]:
 mesh('Collar',[(sign*.015,-.078,1.312),(sign*.072,-.057,1.313),(sign*.13,-.100,1.255),(sign*.076,-.139,1.227),(sign*.033,-.116,1.276)],[(0,1,2,3,4)],2,'Chest')
 curve('CollarOrange',[(sign*.13,-.102,1.256),(sign*.076,-.141,1.227),(sign*.033,-.118,1.276)],.004,3,'Chest')
curve('Placket',[(0,-.103,1.279),(0,-.135,1.225),(0,-.141,1.17)],.005,3,'Chest')
for z in [1.236,1.206]:ell('PoloButton',(.008,-.144,z),(.006,.003,.006),1,'Chest',12,8)
# Base hair follows skull; separate tapered locks break up silhouette and expose direction.
vs=[];fs=[];N=48;K=12
for k in range(K):
 for j in range(N):
  a=j*2*math.pi/N;front=max(0,-math.sin(a));bottom=1.473+.181*front**2;thetaMax=math.acos((bottom-1.54)/.207);theta=.008+(thetaMax-.008)*k/(K-1)
  vs.append((.201*math.sin(theta)*math.cos(a),.012+.171*math.sin(theta)*math.sin(a),1.54+.207*math.cos(theta)))
for k in range(K-1):
 for j in range(N):a=k*N+j;b=k*N+(j+1)%N;fs.append((a,b,b+N,a+N))
mesh('Hair_Base',vs,fs,5,'Head')
def hairlock(name,centers,widths,depths,tile):
 verts=[];faces=[];n=12
 for i,p in enumerate(centers):
  t=(Vector(centers[min(i+1,len(centers)-1)])-Vector(centers[max(0,i-1)])).normalized()
  u=t.cross(Vector((0,1,0))).normalized();v=t.cross(u).normalized()
  for j in range(n):
   a=j*math.tau/n;verts.append(Vector(p)+u*widths[i]*math.cos(a)+v*depths[i]*math.sin(a))
 for i in range(len(centers)-1):
  for j in range(n):a=i*n+j;b=i*n+(j+1)%n;faces.append((a,b,b+n,a+n))
 faces += [tuple(reversed(range(n))),tuple((len(centers)-1)*n+j for j in range(n))]
 o=mesh(name,verts,faces,tile,'Head');sub=o.modifiers.new('Rounded sculpted lock','SUBSURF');sub.levels=1;bpy.context.view_layer.objects.active=o;bpy.ops.object.modifier_apply(modifier=sub.name);return o
for i in range(7):
 y=-.112+i*.032
 hairlock('Hair_SweptLock'+str(i),[(-.154,y+.016,1.661),(-.113,y,1.703),(-.035,y-.006,1.739),(.05,y,1.739),(.128,y+.018,1.708),(.177,y+.027,1.659)],[.005,.018,.026,.025,.018,.003],[.008,.018,.022,.022,.015,.003],11 if i%3==0 else 4)
for sign in [1,-1]:
 for i in range(3):
  hairlock('Hair_SideLock',[(sign*.17,-.072+i*.05,1.676),(sign*.196,-.07+i*.05,1.613),(sign*.2,-.049+i*.05,1.554),(sign*.186,-.025+i*.05,1.491)],[.023,.027,.025,.002],[.016,.023,.020,.003],4 if i%2 else 11)
# Visor band and curved bill in front (-Y), separate cosmetic mesh.
band=loft('Visor_Band',[(1.635,0,0,.207,.177),(1.65,0,0,.208,.178),(1.667,0,0,.208,.178)],1,'Head',48,False)
for v in band.data.vertices:v.co.z+=.075*max(0,-v.co.y/.178)
vs=[];fs=[]
for i in range(5):
 t=i/4
 for j in range(33):
  a=math.pi+math.pi*j/32;x=(.203+.017*t)*math.cos(a);y=(.175+.083*t)*math.sin(a);z=1.635+.075*max(0,-math.sin(a))-.010*t+.008*math.cos(a)**2;vs.append((x,y,z))
for i in range(4):
 for j in range(32):a=i*33+j;fs.append((a,a+1,a+34,a+33))
visor=mesh('Visor_Bill',vs,fs,1,'Head');sol=visor.modifiers.new('Bill thickness','SOLIDIFY');sol.thickness=.009;bpy.context.view_layer.objects.active=visor;bpy.ops.object.modifier_apply(modifier=sol.name)
curve('VisorNavyRim',[vs[132+j] for j in range(33)],.0045,2,'Head')
# Preserve the locked 1.75 m silhouette after adding swept hair volumes.
for o in parts:
 if o.name.startswith('Hair'):
  for v in o.data.vertices:
   if v.co.z>1.6:v.co.z=1.6+(v.co.z-1.6)*.91
# Soften sleeve shoulder corners while retaining the original body extents.
for o in parts:
 if o.name.startswith('Sleeve_'):
  sub=o.modifiers.new('Soft garment shoulder','SUBSURF');sub.levels=1;bpy.context.view_layer.objects.active=o;bpy.ops.object.modifier_apply(modifier=sub.name)
# Assign normalized analytic weights, <= 2 influences per vertex, identity transforms.
def influence(co,kind):
 if kind=='torso':
  t=max(0,min(1,(co.z-.97)/.2));return {'Spine':1-t,'Chest':t}
 if kind=='legs':
  side='L' if co.x>0 else 'R';t=max(0,min(1,(co.z-.69)/.15));return {'UpperLeg.'+side:1-t,'Hips':t}
 if kind.startswith('leg'):
  s=kind[-1];t=max(0,min(1,(co.z-.39)/.1));return {'UpperLeg.'+s:t,'LowerLeg.'+s:1-t}
 if kind.startswith('arm'):
  s=kind[-1];t=max(0,min(1,(1.08-co.z)/.1));return {'UpperArm.'+s:1-t,'LowerArm.'+s:t}
 return {kind:1}
for o in list(dict.fromkeys(parts)):
 if o.name not in bpy.data.objects:continue
 o.vertex_groups.clear();kind=weights[o.name];groups={n:o.vertex_groups.new(name=n) for n in bones}
 for v in o.data.vertices:
  for n,w in influence(v.co,kind).items():
   if w>0:groups[n].add([v.index],w,'REPLACE')
 mod=o.modifiers.new('Hero_01 skin','ARMATURE');mod.object=rig;o.parent=rig;o.matrix_parent_inverse=rig.matrix_world.inverted()
# Join to three export meshes (body, hair, visor), preserving weights and UVs.
allparts=[o.name for o in dict.fromkeys(parts)]
for group,selector in [('Hero_01_Hair',lambda n:n.startswith('Hair')),('Hero_01_Visor',lambda n:n.startswith('Visor')),('Hero_01_Body',lambda n:not n.startswith(('Hair','Visor')))]:
 selected=[bpy.data.objects[n] for n in allparts if n in bpy.data.objects and selector(n)];bpy.ops.object.select_all(action='DESELECT')
 for o in selected:o.select_set(True)
 bpy.context.view_layer.objects.active=selected[0];bpy.ops.object.join();o=bpy.context.object;o.name=group
# Attach sockets, exact names requested, local rest positions independent of runtime code.
for n,loc,par in [('Hat',(0,0,1.715),'Head'),('Hand_R',(-.68,-.02,.77),'Hand.R'),('Hand_L',(.68,-.02,.77),'Hand.L'),('Back',(0,.145,1.12),'Chest'),('FaceExtra',(0,-.195,1.51),'Head')]:
 o=bpy.data.objects.new(n,None);C.objects.link(o);o.empty_display_type='PLAIN_AXES';o.empty_display_size=.045;o.parent=rig;o.parent_type='BONE';o.parent_bone=par;bpy.context.view_layer.update();o.matrix_world.translation=loc
# Verify asset statistics; keep rest pose, no actions.
for o in C.objects:
 if o.animation_data:o.animation_data_clear()
meshes=[o for o in C.objects if o.type=='MESH'];stats={'armatures':1,'height':max(v.co.z for o in meshes for v in o.data.vertices),'head_chin_z':1.313,'atlas':'1024 x 1024','meshes':{},'sockets':['Hat','Hand_R','Hand_L','Back','FaceExtra']}
for o in meshes:o.data.calc_loop_triangles();stats['meshes'][o.name]={'vertices':len(o.data.vertices),'triangles':len(o.data.loop_triangles)}
(P/'validation.json').write_text(json.dumps(stats,indent=2))
# Export selected hero ONLY; no camera, lights or floor in FBX.
bpy.ops.object.select_all(action='DESELECT')
for o in C.objects:o.select_set(True)
bpy.context.view_layer.objects.active=rig
bpy.ops.export_scene.fbx(filepath=str(P/'Hero_01.fbx'),use_selection=True,object_types={'ARMATURE','MESH','EMPTY'},add_leaf_bones=False,bake_anim=False,apply_unit_scale=True,axis_forward='-Z',axis_up='Y',path_mode='COPY',embed_textures=True)
# Separate review-only collection.
review=bpy.data.collections.new('REVIEW_ONLY • cameras lights floor');bpy.context.scene.collection.children.link(review)
def reviewobj(o):
 for c in list(o.users_collection):c.objects.unlink(o)
 review.objects.link(o)
bpy.ops.mesh.primitive_plane_add(size=200);floor=bpy.context.object;floor.name='Review floor';reviewobj(floor);floor.location.z=-.002
fm=bpy.data.materials.new('Review warm floor');fm.diffuse_color=(.89,.87,.81,1);floor.data.materials.append(fm)
scene=bpy.context.scene;scene.unit_settings.system='METRIC';scene.unit_settings.scale_length=1
scene.world=bpy.data.worlds.new('Review sky');scene.world.use_nodes=True;scene.world.node_tree.nodes.get('Background').inputs[0].default_value=(.72,.78,.82,1);scene.world.node_tree.nodes.get('Background').inputs[1].default_value=.14
for pos,power,size,col in [((-2.5,-3,4),380,3,(1,.85,.67)),((3,-2,2.8),120,3,(.84,.91,1)),((1.5,2.3,3.5),280,2,(.85,.93,1))]:
 bpy.ops.object.light_add(type='AREA',location=pos);o=bpy.context.object;reviewobj(o);o.data.energy=power;o.data.size=size;o.data.color=col;o.rotation_euler=(Vector((0,0,1))-o.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add();cam=bpy.context.object;reviewobj(cam);cam.data.type='ORTHO';cam.data.ortho_scale=2.12;scene.camera=cam
scene.render.engine='CYCLES';scene.cycles.samples=48;scene.render.resolution_x=1000;scene.render.resolution_y=1200;scene.render.resolution_percentage=100;scene.view_settings.view_transform='AgX';scene.view_settings.look='AgX - Medium High Contrast';scene.view_settings.exposure=-.20
# Feet origin on asset; studio objects exist only in REVIEW_ONLY.
rig.location=(0,0,0);rig.rotation_euler=(0,0,0);rig.scale=(1,1,1)
for view,pos in [('front',(0,-5,.88)),('side',(5,0,.88)),('threequarter',(3,-5,1.15))]:
 cam.location=pos;cam.rotation_euler=(Vector((0,0,.88))-cam.location).to_track_quat('-Z','Y').to_euler();scene.render.filepath=str(P/f'Hero_01_{view}.png');bpy.ops.render.render(write_still=True)
bpy.ops.object.select_all(action='DESELECT');rig.select_set(True);bpy.context.view_layer.objects.active=rig
bpy.ops.wm.save_as_mainfile(filepath=str(P/'Hero_01.blend'))
print('HERO_DONE',json.dumps(stats),flush=True)
