"""CPU proof of the actual coastal source silhouettes. No Assets writes."""
import bpy,sys,math
from pathlib import Path
from mathutils import Vector
R=Path(__file__).resolve().parents[3]
args=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else []
OUT=R/'work/reference-rebuild/botany'
if '--out' in args:OUT=Path(args[args.index('--out')+1]);OUT=OUT if OUT.is_absolute() else R/OUT
source=OUT/'CoastalBotany.blend';bpy.ops.wm.open_mainfile(filepath=str(source))
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.device='CPU';scene.cycles.samples=20;scene.render.threads_mode='FIXED';scene.render.threads=3
scene.render.resolution_x=1280;scene.render.resolution_y=900;scene.render.resolution_percentage=100
scene.world.use_nodes=True;scene.world.node_tree.nodes['Background'].inputs['Color'].default_value=(.40,.53,.64,1);scene.world.node_tree.nodes['Background'].inputs['Strength'].default_value=.65
scene.view_settings.view_transform='AgX';scene.view_settings.look='AgX - Medium High Contrast'
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.035));floor=bpy.context.object;floor.name='PROOF_FLOOR'
mat=bpy.data.materials.new('Neutral olive proof floor');mat.diffuse_color=(.22,.28,.18,1);mat.use_nodes=True;mat.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value=mat.diffuse_color;mat.node_tree.nodes['Principled BSDF'].inputs['Roughness'].default_value=.87;floor.data.materials.append(mat)
bpy.ops.object.light_add(type='SUN',location=(-10,-15,24));sun=bpy.context.object;sun.data.energy=2.0;sun.data.angle=math.radians(12);sun.rotation_euler=(math.radians(24),math.radians(-21),math.radians(-28))
bpy.ops.object.camera_add();cam=bpy.context.object;scene.camera=cam;cam.data.type='PERSP';cam.data.lens=47
plants=[o for o in bpy.data.objects if o.type=='MESH' and o!=floor]
def shot(name,selected,from_,at):
 for o in plants:o.hide_render=o.name not in selected
 cam.location=from_;cam.rotation_euler=(Vector(at)-cam.location).to_track_quat('-Z','Y').to_euler();scene.render.filepath=str(OUT/(name+'.png'));bpy.ops.render.render(write_still=True)
for n,x in [('CONIFER_A',-9),('CONIFER_B',0),('CONIFER_C',9),('CONIFER_A_FAR',-9),('CONIFER_B_FAR',0),('CONIFER_C_FAR',9)]:bpy.data.objects[n].location.x=x
shot('conifer-near',['CONIFER_A','CONIFER_B','CONIFER_C'],(21,-42,20),(0,0,6.5))
shot('conifer-far',['CONIFER_A_FAR','CONIFER_B_FAR','CONIFER_C_FAR'],(21,-42,20),(0,0,6.5))
for n,x in [('SHRUB_A',-2.8),('SHRUB_B',.3),('FERN',3.3),('SHRUB_A_FAR',-2.8),('SHRUB_B_FAR',.3),('FERN_FAR',3.3)]:bpy.data.objects[n].location.x=x
shot('understory-near',['SHRUB_A','SHRUB_B','FERN'],(5,-10,5.8),(0,0,.5))
shot('understory-far',['SHRUB_A_FAR','SHRUB_B_FAR','FERN_FAR'],(5,-10,5.8),(0,0,.5))
