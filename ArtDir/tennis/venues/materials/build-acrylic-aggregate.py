from pathlib import Path
import numpy as np
from PIL import Image
import argparse
parser=argparse.ArgumentParser();parser.add_argument('--out',type=Path,default=Path(__file__).parents[4]/'Unity/Assets/Resources/Tennis/Premium');args=parser.parse_args()
out=args.out;out.mkdir(parents=True,exist_ok=True)
rng=np.random.default_rng(19073);n=512
raw=rng.random((n,n));f=np.fft.fft2(raw);fy=np.fft.fftfreq(n)[:,None];fx=np.fft.fftfreq(n)[None,:]
h=np.fft.ifft2(f*np.exp(-((fx*fx+fy*fy)/.10))).real
h=(h-h.min())/(h.max()-h.min())
dx=(np.roll(h,-1,1)-np.roll(h,1,1))*.7;dz=(np.roll(h,-1,0)-np.roll(h,1,0))*.7
normal=np.stack([-dx,-dz,np.ones_like(h)],axis=-1);normal/=np.linalg.norm(normal,axis=-1,keepdims=True)
rgba=np.dstack([normal*.5+.5,h]);Image.fromarray((rgba*255).clip(0,255).astype('uint8'),'RGBA').save(out/'AcrylicAggregate.png')
print('512² seamless aggregate, RGB normal/alpha pigment height; one tile = 1 metre')
