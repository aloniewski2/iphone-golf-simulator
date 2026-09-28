# Montage (same order as prior reviews) + stills sheet + GIF from the Unity Play Mode capture.
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
import numpy as np,imageio_ffmpeg,shutil
P0=Path(__file__).resolve().parents[1];P=P0.parent/'fix_hand_clip';P.mkdir(exist_ok=True);S=P.parents[1]/'screenshots/restore_motion_playmode'
font='/System/Library/Fonts/Supplemental/Arial Bold.ttf';f=lambda n:ImageFont.truetype(font,n)
label={'Ready':'Ready · Eyes receive · left hand on throat','Serve':'Serve · Eyes first service','Forehand':'Forehand · Eyes hard hit','Backhand':'Backhand · Eyes TWO-HANDED hard hit',
 'RunForward':'Run forward · original stride · left arm clears body','RunRight':'Run right · original stride · left arm clears body','RunLeft':'Run left · original stride · left arm clears body','Volley':'Forehand volley · Eyes volley','Smash':'Overhead smash · Eyes smash'}
rows=[l.split() for l in (S/'manifest.txt').read_text().splitlines()]
w=imageio_ffmpeg.write_frames(str(P/'V6_fixhand_armclear_montage.mp4'),(960,1040),fps=30,codec='libx264',quality=8,macro_block_size=1,output_params=['-movflags','+faststart']);w.send(None)
gif=[]
for g,name,local,n in rows:
 pic=Image.open(S/f'frame_{int(g):04}.png').convert('RGB');im=Image.new('RGB',(960,1040),'#16283C');im.paste(pic,(0,80));d=ImageDraw.Draw(im)
 d.text((24,9),'V6 / LEFT HAND FIXED · ARMS CLEAR BODY — REVIEW CANDIDATE',font=f(17),fill='#B6DEEF');d.text((24,37),label[name],font=f(25),fill='white')
 d.text((936,45),f'FULL CLIP · 1× · {int(local)+1}/{n}',anchor='ra',font=f(15),fill='#FFB486');w.send(np.asarray(im))
 if int(g)%2==0:gif.append(im.resize((360,390)).quantize(96))
w.close()
gif[0].save(P/'V6_fixhand_armclear_montage.gif',save_all=True,append_images=gif[1:],duration=66,loop=0,optimize=True)
rowsm=[l.split() for l in (S/'manifest.txt').read_text().splitlines()]
def fr(name,local):return f"frame_{next(int(r[0]) for r in rowsm if r[1]==name and int(r[2])==local):04}"
for n,l in [('RunForward',2),('RunRight',2),('RunLeft',2),('RunForward',40)]:shutil.copyfile(S/(fr(n,l)+'.png'),P/f'{n}_f{l:03}_armclear.png')
for fp in S.glob('*.png'):
 if not fp.name.startswith('frame_'):shutil.copyfile(fp,P/fp.name)
def sheet(names,out,cols=3):
 names=[n for n in names if (P/(n+'.png')).exists()];rows_=(len(names)+cols-1)//cols
 sh=Image.new('RGB',(cols*640,rows_*660),'#16283C');d=ImageDraw.Draw(sh)
 for i,n in enumerate(names):
  pic=Image.open(P/(n+'.png')).resize((600,600));x=(i%cols)*640+20;y=(i//cols)*660+45;sh.paste(pic,(x,y));d.text((x,y-32),n.replace('_',' '),font=f(22),fill='white')
 sh.save(P/out)
sheet(['Ready_hands_front','Ready_hands_top','Ready_hands_left','Ready_front','Backhand_LOAD_hands','Backhand_CONTACT_hands'],'sheet_left_hand.png')
sheet(['Forehand_formerWorst_front','Backhand_formerWorst_front','Serve_formerWorst_front','Smash_formerWorst_side','Ready_formerWorst_front','RunForward_f002_armclear','RunRight_f002_armclear','RunLeft_f002_armclear','RunForward_f040_armclear'],'sheet_arm_clearance.png')
sheet(['Serve_HEAD_CLEARANCE','Smash_HEAD_CLEARANCE','RunForward_LEG_CLEARANCE','RunRight_LEG_CLEARANCE','Volley_CONTACT','Backhand_CONTACT','Backhand_WRAP','Forehand_CONTACT','Smash_CONTACT'],'sheet_clearance_contacts.png')
print('PACKAGE_DONE',len(rows))
