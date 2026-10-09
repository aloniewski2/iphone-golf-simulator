import bpy,pathlib,math,json
from mathutils import Vector
from mathutils.bvhtree import BVHTree
R=pathlib.Path.cwd();O=R/'work/reference-rebuild/characters';changes={}
def smooth(a,b,x):
 t=max(0,min(1,(x-a)/(b-a)));return t*t*(3-2*t)
def gauss(x,s):return math.exp(-(x/s)**2)
for sex,label in [('Male','male_animation'),('Female','female_body_stylized')]:
 bpy.ops.wm.open_mainfile(filepath=str(O/f'{label}_portrait.blend'));head=next(o for o in bpy.data.objects if o.type=='MESH' and o.name.startswith('GEO-') and ('iris' not in o.name and 'sclera' not in o.name and '.eye.' not in o.name));base=head.shape_key_add(name='Artist_Basis');key=head.shape_key_add(name='Reference_Form1');key.value=1;maximum=0;touched=0
 eyeObjects=[o for o in bpy.data.objects if o.type=='MESH' and ('.eye.' in o.name or '.sclera.' in o.name)];eyeCentres=[sum((v.co for v in o.data.vertices),Vector())/len(o.data.vertices) for o in eyeObjects]
 for i,v in enumerate(base.data):
  p=v.co.copy();q=p.copy();x,y,z=p
  if sex=='Male':
   # True orbital skin and lid loops expand together around the original eye centers.
   c=min(eyeCentres,key=lambda c:abs(c.x-x));dx=x-c.x;dz=z-c.z;w=gauss(dx,.030)*gauss(dz,.032)*smooth(.065,-.005,y)
   q.x+=dx*.30*w;q.z+=dz*.30*w;q.y-=.0015*w
   lower=smooth(-.020,-.065,z)*(1-smooth(.145,.190,-z))*smooth(.065,-.015,y)
   q.x+=x*.10*lower;q.z+=(z+.035)*(-.10)*lower
   cheek=gauss(abs(x)-.066,.030)*gauss(z+.040,.035)*smooth(.04,-.01,y);q.y-=.0025*cheek
   smile=gauss(abs(x)-.025,.014)*gauss(z+.080,.014)*smooth(.025,-.040,y);q.z+=.0030*smile
  else:
   # Ear contraction is anchored at the scalp attachment, leaving central anatomy unchanged.
   ear=smooth(.092,.113,abs(x))*gauss(z+.005,.067)*smooth(-.080,-.015,y)
   anchor=math.copysign(.092,x);q.x+=(anchor+(x-anchor)*.63-x)*ear;q.z+=(-.005+(z+.005)*.65-z)*ear
   nose=gauss(x,.028)*gauss(z+.050,.038)*smooth(-.010,-.065,y);q.x-=x*.28*nose;q.y+=.0060*nose
   crown=smooth(.073,.137,z);q.x-=x*.080*crown
   smile=gauss(abs(x)-.028,.016)*gauss(z+.111,.014)*smooth(.02,-.040,y);q.z+=.0028*smile
   lip=smooth(-.110,-.117,z)*(1-smooth(-.132,-.143,z))*gauss(x,.035)*smooth(-.025,-.060,y)
   q.z+=(-.112+(z+.112)*.75-z)*lip;q.y+=.0015*lip
  key.data[i].co=q;delta=(q-p).length;maximum=max(maximum,delta);touched+=delta>.0001
 # Eyeballs retain their actual spherical/detailed surfaces and fitted centers.
 if sex=='Male':
  for ob in bpy.data.objects:
   if ob.type=='MESH' and ('.sclera.' in ob.name or '.iris.' in ob.name):
    centre=min(eyeCentres,key=lambda c:abs(c.x-sum(v.co.x for v in ob.data.vertices)/len(ob.data.vertices)))
    for v in ob.data.vertices:v.co=centre+(v.co-centre)*1.30
    ob.data.update()
 else:
  for old in list(bpy.data.objects):
   if old.name.startswith('Pupil_Candidate'):bpy.data.objects.remove(old,do_unlink=True)
  brown=next(m for m in bpy.data.materials if m.name.startswith('BrownIris'));black=next(m for m in bpy.data.materials if m.name.startswith('DeepPupil'))
  for ob in eyeObjects:
   for poly in ob.data.polygons:poly.material_index=0
   points=[v.co.copy() for v in ob.data.vertices];centre=Vector(((min(v.x for v in points)+max(v.x for v in points))*.5,(min(v.y for v in points)+max(v.y for v in points))*.5,(min(v.z for v in points)+max(v.z for v in points))*.5));bv=BVHTree.FromPolygons(points,[poly.vertices[:] for poly in ob.data.polygons]);front=min(v.y for v in points)
   verts=[];faces=[];roles=[];rings=[0,.003,.006,.009]+[.009+(i+1)*(.0245-.009)/12 for i in range(12)];segments=64
   for row,radius in enumerate(rings):
    for j in range(segments):
     theta=j*math.tau/segments;x=centre.x+radius*math.cos(theta);z=centre.z+radius*math.sin(theta);hit,normal,_,_=bv.ray_cast(Vector((x,-1,z)),Vector((0,1,0)));y=hit.y-.00030 if hit is not None else front-.00030+radius*radius/.075;verts.append((x,y,z))
   for row in range(len(rings)-1):
    for j in range(segments):
     a=row*segments+j;b=row*segments+(j+1)%segments;c=b+segments;d=a+segments;faces.append((a,b,c,d));roles.append(1 if rings[row+1]<=.009 else 0)
   me=bpy.data.meshes.new('ContinuousIris');me.from_pydata(verts,[],faces);me.materials.append(brown);me.materials.append(black);iris=bpy.data.objects.new('Eye_Iris_Candidate_'+ob.name,me);bpy.context.scene.collection.objects.link(iris)
   for poly,role in zip(me.polygons,roles):poly.material_index=role;poly.use_smooth=True
 # Solid skin-fitted brows, thicker and nearly straight for male, gentle arch female.
 for ob in bpy.data.objects:
  if not ob.name.startswith('Brow_Candidate'):continue
  vs=ob.data.vertices
  for start in range(0,len(vs),8):
   ids=list(range(start,min(start+8,len(vs))));c=sum((vs[i].co for i in ids),Vector())/len(ids);a=start/max(8,len(vs)-8);arch=.004*math.sin(a*math.pi)
   for i in ids:
    vs[i].co.z=c.z+(vs[i].co.z-c.z)*(2.35 if sex=='Male' else 2.0)-(arch*.65 if sex=='Male' else 0)
  ob.data.update()
 sc=bpy.context.scene;sc.camera.data.ortho_scale=.39 if sex=='Male' else .41;target=Vector((0,0,.006))
 for angle,loc in [('front',(0,-1.1,.010)),('threequarter',(.58,-1.1,.025))]:
  sc.camera.location=loc;sc.camera.rotation_euler=(target-sc.camera.location).to_track_quat('-Z','Y').to_euler();sc.render.filepath=str(O/f'{sex.lower()}_reference_form1_{angle}.png');bpy.ops.render.render(write_still=True)
 bpy.ops.wm.save_as_mainfile(filepath=str(O/f'{sex.lower()}_reference_form1.blend'));changes[sex]={'source':label,'headChangedVertices':touched,'maximumDisplacementM':maximum,'shapeKey':'Reference_Form1'}
(O/'reference-form1-changes.json').write_text(json.dumps(changes,indent=2));print(changes)
