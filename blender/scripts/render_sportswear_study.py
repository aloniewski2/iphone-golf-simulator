"""Render actual source meshes at three authored sizes for the visual iteration review."""
import bpy,json,math
from pathlib import Path
from mathutils import Vector
R=Path(__file__).resolve().parents[2]
O=Path('/tmp/sportswear-review');O.mkdir(exist_ok=True)
for sex in ['Female','Male']:
 bpy.ops.wm.open_mainfile(filepath=str(R/f'SportsLibrary/Customization/Bases/Player{sex}-rigged.blend'))
 body=next(o for o in bpy.data.objects if o.name.startswith('V4 Higgs body'))
 d=json.loads((R/f'GolfArcade/Unity/CharacterAssets/Player{sex}{"Bob" if sex=="Female" else "Swept"}.json').read_text())
 vs=[(d['positions'][i],-d['positions'][i+2],d['positions'][i+1]+1.22) for i in range(0,len(d['positions']),3)]
 fs=[d['triangles'][i:i+3] for i in range(0,len(d['triangles']),3)]
 mesh=bpy.data.meshes.new('Hair');mesh.from_pydata(vs,[],fs);mesh.update();hair=bpy.data.objects.new('Hair',mesh);bpy.context.collection.objects.link(hair)
 mat=bpy.data.materials.new('Chestnut hair');mat.diffuse_color=(.065,.025,.012,1);mat.use_nodes=True;mat.node_tree.nodes.get('Principled BSDF').inputs['Base Color'].default_value=mat.diffuse_color;mat.node_tree.nodes.get('Principled BSDF').inputs['Roughness'].default_value=.65;mesh.materials.append(mat)
 for p in mesh.polygons:p.use_smooth=True
 scene=bpy.context.scene;scene.world=bpy.data.worlds.new('Studio');scene.world.use_nodes=True;scene.world.node_tree.nodes.get('Background').inputs[0].default_value=(.3,.36,.45,1);scene.world.node_tree.nodes.get('Background').inputs[1].default_value=.45
 for pos,power in [((-3,-4,5),450),((3,-2,3),240),((1,3,4),350)]:
  bpy.ops.object.light_add(type='AREA',location=pos);o=bpy.context.object;o.data.energy=power;o.data.shape='DISK';o.data.size=4;o.rotation_euler=(Vector((0,0,1))-o.location).to_track_quat('-Z','Y').to_euler()
 bpy.ops.mesh.primitive_plane_add(size=200);floor=bpy.context.object;floor.location.z=-.015;m=bpy.data.materials.new('Floor');m.diffuse_color=(.16,.2,.27,1);floor.data.materials.append(m)
 bpy.ops.object.camera_add();cam=bpy.context.object;cam.data.type='ORTHO';cam.data.ortho_scale=2.05;scene.camera=cam
 scene.render.engine='CYCLES';scene.cycles.samples=16;scene.render.resolution_x=600;scene.render.resolution_y=760;scene.render.resolution_percentage=100
 scene.view_settings.view_transform='AgX'
 for size in [0,.5,1]:
  body.data.shape_keys.key_blocks['Slim'].value=max(0,1-size*2);body.data.shape_keys.key_blocks['Broad'].value=max(0,size*2-1)
  for view,pos in [('front',(0,-4,1.05)),('side',(4,-.2,1.05)),('back',(0,4,1.05))]:
   if sex=='Male' and view!='front':continue
   cam.location=pos;cam.rotation_euler=(Vector((0,0,.84))-cam.location).to_track_quat('-Z','Y').to_euler();scene.render.filepath=str(O/f'{sex}-{size}-{view}.png');bpy.ops.render.render(write_still=True)
