# Shared helpers: Unity-identical racket prop, grip socket, finger curl, preview render.
import bpy,math,json
from mathutils import Vector,Matrix,Quaternion
FINGERS=['Index','Middle','Ring','Pinky']
MR=Matrix(((-1,0,0),(0,1,0),(0,0,1)))
def socket_matrix(p):
 """Racket-local -> Hand.R bone-local (Blender, metres). Racket +Y = handle->head, +Z = string normal."""
 a=math.radians(p['tilt']);y=Vector((0,math.sin(a),-math.cos(a)));z=Vector((1,0,0))
 roll=math.radians(p.get('face_roll',0));z=(Quaternion(y,roll)@z)
 x=y.cross(z).normalized();z=x.cross(y).normalized()
 M=Matrix((x,y,z)).transposed().to_4x4();M.translation=Vector(p['pos'])-y*p['grip_y'];return M
def build_racket(name='Racket'):
 if name in bpy.data.objects:return bpy.data.objects[name]
 mats={}
 for n,c in [('Blue',(.08,.40,.85,1)),('Strings',(.83,.91,.97,1)),('Grip',(.055,.09,.15,1))]:
  m=bpy.data.materials.new('Racket_'+n);m.diffuse_color=c;m.use_nodes=True;m.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value=c;mats[n]=m
 parts=[]
 def rod(a,b,r,mat):
  a=Vector(a);b=Vector(b);d=b-a;bpy.ops.mesh.primitive_cylinder_add(vertices=12,radius=r,depth=d.length,location=(a+b)/2)
  o=bpy.context.object;o.rotation_mode='QUATERNION';o.rotation_quaternion=Vector((0,0,1)).rotation_difference(d.normalized());o.data.materials.append(mats[mat]);parts.append(o)
 rod((0,-.06,0),(0,.10,0),.017,'Grip');rod((0,.10,0),(0,.24,0),.013,'Blue')
 for i in range(32):
  a=i*math.pi*2/32;b=(i+1)*math.pi*2/32;rod((math.sin(a)*.135,.395+math.cos(a)*.18,0),(math.sin(b)*.135,.395+math.cos(b)*.18,0),.012,'Blue')
 for i in range(-4,5):
  x=i*.027;y=.18*math.sqrt(1-x*x/.135**2);rod((x,.395-y,0),(x,.395+y,0),.0017,'Strings')
 for i in range(-5,6):
  y=i*.028;x=.135*math.sqrt(1-y*y/.18**2);rod((-x,.395+y,0),(x,.395+y,0),.0017,'Strings')
 bpy.ops.object.select_all(action='DESELECT')
 for o in parts:o.select_set(True)
 bpy.context.view_layer.objects.active=parts[0];bpy.ops.object.join();r=bpy.context.object;r.name=name
 bpy.ops.object.transform_apply(location=True,rotation=True,scale=True);return r
def attach(racket,rig,bone,M):
 racket.parent=rig;racket.parent_type='BONE';racket.parent_bone=bone
 # BONE parenting is relative to bone tail; express socket in bone rest space.
 L=rig.data.bones[bone].length;racket.matrix_parent_inverse=Matrix.Translation((0,-L,0));racket.matrix_basis=M
def curl(rig,side,p):
 """Apply grip curl (degrees) to finger/thumb pose bones. Curl axis = bone local X (palm-ward)."""
 sgn=p.get('sign',1)
 for f in FINGERS:
  extra=p.get('spread',{}).get(f,0)
  for j,k in [('1','c1'),('2','c2')]:
   pb=rig.pose.bones[f+j+'.'+side];pb.rotation_mode='QUATERNION';pb.rotation_quaternion=Quaternion((1,0,0),math.radians(sgn*(p[k]+extra)))
 for j,k in [('1','t1'),('2','t2')]:
  pb=rig.pose.bones['Thumb'+j+'.'+side];pb.rotation_mode='QUATERNION'
  pb.rotation_quaternion=Quaternion(Vector(p.get('taxis',(1,0,0))).normalized(),math.radians(sgn*p[k]))
def render(path,cam_loc,target,lens=50,res=720,engine='BLENDER_WORKBENCH'):
 sc=bpy.context.scene;sc.render.engine=engine;sc.render.resolution_x=sc.render.resolution_y=res;sc.render.resolution_percentage=100;sc.render.film_transparent=False
 if engine=='BLENDER_WORKBENCH':
  sc.display.shading.light='STUDIO';sc.display.shading.color_type='MATERIAL';sc.display.shading.show_cavity=False
 cam=bpy.data.objects.get('PreviewCam')
 if not cam:
  cam=bpy.data.objects.new('PreviewCam',bpy.data.cameras.new('PreviewCam'));sc.collection.objects.link(cam)
 cam.data.lens=lens;cam.location=Vector(cam_loc);cam.rotation_mode='QUATERNION';cam.rotation_quaternion=(Vector(target)-Vector(cam_loc)).to_track_quat('-Z','Y');sc.camera=cam
 sc.render.filepath=str(path);bpy.ops.render.render(write_still=True)
