import bpy, numpy as np
ob=[o for o in bpy.data.objects if o.type=='MESH'][0]
me=ob.data; n=len(me.vertices); p=np.zeros(n*3); me.vertices.foreach_get('co',p); p=p.reshape(-1,3)
print('POLOZ min %.4f max %.4f n %d'%(p[:,2].min(),p[:,2].max(),n))
low=p[:,2]<.975
print('LOW', low.sum())
for r in p[low][:20]: print('LOWV', r.round(4))
idx=set(np.nonzero(p[:,2]<.9)[0])
print('ATTRS', list(me.attributes.keys())[:30])
for i,pl in enumerate(me.polygons):
    if idx & set(pl.vertices):
        info={k: int(me.attributes[k].data[i].value) for k in ('panel','face_kind','role','fabric') if k in me.attributes}
        print('FACE', i, info, [tuple(p[v].round(3)) for v in pl.vertices][:4])
        break
# neighbours: z of vertices adjacent
vs=list(idx)[:1]
near=np.argsort(np.linalg.norm(p-p[vs[0]],axis=1))[:12]
for j in near: print('NEAR', j, p[j].round(4))
