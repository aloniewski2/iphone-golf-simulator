import bpy, sys, math
from mathutils import Vector, Matrix, Euler
OUT = sys.argv[sys.argv.index('--') + 1]
exec(open(__file__.replace('probe_dive.py', 'render_review.py')).read().split('# --- rest views')[0].replace("OUT = argv[0]; BG = argv[1] if len(argv) > 1 else 'white'", "OUT = '%s'; BG = 'white'" % OUT))
lunge = {'UpperLeg.R': (-95, 0, 0), 'LowerLeg.R': (95, 0, 0), 'UpperLeg.L': (35, 0, 0), 'LowerLeg.L': (40, 0, 0), 'Spine': (25, 0, 0)}
pose(lunge); hips_drop(.3)
shoot('dive_leg', (.15, .15, .35), 270, 5, 1.2, 50, (900, 900))
bpy.data.objects['Shorts_Default'].hide_render = True
shoot('dive_leg_noshorts', (.15, .15, .35), 270, 5, 1.2, 50, (900, 900))
b = bpy.data.objects['Body_Skin']
for m in b.modifiers:
    if m.type == 'MASK': m.show_render = False
shoot('dive_leg_nomask', (.15, .15, .35), 270, 5, 1.2, 50, (900, 900))
