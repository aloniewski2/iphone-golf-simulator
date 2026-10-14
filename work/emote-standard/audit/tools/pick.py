import sys, glob, os
from PIL import Image
out=sys.argv[1]; ids=[int(x) for x in sys.argv[2].split(',')]; cols=int(sys.argv[3]) if len(sys.argv)>3 else 4; T=int(sys.argv[4]) if len(sys.argv)>4 else 400
fs={int(os.path.basename(f)[:3]):f for f in glob.glob('CLIPPING_AUDIT_images/[0-9]*.jpg')}
rows=(len(ids)+cols-1)//cols; P=Image.new('RGB',(cols*T,rows*T))
for i,n in enumerate(ids): P.paste(Image.open(fs[n]).resize((T,T)),((i%cols)*T,(i//cols)*T))
P.save(out,quality=82)
