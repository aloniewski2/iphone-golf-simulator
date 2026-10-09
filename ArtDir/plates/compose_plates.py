"""Reproducible plate composites only; does not touch runtime art or game code."""
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont,ImageFilter,ImageEnhance,ImageOps
import math
P=Path(__file__).resolve().parent
R=P.parents[1]
W,H=1920,1080
SKY='#7EC8E3';FLOOR='#F4F0E6';INK='#1A1A1A';ORANGE='#FF6B3D';BLUE='#5B8CFF';FAIL='#FF5A7A';GREEN='#3DDC97';SHADOW='#4A5A78'
FONT='/System/Library/Fonts/Supplemental/Arial Rounded Bold.ttf'
REG='/System/Library/Fonts/Supplemental/Arial.ttf'
def font(n,b=True):return ImageFont.truetype(FONT if b else REG,n)
def text(im,xy,s,n=32,fill=INK,b=True):ImageDraw.Draw(im).text(xy,s,font=font(n,b),fill=fill)
def rr(im,box,fill,r=28,outline=None,width=1):ImageDraw.Draw(im).rounded_rectangle(box,r,fill=fill,outline=outline,width=width)
def base():
 im=Image.new('RGBA',(W,H),SKY);d=ImageDraw.Draw(im)
 d.ellipse((-450,-780,1400,780),fill='#BDE2ED');d.ellipse((1100,-150,2300,620),fill='#ABDCEB')
 d.rectangle((0,590,W,H),fill=FLOOR);d.line((0,590,W,590),fill='#FFFFFF',width=8)
 d.line((-100,1070,1250,590),fill='#DDD9CE',width=5);d.line((950,1080,1600,590),fill='#DDD9CE',width=5)
 return im

def shadow(im,x,y,w=350,h=45):
 layer=Image.new('RGBA',im.size);ImageDraw.Draw(layer).ellipse((x-w/2,y-h/2,x+w/2,y+h/2),fill=(74,90,120,62));im.alpha_composite(layer.filter(ImageFilter.GaussianBlur(14)))
def cut(name):
 im=Image.open(P/'sources'/name).convert('RGBA')
 # Remove low-alpha generator halos, retaining soft edge antialiasing.
 if name!='hero-proportion-composite.png':im.putalpha(im.getchannel('A').point(lambda a: max(0,min(255,(a-190)*4))))
 return im.crop(im.getbbox())
def actor(im,name,center,feet,height):
 a=cut(name);a=a.resize((round(a.width*height/a.height),height),Image.Resampling.LANCZOS);im.alpha_composite(a,(round(center-a.width/2),round(feet-height)));return a.width

def ball(im,x,y,r=20):
 d=ImageDraw.Draw(im);d.ellipse((x-r,y-r,x+r,y+r),fill='#E8EE48',outline='#FFFFFF',width=3);d.arc((x-r*.6,y-r,x+r*.6,y+r),70,280,fill='#FFFFFF',width=3)
def footer(im,num,label):
 rr(im,(55,1005,500,1058),'#FFFFFF',18);text(im,(78,1018),f'{num:02d}  /  {label.upper()}',22)
def save(im,name):im.convert('RGB').save(P/name)
hero='hero-proportion-composite.png'
# 01
im=base();shadow(im,1070,957,420);actor(im,hero,1050,958,865)
text(im,(110,165),'YOUR GAME-NIGHT',29);text(im,(110,211),'REGULAR.',72)
text(im,(110,327),'One athlete. Your identity.',30,b=False)
for i,c in enumerate([ORANGE,BLUE,GREEN,FAIL]):ImageDraw.Draw(im).ellipse((115+i*63,405,151+i*63,441),fill=c)
footer(im,1,'Hero');save(im,'01_hero.png')
# 02: retain the original resort horizon only; simplify the entire play surface.
im=base();env=Image.open(R/'SportsLibrary/ArtDirection/Tennis/target-gameplay-b.png').convert('RGB')
env=env.crop((0,40,2688,500)).resize((1920,408),Image.Resampling.LANCZOS);env=ImageEnhance.Color(env).enhance(.55)
im.paste(env,(0,0));d=ImageDraw.Draw(im);d.rectangle((0,408,W,H),fill='#C7DEDD')
corners=[(520,422),(1380,422),(1850,1035),(90,1035)];d.polygon(corners,fill=FLOOR);d.line(corners+[corners[0]],fill='#2B2B2B',width=7)
d.line((960,422,960,1035),fill='#2B2B2B',width=5);d.line((327,700,1619,700),fill='#2B2B2B',width=5)
d.line((455,495,1452,495),fill='#2B2B2B',width=5)
shadow(im,1225,505,125,20);actor(im,hero,1225,505,220)
# net mesh and white tape
for x in range(400,1530,22):d.line((x,550,x,645),fill='#4A5A78',width=2)
for y in range(550,645,15):d.line((395,y,1530,y),fill='#4A5A78',width=2)
d.line((395,547,1530,547),fill='white',width=10);d.line((395,543,395,655),fill=INK,width=8);d.line((1530,543,1530,655),fill=INK,width=8)
shadow(im,715,995,260,38);actor(im,hero,715,995,510)
# A sparse trajectory keeps the single bright ball obvious.
for i in range(9):
 t=i/10;x=1120-160*t;y=530+180*t-55*math.sin(t*math.pi);d.ellipse((x-4,y-4,x+4,y+4),fill=BLUE)
ball(im,947,718,22)
rr(im,(735,32,1185,105),'#FFFFFF',24);text(im,(784,48),'YOU  15   :   15  FRIEND',28)
footer(im,2,'Gameplay');save(im,'02_gameplay.png')
# 03 / 04
for no,name,label,title,sub,accent in [(3,'miss.png','Miss','SO CLOSE.','Same friends. Instant rematch.',FAIL),(4,'win.png','Win','THAT ONE COUNTS!','Now let your friend have a turn.',GREEN)]:
 if not (P/'sources'/name).exists():continue
 im=base();text(im,(95,122),title,58);text(im,(98,202),sub,29,b=False)
 shadow(im,1150,947,520);actor(im,name,1165,940,835)
 if no==3:
  d=ImageDraw.Draw(im)
  for j in range(3):d.arc((745-j*25,840+j*18,1100-j*25,975+j*18),0,115,fill=FAIL,width=6)
 else:
  d=ImageDraw.Draw(im)
  for j,(x,y) in enumerate([(520,470),(1600,260),(1500,640),(710,340),(1680,790),(450,660)]):d.rounded_rectangle((x,y,x+15,y+34),5,fill=[GREEN,ORANGE,BLUE][j%3])
 footer(im,no,label);save(im,f'{no:02d}_{label.lower()}.png')
# 05 home mock: original UI, no actual game changes.
im=base();text(im,(95,55),'PARTY SPORTS',34);rr(im,(1690,45,1835,100),'#FFFFFF',22);text(im,(1715,61),'Settings',23)
shadow(im,520,929,390);actor(im,hero,495,922,700)
rr(im,(888,181,1824,941),'#FFFFFF',42)
text(im,(944,222),'EVERYONE’S GOT A GAME.',25,fill='#4A5A78');text(im,(940,283),'Who’s playing?',65)
text(im,(945,379),'Bring a friend. Make a little noise.',30,b=False)
rr(im,(943,466,1770,614),ORANGE,28);text(im,(984,486),'Start Party',59);text(im,(987,565),'Create a room or join your friends  →',27,b=False)
for a,y,b in [('Quick Play',657,'A casual match'),('Campaign',755,'A little solo practice'),('Cosmetics',853,'Make this athlete yours')]:
 rr(im,(943,y,1770,y+74),FLOOR,20);text(im,(973,y+19),a,30);text(im,(1265,y+23),b,25,b=False)
footer(im,5,'Home');save(im,'05_home.png')
# 06: same hero ready pose and an illustrated ball-about-to-drop gag, explicitly a static mock.
im=base();shadow(im,655,898,330);actor(im,hero,610,895,700)
ball(im,780,147,31);d=ImageDraw.Draw(im)
for j in range(3):d.line((780,68+j*20,780,76+j*20),fill=BLUE,width=5)
text(im,(1000,267),'ONE MORE',57);text(im,(1000,335),'PRACTICE BOUNCE…',49)
text(im,(1004,425),'Tennis is getting ready.',29,b=False)
rr(im,(1002,535,1797,563),'#D2D8D7',14);rr(im,(1002,535,1519,563),ORANGE,14)
text(im,(1005,590),'Preparing the court',26,b=False);text(im,(1713,588),'65%',28)
rr(im,(999,679,1805,805),'#FFFFFF',24);text(im,(1030,703),'Leave a little elbow room before you swing.',26,b=False)
text(im,(1030,748),'Next game night, bring a friend.',25,fill='#4A5A78',b=False)
footer(im,6,'Loading');save(im,'06_loading.png')
# Review sheet
files=[P/f'{i:02d}_{n}.png' for i,n in enumerate(['hero','gameplay','miss','win','home','loading'],1)]
if all(p.exists() for p in files):
 sheet=Image.new('RGB',(1440,1284),'#FFFFFF')
 for i,p in enumerate(files):
  a=Image.open(p).resize((704,396),Image.Resampling.LANCZOS);x=8+(i%2)*720;y=8+(i//2)*428;sheet.paste(a,(x,y));ImageDraw.Draw(sheet).text((x+7,y+399),p.stem.replace('_',' / '),font=font(20),fill=INK)
 sheet.save(P/'review-sheet.jpg',quality=94)
