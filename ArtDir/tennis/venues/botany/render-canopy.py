"""CPU-only source inspection in the actual tennis crown/trunk proportions."""
import argparse, math, sys
from pathlib import Path
import bpy
from mathutils import Vector
parser=argparse.ArgumentParser();parser.add_argument('--source',type=Path,required=True);parser.add_argument('--out',type=Path,required=True)
args=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else []);args.out.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(args.source))
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.device='CPU';scene.cycles.samples=32
scene.render.threads_mode='FIXED';scene.render.threads=3;scene.cycles.use_denoising=True
scene.render.resolution_x=768;scene.render.resolution_y=768;scene.render.resolution_percentage=100
scene.view_settings.view_transform='AgX';scene.view_settings.look='AgX - Medium High Contrast';scene.view_settings.exposure=0
scene.world=bpy.data.worlds.new('Coastal clear sky');scene.world.use_nodes=True
scene.world.node_tree.nodes.get('Background').inputs['Color'].default_value=(.40,.63,.86,1)
scene.world.node_tree.nodes.get('Background').inputs['Strength'].default_value=.40
for name in ('CANOPY','CANOPY_FAR'):
    obj=bpy.data.objects[name];pivot=obj.data.uv_layers['LeafVerticalPivot'];radius=5.5;height=10.0
    weights={loop.vertex_index:pivot.data[i].uv.x for i,loop in enumerate(obj.data.loops)}
    for vertex in obj.data.vertices:
        p=weights.get(vertex.index,-1)
        if p>=0:vertex.co.z=p+(vertex.co.z-p)*radius/height
    obj.scale=(radius,radius,height)
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.02));floor=bpy.context.object;floor.name='Proof floor'
mat=bpy.data.materials.new('Warm limestone');mat.diffuse_color=(.68,.69,.57,1);floor.data.materials.append(mat)
def light(name,kind,at,energy,colour,size=5):
    data=bpy.data.lights.new(name,kind);data.energy=energy;data.color=colour
    if kind=='AREA':data.shape='DISK';data.size=size
    obj=bpy.data.objects.new(name,data);scene.collection.objects.link(obj);obj.location=at
    obj.rotation_euler=(Vector((0,0,7))-obj.location).to_track_quat('-Z','Y').to_euler();return obj
sun=light('Warm afternoon sun','SUN',(-10,-13,22),2.1,(1,.94,.84));sun.data.angle=math.radians(8)
light('Camera sky fill','AREA',(7,-16,14),1500,(.74,.87,1),12)
camera_data=bpy.data.cameras.new('Source proof');camera=bpy.data.objects.new('Source proof',camera_data);scene.collection.objects.link(camera);scene.camera=camera
for role in ('CANOPY','CANOPY_FAR'):
    for name in ('CANOPY','CANOPY_FAR'):bpy.data.objects[name].hide_render=name!=role
    for view,at,target,lens in [('whole',(14,-26,10),(.5,0,6),46),('crown',(9,-13,11),(.2,0,6.7),56)]:
        camera.location=at;camera.rotation_euler=(Vector(target)-camera.location).to_track_quat('-Z','Y').to_euler();camera_data.lens=lens
        scene.render.filepath=str(args.out/(role.lower()+'-'+view+'.png'));bpy.ops.render.render(write_still=True)
