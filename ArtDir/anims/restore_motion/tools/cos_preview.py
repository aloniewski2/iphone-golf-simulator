import bpy,sys,os,math
sys.path.insert(0,os.path.dirname(__file__))
from common import render
from mathutils import Vector
P=os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
bpy.ops.wm.open_mainfile(filepath=P+'/../cosmetics/Hero_Cosmetics_v1.blend')
def show(names):
 for o in bpy.data.objects:
  if o.type=='MESH':o.hide_render=o.name not in names
base=['Body_Skin','Body_EyeSphere_L','Body_EyeSphere_R','Hair_Default','Shirt_Default','Shorts_Default','Shoes_Default']
h=Vector((0,.05,1.45))
for tag,extra in [('visor',['Hat_Visor']),('cap',['Hat_Cap']),('band',['Hat_Sweatband','Hat_SweatbandStripe'])]:
 show(base+extra)
 for i,d in enumerate([(0,-1.1,.15),(1.1,0,.1),(0,1.1,.2),(.7,-.7,.6)]):
  render(P+f'/preview/cos_{tag}_{i}.png',h+Vector(d),h,lens=50,res=300)
