"""Male-only A-pose blockout. Direct measured fields, no existing mesh remesh,
textures, hair, facial polish, exports or Unity work.
"""
import bpy,bmesh,math,json,sys,hashlib
import numpy as np
from pathlib import Path
from mathutils import Vector
R=Path(__file__).resolve().parents[2];D=R/'ArtDir/hero/base_lock';W=R/'work/male-silhouette';P=D/'proof/male_silhouette'
P.mkdir(parents=True,exist_ok=True)
scale=1.7/593;sole=654;step=4.1
# Change of coordinate origin from the preliminary profile centres to the
# independently measured ankle axis; this puts the ankle near depth zero.
depth_origin_offset=8.6606529209622
pix=lambda x,y,d:Vector((x*scale,(d+depth_origin_offset)*scale,(sole-y)*scale))

def interp(rows,t):
    if t<=rows[0][0]:return rows[0][1:]
    if t>=rows[-1][0]:return rows[-1][1:]
    for i in range(len(rows)-1):
        a,b=rows[i:i+2]
        if a[0]<=t<=b[0]:break
    u=(t-a[0])/(b[0]-a[0]);out=[]
    for j in range(1,len(a)):
        d=(b[j]-a[j])/(b[0]-a[0]);dl=(a[j]-rows[i-1][j])/(a[0]-rows[i-1][0]) if i else d;dr=(rows[i+2][j]-b[j])/(rows[i+2][0]-b[0]) if i+2<len(rows) else d
        m0=0 if dl*d<=0 else 2*dl*d/(dl+d);m1=0 if dr*d<=0 else 2*dr*d/(dr+d)
        out.append((2*u**3-3*u*u+1)*a[j]+(u**3-2*u*u+u)*(b[0]-a[0])*m0+(-2*u**3+3*u*u)*b[j]+(u**3-u*u)*(b[0]-a[0])*m1)
    return out

# Rows: canonical plate Y, transverse half-width, depth half-width, depth centre.
# Front/back widths and side depths are measured independently on source masks.
torso=[(155,23,23,-10),(160,31,28,-13),(170,52,30,-10),(180,70,32,-8),
       (190,80,38,-9),(205,82,44,-17),(220,79,47,-20),(230,73,48,-22),
       (240,66,48,-23),(250,63,48,-23),(270,57,45,-26),(280,55.5,44,-27),
       (300,56,42,-28),(310,56,41,-28),(325,58,44,-25),(340,60,46,-22),
       (355,63,49,-23),(365,65,49,-23),(375,68,48,-23),(381,65,43,-23),
       (384,20,28,-24),(386,1,1,-24)]
neck=[(124,20,22,-10),(135,24,25,-12),(145,24,26,-13),(155,24,27,-13),(171,33,32,-10)]
head=[(59.5,.5,.5,-28),(61,9,10,-28),(63,15,17,-28),(66,20,23,-27),
      (70,23.5,30,-26),(76,28,35,-27),(84,31,38,-27),(94,31.5,40,-27),
      (104,31.5,40,-27),(115,31,38,-27),(125,30,36,-28),(130,28.5,34,-30),
      (135,27,31,-32),(140,24,28,-35),(145,20,23,-40),(148,14,17,-46),
      (150,8,9,-53),(152,.5,.5,-60)]
# Rows: Y, leg centre X, half-width, depth half-width, depth centre.
leg=[(360,34,29,40,-23),(375,38,32,40,-24),(390,40,30,39,-24),
     (410,42,31,38,-24),(425,42,30.5,36.5,-24),(445,45,27,33,-24),
     (465,47,23.5,28.5,-22),(480,50,22,27,-19),(490,52,21.5,26.5,-18),
     (505,54,23,28,-12),(520,55,25,28,-7),(540,58,23,26,-5),
     (555,59,20.5,24.5,-6),(570,60,18,23,-7),(585,62,15.5,22.5,-9),
     (600,63,16,29,-14),(610,65,17,48,-30),(625,71,21,51,-31),
     (635,75,24,51,-31),(644,76,21,51,-33),(649,75,17,48,-34),
     (652,75,12,42,-35),(654,75,4,28,-35)]
# Rows: Y, arm centre X, half-width, depth half-width, depth centre.
arm=[(181,70,1,1,0),(190,78,15,29,0),(205,88,18,32,-2),
     (220,95,19,31,2),(230,103,22,31,0),(240,111,25,32,-4),
     (250,117,26,33,-10),(260,127,27,34,-15),(270,137,27,35,-19),
     (280,146,26,34,-24),(290,155,24,33,-29),(300,166,22,31,-32),
     (310,178,18,29,-35),(320,187,16,27,-39),(330,196,15,24,-42),
     (340,205,15,22,-43),(349,214,13,19,-44),(357,223,9,17,-45),
     (368,232,4,12,-46),(376,237,.5,1,-47)]
# Corrections from native overlays: lift the crotch opening, bring the throat
# behind the measured chin, and account for sampling at the leg outline.
neck=[(y,w,d,c-(6 if y<=160 else 4)) for y,w,d,c in neck]
head=[(y,w+(.8 if 70<=y<=140 else 0),d+(3 if y==70 else 1.5 if 76<=y<=125 else 0),c-min(8,max(0,(y-115)*.4))) for y,w,d,c in head]
leg=[(y,cx,w+(.5 if 390<=y<=585 else 3 if 635<=y<=649 else 4 if y>=652 else 0),d+(1.4 if 390<=y<=600 else 0),c) for y,cx,w,d,c in leg]
arm=[(y+1.5,cx,w-(1 if 185<=y<215 else 0)+(1.5 if 235<=y<=340 else 0),d,c) for y,cx,w,d,c in arm]
profiles={'scale_metres_per_canonical_pixel':scale,'depth_origin_offset_canonical_pixels':depth_origin_offset,'torso':torso,'neck':neck,'head':head,'leg':leg,'arm':arm,'grid_pixels':step,'stage':'male A-pose silhouette only'}
(W/'blockout-sections.json').write_text(json.dumps(profiles,indent=2))

xs=np.arange(-262.4,266,step,dtype=np.float32);ds=np.arange(-114.1,85,step,dtype=np.float32);ys=np.arange(50,666,step,dtype=np.float32)
X=xs[:,None,None];Z=ds[None,:,None];Y=ys[None,None,:];shape=(len(xs),len(ds),len(ys));field=np.full(shape,1e4,np.float32)
def add(a,k=2):
    global field
    h=np.maximum(k-np.abs(field-a),0)/max(k,.001);field=np.minimum(field,a)-h*h*k*.25
def curve(rows):
    p=np.array([interp(rows,float(y)) for y in ys],np.float32)
    return [p[:,j][None,None,:] for j in range(p.shape[1])]
def section(rows,sign=0,k=2):
    if sign:cx,w,d,centre=curve(rows);xx=X-sign*cx
    else:w,d,centre=curve(rows);xx=X
    a=(np.sqrt((xx/np.maximum(w,.5))**2+((Z-centre)/np.maximum(d,.5))**2)-1)*np.minimum(w,d)
    a=np.maximum(a,np.maximum(rows[0][0]-Y,Y-rows[-1][0]));add(a,k)
def ellipsoid(cx,y,dep,rx,ry,rd,k=1):
    a=(np.sqrt(((X-cx)/rx)**2+((Y-y)/ry)**2+((Z-dep)/rd)**2)-1)*min(rx,ry,rd);add(a,k)
section(torso,k=2);section(neck,k=3);section(head,k=2)
ellipsoid(0,120,-68,5.8,6,8,1.2)
for sign in (-1,1):
    section(leg,sign,3);section(arm,sign,2)
    ellipsoid(sign*34,114,-24,6,13,10,1)
    # Thumb and fingers are only silhouette masses, not finished hand anatomy.
    ellipsoid(sign*202,353,-50,4,11,8,2)
    for j in range(4):ellipsoid(sign*(226+j*2),365+j,-(63-j*11),3,9-j,3,2)
field=np.maximum(field,Y-sole)

# Direct surface nets from the authored fields. No mesh is remeshed or decimated.
corners=[(0,0,0),(1,0,0),(0,1,0),(1,1,0),(0,0,1),(1,0,1),(0,1,1),(1,1,1)]
edges=[(0,1),(2,3),(4,5),(6,7),(0,2),(1,3),(4,6),(5,7),(0,4),(1,5),(2,6),(3,7)]
vals=[field[a:a+len(xs)-1,b:b+len(ds)-1,c:c+len(ys)-1] for a,b,c in corners]
mixed=(np.minimum.reduce(vals)<0)&(np.maximum.reduce(vals)>=0);ii,jj,kk=np.nonzero(mixed)
cell=np.full(mixed.shape,-1,np.int32);cell[ii,jj,kk]=np.arange(len(ii))
points=np.zeros((len(ii),3));count=np.zeros(len(ii),int)
for a,b in edges:
    va=vals[a][ii,jj,kk];vb=vals[b][ii,jj,kk];cross=(va<0)!=(vb<0);t=va/np.where(abs(va-vb)>1e-8,va-vb,1)
    aa=np.array(corners[a]);bb=np.array(corners[b]);points+=(aa[None,:]+t[:,None]*(bb-aa)[None,:])*cross[:,None];count+=cross
points/=np.maximum(count,1)[:,None];points+=np.stack((ii,jj,kk),axis=1);points=points*step+np.array([xs[0],ds[0],ys[0]])
verts=[pix(float(x),float(y),float(d)) for x,d,y in points];faces=[]
def face(ids):
    if min(ids)>=0:faces.append(tuple(int(i) for i in ids))
inside=field<0
for i,j,k in zip(*np.nonzero(inside[:-1,1:-1,1:-1]^inside[1:,1:-1,1:-1])):
    j+=1;k+=1;face([cell[i,j-1,k-1],cell[i,j,k-1],cell[i,j,k],cell[i,j-1,k]])
for i,j,k in zip(*np.nonzero(inside[1:-1,:-1,1:-1]^inside[1:-1,1:,1:-1])):
    i+=1;k+=1;face([cell[i-1,j,k-1],cell[i-1,j,k],cell[i,j,k],cell[i,j,k-1]])
for i,j,k in zip(*np.nonzero(inside[1:-1,1:-1,:-1]^inside[1:-1,1:-1,1:])):
    i+=1;j+=1;face([cell[i-1,j-1,k],cell[i,j-1,k],cell[i,j,k],cell[i-1,j,k]])
bpy.ops.wm.read_factory_settings(use_empty=True)
me=bpy.data.meshes.new('Male_Apose_Silhouette');me.from_pydata(verts,[],faces);me.update()
bm=bmesh.new();bm.from_mesh(me);bmesh.ops.recalc_face_normals(bm,faces=bm.faces);bm.to_mesh(me);bm.free()
obj=bpy.data.objects.new('Male_Apose_Blockout',me);bpy.context.collection.objects.link(obj)
mat=bpy.data.materials.new('Blockout_Grey');mat.diffuse_color=(.42,.42,.42,1);me.materials.append(mat)
for f in me.polygons:f.use_smooth=True
obj['stage']='Male silhouette blockout only';obj['hair']=False;obj['stubble']=False;obj['reference_index']='ArtDir/hero/base_lock/REF_INDEX.md'
scene=bpy.context.scene;scene.unit_settings.system='METRIC';scene.unit_settings.scale_length=1
scene.render.engine='BLENDER_WORKBENCH';scene.display.shading.light='STUDIO';scene.display.shading.color_type='MATERIAL';scene.display.shading.show_shadows=False;scene.display.shading.show_cavity=False
scene.render.resolution_x=1280;scene.render.resolution_y=720;scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG';scene.render.image_settings.color_mode='RGBA';scene.render.film_transparent=True
scene.view_settings.view_transform='Standard'
views=json.loads((W/'reference-cameras.json').read_text())['views']
for v in views:
    angle=math.radians(v['angle_degrees']);mpp=v['metres_per_pixel'];offset=(640-v['centre_x'])*mpp;h=(v['sole_y']-360)*mpp
    target=Vector((math.cos(angle)*offset,math.sin(angle)*offset,h))
    data=bpy.data.cameras.new('Locked_'+v['name']);cam=bpy.data.objects.new('Locked_'+v['name'],data);bpy.context.collection.objects.link(cam)
    data.type='ORTHO';data.ortho_scale=1280*mpp;cam.location=target+Vector((math.sin(angle)*5,-math.cos(angle)*5,0));cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler()
    cam.lock_location=(True,True,True);cam.lock_rotation=(True,True,True);cam.lock_scale=(True,True,True);cam.hide_select=True
    data.show_background_images=True;bg=data.background_images.new();bg.image=bpy.data.images.load(str(D/v['file']),check_existing=True);bg.alpha=.35;bg.display_depth='FRONT'
    cam['reference_sha256']=v['sha256'];cam['metres_per_pixel']=mpp;cam['source_centre_x']=v['centre_x'];cam['source_sole_y']=v['sole_y']
    scene.camera=cam;scene.render.filepath=str(P/(v['name']+'.png'));bpy.ops.render.render(write_still=True)
# Pack the head-detail authority as a locked modeling reference, without face polish.
head_ref=bpy.data.objects.new('LOCKED_Male_Head_Detail',None);bpy.context.collection.objects.link(head_ref);head_ref.empty_display_type='IMAGE';head_ref.data=bpy.data.images.load(str(D/'male_head_detail.jpg'));head_ref.empty_display_size=2.5;head_ref.location=(3,0,1.3);head_ref.rotation_euler=(math.pi/2,0,0);head_ref.hide_render=True;head_ref.hide_select=True
head_ref['reference_sha256']=hashlib.sha256((D/'male_head_detail.jpg').read_bytes()).hexdigest()
scene.camera=bpy.data.objects['Locked_bald_front'];bpy.context.view_layer.objects.active=obj;obj.select_set(True)
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':area.spaces.active.region_3d.view_perspective='CAMERA'
me.calc_loop_triangles();report={'stage':'male A-pose silhouette only','height_metres':max(v.co.z for v in me.vertices)-min(v.co.z for v in me.vertices),'vertices':len(me.vertices),'triangles':len(me.loop_triangles),'meshes':[obj.name],'unit_scale':1,'source_profiles':profiles,'reference_views':views,'head_detail_sha256':head_ref['reference_sha256']}
(P/'source-audit.json').write_text(json.dumps(report,indent=2));bpy.ops.file.pack_all();bpy.ops.wm.save_as_mainfile(filepath=str(D/'blender/HeroBase_Male_Silhouette.blend'))
print('MALE_SILHOUETTE_BUILT',json.dumps({k:report[k] for k in ['height_metres','vertices','triangles','meshes']}))
