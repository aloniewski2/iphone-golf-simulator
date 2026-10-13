import sys, subprocess, os, glob
from PIL import Image, ImageDraw
# burst.py video out.jpg start step count cropx0 cropx1 [cols]
v,out,start,step,count,x0,x1=sys.argv[1],sys.argv[2],float(sys.argv[3]),float(sys.argv[4]),int(sys.argv[5]),int(sys.argv[6]),int(sys.argv[7])
cols=int(sys.argv[8]) if len(sys.argv)>8 else 6
d=out+'.d'; os.makedirs(d,exist_ok=True)
ts=[f'{start+i*step:.2f}' for i in range(count)]
subprocess.run([os.path.dirname(os.path.abspath(__file__))+'/frames',v,d]+ts,check=True,capture_output=True)
w=(x1-x0)*540//720; h=540; rows=(count+cols-1)//cols
S=Image.new('RGB',(w*cols,h*rows))
for i,t in enumerate(ts):
    p=f'{d}/f_{t}.jpg'
    if not os.path.exists(p): continue
    im=Image.open(p).crop((x0,0,x1,720)).resize((w,h)); dr=ImageDraw.Draw(im); dr.rectangle((0,0,60,16),fill=(0,0,0)); dr.text((3,3),t,fill=(255,255,0))
    S.paste(im,((i%cols)*w,(i//cols)*h))
S.save(out,quality=80)
