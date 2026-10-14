import bpy, numpy as np
ob=[o for o in bpy.data.objects if o.name.startswith('Kit_Shoe')][0]
me=ob.data; n=len(me.vertices); p=np.zeros(n*3); me.vertices.foreach_get('co',p); p=p.reshape(-1,3)
panel=np.zeros(len(me.polygons),np.int32); me.attributes['panel'].data.foreach_get('value',panel)
for pa in sorted(set(panel)):
    vs=sorted({v for pl,q in zip(me.polygons,panel) if q==pa for v in pl.vertices}); q=p[vs]
    print('PANEL',pa,'nv',len(q),'ymin %.4f ymax %.4f zmin %.4f xrange %.3f..%.3f'%(q[:,1].min(),q[:,1].max(),q[:,2].min(),q[:,0].min(),q[:,0].max()))
ymin=p[:,1].min(); print('YMIN',ymin)
m=p[:,1]<ymin+.004
print('verts near toe tip', m.sum())
for i in np.nonzero(m)[0][:12]: print(i, p[i].round(4))
vs=sorted({v for pl,q in zip(me.polygons,panel) if q==0 for v in pl.vertices}); q=p[vs]
m=q[:,2]<-.004
print('LOW upper verts',m.sum()); 
for r in q[m][:15]: print('LOW',r.round(4))
print('LOW ymin', q[m][:,1].min() if m.sum() else None)
m=q[:,2]<-.015
print('VLOW',m.sum())
for r in q[m][:20]: print('VLOW',r.round(4))
