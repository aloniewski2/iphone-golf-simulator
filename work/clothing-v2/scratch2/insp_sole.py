import bpy, numpy as np
ob=[o for o in bpy.data.objects if o.name.startswith('Kit_Shoe')][0]; me=ob.data
n=len(me.vertices); p=np.zeros(n*3); me.vertices.foreach_get('co',p); p=p.reshape(-1,3)
panel=np.zeros(len(me.polygons),np.int32); me.attributes['panel'].data.foreach_get('value',panel)
sel=[pl for pl,q in zip(me.polygons,panel) if q==5]
cen=p[sorted({v for pl in sel for v in pl.vertices})].mean(0)
out=0; inn=0; side_out=0; side_tot=0
for pl in sel:
    c=np.array(pl.center); nrm=np.array(pl.normal)
    d=np.dot(nrm,(c-cen)*np.array([1,1,0]))   # horizontal outwardness
    wall=abs(nrm[2])<.6
    if wall:
        side_tot+=1; side_out+= d>0
print('SOLE faces',len(sel),'wall faces',side_tot,'wall facing OUT',side_out)
# bottom faces: normal z should be negative
bot=[pl for pl in sel if pl.center[2]<-.02 and abs(pl.normal[2])>.7]
print('bottom faces',len(bot),'facing down',sum(1 for pl in bot if pl.normal[2]<0))
