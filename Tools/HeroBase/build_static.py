"""Plate-constrained static HeroBase sources. No archived character geometry, rig or outfit."""
import bpy, math, sys, json, hashlib
from pathlib import Path
from mathutils import Vector
sys.path.insert(0,str(Path(__file__).parent))
from plate_profiles import MALE,FEMALE
R=Path(__file__).resolve().parents[2]; D=R/'ArtDir/hero/base_lock'; P=D/'proof'
for p in [D/'blender',D/'unity_import',P/'blender',P/'masks']:p.mkdir(parents=True,exist_ok=True)
TAU=math.tau

def interp(rows,t):
    # Shape-preserving cubic Hermite, with monotone slopes (no unconstrained subdivision).
    if t<=rows[0][0]:return rows[0][1:]
    if t>=rows[-1][0]:return rows[-1][1:]
    for i in range(len(rows)-1):
        a,b=rows[i:i+2]
        if a[0]<=t<=b[0]:break
    u=(t-a[0])/(b[0]-a[0]);out=[]
    for j in range(1,len(a)):
        d=(b[j]-a[j])/(b[0]-a[0])
        dl=(a[j]-rows[i-1][j])/(a[0]-rows[i-1][0]) if i else d
        dr=(rows[i+2][j]-b[j])/(rows[i+2][0]-b[0]) if i+2<len(rows) else d
        m0=0 if dl*d<=0 else 2*dl*d/(dl+d)
        m1=0 if dr*d<=0 else 2*dr*d/(dr+d)
        out.append((2*u**3-3*u*u+1)*a[j]+(u**3-2*u*u+u)*(b[0]-a[0])*m0+(-2*u**3+3*u*u)*b[j]+(u**3-u*u)*(b[0]-a[0])*m1)
    return out

def linrgb(c):return tuple(((v+.055)/1.055)**2.4 if v>.04045 else v/12.92 for v in c)
def material(name,c,rough=.62):
    m=bpy.data.materials.new(name);m.diffuse_color=(*linrgb(c),1);m.use_nodes=True
    n=m.node_tree.nodes.get('Principled BSDF');n.inputs['Base Color'].default_value=m.diffuse_color;n.inputs['Roughness'].default_value=rough;n.inputs['Metallic'].default_value=0
    return m

class Geo:
    def __init__(self,name,mat):self.name=name;self.v=[];self.f=[];self.mi=[];self.mat=mat
    def vert(self,p):self.v.append(tuple(p));return len(self.v)-1
    def face(self,ids,mi=0):self.f.append(ids);self.mi.append(mi)
    def bridge(self,a,b,mi=0):
        for j in range(len(a)):self.face((a[j],a[(j+1)%len(a)],b[(j+1)%len(a)],b[j]),mi)
    def cap(self,ring,mi=0):
        c=self.vert(sum((Vector(self.v[i]) for i in ring),Vector())/len(ring))
        for j in range(len(ring)):self.face((ring[j],ring[(j+1)%len(ring)],c),mi)
    def make(self):
        me=bpy.data.meshes.new(self.name);me.from_pydata(self.v,[],self.f);me.update()
        o=bpy.data.objects.new(self.name,me);bpy.context.collection.objects.link(o)
        for m in self.mat:me.materials.append(m)
        for f,i in zip(me.polygons,self.mi):f.material_index=i;f.use_smooth=True
        import bmesh
        bm=bmesh.new();bm.from_mesh(me);bmesh.ops.recalc_face_normals(bm,faces=bm.faces);bm.to_mesh(me);bm.free()
        me.update()
        if hasattr(self,'custom_normals'):
            normals=[tuple(self.custom_normals.get(i,me.vertices[i].normal)) for i in range(len(me.vertices))]
            me.normals_split_custom_set_from_vertices(normals)
        return o

def uv(o):
    bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o
    bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.uv.smart_project(angle_limit=math.radians(65),island_margin=.015);bpy.ops.object.mode_set(mode='OBJECT')

def ellipsoid(g,c,r,mi=0,n=32,k=18):
    n=max(12,round(n*.70));k=max(8,round(k*.70));c=Vector(c);rings=[]
    for j in range(1,k):
        ph=math.pi*j/k;ring=[]
        for i in range(n):
            th=TAU*i/n;ring.append(g.vert(c+Vector((r[0]*math.sin(ph)*math.sin(th),-r[1]*math.sin(ph)*math.cos(th),r[2]*math.cos(ph)))))
        rings.append(ring)
    for a,b in zip(rings,rings[1:]):g.bridge(a,b,mi)
    for ri,z in [(rings[0],r[2]),(rings[-1],-r[2])]:
        ix=g.vert(c+Vector((0,0,z)))
        for i in range(n):g.face((ri[i],ri[(i+1)%n],ix),mi)

def tube(g,pts,widths,depths,mi=0,n=16,steps=36,cap_start=True):
    # Catmull-free monotone coordinates; pointed sculpted volumes, not cards.
    n=max(8,round(n*.64));steps=max(8,round(steps*.58));rows=[(i,*p,widths[i],depths[i]) for i,p in enumerate(pts)];rings=[]
    for j in range(steps+1):
        t=(len(pts)-1)*j/steps;v=interp(rows,t);c=Vector(v[:3]);lo=Vector(interp(rows,max(0,t-.01))[:3]);hi=Vector(interp(rows,min(len(pts)-1,t+.01))[:3]);axis=(hi-lo).normalized()
        side=axis.cross(Vector((0,1,0))).normalized()
        if side.length<.01:side=Vector((1,0,0))
        dep=side.cross(axis).normalized();ring=[]
        for i in range(n):
            th=TAU*i/n;ring.append(g.vert(c+side*(v[3]*math.cos(th))+dep*(v[4]*math.sin(th))))
        rings.append(ring)
    for a,b in zip(rings,rings[1:]):g.bridge(a,b,mi)
    if cap_start:g.cap(rings[0],mi)
    g.cap(rings[-1],mi)
    return rings

def build(sex,suffix,profile):
    for obj in list(bpy.data.objects):bpy.data.objects.remove(obj,do_unlink=True)
    for m in list(bpy.data.materials):bpy.data.materials.remove(m)
    scale=1.7/profile['height'];sole=profile['sole'];female=sex=='female'
    pix=lambda x,y,d=0:Vector((x*scale,d*scale,(sole-y)*scale))
    mats=[material('Base_Grey_'+suffix,profile['grey']),material('Skin_'+suffix,profile['skin']),material('Eye_White',(.88,.87,.82),.30),material('Iris_Dark',(.18,.095,.042),.52),material('Pupil',(.006,.006,.005),.40),material('Brow',(.075,.064,.053)),material('Mouth',(.50,.29,.22) if female else (.43,.245,.16)),material('Teeth',(.91,.88,.80),.36),material('Lip_'+suffix,tuple(v*f for v,f in zip(profile['skin'],(.97,.85,.79)))),material('Ear_Shadow_'+suffix,tuple(v*.88 for v in profile['skin']))]
    mats[3].node_tree.nodes['Principled BSDF'].inputs['Specular IOR Level'].default_value=.08; mats[4].node_tree.nodes['Principled BSDF'].inputs['Specular IOR Level'].default_value=.05
    g=Geo('Body_'+suffix,mats);n=48
    from measured_volume import append_volume
    append_volume(g,pix,profile,interp,female,step=5.2 if female else 6.0)
    volume_end=len(g.v)
    # Hands remain part of the grey mannequin. Female feet retain the skin boundary of the supplied plate.
    for sign in (-1,1):
        if female:
            ellipsoid(g,pix(sign*91,381,-1),(10*scale,7*scale,14*scale),0,n=24,k=16)
            for fi in range(4):
                x=sign*(86+fi*3.8);y=391+fi*.3
                tube(g,[pix(x,y,-1),pix(sign*(85+fi*3.2),402-fi*.5,-3),pix(sign*(87+fi*1.8),411-fi*1.1,-1)],[2.5*scale,2.2*scale,.8*scale],[2.7*scale,2.4*scale,.9*scale],0,n=10,steps=12)
            tube(g,[pix(sign*83,378,-5),pix(sign*82,391,-8),pix(sign*85,400,-8)],[3.5*scale,3*scale,1.2*scale],[3.6*scale,3*scale,1.3*scale],0,n=12,steps=14)
            for fi in range(5):ellipsoid(g,pix(sign*(48+(fi-2)*5),sole-6,-43+fi),(3.3*scale,7*scale,3.5*scale),1,n=12,k=10)
        else:
            tube(g,[pix(sign*204,340,-6),pix(sign*201,350,-8),pix(sign*200,363,-7)],[5.8*scale,5.4*scale,1.5*scale],[5.5*scale,5*scale,1.8*scale],0,n=12,steps=16)
    # The neck is part of the continuous measured body surface above.
    # Closed skull loft with continuous cheeks and jaw. The lower jaw translates
    # as a whole; extending only the front hemisphere creates a pinched chin seam.
    hrows=list(reversed(profile['head']));hrows.sort();hr=[];N=96;K=64
    def jawshift(y):
        if not female:return interp(profile['jaw_centres'],y)[0]
        return 5.5*math.exp(-((y-(profile['mouth_y']+15))/16)**2)
    def frontdepth(x,y):
        w,d=interp(hrows,y);ratio=min(1,abs(x)/max(.01,w))
        # Broader facial planes fade smoothly into the round cranium and chin.
        # A constant superellipse makes the entire forehead read as a flat box.
        smooth=lambda u:(lambda t:t*t*(3-2*t))(max(0,min(1,u)))
        facial=smooth((y-(profile['eye_y']-16))/16)*smooth(((profile['mouth_y']+18)-y)/18)
        power=2+(.5 if female else 1.0)*facial
        base=d*max(0,1-ratio**power)**(1/power)+jawshift(y);shape=0
        ex=profile['eye_x'];ey=profile['eye_y'];ny=profile['nose_y']
        for sx in (-1,1):
            shape-=(2.0 if female else 2.8)*math.exp(-((x-sx*ex)/(profile['eye_r']+3))**2-((y-ey)/9)**2)
            shape+=(2.7 if female else 3.4)*math.exp(-((x-sx*(ex+3))/11)**2-((y-(ey+14))/12)**2)
            shape+=(1.2 if female else 1.7)*math.exp(-((x-sx*ex)/10)**2-((y-(profile['brow_y']+3))/4.5)**2)
        # Nose, bridge, philtrum and chin grow from a single head surface.
        shape+=profile['nose_depth']*.55*math.exp(-(x/2.8)**2-((y-(ny-8))/9)**2)
        shape+=profile['nose_depth']*math.exp(-(x/(4.6 if female else 5.2))**2-((y-ny)/3.3)**2)
        for sx in (-1,1):
            shape+=(2.0 if female else 2.8)*math.exp(-((x-sx*(3.5 if female else 4.5))/(2.0 if female else 2.3))**2-((y-(ny+.8))/2.6)**2)
        # The recessed underside separates the nasal tip from the upper lip.
        shape-=(.9 if female else 1.4)*math.exp(-(x/(4.7 if female else 6.0))**4-((y-(ny+3.5))/1.5)**2)
        shape+=.75*math.exp(-(x/13)**2-((y-(profile['mouth_y']+10))/6)**2)
        shape-=.35*math.exp(-(x/1.7)**2-((y-(profile['mouth_y']-4))/3)**2)
        shape+=.8*math.exp(-(x/9)**2-((y-(profile['mouth_y']-1))/2.8)**2)+.7*math.exp(-(x/10)**2-((y-(profile['mouth_y']+2.2))/2.4)**2)
        return base+shape*max(0,1-ratio**2)**.8
    for j in range(K+1):
        face_top=profile['eye_y']-15;face_bottom=profile['mouth_y']+9
        if j<=18:y=hrows[0][0]+(face_top-hrows[0][0])*(.5-.5*math.cos(math.pi*j/18))
        elif j<=56:y=face_top+(face_bottom-face_top)*(j-18)/38
        else:y=face_bottom+(hrows[-1][0]-face_bottom)*(.5-.5*math.cos(math.pi*(j-56)/8))
        w,d=interp(hrows,y);ring=[]
        for i in range(N):
            angle=TAU*i/N;th=angle-.5*math.sin(angle);x=w*math.sin(th);dep=-d*math.cos(th)-jawshift(y)
            if math.cos(th)>0:dep=-frontdepth(x,y)
            actual_y=y
            if not female:
                # Mandible rises towards the hinges below the ears. Its final
                # ring closes across the underside inside the continuous neck,
                # rather than collapsing every angle to a forward-pointing tip.
                lower=max(0,min(1,(y-134)/17.5))**2
                actual_y-=lower*(9*math.sin(th)**2+5*max(0,-math.cos(th)))
                dep+=4*lower*max(0,-math.cos(th))
            ring.append(g.vert(pix(x,actual_y,dep)))
        hr.append(ring)
    for j,(a,b) in enumerate(zip(hr,hr[1:])):
        for i in range(N):
            ni=(i+1)%N;c=sum((Vector(g.v[k]) for k in (a[i],a[ni],b[ni],b[i])),Vector())/4;x=c.x/scale;y=sole-c.z/scale;front=c.y<-.018
            g.face((a[i],a[ni],b[ni],b[i]),1)
    g.cap(hr[0],1);g.cap(hr[-1],1)
    # Closed ear shells with a recessed concha and a raised helix. The recess
    # belongs to the ear surface, rather than a second oval floating above it.
    ex,ey,ew,eh=profile['ear']
    for sign in (-1,1):
        v0=len(g.v);f0=len(g.f)
        ellipsoid(g,pix(sign*ex,ey,-2),(ew*scale,7*scale,eh*scale),1,n=28,k=20)
        for ix in range(v0,len(g.v)):
            v=Vector(g.v[ix]);u=(v.x/scale-sign*ex)/ew;vv=(sole-v.z/scale-ey)/eh;dep=v.y/scale
            if dep<-2:
                v.y+=4.8*math.exp(-(u/.50)**2-(vv/.68)**2)*scale;g.v[ix]=tuple(v)
        for fi in range(f0,len(g.f)):
            c=sum((Vector(g.v[ix]) for ix in g.f[fi]),Vector())/len(g.f[fi]);u=(c.x/scale-sign*ex)/ew;vv=(sole-c.z/scale-ey)/eh
            if c.y/scale<-2 and u*u+vv*vv<.32:g.mi[fi]=9
    # Shallow eye surfaces conform to the skull, instead of protruding eyeball spheres.
    ex=profile['eye_x'];ey=profile['eye_y'];er=profile['eye_r'];ery=profile['eye_ry']
    upper_height=profile['eye_upper'];lower_height=profile['eye_lower']
    def eye_limits(cx,x):
        u=min(1,abs(x-cx)/er);h=max(0,1-u*u)**.65
        mid=ey-.12*(1 if cx>0 else -1)*(x-cx)
        return mid-upper_height*h,mid+lower_height*h
    def eye_depth(cx,x,y):
        mid=ey-.12*(1 if cx>0 else -1)*(x-cx);dy=y-mid
        r=((x-cx)/er)**2+(dy/(upper_height if dy<0 else lower_height))**2
        return -frontdepth(x,y)-2.0*math.sqrt(max(0,1-r))
    def patch(cx,cy,rx,ry,depth_fn,mi,n=32,k=7):
        # Closed relief volume: front surface and skin-embedded backing share the outer edge.
        center=g.vert(pix(cx,cy,depth_fn(cx,cy)));rings=[]
        for j in range(1,k+1):
            rho=j/k;ring=[]
            for i in range(n):
                th=TAU*i/n;x=cx+rho*rx*math.cos(th);s=math.sin(th);mid=ey-.12*(1 if cx>0 else -1)*(x-cx)
                if mi==2:
                    y=mid+rho*(upper_height if s<0 else lower_height)*s*abs(s)**.3
                else:
                    ox=cx+rx*math.cos(th);oy=cy+ry*s-.12*(1 if cx>0 else -1)*(ox-cx)
                    lo,hi=eye_limits(cx,ox);oy=max(lo+.20,min(hi-.20,oy))
                    y=cy+rho*(oy-cy)
                ring.append(g.vert(pix(x,y,depth_fn(x,y))))
            rings.append(ring)
        for i in range(n):g.face((center,rings[0][i],rings[0][(i+1)%n]),mi)
        for a,b in zip(rings,rings[1:]):g.bridge(a,b,mi)
        rear=g.vert(pix(cx,cy,-frontdepth(cx,cy)+1.0))
        for i in range(n):g.face((rings[-1][(i+1)%n],rings[-1][i],rear),mi)
    for sign in (-1,1):
        cx=sign*ex
        patch(cx,ey,er,ery,lambda x,y:eye_depth(cx,x,y)-.16,2)
        patch(cx,ey-.2,profile['iris_x'],profile['iris_y'],lambda x,y:eye_depth(cx,x,y)-.34,3,n=28,k=5)
        patch(cx,ey-.2,profile['pupil_x'],profile['pupil_y'],lambda x,y:eye_depth(cx,x,y)-.46,4,n=24,k=4)
        sx=cx-1.1;sy=ey-1.7;ellipsoid(g,pix(sx,sy,eye_depth(cx,sx,sy)-.62),(.6*scale,.15*scale,.7*scale),2,n=12,k=10)
        for upper in (True,False):
            pts=[];ws=[]
            for i in range(13):
                t=math.pi*i/12;x=cx+er*math.cos(t);y=eye_limits(cx,x)[0 if upper else 1]
                pts.append(pix(x,y,eye_depth(cx,x,y)-.4));ws.append((1.1 if female and upper else .85 if upper else .55)*scale)
            tube(g,pts,ws,[w*.65 for w in ws],5 if upper else 1,n=8,steps=28)
        by=profile['brow_y'];bw=11.5 if female else 10;pts=[];ws=[]
        for i in range(17):
            t=-1+2*i/16;x=cx+bw*t;y=by+1.2-3.1*(1-t*t)
            pts.append(pix(x,y,-frontdepth(x,y)-.9));ws.append((.16+(2.3 if female else 2.8)*math.sin(math.pi*i/16)**.6)*scale)
        tube(g,pts,ws,[.58*w for w in ws],5,n=12,steps=44)
    ny=profile['nose_y']
    for sign in (-1,1):
        x=sign*(5.0 if not female else 3.8);y=ny+2.1
        ellipsoid(g,pix(x,y,-frontdepth(x,y)-.4),(.78*scale,.25*scale,.38*scale),6,n=12,k=8)
    # Both new plates have a gentle closed smile, without a large mouth cavity or teeth bar.
    my=profile['mouth_y'];mw=profile['mouth_w']
    # Soft closed lip volumes, with the coloured seam lifted clear of the loft.
    for upper in (True,False):
        pts=[];ws=[]
        for i in range(25):
            t=i/24;x=-mw+2*mw*t;arch=(2.7 if female else 2.1)*(x/mw)**2;full=math.sin(math.pi*t)**.6
            y=my+(-.8 if upper else 1.2)*full-arch
            pts.append(pix(x,y,-frontdepth(x,y)-.9));ws.append((.12+(.65 if upper else .95)*full)*scale)
        tube(g,pts,ws,[.52*w for w in ws],8,n=10,steps=58)
    pts=[];ws=[]
    for i in range(25):
        t=i/24;x=-mw+2*mw*t;y=my-(2.7 if female else 2.1)*(x/mw)**2
        pts.append(pix(x,y,-frontdepth(x,y)-1.5));ws.append((.14+.45*math.sin(math.pi*t)**.5)*scale)
    tube(g,pts,ws,[w*.6 for w in ws],6,n=8,steps=58)
    body=g.make()
    uv(body)
    atlas=bpy.data.images.load(str(D/'unity_import/Textures'/('Head_'+sex.title()+'_Albedo.png')))
    atlas.colorspace_settings.name='sRGB'
    node=mats[1].node_tree.nodes.new('ShaderNodeTexImage');node.image=atlas;node.extension='EXTEND';mats[1].node_tree.links.new(node.outputs['Color'],mats[1].node_tree.nodes['Principled BSDF'].inputs['Base Color'])
    for poly in body.data.polygons:
        if poly.material_index!=1:continue
        for ix in poly.loop_indices:
            v=body.data.vertices[body.data.loops[ix].vertex_index].co;y=sole-v.z/scale
            vi=body.data.loops[ix].vertex_index
            centre=0
            if vi<volume_end and y<profile['neck'][-1][0]:
                nt=max(0,min(1,(y-profile['neck'][0][0])/(profile['neck'][-1][0]-profile['neck'][0][0])))
                nw=interp(profile['neck'],y)[0]
                centre=6*(1-.6*nt)*math.exp(-(v.x/scale/max(nw,1))**6)*scale
            angle=math.atan2(v.x,-(v.y-centre))
            if not female and vi<volume_end and v.y-centre<0:
                angle=math.asin(max(-1,min(1,v.x/scale/48)))
            body.data.uv_layers.active.data[ix].uv=(.02,.98) if female and y>=620 else ((angle/TAU+.5),max(0,min(1,(182-y)/158)))
        # Keep longitude continuous across the posterior seam. Interpolating
        # directly from U≈0 to U≈1 samples front-face pigment on the back.
        loops=[body.data.uv_layers.active.data[ix] for ix in poly.loop_indices]
        if max(loop.uv.x for loop in loops)-min(loop.uv.x for loop in loops)>.5:
            for loop in loops:
                if loop.uv.x<.5:loop.uv.x+=1
    # Hair cap and opaque sculpted clumps, independent of Body.
    hm=material('Hair_'+suffix,(.045,.038,.032),.64);hm.node_tree.nodes['Principled BSDF'].inputs['Specular IOR Level'].default_value=.20;h=Geo('Hair_'+suffix,[hm])
    if not female:
        N=48;K=12;rings=[]
        for j in range(K+1):
            u=j/K;ring=[]
            for i in range(N):
                th=TAU*i/N;front=max(0,math.cos(th));back=max(0,-math.cos(th));yedge=104-39*front+45*back
                y=30+(yedge-30)*u
                width,depth=interp([(30,10,12),(38,36,30),(50,48,40),(66,53,46),(84,53,47),(102,49,46),(122,45,44),(138,39,36),(149,31,31)],y)
                x=width*math.sin(th);dep=-depth*math.cos(th)+1
                ring.append(h.vert(pix(x,y,dep)))
            rings.append(ring)
        for a,b in zip(rings,rings[1:]):h.bridge(a,b)
        h.cap(rings[0])
        inner=[];center=pix(0,128 if female else 91,3)
        for ring in rings:
            inner.append([h.vert(Vector(h.v[ix])+(center-Vector(h.v[ix])).normalized()*2*scale) for ix in ring])
        for a,b in zip(inner,inner[1:]):h.bridge(list(reversed(a)),list(reversed(b)))
        h.cap(list(reversed(inner[0])));h.bridge(rings[-1],inner[-1])
        # Broad swept quiff leaves follow distinct plate strokes.
        locks=[([(-51,66,-27),(-49,46,-37),(-22,31,-39),(9,43,-43)],[7,10,10,.3]),([(-46,55,-31),(-45,36,-33),(-17,28,-30),(17,48,-42)],[6,9,8,.4]),([(-33,44,-34),(-41,27,-18),(-12,29,-22),(24,55,-42)],[7,9,10,.5]),([(-17,40,-32),(-31,24,-10),(-10,25,-15),(28,51,-38)],[8,10,8,.6]),([(0,41,-28),(-10,22,2),(10,27,-8),(33,59,-35)],[8,7,7,.4]),([(18,48,-25),(15,26,10),(31,32,0),(36,62,-34)],[7,8,7,.4]),([(32,56,-22),(30,32,18),(42,45,1),(40,67,-27)],[7,6,6,.4]),([(43,65,-17),(47,49,14),(49,62,4),(49,81,-13)],[6,6,6,.4]),([(-54,80,-12),(-53,60,4),(-38,50,-19),(-8,63,-42)],[4,5,6,.4]),([(-50,98,-9),(-55,76,7),(-43,68,-14),(-24,72,-37)],[4,5,5,.5])]
        for pts,ws in locks:tube(h,[pix(*p) for p in pts],[w*scale for w in ws],[max(.4,w*.43)*scale for w in ws],0,n=12,steps=24)
        # Four layered rows of tapered opaque clumps follow measured back head mass.
        profile_h=[(30,10,12),(38,36,30),(50,48,40),(66,53,46),(84,53,47),(102,49,46),(122,45,44),(138,39,36),(149,31,31)]
        for row in range(3):
            for i in range(10):
                th=math.pi/2+math.pi*i/9;back=max(0,-math.cos(th));yend=min([81,117,151][row],104+45*back)
                ystart=[32,65,101][row]
                if yend<ystart+8:continue
                pts=[]
                for t in (0,.33,.72,1):
                    y=ystart+(yend-ystart)*t;w,d=interp(profile_h,y);ang=th+.07*(t-.5);pts.append(pix((w+1.5)*math.sin(ang),y,-(d+1.5)*math.cos(ang)+1))
                tube(h,pts,[.4*scale,5.7*scale,4.8*scale,.3*scale],[.3*scale,2.2*scale,1.7*scale,.3*scale],0,n=10,steps=18)
    else:
        N=32;K=8;rings=[]
        for j in range(K+1):
            u=j/K;ring=[]
            for i in range(N):
                th=TAU*i/N;front=max(0,math.cos(th));yedge=187
                profile_h=[(79,4,6),(88,27,24),(101,42,34),(120,52,42),(141,58,46),(162,56,44),(179,47,39),(188,31,31)]
                if front>.15:
                    bangs=[(-57,181),(-43,153),(-30,127),(-15,118),(0,111),(14,106),(26,117),(37,146),(57,181)]
                    lo,hi=79,187
                    for _ in range(24):
                        mid=(lo+hi)/2;ww,dd=interp(profile_h,mid);xx=ww*math.sin(th)
                        if mid>interp(bangs,xx)[0]:hi=mid
                        else:lo=mid
                    yedge=(lo+hi)/2
                y=79+(yedge-79)*u;width,depth=interp(profile_h,y);x=width*math.sin(th)-4*max(0,1-(y-79)/55);dep=-depth*math.cos(th)+3
                ring.append(h.vert(pix(x,y,dep)))
            rings.append(ring)
        for a,b in zip(rings,rings[1:]):h.bridge(a,b)
        h.cap(rings[0])
        inner=[];center=pix(0,128 if female else 91,3)
        for ring in rings:
            inner.append([h.vert(Vector(h.v[ix])+(center-Vector(h.v[ix])).normalized()*2*scale) for ix in ring])
        for a,b in zip(inner,inner[1:]):h.bridge(list(reversed(a)),list(reversed(b)))
        h.cap(list(reversed(inner[0])));h.bridge(rings[-1],inner[-1])
        for i in range(6):
            off=i*3.8;pts=[pix(15-off*.32,84+off*.12,-17),pix(-5-off*.48,98+off*.1,-40),pix(-32-off*.37,116+off*.55,-39),pix(-48+off*.05,153+off*.5,-18),pix(-41+off*.12,178+off*.18,-8)]
            tube(h,pts,[.5*scale,6*scale,6*scale,5*scale,.4*scale],[.5*scale,2.9*scale,3*scale,2.4*scale,.4*scale],0,n=12,steps=22)
        for i in range(5):
            off=i*4;pts=[pix(17+off*.2,86+off*.13,-10),pix(35+off*.30,112,-28),pix(48+off*.17,145,-22),pix(45+off*.17,173,-10),pix(36+off*.36,184,-2)]
            tube(h,pts,[.3*scale,5.5*scale,5.7*scale,4.7*scale,.35*scale],[.3*scale,2.6*scale,2.7*scale,2.5*scale,.3*scale],0,n=12,steps=22)
        for i in range(6):
            th=math.pi/2+math.pi*i/5;pts=[]
            for y in (82,105,136,163,185+(i%3-1)*2):
                w,d=interp(profile_h,y);pts.append(pix((w+1.6)*math.sin(th),y,-(d+1.6)*math.cos(th)+3))
            tube(h,pts,[.3*scale,5.7*scale,6.3*scale,5.7*scale,.3*scale],[.3*scale,2.6*scale,2.8*scale,2.6*scale,.3*scale],0,n=12,steps=22)
    if not female:
        ymin=min(sole-v[2]/scale for v in h.v)
        h.v=[(x,d,(sole-(22+(y-ymin)*(100-22)/(100-ymin) if (y:=sole-z/scale)<100 else y))*scale) for x,d,z in h.v]
    # Optional hairstyles are fitted to the new complete skull and kept out of the base proofs.
    old_eye=profile['hair_reference_eye_y'];new_eye=profile['eye_y']
    h.v=[(x*profile['hair_fit_x'],d*profile['hair_fit_depth'],(sole-(new_eye+(sole-z/scale-old_eye)*profile['hair_fit_y']))*scale) for x,d,z in h.v]
    hair=h.make();uv(hair)
    # Geometry is modeled in world metres, transforms already identity and origins at feet.
    scene=bpy.context.scene;scene.unit_settings.system='METRIC';scene.unit_settings.scale_length=1
    # Reference images are non-rendering locked empties in the source.
    for view in ('front','back'):
        cx=profile['cx' if view=='front' else 'back_cx'];back=view=='back'
        empty=bpy.data.objects.new('LOCKED_PLATE_'+view,None);bpy.context.collection.objects.link(empty);empty.empty_display_type='IMAGE';empty.data=bpy.data.images.load(str(D/(sex+'_body_plain.jpg')));empty.empty_display_size=1280*scale;empty.location=(((cx-640) if back else (640-cx))*scale,-.55 if back else .55,(sole-360)*scale);empty.rotation_euler=(math.pi/2,0,math.pi if back else 0);empty.hide_render=True;empty.hide_viewport=True;empty.hide_select=True;empty['authority_sha256']=hashlib.sha256((D/(sex+'_body_plain.jpg')).read_bytes()).hexdigest()
    report={'sex':sex,'scale_m_per_pixel':scale,'source_sha256':hashlib.sha256((D/(sex+'_body_plain.jpg')).read_bytes()).hexdigest(),'parts':[]}
    for o in [body,hair]:
        o.data.calc_loop_triangles();zs=[v.co.z for v in o.data.vertices]
        report['parts'].append({'name':o.name,'vertices':len(o.data.vertices),'tris':len(o.data.loop_triangles),'scale':list(o.scale),'origin':list(o.location),'z_min':min(zs),'z_max':max(zs),'material_names':[m.name for m in o.data.materials]})
    report['height_m']=max(v.co.z for v in body.data.vertices)-min(v.co.z for v in body.data.vertices);report['optional_hair_preview_height_m']=max(v.co.z for o in (body,hair) for v in o.data.vertices)-min(v.co.z for v in body.data.vertices)
    report['tris']=sum(p['tris'] for p in report['parts']);(P/(sex+'-blender-audit.json')).write_text(json.dumps(report,indent=2))
    export_args=dict(use_selection=True,object_types={'MESH'},global_scale=1,apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',axis_forward='-Z',axis_up='Y',use_mesh_modifiers=True,mesh_smooth_type='OFF',add_leaf_bones=False,bake_anim=False)
    bpy.ops.object.select_all(action='DESELECT');body.select_set(True);bpy.context.view_layer.objects.active=body
    bpy.ops.export_scene.fbx(filepath=str(D/'unity_import'/('HeroBase_'+sex.title()+'_Body.fbx')),**export_args)
    # A hairstyle is a separate asset in head-local metres. It never supplies scalp, ears or neck.
    anchor_z=(sole-profile['eye_y'])*scale
    style=hair.copy();style.data=hair.data.copy();style.name='Hair_'+suffix;bpy.context.collection.objects.link(style)
    for v in style.data.vertices:v.co.z-=anchor_z
    bpy.ops.object.select_all(action='DESELECT');style.select_set(True);bpy.context.view_layer.objects.active=style
    bpy.ops.export_scene.fbx(filepath=str(D/'unity_import'/('Hair_Default_'+sex.title()+'.fbx')),**export_args)
    bpy.data.objects.remove(style,do_unlink=True)
    (D/'unity_import'/('HeroBase_'+sex.title()+'_Attachments.json')).write_text(json.dumps({'body_fbx':'HeroBase_'+sex.title()+'_Body.fbx','default_hair_fbx':'Hair_Default_'+sex.title()+'.fbx','hair_anchor_metres':[0,anchor_z,0],'hair_origin':'head-local; Unity Y up','body_contains_hair':False,'bald_height_m':max(v.co.z for v in body.data.vertices)-min(v.co.z for v in body.data.vertices)},indent=2))
    # Neutral studio. Consistent lighting/color settings saved in Blender source.
    scene.render.engine='CYCLES';scene.cycles.samples=48;scene.cycles.use_denoising=True
    scene.view_settings.view_transform='Standard';scene.view_settings.look='None';scene.view_settings.exposure=0;scene.view_settings.gamma=1
    scene.world.color=(.23,.23,.23);scene.world.use_nodes=True;scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.23,.23,.23,1);scene.world.node_tree.nodes['Background'].inputs[1].default_value=.34
    def area(name,loc,energy,size):
        data=bpy.data.lights.new(name,'AREA');data.energy=energy;data.shape='DISK';data.size=size;o=bpy.data.objects.new(name,data);bpy.context.collection.objects.link(o);o.location=loc;o.rotation_euler=(Vector((0,0,1))-o.location).to_track_quat('-Z','Y').to_euler()
    area('Neutral_Key',(-2.8,-3.4,4),300,4);area('Neutral_Fill',(2.6,-1.8,2),145,3);area('Neutral_Back',(0,2.6,3),70,3)
    data=bpy.data.cameras.new('ProofCamera');cam=bpy.data.objects.new('ProofCamera',data);bpy.context.collection.objects.link(cam);scene.camera=cam;data.type='ORTHO';scene.render.resolution_x=1280;scene.render.resolution_y=720;scene.render.resolution_percentage=100;data.ortho_scale=1280*scale
    scene.render.image_settings.file_format='PNG';scene.render.image_settings.color_mode='RGBA';scene.render.film_transparent=True
    data.lens=85
    light_positions={name:bpy.data.objects[name].location.copy() for name in ('Neutral_Key','Neutral_Fill','Neutral_Back')}
    for mode in ('base','bald','withhair'):
        hair.hide_render=mode!='withhair'
        shots=[('front',0),('back',math.pi),('threequarter',math.pi/5)]
        if mode=='base':
            shots += [('allaround_'+name,i*math.pi/4) for i,name in enumerate(
                ('front','front_right','right','back_right','back','back_left','left','front_left'))]
        for name,ang in shots:
            for light_name,loc in light_positions.items():
                light=bpy.data.objects[light_name];light.location=(loc.x*math.cos(ang)-loc.y*math.sin(ang),loc.x*math.sin(ang)+loc.y*math.cos(ang),loc.z);light.rotation_euler=(Vector((0,0,1))-light.location).to_track_quat('-Z','Y').to_euler()
            cam.location=(math.sin(ang)*5,-math.cos(ang)*5,(sole-360)*scale);cam.rotation_euler=(Vector((0,0,cam.location.z))-cam.location).to_track_quat('-Z','Y').to_euler()
            tag='' if mode=='base' else '_'+mode
            scene.render.filepath=str(P/'blender'/(suffix+tag+'_'+name+'.png'));bpy.ops.render.render(write_still=True)
    hair.hide_render=True;hair.hide_viewport=True
    for light_name,loc in light_positions.items():
        light=bpy.data.objects[light_name];light.location=loc;light.rotation_euler=(Vector((0,0,1))-loc).to_track_quat('-Z','Y').to_euler()
    cam.location=(0,-5,(sole-360)*scale);cam.rotation_euler=(math.pi/2,0,0)
    bpy.ops.file.pack_all()
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type=='VIEW_3D':
                region=area.spaces.active.region_3d;region.view_location=(0,0,.85);region.view_distance=2.7;region.view_rotation=cam.rotation_euler.to_quaternion();region.view_perspective='ORTHO';area.spaces.active.shading.color_type='MATERIAL'
    bpy.ops.wm.save_as_mainfile(filepath=str(D/'blender'/('HeroBase_'+sex.title()+'.blend')))
    return report

reports=[]
for sex,suffix,profile in [('male','M',MALE),('female','F',FEMALE)]:
    if len(sys.argv)>sys.argv.index('--')+1 if '--' in sys.argv else False:
        requested=sys.argv[sys.argv.index('--')+1]
        if requested not in ('all',sex):continue
    reports.append(build(sex,suffix,profile))
print('BASE_BUILT',json.dumps(reports))
