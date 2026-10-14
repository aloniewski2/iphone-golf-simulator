import sys
sys.path.insert(0, '/Users/adnanyonathan/Documents/Codex/2026-09-20/wh/outputs/iphone-golf-simulator/work/accessories/py')
import bag_hang as H
a = sys.argv[sys.argv.index('--')+1:]
sex = a[0]
mot = H.Motion2(sex, step=1)
for rim in [float(v) for v in a[1].split(',')]:
    H.solve_hang(sex, -6.0, 3.0, rim=rim, mot=mot)
