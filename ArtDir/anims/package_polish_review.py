"""Arrange unretouched Unity captures beside the locked plate for review."""
from pathlib import Path
from PIL import Image,ImageOps,ImageDraw,ImageFont
P=Path(__file__).resolve().parents[1];S=P/'screenshots'
font=ImageFont.truetype('/System/Library/Fonts/Supplemental/Arial Bold.ttf',26)
small=ImageFont.truetype('/System/Library/Fonts/Supplemental/Arial.ttf',18)
plate=Image.open(P/'plates/01_hero.png').convert('RGB').crop((725,65,1390,980))
hero=Image.open(S/'unity_hero.png').convert('RGB').crop((205,125,725,1020))
def compare(images,titles,path,footer):
 out=Image.new('RGB',(1440,1080),'#F4F0E6');d=ImageDraw.Draw(out)
 for i,im in enumerate(images):
  fit=ImageOps.contain(im,(660,930),Image.Resampling.LANCZOS);out.paste(fit,(i*720+(720-fit.width)//2,75+(930-fit.height)//2));d.text((i*720+30,25),titles[i],font=font,fill='#1A1A1A')
 d.line((720,15,720,1010),fill='#CED2D0',width=2);d.text((30,1036),footer,font=small,fill='#4A5A78');out.save(path)
compare([plate,hero],['LOCKED PLATE 01','UNITY URP • POLISH V2'],S/'unity_hero_vs_plate01_v2.png','Original plate | Actual Unity Play Mode capture • shared Humanoid • default wardrobe')
before=Image.open(P/'anims/polish_before/unity_hero.png').convert('RGB').crop((205,125,725,1020))
compare([before,hero],['BEFORE • UNITY LOOK','AFTER • UNITY LOOK POLISH'],S/'unity_hero_polish_before_after.png','Same camera and crop • shoulders / hands / eye glints / materials / soft lighting')
Image.open(S/'unity_hero.png').save(S/'polish_05_lighting_final.png')
