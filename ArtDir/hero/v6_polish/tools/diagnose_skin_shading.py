import bpy,sys
from pathlib import Path
R=Path(__file__).resolve().parents[4];O=R/'ArtDir/hero/v6_polish';bpy.ops.wm.open_mainfile(filepath=str(O/'Hero_V6_Polish.blend'))
m=bpy.data.materials['skin_WarmSkin'];m=m.copy();m.name='DIAGNOSTIC uniform skin'
b=m.node_tree.nodes.get('Principled BSDF')
for n in ['Base Color','Normal']:
 for l in list(b.inputs[n].links):m.node_tree.links.remove(l)
b.inputs['Base Color'].default_value=(1,.745,.539,1);b.inputs['Roughness'].default_value=.6
ob=bpy.data.objects['Body_Skin']
for i,x in enumerate(ob.data.materials):
 if x and x.name=='skin_WarmSkin':ob.data.materials[i]=m
sys.argv=['diagnostic','--',str(R/'ArtDir/hero/v6_proof/shading_diagnostic')]
import os;os.environ['QUICK']='1';exec((R/'ArtDir/hero/v5_tools/render_review.py').read_text())
