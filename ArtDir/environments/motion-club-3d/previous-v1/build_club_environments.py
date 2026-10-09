"""ImageGen planar projections -> Blender UV bake -> real runtime triangle meshes.
Run Blender --background --python this_file -- <repo root>.
"""
import bpy, math, json, sys, random
from pathlib import Path
from mathutils import Vector
ROOT=Path(sys.argv[sys.argv.index('--')+1]); ART=ROOT/'ArtDir/environments/motion-club-3d'; OUT=ROOT/'GolfArcade/Unity/ClubEnvironments'
random.seed(42)
source=bpy.data.images.load(str(ART/'sources/club-material-projections.png'))
PAL={'stucco':('e9dfc8',0),'wood':('b9874e',1),'stone':('dcd2ba',2),'clay':('bd7155',3),'navy':('203952',4),'linen':('e8dfcc',5),'green':('669254',None),'darkgreen':('3d7454',None),'water':('5ba9b9',None),'glass':('83b9c1',None),'brass':('bd9457',None),'white':('f1ead9',None),'court':('477d85',None),'sand':('d4bd91',None),'hill':('7da99b',None)}
def rgba(h): return tuple(int(h[i:i+2],16)/255 for i in (0,2,4))+(1,)
def mat(name):
 h,tile=PAL[name]; m=bpy.data.materials.new(name);m.use_nodes=True;m.diffuse_color=rgba(h)
 n=m.node_tree.nodes;n.clear();out=n.new('ShaderNodeOutputMaterial');em=n.new('ShaderNodeEmission');em.inputs['Color'].default_value=rgba(h);m.node_tree.links.new(em.outputs[0],out.inputs[0])
 if tile is not None:
  uv=n.new('ShaderNodeUVMap');uv.uv_map='ProjectionUV';tx=n.new('ShaderNodeTexImage');tx.image=source;tx.extension='EXTEND';m.node_tree.links.new(uv.outputs[0],tx.inputs[0]);m.node_tree.links.new(tx.outputs['Color'],em.inputs['Color'])
 return m
M={k:mat(k) for k in PAL}
objects=[]
def finish(o,name,material,bevel=0):
 o.name=name;o.data.materials.append(M[material]);bpy.context.view_layer.objects.active=o;bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
 if bevel:
  mod=o.modifiers.new('Near-camera softened edges','BEVEL');mod.width=bevel;mod.segments=3;bpy.ops.object.modifier_apply(modifier=mod.name)
  for p in o.data.polygons:p.use_smooth=True
  mod=o.modifiers.new('Weighted corner normals','WEIGHTED_NORMAL');mod.keep_sharp=True;bpy.ops.object.modifier_apply(modifier=mod.name)
 objects.append(o);return o
def box(name,loc,scale,material,bevel=.045):
 bpy.ops.mesh.primitive_cube_add(size=1,location=loc);o=bpy.context.object;o.scale=scale;return finish(o,name,material,bevel)
def cyl(name,loc,r,depth,material,verts=24):
 bpy.ops.mesh.primitive_cylinder_add(vertices=verts,radius=r,depth=depth,location=loc);return finish(bpy.context.object,name,material,.025 if verts>12 else 0)
def sphere(name,loc,scale,material):
 bpy.ops.mesh.primitive_uv_sphere_add(segments=12,ring_count=6,location=loc);o=bpy.context.object;o.scale=scale;return finish(o,name,material)
def beam(name,a,b,width,material):
 a,b=Vector(a),Vector(b);o=box(name,(a+b)/2,(width,width,(b-a).length),material,.015);o.rotation_euler=(b-a).to_track_quat('Z','Y').to_euler();return o
def palm(x,y,size=1):
 base=Vector((x,y,0));top=base+Vector((.28*size,0,4.8*size));beam('Palm trunk',base,top,.22*size,'wood')
 for k in range(7):
  a=k*math.tau/7;v=[];f=[]
  for j in range(6):
   t=j/5;rad=2.1*size*t;z=.35*size*math.sin(t*math.pi)-.9*size*t*t;w=.30*size*math.sin(t*math.pi)
   for side in [-1,1]:v.append(tuple(top+Vector((math.cos(a)*rad-math.sin(a)*w*side,math.sin(a)*rad+math.cos(a)*w*side,z))))
  for j in range(5):f.append((2*j,2*j+1,2*j+3,2*j+2))
  mesh=bpy.data.meshes.new('Leaf');mesh.from_pydata(v,[],f);o=bpy.data.objects.new('Sculpted palm frond',mesh);bpy.context.collection.objects.link(o);finish(o,'Sculpted palm frond','green')
def planter(x,y):
 cyl('Terracotta planter',(x,y,.36),.48,.72,'clay');cyl('Planter lip',(x,y,.73),.53,.12,'clay');
 for dx,dy,z in [(-.18,0,.99),(.18,.05,1.06),(0,-.18,1.22)]:sphere('Plant',(x+dx,y+dy,z),(.36,.34,.40),'green')
def bench(x,y):
 for z in [.65,1.10]:box('Bench teak slab',(x,y+(0 if z<1 else .32),z),(2.8,.70 if z<1 else .12,.13 if z<1 else .62),'wood')
 for dx in [-1.05,1.05]:box('Bench feet',(x+dx,y,.32),(.14,.55,.64),'navy')
 box('Bench cushion',(x,y,.79),(2.68,.64,.19),'linen',.075)
def window(x,y,z,w=1.8,h=2):
 box('Window glass',(x,y,z),(w,.12,h),'glass',.02)
 for dx in [-w/2,w/2]:box('Window side casing',(x+dx,y-.10,z),(.12,.20,h+.22),'wood')
 for dz in [-h/2,h/2]:box('Window sill',(x,y-.10,z+dz),(w+.24,.24,.12),'wood')
 box('Window mullion',(x,y-.17,z),(.065,.10,h),'white',.015)
def floor():
 box('Foundation',(0,5,-.22),(30,32,.4),'stone',.02)
 # Real joints and bevels only near the viewpoint; large calm surfaces farther away.
 for x in range(-7,8,2):
  for y in range(-5,6,2):box('Foreground limestone paving',(x,y,.015),(1.98,1.98,.045),'stone',.015)
def sea():
 box('Ocean',(0,45,-.7),(200,100,.2),'water',0)
 for x,y,s in [(-20,57,8),(18,65,11),(40,70,7)]:sphere('Distant island',(x,y,-.3),(s,s*.6,s*.32),'hill')
 for x,y,z in [(-22,65,12),(15,78,14),(39,85,11)]:
  for dx,dz,s in [(-2,0,2.8),(0,1,3.4),(2.8,0,2.4)]:sphere('Distant cloud',(x+dx,y,z+dz),(s,1.4,s*.45),'white')
def rails(y=10):
 for x in [-10,-6,-2,2,6,10]:box('Balustrade post',(x,y,.75),(.28,.3,1.5),'stucco')
 for z in [.35,1.3]:box('Balustrade rail',(0,y,z),(21,.2,.18),'wood')
def build(room):
 floor()
 if room!='locker':sea()
 if room=='entrance':
  # Clubhouse on right, open arrival plaza left.
  box('Clubhouse',(5.8,9,2.5),(9,5,5),'stucco',.12)
  for x in [3.1,6.0,8.8]:window(x,6.42,2.8,1.7,2.6)
  box('Veranda canopy',(5.8,5.2,4.5),(10,3,.22),'wood')
  for x in [1,10.5]:box('Porch column',(x,4.4,2.2),(.35,.4,4.4),'stucco',.06)
  for i in range(9):box('Roof tile band',(5.8,7+i*.4,5.15+i*.11),(10,.46,.12),'clay',.02)
  for i in range(3):box('Porch step',(5.8,3.7-i*.5,.13*(3-i)),(10,.52,.26*(3-i)),'stone',.03)
  planter(-5,1);planter(9,2);palm(-8,8,1.15);palm(12,15,1.1);bench(-5,5)
 elif room=='terrace':
  rails(11)
  for x in [-8,9]:box('Pergola column',(x,5,2.55),(.40,.40,5.1),'stucco',.07)
  for y in [2,5,8]:box('Pergola crossbeam',(.5,y,5.1),(18,.30,.32),'wood')
  for x in range(-8,10,2):box('Pergola rafter',(x,5,5.34),(.22,8,.24),'wood')
  bench(6,5);planter(-5,0);palm(-8,12,1.1);palm(8,18,1.3)
  cyl('Round teak side table',(5.6,-1,.78),.85,.14,'wood',48);cyl('Table stem',(5.6,-1,.38),.10,.72,'brass')
  box('Lounge seat',(7,-.2,.50),(1.5,1.8,.3),'linen',.12);o=box('Lounge back',(7,.65,1.1),(1.5,.3,1.3),'navy',.12);o.rotation_euler.x=math.radians(-12)
 elif room=='locker':
  box('Back room wall',(0,10,2.8),(25,.4,5.6),'stucco',.04);box('Side room wall',(-10,4,2.8),(.35,12,5.6),'stucco');box('Right room wall',(11,4,2.8),(.35,12,5.6),'stucco');box('Locker ceiling',(0,4,5.6),(22,13,.25),'stucco')
  for x in range(-9,11,2):
   box('Locker carcass',(x,9,2.2),(1.9,1.6,4.4),'wood',.07)
   box('Navy inset locker door',(x,8.15,2.2),(1.65,.14,3.95),'navy',.06)
   for z in [.36,4.05]:box('Locker inset trim',(x,8.05,z),(1.48,.04,.04),'brass',.006)
   for dx in [-.73,.73]:box('Locker door side trim',(x+dx,8.05,2.20),(.04,.04,3.7),'brass',.006)
   box('Brass locker handle',(x+.54,7.97,2.15),(.07,.12,.40),'brass',.025)
   for z in [3.5,3.62,3.74]:box('Locker vent',(x,8.03,z),(.6,.05,.028),'wood',.006)
  bench(-5,4.8);bench(6,4.8);planter(9,1.5)
  box('Changing mat',(3,2,.07),(4,2,.045),'navy',.02)
  box('Ceiling edge',(0,9,5.5),(22,2,.24),'wood')
 elif room=='loading':
  box('Tennis court',(0,16,.035),(14,20,.045),'court',0)
  for x in [-6,6]:box('Court sideline',(x,16,.07),(.08,18,.015),'white',0)
  for y in [7,16,25]:box('Court baseline',(0,y,.075),(12,.08,.015),'white',0)
  box('Service centre',(0,16,.08),(.08,10,.015),'white',0)
  for x in [-7,7]:cyl('Net post',(x,16,.65),.075,1.3,'navy',12)
  beam('Net tape',(-7,16,1.25),(7,16,1.25),.07,'white')
  for x in range(-7,8):beam('Net vertical',(x,16,.25),(x,16,1.2),.016,'navy')
  for z in [.3,.55,.8,1.05]:beam('Net horizontal',(-7,16,z),(7,16,z),.016,'navy')
  bench(8,3);planter(-8,1);palm(-11,13,1);palm(11,20,1.3)
  for x in [-9,9]:
   for y in [10,17,25]:beam('Court fence post',(x,y,0),(x,y,2.5),.055,'navy')
   for z in [.6,1.4,2.5]:beam('Court fence rail',(x,10,z),(x,25,z),.035,'navy')
  # Foreground practice basket with modeled rim and balls.
  cyl('Ball basket',(7,-.7,.7),.4,.65,'wood');cyl('Basket rim',(7,-.7,1.04),.43,.08,'navy')
  for dx,dy in [(-.17,0),(.12,.1),(0,-.15)]:sphere('Tennis ball',(7+dx,-.7+dy,1.08),(.12,.12,.12),'green')

def project_and_bake(room):
 bpy.ops.object.select_all(action='DESELECT')
 for o in objects:o.select_set(True)
 bpy.context.view_layer.objects.active=objects[0];bpy.ops.object.join();o=bpy.context.object;o.name='Club_'+room
 bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
 for layer in list(o.data.uv_layers):o.data.uv_layers.remove(layer)
 uv=o.data.uv_layers.new(name='ProjectionUV')
 for p in o.data.polygons:
  name=o.material_slots[p.material_index].material.name.split('.')[0];tile=PAL[name][1]
  if tile is None:continue
  normal=p.normal;axis=max(range(3),key=lambda k:abs(normal[k]));axes=[k for k in range(3) if k!=axis]
  # Orthographic planar material projection. Repeat in world space; bake into unique UVs below.
  for li in p.loop_indices:
   co=o.data.vertices[o.data.loops[li].vertex_index].co
   a=(co[axes[0]]*.42)%1;b=(co[axes[1]]*.42)%1
   uv.data[li].uv=((tile%2+.025+.95*a)/2,(2-tile//2+.025+.95*b)/3)
 target=o.data.uv_layers.new(name='BakedUV');o.data.uv_layers.active=target;target.active_render=True
 bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.uv.smart_project(angle_limit=math.radians(66),island_margin=.006);bpy.ops.object.mode_set(mode='OBJECT')
 # Allocate texels by visibility: foreground props and paving outrank the sea/foundation.
 # Group connected UV islands, scale their area, then repack. This is baked once, not runtime LOD.
 uvl=o.data.uv_layers.active.data;parent=list(range(len(uvl)))
 def root(i):
  while parent[i]!=i:parent[i]=parent[parent[i]];i=parent[i]
  return i
 def union(a,b):parent[root(a)]=root(b)
 seen={}
 for poly in o.data.polygons:
  for li in poly.loop_indices:
   union(poly.loop_indices[0],li);key=tuple(round(v,6) for v in uvl[li].uv)
   if key in seen:union(seen[key],li)
   else:seen[key]=li
 groups={}
 for li in range(len(uvl)):groups.setdefault(root(li),[]).append(li)
 for ids in groups.values():
  coords=[o.data.vertices[o.data.loops[li].vertex_index].co for li in ids]
  y=sum(c.y for c in coords)/len(coords);span=max(max(c[k] for c in coords)-min(c[k] for c in coords) for k in range(3))
  factor=.02 if y>30 else .08 if span>25 else .3 if y>16 else 1.5 if y>6 else 3.0
  center=sum((uvl[li].uv for li in ids),Vector((0,0)))/len(ids)
  for li in ids:uvl[li].uv=center+(uvl[li].uv-center)*factor
 bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.uv.pack_islands(rotate=True,margin=.003);bpy.ops.object.mode_set(mode='OBJECT')
 atlas=bpy.data.images.new('Club_'+room+'_Albedo',width=2048,height=2048,alpha=False)
 for slot in o.material_slots:
  n=slot.material.node_tree.nodes;tx=n.new('ShaderNodeTexImage');tx.image=atlas;tx.label='UV_BAKE_TARGET';n.active=tx
 scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=1;scene.render.bake.margin=8;scene.render.bake.use_clear=True
 bpy.ops.object.bake(type='EMIT')
 atlas.filepath_raw=str(OUT/('Club_'+room+'_Albedo.png'));atlas.file_format='PNG';atlas.save()
 # Runtime material is shared across the scene: one draw material + baked atlas, dynamic lights.
 baked=bpy.data.materials.new('Baked_'+room);baked.use_nodes=True;nodes=baked.node_tree.nodes;bs=nodes.get('Principled BSDF');bs.inputs['Roughness'].default_value=.78
 tex=nodes.new('ShaderNodeTexImage');tex.image=atlas;baked.node_tree.links.new(tex.outputs['Color'],bs.inputs['Base Color'])
 o.data.materials.clear();o.data.materials.append(baked)
 for poly in o.data.polygons:poly.material_index=0
 o.data.uv_layers.remove(o.data.uv_layers['ProjectionUV']);o.data.uv_layers.active=o.data.uv_layers['BakedUV']
 o.data.calc_loop_triangles();pos=[];norm=[];uvs=[];indices=[]
 for tri in o.data.loop_triangles:
  for li in tri.loops:
   v=o.data.vertices[o.data.loops[li].vertex_index];n=o.data.corner_normals[li].vector;t=o.data.uv_layers.active.data[li].uv
   pos.extend([round(v.co.x,5),round(v.co.z,5),round(-v.co.y,5)]);norm.extend([round(n.x,5),round(n.z,5),round(-n.y,5)]);uvs.extend([round(t.x,6),round(1-t.y,6)]);indices.append(len(indices))
 camera=(0,-12,4.3) if room!='locker' else (0,-10,3.6);look=(0,5,2.0) if room!='locker' else (0,7,2.0)
 data={'positions':pos,'normals':norm,'uv':uvs,'triangles':indices,'texture':'Club_'+room+'_Albedo','camera':[camera[0],camera[2],-camera[1]],'target':[look[0],look[2],-look[1]],'room':room}
 (OUT/('Club_'+room+'.json')).write_text(json.dumps(data,separators=(',',':')))
 # Editable authoring scene and camera render for mesh review.
 scene.world.color=(.3,.4,.5);scene.render.engine='CYCLES';scene.cycles.samples=16
 world=scene.world;world.use_nodes=True;world.node_tree.nodes['Background'].inputs[0].default_value=(.48,.66,.8,1);world.node_tree.nodes['Background'].inputs[1].default_value=.5
 bpy.ops.object.light_add(type='AREA',location=(-6,-3,12));bpy.context.object.data.energy=2300;bpy.context.object.data.shape='DISK';bpy.context.object.data.size=8;bpy.context.object.rotation_euler=(Vector((0,5,0))-bpy.context.object.location).to_track_quat('-Z','Y').to_euler()
 bpy.ops.object.camera_add(location=camera);cam=bpy.context.object;cam.rotation_euler=(Vector(look)-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.lens=24;scene.camera=cam
 scene.render.resolution_x=1280;scene.render.resolution_y=720;scene.render.resolution_percentage=100;scene.view_settings.view_transform='Standard'
 source.pack();atlas.pack();bpy.ops.wm.save_as_mainfile(filepath=str(ART/'blender'/('Club_'+room+'.blend')))
 scene.render.filepath=str(ART/'review'/(room+'-geometry.png'));bpy.ops.render.render(write_still=True)
 return {'room':room,'triangles':len(indices)//3,'atlas':'2048x2048','projection':'six orthographic ImageGen material panels -> unique BakedUV emission bake','objects_before_join':len(objects)}
reports=[]
for room in ['entrance','terrace','locker','loading']:
 bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False);objects.clear();build(room);reports.append(project_and_bake(room))
(ART/'bake-report.json').write_text(json.dumps(reports,indent=2))
