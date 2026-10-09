from pathlib import Path
import json,subprocess,sys
import numpy as np
from PIL import Image,ImageDraw,ImageFont
import imageio_ffmpeg
O=Path(__file__).resolve().parent.parent
fontpath='/System/Library/Fonts/Supplemental/Avenir Next.ttc'
if not Path(fontpath).exists():fontpath='/System/Library/Fonts/Helvetica.ttc'
def font(s):return ImageFont.truetype(fontpath,s)
W,H=1400,900
names={'01_ready':'READY STANCE','02_forehand':'FOREHAND','03_backhand':'BACKHAND / 2-HAND PROMPT','04_serve':'OVERHEAD SERVE','05_volley':'FOREHAND VOLLEY'}
enc=imageio_ffmpeg.get_ffmpeg_exe()
def decorated(im,title,speed,frame):
 im=im.convert('RGB');d=ImageDraw.Draw(im)
 d.rectangle((0,0,W,106),fill='#102B3C');d.text((32,16),'MOTION CLUB  /  UNIMATE MOTION TEST',font=font(20),fill='#9FBEC9')
 d.text((30,46),title,font=font(33),fill='#F8F4E9');d.text((1170,54),speed,font=font(23),fill='#D5ED91')
 d.rounded_rectangle((28,124,213,159),radius=10,fill='#102B3C');d.text((42,127),'FRONT / THREE-QUARTER',font=font(13),fill='white')
 d.rounded_rectangle((728,124,871,159),radius=10,fill='#102B3C');d.text((741,127),'SIDE VIEW',font=font(15),fill='white')
 d.line((700,110,700,842),fill='#FFFFFF',width=2)
 d.rectangle((0,849,W,H),fill='#102B3C');d.text((30,863),'V5 DEFAULT HERO  •  RAW MODEL OUTPUT  •  REVIEW ONLY',font=font(18),fill='#E5EBE7')
 d.text((1160,863),f'Frame {frame:02d} / 60',font=font(18),fill='#ADC6D0')
 return im
out=O/'UniMate_V5_Tennis_Review.mp4'
writer=subprocess.Popen([enc,'-y','-v','error','-f','rawvideo','-vcodec','rawvideo','-pix_fmt','rgb24','-s',f'{W}x{H}','-r','30','-i','-','-an','-c:v','libx264','-crf','18','-preset','medium','-pix_fmt','yuv420p','-movflags','+faststart',str(out)],stdin=subprocess.PIPE)
for clip,title in names.items():
 for speed,repeats in [('1× SPEED',1),('0.5× SPEED',2)]:
  for f in range(1,61):
   im=decorated(Image.open(O/'frames'/f'{clip}_{f:03d}.png'),title,speed,f)
   for _ in range(repeats):writer.stdin.write(im.tobytes())
writer.stdin.close();assert writer.wait()==0
# Each full resolution peak still remains individually viewable.
peaks={'01_ready':30,'02_forehand':35,'03_backhand':35,'04_serve':35,'05_volley':35}
thumbs=[]
for clip,frame in peaks.items():
 im=decorated(Image.open(O/'frames'/f'{clip}_{frame:03d}.png'),names[clip],'1× SPEED',frame)
 im.save(O/f'{clip}_preview.png');thumbs.append(im.resize((700,450)))
sheet=Image.new('RGB',(1400,1400),'#102B3C');d=ImageDraw.Draw(sheet)
for i,im in enumerate(thumbs):sheet.paste(im,((i%2)*700,(i//2)*450))
d.text((730,955),'UNIMATE / V5',font=font(32),fill='#F8F4E9');d.text((730,1008),'Five generated tennis candidates',font=font(20),fill='#ADC6D0');d.text((730,1048),'Not connected to gameplay.',font=font(20),fill='#ADC6D0')
sheet.save(O/'UniMate_V5_ContactSheet.jpg',quality=95)
print('PACKAGED',out)
