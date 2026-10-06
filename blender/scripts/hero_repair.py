"""Repairs to Adnan's Hero, made on the meshes as hero_parts_export.py writes them (his sources are untouched).

What the handoff (docs/character-handoff on origin/tennisgameplaydone-needtofixcharacters) and Adnan
himself flagged, and what is done about it here:

  * "The legs are fat": the thighs and calves (body skin), the shorts' legs and the socks are slimmed
    about each leg's own axis, by bone weight, so nothing kinks at the knee. Every shape key (the girl) is
    slimmed the same way, or the blend from one to the other would undo it.
  * The head, the hair and the hats (hero_head.py, hero_hair.py, hero_hats.py, run by hero_parts_export.py):
    his head was a torn face mask under a visor-shaped scalp, and his haircuts and hats scans built round the
    visor. The head is closed (the mask trimmed, a shell with ears behind it) and the haircuts and hats are made
    to sit on it, so none of his hair or hat sculpts is used.
  * The clothes: the Tripo noise on the shirt and shorts is relaxed (a light smooth that keeps the
    silhouette, the hems and the seams), and the shirt's collar, which was left torn at the back of the neck
    (the hair covered it), has its hanging shards peeled and its rim smoothed (`mend_collar`).

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


def mend_collar(obj, low=1.20, passes=8, rounds=8, factor=0.5, behind=0.0):
    """The shirt's collar was left torn at the back of the neck (Adnan's Hero wore hair over it): its rim is a sawtooth
    of triangles hanging by a corner or an edge. They are peeled, over and over, above `low`, and what is left of the
    rim behind the neck (y > `behind`) is eased along itself, so the back of the collar is one clean curve. `relax`
    leaves boundary vertices alone, so nothing else touches it. The shape keys follow. Returns (peeled faces)."""
    mw = obj.matrix_world
    bm = bmesh.new(); bm.from_mesh(obj.data); bm.faces.ensure_lookup_table()
    peeled = 0
    for _ in range(passes):
        bad = [f for f in bm.faces if (mw @ f.calc_center_median()).z > low
               and (sum(1 for e in f.edges if e.is_boundary) >= 2 or any(len(v.link_faces) == 1 for v in f.verts))]
        if not bad: break
        peeled += len(bad)
        bmesh.ops.delete(bm, geom=bad, context='FACES')
        bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
        bm.faces.ensure_lookup_table()
    layers = list(bm.verts.layers.shape.values())
    for _ in range(rounds):
        moves = {}
        for v in bm.verts:
            p = mw @ v.co
            if p.z <= low or p.y <= behind: continue
            nb = [e.other_vert(v) for e in v.link_edges if e.is_boundary]
            if len(nb) != 2: continue
            moves[v] = ((nb[0].co + nb[1].co) / 2 - v.co) * factor
        for v, d in moves.items():
            v.co += d
            for L in layers: v[L] = v[L] + d
    bm.to_mesh(obj.data); bm.free()
    obj.data.update()
    return peeled


def apply(slot, meshes, hero_dir=None):
    """Called by hero_parts_export.py for each slot's meshes, in a scene that has the slot's rig."""
    arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
    for m in meshes:
        if slot == "body" and m.name == "Body_Skin":
            slim_legs(m, arm, *SLIM["body"])
            relax(m, 2, 0.5)
        elif slot == "shirt":
            # (HERO_SKIP_RELAX=1: the source is already a repaired hero_shirt.fbx, not Adnan's original)
            if os.environ.get("HERO_SKIP_RELAX") != "1": relax(m, 2, 0.5)
            print("collar: peeled", mend_collar(m))
        elif slot == "shorts":
            slim_legs(m, arm, SLIM["shorts"], 1.0)
            relax(m, 2, 0.5)
        elif slot == "shoes":
            # only the socks: the calf part of the mesh (the shoes stay as modelled)
            slim_legs(m, arm, 1.0, SLIM["shoes"])
    print(f"repaired {slot}")
