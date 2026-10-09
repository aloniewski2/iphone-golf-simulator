from PIL import Image,ImageDraw,ImageFont
from pathlib import Path
import json
P=Path(__file__).resolve().parent;frames=[];meta=json.loads((P/'preview_frames.json').read_text())
font=ImageFont.truetype('/System/Library/Fonts/Supplemental/Arial Bold.ttf',18)
for x in meta:
 im=Image.open(P/'preview_frames'/x['file']).convert('RGB');d=ImageDraw.Draw(im);d.rounded_rectangle((12,12,im.width-12,49),radius=8,fill='#192844');d.text((24,20),x['clip'].upper()+'  |  Default wardrobe',font=font,fill='white');frames.append(im)
frames[0].save(P/'mixamo_preview.gif',save_all=True,append_images=frames[1:],duration=[67 if i%3 else 66 for i in range(len(frames))],loop=0,optimize=True,disposal=2)
indices=[0,22,44,45,63,82];sheet=Image.new('RGB',(440*3,520*2),'#eeeeee')
for i,k in enumerate(indices):sheet.paste(frames[min(k,len(frames)-1)],((i%3)*440,(i//3)*520))
sheet.save(P/'motion_contact_sheet.jpg',quality=90)
print('GIF_DONE',len(frames))
