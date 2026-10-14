"""Converts out/<Sex>/<item>/piece.json (+ the piece entries) into unity_item.json, the schema Unity/Assets/Editor/AccessoriesInstall.cs reads.
python3 py/prep_install.py [item ...]"""
import json, os, sys, glob
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..'))
OUT = os.path.join(ROOT, 'out')
CLOTH = {'tennis_cap': 'Acc_Cap', 'golf_cap': 'Acc_Cap', 'golf_glove': 'Acc_Glove', 'golf_bag': 'Acc_Bag'}
RENDERER = {'Kit_Cap': 'Acc_Cap', 'Kit_Glove_L': 'Acc_GloveL', 'Kit_Glove_R': 'Acc_GloveR', 'Kit_Bag': 'Acc_Bag'}
SLOT = {'Headwear': 'Headwear', 'HandGlove': 'Glove', 'Glove': 'Glove', 'BackCarry': 'BackCarry'}


def main():
    only = set(sys.argv[1:])
    for pj in sorted(glob.glob(os.path.join(OUT, '*', '*', 'piece.json'))):
        d = os.path.dirname(pj); item = os.path.basename(d); sex = os.path.basename(os.path.dirname(d))
        if only and item not in only: continue
        m = json.load(open(pj))
        cloth = CLOTH[item]
        pieces = []
        entries = m.get('entries') or {m['pieces'][0]: m['piece']}
        tags = m.get('tags') or {}
        for name in m['pieces']:
            e = entries[name]
            roles = [(cloth + 'Metal') if s['role'] == 'Kit_Buckle' else cloth for s in e['submeshes']]
            pieces.append(dict(name=name, renderer=RENDERER[name], mesh=name, maps=tags.get(name, m.get('tag')), metresPerUv=e['metresPerUv'], vertexCount=e['vertexCount'],
                               triangleCount=e['triangleCount'], texture=e['texture'], bones=e['bones'], bindposes=e['bindposes'], submeshes=e['submeshes'], roles=roles))
        sock = m.get('socket')
        pos = m.get('socket_local_position') or [0, 0, 0]; rot = m.get('socket_local_rotation') or [0, 0, 0, 1]
        uni = dict(item=item, sex=sex, slot=SLOT[m['slot']], parentBone=m.get('parent_bone') or '', socket=sock or '', socketPosition=pos, socketRotation=rot, pieces=pieces)
        json.dump(uni, open(os.path.join(d, 'unity_item.json'), 'w'))
        print(sex, item, 'pieces', [p['renderer'] for p in pieces], 'socket', sock)


main()
