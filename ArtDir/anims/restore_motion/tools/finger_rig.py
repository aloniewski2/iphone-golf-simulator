# Adds finger bones (4 fingers x 2 phalanges + 2-bone thumb per hand) to the locked V4 knee-weighted body.
# Only Hand.L/Hand.R weight is redistributed; every other vertex weight, the mesh and the wardrobe are untouched.
import bpy,json,math,sys
from mathutils import Vector,Matrix,Quaternion
from pathlib import Path
P=Path(__file__).resolve().parents[1];A=P.parent
bpy.ops.wm.open_mainfile(filepath=str(A/'knees_offarm/Hero_01_Knees_QA.blend'))
rig=bpy.data.objects['Hero_01_Rig'];body=bpy.data.objects['Body_Skin'];rig.animation_data_clear()
for b in rig.pose.bones:b.matrix_basis.identity()
def ss(t):t=max(0,min(1,t));return t*t*(3-2*t)
# Right-hand bone-local layout measured from the mesh (hand_vertices): fingers spread along local Z, palm faces +X, thumb on -Z.
FING={'Index':-.064,'Middle':-.030,'Ring':.004,'Pinky':.037}
KN,MID,TIP=.090,.122,.154
XC=-.010
THUMB=[Vector((.022,.030,-.074)),Vector((.030,.064,-.097)),Vector((.034,.094,-.103))]
def layout():
 out={}
 for f,z in FING.items():
  tip=TIP-(.010 if f=='Pinky' else 0)
  out[f+'1']=(Vector((XC,KN,z)),Vector((XC,MID,z)));out[f+'2']=(Vector((XC,MID,z)),Vector((XC,tip,z)))
 out['Thumb1']=(THUMB[0],THUMB[1]);out['Thumb2']=(THUMB[1],THUMB[2]);return out
L=layout()
bpy.context.view_layer.objects.active=rig;bpy.ops.object.mode_set(mode='EDIT');eb=rig.data.edit_bones
MR=Matrix(((-1,0,0),(0,1,0),(0,0,1)))
for s in 'RL':
 hand=eb['Hand.'+s];Mh=rig.data.bones['Hand.'+s].matrix_local if False else None
bpy.ops.object.mode_set(mode='OBJECT')
HM={s:rig.data.bones['Hand.'+s].matrix_local.copy() for s in 'RL'}
bpy.ops.object.mode_set(mode='EDIT');eb=rig.data.edit_bones
for s in 'RL':
 for n,(h,t) in L.items():
  name=n+'.'+s
  if name in eb:eb.remove(eb[name])
  b=eb.new(name)
  hw=HM['R']@h;tw=HM['R']@t;palm=(HM['R'].to_3x3()@Vector((1,0,0))).normalized()
  if s=='L':hw=MR@hw;tw=MR@tw;palm=MR@palm
  b.head=hw;b.tail=tw;b.align_roll(palm)
  b.parent=eb['Hand.'+s] if n.endswith('1') else eb[n[:-1]+'1.'+s];b.use_connect=False;b.use_deform=True
bpy.ops.object.mode_set(mode='OBJECT')
# Weights: compute the right hand from position, then mirror-copy onto the left by nearest mirrored vertex
from mathutils import kdtree
def split(s,co,w):
 p=HM['R'].inverted()@co
 thumb=ss((-p.z-.080)/.012)*ss((p.y-.010)/.02)*ss((p.x+.005)/.02)
 fy=ss((p.y-(KN-.012))/.022)*(1-thumb)
 g=ss((p.y-(MID-.008))/.016)
 bands={f:math.exp(-((p.z-z)/.013)**2) for f,z in FING.items()};tot=sum(bands.values())
 share={'Hand':(1-fy)*(1-thumb)}
 for f in FING:share[f+'1']=fy*(1-g)*bands[f]/tot;share[f+'2']=fy*g*bands[f]/tot
 tg=ss((p.y-.060)/.02);share['Thumb1']=thumb*(1-tg);share['Thumb2']=thumb*tg
 top=sorted(share.items(),key=lambda k:-k[1])[:3];t=sum(x for _,x in top)
 return {n:x/t for n,x in top if x/t>1e-3}
names=[n for n in L]
hgR=body.vertex_groups['Hand.R'];hgL=body.vertex_groups['Hand.L']
groups={n+'.'+s:(body.vertex_groups.get(n+'.'+s) or body.vertex_groups.new(name=n+'.'+s)) for n in names for s in 'RL'}
right={}
for v in body.data.vertices:
 w=next((g.weight for g in v.groups if g.group==hgR.index),0)
 if w>0:right[v.index]=(w,split('R',v.co.copy(),w))
kd=kdtree.KDTree(len(right))
for i in right:kd.insert(body.data.vertices[i].co,i)
kd.balance()
def apply(v,hg,side,w,fr):
 hg.remove([v.index])
 for n,x in fr.items():(hg if n=='Hand' else groups[n+'.'+side]).add([v.index],w*x,'REPLACE')
for i,(w,fr) in right.items():apply(body.data.vertices[i],hgR,'R',w,fr)
mirrored=0
for v in body.data.vertices:
 w=next((g.weight for g in v.groups if g.group==hgL.index),0)
 if w<=0:continue
 co,j,d=kd.find(MR@v.co);apply(v,hgL,'L',w,right[j][1]);mirrored+=1
print('MIRRORED_LEFT',mirrored)
bpy.ops.wm.save_as_mainfile(filepath=str(P/'Hero_01_FingerRig.blend'))
print('FINGER_RIG_DONE',len(rig.data.bones))
