from pathlib import Path
from PIL import Image,ImageDraw,ImageFilter
import json
P=Path(__file__).resolve().parent;d=json.loads((P/'skin-mask-input.json').read_text());im=Image.open(d['source_color']).convert('RGB');w,h=im.size
region=Image.new('L',(w,h));draw=ImageDraw.Draw(region)
for tri in d['triangles']:draw.polygon([(u*(w-1),(1-v)*(h-1)) for u,v in tri],fill=255)
region=region.filter(ImageFilter.MaxFilter(9))
face=Image.new('L',(w,h));fd=ImageDraw.Draw(face)
for tri in d['face_triangles']:fd.polygon([(u*(w-1),(1-v)*(h-1)) for u,v in tri],fill=255)
face=face.filter(ImageFilter.MaxFilter(5))
vals=[];lumtotal=0;count=0;tot=[0,0,0]
def linear(v):return v/12.92 if v<=.04045 else ((v+.055)/1.055)**2.4
for (rr,gg,bb),reg,fac in zip(im.getdata(),region.getdata(),face.getdata()):
 r,g,b=rr/255,gg/255,bb/255;valid=reg>0 and g>r*.55 and (not fac or (r>g*1.015 and r>b*1.04 and g>.26))
 vals.append(255 if valid else 0)
 if valid:
  lumtotal+=linear(r)*.2126+linear(g)*.7152+linear(b)*.0722;count+=1;tot[0]+=rr;tot[1]+=gg;tot[2]+=bb
alpha=Image.new('L',(w,h));alpha.putdata(vals);alpha=alpha.filter(ImageFilter.GaussianBlur(.45));mask=Image.new('RGBA',(w,h),(0,0,0,0));mask.putalpha(alpha);mask.save(P/'Hero_01_SkinMask.png');ref=lumtotal/count
(P/'Hero_01_SkinMask.json').write_text(json.dumps({'skin':ref,'mask_channel':'alpha','rgb_channels':'zero; existing KitRecolor-compatible','color_space':'mask linear; base-color sRGB','reference_mean_linear_luminance':ref,'mean_srgb_hex':'#'+''.join(f'{round(c/count):02X}' for c in tot),'example_tones':['#F2C093','#B77950','#6A4030'],'default':'Keep _Skin.a=0 to preserve approved source texture exactly; >0 enables recoloring.'},indent=2))
print('Skin mask reference',ref,'coverage texels',count)
