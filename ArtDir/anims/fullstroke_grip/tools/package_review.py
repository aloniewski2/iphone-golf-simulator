from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
import numpy as np,imageio_ffmpeg,json,shutil
P=Path(__file__).resolve().parents[1];S=P.parent.parent/'screenshots/fullstroke_grip_playmode'
font='/System/Library/Fonts/Supplemental/Arial Bold.ttf';f=lambda n:ImageFont.truetype(font,n)
names=['Ready','Jump serve','Forehand','Backhand v4 · TWO HANDS','Run forward · UNCHANGED','Run right · UNCHANGED','Run left · UNCHANGED','Forehand volley v3','Overhead smash']
def writer(name):
 w=imageio_ffmpeg.write_frames(str(P/name),(960,1040),fps=30,codec='libx264',quality=8,macro_block_size=1,output_params=['-movflags','+faststart']);w.send(None);return w
full=writer('V4_fullstroke_full_takes.mp4');montage=writer('V4_fullstroke_montage.mp4')
for i in range(1080):
 idx=i//120;t=i%120
 for w,local,note in [(full,t,'FULL TAKE · 1×'),(montage,15+t%30 if idx in [1,2,8] else t,'ACTION WINDOW · REPEAT · 1×' if idx in [1,2,8] else 'FULL TAKE · 1×')]:
  pic=Image.open(S/f'frame_{idx*120+local:04}.png');im=Image.new('RGB',(960,1040),'#16283C');im.paste(pic,(0,80));d=ImageDraw.Draw(im);d.text((24,9),'V4 / FULL STROKE REVIEW · GRIP NOT ACCEPTED',font=f(17),fill='#B6DEEF');d.text((24,37),names[idx],font=f(25),fill='white');d.text((936,45),note,anchor='ra',font=f(15),fill='#FFB486');w.send(np.asarray(im))
full.close();montage.close()
stills={'Backhand_LOAD':390,'Backhand_DROP':402,'Backhand_CONTACT':411,'Backhand_FOLLOW':441,'Volley_PREP':861,'Volley_CONTACT':876}
for name,i in stills.items():shutil.copyfile(S/f'frame_{i:04}.png',P/(name+'.png'))
for name in ['Backhand_LOAD_side','Grip_closeup']:shutil.copyfile(S/(name+'.png'),P/(name+'.png'))
sheet=Image.new('RGB',(1920,1240),'#16283C');d=ImageDraw.Draw(sheet)
for i,n in enumerate(['Backhand_LOAD_side','Backhand_DROP','Backhand_CONTACT','Backhand_FOLLOW','Volley_PREP','Volley_CONTACT']):
 pic=Image.open(P/(n+'.png')).crop((245,305,735,795)).resize((590,590));x=(i%3)*640;y=(i//3)*620;sheet.paste(pic,(x+25,y+30));d.text((x+25,y+5),n.replace('_',' '),font=f(21),fill='white')
sheet.save(P/'fullstroke_contact_sheet.png');print('FULLSTROKE_MEDIA_COMPLETE',flush=True)
