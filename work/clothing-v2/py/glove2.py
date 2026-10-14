"""GLOVE v2 (golf): a leather golf glove (a tight garment: it follows the real hand), cut cleanly at the wrist with a wrist strap band; fingers fully covered.
The shell is the hand + fingers skin region (Taubin-softened so knuckles and tendons do not read), offset by gap + thickness, pushed clear of the skin,
smart-unwrapped; per-vertex finger ids for the finger seams and a dorsal mask for the perforated back of the hand.
Blender -b --factory-startup --python glove2.py -- <hero> <side L|R> <out.blend>"""
import sys, os, math
sys.path.insert(0, os.path.dirname(__file__))
import numpy as np
import bpy, bmesh
from mathutils import Vector
from polo2 import apply_modifier, smoothstep, set_vattr, new_object
KIND = {'shell': 0, 'band': 12, 'tab': 17}


def build(hero_id, side, outp):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    import cv2io, glib as G, fit
    hero = cv2io.Hero(hero_id); body = G.Body(hero)
    s = 'Left' if side == 'L' else 'Right'
    arm = body.armL if side == 'L' else body.armR
    hand = body.handL if side == 'L' else body.handR
    wrist = body.head(s + 'Hand'); elbow = body.head(s + 'LowerArm')
    ax = (wrist - elbow); ax /= np.linalg.norm(ax)
    p = body.pos
    t_all = (p - wrist) @ ax
    cuff_len = .042
    sel = (arm > .5) & (t_all > -cuff_len - .012)
    tri = body.tri[sel[body.tri].all(1)]
    r = G.compact(p, tri); r = G.largest_component(r[0], r[1])
    pos, tri = r[0], r[1]
    # clean cut at the cuff line
    f = (pos - wrist) @ ax + cuff_len
    x2, t2, f2, a2 = G.cut_isoline(pos, tri, f, [])
    pos, tri, _, _, _ = G.keep_side(x2, t2, f2, [], 1)
    r = G.compact(pos, tri); pos, tri = r[0], r[1]
    nb = G.neighbours(len(pos), tri)
    x = pos.copy()
    for _ in range(5): x += .5 * G.umbrella(x, nb); x -= .52 * G.umbrella(x, nb)
    n = G.vertex_normals(x, tri)
    gap, thick = .0010, .0014
    x = x + n * (gap + thick)
    d_, _ = fit.signed(body, x)
    x = x + n * np.clip(gap + thick * .8 - d_, 0, .004)[:, None]
    n = G.vertex_normals(x, tri)
    cen = x.mean(0)
    if np.mean(np.einsum('ij,ij->i', G.face_normals(x, tri), x[tri].mean(1) - cen)) < 0: tri = tri[:, ::-1]
    # per-vertex finger id (argmax among the digit bones of this hand; palm/back = -1) and surface weights from the body
    cp, fi, dd = body.nearest(x)
    bc = body.bary(cp, fi)
    Wn = body.weights_at(fi, bc)
    digits = ['Thumb', 'Index', 'Middle', 'Ring', 'Little']
    fid = np.full(len(x), -1.0)
    for k, dgt in enumerate(digits):
        idx = [i for i, nm in enumerate(body.bones) if nm.startswith(s + dgt)]
        w = Wn[:, idx].sum(1)
        better = w > .5
        fid[better] = k
    # dorsal mask: the side of the palm plane facing away from the body (back of the hand)
    palm = x[(fid < 0) & (((x - wrist) @ ax) > .0) & (((x - wrist) @ ax) < .09)]
    c0 = palm.mean(0); u_, s_, vt = np.linalg.svd(palm - c0); pn = vt[2]
    out = np.array([1.0 if side == 'R' else -1.0, 0, 0]) + np.array([0, 0, 1.0])
    if pn @ out < 0: pn = -pn
    dorsal = (n @ pn > .5).astype(float)
    # ---- the object: shell (faces below the strap line) and the strap band (separate, a little proud)
    t_v = (x - wrist) @ ax
    strap_top = -cuff_len + .022
    # cut exactly at the strap line (clean edge between the glove and its strap), carrying the per-vertex data across the cut
    x, tri, _f, A2 = G.cut_isoline(x, tri, t_v - strap_top, [n, fid[:, None], dorsal[:, None], t_v[:, None]])
    n = A2[0]; n = n / (np.linalg.norm(n, axis=1, keepdims=True) + 1e-12); fid = A2[1][:, 0]; dorsal = A2[2][:, 0]; t_v = A2[3][:, 0]
    fcz = t_v[tri].mean(1)
    shell_t = tri[fcz >= strap_top - 1e-6]; strap_t = tri[fcz < strap_top - 1e-6]
    def part(nm, t_, lift, kind, fabric):
        used = np.unique(t_); remap = -np.ones(len(x), int); remap[used] = np.arange(len(used))
        ob = new_object(nm, x[used] + n[used] * lift, [tuple(remap[a] for a in tt) for tt in t_])
        me = ob.data
        for k_, v_ in (('role', 0), ('face_kind', kind), ('fabric', fabric), ('panel', 0 if nm == 'Glove' else 1)):
            a = me.attributes.new(k_, 'INT', 'FACE'); a.data.foreach_set('value', np.full(len(me.polygons), v_, np.int32))
        for k_, v_ in (('fid', fid), ('dorsal', dorsal), ('t_wrist', t_v), ('f_strap', t_v - strap_top)):
            a = me.attributes.new(k_, 'FLOAT', 'POINT'); a.data.foreach_set('value', np.asarray(v_[used], np.float32))
        return ob
    glove = part('Glove', shell_t, 0.0, KIND['shell'], 8)
    strap = part('Strap', strap_t, .0012, KIND['band'], 8)
    objs = [glove, strap]
    # UVs: smart project (the hand has no simple developable layout), then every part gets its own islands
    for o in objs:
        bpy.context.view_layer.objects.active = o
        for oo in bpy.context.selected_objects: oo.select_set(False)
        o.select_set(True)
        bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
        bpy.ops.uv.smart_project(angle_limit=math.radians(70), island_margin=.004, scale_to_bounds=False)
        bpy.ops.object.mode_set(mode='OBJECT')
    for o in objs:
        o.data.shade_smooth()
        for nm in ('outer', 'inner', 'rim'): o.data.materials.append(bpy.data.materials.get(nm) or bpy.data.materials.new(nm))
        sm = o.modifiers.new('solid', 'SOLIDIFY'); sm.thickness = thick if o.name == 'Glove' else thick * 1.3; sm.offset = -1; sm.use_even_offset = True
        sm.use_quality_normals = True; sm.use_rim = True; sm.material_offset = 1; sm.material_offset_rim = 2
        apply_modifier(o, sm)
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs: o.select_set(True)
    bpy.context.view_layer.objects.active = glove
    bpy.ops.object.join()
    glove.name = 'Kit_Glove_' + side; glove['roles'] = ['Kit_Glove']
    bpy.ops.wm.save_as_mainfile(filepath=outp)
    print('GLOVE %s faces %d (verts %d)' % (side, len(glove.data.polygons), len(glove.data.vertices)))


if __name__ == '__main__' or 'glove2.py' in ' '.join(sys.argv):
    a = sys.argv[sys.argv.index('--') + 1:]
    build(a[0], a[1], a[2])
