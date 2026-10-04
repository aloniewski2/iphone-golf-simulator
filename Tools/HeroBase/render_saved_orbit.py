"""Eight full-body views from a saved bald source, without modifying that source."""
import bpy, math, sys
from pathlib import Path
from mathutils import Vector
sys.path.insert(0,str(Path(__file__).parent))
from plate_profiles import MALE,FEMALE
R=Path(__file__).resolve().parents[2];D=R/'ArtDir/hero/base_lock'
sex=sys.argv[sys.argv.index('--')+1] if '--' in sys.argv else 'Female'
pr=MALE if sex=='Male' else FEMALE;suffix=sex[0]
bpy.ops.wm.open_mainfile(filepath=str(D/'blender'/('HeroBase_'+sex+'.blend')))
scene=bpy.context.scene;cam=scene.camera
bpy.data.objects['Hair_'+suffix].hide_render=True
positions={name:bpy.data.objects[name].location.copy() for name in ('Neutral_Key','Neutral_Fill','Neutral_Back')}
for i,name in enumerate(('front','front_right','right','back_right','back','back_left','left','front_left')):
    angle=i*math.pi/4
    for light_name,loc in positions.items():
        light=bpy.data.objects[light_name]
        light.location=(loc.x*math.cos(angle)-loc.y*math.sin(angle),loc.x*math.sin(angle)+loc.y*math.cos(angle),loc.z)
        light.rotation_euler=(Vector((0,0,1))-light.location).to_track_quat('-Z','Y').to_euler()
    h=(pr['sole']-360)*1.7/pr['height']
    cam.location=(math.sin(angle)*5,-math.cos(angle)*5,h)
    cam.rotation_euler=(Vector((0,0,h))-cam.location).to_track_quat('-Z','Y').to_euler()
    scene.render.filepath=str(D/'proof/blender'/(suffix+'_allaround_'+name+'.png'))
    bpy.ops.render.render(write_still=True)
print('SAVED_ORBIT_RENDER_COMPLETE',sex)
