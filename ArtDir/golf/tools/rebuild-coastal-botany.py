"""Build the editable artist-derived botanical kit; never runs Unity or edits gameplay."""
import argparse,pathlib,subprocess,sys
R=pathlib.Path(__file__).resolve().parents[3];tools=R/'ArtDir/golf/tools'
p=argparse.ArgumentParser();p.add_argument('--cache',type=pathlib.Path,default=R/'ArtDir/golf/vendor/polyhaven');p.add_argument('--out',type=pathlib.Path,default=R/'ArtDir/golf/source/coastal-botany-generated');p.add_argument('--blender',default='/Applications/Blender.app/Contents/MacOS/Blender');p.add_argument('--fetch',action='store_true');a=p.parse_args()
if a.fetch:subprocess.run([sys.executable,str(tools/'fetch-coastal-artists.py'),'--cache',str(a.cache)],check=True)
args=['--cache',str(a.cache.resolve()),'--out',str(a.out.resolve())]
for step in ['build-coastal-botany.py','bake-coastal-botany.py','pack-coastal-botany-maps.py','finalize-coastal-botany.py','proof-coastal-botany.py']:
 if step.startswith('pack-'):cmd=[sys.executable,str(tools/step),'--']+args
 else:cmd=[a.blender,'--background','--disable-autoexec','--python',str(tools/step),'--']+args
 subprocess.run(cmd,check=True)
 if step=='build-coastal-botany.py':assert (a.out/'CoastalBotany.blend').exists()
print('Editable blend, FBX, maps, mesh manifest and actual source proofs:',a.out)
