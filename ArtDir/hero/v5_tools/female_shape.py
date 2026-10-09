"""Hero V5 girl body: one 'Female' shape key, the SAME spatial field applied to the body and the clothes over it
(so the shirt/shorts stay over the skin): narrower waist and shoulders, slightly wider hips, a modest bust.
Unity HeroKit sets the 'Female' blend shape to 100 for a girl; the locker bakes both (tag body=Girl/Boy)."""
import bpy, math
from mathutils import Vector
CY = .065   # torso centre (y); forward is -Y
def g(x, mu, s): return math.exp(-((x - mu) / s) ** 2)
def smooth(e0, e1, x):
    t = min(1, max(0, (x - e0) / (e1 - e0))); return t * t * (3 - 2 * t)
def field(p):
    x, y, z = p; ax = abs(x); d = Vector((0, 0, 0))
    torso = 1 - smooth(.17, .24, ax) if z > .95 else 1.0      # arms (outside the shoulder joint) keep their place
    # waist: narrower and a touch shallower
    w = g(z, .90, .07) * torso
    d.x += -x * .075 * w; d.y += -(y - CY) * .04 * w
    # hips + top of the thighs: a little wider
    h = g(z, .76, .08)
    d.x += x * .06 * h
    d.y += (y - CY) * .03 * h * (1 if y > CY else 0)          # a little fuller seat
    # shoulders / upper chest: slightly narrower (torso only)
    s = smooth(1.02, 1.12, z) * (1 - smooth(1.2, 1.28, z)) * torso
    d.x += -x * .05 * s
    # bust: two soft domes on the front of the chest
    if y < CY:
        for sx in (-.075, .075):
            b = g(x, sx, .055) * g(z, 1.04, .05) * smooth(CY, CY - .06, y)
            d.y += -.02 * b
            d.z += -.004 * b
    return d
def add(context, report, names=('Body_Skin', 'Shirt_Default', 'Shorts_Default')):
    for n in names:
        o = bpy.data.objects.get(n)
        if not o: continue
        if not o.data.shape_keys: o.shape_key_add(name='Basis', from_mix=False)
        k = o.data.shape_keys.key_blocks.get('Female') or o.shape_key_add(name='Female', from_mix=False)
        mw = o.matrix_world; inv = mw.inverted().to_3x3(); moved = 0
        for i, v in enumerate(o.data.vertices):
            wp = mw @ v.co; dd = field(wp)
            k.data[i].co = v.co + inv @ dd
            if dd.length > .002: moved += 1
        report['female_' + n] = moved
