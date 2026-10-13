"""the runtime blend of the pose correctives (same maths as KitV2PoseCorrectives / GolfGarmentCorrectives)."""
import numpy as np
from correctives import features


def weights(hero, bones_world, feats):
    f = features(hero, bones_world)
    d = np.mean((feats - f[None, :]) ** 2, axis=1)
    w = 1.0 / np.maximum(d, 1e-10) ** 2
    return w / w.sum()


def blend(hero, bones_world, C):
    w = weights(hero, bones_world, C['feats'])
    shapes = C['shapes']
    # anchors 1..K carry shapes 0..K-1 (anchor 0 = rest, the rest are neutral)
    k = len(shapes)
    return np.einsum('k,kvc->vc', w[1:1 + k], shapes) if k else 0
