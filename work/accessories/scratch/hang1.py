import sys
sys.path.insert(0, '/Users/adnanyonathan/Documents/Codex/2026-09-20/wh/outputs/iphone-golf-simulator/work/accessories/py')
import bag_hang as H
a = sys.argv[sys.argv.index('--')+1:]
sex = a[0]
for lean in (-6.0, -9.0):
    H.solve_hang(sex, lean, 3.0)
