from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
import numpy as np,imageio_ffmpeg,json
P=Path(__file__).resolve().parents[1];S=P.parent.parent/'screenshots';fixed=S/'backhand_run_playmode';old=S/'knees_offarm_playmode'
font='/System/Library/Fonts/Supplemental/Arial Bold.ttf';f=lambda n:ImageFont.truetype(font,n)
names=['Ready','Jump serve','Forehand','Backhand (NEW)','Run forward','Run right','Run left','Forehand volley','Overhead smash']
mono=imageio_ffmpeg.write_frames(str(P/'V4_backhand_run_full.mp4'),(960,1040),fps=30,codec='libx264',quality=8,macro_block_size=1,output_params=['-movflags','+faststart']);mono.send(None)
pair=imageio_ffmpeg.write_frames(str(P/'V4_backhand_run_before_after.mp4'),(1440,820),fps=30,codec='libx264',quality=8,macro_block_size=1,output_params=['-movflags','+faststart']);pair.send(None)
for i in range(1080):
 idx=i//120;new=Image.open(fixed/f'frame_{i:04}.png');before=Image.open(old/f'frame_{i:04}.png')
 im=Image.new('RGB',(960,1040),'#16283C');im.paste(new,(0,80));d=ImageDraw.Draw(im);d.text((24,10),'V4 / NEW BACKHAND + NATURAL RUN ARM',font=f(17),fill='#B6DEEF');d.text((24,37),names[idx],font=f(25),fill='white');d.text((936,42),'FULL CLIP · 1×',anchor='ra',font=f(18),fill='#FFB486');mono.send(np.asarray(im))
 compare=Image.new('RGB',(1440,820),'#16283C');compare.paste(before.resize((720,720)),(0,100));compare.paste(new.resize((720,720)),(720,100));d=ImageDraw.Draw(compare);d.text((25,15),names[idx]+' / same gameplay timing',font=f(24),fill='white');d.text((25,61),'BEFORE · prior backhand / clearance-run arm',font=f(19),fill='#FDB9AA');d.text((745,61),'AFTER · left-side backhand / natural arm pump',font=f(19),fill='#B6EBC6');pair.send(np.asarray(compare))
mono.close();pair.close()
sheet=Image.new('RGB',(1800,840),'#16283C');d=ImageDraw.Draw(sheet)
for i,k in enumerate([20,20,23,33,55,55,55,20,20]):
 im=Image.open(fixed/f'frame_{i*120+k:04}.png').crop((200,70,810,850));im.thumbnail((300,365));x=i%6*300;y=i//6*420;sheet.paste(im,(x+(300-im.width)//2,y+40));d.text((x+12,y+10),names[i],font=f(20),fill='white');im.save(P/(names[i].replace(' ','_')+'.png'))
sheet.save(P/'V4_backhand_run_contact_sheet.png');(P/'chapters.json').write_text(json.dumps([{'clip':n,'start_seconds':i*4,'duration_seconds':4} for i,n in enumerate(names)],indent=2));print('PACKAGED_FULL_1080_FRAMES',flush=True)
