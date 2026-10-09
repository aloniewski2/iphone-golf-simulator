from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
import numpy as np,imageio_ffmpeg,json,shutil
P=Path(__file__).resolve().parents[1];screens=P.parent/'screenshots';frames=screens/'owned'
font='/System/Library/Fonts/Supplemental/Arial.ttf';bold='/System/Library/Fonts/Supplemental/Arial Bold.ttf'
names=['Ready idle','Forehand','Backhand','Serve','Volley'];W,H=960,1040
writer=imageio_ffmpeg.write_frames(str(P/'hero_owned/Hero_Owned_v1_review.mp4'),(W,H),fps=30,codec='libx264',quality=8,macro_block_size=1,output_params=['-pix_fmt','yuv420p','-movflags','+faststart']);writer.send(None)
for i in range(540):
 im=Image.new('RGB',(W,H),'#16283C');im.paste(Image.open(frames/f'frame_{i:04}.png'),(0,80));d=ImageDraw.Draw(im);idx=i//108;t=(i%108)/30
 d.text((26,12),'HERO 01 / OWNED TENNIS v1',fill='#B6DEEF',font=ImageFont.truetype(bold,16));d.text((26,36),names[idx],fill='white',font=ImageFont.truetype(bold,26))
 phase='READY LOOP' if idx==0 else 'WINDUP' if t<.95 else 'CONTACT' if t<1.16 else 'FOLLOW-THROUGH / HOLD' if t<2.12 else 'SETTLE'
 d.text((930,40),phase,anchor='ra',fill='#FFB486',font=ImageFont.truetype(bold,18));writer.send(np.asarray(im))
writer.close()
# Review stills identify a readable phase, rather than claiming a ball-contact solve.
selected={'readyidle':32,'forehand':108+38,'backhand':216+38,'serve':324+32,'volley':432+33}
for name,f in selected.items():shutil.copy2(frames/f'frame_{f:04}.png',screens/f'unity_owned_{name}.png')
sheet=Image.new('RGB',(1600,440),'#16283C');d=ImageDraw.Draw(sheet)
for i,(name,f) in enumerate(selected.items()):
 im=Image.open(frames/f'frame_{f:04}.png');im=im.crop((160,90,810,840));im.thumbnail((320,380));sheet.paste(im,(i*320+(320-im.width)//2,45));d.text((i*320+18,12),names[i],fill='white',font=ImageFont.truetype(bold,22))
sheet.save(screens/'unity_owned_tennis_contact_sheet.png')
print(P/'hero_owned/Hero_Owned_v1_review.mp4')
