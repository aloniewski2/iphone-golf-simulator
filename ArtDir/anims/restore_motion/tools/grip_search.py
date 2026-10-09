import bpy,sys,json,math,itertools
sys.path.insert(0,__import__('os').path.dirname(__file__))
from common import *
from pathlib import Path
import numpy as np
P=Path(__file__).resolve().parents[1]
bpy.ops.wm.open_mainfile(filepath=str(P/'Hero_01_FingerRig.blend'))
rig=bpy.data.objects['Hero_01_Rig'];body=bpy.data.objects['Body_Skin']
for m in body.modifiers:
 if m.type=='MASK':m.show_viewport=False
hg=body.vertex_groups['Hand.R'].index;names={body.vertex_groups[n].index for n in body.vertex_groups.keys() if n.endswith('.R') and (n.startswith(tuple(FINGERS)) or n.startswith('Thumb') or n=='Hand.R')}
idx=[v.index for v in body.data.vertices if any(g.group in names and g.weight>.05 for g in v.groups)]
Minv=(rig.matrix_world@rig.data.bones['Hand.R'].matrix_local).inverted()
def evaluate(p):
 curl(rig,'R',p);bpy.context.view_layer.update()
 dg=bpy.context.evaluated_depsgraph_get();ev=body.evaluated_get(dg);me=ev.to_mesh()
 pts=np.array([tuple(Minv@(body.matrix_world@me.vertices[i].co)) for i in idx]);ev.to_mesh_clear()
 S=socket_matrix(p);o=np.array(S.translation+S.to_3x3()@Vector((0,-.06,0)));a=np.array(S.to_3x3()@Vector((0,1,0)))
 xa=np.array(S.to_3x3()@Vector((1,0,0)));za=np.array(S.to_3x3()@Vector((0,0,1)))
 rel=pts-o;t=rel@a;m=(t>0)&(t<.16);rel=rel[m]-np.outer(t[m],a);d=np.linalg.norm(rel,axis=1)
 phi=np.arctan2(rel@za,rel@xa)
 pen=int((d<.015).sum());near=d<.030
 bins=np.unique(((phi[near]+math.pi)/(2*math.pi)*24).astype(int)) if near.any() else []
 return pen,len(bins)/24,float(np.percentile(d,1)) if len(d) else 1
res=[]
import sys
TILT=float(sys.argv[-1])
for x,y,c1,c2 in itertools.product([.015,.03,.045],[.08,.095,.11,.125],[60,75,90],[70,85,100]):
 p={'c1':c1,'c2':c2,'t1':0,'t2':0,'tilt':TILT,'pos':[x,y,-.01],'spread':{'Index':-15,'Middle':-5,'Pinky':10},'grip_y':.02}
 pen,cov,dmin=evaluate(p);res.append((pen,cov,dmin,x,y,c1,c2))
res.sort(key=lambda r:(r[0]>25,-r[1],r[0]))
for r in res[:15]:print('RES',r)
json.dump(res,open(P/'grip_iter/search.json','w'))
