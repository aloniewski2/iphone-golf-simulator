"""Assemble unretouched capture sheets. This lays out evidence; it does not repaint renders."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
R=Path(__file__).resolve().parents[4];D=R/'ArtDir/hero/v6_proof'
font=ImageFont.truetype('/System/Library/Fonts/Supplemental/Arial.ttf',22)
small=ImageFont.truetype('/System/Library/Fonts/Supplemental/Arial.ttf',16)
def sheet(name,items,cols=3,w=600,h=660):
 rows=(len(items)+cols-1)//cols;im=Image.new('RGB',(cols*w,rows*h),(246,246,244));draw=ImageDraw.Draw(im)
 for i,(label,p) in enumerate(items):
  src=Image.open(R/p).convert('RGB');src.thumbnail((w-16,h-60));x=(i%cols)*w;y=(i//cols)*h;im.paste(src,(x+(w-src.width)//2,y+48+(h-56-src.height)//2));draw.text((x+14,y+12),label,font=font,fill=(25,30,40))
 im.save(D/name)
p='ArtDir/hero/v6_proof/Unity/';b='ArtDir/hero/v6_target/BEFORE/Unity/'
sheet('before_after.png',[('V5 BEFORE — same Ready sample',b+'Ready_0.png'),('V6 CANDIDATE — FAIL',p+'Ready_0.png'),('AUTHORITY — target, not runtime','ArtDir/plates/01_hero.png')])
sheet('failure_evidence.png',[('Hair / visor / face',p+'customize_closeup.png'),('Bare skull / neck join',p+'hair_off_back.png'),('Knee and ankle silhouette',p+'knees_feet.png'),('Serve / armpit',p+'serve_armpit_back.png'),('LOD2 — opaque geometry',p+'hair_LOD2.png'),('Blender lunge — NOT gameplay dive','ArtDir/hero/v6_proof/blender/stress_dive_side.png')])
sheet('turnaround.png',[(n,'ArtDir/hero/v6_proof/blender/turn_'+n+'.png') for n in ['front','three_quarter','side','back']],4,450,560)
sheet('clip_samples.png',[(n,p+n+'_2.png') for n in ['Ready','Serve','Forehand','Backhand','RunForward','RunRight','RunLeft','Volley','Smash']],3,450,520)
