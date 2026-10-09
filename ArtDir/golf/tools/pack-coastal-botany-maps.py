import sys
from pathlib import Path
from PIL import Image,ImageFilter
import json
import argparse
R=Path(__file__).resolve().parents[3]
p=argparse.ArgumentParser();p.add_argument('--cache',type=Path,default=R/'ArtDir/golf/vendor/polyhaven');p.add_argument('--out',type=Path,default=R/'ArtDir/golf/source/coastal-botany-generated')
a=p.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
V=a.cache.resolve();OUT=a.out.resolve();OUT.mkdir(parents=True,exist_ok=True)
O=OUT/'maps';O.mkdir(exist_ok=True)
for a,r,n in [('fir_tree_01','twig','FirNeedles'),('fir_sapling_medium','twigs','SaplingNeedles'),('fir_tree_01','bark','FirBark'),('fir_tree_01','trunk_c','FirTrunkC'),('shrub_02','','Shrub'),('fern_02','','Fern')]:
 stem=a+('_'+r if r else '')
 diff=V/a/'textures'/(stem+'_diff_1k.'+('jpg' if n in ['Shrub','Fern'] else 'png'));c=Image.open(diff).convert('RGBA')
 if n in ['FirNeedles','SaplingNeedles','Shrub','Fern']:c.putalpha(Image.open(V/a/'textures'/(stem+'_alpha_1k.png')).convert('L'))
 c.save(O/(n+'_C.png'))
 normal=V/a/'textures'/(stem+'_nor_gl_1k.png')
 if normal.exists():Image.open(normal).save(O/(n+'_N.png'))
for l in 'ABCD':
 atlas=Image.new('RGBA',(1536,2048))
 for i in range(4):
  im=Image.open(O/('fir_'+l+'_view'+str(i)+'.png')).convert('RGBA')
  assert im.size==(768,1024),(l,i,im.size)
  # Source must contain the complete crown, not an edge-clipped photograph.
  box=im.getchannel('A').getbbox();assert box[0]>0 and box[2]<768,(l,i,box)
  atlas.paste(im,((i%2)*768,(i//2)*1024))
 atlas.save(O/('FirView_'+l+'_C.png'))
print('Packed six artist material roles + four true distant tree atlases')
