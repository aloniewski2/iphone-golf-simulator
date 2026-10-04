import bpy,bmesh,math
from pathlib import Path
from mathutils import Vector
R=Path(__file__).resolve().parents[4];O=R/'ArtDir/hero/v6_polish';bpy.ops.wm.open_mainfile(filepath=str(O/'Hero_V6_Polish.blend'))
for name in ['Hair_Default','Hair_Default_Free']:
 ob=bpy.data.objects[name];bm=bmesh.new();bm.from_mesh(ob.data)
 for k in range(5):
  y=.025+k*.035;pts=[Vector((.14,y,1.625)),Vector((.10,y-.10,1.79)),Vector((-.08,y-.15,1.79-k*.007)),Vector((-.205,y-.15,1.69-k*.009))];rings=[]
  def bez(t):return (1-t)**3*pts[0]+3*t*(1-t)**2*pts[1]+3*t*t*(1-t)*pts[2]+t**3*pts[3]
  for j in range(17):
   t=j/16;p=bez(t);tangent=(bez(min(1,t+.01))-bez(max(0,t-.01))).normalized();side=Vector((0,1,0));normal=tangent.cross(side).normalized();w=.041*(.75+.25*math.sin(math.pi*t))*(1-t**3.2)+.0001;h=.020*(1-t**2)+.0001
   ring=[]
   for i in range(12):a=i*math.pi/6;ring.append(bm.verts.new(p+side*(math.cos(a)*w)+normal*(math.sin(a)*h)))
   rings.append(ring)
  for j in range(16):
   for i in range(12):f=bm.faces.new((rings[j][i],rings[j][(i+1)%12],rings[j+1][(i+1)%12],rings[j+1][i]));f.smooth=True;f.material_index=1+k%3
  bm.faces.new(tuple(reversed(rings[0])));bm.faces.new(tuple(rings[-1]))
 bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(ob.data);bm.free();g=ob.vertex_groups.get('Head');g.add(list(range(len(ob.data.vertices))),1,'REPLACE')
 bpy.context.view_layer.objects.active=ob;d=ob.modifiers.new('Closed lock mobile budget','DECIMATE');d.ratio=.66;bpy.ops.object.modifier_apply(modifier=d.name)
 bpy.ops.object.select_all(action='DESELECT');ob.select_set(True);bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.uv.smart_project(angle_limit=math.radians(65),island_margin=.015);bpy.ops.object.mode_set(mode='OBJECT')
bpy.ops.wm.save_as_mainfile(filepath=str(O/'Hero_V6_Polish.blend'))
