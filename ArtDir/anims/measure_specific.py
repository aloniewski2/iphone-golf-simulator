from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
import math,json
P=Path(__file__).resolve().parents[1]
configs={
 'plate':{'path':P/'plates/01_hero.png','hip_y':550,'sole_y':954,'regions':{
 'biceps':[((984,453),(-16,53)),((1239,455),(23,46))],
 'forearm':[((956,502),(-46,62)),((1270,516),(40,73))],
 'thigh':[((1003,710),(-19,56)),((1214,708),(30,56))],
 'calf':[((981,770),(-20,69)),((1244,772),(26,74))]}},
 'v2':{'path':P/'anims/polish_v3_before/unity_hero.png','hip_y':596,'sole_y':985,'regions':{
 'biceps':[((315,493),(-13,60)),((560,509),(16,56))],
 'forearm':[((310,551),(4,56)),((575,560),(9,43))],
 'thigh':[((362,750),(-9,52)),((518,753),(10,50))],
 'calf':[((351,805),(-13,65)),((525,810),(4,64))]}}
}
configs['working_v3']={**configs['v2'],'path':P/'anims/specific_before/unity_hero.png'}
configs['after']={**configs['v2'],'path':P/'screenshots/unity_hero.png','sole_y':983}
def skin(rgb):
 r,g,b=rgb;return r>130 and r>g*1.065 and g>b*1.06
out={}
for name,cfg in configs.items():
 im=Image.open(cfg['path']).convert('RGB');annotated=im.copy();d=ImageDraw.Draw(annotated);result={}
 for region,lines in cfg['regions'].items():
  samples=[]
  for (cx,cy),(ax,ay) in lines:
   length=math.hypot(ax,ay);tx,ty=ax/length,ay/length;nx,ny=ty,-tx
   cuts=[]
   for along in [-8,-4,0,4,8]:
    x,y=cx+along*tx,cy+along*ty;ends=[]
    for sign in [-1,1]:
     distance=0
     for k in range(1,401):
      distance=k*.25;px,py=round(x+nx*sign*distance),round(y+ny*sign*distance)
      if not skin(im.getpixel((px,py))):break
     ends.append((x+nx*sign*distance,y+ny*sign*distance))
    width=math.dist(*ends);cuts.append((width,ends))
   w,ends=max(cuts,key=lambda x:x[0]);samples.append(w);d.line(ends,fill='#00B6FF',width=3);d.text(ends[0],f'{region} {w:.1f}',fill='black')
  result[region]={'left_right_px':samples,'mean_max_px':sum(samples)/2,'normalized_to_plate_hip_height':sum(samples)/2*404/(cfg['sole_y']-cfg['hip_y'])}
 annotated.save(P/f'screenshots/specific_measure_{name}.png');out[name]=result
out['method']='Perpendicular skin silhouette scans at five positions ±8 px around each annotated anatomical midpoint; mean of left/right local maxima. Normalize sole-to-polo-hem (hip proxy) height to plate 404 px. Perspective/pose uncertainty remains; no physical 3D dimension can be recovered exactly from one plate.'
(P/'anims/specific-measurements-before.json').write_text(json.dumps(out,indent=2));print(json.dumps(out,indent=2))
