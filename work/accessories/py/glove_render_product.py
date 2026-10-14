"""Render the glove product (with its N/M maps) from the reference sheet's views. Blender -b --python glove_render_product.py -- <closed|open> <outdir> [view,view...]"""
import sys, os, math
sys.path.insert(0, os.path.dirname(__file__))
import numpy as np, bpy
import acc_common as A, acc_preview as AP, glove_view as GV

PROD = os.path.join(A.ROOT, 'products', 'glove_v1')
# name: (target, direction-to-camera, up, dist, lens)
VIEWS = {
    'back': ((0, .075, 0), (0, 0, 1), (0, 1, 0), .62, 70),
    'palm': ((0, .075, 0), (0, 0, -1), (0, 1, 0), .62, 70),
    'side': ((0, .06, 0), (-1, 0, .0), (0, 1, 0), .62, 70),
    'sidethumb': ((0, .06, 0), (1, 0, .0), (0, 1, 0), .62, 70),
    'q34': ((0, .07, 0), (.62, -.45, .64), (0, 1, 0), .62, 70),
    'q34b': ((0, .07, 0), (-.62, -.45, .64), (0, 1, 0), .62, 70),
    'wrist': ((0, -.034, 0), (0, -1, .35), (0, 0, 1), .26, 70),
    'wrist2': ((0, -.034, .004), (.28, -1, .5), (0, 0, 1), .21, 70),
    'tab': ((0, -.018, .02), (.2, -.9, .75), (0, 0, 1), .21, 70),
    'tab_open': ((.02, -.02, .02), (.75, -.6, .6), (0, 0, 1), .22, 70),
    'tab_open2': ((0, -.02, .02), (.0, -.9, .8), (0, 0, 1), .22, 70),
    'cuff_close': ((0, -.034, .015), (0, -1, .6), (0, 0, 1), .15, 70),
    'finger_close': ((0, .15, .0), (.1, -.2, 1), (0, 1, 0), .24, 70),
    'finger_palm': ((0, .13, .0), (.0, -.2, -1), (0, 1, 0), .30, 70),
    'palm_close': ((0, .05, 0), (0, -.1, -1), (0, 1, 0), .32, 70),
    'back_close': ((0, .04, 0), (0, -.1, 1), (0, 1, 0), .30, 70),
    'thumb_close': ((.05, .06, -.02), (.9, -.2, -.3), (0, 0, 1), .30, 70),
}


def load_piece(d, name='Kit_Glove_L', item='golf_glove', tag='Glove'):
    g = np.load(os.path.join(d, name + '_final.npz'))
    return AP._object_from_npz(g, 'glove', d, tag, item)


if __name__ == '__main__':
    a = A.args_after_dashes()
    mode, outdir = a[0], a[1]
    views = a[2].split(',') if len(a) > 2 else ['back', 'palm', 'side', 'q34', 'wrist', 'tab']
    A.reset()
    ob = load_piece(os.path.join(PROD, mode))
    GV.setup((1000, 1000), bg=(.34, .35, .37), samples=64)
    for v in views:
        tgt, d, up, dist, lens = VIEWS[v]
        GV.shoot(os.path.join(outdir, f'glove_{mode}_{v}.png'), tgt, d, dist=dist, lens=lens, up=up)
