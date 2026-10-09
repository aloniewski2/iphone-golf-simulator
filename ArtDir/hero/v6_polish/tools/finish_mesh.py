import bpy,bmesh,math,json
from pathlib import Path
from mathutils import Vector,Matrix
R=Path(__file__).resolve().parents[4];O=R/'ArtDir/hero/v6_polish';bpy.ops.wm.open_mainfile(filepath=str(O/'Hero_V6_Polish.blend'));rig=bpy.data.objects['Hero_01_Rig']
def ss(a,b,x):
 t=max(0,min(1,(x-a)/(b-a)));return t*t*(3-2*t)
def material(n,c):
 m=bpy.data.materials.new(n);m.use_nodes=True;b=m.node_tree.nodes.get('Principled BSDF');b.inputs['Base Color'].default_value=c;b.inputs['Roughness'].default_value=.68;return m
navy=material('Hero_V6_ShortsNavy',(.016,.031,.071,1));orange=bpy.data.materials['Hero_V6_Orange']
vs=[];fs=[];ws=[];matids=[];N=40
for side,sign in [('L',1),('R',-1)]:
 start=len(vs);zs=[.514,.520,.535,.56,.59,.62,.65,.68,.71,.75,.79,.825]
 for j,z in enumerate(zs):
  cx=sign*(.147-(z-.52)*.10);rx=.105+.015*ss(.56,.68,z)-.007*ss(.74,.825,z);ry=.108+.022*ss(.55,.73,z);cy=.052
  for i in range(N):
   t=2*math.pi*i/N;x=cx+rx*math.cos(t);y=cy+ry*math.sin(t)
   # The inner seam opens below crotch and meets at centre above it.
   if sign*x<.002 and z>.66:x=sign*.001
   y+=.0015*math.sin(t*6)*ss(.52,.56,z)*(1-ss(.73,.82,z))
   vs.append((x,y,z));h=ss(.62,.755,z);ws.append({'UpperLeg.'+side:1-h,'Hips':h})
 for j in range(len(zs)-1):
  for i in range(N):a=start+j*N+i;b=start+j*N+(i+1)%N;fs.append((a,b,b+N,a+N));matids.append(1 if j==0 else 0)
 # Return cuff inward rather than cap across leg.
 cuff=len(vs)
 for i in range(N):
  p=Vector(vs[start+i]);c=Vector((sign*.147,.052,.514));q=c+(p-c)*.96;q.z=.525;vs.append(tuple(q));ws.append({'UpperLeg.'+side:1})
 for i in range(N):fs.append((start+(i+1)%N,start+i,cuff+i,cuff+(i+1)%N));matids.append(0)
 fs.append(tuple(start+(len(zs)-1)*N+i for i in range(N)));matids.append(0)
old=bpy.data.objects['Shorts_Default'];bpy.data.objects.remove(old,do_unlink=True)
if bpy.data.objects.get('Shorts_Piping'):bpy.data.objects.remove(bpy.data.objects['Shorts_Piping'],do_unlink=True)
me=bpy.data.meshes.new('Hero_V6_Shorts_QuadLoops');me.from_pydata(vs,[],fs);me.update();ob=bpy.data.objects.new('Shorts_Default',me);bpy.context.collection.objects.link(ob);ob.parent=rig;me.materials.append(navy);me.materials.append(orange)
for p,m in zip(me.polygons,matids):p.use_smooth=True;p.material_index=m
for i,w in enumerate(ws):
 for n,v in w.items():
  if v>0:(ob.vertex_groups.get(n) or ob.vertex_groups.new(name=n)).add([i],v,'REPLACE')
a=ob.modifiers.new('Shared Humanoid','ARMATURE');a.object=rig
# Sculpt short side/pocket accents into a separate named Bottom piece, not body texture.
pv=[];pf=[];pw=[]
for side,sign in [('L',1),('R',-1)]:
 path=[]
 for k in range(25):
  z=.53+k/24*.285;cx=.147-(z-.52)*.1;rx=.105+.015*ss(.56,.68,z)-.007*ss(.74,.825,z)
  path.append(Vector((sign*(cx+rx+.0015),.043-.045*ss(.66,.75,z),z)))
 base=len(pv)
 for k,p in enumerate(path):
  t=(path[min(k+1,24)]-path[max(0,k-1)]).normalized();u=Vector((0,1,0));v=t.cross(u).normalized()
  for j in range(8):pv.append(tuple(p+u*(.003*math.cos(j*math.pi/4))+v*(.003*math.sin(j*math.pi/4))));h=ss(.62,.755,p.z);pw.append({'UpperLeg.'+side:1-h,'Hips':h})
 for k in range(24):
  for j in range(8):pf.append((base+k*8+j,base+k*8+(j+1)%8,base+(k+1)*8+(j+1)%8,base+(k+1)*8+j))
pm=bpy.data.meshes.new('Hero_V6_ShortsSidePiping');pm.from_pydata(pv,[],pf);po=bpy.data.objects.new('Shorts_Piping',pm);bpy.context.collection.objects.link(po);po.parent=rig;pm.materials.append(orange)
for p in pm.polygons:p.use_smooth=True
for i,w in enumerate(pw):
 for n,v in w.items():
  if v>0:(po.vertex_groups.get(n) or po.vertex_groups.new(name=n)).add([i],v,'REPLACE')
am=po.modifiers.new('Shared Humanoid','ARMATURE');am.object=rig
# Clear stale custom normals without modifying facial positions.
for ob in bpy.data.objects:
 if ob.type!='MESH':continue
 for p in ob.data.polygons:p.use_smooth=True
 bpy.context.view_layer.objects.active=ob
 if ob.data.has_custom_normals:
  try:bpy.ops.mesh.customdata_custom_splitnormals_clear()
  except:pass
# Unwrap newly built garments.
for ob in [bpy.data.objects['Shorts_Default'],po]:
 bpy.ops.object.select_all(action='DESELECT');ob.select_set(True);bpy.context.view_layer.objects.active=ob;bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.uv.smart_project(angle_limit=math.radians(65),island_margin=.015);bpy.ops.object.mode_set(mode='OBJECT')
# Author bounded size targets on every affected body and garment surface, no skeleton scaling.
for name in ['Body_Skin','Body_Legs','Body_Legs_Full','Shirt_Default','Shorts_Default','Shorts_Piping']:
 ob=bpy.data.objects[name]
 if not ob.data.shape_keys:ob.shape_key_add(name='Basis')
 for label,amount in [('BuildSlim',-.12),('BuildBroad',.12)]:
  key=ob.shape_key_add(name=label,from_mix=False)
  for v,kv in zip(ob.data.shape_keys.key_blocks[0].data,key.data):
   p=ob.matrix_world@v.co;x,y,z=p
   if .23<z<.70:
    side='L' if x>0 else 'R';u=rig.data.bones['UpperLeg.'+side];l=rig.data.bones['LowerLeg.'+side];a,b=(u.head_local,l.head_local) if z>=.44 else (l.head_local,l.tail_local);t=max(0,min(1,(z-a.z)/(b.z-a.z)));c=a.lerp(b,t);f=amount*ss(.23,.28,z)*(1-ss(.62,.70,z));p.x+=(x-c.x)*f;p.y+=(y-c.y)*f
   elif .70<=z<1.16:
    f=amount*ss(.70,.80,z)*(1-ss(1.05,1.16,z))*(1-ss(.18,.28,abs(x)));p.x+=x*f;p.y+=(y-.055)*f
   kv.co=ob.matrix_world.inverted()@p
# Explicit slot anchors; the serialized Unity slots are preserved separately on prefab duplication.
for name,parent,pos in [('Hair','Head',(0,.04,1.61)),('Visor','Head',(0,.04,1.57)),('Top','Chest',(0,.055,1.04)),('Bottom','Hips',(0,.055,.75)),('Shoes','Root',(0,0,.1)),('Racket','Hand.R',(-.49,.03,.76))]:
 if bpy.data.objects.get(name):continue
 e=bpy.data.objects.new(name,None);bpy.context.collection.objects.link(e);e.parent=rig;e.parent_type='BONE';e.parent_bone=parent;e.matrix_world=Matrix.Translation(pos)
bpy.ops.wm.save_as_mainfile(filepath=str(O/'Hero_V6_Polish.blend'));print('V6_FINISH_MESH')
