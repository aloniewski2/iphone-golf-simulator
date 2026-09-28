from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
import numpy as np,imageio_ffmpeg,json
P=Path(__file__).resolve().parent;S=P.parents[1]/'screenshots/v4_legacy'
names=['Ready stance','Jump serve','Forehand','Backhand','Run forward','Run right','Run left','Forehand volley','Overhead smash']
font='/System/Library/Fonts/Supplemental/Arial.ttf';bold='/System/Library/Fonts/Supplemental/Arial Bold.ttf'
sequence=[];chapters=[]
for i,name in enumerate(names):
 chapters.append({'name':name,'starts_seconds':len(sequence)/30})
 if i==0:selection=[(f,'REAL TIME') for f in range(60)]
 elif i in [4,5,6]:selection=[(f,'REAL TIME · CAMERA HOLDS POSITION') for f in range(20,110)]
 else:selection=[(f,'REAL TIME') for f in range(50)]+[(f,'HALF-SPEED REPLAY') for f in range(12,38) for _ in range(2)]
 sequence.extend((i,f,label) for f,label in selection)
w=imageio_ffmpeg.write_frames(str(P/'V4_existing_tennis_review.mp4'),(960,1040),fps=30,codec='libx264',quality=8,macro_block_size=1,output_params=['-movflags','+faststart']);w.send(None)
for i,f,label in sequence:
 im=Image.new('RGB',(960,1040),'#16283C');im.paste(Image.open(S/f'frame_{i*120+f:04}.png'),(0,80));d=ImageDraw.Draw(im)
 d.text((24,10),'V4 / EXISTING GAMEPLAY ANIMATIONS',fill='#B6DEEF',font=ImageFont.truetype(bold,16));d.text((24,36),names[i],fill='white',font=ImageFont.truetype(bold,26));d.text((936,45),label,anchor='ra',fill='#FFB486',font=ImageFont.truetype(bold,16));w.send(np.asarray(im))
w.close();(P/'chapters.json').write_text(json.dumps(chapters,indent=2))
sheet=Image.new('RGB',(1500,880),'#16283C');d=ImageDraw.Draw(sheet)
for i,f in enumerate([20,20,23,23,55,55,55,20,20]):
 im=Image.open(S/f'frame_{i*120+f:04}.png').crop((220,100,780,830));im.thumbnail((280,365));x=(i%5)*300;y=(i//5)*440;sheet.paste(im,(x+(300-im.width)//2,y+45));d.text((x+12,y+12),names[i],fill='white',font=ImageFont.truetype(bold,20))
sheet.save(P/'V4_existing_tennis_contact_sheet.png');print('REVIEW_DONE',len(sequence)/30)
