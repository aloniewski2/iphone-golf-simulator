import bpy,json
from pathlib import Path
P=Path(__file__).resolve().parent;F=P/'preview_frames';F.mkdir(exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(P/'Hero_01_Mixamo_QA.blend'))
rig=bpy.data.objects['Hero_01_Rig'];scene=bpy.context.scene;scene.cycles.samples=12;scene.render.resolution_percentage=40
frames=[]
for name,start,end in [('Idle',1,89),('Ready',1,75)]:
 rig.animation_data.action=bpy.data.actions[name]
 for f in range(start,end+1,2):
  scene.frame_set(f);filename=f'{len(frames):04}.png';scene.render.filepath=str(F/filename);bpy.ops.render.render(write_still=True);frames.append({'file':filename,'clip':name,'source_frame':f})
(P/'preview_frames.json').write_text(json.dumps(frames,indent=2));print('PREVIEW_FRAMES_DONE')
