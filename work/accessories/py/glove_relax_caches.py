"""apply glove_fit.relax_folds to every cached hero fit (products/glove_hero_<tag>.npz): Vh <- relaxed, Vh_prefold keeps the clearance-hug result.
Blender -b --python glove_relax_caches.py -- <Male|Male_R|Female|Female_R> ..."""
import sys, os
sys.path.insert(0, os.path.dirname(__file__))
import numpy as np
import acc_common as A, glove_fit as FT
for tag in A.args_after_dashes():
    sex = tag.split('_')[0]; side = 'Right' if tag.endswith('_R') else 'Left'
    hs = FT.HeroSkin(sex, side)
    p = os.path.join(A.ROOT, 'products', f'glove_hero_{tag}.npz')
    d = dict(np.load(p))
    V0 = d['Vh_prefold'] if 'Vh_prefold' in d else d['Vh']
    V = FT.relax_folds(V0, d['T'], hs)
    cc, _ = hs.clear(V, with_dir=True)
    print(tag, 'folds', len(FT.fold_faces(V0, d['T'])), '->', len(FT.fold_faces(V, d['T'])), ' min clearance mm %.2f' % (cc.min() * 1000), ' vertices below 0.8 mm:', int((cc < .0008).sum()), 'of', len(cc))
    d['Vh_prefold'] = V0; d['Vh'] = V
    np.savez(p, **d)
