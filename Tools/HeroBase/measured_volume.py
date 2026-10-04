"""Direct analytic surface construction from plate sections, never from an input mesh.
Surface nets keep quad connectivity. No decimation, input-mesh remesh or face smoothing.
"""
import numpy as np,math

def append_volume(g,pix,pr,interp,female,step=4.7):
    xs=np.arange(-265 if not female else -110,270 if not female else 115,step,dtype=np.float32)
    ds=np.arange(-83,83,step,dtype=np.float32)
    prior_origin=pr['torso'][0][0]-10
    y0=prior_origin-math.ceil((prior_origin-(pr['neck'][0][0]-6))/step)*step
    ys=np.arange(y0,pr['sole']+12,step,dtype=np.float32)
    X=xs[:,None,None];Z=ds[None,:,None];Y=ys[None,None,:]
    shape=(len(xs),len(ds),len(ys));field=np.full(shape,1e4,dtype=np.float32)
    def smoothunion(a,b,k=3.5):
        h=np.maximum(k-np.abs(a-b),0)/k
        return np.minimum(a,b)-h*h*k*.25
    def curve(rows):
        out=np.array([interp(rows,float(y)) for y in ys],dtype=np.float32)
        return [out[:,j][None,None,:] for j in range(out.shape[1])]
    def add(a,k=3.5):
        nonlocal field;field=smoothunion(field,a,k)
    tr=list(pr['torso'][:-1])
    tr=[(pr['torso'][0][0]-3,1,1)]+tr+([(374,65,42),(384,20,24),(388,1,1)] if not female else [(359,58,40),(366,16,24),(369,1,1)])
    w,d=curve(tr)
    # Continuous chest and abdomen shaping lives on the body surface, with no glued-on muscle pieces.
    detail=np.zeros(shape,dtype=np.float32)
    if not female:
        detail=4.5*np.exp(-((np.abs(X)-31)/26)**2-((Y-225)/24)**2)-2*np.exp(-(X/5)**2-((Y-230)/34)**2)+1.4*np.exp(-((np.abs(X)-18)/14)**2-((Y-288)/32)**2)
    else:detail=2.6*np.exp(-((np.abs(X)-23)/20)**2-((Y-211)/27)**2)
    depth=d+np.where(Z<0,detail,0)
    a=(np.sqrt((X/np.maximum(w,1))**2+(Z/np.maximum(depth,1))**2)-1)*np.minimum(w,depth)
    a=np.maximum(a,np.maximum(tr[0][0]-Y,Y-tr[-1][0]));add(a,0.01)
    # Neck and shoulders are one authored continuous field, with the measured
    # neck entering behind the jaw. There is no overlapping neck sleeve seam.
    nr=[(pr['neck'][0][0]-4,.5,.5)]+list(pr['neck'])
    nw,nd=curve(nr);nt=np.clip((Y-pr['neck'][0][0])/(pr['neck'][-1][0]-pr['neck'][0][0]),0,1)
    nc=6*(1-.6*nt)
    a=(np.sqrt((X/np.maximum(nw,1))**2+((Z-nc)/np.maximum(nd,1))**2)-1)*np.minimum(nw,nd)
    a=np.maximum(a,np.maximum(nr[0][0]-Y,Y-nr[-1][0]));add(a,4)
    for sign in (-1,1):
        lr=list(pr['leg']);lr=[(lr[0][0]-30,lr[0][1],1,1),(lr[0][0]-15,lr[0][1],lr[0][2]*.62,25)]+lr
        cx,w,d=curve(lr);dc=np.maximum(0,(Y-(pr['sole']-40))/40)*14
        a=(np.sqrt(((X-sign*cx)/np.maximum(w,1))**2+((Z+dc)/np.maximum(d,1))**2)-1)*np.minimum(w,d)
        a=np.maximum(a,np.maximum(lr[0][0]-Y,Y-pr['sole']));add(a,4)
    def path(points,widths,depths,k=3.0):
        rows=[(i,*point,widths[i],depths[i]) for i,point in enumerate(points)]
        sampled=[interp(rows,t) for t in np.linspace(0,len(rows)-1,36)]
        points=[r[:3] for r in sampled];widths=[r[3] for r in sampled];depths=[r[4] for r in sampled]
        pathfield=np.full(shape,1e4,dtype=np.float32)
        for i in range(len(points)-1):
            a=np.array(points[i]);b=np.array(points[i+1]);delta=b-a;l=np.linalg.norm(delta);v=delta/l
            # points are X,Y,depth. Cross-section normal is in the XY plane.
            dx=X-a[0];dy=Y-a[1];dz=Z-a[2];t0=(dx*v[0]+dy*v[1]+dz*v[2])/l;t=np.clip(t0,0,1)
            px=dx-delta[0]*t;py=dy-delta[1]*t;pz=dz-delta[2]*t
            w=widths[i]+t*(widths[i+1]-widths[i]);d=depths[i]+t*(depths[i+1]-depths[i]);val=(np.sqrt((px*px+py*py)/(w*w)+(pz*pz)/(d*d))-1)*np.minimum(w,d)
            pathfield=np.minimum(pathfield,val)
        add(pathfield,k)
    for sign in (-1,1):
        rows=pr['arm_sections'];cx,w,d=curve(rows)
        a=(np.sqrt(((X-sign*cx)/np.maximum(w,1))**2+(Z/np.maximum(d,1))**2)-1)*np.minimum(w,d)
        a=np.maximum(a,np.maximum(rows[0][0]-Y,Y-rows[-1][0]));add(a,2.5)
    # exact ground plane; the source is authored in metres via pix().
    field=np.maximum(field,Y-pr['sole'])
    corner_offsets=[(0,0,0),(1,0,0),(0,1,0),(1,1,0),(0,0,1),(1,0,1),(0,1,1),(1,1,1)]
    edges=[(0,1),(2,3),(4,5),(6,7),(0,2),(1,3),(4,6),(5,7),(0,4),(1,5),(2,6),(3,7)]
    vals=[field[a:a+len(xs)-1,b:b+len(ds)-1,c:c+len(ys)-1] for a,b,c in corner_offsets]
    mixed=np.logical_and(np.minimum.reduce(vals)<0,np.maximum.reduce(vals)>=0);ii,jj,kk=np.nonzero(mixed)
    cell=np.full(mixed.shape,-1,dtype=np.int32);cell[ii,jj,kk]=np.arange(len(ii))
    points=np.zeros((len(ii),3),dtype=np.float64);count=np.zeros(len(ii),dtype=np.int32)
    for a,b in edges:
        va=vals[a][ii,jj,kk];vb=vals[b][ii,jj,kk];cross=(va<0)!=(vb<0);t=va/np.where(abs(va-vb)>1e-8,va-vb,1)
        aa=np.array(corner_offsets[a]);bb=np.array(corner_offsets[b]);pt=aa[None,:]+t[:,None]*(bb-aa)[None,:];points+=pt*cross[:,None];count+=cross
    points/=np.maximum(count,1)[:,None];points+=np.stack((ii,jj,kk),axis=1)
    points=points*step+np.array([xs[0],ds[0],ys[0]])
    original_points=points.copy()
    base=len(g.v)
    for x,depth,y in points:g.vert(pix(float(x),float(y),float(depth)))
    # Analytic field normals avoid visible cell facets without moving the silhouette.
    gradient=np.gradient(field,step)
    gridcoords=(original_points-np.array([xs[0],ds[0],ys[0]]))/step
    lo=np.floor(gridcoords).astype(int);fr=gridcoords-lo;norm=np.zeros_like(points)
    for a,b,c in corner_offsets:
        wt=(fr[:,0] if a else 1-fr[:,0])*(fr[:,1] if b else 1-fr[:,1])*(fr[:,2] if c else 1-fr[:,2])
        idx=lo+np.array([a,b,c]);idx=np.minimum(np.maximum(idx,0),np.array(field.shape)-1)
        for ax in range(3):norm[:,ax]+=gradient[ax][idx[:,0],idx[:,1],idx[:,2]]*wt
    norm/=np.maximum(np.linalg.norm(norm,axis=1),1e-9)[:,None]
    if not hasattr(g,'custom_normals'):g.custom_normals={}
    for i,n in enumerate(norm):g.custom_normals[base+i]=(float(n[0]),float(n[1]),float(-n[2]))
    def face(corners):
        if min(corners)<0:return
        ids=tuple(base+int(c) for c in corners);y=sum(points[c,2] for c in corners)/4;x=sum(points[c,0] for c in corners)/4;z=sum(points[c,1] for c in corners)/4
        skin=(female and (y>=620 or y<139)) or (not female and y<182)
        g.face(ids,1 if skin else 0)
    flips=(field<0)
    for i,j,k in zip(*np.nonzero(flips[:-1,1:-1,1:-1]^flips[1:,1:-1,1:-1])):
        j+=1;k+=1;face([cell[i,j-1,k-1],cell[i,j,k-1],cell[i,j,k],cell[i,j-1,k]])
    for i,j,k in zip(*np.nonzero(flips[1:-1,:-1,1:-1]^flips[1:-1,1:,1:-1])):
        i+=1;k+=1;face([cell[i-1,j,k-1],cell[i-1,j,k],cell[i,j,k],cell[i,j,k-1]])
    for i,j,k in zip(*np.nonzero(flips[1:-1,1:-1,:-1]^flips[1:-1,1:-1,1:])):
        i+=1;j+=1;face([cell[i-1,j-1,k],cell[i,j-1,k],cell[i,j,k],cell[i-1,j,k]])
    print('MEASURED_VOLUME',len(points),'vertices',len(g.f),'quads','grid',step)
