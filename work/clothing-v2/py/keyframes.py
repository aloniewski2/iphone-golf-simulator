"""Key frames of the main clips for one hero, new kit (and optionally old kit) -> one sheet.
Blender ... -- <hero> <out.png> [old]"""
import sys, os, subprocess
sys.path.insert(0, os.path.dirname(__file__))
import cv2io
a = sys.argv[sys.argv.index('--') + 1:]
hero_id, outp = a[0], a[1]; old = len(a) > 2 and a[2] == 'old'
hero = cv2io.Hero(hero_id)
tennis = ['ReadyIdle', 'ForehandArmCorrected', 'BackhandArmCorrected', 'Serve', 'RunForward', 'Walk', 'Overhead', 'VolleyForehand']
golf = ['Idle', 'IronSwing', 'Drive', 'Putt', 'Walk', 'Chip']
names = [c['name'] for c in hero.clips]
sel = [n for n in names if any(n.endswith(t) for t in (golf if hero.golf else tennis))]
B = '/Applications/Blender.app/Contents/MacOS/Blender'
tmp = os.path.join(cv2io.ROOT, 'proof', 'kf'); os.makedirs(tmp, exist_ok=True)
files = []
for n in sel:
    c = next(c for c in hero.clips if c['name'] == n)
    for k, fr in enumerate([0, c['frames'] // 3, (2 * c['frames']) // 3]):
        f = os.path.join(tmp, f'{hero_id}_{n}_{fr}_{"old" if old else "new"}.png')
        env = dict(os.environ); env['CV2_OLD'] = '1' if old else '0'
        subprocess.run([B, '-b', '--factory-startup', '--python', os.path.join(os.path.dirname(__file__), 'posed.py'), '--', hero_id, n, str(fr), f, '-30', '8'],
                       env=env, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        files.append(f)
from PIL import Image
ims = [Image.open(f).crop((150, 80, 750, 1150)).resize((300, 535)) for f in files if os.path.exists(f)]
cols = 9
rows = (len(ims) + cols - 1) // cols
sheet = Image.new('RGB', (cols * 300, rows * 535), (120, 120, 122))
for i, im in enumerate(ims): sheet.paste(im, ((i % cols) * 300, (i // cols) * 535))
sheet.save(outp)
print('frames', len(ims))
