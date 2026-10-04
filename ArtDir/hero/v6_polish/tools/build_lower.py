import bpy,bmesh,math,json,hashlib,struct
from pathlib import Path
from mathutils import Vector,Matrix
R=Path(__file__).resolve().parents[4];O=R/'ArtDir/hero/v6_polish';bpy.ops.wm.open_mainfile(filepath=str(O/'Hero_V6_Polish.blend'))
rig=bpy.data.objects['Hero_01_Rig'];body=bpy.data.objects['Body_Skin'];report={}
def facehash():return hashlib.sha256(b''.join(struct.pack('fff',*v.co) for v in body.data.vertices if (body.matrix_world@v.co).z>1.20)).hexdigest()
report['face_before']=facehash()
def ss(a,b,x):
 t=max(0,min(1,(x-a)/(b-a)));return t*t*(3-2*t)
def axis(side,z):
 pts=[rig.matrix_world@rig.data.bones[n+'.'+side].head_local for n in ['UpperLeg','LowerLeg','Foot']]
 for a,b in zip(pts,pts[1:]):
  if b.z<=z<=a.z:return a.lerp(b,(a.z-z)/(a.z-b.z))
 return pts[2] if z<pts[2].z else pts[0]
profile=[(.12,.048,.055),(.20,.050,.059),(.25,.055,.066),(.30,.071,.080),(.35,.080,.091),(.39,.077,.085),(.425,.070,.076),(.445,.069,.072),(.47,.073,.078),(.52,.085,.090),(.58,.095,.102),(.65,.098,.112),(.70,.098,.112)]
def radii(z):
 for (a,x,y),(b,u,v) in zip(profile,profile[1:]):
  if z<=b:
   t=ss(a,b,z);return x+(u-x)*t,y+(v-y)*t
 return profile[-1][1:]
def weights(side,z,y):
 # Broad blend over six knee rings; anterior cap biased toward thigh.
 t=ss(.397,.487,z);cap=(1-ss(.008,.045,abs(z-.44)))*ss(.005,.065,.055-y)
 t=t*(1-cap*.22)+.78*cap*.22
 ankle=1-ss(.145,.215,z)
 return {'UpperLeg.'+side:t*(1-ankle),'LowerLeg.'+side:(1-t)*(1-ankle),'Foot.'+side:ankle}
# Preserve source upper body, face, hands, UVs and all their existing groups. Replace lower surfaces.
# New full skin legs remain a separate mesh for reversible wardrobe body coverage.
old_facecount=len(body.data.polygons)
bm=bmesh.new();bm.from_mesh(body.data)
kill=[v for v in bm.verts if (body.matrix_world@v.co).z<.65]
bmesh.ops.delete(bm,geom=kill,context='VERTS');bm.to_mesh(body.data);bm.free()
report['old_lower_vertices_removed']=len(kill)
vs=[];fs=[];ws=[];uv=[];N=40;zs=sorted(set([.12,.16,.20,.235,.26,.29,.32,.35,.375,.39,.403,.415,.427,.439,.451,.463,.475,.49,.515,.54,.57,.60,.63,.655,.68]))
for side in ['L','R']:
 start=len(vs)
 for j,z in enumerate(zs):
  a=axis(side,z);rx,ry=radii(z)
  for i in range(N):
   theta=2*math.pi*i/N;x=a.x+rx*math.cos(theta);y=a.y+ry*math.sin(theta)
   y+=.009*ss(.26,.33,z)*(1-ss(.37,.42,z));p=(x,y,z);vs.append(p);ws.append(weights(side,z,y));uv.append((i/N,j/(len(zs)-1)))
 for j in range(len(zs)-1):
  for i in range(N):a=start+j*N+i;b=start+j*N+(i+1)%N;fs.append((a,b,b+N,a+N))
 fs.append(tuple(reversed([start+i for i in range(N)])));fs.append(tuple(start+(len(zs)-1)*N+i for i in range(N)))
me=bpy.data.meshes.new('Hero_V6_Legs_QuadLoops');me.from_pydata(vs,[],fs);me.update();legs=bpy.data.objects.new('Body_Legs',me);bpy.context.collection.objects.link(legs);legs.parent=rig
skin=bpy.data.materials.get('Hero_V6_Scalp');me.materials.append(skin)
for i,w in enumerate(ws):
 for n,v in w.items():
  if v>1e-6:(legs.vertex_groups.get(n) or legs.vertex_groups.new(name=n)).add([i],v,'REPLACE')
for p in me.polygons:p.use_smooth=True
arm=legs.modifiers.new('Shared Humanoid linear skinning','ARMATURE');arm.object=rig;arm.use_deform_preserve_volume=False
# Hide skin well inside the default shorts; toggled off for body-only / garment changes.
g=legs.vertex_groups.new(name='DefaultShortsCovered');g.add([i for i,p in enumerate(vs) if p[2]>.535],1,'REPLACE')
mask=legs.modifiers.new('Default outfit coverage — reversible','MASK');mask.vertex_group=g.name;mask.invert_vertex_group=True
# UV unwrap is independent per part, exported at <=2K.
def unwrap(o):
 bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o
 bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.uv.smart_project(angle_limit=math.radians(65),island_margin=.015);bpy.ops.object.mode_set(mode='OBJECT')
unwrap(legs)
# Preserve a full body-only skin mesh as an export variant; no clothing baked into skin.
full=legs.copy();full.data=legs.data.copy();bpy.context.collection.objects.link(full);full.name='Body_Legs_Full';full.modifiers.remove(full.modifiers.get(mask.name));full.hide_render=True;full.hide_set(True)
# Shorts retopology refinement, shape and same thigh weights; preserve their original UV/detail.
shorts=bpy.data.objects['Shorts_Default']
if shorts.data.shape_keys:shorts.shape_key_clear()
bm=bmesh.new();bm.from_mesh(shorts.data);es=[e for e in bm.edges if all((shorts.matrix_world@v.co).z<.68 for v in e.verts)];bmesh.ops.subdivide_edges(bm,edges=es,cuts=1,use_grid_fill=True);bm.to_mesh(shorts.data);bm.free()
for v in shorts.data.vertices:
 p=shorts.matrix_world@v.co
 if p.z>.70:continue
 side='L' if p.x>=0 else 'R';a=axis(side,p.z);d=Vector((p.x-a.x,p.y-a.y,0));theta=math.atan2(d.y,d.x);rx,ry=radii(p.z)
 # Hem follows the thigh with 15 mm clearance and a gentle 5 mm flare.
 flare=.005*(1-ss(.52,.59,p.z));target=Vector((a.x+(rx+.016+flare)*math.cos(theta),a.y+(ry+.016+flare)*math.sin(theta),p.z));f=1-ss(.61,.70,p.z)
 v.co=shorts.matrix_world.inverted()@p.lerp(target,f)
 if p.z<.67:
  for vg in shorts.vertex_groups:
   if vg.name in rig.data.bones:
    try:vg.remove([v.index])
    except:pass
  hips=ss(.61,.72,p.z);(shorts.vertex_groups.get('Hips') or shorts.vertex_groups.new(name='Hips')).add([v.index],hips,'REPLACE')
  (shorts.vertex_groups.get('UpperLeg.'+side) or shorts.vertex_groups.new(name='UpperLeg.'+side)).add([v.index],1-hips,'REPLACE')
for p in shorts.data.polygons:p.use_smooth=True
# New clean orange hem piping, follows identical thigh/hips weights.
orange=bpy.data.materials.new('Hero_V6_Orange');orange.use_nodes=True;bs=orange.node_tree.nodes.get('Principled BSDF');bs.inputs['Base Color'].default_value=(.95,.22,.045,1);bs.inputs['Roughness'].default_value=.65
pv=[];pf=[];pw=[]
for side in ['L','R']:
 base=len(pv);a=axis(side,.525);rx,ry=radii(.525)
 for i in range(64):
  t=2*math.pi*i/64;normal=Vector((math.cos(t),math.sin(t),0));c=Vector((a.x+(rx+.022)*math.cos(t),a.y+(ry+.022)*math.sin(t),.523))
  for j in range(6):p=c+normal*(.003*math.cos(j*math.pi/3))+Vector((0,0,.003*math.sin(j*math.pi/3)));pv.append(tuple(p));pw.append({'UpperLeg.'+side:1})
 for i in range(64):
  for j in range(6):pf.append((base+i*6+j,base+((i+1)%64)*6+j,base+((i+1)%64)*6+(j+1)%6,base+i*6+(j+1)%6))
pm=bpy.data.meshes.new('Shorts_Hem_Orange');pm.from_pydata(pv,[],pf);po=bpy.data.objects.new('Shorts_Piping',pm);bpy.context.collection.objects.link(po);po.parent=rig;pm.materials.append(orange)
for i,w in enumerate(pw):
 for n,v in w.items():(po.vertex_groups.get(n) or po.vertex_groups.new(name=n)).add([i],v,'REPLACE')
for p in pm.polygons:p.use_smooth=True
am=po.modifiers.new('Shared Humanoid','ARMATURE');am.object=rig;unwrap(po)
# Shoes retain detailed laces and UVs; clean normals and narrow bulbous outer silhouette around each foot.
shoes=bpy.data.objects['Shoes_Default'];n=0
for v in shoes.data.vertices:
 p=shoes.matrix_world@v.co;side='L' if p.x>0 else 'R';a=axis(side,.165);p.x=a.x+(p.x-a.x)*.93;v.co=shoes.matrix_world.inverted()@p;n+=1
for p in shoes.data.polygons:p.use_smooth=True
# Wider, normalized falloff at source armpits; retain hand/racket bone and curve data.
import sys;sys.path.insert(0,str(R/'ArtDir/hero/v5_tools'));import build_body_v5
build_body_v5.armpits(body,report,'armpit');build_body_v5.armpits(bpy.data.objects['Shirt_Default'],report,'armpit')
for ob in [legs,full,shorts,shoes,po]:
 bm=bmesh.new();bm.from_mesh(ob.data);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(ob.data);bm.free()
report['face_after']=facehash();report['face_geometry_identical']=report['face_after']==report['face_before'];report['new_leg_rings']=len(zs);report['ring_vertices']=N;report['profiles']=profile
report['changes']=['Replaced lower-body surfaces with authored quad-loop legs on original joints','Default shorts coverage is a reversible mask; full skin legs retained','Subdivided and refitted lower shorts, matching thigh weights, new hem piping','Shoes width -7% around foot centres, original texture/sole height','Source armpit weights smoothed without animation edits']
(O/'build-stage2.json').write_text(json.dumps(report,indent=2));bpy.ops.wm.save_as_mainfile(filepath=str(O/'Hero_V6_Polish.blend'));print('V6_LOWER_DONE',report['face_geometry_identical'])
