"""Fetch pinned public CC0 inputs, verify size and MD5 before Blender loads them."""
import argparse,json,pathlib,subprocess,hashlib,concurrent.futures
R=pathlib.Path(__file__).resolve().parents[3]
p=argparse.ArgumentParser();p.add_argument('--cache',type=pathlib.Path,default=R/'ArtDir/golf/vendor/polyhaven');a=p.parse_args()
metadata=json.loads((R/'ArtDir/golf/vendor/coastal-botany-downloads.json').read_text());items=[]
for name,data in metadata.items():
 source=data['source'];out=a.cache/name;items.append((out/(name+'_1k.blend'),source));items.extend((out/k,v) for k,v in source['include'].items())
def fetch(item):
 path,spec=item;path.parent.mkdir(parents=True,exist_ok=True)
 if not path.exists() or path.stat().st_size!=spec['size'] or hashlib.md5(path.read_bytes()).hexdigest()!=spec['md5']:
  subprocess.run(['curl','--fail','--location','--silent','--show-error','--output',str(path),spec['url']],check=True)
 assert path.stat().st_size==spec['size'] and hashlib.md5(path.read_bytes()).hexdigest()==spec['md5'],str(path)
 return str(path)
with concurrent.futures.ThreadPoolExecutor(max_workers=5) as pool:
 for result in pool.map(fetch,items):print(result)
