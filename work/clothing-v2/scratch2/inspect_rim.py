import bpy, numpy as np
ob=[o for o in bpy.data.objects if o.name.startswith('Kit_Shoe')][0]
me=ob.data; n=len(me.vertices); p=np.zeros(n*3); me.vertices.foreach_get('co',p); p=p.reshape(-1,3)
panel=np.zeros(len(me.polygons),np.int32); me.attributes['panel'].data.foreach_get('value',panel)
# vertices of panel 2 (roll)
vs=set()
for poly,pa in zip(me.polygons,panel):
    if pa==2: vs.update(poly.vertices)
vs=sorted(vs); q=p[vs]
print('roll verts',len(q),'z range',q[:,2].min(),q[:,2].max())
# group by y
order=np.argsort(q[:,1])
for y0 in np.linspace(q[:,1].min(),q[:,1].max(),14):
    m=np.abs(q[:,1]-y0)<.012
    if m.sum(): print('y %.3f  roll z %.3f..%.3f  x %.3f..%.3f'%(y0,q[m,2].min(),q[m,2].max(),q[m,0].min(),q[m,0].max()))
up=[v for poly,pa in zip(me.polygons,panel) if pa==0 for v in poly.vertices]; u=p[sorted(set(up))]
for y0 in np.linspace(u[:,1].min(),u[:,1].max(),14):
    m=np.abs(u[:,1]-y0)<.012
    print('y %.3f  upper z max %.3f  x %.3f..%.3f'%(y0,u[m,2].max(),u[m,0].min(),u[m,0].max()))
