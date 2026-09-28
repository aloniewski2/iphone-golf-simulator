import bpy,json,math,bmesh
from mathutils.kdtree import KDTree
from pathlib import Path
from mathutils import Vector
P=Path(__file__).resolve().parent
bpy.ops.wm.open_mainfile(filepath=str(P/'Hero_01_Mixamo_QA.blend'))
rig=bpy.data.objects['Hero_01_Rig'];bn={b.name for b in rig.data.bones}
def smooth(a,b,x):
 t=max(0,min(1,(x-a)/(b-a)));return t*t*(3-2*t)
def mix(a,b,t):
 d={k:v*(1-t) for k,v in a.items()}
 for k,v in b.items():d[k]=d.get(k,0)+v*t
 return d
def weights(v,forcearm=False):
 x,y,z=v;side='L' if x>=0 else 'R';x=abs(x)
 torso=mix({'Hips':1},{'Spine':1},smooth(.78,.99,z));torso=mix(torso,{'Chest':1},smooth(.97,1.14,z));torso=mix(torso,{'Neck':1},smooth(1.16,1.255,z));torso=mix(torso,{'Head':1},smooth(1.25,1.32,z))
 arm=mix({'UpperArm.'+side:1},{'LowerArm.'+side:1},1-smooth(.91,1.025,z));arm=mix(arm,{'Hand.'+side:1},1-smooth(.735,.825,z))
 if forcearm:return arm
 arm=mix({'Shoulder.'+side:1},arm,smooth(.17,.29,x))
 armfrac=smooth(.15,.29,x)*(1-smooth(1.205,1.30,z))*smooth(.49,.60,z)
 # The flank below the sleeve is torso; the detached arm is further out.
 armfrac*=smooth(.70-.50*z,.755-.50*z,x)
 upper=mix(torso,arm,armfrac)
 leg=mix({'UpperLeg.'+side:1},{'LowerLeg.'+side:1},1-smooth(.385,.495,z))
 foot=mix({'Foot.'+side:1},{'Toes.'+side:1},1-smooth(-.18,-.12,y))
 leg=mix(leg,foot,1-smooth(.13,.25,z))
 lower=mix(leg,{'Hips':1},smooth(.64,.84,z))
 return mix(mix(lower,torso,smooth(.73,.88,z)),arm,armfrac)
# Identify the exposed arms by connected topology, not just proximity to the hips.
o=bpy.data.objects['Body_Skin'];bm=bmesh.new();bm.from_mesh(o.data);remain=set(bm.verts);armcoords=[]
while remain:
 stack=[remain.pop()];vs=[]
 while stack:
  v=stack.pop();vs.append(v)
  for e in v.link_edges:
   u=e.other_vert(v)
   if u in remain:remain.remove(u);stack.append(u)
 if len(vs)>500 and abs(sum(v.co.x for v in vs)/len(vs))>.35:armcoords.extend(v.co.copy() for v in vs)
bm.free();kd=KDTree(len(armcoords))
for i,v in enumerate(armcoords):kd.insert(v,i)
kd.balance()
changed={}
for o in bpy.data.collections['Hero_01'].objects:
 if o.type!='MESH':continue
 for g in list(o.vertex_groups):
  if g.name in bn:o.vertex_groups.remove(g)
 gs={n:o.vertex_groups.new(name=n) for n in bn}
 for v in o.data.vertices:
  w={'Head':1} if o.name.startswith(('Hair_','Hat_','Body_Eye')) else weights(v.co)
  if not o.name.startswith(('Hair_','Hat_','Body_Eye')):
   co,idx,dist=kd.find(v.co)
   if dist<.035:w=mix(w,weights(v.co,True),1-smooth(.004,.035,dist))
  w=dict(sorted(((k,x) for k,x in w.items() if x>.002),key=lambda kv:-kv[1])[:4]);s=sum(w.values())
  for n,x in w.items():gs[n].add([v.index],x/s,'REPLACE')
 for m in o.modifiers:
  if m.type=='ARMATURE':m.use_deform_preserve_volume=False
 changed[o.name]=len(o.data.vertices)
# Lock duplicate vertices at slot boundaries to identical weights.
from collections import defaultdict
shared=defaultdict(list)
for o in bpy.data.collections['Hero_01'].objects:
 if o.type=='MESH':
  for v in o.data.vertices:shared[tuple(round(float(c),5) for c in v.co)].append((o,v))
for entries in shared.values():
 if len({o.name for o,v in entries})<2:continue
 o,v=entries[0];w={o.vertex_groups[g.group].name:g.weight for g in v.groups if o.vertex_groups[g.group].name in bn}
 if any(ob.name.startswith(('Hair_','Hat_','Body_Eye')) for ob,ve in entries):w={'Head':1}
 for ob,ve in entries:
  for g in ob.vertex_groups:
   if g.name in bn:g.remove([ve.index])
  for n,val in w.items():ob.vertex_groups[n].add([ve.index],val,'REPLACE')
scene=bpy.context.scene;scene.cycles.samples=16;scene.render.resolution_percentage=50
for name in ['Idle','Ready']:
 a=bpy.data.actions[name];rig.animation_data.action=a
 for idx,f in enumerate([int(a.frame_range[0]),int(sum(a.frame_range)/2)]):
  scene.frame_set(f);scene.render.filepath=str(P/f'qa_{name}_{idx}.png');bpy.ops.render.render(write_still=True)
rig.animation_data.action=bpy.data.actions['Idle'];scene.frame_set(1)
bpy.ops.wm.save_as_mainfile(filepath=str(P/'Hero_01_Mixamo_QA.blend'));print('WEIGHTS_FIXED',changed)
