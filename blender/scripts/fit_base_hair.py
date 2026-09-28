"""Original fitted hair silhouettes for the supplied heads: smooth caps and shaped hems."""
import bpy,json,math
from pathlib import Path
from mathutils import Vector
R=Path(__file__).resolve().parents[2];U=R/'Unity/Assets/Resources/Tennis/Customization';N=R/'GolfArcade/Unity/CharacterAssets'
for sex in ['Male','Female']:
 bpy.ops.wm.open_mainfile(filepath=str(R/f'SportsLibrary/Customization/Bases/Player{sex}-rigged.blend'))
 for style in ['Swept','Curls','Bob']:
  vs=[];fs=[];sides=64;rings=20
  for i in range(rings):
   for j in range(sides):
    a=2*math.pi*j/sides;front=max(0,-math.sin(a))
    bottom=(1.345+.29*front**2.5) if style=='Bob' else (1.48+.15*front**2)
    theta_max=math.acos(max(-1,min(1,(bottom-1.47)/.255)))
    theta=.012+(theta_max-.012)*i/(rings-1)
    z=1.47+.255*math.cos(theta);r=math.sin(theta)
    if style=='Bob' and z<1.49:r=max(r,.965)
    vs.append((.233*r*math.cos(a),.016+.196*r*math.sin(a),z))
  for i in range(rings-1):
   for j in range(sides):fs.append((i*sides+j,i*sides+(j+1)%sides,(i+1)*sides+(j+1)%sides,(i+1)*sides+j))
  fs.append(tuple(reversed(range(sides))))
  # A rounded-in lower edge gives the cut thickness without copying ear/face contours.
  for j in range(sides):
   x,y,z=vs[(rings-1)*sides+j];vs.append((x*.965,(y-.016)*.965+.016,z+.009))
  for j in range(sides):fs.append(((rings-1)*sides+j,(rings-1)*sides+(j+1)%sides,rings*sides+(j+1)%sides,rings*sides+j))
  mesh=bpy.data.meshes.new(style);mesh.from_pydata(vs,[],[tuple(reversed(face)) for face in fs]);mesh.update();cap=bpy.data.objects.new(style,mesh);bpy.context.collection.objects.link(cap);parts=[cap]
  def lock(center,scale):
   bpy.ops.mesh.primitive_uv_sphere_add(segments=16,ring_count=10,location=center);o=bpy.context.object;o.scale=scale;bpy.ops.object.transform_apply(location=True,rotation=True,scale=True);parts.append(o)
  if style=='Curls':
   for i in range(2,rings-1,2):
    for j in range((i%4)*3,sides,6):
     v=vs[i*sides+j]
     if v[2]>1.51:lock(v,(.032,.032,.032))
  bpy.ops.object.select_all(action='DESELECT')
  for o in parts:o.select_set(True)
  bpy.context.view_layer.objects.active=cap
  if len(parts)>1:bpy.ops.object.join()
  for polygon in cap.data.polygons:polygon.use_smooth=True
  cap.data.calc_loop_triangles();p=[];n=[];uv=[];ix=[]
  for t in cap.data.loop_triangles:
   for vi in t.vertices:
    v=cap.data.vertices[vi];p.extend([round(v.co.x,6),round(v.co.z-1.22,6),round(-v.co.y,6)]);n.extend([round(v.normal.x,5),round(v.normal.z,5),round(-v.normal.y,5)]);uv.extend([0,0]);ix.append(len(ix))
  data=dict(positions=p,normals=n,uv=uv,triangles=ix,texture='',mask='',reference=[1,1,1,1]);name='Player'+sex+style
  for folder in [U,N]:(folder/(name+'.json')).write_text(json.dumps(data,separators=(',',':')))
  bpy.data.objects.remove(cap,do_unlink=True)
  print('HAIR',name,len(ix)//3,flush=True)
