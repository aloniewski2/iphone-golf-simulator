import sys, json, copy
sys.path.insert(0, '/Users/adnanyonathan/Documents/Codex/2026-09-20/wh/outputs/iphone-golf-simulator/work/accessories/py')
import bag_fit as BF, bag_strap_eval as SE
import acc_common as A
a = A.args_after_dashes()
sex = a[0]; variants = json.loads(a[1])
ev = SE.StrapEval(sex)
base = copy.deepcopy(BF.CFG)
for name, ov in variants.items():
    BF.CFG.clear(); BF.CFG.update(copy.deepcopy(base))
    for k, v in ov.items():
        if isinstance(v, dict) and isinstance(BF.CFG.get(k), dict): BF.CFG[k].update(v)
        else: BF.CFG[k] = v
    st = SE.build_strap(sex, ev)
    res = ev.evaluate(st['pos'], st['bi'], st['bw'])
    print('%-14s rest %5.1f | ' % (name, st['rest_min'] * 1000) + ' '.join('%s %6.1f' % (c[:5], res[c]['worst'] * 1000) for c in SE.CRIT), flush=True)
