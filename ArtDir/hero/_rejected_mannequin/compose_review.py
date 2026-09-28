from pathlib import Path
from PIL import Image,ImageDraw,ImageFont,ImageOps
P=Path(__file__).resolve().parent
font=ImageFont.truetype('/System/Library/Fonts/Supplemental/Arial Bold.ttf',26)
plate=Image.open(P.parent/'plates/01_hero.png').convert('RGB').crop((710,65,1410,995))
hero=Image.open(P/'Hero_01_threequarter.png').convert('RGB')
canvas=Image.new('RGB',(1700,1110),'#F4F0E6');d=ImageDraw.Draw(canvas)
for im,x,label in [(plate,0,'01 / LOCKED PLATE — character crop'),(hero,850,'HERO 01 / BLENDER POLISH — rest pose')]:
 tile=ImageOps.contain(im,(830,1020));canvas.paste(tile,(x+(850-tile.width)//2,65+(1020-tile.height)//2));d.text((x+22,20),label,font=font,fill='#283952')
canvas.save(P/'Hero_01_compare.png')
sheet=Image.new('RGB',(2000,670),'#F4F0E6');d=ImageDraw.Draw(sheet)
for i,(im,label) in enumerate([(plate,'PLATE'),(Image.open(P/'Hero_01_front.png'),'FRONT'),(Image.open(P/'Hero_01_side.png'),'SIDE'),(hero,'THREE-QUARTER')]):
 im=ImageOps.contain(im.convert('RGB'),(490,600));sheet.paste(im,(i*500+(500-im.width)//2,60));d.text((i*500+15,15),label,font=font,fill='#283952')
sheet.save(P/'Hero_01_review.jpg',quality=94)
