"""Approved v2 resort kit. Executed in the environment builder's namespace.
All plants, arches, furnishings and courts below are triangle geometry, never backdrop cards.
"""
PAL.update({'leaf':('4a7d39',None),'leaflight':('73984e',None),'leafdark':('315f35',None),'pink':('db7775',None),'flower':('ead99c',None),'bark':('857154',1),'court':('427eaa',None),'courtouter':('698d6e',None),'glass':('41616b',None),'tile':('284f66',None),'lemon':('e8c956',None)})
for key in PAL:
 if key not in M:M[key]=mat(key)
# Correct the previous placeholder court/glass palettes too.
for key in ['court','glass']:M[key]=mat(key)

def mesh_object(name,verts,faces,material,smooth=False):
 mesh=bpy.data.meshes.new(name);mesh.from_pydata(verts,[],faces);mesh.update();o=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(o)
 if smooth:
  for p in mesh.polygons:p.use_smooth=True
 return finish(o,name,material)

def rod(name,a,b,r,material,sides=8):
 a,b=Vector(a),Vector(b);length=(b-a).length
 bpy.ops.mesh.primitive_cylinder_add(vertices=sides,radius=r,depth=length,location=(a+b)/2);o=bpy.context.object;o.rotation_euler=(b-a).to_track_quat('Z','Y').to_euler()
 return finish(o,name,material)

def leaf(name,base,tip,width,material='leaf',bend=.1):
 a,b=Vector(base),Vector(tip);delta=b-a;side=delta.cross(Vector((0,0,1))).normalized()
 if side.length<.1:side=Vector((1,0,0))
 verts=[];faces=[]
 for i in range(5):
  t=i/4;center=a+delta*t+Vector((0,0,bend*math.sin(t*math.pi)));w=width*math.sin(math.pi*t)**.8
  verts.extend([tuple(center-side*w),tuple(center+Vector((0,0,.07*width))),tuple(center+side*w)])
 for i in range(4):
  j=i*3;faces.extend([(j,j+3,j+4,j+1),(j+1,j+4,j+5,j+2)])
 return mesh_object(name,verts,faces,material,True)

def palm(x,y,size=1):
 top=Vector((x+.32*size,y,5.4*size));base=Vector((x,y,0))
 # Curved tapered segmented trunk with subtle ring collars.
 for i in range(7):
  a=base+(top-base)*(i/7);b=base+(top-base)*((i+1)/7)
  rod('Palm trunk',a,b,(.17-.06*i/7)*size,'bark',10)
  if y<20:rod('Palm trunk collar',b-Vector((0,0,.026)),b+Vector((0,0,.026)),(.185-.06*i/7)*size,'wood',10)
 for k in range(9):
  ang=k*math.tau/9+.15;axis=Vector((math.cos(ang),math.sin(ang),0));cross=Vector((-axis.y,axis.x,0));end=top+axis*2.8*size+Vector((0,0,-.5*size))
  for j in range(7):
   t=(j+.8)/8;center=top+axis*(2.8*size*t)+Vector((0,0,size*(.70*math.sin(math.pi*t)-.5*t)))
   if j<6:
    for side in [-1,1]:
     length=(.72*math.sin(math.pi*t)+.1)*size
     tip=center+cross*side*length+axis*.45*size+Vector((0,0,-.19*size))
     leaf('Palm feather',center,tip,.16*size,'leaflight' if k%3==0 else 'leaf',.07*size)
  for j in range(6):
   a=j/6;b=(j+1)/6
   pa=top+axis*(2.8*size*a)+Vector((0,0,size*(.70*math.sin(math.pi*a)-.5*a)))
   pb=top+axis*(2.8*size*b)+Vector((0,0,size*(.70*math.sin(math.pi*b)-.5*b)))
   rod('Palm frond rib',pa,pb,.017*size,'leaf',5)

def tree(x,y,size=1):
 rod('Broadleaf trunk',(x,y,0),(x,y,2.7*size),.14*size,'bark')
 for i in range(6):
  a=i*math.tau/6;cx=x+math.cos(a)*.9*size;cy=y+math.sin(a)*.8*size;z=(2.6+(i%2)*.6)*size
  rod('Tree branch',(x,y,1.7*size),(cx,cy,z),.07*size,'bark',6)
  sphere('Tree crown',(cx,cy,z),(.95*size,.82*size,.70*size),'leaflight' if i%3==0 else 'leaf')
  if y<28:
   for j in range(8):
    theta=j*math.tau/8;start=Vector((cx,cy,z));tip=start+Vector((math.cos(theta),math.sin(theta),.15))*.9*size
    leaf('Tree silhouette leaf',start,tip,.28*size,'leafdark' if j%3==0 else 'leaf',.1)

def shrub(x,y,z=.4,size=.7,flowers=False):
 for i in range(3):
  a=i*math.tau/3;sphere('Garden shrub',(x+math.cos(a)*size*.35,y+math.sin(a)*size*.35,z),(.56*size,.5*size,.48*size),'leaflight' if i==0 else 'leafdark')
 for k in range(7):
  a=k*math.tau/7;start=Vector((x,y,z));tip=start+Vector((math.cos(a)*size,math.sin(a)*size,.35))
  leaf('Garden leaf',start,tip,.21*size,'leaf',.13)
 if flowers:
  for i in range(5):
   a=i*math.tau/5;cx=x+.45*math.cos(a)*size;cy=y+.45*math.sin(a)*size;zz=z+.4*size
   for k in range(4):
    theta=k*math.tau/4;leaf('Coral flower petal',(cx,cy,zz),(cx+.18*math.cos(theta),cy+.18*math.sin(theta),zz+.03),.09,'pink',.05)

def planter(x,y,scale=1):
 cyl('Terracotta planter',(x,y,.4*scale),.52*scale,.80*scale,'clay',20);cyl('Planter rolled rim',(x,y,.80*scale),.58*scale,.12*scale,'clay',20)
 shrub(x,y,1.02*scale,.9*scale,True)
 for k in range(9):
  a=k*math.tau/9;leaf('Planter broad tropical leaf',(x,y,.8*scale),(x+math.cos(a)*.8*scale,y+math.sin(a)*.8*scale,(1.7+(k%3)*.22)*scale),.24*scale,'leaflight' if k%2 else 'leaf',.2)

def fern(x,y,z=0,size=1):
 for k in range(8):
  angle=k*math.tau/8;axis=Vector((math.cos(angle),math.sin(angle),0));side=Vector((-axis.y,axis.x,0));base=Vector((x,y,z))
  for j in range(5):
   t=(j+1)/6;c=base+axis*t*size+Vector((0,0,math.sin(t*math.pi)*.8*size));length=.28*size*math.sin(math.pi*t)
   for sign in [-1,1]:leaf('Fern leaflet',c,c+axis*.18*size+side*length*sign,.07*size,'leaf',.05)

def gardenbed(x,y,w=4,d=1.7):
 box('Raised stone planter',(x,y,.3),(w,d,.6),'stone',.06)
 box('Planter coping',(x,y,.64),(w+.12,d+.12,.13),'stucco',.035)
 box('Planting soil',(x,y,.72),(w-.25,d-.25,.03),'leafdark',0)
 for i in range(max(2,int(w))):shrub(x-w*.4+i*w*.8/max(1,int(w)-1),y,.9,.8,i%2==0)

def arch_trim(x,y,z,width,height,material='stone'):
 r=width/2;spring=z+height-r
 for dx in [-r,r]:box('Arch pier',(x+dx,y,z+(height-r)/2),(.19,.24,height-r),material,.035)
 verts=[];faces=[]
 for i in range(19):
  t=i*math.pi/18
  for radius in [r-.08,r+.13]:
   for dep in [-.12,.12]:verts.append((x+math.cos(t)*radius,y+dep,spring+math.sin(t)*radius))
 for i in range(18):
  j=i*4;faces.extend([(j,j+4,j+6,j+2),(j+1,j+3,j+7,j+5),(j,j+1,j+5,j+4),(j+2,j+6,j+7,j+3)])
 mesh_object('Arch curved molding',verts,faces,material,True)

def roof(x,y,z,w,d):
 # Two genuine sloped roof surfaces, ridge and modeled barrel-tile rows.
 rise=d*.27
 for side in [-1,1]:
  verts=[(x-w/2,y,z+rise),(x+w/2,y,z+rise),(x+w/2,y+side*d/2,z),(x-w/2,y+side*d/2,z)]
  mesh_object('Terracotta roof slope',verts,[(0,1,2,3)],'clay')
  for i in range(max(8,int(w/.5))):
   xx=x-w/2+i*w/max(8,int(w/.5))
   rod('Terracotta barrel ridge',(xx,y,z+rise+.05),(xx,y+side*d/2,z+.04),.10,'clay',6)
 rod('Roof central ridge',(x-w/2,y,z+rise+.12),(x+w/2,y,z+rise+.12),.14,'clay',8)

def striped_awning(x,y,z,w=3.6,d=1.8):
 for i in range(12):
  xx=x-w/2+w*i/12
  verts=[]
  for t in range(6):
   a=t*math.pi/2/5
   for dx in [0,w/12]:verts.append((xx+dx,y-d*math.sin(a),z-.65*(1-math.cos(a))))
  mesh_object('Awning stripe',verts,[(2*j,2*j+1,2*j+3,2*j+2) for j in range(5)],'navy' if i%2 else 'linen',True)
  box('Awning valance',(xx+w/24,y-d,z-.8),(w/12,.05,.30),'navy' if i%2 else 'linen',.025)

def lantern(x,y,z):
 box('Lantern wall mount',(x,y,z+.2),(.15,.14,.46),'brass',.025);box('Lantern warm glass',(x,y-.3,z),(.28,.25,.43),'flower',.02)
 for dx in [-.16,.16]:rod('Lantern brass rib',(x+dx,y-.45,z-.25),(x+dx,y-.45,z+.25),.024,'brass',6)
 box('Lantern cap',(x,y-.3,z+.26),(.39,.36,.06),'brass',.025)

def pavilion(x,y,w=8,d=5,z=0):
 box('Pavilion rear wall',(x,y+d/2,z+2),(w,.30,4),'stucco',.08)
 for dx in [-w/2,w/2]:box('Pavilion side wall',(x+dx,y,z+2),(.30,d,4),'stucco',.05)
 box('Pavilion cornice',(x,y-d/2,z+3.9),(w+.25,.38,.22),'stone')
 for xx in [x-w*.32,x,x+w*.32]:
  box('Recessed arch shadow',(xx,y+d*.38,z+1.4),(w*.25,.04,2.8),'glass',.02)
  arch_trim(xx,y-d/2,z,w*.28,3.6)
 roof(x,y,z+4.1,w+1,d+1)

def umbrella(x,y,z=0,size=1):
 rod('Parasole pole',(x,y,z),(x,y,z+2.8*size),.055*size,'wood',10)
 for k in range(12):
  a=k*math.tau/12;b=(k+1)*math.tau/12;r=1.5*size
  verts=[(x,y,z+3.1*size),(x+math.cos(a)*r,y+math.sin(a)*r,z+2.65*size),(x+math.cos(b)*r,y+math.sin(b)*r,z+2.65*size)]
  mesh_object('Striped parasol panel',verts,[(0,1,2)],'linen' if k%2 else 'navy')
  rod('Parasole spar',verts[0],verts[1],.02*size,'wood',6)
 cyl('Parasole base',(x,y,z+.05),.35*size,.10,'stone',16)

def table(x,y):
 cyl('Inlaid teak table',(x,y,.80),.70,.12,'wood',32);cyl('Table stem',(x,y,.39),.08,.76,'brass',16);cyl('Table foot',(x,y,.06),.32,.08,'wood',20)
 # Ceramic pitcher and a small cup with handles, modeled rather than drawn into albedo.
 cyl('Cream pitcher',(x+.13,y,.99),.11,.26,'linen',16);cyl('Pitcher navy band',(x+.13,y,1.01),.112,.055,'navy',16)
 rod('Pitcher handle',(x+.28,y,.95),(x+.28,y,1.13),.024,'linen',8)
 cyl('Drinking cup',(x-.2,y-.15,.94),.075,.17,'linen',14)

def lounge(x,y):
 box('Lounge base',(x,y,.34),(1.65,1.65,.18),'wood',.05)
 for dx in [-.7,.7]:
  for dy in [-.6,.6]:box('Lounge legs',(x+dx,y+dy,.18),(.12,.12,.36),'wood',.025)
 box('Lounge seat',(x,y,.58),(1.45,1.45,.31),'linen',.12)
 box('Lounge back',(x,y+.65,1.05),(1.65,.25,1.2),'wood',.06)
 box('Cream back cushion',(x,y+.46,1.05),(1.42,.30,.96),'linen',.12)
 for dx in [-.82,.82]:box('Lounge arm',(x+dx,y,.90),(.17,1.6,.16),'wood',.04)
 box('Navy throw pillow',(x+.34,y+.20,1.00),(.6,.27,.58),'navy',.13)
 for dx in [.18,.45]:box('Pillow stripe',(x+dx,y+.055,1.00),(.055,.017,.51),'linen',.01)

def towel(x,y,z):
 for i in range(3):
  box('Folded towel',(x,y,z+i*.11),(.74,.45,.10),'linen',.055)
  box('Towel navy stripe',(x-.25,y,z+.052+i*.11),(.06,.43,.015),'navy',.006)

def duffel(x,y,z):
 box('Navy tennis bag',(x,y,z+.25),(.90,.43,.50),'navy',.16)
 for dx in [-.3,.3]:
  box('Bag cream strap',(x+dx,y-.23,z+.25),(.07,.025,.39),'linen',.02)
  rod('Bag top handle',(x+dx,y-.05,z+.48),(x+dx,y-.05,z+.66),.025,'navy',8)
 rod('Bag handle grip',(x-.3,y-.05,z+.66),(x+.3,y-.05,z+.66),.03,'navy',8)

def bench(x,y):
 for i in range(7):box('Teak bench seat slat',(x-1.25+i*.416,y,.68),(.37,.72,.12),'wood',.025)
 for i in range(8):box('Teak bench back slat',(x-1.25+i*.357,y+.35,1.1),(.30,.12,.70),'wood',.025)
 for dx in [-1.2,1.2]:
  box('Bench leg',(x+dx,y,.34),(.14,.62,.68),'wood',.03);box('Bench arm',(x+dx,y,.98),(.16,.78,.12),'wood',.035)
 box('Bench upholstered seat',(x,y,.81),(2.3,.63,.18),'linen',.08)

def vine(x,y,z=0,height=5):
 for i in range(17):
  t=i/16;xx=x+math.sin(t*math.tau*2)*.18;yy=y+math.cos(t*math.tau*2)*.18;zz=z+t*height
  for side in [-1,1]:leaf('Climbing vine leaf',(xx,yy,zz),(xx+side*.45,yy-.12,zz+.20),.20,'leaf' if i%2 else 'leaflight',.09)
  if i%4==0:
   for k in range(4):
    a=k*math.tau/4;leaf('Vine blossom',(xx,yy-.1,zz),(xx+.14*math.cos(a),yy-.12,zz+.14*math.sin(a)),.09,'linen',.025)

def coastal_garden(clear_court=False):
 # Several elevations and a winding walk instead of a single floating island plane.
 box('Lower garden lawn',(0,26,-.5),(54,31,.55),'leaf',.05)
 for x,y,s in [(-24,48,10),(25,61,15),(8,75,18)]:
  sphere('Coastal headland',(x,y,-1),(s,s*.60,s*.25),'stone');sphere('Wooded headland',(x,y,.7),(s*.97,s*.57,s*.20),'leafdark')
 for x,y,s in [(-17,17,1.3),(-11,25,1.1),(-5,29,1.25),(4,33,1.3),(12,28,1.5),(19,21,1.4),(-22,35,1.5),(24,36,1.55)]:palm(x,y,s)
 for x,y,s in [(-15,31,1.2),(17,32,1.4),(-23,23,1),(25,19,1.2),(-7,39,1.4),(11,40,1.5)]:tree(x,y,s)
 for i in range(8):
  x=-14+4*i
  if not clear_court or abs(x)>9:shrub(x,17,.45,1.6,i%3==0)
 for y in ([] if clear_court else [15,21,27,33]):box('Garden walking terrace',(-5+math.sin(y*.2)*3,y,-.18),(4.5,6,.12),'stone',.03)


def court(x,y,scale=1):
 box('Court green surround',(x,y,-.04),(16*scale,25*scale,.09),'courtouter',0)
 box('Blue tennis surface',(x,y,.02),(12*scale,21*scale,.04),'court',0)
 for xx in [-5.5,5.5]:box('Tennis sideline',(x+xx*scale,y,.055),(.055*scale,19*scale,.008),'white',0)
 for yy in [-9.5,9.5,-4.5,4.5]:box('Tennis end service line',(x,y+yy*scale,.06),(11*scale,.055*scale,.008),'white',0)
 box('Tennis centre line',(x,y,.06),(.055*scale,9*scale,.009),'white',0)
 for xx in [-6,6]:rod('Net upright',(x+xx*scale,y,0),(x+xx*scale,y,1.12*scale),.045*scale,'navy',8)
 rod('Net white band',(x-6*scale,y,1.12*scale),(x+6*scale,y,1.12*scale),.036*scale,'white',6)
 for i in range(41):rod('Net cord',(x+(-6+i*.3)*scale,y,.20*scale),(x+(-6+i*.3)*scale,y,1.1*scale),.009*scale,'navy',4)
 for i in range(7):rod('Net horizontal cord',(x-6*scale,y,(.2+i*.15)*scale),(x+6*scale,y,(.2+i*.15)*scale),.009*scale,'navy',4)
 for xx in [-8,8]:
  for yy in [-12,0,12]:rod('Fence pole',(x+xx*scale,y+yy*scale,0),(x+xx*scale,y+yy*scale,2.7*scale),.05*scale,'navy',6)
  rod('Fence top',(x+xx*scale,y-12*scale,2.7*scale),(x+xx*scale,y+12*scale,2.7*scale),.035*scale,'navy',6)
 box('Court rear windscreen',(x,y+12*scale,1.3*scale),(16*scale,.035,2.6*scale),'leafdark',0)
 for xx in [-7,7]:
  rod('Court floodlight mast',(x+xx*scale,y+11*scale,0),(x+xx*scale,y+11*scale,5*scale),.055*scale,'navy',8)
  box('Court floodlight',(x+xx*scale,y+11*scale,5*scale),(.55*scale,.23*scale,.20*scale),'brass',.02)


def rich_build(room):
 floor()
 if room!='locker':sea()
 if room=='entrance':
  # Right clubhouse: an actual entrance recess, open arch, deep steps, tiled roof and awning.
  pavilion(7.0,8.5,8.5,6)
  striped_awning(7,5.5,3.8,4.3,1.6)
  for x in [2.1,11.7]:lantern(x,5.0,3.0)
  for i in range(3):box('Clubhouse broad step',(7,4.7-i*.6,.15*(3-i)),(10,.63,.30*(3-i)),'stone',.04)
  vine(2.5,5.2,0,4.2);vine(11.3,5.2,0,4.0)
  gardenbed(-6,2.7,4.0,2.1);planter(9.4,1.0,1.2);bench(-5,6.3);umbrella(-6,7.5,0,1.3)
  palm(-7,9,1.4);palm(-10,15,1.65);tree(-2,17,1.0)
  gardenbed(-6,13,4.5,1.8);gardenbed(7,18,5,1.8)
  coastal_garden();pavilion(-18,43,6,4,-.5)
 elif room=='terrace':
  rails(10)
  box('Terrace balustrade plinth',(0,10,.32),(22,.65,.64),'stone',.045)
  for x in [-7.2,8.2]:
   box('Pergola column',(x,3,2.9),(.46,.46,5.8),'stucco',.07);box('Pergola capital',(x,3,5.6),(.74,.74,.28),'stone',.04);vine(x,2.74,0,5.7)
  for y in [1,4,7]:box('Pergola crossbeam',(.5,y,5.85),(17,.30,.35),'wood',.045)
  for x in range(-8,10,2):box('Pergola rafter',(x,4,6.05),(.19,8,.25),'wood',.035)
  planter(-5.7,-.6,1.1);planter(7.5,1.2,1.0);lounge(6.7,-.1);table(5.4,-1.6)
  gardenbed(-7,6,3.3,1.5);gardenbed(8,6,3.5,1.6)
  coastal_garden();court(-3,28,.6);pavilion(16,25,10,6)
  umbrella(6,15,0,1.2);bench(7,14);umbrella(-12,21,0,1.1)
  tree(12,15,1.2);palm(-10,17,1.5)
 elif room=='locker':
  box('Locker rear wall',(0,10,2.8),(24,.4,5.6),'stucco',.05)
  box('Locker left wall',(-10,3,2.8),(.35,14,5.6),'stucco',.04)
  box('Locker ceiling',(0,4,5.8),(22,14,.25),'stucco',.02)
  for y in [-1,3,7]:box('Locker ceiling beam',(0,y,5.58),(22,.28,.35),'wood',.04)
  # Window opening cut into built sidewall by construction, not a blue painted rectangle.
  box('Right window sill wall',(10.5,4,.55),(.35,14,1.1),'stucco',.04)
  box('Right window top wall',(10.5,4,5.1),(.35,14,1.4),'stucco',.04)
  for y in [-2,2,6,10]:box('Window wall pier',(10.5,y,2.8),(.35,.28,5.6),'stucco',.04)
  for y in [0,4,8]:
   objs_start=len(objects);arch_trim(0,0,1.1,3.6,3.3,'wood')
   for o in objects[objs_start:]:
    # local arch plane x/z -> side wall y/z
    o.rotation_euler.z+=math.pi/2;o.location=Vector((10.3,y,0))+Vector((-o.location.y,o.location.x,o.location.z))
   rod('Window horizontal mullion',(10.2,y-1.7,2.6),(10.2,y+1.7,2.6),.04,'navy',6)
  # Actual garden outside the open windows.
  for x,y,s in [(13,1,1),(15,7,1.2),(12,12,1)]:palm(x,y,s)
  tree(15,5,1);box('Garden outside locker',(17,5,-.2),(12,20,.3),'leaf',0)
  for x in range(-8,10,2):
   box('Locker oak case',(x,9,2.15),(1.88,1.5,4.3),'wood',.06)
   box('Locker inset navy door',(x,8.18,2.15),(1.63,.16,3.93),'navy',.045)
   for z in [.28,4.0]:box('Locker brass trim',(x,8.07,z),(1.49,.035,.035),'brass',.006)
   for dx in [-.735,.735]:box('Locker brass side',(x+dx,8.07,2.14),(.035,.035,3.7),'brass',.006)
   box('Locker pull',(x+.53,7.98,2.05),(.07,.12,.4),'brass',.025)
   for z in [3.40,3.52,3.64]:box('Locker vent',(x,8.065,z),(.65,.035,.026),'brass',.004)
  box('Locker oak display shelf',(0,8,4.44),(20,1.1,.14),'wood',.04)
  for x in [-7,-2,4]:towel(x,7.9,4.56)
  # Plants cascading over cabinet, bench equipment, fern and shelves close to camera.
  planter(8.1,3.3,.9);fern(8.1,3.3,.85,1.3);bench(6.6,3.8);towel(5.7,3.8,.92);duffel(7.3,3.8,.93)
  bench(-5,4.7);box('Changing rug',(3,1,.08),(4.7,2.4,.055),'navy',.02)
  for x in [-4,2]:
   box('Shelf planter',(x,8,4.64),(1.1,.55,.35),'clay',.07);fern(x,8,4.80,.7)
  for z in [.6,1.7,2.8,3.9]:box('Equipment side shelf',(-8.4,.8,z),(2.5,.70,.13),'wood',.04)
  towel(-8.3,.8,.74);duffel(-8.2,.8,1.83);towel(-8.3,.8,2.94)
  # Clock and framed racket sketch use modeled hands/frame, no baked slogan.
  clock=cyl('Club clock face',(7,9.7,4.8),.38,.05,'linen',32);clock.rotation_euler.x=math.pi/2
  rod('Clock hour hand',(7,9.64,4.8),(6.85,9.64,4.98),.018,'navy',6);rod('Clock minute hand',(7,9.64,4.8),(7.22,9.64,4.85),.015,'navy',6)
  lantern(8.8,8.8,3.6)
 elif room=='loading':
  court(0,18,1)
  coastal_garden(clear_court=True);pavilion(13,33,12,6)
  for x,y,s in [(-10,9,1.25),(10,15,1.6),(-13,22,1.4),(15,24,1.5)]:palm(x,y,s)
  for x in [-10,10]:
   for y in [10,17,24]:shrub(x,y,.6,1.4,True)
  gardenbed(-7,1.8,3.3,1.4);planter(8,1.5,.9);bench(7,3.5);towel(6,3.5,.92);duffel(7.6,3.5,.92);umbrella(9,5,0,1.6)
  for x in [-7.5,-6.8]:rod('Umpire chair leg',(x,26,.1),(x,26,2.6),.045,'linen',8)
  box('Umpire high seat',(-7.15,26,2.2),(.9,.8,.13),'navy',.03)
  for z in [.4,.8,1.2,1.6,2.0]:rod('Umpire ladder rung',(-7.5,25.7,z),(-6.8,25.7,z),.03,'linen',8)
  cyl('Ball basket',(6.6,-.5,.56),.33,.64,'navy',16)
  for dx,dy in [(-.15,0),(.12,.05),(0,-.12)]:sphere('Basket tennis ball',(6.6+dx,-.5+dy,.94),(.12,.12,.12),'lemon')
  cyl('Water bottle',(6,3.25,1.15),.075,.40,'navy',16)

build=rich_build
