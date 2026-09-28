import bpy, sys
argv = sys.argv[sys.argv.index('--') + 1:]
exec(open(__file__.replace('render_nohat.py', 'render_review.py')).read().split('# --- rest views')[0].replace("OUT = argv[0]; BG = argv[1] if len(argv) > 1 else 'white'", "OUT = argv[0]; BG = 'magenta'"))
bpy.data.objects['Hat_Visor'].hide_render = True
pose({})
for yaw, pitch, n in ((25, 4, 'nohat_34'), (180, 8, 'nohat_back'), (90, 0, 'nohat_side'), (270, 0, 'nohat_side2'), (150, 55, 'nohat_top')):
    shoot(n, (0, 0, 1.47), yaw, pitch, 1.25, 50, (700, 700))
