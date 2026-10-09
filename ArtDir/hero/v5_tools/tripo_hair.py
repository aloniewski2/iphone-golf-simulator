"""Graft the Tripo-sculpted hair onto Hero V5.
Source: Tripo image-to-model of HERO_V5_TARGET_hair_closeup.png, split by Tripo smart segmentation
(v5_tripo/seg/.../seg_model.glb): the largest part is the hair, the part reaching furthest forward is the visor.
Fit: the bust faces +X -> rotate -90 deg about Z; Tripo's visor band ring (centre + radius) is mapped onto the V5
visor ring, so the sculpted hair sits around the band exactly as sculpted and the V5 visor covers the band gap.
The hair is pushed just outside the V5 cap (gap-free underlayer), decimated to budget, given the flat recolourable
hair material and Head weights, and joined onto Hair_Default (whose mesh is the V5 cap).
Called from build_v5.py when 'tripo' is in the steps."""
import bpy, bmesh, math
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree

FIT = None   # optional nudges: dict(s=scale multiplier, t=(dx, dy, dz) metres, rotz=deg)
# Tripo mesh_segmentation + ai_completion (v5_tripo/complete): hair = main mass, fringe, nape and three front tufts;
# the visor part anchors the fit. (Identified by rendering each part: v5_tripo/cparts_sheet*.jpg.)
HAIR_PARTS = ['tripo_part_5', 'tripo_part_9', 'tripo_part_10', 'tripo_part_3', 'tripo_part_4', 'tripo_part_14']
VISOR_PART = 'tripo_part_11'
# Band ring (centre, radius) of Tripo's visor measured on the smart-segmentation visor (rotated frame); completion
# thickens the visor part and biases its ring fit, so the completed model reuses this anchor (identical frame).
ANCHOR = ((-0.003, 0.022, 0.07), 0.313)

def ring(pts):
    """Band circle from the BACK half of a visor (no brim there): algebraic least-squares circle in XY."""
    import numpy as np
    ys = sorted(p.y for p in pts); ymid = ys[len(ys) // 2]
    back = [p for p in pts if p.y > ymid]
    A = np.array([[p.x, p.y, 1.0] for p in back]); b = np.array([-(p.x * p.x + p.y * p.y) for p in back])
    D, E, F = np.linalg.lstsq(A, b, rcond=None)[0]
    cx, cy = -D / 2, -E / 2; r = math.sqrt(max(1e-9, cx * cx + cy * cy - F))
    cz = sum(p.z for p in back) / len(back)
    return Vector((cx, cy, cz)), r

def part_stats(o):
    """Median base-colour (b/r, g/r) and extents of an imported Tripo part."""
    import numpy as np
    me = o.data; mat = me.materials[0] if me.materials else None; img = None
    if mat and mat.use_nodes:
        for n in mat.node_tree.nodes:
            if n.type == 'TEX_IMAGE' and n.image: img = n.image; break
    vs = [v.co for v in me.vertices]
    st = dict(n=len(me.polygons), xmax=max(v.x for v in vs), zmax=max(v.z for v in vs), br=1.0, gr=1.0)
    if img and me.uv_layers.active:
        W, H = img.size; px = np.asarray(img.pixels[:], dtype=np.float32).reshape(H, W, 4); uv = me.uv_layers.active.data
        cols = []
        for p in list(me.polygons)[::max(1, len(me.polygons) // 300)]:
            u = sum(uv[i].uv[0] for i in p.loop_indices) / p.loop_total; v = sum(uv[i].uv[1] for i in p.loop_indices) / p.loop_total
            cols.append(px[min(H - 1, int(v % 1 * H)), min(W - 1, int(u % 1 * W)), :3])
        c = np.median(np.array(cols), 0)
        if c[0] > 1e-3: st['br'] = float(c[2] / c[0]); st['gr'] = float(c[1] / c[0])
    return st

def classify_parts(parts):
    """Hair = golden/brown parts (b/r < .40, g/r > .55) that are big or reach above the face (brows / lashes are
    small and low); visor = the non-hair part reaching furthest forward (+X in Tripo's frame, before rotation)."""
    st = {o.name: part_stats(o) for o in parts}
    hair = [o for o in parts if st[o.name]['br'] < .40 and st[o.name]['gr'] > .55 and (st[o.name]['n'] > 3000 or st[o.name]['zmax'] > .2)]
    rest = [o for o in parts if o not in hair and st[o.name]['n'] > 1000]
    visor = max(rest, key=lambda o: st[o.name]['xmax']) if rest else None
    return hair, visor, st

def anchor_from(context, glb, visor_name=None):
    """Band ring (centre, radius) of the visor part of an UN-completed Tripo segmentation, in the rotated frame."""
    before = set(bpy.data.objects); bpy.ops.import_scene.gltf(filepath=str(glb))
    new = [o for o in bpy.data.objects if o not in before]; parts = [o for o in new if o.type == 'MESH']
    for o in parts: o.data.transform(o.matrix_world); o.matrix_world = Matrix.Identity(4); o.parent = None
    visor = next((o for o in parts if o.name == visor_name), None) if visor_name else classify_parts(parts)[1]
    rot = Matrix.Rotation(math.radians(-90.0), 4, 'Z'); visor.data.transform(rot)
    c, r = ring([v.co.copy() for v in visor.data.vertices])
    for o in new: bpy.data.objects.remove(o)
    return (tuple(c), r)

def graft(context, hair_obj, body, seg_glb, report, cap_off=.003, target_tris=10500, anchor=None, auto=False, drape=False, hair_names=None, visor_name=None):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=str(seg_glb))
    new = [o for o in bpy.data.objects if o not in before]
    parts = [o for o in new if o.type == 'MESH']
    for o in new:
        if o.type != 'MESH': bpy.data.objects.remove(o)
    for o in parts: o.data.transform(o.matrix_world); o.matrix_world = Matrix.Identity(4); o.parent = None
    rot = Matrix.Rotation(math.radians((FIT or {}).get('rotz', -90.0)), 4, 'Z')
    for o in parts: o.data.transform(rot)
    byname = {o.name: o for o in parts}
    if hair_names:
        # per-style part lists picked from render_parts_color.py sheets (v5_styles/<style>/parts.json)
        hs = [byname[n] for n in hair_names if n in byname]; visor = byname.get(visor_name)
        report['tripo_hair_parts'] = [o.name for o in hs]
        me = bpy.data.meshes.new('hairjoin'); bmj = bmesh.new()
        for o in hs: bmj.from_mesh(o.data)
        bmj.to_mesh(me); bmj.free()
        hair = bpy.data.objects.new('hairjoin', me); context.scene.collection.objects.link(hair); parts.append(hair)
    elif auto:
        # classify BEFORE the rotation (the rule uses Tripo's +X forward)
        for o in parts: o.data.transform(rot.inverted())
        hs, visor, st = classify_parts(parts)
        for o in parts: o.data.transform(rot)
        report['tripo_auto_hair_parts'] = [o.name for o in hs]; report['tripo_auto_visor'] = visor.name if visor else None
        me = bpy.data.meshes.new('hairjoin'); bmj = bmesh.new()
        for o in hs: bmj.from_mesh(o.data)
        bmj.to_mesh(me); bmj.free()
        hair = bpy.data.objects.new('hairjoin', me); context.scene.collection.objects.link(hair); parts.append(hair)
    elif all(n in byname for n in HAIR_PARTS + [VISOR_PART]):
        hs = [byname[n] for n in HAIR_PARTS]; visor = byname[VISOR_PART]
        for o in hs[1:]:
            o.data.transform(Matrix.Identity(4))
        me = bpy.data.meshes.new('hairjoin'); bmj = bmesh.new()
        for o in hs: bmj.from_mesh(o.data)
        bmj.to_mesh(me); bmj.free()
        hair = bpy.data.objects.new('hairjoin', me); context.scene.collection.objects.link(hair); parts.append(hair)
    else:
        hair = max(parts, key=lambda o: len(o.data.polygons))
        visor = min((o for o in parts if o is not hair), key=lambda o: min(v.co.y for v in o.data.vertices))   # furthest forward (-Y)
    report['tripo_parts'] = {o.name: len(o.data.polygons) for o in parts}; report['tripo_hair_part'] = hair.name; report['tripo_visor_part'] = visor.name if visor else None
    tc, tr = ring([v.co.copy() for v in visor.data.vertices]) if visor else (Vector((0, 0, 0)), 1.0)
    a_ = anchor or ANCHOR
    if a_: tc, tr = Vector(a_[0]), a_[1]     # band ring measured on the un-completed visor (same frame)
    hat = bpy.data.objects['Hat_Visor']; hmw = hat.matrix_world
    vc, vr = ring([hmw @ v.co for v in hat.data.vertices])
    s = vr / tr * (FIT or {}).get('s', 1.0); off = Vector((FIT or {}).get('t', (0, 0, 0)))
    report['tripo_scale'] = round(s, 4); report['tripo_ring'] = dict(tripo=round(tr, 3), v5=round(vr, 3), tripo_c=[round(x, 3) for x in tc], v5_c=[round(x, 3) for x in vc])
    M = Matrix.Translation(vc + off) @ Matrix.Scale(s, 4) @ Matrix.Translation(-tc)
    hair.data.transform(M)
    bm = bmesh.new(); bm.from_mesh(hair.data)
    bm.faces.ensure_lookup_table(); seen = set(); islands = []
    for f in bm.faces:
        if f.index in seen: continue
        stack = [f]; isl = []; seen.add(f.index)
        while stack:
            g = stack.pop(); isl.append(g)
            for e in g.edges:
                for h in e.link_faces:
                    if h.index not in seen: seen.add(h.index); stack.append(h)
        islands.append(isl)
    big = max(len(i) for i in islands)
    small = [f for isl in islands if len(isl) < .01 * big for f in isl]
    bmesh.ops.delete(bm, geom=small, context='FACES')
    loose = [v for v in bm.verts if not v.link_faces]; bmesh.ops.delete(bm, geom=loose, context='VERTS')
    cap_vs = [hair_obj.matrix_world @ v.co for v in hair_obj.data.vertices]
    tree = BVHTree.FromPolygons(cap_vs, [list(p.vertices) for p in hair_obj.data.polygons])
    C = Vector((0, 0, 1.405)); pushed = 0
    for v in bm.verts:
        d = v.co - C
        if d.length < 1e-4: continue
        hit = tree.ray_cast(C + d.normalized() * .5, -d.normalized(), .6)
        if hit[0]:
            rc = (hit[0] - C).length + cap_off
            if d.length < rc: v.co = C + d.normalized() * rc; pushed += 1
    report['tripo_verts_pushed_out'] = pushed
    me = bpy.data.meshes.new('TripoHair'); bm.to_mesh(me); bm.free()
    th = bpy.data.objects.new('TripoHair', me); context.scene.collection.objects.link(th)
    for o in parts: bpy.data.objects.remove(o)
    tris = sum(len(p.vertices) - 2 for p in me.polygons)
    if tris > target_tris:
        dm = th.modifiers.new('dec', 'DECIMATE'); dm.ratio = target_tris / tris
        with context.temp_override(object=th, active_object=th): bpy.ops.object.modifier_apply(modifier=dm.name)
    for p in th.data.polygons: p.use_smooth = True
    report['tripo_hair_tris'] = sum(len(p.vertices) - 2 for p in th.data.polygons)
    th.data.materials.clear()
    for m in hair_obj.data.materials: th.data.materials.append(m)
    for p in th.data.polygons: p.material_index = 0
    th.data.transform(hair_obj.matrix_world.inverted()); th.matrix_world = hair_obj.matrix_world.copy()
    vg = th.vertex_groups.new(name='Head')
    if drape:
        # long hair / ponytail BELOW THE CHIN follows the chest (Head -> Neck -> Chest), not the head's full turn.
        # Everything around the skull stays 100% Head: the ramp used to start at the head bone (1.30), which on this
        # big chibi head is ear/jaw height, so the lower half of the hair shell was glued to the neck and tore away
        # from the scalp whenever the head turned (reads as floating hair). Chin is at ~1.17.
        vn = th.vertex_groups.new(name='Neck'); vc = th.vertex_groups.new(name='Chest'); mw = th.matrix_world; low = 0
        def sm(a, b, x): t = min(1, max(0, (x - a) / (b - a))); return t * t * (3 - 2 * t)
        for v in th.data.vertices:
            z = (mw @ v.co).z; h = sm(1.06, 1.17, z); c = 1 - sm(0.94, 1.06, z); n = max(0.0, 1 - h - c)
            if h > 0: vg.add([v.index], h, 'REPLACE')
            if n > 0: vn.add([v.index], n, 'REPLACE')
            if c > 0: vc.add([v.index], c, 'REPLACE'); low += 1
        report['drape_chest_verts'] = low
    else:
        vg.add(list(range(len(th.data.vertices))), 1.0, 'REPLACE')
    bpy.ops.object.select_all(action='DESELECT'); th.select_set(True); hair_obj.select_set(True)
    with context.temp_override(active_object=hair_obj, selected_editable_objects=[hair_obj, th], object=hair_obj):
        bpy.ops.object.join()
    report['hair_tris_total'] = sum(len(p.vertices) - 2 for p in hair_obj.data.polygons)
