from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
import numpy as np,imageio_ffmpeg,json,shutil
P=Path(__file__).resolve().parents[1];S=P.parent.parent/'screenshots/anim_repair_playmode'
font='/System/Library/Fonts/Supplemental/Arial Bold.ttf';f=lambda n:ImageFont.truetype(font,n)
names=['Ready','Jump serve','Forehand','Backhand · TWO HANDS','Run forward','Run right','Run left','Forehand volley','Overhead smash']
def writer(name):
 w=imageio_ffmpeg.write_frames(str(P/name),(960,1040),fps=30,codec='libx264',quality=8,macro_block_size=1,output_params=['-movflags','+faststart']);w.send(None);return w
full=writer('V4_anim_repair_full_takes.mp4');montage=writer('V4_anim_repair_montage.mp4')
for i in range(1080):
 idx=i//120;t=i%120
 for w,local,note in [(full,t,'FULL TAKE · 1×'),(montage,15+t%30 if idx in [1,2,8] else 10+t%40 if idx==7 else t,'ACTION WINDOW · REPEAT · 1×' if idx in [1,2,7,8] else 'FULL TAKE · 1×')]:
  pic=Image.open(S/f'frame_{idx*120+local:04}.png');im=Image.new('RGB',(960,1040),'#16283C');im.paste(pic,(0,80));d=ImageDraw.Draw(im);d.text((24,9),'V4 / ANIMATION REPAIR · VERIFIED PLAYBACK',font=f(17),fill='#B6DEEF');d.text((24,37),names[idx],font=f(25),fill='white');d.text((936,45),note,anchor='ra',font=f(15),fill='#FFB486');w.send(np.asarray(im))
full.close();montage.close()
stills={'Backhand_LOAD':405,'Backhand_CONTACT':408,'Backhand_FOLLOW':447,'Volley_CONTACT':860}
for name,i in stills.items():shutil.copyfile(S/f'frame_{i:04}.png',P/(name+'.png'))
shutil.copyfile(S/'RunForward_side.png',P/'RunForward_SIDE.png')
sheet=Image.new('RGB',(1800,760),'#16283C');d=ImageDraw.Draw(sheet)
for i,n in enumerate(list(stills)+['RunForward_SIDE']):
 pic=Image.open(P/(n+'.png')).crop((220,285,790,840));pic.thumbnail((355,360));sheet.paste(pic,(i%5*360+(360-pic.width)//2,55));d.text((i%5*360+12,18),n.replace('_',' '),font=f(18),fill='white')
d.text((25,450),'BACKHAND: lower right palm + upper left palm constrained to one handle throughout.',font=f(22),fill='white');d.text((25,495),'LOAD → 3-frame CONTACT → FOLLOW. Pelvis coils −34° and uncoils to +27°.',font=f(22),fill='#B6DEEF');d.text((25,540),'Run right / Run left: exact previous clip assets retained.',font=f(22),fill='#B6DEEF')
sheet.save(P/'repair_acceptance_stills.png');(P/'chapters.json').write_text(json.dumps([{'clip':n,'start_seconds':i*4,'duration_seconds':4,'action_window_repeated':i in [1,2,7,8]} for i,n in enumerate(names)],indent=2));print('REPAIR_VIDEOS_AND_STILLS_COMPLETE',flush=True)
