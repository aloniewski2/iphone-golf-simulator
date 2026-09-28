# Cosmetic pack v1 on the LOCKED V4 hero: meshes fitted to the existing head/hair, skinned to existing bones.
# Nothing on the body, rig or wardrobe changes. Hats replace the Hat (visor) wardrobe slot; the trophy is a
# static prop for the Hand_L socket.
import bpy,bmesh,math,json
from mathutils import Vector,Matrix
from pathlib import Path
P=Path(__file__).resolve().parents[1];OUT=P.parent/'cosmetics';OUT.mkdir(exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(P/'Hero_01_FingerRig.blend'))
rig=bpy.data.objects['Hero_01_Rig'];rig.animation_data_clear()
for b in rig.pose.bones:b.matrix_basis.identity()
def verts(o,group=None,minw=.5):
 gi=o.vertex_groups[group].index if group else None
 return [o.matrix_world@v.co for v in o.data.vertices if gi is None or any(g.group==gi and g.weight>=minw for g in v.groups)]
hair=verts(bpy.data.objects['Hair_Default']);head=verts(bpy.data.objects['Body_Skin'],'Head');visor=verts(bpy.data.objects['Hat_Visor'])
vz=sorted(v.z for v in visor);zb=vz[len(vz)//10]            # visor band height (lower decile)
top=[v for v in hair+head if v.z>zb-.01]
cx=sum(v.x for v in top)/len(top);cy=sorted(v.y for v in top)[len(top)//2]
report={'band_z':zb}
def ell_radii(pts,c,z0,margin):
 rx=max(abs(p.x-c.x) for p in pts)+margin;ry=max(abs(p.y-c.y) for p in pts)+margin;rz=max(p.z-z0 for p in pts)+margin
 # grow uniformly until every point is inside the ellipsoid
 k=max(((p.x-c.x)/rx)**2+((p.y-c.y)/ry)**2+((p.z-z0)/rz)**2 for p in pts)
 s=math.sqrt(max(1,k));return rx*s,ry*s,rz*s
def mat(name,color):
 m=bpy.data.materials.get(name) or bpy.data.materials.new(name);m.diffuse_color=color;return m
def new_obj(name,bm,material_slots):
 for f in bm.faces:f.smooth=True
 me=bpy.data.meshes.new(name);bm.to_mesh(me);bm.free();o=bpy.data.objects.new(name,me);bpy.context.scene.collection.objects.link(o)
 for m in material_slots:o.data.materials.append(m)
 return o
def skin_to(o,bone):
 o.parent=rig;o.matrix_parent_inverse=rig.matrix_world.inverted()
 vg=o.vertex_groups.new(name=bone);vg.add(list(range(len(o.data.vertices))),1.0,'REPLACE')
 mod=o.modifiers.new('Armature','ARMATURE');mod.object=rig
def export(objs,path):
 bpy.ops.object.select_all(action='DESELECT');rig.select_set(True)
 for o in objs:o.select_set(True)
 bpy.context.view_layer.objects.active=rig
 bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types={'MESH','ARMATURE'},add_leaf_bones=False,bake_anim=False,axis_forward='-Z',axis_up='Y')
# ---- Hat A: cap. Dome encloses all hair/head above the band; brim to the front (-Y).
c=Vector((cx,cy,zb));rx,ry,rz=ell_radii(top,c,zb-.012,.018)
bm=bmesh.new();seg=48;rings=14
rows=[]
for i in range(rings+1):
 a=(math.pi/2)*i/rings;row=[]
 for j in range(seg):
  t=2*math.pi*j/seg;row.append(bm.verts.new((cx+rx*math.cos(a)*math.cos(t),cy+ry*math.cos(a)*math.sin(t),zb-.012+rz*math.sin(a))))
 rows.append(row)
apex=bm.verts.new((cx,cy,zb-.012+rz))
for i in range(rings):
 for j in range(seg):bm.faces.new((rows[i][j],rows[i][(j+1)%seg],rows[i+1][(j+1)%seg],rows[i+1][j]))
# brim: front half-ring from the dome base, flared and slightly down
brim=[];rim=rows[0]
for j in range(seg):
 t=2*math.pi*j/seg;front=max(0,-math.sin(t))   # -Y is front
 ext=.02+.13*front**1.5
 brim.append(bm.verts.new((cx+(rx+ext)*math.cos(t),cy+(ry+ext)*math.sin(t),zb-.012-.02*front)))
for j in range(seg):bm.faces.new((rim[(j+1)%seg],rim[j],brim[j],brim[(j+1)%seg]))
bmesh.ops.recalc_face_normals(bm,faces=bm.faces[:])
cap=new_obj('Hat_Cap',bm,[mat('Cos_CapOrange',(1,.42,.24,1))]);skin_to(cap,'Head')
report['cap']={'radii':[rx,ry,rz]}
# ---- Hat B: sweatband. Per-angle radius from the head/hair profile at the band height.
band=[v for v in hair+head if zb-.02<v.z<zb+.05]
bins=72;rad=[0]*bins
for v in band:
 t=math.atan2(v.y-cy,v.x-cx);k=int((t+math.pi)/(2*math.pi)*bins)%bins;rad[k]=max(rad[k],math.hypot(v.x-cx,v.y-cy))
for _ in range(12):rad=[max(rad[k],(rad[k-1]+rad[k]+rad[(k+1)%bins])/3) for k in range(bins)]
bm=bmesh.new();h0,h1=zb-.005,zb+.045;layers=[]
for (z,off) in [(h0,.006),(h1,.006),(h1,.018),(h0,.018)]:
 layer=[]
 for k in range(bins):
  t=-math.pi+2*math.pi*(k+.5)/bins;r=rad[k]+off;layer.append(bm.verts.new((cx+r*math.cos(t),cy+r*math.sin(t),z)))
 layers.append(layer)
for a in range(4):
 A=layers[a];B=layers[(a+1)%4]
 for k in range(bins):bm.faces.new((A[k],A[(k+1)%bins],B[(k+1)%bins],B[k]))
bmesh.ops.recalc_face_normals(bm,faces=bm.faces[:])
sb=new_obj('Hat_Sweatband',bm,[mat('Cos_BandWhite',(.95,.95,.95,1))]);skin_to(sb,'Head')
# orange stripe
bm=bmesh.new();s0,s1=zb+.014,zb+.026;layer=[]
for z in (s0,s1):
 row=[]
 for k in range(bins):
  t=-math.pi+2*math.pi*(k+.5)/bins;r=rad[k]+.0195;row.append(bm.verts.new((cx+r*math.cos(t),cy+r*math.sin(t),z)))
 layer.append(row)
for k in range(bins):bm.faces.new((layer[0][k],layer[0][(k+1)%bins],layer[1][(k+1)%bins],layer[1][k]))
bmesh.ops.recalc_face_normals(bm,faces=bm.faces[:])
st=new_obj('Hat_SweatbandStripe',bm,[mat('Cos_BandOrange',(1,.42,.24,1))]);skin_to(st,'Head')
report['sweatband']={'min_r':min(rad),'max_r':max(rad)}
# ---- Celebration prop: mini trophy (static mesh, origin at the grip, +Y up the stem)
bm=bmesh.new()
def lathe(profile,segs=24,yoff=0):
 rings=[]
 for (r,y) in profile:
  rings.append([bm.verts.new((r*math.cos(2*math.pi*k/segs),y+yoff,r*math.sin(2*math.pi*k/segs))) for k in range(segs)])
 for A,B in zip(rings,rings[1:]):
  for k in range(segs):bm.faces.new((A[k],A[(k+1)%segs],B[(k+1)%segs],B[k]))
lathe([(.001,-.075),(.045,-.075),(.045,-.06),(.02,-.05),(.016,-.02),(.016,.05),(.022,.06),(.05,.08),(.065,.13),(.07,.17),(.066,.175),(.0,.175)])
bmesh.ops.recalc_face_normals(bm,faces=bm.faces[:])
trophy=new_obj('Prop_Trophy',bm,[mat('Cos_Gold',(1,.78,.2,1))])
bpy.ops.object.select_all(action='DESELECT');trophy.select_set(True);bpy.context.view_layer.objects.active=trophy
bpy.ops.export_scene.fbx(filepath=str(OUT/'Hero_Prop_Trophy.fbx'),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',bake_space_transform=True)
export([cap],OUT/'Hero_01_Hat_Cap.fbx');export([sb,st],OUT/'Hero_01_Hat_Sweatband.fbx')
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Hero_Cosmetics_v1.blend'))
(OUT/'cosmetics_report.json').write_text(json.dumps(report,indent=1));print('COSMETICS',json.dumps(report))
