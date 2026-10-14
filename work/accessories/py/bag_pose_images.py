"""BAG: render the worst frames of the critical clips (from pose_check.json). plain python3.  python3 py/bag_pose_images.py <Male|Female>"""
import sys, os, json, subprocess
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..'))
BL = '/Applications/Blender.app/Contents/MacOS/Blender'
sex = sys.argv[1]
res = json.load(open(os.path.join(ROOT, 'out', sex, 'golf_bag', 'pose_check.json')))
sel = []
for key, r in res.items():
    if r['hero'].startswith('GolfKitHero') and r['critical']:
        for fr in {r['strap_frame'], r['bag_frame']}:
            sel.append(f"{r['clip']}:{fr}")
out = os.path.join(ROOT, 'proof', 'bag', f'pose_{sex}'); os.makedirs(out, exist_ok=True)
cmd = [BL, '-b', '--factory-startup', '--python', os.path.join(ROOT, 'py', 'bag_pose_render.py'), '--', sex, out, 'pw', 'GolfKitHero', ','.join(sel), 'back34,front34r,strap', '700x900']
subprocess.run(cmd, cwd=ROOT, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
print('rendered', sel)
