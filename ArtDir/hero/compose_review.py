from pathlib import Path
from PIL import Image,ImageOps,ImageDraw,ImageFont
P=Path(__file__).resolve().parent
plate=Image.open(P.parent/'plates/01_hero.png').convert('RGB').crop((770,65,1430,995))
hero=Image.open(P/'Hero_01_threequarter.png').convert('RGB').crop((50,60,1050,1260))
# Equal character standing height: no reshaping or retouching of either source.
out=Image.new('RGB',(1800,1120),'#F4F0E6');d=ImageDraw.Draw(out);font=ImageFont.truetype('/System/Library/Fonts/Supplemental/Arial Bold.ttf',25)
for im,x,label in [(plate,0,'PLATE 01 / TARGET'),(hero,900,'HERO FROM PLATE / BLENDER')]:
 im=ImageOps.contain(im,(880,1030));out.paste(im,(x+(900-im.width)//2,65+(1030-im.height)//2));d.text((x+24,20),label,font=font,fill='#283952')
out.save(P/'Hero_01_compare.png')
