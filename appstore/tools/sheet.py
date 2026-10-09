import sys, glob
from PIL import Image
pre=sys.argv[1]; w=int(sys.argv[2]) if len(sys.argv)>2 else 440
fs=sorted(glob.glob(f'../screenshots/{pre}*.jpg')); ims=[Image.open(f) for f in fs]
h=int(2868*w/1320); S=Image.new('RGB',(w*len(ims)+10*(len(ims)+1),h+20),(60,60,60))
for i,im in enumerate(ims): S.paste(im.resize((w,h)),(10+i*(w+10),10))
S.save(f'../build/sheet{pre}.jpg')
