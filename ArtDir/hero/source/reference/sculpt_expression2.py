import bpy,bmesh,pathlib,math,json
from mathutils import Vector
from mathutils.bvhtree import BVHTree
R=pathlib.Path.cwd();O=R/'work/reference-rebuild/characters';reports={}
def smooth(a,b,x):
 t=max(0,min(1,(x-a)/(b-a)));return t*t*(3-2*t)
def g(x,s):return math.exp(-(x/s)**2)
for sex in ('male','female'):
 bpy.ops.wm.open_mainfile(filepath=str(O/f'{sex}_reference_form1.blend'));head=next(o for o in bpy.data.objects if o.type=='MESH' and o.name.startswith('GEO-') and all(t not in o.name for t in ('.iris.','.sclera.','.eye.')));base=head.data.shape_keys.key_blocks['Artist_Basis'];form=head.data.shape_keys.key_blocks['Reference_Form1'];key=head.shape_key_add(name='Reference_Expression2',from_mix=True);key.relative_key=form;key.value=1
 eyes=[o for o in bpy.data.objects if o.type=='MESH' and ('.sclera.' in o.name or '.eye.' in o.name)];centres=[Vector(((min(v.co.x for v in o.data.vertices)+max(v.co.x for v in o.data.vertices))*.5,(min(v.co.y for v in o.data.vertices)+max(v.co.y for v in o.data.vertices))*.5,(min(v.co.z for v in o.data.vertices)+max(v.co.z for v in o.data.vertices))*.5)) for o in eyes]
 if sex=='male':
  for i,v in enumerate(base.data):
   p=v.co;c=min(centres,key=lambda c:abs(c.x-p.x));dx=p.x-c.x;dz=p.z-c.z;w=g(dx,.030)*g(dz,.032)*smooth(.065,-.005,p.y);oldX=dx*.30*w;newX=sum((p.x-c.x)*.30*g(p.x-c.x,.030)*g(p.z-c.z,.032)*smooth(.065,-.005,p.y) for c in centres);key.data[i].co.x+=newX-oldX
  # Fill the actual glabella crease to the neighboring forehead envelope.
  old=[v.co.copy() for v in key.data]
  for i,p in enumerate(old):
   w=g(p.x,.012)*g(p.z-.026,.020)*smooth(.050,-.010,p.y)
   if w<.08:continue
   neighbors=[v.y for v in old if .008<abs(v.x)<.019 and abs(v.z-p.z)<.004 and v.y<.025]
   if neighbors:target=sum(neighbors)/len(neighbors);key.data[i].co.y+=max(-.005,min(.005,target-p.y))*.75*w
  # Preserve actual lid rims, gently fill/smooth under-eye trough skin.
  adj=[set() for _ in head.data.vertices]
  for e in head.data.edges:a,b=e.vertices;adj[a].add(b);adj[b].add(a)
  for step in range(16):
   old=[v.co.copy() for v in key.data]
   for i,p in enumerate(old):
    w=max(g(p.x-c.x,.030)*g(p.z-c.z+.022,.012) for c in centres)*smooth(.045,-.004,p.y)
    if w<.03 or not adj[i]:continue
    avg=sum((old[j] for j in adj[i]),Vector())/len(adj[i]);key.data[i].co=p+(avg-p)*(.18*w)
  for i,pv in enumerate(key.data):
   p=pv.co;x,y,z=p;smile=g(abs(x)-.027,.015)*g(z+.080,.013)*smooth(.025,-.036,y);pv.co.z+=.003*smile;cheek=g(abs(x)-.052,.035)*g(z+.047,.026)*smooth(.035,-.006,y);pv.co.z+=.0012*cheek;pv.co.y-=.001*cheek
   c=min(centres,key=lambda c:abs(c.x-x));relax=g(x-c.x,.024)*g(z-c.z,.016)*smooth(.025,-.008,y);pv.co.z-=math.copysign(.00095*relax,z-c.z)
  for ob in bpy.data.objects:
   if '.iris.' in ob.name:
    c=min(centres,key=lambda c:abs(c.x-sum(v.co.x for v in ob.data.vertices)/len(ob.data.vertices)))
    for v in ob.data.vertices:v.co.x=c.x+(v.co.x-c.x)*1.15;v.co.z=c.z+(v.co.z-c.z)*1.15
    ob.data.update()
 else:
  # Reduce the modeled lower-lip height and projection; retain closed-mouth anatomy.
  for i,pv in enumerate(key.data):
   p=pv.co.copy();x,y,z=p;lip=smooth(-.110,-.117,z)*(1-smooth(-.130,-.145,z))*g(x,.033)*smooth(-.025,-.055,y);pv.co.z+=(-.111+(z+.111)*.75-z)*lip;pv.co.y+=.0032*lip;smile=g(abs(x)-.028,.014)*g(z+.108,.013)*smooth(.010,-.035,y);pv.co.z+=.0030*smile
  for ob in bpy.data.objects:
   if ob.name.startswith('Brow_Candidate'):
    for start in range(0,len(ob.data.vertices),8):
     a=start/max(8,len(ob.data.vertices)-8);delta=-.006+.0020*math.sin(a*math.pi)
     for i in range(start,min(start+8,len(ob.data.vertices))):ob.data.vertices[i].co.z+=delta
    ob.data.update()
 # Refit physical brow centers onto the edited source forehead.
 bpy.context.view_layer.update();ev=head.evaluated_get(bpy.context.evaluated_depsgraph_get());me=ev.to_mesh();bv=BVHTree.FromPolygons([v.co for v in me.vertices],[p.vertices[:] for p in me.polygons]);ev.to_mesh_clear()
 for ob in bpy.data.objects:
  if not ob.name.startswith('Brow_Candidate'):continue
  for start in range(0,len(ob.data.vertices),8):
   ids=list(range(start,min(start+8,len(ob.data.vertices))));c=sum((ob.data.vertices[i].co for i in ids),Vector())/len(ids);hit,n,_,_=bv.ray_cast(Vector((c.x,-1,c.z)),Vector((0,1,0)))
   if hit is not None:
    delta=hit.y-.0018-c.y
    for i in ids:ob.data.vertices[i].co.y+=delta
  ob.data.update()
 sc=bpy.context.scene;target=Vector((0,0,.006))
 for angle,loc in [('front',(0,-1.1,.010)),('threequarter',(.58,-1.1,.025))]:
  sc.camera.location=loc;sc.camera.rotation_euler=(target-sc.camera.location).to_track_quat('-Z','Y').to_euler();sc.render.filepath=str(O/f'{sex}_expression2_{angle}.png');bpy.ops.render.render(write_still=True)
 bpy.ops.wm.save_as_mainfile(filepath=str(O/f'{sex}_reference_expression2.blend'));ds=[(key.data[i].co-form.data[i].co).length for i in range(len(key.data))];reports[sex]={'expressionShape':'Reference_Expression2','maximumExpressionDisplacementM':max(ds),'changedVertices':sum(x>.0001 for x in ds),'eyeAnchors':[[float(x) for x in c] for c in centres]}
(O/'expression2-manifest.json').write_text(json.dumps(reports,indent=2));print('EXPRESSION2',reports)
