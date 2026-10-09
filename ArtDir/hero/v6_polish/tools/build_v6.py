"""V6 isolated authoring. No source asset or animation edits."""
import bpy,bmesh,math,json,hashlib,sys
from pathlib import Path
from mathutils import Vector,Matrix
R=Path(__file__).resolve().parents[4];O=R/'ArtDir/hero/v6_polish'
bpy.ops.wm.open_mainfile(filepath=str(R/'ArtDir/hero/v5/Hero_01_V5.blend'))
rig=bpy.data.objects['Hero_01_Rig'];rig.animation_data_clear()
for p in rig.pose.bones:p.matrix_basis=Matrix.Identity(4)
bpy.context.view_layer.update()
body=bpy.data.objects['Body_Skin']
def facehash():return hashlib.sha256(b''.join(__import__('struct').pack('fff',*v.co) for v in body.data.vertices if (body.matrix_world@v.co).z>1.20)).hexdigest()
report={'face_before':facehash(),'bones_before':{b.name:[list(x) for x in b.matrix_local] for b in rig.data.bones},'changes':[]}
# Keep only the approved default loadout in this duplicate; other hair assets stay in V5.
keep={'Body_Skin','Body_EyeSphere_L','Body_EyeSphere_R','Hair_Default','Hat_Visor','Shirt_Default','Shorts_Default','Shoes_Default'}
for o in list(bpy.data.objects):
 if o.type=='MESH' and o.name not in keep:bpy.data.objects.remove(o,do_unlink=True)
for o in bpy.data.objects:
 if o.type=='MESH':
  o.hide_render=False;o.hide_set(False)
  for poly in o.data.polygons:poly.use_smooth=True
def srgb(v):return v/12.92 if v<.04045 else ((v+.055)/1.055)**2.4
def mat(name,h,rough=.6):
 m=bpy.data.materials.new(name);m.use_nodes=True;c=tuple(srgb(int(h[i:i+2],16)/255) for i in (0,2,4))+(1,);m.diffuse_color=c
 bs=m.node_tree.nodes.get('Principled BSDF');bs.inputs['Base Color'].default_value=c;bs.inputs['Roughness'].default_value=rough;bs.inputs['Metallic'].default_value=0;bs.inputs['Alpha'].default_value=1
 return m
hairm=[mat('Hero_V6_Hair_'+str(i),h,.59) for i,h in enumerate(['D8AB62','DDB36D','E3BD7B','D3A159'])]
skin=mat('Hero_V6_Scalp','FFE0C2',.63)
def mesh(name,vs,fs,material,weights=None):
 me=bpy.data.meshes.new(name);me.from_pydata(vs,[],fs);me.update();ob=bpy.data.objects.new(name,me);bpy.context.collection.objects.link(ob)
 for m in material:me.materials.append(m)
 for p in me.polygons:p.use_smooth=True
 ob.parent=rig
 if weights is None:weights=[{'Head':1} for _ in vs]
 for i,ws in enumerate(weights):
  for n,w in ws.items():
   if w>1e-6:(ob.vertex_groups.get(n) or ob.vertex_groups.new(name=n)).add([i],w,'REPLACE')
 arm=ob.modifiers.new('Shared Humanoid','ARMATURE');arm.object=rig;arm.use_deform_preserve_volume=False
 bm=bmesh.new();bm.from_mesh(me);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(me);bm.free()
 return ob
C=Vector((0,.04,1.455));rad=Vector((.194,.205,.257))
def direction(lat,lon):
 a,b=math.radians(lat),math.radians(lon);return Vector((math.cos(a)*math.sin(b),-math.cos(a)*math.cos(b),math.sin(a)))
def point(lat,lon,off=0):
 d=direction(lat,lon);return C+Vector((d.x*rad.x,d.y*rad.y,d.z*rad.z))+d*off
# Closed hair core with a tucked hairline, never an alpha shell.
def core(name,scalp=False):
 vs=[];fs=[];N=64;M=16
 for j in range(M+1):
  for i in range(N):
   lon=360*i/N;a=abs((lon+180)%360-180);bottom=22 if a<50 else (10 if a<110 else -26)
   lat=bottom+(89.9-bottom)*j/M
   p=point(lat,lon,-.030 if scalp else -.010)
   if j==0:p=point(lat,lon,-.043 if scalp else -.026)
   vs.append(tuple(p))
 for j in range(M):
  for i in range(N):a=j*N+i;b=j*N+(i+1)%N;fs.append((a,b,b+N,a+N))
 top=len(vs);vs.append(tuple(point(90,0,-.030 if scalp else -.010)))
 bottom=len(vs);vs.append(tuple(C))
 for i in range(N):fs.extend([(M*N+i,M*N+(i+1)%N,top),((i+1)%N,i,bottom)])
 return vs,fs
vs,fs=core('hair');mi=[0]*len(fs)
# Authored rows of broad overlapping locks; each is a closed tapered lens, with smooth clean ridges.
paths=[]
for lon in range(-155,181,30):paths.append((62,lon,17 if abs(lon)>65 else 32,lon-24,.073,.016))
for lon in [-145,-110,-80,75,105,135,165]:paths.append((31,lon,-22 if abs(lon)>110 else 2,lon-8,.060,.014))
# Crown swept to the character's right. Five primary locks.
for lon in [-20,8,36,64,92]:paths.append((40,lon,67,lon-85,.102,.027))
# Three small fringe pieces below the band, above the eyebrows.
for lon in [-30,0,30]:paths.append((28,lon,12,lon-19,.054,.013))
for k,(la,lo,lb,lob,width,depth) in enumerate(paths):
 rings=[];S=12;T=12
 for j in range(T+1):
  t=j/T;lat=la+(lb-la)*t;lon=lo+(lob-lo)*t;p=point(lat,lon,.002+.012*math.sin(math.pi*t))
  tangent=(point(lat+(lb-la)*.001,lon+(lob-lo)*.001)-point(lat,lon)).normalized();out=direction(lat,lon);side=tangent.cross(out).normalized();normal=side.cross(tangent).normalized()
  w=width*(.55+.65*math.sin(math.pi*t*.9))*(1-t**3.5)+.00025;h=depth*(.6+.4*math.sin(math.pi*t))*(1-t**2)+.0002
  ring=[]
  for i in range(S):
   a=2*math.pi*i/S;ridge=1+.045*math.cos(a*4);q=p+side*(math.cos(a)*w*.5)+normal*(math.sin(a)*h*ridge)
   ring.append(len(vs));vs.append(tuple(q))
  rings.append(ring)
 for j in range(T):
  for i in range(S):fs.append((rings[j][i],rings[j][(i+1)%S],rings[j+1][(i+1)%S],rings[j+1][i]));mi.append(1+k%3)
 fs.append(tuple(reversed(rings[0])));mi.append(1+k%3);fs.append(tuple(rings[-1]));mi.append(1+k%3)
bpy.data.objects.remove(bpy.data.objects['Hair_Default'],do_unlink=True)
hair=mesh('Hair_Default',vs,fs,hairm)
for p,m in zip(hair.data.polygons,mi):p.material_index=m
# Natural no-headwear variant; visor version compresses only geometry that intersects the band envelope.
free=hair.copy();free.data=hair.data.copy();bpy.context.collection.objects.link(free);free.name='Hair_Default_Free';free.hide_render=True
sys.path.insert(0,str(R/'ArtDir/hero/v5_tools'))
import build_body_v5
build_body_v5.hair_under_band(bpy.data.objects['Hat_Visor'],hair,report,clearance=.005,falloff=.008)
sv,sf=core('scalp',True);scalp=mesh('Body_Scalp',sv,sf,[skin])
# Scalp stays independent of the Hair slot so hair-off can never remove the skull cover.
for ob in [hair,free,scalp]:
 bpy.ops.object.select_all(action='DESELECT');ob.select_set(True);bpy.context.view_layer.objects.active=ob
 bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.uv.smart_project(angle_limit=math.radians(65),island_margin=.012);bpy.ops.object.mode_set(mode='OBJECT')
report['changes'].append('New closed core and 27 sculpted closed hair locks; independent closed scalp; fitted visor hair plus free variant.')
# Custom split normals from the source can retain faceted shading after weight edits.
for ob in [body,bpy.data.objects['Shirt_Default'],bpy.data.objects['Shorts_Default']]:
 if ob.data.has_custom_normals:ob.data.normals_split_custom_set([(0,0,0)]*len(ob.data.loops))
report['face_after']=facehash();report['face_geometry_identical']=report['face_before']==report['face_after']
report['bones_identical']=report['bones_before']=={b.name:[list(x) for x in b.matrix_local] for b in rig.data.bones}
for ob in [hair,scalp]:
 bm=bmesh.new();bm.from_mesh(ob.data);report[ob.name]={'tris':sum(len(f.verts)-2 for f in bm.faces),'boundary_edges':sum(e.is_boundary for e in bm.edges),'nonmanifold_edges':sum(not e.is_manifold for e in bm.edges)};bm.free()
(O/'build-stage1.json').write_text(json.dumps(report,indent=2));bpy.ops.wm.save_as_mainfile(filepath=str(O/'Hero_V6_Polish.blend'));print('V6_STAGE1_DONE',report['face_geometry_identical'],report['bones_identical'])
