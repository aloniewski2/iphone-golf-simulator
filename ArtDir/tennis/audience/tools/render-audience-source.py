import bpy,math,sys
from pathlib import Path
from mathutils import Vector
R=Path(__file__).resolve().parents[4];W=R/'ArtDir/tennis/audience/staging5';P=R/'proof/full-visual-overhaul/tennis/audience5'
asset='TennisSeatedHero3' if '--seated' in sys.argv else 'TennisPromenadeHero3'
bpy.ops.wm.open_mainfile(filepath=str(W/(asset+'.blend')))
sc=bpy.context.scene;sc.render.engine='CYCLES';sc.cycles.device='CPU';sc.cycles.samples=32;sc.render.threads_mode='FIXED';sc.render.threads=4;sc.render.resolution_x=640;sc.render.resolution_y=640;sc.render.resolution_percentage=100
sc.world=bpy.data.worlds.new('Audience source studio');sc.world.use_nodes=True;sc.world.node_tree.nodes['Background'].inputs['Color'].default_value=(.29,.39,.50,1);sc.world.node_tree.nodes['Background'].inputs['Strength'].default_value=.6
bpy.ops.object.light_add(type='AREA',location=(2,-4,5));bpy.context.object.data.energy=700;bpy.context.object.data.size=5
bpy.ops.object.camera_add(location=(.2,-4,1.3));cam=bpy.context.object;cam.data.type='ORTHO';cam.data.ortho_scale=1.9;sc.camera=cam;sc.view_settings.view_transform='AgX'
for variant,sex in [(0,'male'),(1,'female')]:
 root=bpy.data.objects['FAN_'+str(variant)]
 for o in bpy.data.objects:
  if o.type=='MESH':o.hide_render=not ('_FAR' in o.name)
 bpy.context.view_layer.update()
 head=next(o for o in root.children if o.name.startswith('HEAD'));points=[head.matrix_world@v.co for v in head.data.vertices];low=min(v.z for v in points);high=max(v.z for v in points)
 centre=head.matrix_world.translation;target=Vector((centre.x,-.02,high-.12));cam.data.ortho_scale=.43;cam.location=target+Vector((.18,-3,.005));cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();sc.render.filepath=str(P/(asset+'-'+sex+'-far-face.png'));bpy.ops.render.render(write_still=True)
 for detail in ['near','far']:
  for o in bpy.data.objects:
   if o.type=='MESH':o.hide_render=('_FAR' in o.name) if detail=='near' else not ('_FAR' in o.name)
  target=Vector((centre.x,-.05,.38 if '--seated' in sys.argv else .90));cam.data.ortho_scale=1.95 if '--seated' in sys.argv else 1.95;cam.location=target+Vector((2.1,-3,.10));cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();sc.render.filepath=str(P/(asset+'-'+sex+'-'+detail+'-body.png'));bpy.ops.render.render(write_still=True)
for variant,sex in [(0,'male'),(1,'female')]:
 root=bpy.data.objects['FAN_'+str(variant)]
 for ob in root.children:
  if ob.name.startswith(('ARM_L','ARM_R')):ob.rotation_euler.x=math.radians(-110)
 for ob in bpy.data.objects:
  if ob.type=='MESH':ob.hide_render=('_FAR' in ob.name)
 bpy.context.view_layer.update()
 head=next(ob for ob in root.children if ob.name.startswith('HEAD'));centre=head.matrix_world.translation;target=Vector((centre.x,0,.40 if '--seated' in sys.argv else .90));cam.data.ortho_scale=2.2;cam.location=target+Vector((2,-3,.3));cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();sc.render.filepath=str(P/(asset+'-'+sex+'-cheer-near.png'));bpy.ops.render.render(write_still=True)
print('AUDIENCE_SOURCE_CPU_PROOF_COMPLETE',asset)
