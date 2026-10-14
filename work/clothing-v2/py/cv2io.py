"""CLOTHING V2: read the Unity dump (work/clothing-v2/dump/<hero>/) into numpy. Unity coordinates (left-handed, Y up, hero faces +Z)."""
import json, os
import numpy as np

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..'))
DUMP = os.path.join(ROOT, 'dump')
HEROES = ['MatchHero_Male', 'MatchHero_Female', 'GolfKitHero_Male', 'GolfKitHero_Female']


def mat(a):
    return np.array(a, dtype=np.float64).reshape(4, 4).T   # dumped column-major


class Part:
    def __init__(self, d, info, tag):
        self.info = info
        self.name = info['name']
        n = info['vertexCount']
        f = lambda s, dt, k: np.fromfile(os.path.join(d, f'{tag}_{s}'), dtype=dt).reshape(-1, k) if os.path.exists(os.path.join(d, f'{tag}_{s}')) else None
        self.pos = f('pos.f32', np.float32, 3).astype(np.float64)
        self.nrm = f('nrm.f32', np.float32, 3).astype(np.float64)
        self.tri = f('tri.i32', np.int32, 3)
        self.bi = f('bi.i32', np.int32, 4)
        self.bw = f('bw.f32', np.float32, 4).astype(np.float64)
        self.uv = f('uv.f32', np.float32, 2)
        self.bones = info['bones']
        self.bind = np.array([mat(b) for b in info['bindposes']])
        self.r2w = mat(info['rendererToWorld'])
        self.subs = info['submeshes']
        assert self.pos.shape[0] == n


class Hero:
    def __init__(self, hid):
        self.id = hid
        self.dir = os.path.join(DUMP, hid)
        self.meta = json.load(open(os.path.join(self.dir, 'hero.json')))
        self.female = hid.endswith('Female')
        self.golf = hid.startswith('Golf')
        parts = self.meta['parts']
        self.body = Part(self.dir, parts[0], 'body')
        self.kit = {}
        for p in parts[1:]:
            # golf heroes carry the disabled golfer-FBX kit (54 bones) and the live match-surface kit (53): keep the live one
            if p['name'] in self.kit and len(p['bones']) != len(self.body.bones):
                continue
            if len(p['bones']) != len(self.body.bones) and p['name'] in [q['name'] for q in parts[1:] if len(q['bones']) == len(self.body.bones)]:
                continue
            self.kit[p['name']] = Part(self.dir, p, p['name'])
        self.rig = {k: mat(v) for k, v in self.meta['rig'].items()}
        self.clips = self.meta['clips']
        self.rest_world = np.fromfile(os.path.join(self.dir, 'rest_body_world.f32'), np.float32).reshape(-1, 3).astype(np.float64)
        self.rest_bones = np.fromfile(os.path.join(self.dir, 'rest_bones.f32'), np.float32).reshape(-1, 4, 4).transpose(0, 2, 1).astype(np.float64)

    def clip(self, name):
        c = next(c for c in self.clips if c['name'] == name or c['file'] == name)
        nb = len(self.body.bones)
        body = np.fromfile(os.path.join(self.dir, f"clip_{c['file']}_body.f32"), np.float32).reshape(c['frames'], -1, 3)
        bones = np.fromfile(os.path.join(self.dir, f"clip_{c['file']}_bones.f32"), np.float32).reshape(c['frames'], nb, 4, 4).transpose(0, 1, 3, 2)
        return body, bones

    def skin_matrices(self, bone_world):
        """bone L2W (nb,4,4) -> skinning matrices (nb,4,4) = boneL2W @ bindpose (maps mesh space -> world)."""
        return np.einsum('bij,bjk->bik', bone_world, self.body.bind)


def lbs(pos, bi, bw, skin):
    """linear blend skinning in numpy (Unity Bone4): pos (n,3) mesh space -> world."""
    p4 = np.concatenate([pos, np.ones((len(pos), 1))], 1)
    out = np.zeros((len(pos), 3))
    for k in range(4):
        m = skin[bi[:, k]]                       # (n,4,4)
        out += bw[:, k:k + 1] * np.einsum('nij,nj->ni', m, p4)[:, :3]
    return out


def bone_index(hero, name):
    return hero.body.bones.index(name)
