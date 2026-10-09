"""CPU source inspection of the actual atlas-painted crafted clubhouse."""
import argparse,sys,math
from pathlib import Path
import bpy
from mathutils import Vector
parser=argparse.ArgumentParser();parser.add_argument('--source',type=Path,required=True);parser.add_argument('--out',type=Path,required=True)
args=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
args.out.mkdir(parents=True,exist_ok=True);bpy.ops.wm.open_mainfile(filepath=str(args.source))
root=Path(__file__).resolve().parents[4]
image=bpy.data.images.load(str(root/'Unity/Assets/Resources/Tennis/EnvV4/EnvV4_Palette.png'))
for mat in bpy.data.materials:
    mat.use_nodes=True;bsdf=mat.node_tree.nodes.get('Principled BSDF')
    tex=mat.node_tree.nodes.new('ShaderNodeTexImage');tex.image=image;tex.interpolation='Closest';tex.extension='EXTEND'
    mat.node_tree.links.new(tex.outputs['Color'],bsdf.inputs['Base Color'])
    bsdf.inputs['Roughness'].default_value=.68
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.device='CPU';scene.cycles.samples=32
scene.render.threads_mode='FIXED';scene.render.threads=3;scene.cycles.use_denoising=True
scene.render.resolution_x=1280;scene.render.resolution_y=800;scene.render.resolution_percentage=100
scene.view_settings.view_transform='AgX';scene.view_settings.look='AgX - Medium High Contrast'
world=bpy.data.worlds.new('Clear resort');world.use_nodes=True;scene.world=world
world.node_tree.nodes.get('Background').inputs['Color'].default_value=(.45,.65,.85,1)
world.node_tree.nodes.get('Background').inputs['Strength'].default_value=.45
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.015));floor=bpy.context.object
mat=bpy.data.materials.new('Warm plaza');mat.diffuse_color=(.74,.70,.60,1);floor.data.materials.append(mat)
data=bpy.data.lights.new('Afternoon sun','SUN');data.energy=2.0;data.angle=math.radians(5)
sun=bpy.data.objects.new('Afternoon sun',data);scene.collection.objects.link(sun);sun.location=(-25,-20,30)
sun.rotation_euler=(Vector((0,0,6))-sun.location).to_track_quat('-Z','Y').to_euler()
data=bpy.data.cameras.new('Source review');camera=bpy.data.objects.new('Source review',data);scene.collection.objects.link(camera);scene.camera=camera
for name,position,target,lens in [('threequarter',(23,-36,16),(0,0,6),48),('roof',(-16,-26,20),(-1,.5,9.3),62),('front',(0,-38,8),(0,0,6),45)]:
    camera.location=position;camera.rotation_euler=(Vector(target)-camera.location).to_track_quat('-Z','Y').to_euler();data.lens=lens
    scene.render.filepath=str(args.out/(name+'.png'));bpy.ops.render.render(write_still=True)
