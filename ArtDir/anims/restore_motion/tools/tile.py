import sys
from PIL import Image
files=sys.argv[2:];ims=[Image.open(f).convert('RGB') for f in files];w,h=ims[0].size;n=len(ims);cols=min(n,4);rows=(n+cols-1)//cols
s=Image.new('RGB',(w*cols,h*rows),'white')
for i,im in enumerate(ims):s.paste(im,((i%cols)*w,(i//cols)*h))
s.save(sys.argv[1])
