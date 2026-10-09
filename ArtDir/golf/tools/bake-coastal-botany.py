import bpy,sys,math,json
from pathlib import Path
from mathutils import Vector
import argparse
R=Path(__file__).resolve().parents[3]
p=argparse.ArgumentParser();p.add_argument('--cache',type=Path,default=R/'ArtDir/golf/vendor/polyhaven');p.add_argument('--out',type=Path,default=R/'ArtDir/golf/source/coastal-botany-generated')
a=p.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
V=a.cache.resolve();OUT=a.out.resolve();OUT.mkdir(parents=True,exist_ok=True)

bpy.ops.wm.open_mainfile(filepath=str(OUT/'CoastalBotany.blend'))
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.device='CPU';scene.cycles.samples=16;scene.render.threads_mode='FIXED';scene.render.threads=4
scene.render.resolution_x=1280;scene.render.resolution_y=1080;scene.render.resolution_percentage=100;scene.render.film_transparent=False
scene.world=bpy.data.worlds.new('Coastal proof environment');scene.world.use_nodes=True;scene.world.node_tree.nodes['Background'].inputs['Color'].default_value=(.45,.55,.65,1);scene.world.node_tree.nodes['Background'].inputs['Strength'].default_value=.65
scene.view_settings.view_transform='AgX';scene.view_settings.look='AgX - Medium High Contrast'
bpy.ops.mesh.primitive_plane_add(size=100,location=(0,0,-.035));floor=bpy.context.object;floor.name='PROOF_FLOOR';m=bpy.data.materials.new('Floor');m.diffuse_color=(.21,.26,.17,1);m.use_nodes=True;m.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value=m.diffuse_color;floor.data.materials.append(m)
bpy.ops.object.light_add(type='SUN');sun=bpy.context.object;sun.data.energy=2.4;sun.data.angle=math.radians(12);sun.rotation_euler=(math.radians(24),math.radians(-21),math.radians(-28))
bpy.ops.object.camera_add();cam=bpy.context.object;scene.camera=cam;cam.data.type='PERSP';cam.data.lens=50
plants=[o for o in bpy.data.objects if o.type=='MESH' and o!=floor]
def shot(name,selected,from_,at):
 for o in plants:o.hide_render=o.name not in selected
 cam.location=from_;cam.rotation_euler=(Vector(at)-cam.location).to_track_quat('-Z','Y').to_euler();scene.render.filepath=str(OUT/(name+'.png'));bpy.ops.render.render(write_still=True)
for l,x in zip('ABCD',[-10.5,-3.5,3.5,10.5]):bpy.data.objects['CONIFER_'+l].location.x=x
shot('artist-fir-near',['CONIFER_'+l for l in 'ABCD'],(15,-42,15),(0,0,7.5))
for l in 'ABCD':bpy.data.objects['CONIFER_'+l].location.x=0
shot('artist-fir-branch-close',['CONIFER_A'],(3.3,-4.5,7.5),(0,0,7))
for n,x in [('SHRUB_A',-2.8),('SHRUB_B',0),('FERN',3)]:bpy.data.objects[n].location.x=x
shot('artist-understory',['SHRUB_A','SHRUB_B','FERN'],(4,-9,4.4),(0,0,.6))
# Bake the exact artist tree at four yaw angles for a bounded distant cross.
scene.render.resolution_x=768;scene.render.resolution_y=1024;scene.render.film_transparent=True;floor.hide_render=True
scene.view_settings.view_transform='Standard';scene.view_settings.look='Medium High Contrast';sun.data.energy=.4;scene.world.node_tree.nodes['Background'].inputs['Strength'].default_value=1.1;cam.data.type='ORTHO'
(OUT/'maps').mkdir(exist_ok=True)
for l in 'ABCD':
 tree=bpy.data.objects['CONIFER_'+l];tree.location=(0,0,0)
 zmin=min(v.co.z for v in tree.data.vertices);zmax=max(v.co.z for v in tree.data.vertices);height=zmax-zmin
 width=max(max(v.co.x for v in tree.data.vertices)-min(v.co.x for v in tree.data.vertices),max(v.co.y for v in tree.data.vertices)-min(v.co.y for v in tree.data.vertices))*1.05
 cam.data.ortho_scale=height*1.08
 for i,ang in enumerate([0,math.pi/2,math.pi,3*math.pi/2]):
  cam.location=(math.sin(ang)*35,-math.cos(ang)*35,height*.5);cam.rotation_euler=(Vector((0,0,height*.5))-cam.location).to_track_quat('-Z','Y').to_euler()
  for o in plants:o.hide_render=o!=tree
  scene.render.filepath=str(OUT/'maps'/('fir_'+l+'_view'+str(i)+'.png'));bpy.ops.render.render(write_still=True)
