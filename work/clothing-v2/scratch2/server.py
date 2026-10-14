"""persistent headless Blender: preloads heroes, then runs scratch2/cmd_*.py files in one namespace (so Hero loading is paid once)."""
import sys, os, glob, time, traceback, builtins
D = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, os.path.join(D, '..', 'py'))
import cv2io, glib as G
builtins._HCACHE = {}
for h in os.environ.get('SRV_HEROES', 'MatchHero_Male').split(','):
    hero = cv2io.Hero(h); builtins._HCACHE[h] = (hero, G.Body(hero)); print('LOADED', h, flush=True)
NS = {'__name__': 'cmd'}
while True:
    for f in sorted(glob.glob(os.path.join(D, 'cmd_*.py'))):
        src = open(f).read(); os.rename(f, f + '.done')
        try: exec(compile(src, f, 'exec'), NS)
        except Exception: traceback.print_exc()
        print('DONE', os.path.basename(f), flush=True)
    time.sleep(.4)
