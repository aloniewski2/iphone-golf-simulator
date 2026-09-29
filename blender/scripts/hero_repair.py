"""Repairs to Adnan's Hero, made on the meshes as hero_parts_export.py writes them (his sources are untouched).

What the handoff (docs/character-handoff on origin/tennisgameplaydone-needtofixcharacters) and Adnan
himself flagged, and what is done about it here:

  * "The legs are fat": the thighs and calves (body skin), the shorts' legs and the socks are slimmed
    about each leg's own axis, by bone weight, so nothing kinks at the knee. Every shape key (the girl) is
    slimmed the same way, or the blend from one to the other would undo it.
  * The head, the hair and the hats (hero_head.py, run by hero_parts_export.py): a closed scalp where his
    head stopped at the forehead, every haircut seated on it without the visor ring it was built on, the
    hats fitted over the hair (not cut into it), and the hair's flex (below) so it moves.
  * The clothes: the Tripo noise on the shirt and shorts is relaxed (a light smooth that keeps the
    silhouette, the hems and the seams).

Every step is a function of (object, parameters), and `apply(slot, meshes)` is the only entry point.
"""
import bpy, bmesh, math, os
from mathutils import Vector

# thigh, calf (body skin) / shorts' leg / socks: how much of the girth stays (1 = as modelled)
SLIM = {"body": (0.80, 0.78), "shorts": 0.90, "shoes": 0.82}
LEG_BONES = (("UpperLeg", "L"), ("LowerLeg", "L"), ("UpperLeg", "R"), ("LowerLeg", "R"))


def _bone_axes(arm):
    """Head and tail of the leg bones in armature space, by name."""
    out = {}
    for b in arm.data.bones:
        out[b.name] = (b.head_local.copy(), b.tail_local.copy())
    return out


def _closest_on_segment(p, a, b):
    ab = b - a
    t = max(0.0, min(1.0, (p - a).dot(ab) / max(ab.length_squared, 1e-9)))
    return a + ab * t


def _transform_all_keys(obj, fn):
    """Apply fn(index, co) -> co to the basis and to every shape key of obj."""
    me = obj.data
    for v in me.vertices:
        v.co = fn(v.index, v.co.copy())
    if me.shape_keys:
        for kb in me.shape_keys.key_blocks:
            if kb.name == me.shape_keys.reference_key.name: continue
            for i, d in enumerate(kb.data):
                d.co = fn(i, d.co.copy())
        # the reference key follows the mesh
        for i, d in enumerate(me.shape_keys.reference_key.data):
            d.co = me.vertices[i].co.copy()


def slim_legs(obj, arm, thigh, calf):
    """Pull the vertices in toward each leg's axis by the leg bones' weights."""
    axes = _bone_axes(arm)
    groups = {g.index: g.name for g in obj.vertex_groups}
    me = obj.data
    # per vertex: weights on each leg bone
    weights = []
    for v in me.vertices:
        w = {}
        for g in v.groups:
            n = groups.get(g.group, "")
            base = n.split(".")[0]
            if base in ("UpperLeg", "LowerLeg"): w[n] = g.weight
        weights.append(w)

    def fn(i, co):
        w = weights[i]
        if not w: return co
        total = sum(w.values())
        out = Vector((0, 0, 0)); used = 0.0
        for n, wt in w.items():
            a, b = axes[n]
            k = thigh if n.startswith("UpperLeg") else calf
            c = _closest_on_segment(co, a, b)
            out += (c + (co - c) * k) * wt
            used += wt
        keep = max(0.0, 1.0 - used)
        return out + co * keep

    _transform_all_keys(obj, fn)


def relax(obj, iterations=2, factor=0.5, skip=("Head", "Neck", "Hand", "Index", "Middle", "Ring", "Pinky", "Thumb", "Toes", "Foot"), min_weight=0.2):
    """A light Laplacian smooth of the Tripo noise on the limbs, torso and clothes. Boundary vertices (hems, seams,
    cuffs) and anything weighted to the head, hands, fingers or feet (features, fingers) are left alone. Every shape
    key is smoothed the same way, so the girl's shape is as clean as the boy's."""
    me = obj.data
    n = len(me.vertices)
    groups = {g.index: g.name for g in obj.vertex_groups}
    fixed = set()
    for v in me.vertices:
        for g in v.groups:
            nm = groups.get(g.group, "")
            if g.weight >= min_weight and nm.startswith(skip): fixed.add(v.index); break
    adj = [set() for _ in range(n)]
    count = {}
    for p in me.polygons:
        vs = p.vertices
        for k in range(len(vs)):
            a, b = vs[k], vs[(k + 1) % len(vs)]
            key = (min(a, b), max(a, b)); count[key] = count.get(key, 0) + 1
    for (a, b), c in count.items():
        adj[a].add(b); adj[b].add(a)
        if c == 1: fixed.add(a); fixed.add(b)

    def smooth(coords):
        for _ in range(iterations):
            new = list(coords)
            for i in range(n):
                if i in fixed or not adj[i]: continue
                avg = Vector()
                for j in adj[i]: avg += coords[j]
                new[i] = coords[i].lerp(avg / len(adj[i]), factor)
            coords = new
        return coords

    base = smooth([v.co.copy() for v in me.vertices])
    keys = []
    if me.shape_keys:
        for kb in me.shape_keys.key_blocks:
            if kb.name == me.shape_keys.reference_key.name: continue
            keys.append((kb, smooth([d.co.copy() for d in kb.data])))
    for v, c in zip(me.vertices, base): v.co = c
    if me.shape_keys:
        for i, d in enumerate(me.shape_keys.reference_key.data): d.co = base[i]
        for kb, c in keys:
            for d, co in zip(kb.data, c): d.co = co


def hair_flex(hair_objs, reference_objs, near=0.03, far=0.22):
    """Per-vertex 'flex' for the hair: 0 at the roots on the scalp, 1 at the free ends, written as the R of a vertex
    colour layer (Col). The game's HeroKit shader swings the hair by it, so hair follows the head with a lag: the
    strands' ends move, the roots stay. `reference_objs` are the head (face and skull) the hair grows from."""
    from mathutils.bvhtree import BVHTree
    dg = bpy.context.evaluated_depsgraph_get()
    verts, polys = [], []
    for o in reference_objs:
        base = len(verts)
        verts += [o.matrix_world @ v.co for v in o.data.vertices]
        polys += [tuple(base + i for i in p.vertices) for p in o.data.polygons]
    tree = BVHTree.FromPolygons(verts, polys)
    for o in hair_objs:
        me = o.data
        attr = me.color_attributes.get("Col") or me.color_attributes.new("Col", 'FLOAT_COLOR', 'POINT')
        mw = o.matrix_world
        for v in me.vertices:
            hit = tree.find_nearest(mw @ v.co)
            d = hit[3] if hit and hit[3] is not None else far
            t = max(0.0, min(1.0, (d - near) / (far - near)))
            f = t * t * (3 - 2 * t)
            attr.data[v.index].color = (f, 0.0, 0.0, 1.0)


def apply(slot, meshes, hero_dir=None):
    """Called by hero_parts_export.py for each slot's meshes, in a scene that has the slot's rig."""
    arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
    for m in meshes:
        if slot == "body" and m.name == "Body_Skin":
            slim_legs(m, arm, *SLIM["body"])
            relax(m, 2, 0.5)
        elif slot == "shirt":
            relax(m, 2, 0.5)
        elif slot == "shorts":
            slim_legs(m, arm, SLIM["shorts"], 1.0)
            relax(m, 2, 0.5)
        elif slot == "shoes":
            # only the socks: the calf part of the mesh (the shoes stay as modelled)
            slim_legs(m, arm, 1.0, SLIM["shoes"])
    print(f"repaired {slot}")
