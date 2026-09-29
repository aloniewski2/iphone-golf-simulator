"""The Hero's head, made whole, and the hair and hats layered on it (called from hero_repair.py).

Adnan's Hero was sculpted wearing a visor. His body's head stops at the forehead (z 1.51); the skull
exists only as the bald piece, set ~10 cm back from the face; and every haircut was modelled around the
visor's band. Without the visor the front of the head was a hole, and the hair floated where the band
had been. Here:

  * `build_scalp` makes one closed head: a sphere around the head's centre, each direction pushed out to
    the skull where there is skull and tucked just under the face where there is face, the gap between
    them (the forehead and temples he never modelled) filled smoothly. It carries a hairline mask
    (vertex colour R: 0 skin, 1 hair) so the game can paint it skin at the forehead and in the hair's
    colour under the hair; bald, it is all skin.
  * `seat_hair` lowers each haircut onto that scalp: every direction from the head's centre is moved in by
    the gap between the hair's innermost surface and the scalp (smoothed), so the roots sit on the head.
  * `fit_hat` / `under_hat` layer a hat over any head: the hat grows where the scalp (or bare skull)
    would show through it, and each haircut gets a shape key per hat that tucks the hair in under it,
    so the full hair is always there and the hat sits on top (nothing is cut away).

Everything works in directions from one centre, `CENTER`, which sees the whole cranium (the head is
star-shaped from it). Units are metres, Blender axes (the face looks down -Y, Z up).
"""
import bpy, bmesh, math
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree
from mathutils.kdtree import KDTree

CENTER = Vector((0.0, 0.03, 1.42))
FACE_TUCK = 0.007        # the scalp runs this far under the face (his face is bumpy: any shallower and they cross)
SKULL_BAND = math.radians(22)   # skull this close (in angle) to the face is re-shaped to meet it
FACE_MARGIN = math.radians(10)  # how far the scalp reaches in under the face
HAIRLINE = (math.radians(16), math.radians(28))   # skin below, hair above: angular distance from the face


def _world_bvh(objs, poly_filter=None):
    verts, polys = [], []
    for o in objs:
        base = len(verts)
        mw = o.matrix_world
        verts += [mw @ v.co for v in o.data.vertices]
        for p in o.data.polygons:
            if poly_filter and not poly_filter(o, p): continue
            polys.append(tuple(base + i for i in p.vertices))
    return BVHTree.FromPolygons(verts, polys) if polys else None


def _ray(tree, d, far=1.0):
    """Distance from CENTER along d to the first surface, or None."""
    if tree is None: return None
    hit = tree.ray_cast(CENTER, d, far)
    return hit[3] if hit[0] is not None else None


def _neighbours(bm):
    adj = [[] for _ in bm.verts]
    for e in bm.edges:
        a, b = e.verts[0].index, e.verts[1].index
        adj[a].append(b); adj[b].append(a)
    return adj


def _harmonic(values, fixed, adj, active, iterations=600):
    """Fill the unfixed active entries of `values` smoothly from their fixed neighbours (Jacobi)."""
    v = values.copy()
    free = [i for i in range(len(v)) if active[i] and not fixed[i]]
    nb = {i: [j for j in adj[i] if active[j]] for i in free}
    for _ in range(iterations):
        new = v.copy()
        for i in free:
            if nb[i]: new[i] = sum(v[j] for j in nb[i]) / len(nb[i])
        v = new
    return v


def _angular_distance(dirs, mask):
    """For each direction, the angle to the nearest direction where mask is set."""
    kd = KDTree(int(mask.sum()))
    k = 0
    for i, d in enumerate(dirs):
        if mask[i]: kd.insert(d, k); k += 1
    kd.balance()
    out = np.zeros(len(dirs))
    for i, d in enumerate(dirs):
        if mask[i]: continue
        _, _, chord = kd.find(d)
        out[i] = 2 * math.asin(min(1.0, chord / 2))
    return out


def _face_filter(face):
    skin_slots = {i for i, s in enumerate(face.material_slots) if s.material and "WarmSkin" in s.material.name}
    return lambda o, p: o is not face or (p.material_index in skin_slots and (o.matrix_world @ p.center).z > 1.25)


def build_scalp(face, skull, arm, eyes=(), name="Head_Scalp", segments=64, rings=44):
    """One closed skull under the hair: see the module's note. `face` is the body (only its skin faces above
    the jaw count) and `eyes` its eyeballs (they count as face: nothing may show round them), `skull` Adnan's
    bald piece. Returns the new object, bound to `arm`'s Head bone."""
    face_tree = _world_bvh([face, *eyes], _face_filter(face))
    skull_tree = _world_bvh([skull])

    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=segments, v_segments=rings, radius=1.0)
    bm.verts.ensure_lookup_table(); bm.verts.index_update()
    dirs = [v.co.normalized() for v in bm.verts]
    n = len(dirs)
    rf = np.array([_ray(face_tree, d) or 0.0 for d in dirs])
    rk = np.array([_ray(skull_tree, d) or 0.0 for d in dirs])
    has_f, has_k = rf > 0, rk > 0
    to_face = _angular_distance(dirs, has_f)

    r = np.zeros(n); fixed = np.zeros(n, bool)
    # under the face the scalp runs FACE_TUCK below it (blend_face_edge then brings the face's edge down onto it)
    r[has_f] = rf[has_f] - FACE_TUCK; fixed[has_f] = True
    far_skull = has_k & ~has_f & (to_face > SKULL_BAND)
    r[far_skull] = rk[far_skull]; fixed[far_skull] = True
    # the cranium: skull, face, and the band between them; nothing under the jaw
    active = has_f | has_k | np.array([d.z > -0.25 and to_face[i] < math.radians(40) for i, d in enumerate(dirs)])
    start = np.where(fixed, r, np.mean(rk[far_skull]))
    r = _harmonic(start, fixed, _neighbours(bm), active)

    # keep: the cranium (everything not face that joins the crown; not the gaps round the eyes or ears) and the
    # face only near the cranium's edge, where the scalp reaches in under it
    adj = _neighbours(bm)
    top = max(range(n), key=lambda i: dirs[i].z)
    cranium = np.zeros(n, bool); cranium[top] = True; stack = [top]
    while stack:
        i = stack.pop()
        for j in adj[i]:
            if not cranium[j] and not has_f[j] and active[j]: cranium[j] = True; stack.append(j)
    keep = cranium | (active & (_angular_distance(dirs, cranium) < FACE_MARGIN))
    for i, v in enumerate(bm.verts): v.co = CENTER + dirs[i] * float(r[i])
    bmesh.ops.delete(bm, geom=[f for f in bm.faces if not all(keep[v.index] for v in f.verts)], context='FACES')
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')

    # hairline: skin at the forehead and temples, hair colour under the hair
    col = bm.verts.layers.float_color.new("Col")
    lo, hi = HAIRLINE
    kd = KDTree(n)
    for i, d in enumerate(dirs): kd.insert(d, i)
    kd.balance()
    for v in bm.verts:
        _, i, _ = kd.find((v.co - CENTER).normalized())
        t = 0.0 if has_f[i] else min(1.0, max(0.0, (to_face[i] - lo) / (hi - lo)))
        m = t * t * (3 - 2 * t)
        v[col] = (m, 0.0, 0.0, 1.0)

    me = bpy.data.meshes.new(name)
    bm.to_mesh(me); bm.free()
    for p in me.polygons: p.use_smooth = True
    obj = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(obj)
    _bind_to_head(obj, arm)
    obj.data.materials.append(bpy.data.materials.get("Hero_01_Scalp") or bpy.data.materials.new("Hero_01_Scalp"))
    return obj


def trim_face(face, eyes=(), margin=math.radians(4), segments=128, rings=88):
    """Trim the ragged top of the body's head: it was cut for the visor, and its last centimetre or two curls
    out in a torn lip (a ridge on a bald forehead, and through the rim of any hat). It goes back to one smooth
    line `margin` below the lowest point of the tear in each direction round the head; the scalp takes over
    from there, flush. Only the edge toward the cranium is touched (not round the eyes, ears or mouth, and
    never under the jaw). Returns how many faces went."""
    tree = _world_bvh([face, *eyes], _face_filter(face))
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=segments, v_segments=rings, radius=1.0)
    bm.verts.ensure_lookup_table(); bm.verts.index_update()
    dirs = [v.co.normalized() for v in bm.verts]
    adj = _neighbours(bm); bm.free()
    n = len(dirs)
    has_f = np.array([_ray(tree, d) is not None for d in dirs])
    # the cranium: up from the brow, and down the back to the nape (never round under the jaw)
    up = np.array([d.z > -0.2 or d.y > 0.35 for d in dirs])
    top = max(range(n), key=lambda i: dirs[i].z)
    cranium = np.zeros(n, bool); cranium[top] = True; stack = [top]
    while stack:
        i = stack.pop()
        for j in adj[i]:
            if not cranium[j] and not has_f[j] and up[j]: cranium[j] = True; stack.append(j)
    # the edge: in each slice of azimuth, the lowest the cranium reaches (the bottom of the tear), smoothed
    bins = 96
    azim = lambda d: (math.atan2(d.x, -d.y) + math.pi) / (2 * math.pi) * bins
    elev = lambda d: math.asin(max(-1.0, min(1.0, d.z)))
    low = np.full(bins, math.pi / 2)
    for i in range(n):
        if cranium[i]:
            b = int(azim(dirs[i])) % bins
            low[b] = min(low[b], elev(dirs[i]))
    low = np.array([np.median([low[(b + k) % bins] for k in range(-3, 4)]) for b in range(bins)])
    for _ in range(3):
        low = np.array([np.mean([low[(b + k) % bins] for k in range(-2, 3)]) for b in range(bins)])

    def above(p):
        d = (p - CENTER).normalized()
        a = azim(d); b0 = int(math.floor(a)) % bins; t = a - math.floor(a)
        edge = low[b0] * (1 - t) + low[(b0 + 1) % bins] * t
        return elev(d) > edge - margin

    head_slots = {i for i, s in enumerate(face.material_slots) if s.material and "WarmSkin" in s.material.name}
    mw = face.matrix_world
    fbm = bmesh.new(); fbm.from_mesh(face.data); fbm.faces.ensure_lookup_table()
    gone = []
    for f in fbm.faces:
        if f.material_index not in head_slots: continue
        c = mw @ f.calc_center_median()
        if c.z < 1.30 or (c - CENTER).length > 0.3: continue
        if any(above(mw @ v.co) for v in f.verts): gone.append(f)
    bmesh.ops.delete(fbm, geom=gone, context='FACES')
    bmesh.ops.delete(fbm, geom=[v for v in fbm.verts if not v.link_faces], context='VERTS')
    fbm.to_mesh(face.data); fbm.free()
    # where the face now ends (for blend_face_edge): its boundary near that line, not round the eyes or mouth
    wide = margin + math.radians(4)
    def near_edge(p):
        d = (p - CENTER).normalized()
        a = azim(d); b0 = int(math.floor(a)) % bins; t = a - math.floor(a)
        edge = low[b0] * (1 - t) + low[(b0 + 1) % bins] * t
        return elev(d) > edge - wide
    return len(gone), near_edge


def blend_face_edge(face, scalp, near_edge, reach=0.02, above=0.0005):
    """Ease the last `reach` of the face down onto the scalp (which runs FACE_TUCK under it), so the forehead
    runs smoothly into the scalp: no step at the face's edge, and no sawtooth where a bumpy face and a smooth
    scalp would cross."""
    me = face.data; mw = face.matrix_world; inv = mw.inverted()
    bm = bmesh.new(); bm.from_mesh(me); bm.verts.ensure_lookup_table()
    seeds = [v.index for v in bm.verts if any(e.is_boundary for e in v.link_edges)
             and (mw @ v.co).z > 1.36 and near_edge(mw @ v.co)]
    bm.free()
    adj = _mesh_adjacency(face)
    dist = _hop_distance(face, seeds, adj)
    tree = _world_bvh([scalp])
    moved = 0
    for v in me.vertices:
        g = dist[v.index]
        if g >= reach: continue
        p = mw @ v.co; d = (p - CENTER); r = d.length; d = d / r
        rs = _ray(tree, d)
        if rs is None or rs + above >= r: continue
        t = 1 - g / reach; w = t * t * (3 - 2 * t)
        v.co = inv @ (CENTER + d * (r + (rs + above - r) * w))
        moved += 1
    if me.shape_keys:   # the girl's shape carries the same edge
        for kb in me.shape_keys.key_blocks:
            for v in me.vertices:
                if dist[v.index] < reach: kb.data[v.index].co = v.co.copy()
    return moved


def _bind_to_head(obj, arm):
    obj.parent = arm
    obj.matrix_parent_inverse = arm.matrix_world.inverted()
    g = obj.vertex_groups.new(name="Head")
    g.add(list(range(len(obj.data.vertices))), 1.0, 'REPLACE')
    mod = obj.modifiers.new("Armature", 'ARMATURE'); mod.object = arm


class Radial:
    """A surface seen from CENTER: its radius in any direction (first hit), for the scalp and the hats."""
    def __init__(self, objs, poly_filter=None):
        self.tree = _world_bvh(objs, poly_filter)

    def __call__(self, d, far=1.0):
        return _ray(self.tree, d, far)


class Sphere:
    """Directions around CENTER (a UV sphere's vertices) with their neighbours, and a lookup from any direction."""
    def __init__(self, segments=72, rings=48):
        bm = bmesh.new()
        bmesh.ops.create_uvsphere(bm, u_segments=segments, v_segments=rings, radius=1.0)
        bm.verts.ensure_lookup_table(); bm.verts.index_update()
        self.dirs = [v.co.normalized() for v in bm.verts]
        self.adj = _neighbours(bm)
        bm.free()
        self.kd = KDTree(len(self.dirs))
        for i, d in enumerate(self.dirs): self.kd.insert(d, i)
        self.kd.balance()

    def sample(self, field, d, k=6):
        """field (one value per direction) at direction d, inverse-distance weighted."""
        num = den = 0.0
        for _, i, dist in self.kd.find_n(d, k):
            w = 1.0 / max(dist, 1e-4)
            num += field[i] * w; den += w
        return num / den


def _median_smooth(values, valid, adj):
    out = values.copy()
    for i in range(len(values)):
        if not valid[i]: continue
        ring = [values[j] for j in adj[i] if valid[j]] + [values[i]]
        out[i] = float(np.median(ring))
    return out


def head_surface(face, scalp, eyes=()):
    """The whole head as seen from CENTER: the scalp, the face's skin and the eyes."""
    return Radial([face, scalp, *eyes], _face_filter(face))


def face_weight(sphere, face, eyes=()):
    """1 over the cranium, fading to 0 across the face (hair beside the cheeks is left where it hangs)."""
    tree = _world_bvh([face, *eyes], _face_filter(face))
    has_f = np.array([_ray(tree, d) is not None for d in sphere.dirs])
    into = _angular_distance(sphere.dirs, ~has_f)
    t = np.clip(into / math.radians(14), 0, 1)
    return 1 - t * t * (3 - 2 * t)


def seat_hair(hair, head, sphere, weight, max_shift=0.08, iterations=300):
    """Lower `hair` onto the head: in each direction the hair moves in by the gap between its innermost surface
    and the head (smoothed over neighbouring directions, weighted by `weight`), so its roots sit on the scalp and
    its thickness is kept. Returns the per-direction shift (metres) for the report."""
    hair_r = Radial([hair])
    n = len(sphere.dirs)
    gap = np.zeros(n); valid = np.zeros(n, bool)
    for i, d in enumerate(sphere.dirs):
        rh, rs = hair_r(d), head(d)
        if rh is not None and rs is not None:
            gap[i] = rh - rs; valid[i] = True
    gap = np.clip(_median_smooth(_median_smooth(gap, valid, sphere.adj), valid, sphere.adj), 0.0, max_shift)
    # where the hair leaves the head bare (the old band) the shift carries on smoothly from either side
    active = np.array([head(d) is not None for d in sphere.dirs])
    field = _harmonic(np.where(valid, gap, 0.0), valid & (weight > 0.99), sphere.adj, active, iterations)
    field = np.where(active, field, 0.0) * weight
    mw = hair.matrix_world; inv = mw.inverted()
    for v in hair.data.vertices:
        p = mw @ v.co
        d = (p - CENTER); r = d.length; d = d / r
        s = sphere.sample(field, d)
        v.co = inv @ (CENTER + d * max(0.01, r - s))
    return field


def islands(obj):
    """Vertex index lists of obj's connected pieces, largest first."""
    bm = bmesh.new(); bm.from_mesh(obj.data); bm.verts.ensure_lookup_table()
    seen = set(); out = []
    for v in bm.verts:
        if v.index in seen: continue
        comp = [v.index]; seen.add(v.index); stack = [v]
        while stack:
            x = stack.pop()
            for e in x.link_edges:
                y = e.other_vert(x)
                if y.index not in seen: seen.add(y.index); comp.append(y.index); stack.append(y)
        out.append(comp)
    bm.free()
    return sorted(out, key=len, reverse=True)


def remove_band(hair):
    """Drop the base each haircut was built on: one piece (913 vertices in every cut) wrapped round the head from
    the nape to the visor's band, pressed flat where the band sat and flared out from the forehead like a brim in
    the hat-less cuts. The scalp is under the hair now; the clumps are the haircut."""
    pieces = islands(hair)
    mw = hair.matrix_world
    ring = pieces[0]
    pts = [mw @ hair.data.vertices[i].co for i in ring]
    span = lambda k: max(p[k] for p in pts) - min(p[k] for p in pts)
    wraps = span(0) > 0.3 and span(1) > 0.3 and min(p.z for p in pts) > 1.25 and max(p.z for p in pts) < 1.7
    if not wraps:
        return 0
    bm = bmesh.new(); bm.from_mesh(hair.data); bm.verts.ensure_lookup_table()
    bmesh.ops.delete(bm, geom=[bm.verts[i] for i in ring], context='VERTS')
    bm.to_mesh(hair.data); bm.free()
    return len(ring)


def uv_from(target, source):
    """UVs for `target` from the nearest face of `source` (the buzz cut's shell, for the scalp's short-hair map):
    each of target's faces takes the affine map of the one source triangle nearest its centre, so a source seam
    stays a clean seam instead of a smear."""
    from mathutils.geometry import barycentric_transform
    bm = bmesh.new(); bm.from_mesh(source.data)
    bmesh.ops.transform(bm, matrix=source.matrix_world, verts=bm.verts)
    bmesh.ops.triangulate(bm, faces=bm.faces)
    bm.faces.ensure_lookup_table()
    uvl = bm.loops.layers.uv.active
    tree = BVHTree.FromBMesh(bm)
    me = target.data
    uv = me.uv_layers.get("UVMap") or me.uv_layers.new(name="UVMap")
    mw = target.matrix_world
    for p in me.polygons:
        _, _, fi, _ = tree.find_nearest(mw @ p.center)
        f = bm.faces[fi]
        tri = [l.vert.co for l in f.loops]; tuv = [l[uvl].uv.to_3d() for l in f.loops]
        for li in p.loop_indices:
            co = mw @ me.vertices[me.loops[li].vertex_index].co
            u = barycentric_transform(co, *tri, *tuv)
            uv.data[li].uv = (u.x, u.y)
    bm.free()


def _mesh_adjacency(obj):
    adj = [[] for _ in obj.data.vertices]
    for e in obj.data.edges:
        a, b = e.vertices
        adj[a].append(b); adj[b].append(a)
    return adj


def _hop_distance(obj, seeds, adj):
    """Edge-length distance over the mesh from the seed vertices (Dijkstra)."""
    import heapq
    co = [v.co for v in obj.data.vertices]
    dist = [math.inf] * len(co)
    heap = []
    for s in seeds: dist[s] = 0.0; heap.append((0.0, s))
    heapq.heapify(heap)
    while heap:
        d, i = heapq.heappop(heap)
        if d > dist[i]: continue
        for j in adj[i]:
            nd = d + (co[i] - co[j]).length
            if nd < dist[j]: dist[j] = nd; heapq.heappush(heap, (nd, j))
    return dist


def fit_hat(hat, head, lift=0.006, snug=0.05):
    """Where `hat` must sit so it rests on the head, `lift` over it: returns each vertex's new world position.
    First the whole hat is scaled (its width on its own, its depth and height together, so a brim keeps its
    angle) and moved so its lower edge best meets the head all round; then, wherever it would still sink in,
    it is eased out there alone. `head` is a Radial of what it sits on: the scalp (and face), or the hair's
    pressed layer."""
    me = hat.data; mw = hat.matrix_world
    adj = _mesh_adjacency(hat)
    n = len(me.vertices)
    pts = [mw @ v.co for v in me.vertices]
    polys = [tuple(p.vertices) for p in me.polygons]

    def bumps(d):
        """The most the head reaches within a few degrees of d (a hat must clear its bumps, not its average)."""
        a = d.orthogonal().normalized(); b = d.cross(a)
        best = head(d)
        for k in range(6):
            t = k * math.pi / 3
            e = (d + (a * math.cos(t) + b * math.sin(t)) * 0.06).normalized()
            r = head(e)
            if r is not None and (best is None or r > best): best = r
        return best

    def classify(positions):
        tree = BVHTree.FromPolygons(positions, polys)
        need = np.zeros(n); known = np.zeros(n, bool)
        for i, p in enumerate(positions):
            d = (p - CENTER).normalized()
            hit = tree.ray_cast(CENTER, d, 1.0)
            rs = bumps(d)
            if hit[0] is None or rs is None: continue
            # on the head where the hat's surface wraps round it (faces the centre); a brim or a bill lies across
            if abs(hit[1].dot(d)) < 0.5: continue
            need[i] = rs + lift - hit[3]; known[i] = True
        return need, known

    need, known = classify(pts)
    # the hat's lower edge round the head: the lowest point on the head in each slice of azimuth
    az = lambda p: math.atan2(p.x - CENTER.x, CENTER.y - p.y)
    bins = 24
    bottom = [math.inf] * bins
    for i in range(n):
        if known[i]:
            b = int((az(pts[i]) + math.pi) / (2 * math.pi) * bins) % bins
            bottom[b] = min(bottom[b], pts[i].z)
    edge = []
    for i in range(n):
        if not known[i]: continue
        b = int((az(pts[i]) + math.pi) / (2 * math.pi) * bins) % bins
        low = min(bottom[b], bottom[(b - 1) % bins], bottom[(b + 1) % bins])
        if pts[i].z - low < snug: edge.append(i)
    # 1. scale and move the whole hat so its edge meets the head
    P = np.array([tuple(pts[i]) for i in edge])
    Q = np.array([tuple(pts[i] + (pts[i] - CENTER).normalized() * need[i]) for i in edge])
    pc, qc = P.mean(0), Q.mean(0)
    dp, dq = P - pc, Q - qc
    sx = float((dp[:, 0] * dq[:, 0]).sum() / max((dp[:, 0] ** 2).sum(), 1e-9))
    syz = float(((dp[:, 1:] * dq[:, 1:]).sum()) / max((dp[:, 1:] ** 2).sum(), 1e-9))
    sx, syz = min(1.3, max(0.7, sx)), min(1.3, max(0.7, syz))
    moved = [Vector((sx * (p.x - pc[0]) + qc[0], syz * (p.y - pc[1]) + qc[1], syz * (p.z - pc[2]) + qc[2])) for p in pts]
    # 2. wherever it still sinks into the head, ease it out (a brim moves with its band, as one piece)
    need2, known2 = classify(moved)
    need2 = np.where(known2, np.maximum(need2, 0.0), 0.0)
    # a smooth push that clears every bump (the least smooth envelope over what each vertex needs): pushing
    # each vertex by its own need left a thin band's edge in a zigzag
    push = need2.copy()
    for _ in range(12):
        push = np.array([max(need2[i], (push[i] + sum(push[j] for j in adj[i])) / (1 + len(adj[i]))) if known2[i] and adj[i] else push[i] for i in range(n)])
    off = [(moved[i] - CENTER).normalized() * push[i] for i in range(n)]
    seen = known2.copy()
    kd = KDTree(max(1, int(known2.sum())))
    for i in range(n):
        if known2[i]: kd.insert(moved[i], i)
    kd.balance()
    for i in range(n):
        if seen[i]: continue
        piece = [i]; seen[i] = True; stack = [i]
        while stack:
            a = stack.pop()
            for b in adj[a]:
                if not seen[b]: seen[b] = True; piece.append(b); stack.append(b)
        # the piece's nearest point on the head part carries it
        best = min(piece, key=lambda j: kd.find(moved[j])[2] if known2.any() else 0)
        _, k, _ = kd.find(moved[best]) if known2.any() else (None, None, None)
        o = off[k] if k is not None else Vector()
        for j in piece: off[j] = o
    return [moved[i] + off[i] for i in range(n)]


def double_side(obj):
    """Open shells (a cap's brim and crown, a visor's bill) get a back face, so they show from below as well as
    from above (the game culls back faces). Closed ones (a sweatband) are left as they are."""
    bm = bmesh.new(); bm.from_mesh(obj.data)
    if not any(e.is_boundary for e in bm.edges):
        bm.free(); return False
    dup = bmesh.ops.duplicate(bm, geom=list(bm.faces))
    faces = [g for g in dup["geom"] if isinstance(g, bmesh.types.BMFace)]
    bmesh.ops.reverse_faces(bm, faces=faces)
    bm.to_mesh(obj.data); bm.free()
    return True


def under_hat(hair, hat_inner, head, margin=0.006, floor=0.004, taper=0.025):
    """The hair tucked in under a hat: every vertex beyond the hat's inner surface (seen from CENTER) comes in to
    just inside it (never into the head), and its neighbours along the strand ease in after it over `taper`,
    so the hair runs in under the hat's edge instead of breaking there. Returns (new world positions, how much
    each vertex is held by the hat 0..1: the game keeps held hair from swaying through the hat)."""
    me = hair.data; mw = hair.matrix_world
    n = len(me.vertices)
    pts = [mw @ v.co for v in me.vertices]
    push = np.zeros(n)
    for i, p in enumerate(pts):
        d = (p - CENTER); r = d.length; d = d / r
        rh = hat_inner(d)
        if rh is None: continue
        rs = head(d)
        target = rh - margin
        if rs is not None: target = max(target, rs + floor)
        if r > target: push[i] = r - target
    held = push > 0
    adj = _mesh_adjacency(hair)
    # ease: each un-held vertex takes a falling share of its held neighbours' push
    ease = push.copy()
    co = pts
    for _ in range(6):
        new = ease.copy()
        for i in range(n):
            if held[i]: continue
            best = 0.0
            for j in adj[i]:
                fall = max(0.0, 1.0 - (co[i] - co[j]).length / taper)
                best = max(best, ease[j] * fall)
            new[i] = max(new[i], best)
        ease = new
    out = []
    for i, p in enumerate(pts):
        d = (p - CENTER); r = d.length; d = d / r
        rr = r - ease[i]
        if ease[i] > 0:
            rs = head(d)
            if rs is not None: rr = max(rr, min(r, rs + floor))
        out.append(CENTER + d * rr)
    hold = np.clip(ease / 0.01, 0, 1)
    return out, hold


HAT_LAYER = 0.02       # the hair pressed under a hat: this thick (under a cap; at least this under a band)
BAND_RIDE = 0.65       # a band rides on this share of the hair's thickness under it
BAND_MAX = 0.09        # but no more than this far off the scalp
HATS = ("Visor", "Cap", "Sweatband")   # the order of their hold channels in the hair's colour: G, B, A


def _tree(obj, world_positions):
    polys = [tuple(p.vertices) for p in obj.data.polygons]
    return BVHTree.FromPolygons(list(world_positions), polys)


def _set_key(obj, name, world_positions):
    if not obj.data.shape_keys: obj.shape_key_add(name="Basis", from_mix=False)
    key = obj.shape_key_add(name=name, from_mix=False)
    inv = obj.matrix_world.inverted()
    for d, p in zip(key.data, world_positions): d.co = inv @ p


def build_head(arm, face, eyes, skull, buzz, cuts, hats, flex=None):
    """The whole head, from Adnan's pieces: the scalp (UVs from `buzz`), every haircut (`cuts`, name -> object)
    with its visor ring dropped and seated on the scalp, every hat (`hats`, "Visor"/"Cap"/"Sweatband" -> object)
    fitted to the bare head with a shape key "OnHair_<cut>" for over each haircut, and on every haircut a shape key
    "Under_<hat>" that tucks it in under that hat plus how firmly each vertex is held there (colour G/B/A in
    HATS order; R is the hair's flex, from `flex(hair_objs, reference_objs)`). Returns the scalp."""
    trimmed, near_edge = trim_face(face, eyes)
    scalp = build_scalp(face, skull, arm, eyes)
    blended = blend_face_edge(face, scalp, near_edge)
    uv_from(scalp, buzz)
    sphere = Sphere()
    head = head_surface(face, scalp, eyes)
    weight = face_weight(sphere, face, eyes)
    report = {"face": {"trimmed": trimmed, "edge_blended": blended}}
    for name, hair in cuts.items():
        dropped = remove_band(hair)
        shift = seat_hair(hair, head, sphere, weight)
        report[name] = {"ring": dropped, "seated_cm": round(float(shift.max()) * 100, 1)}
    if flex: flex(list(cuts.values()), [face, scalp])
    # how thick each cut is round the head (its outer surface over the scalp), smoothed over directions
    thick = {}
    for name, hair in cuts.items():
        tree = _world_bvh([hair])
        field = np.zeros(len(sphere.dirs)); valid = np.zeros(len(sphere.dirs), bool)
        for i, d in enumerate(sphere.dirs):
            hit = tree.ray_cast(CENTER + d * 0.6, -d, 0.6)
            rs = head(d)
            if hit[0] is not None and rs is not None:
                field[i] = max(0.0, 0.6 - hit[3] - rs); valid[i] = True
        field = _median_smooth(_median_smooth(field, valid, sphere.adj), valid, sphere.adj)
        thick[name] = (field, valid)

    def layer(hname, name):
        """What a hat sits on over a cut: a band (visor, sweatband) rides on most of the hair, pressing in only
        its outer part, as a headband does on big hair; a cap presses the hair flat under its crown."""
        if hname == "Cap":
            return lambda d: (lambda r: None if r is None else r + HAT_LAYER)(head(d))
        field, valid = thick[name]
        def on(d):
            r = head(d)
            if r is None: return None
            t = sphere.sample(np.where(valid, field, 0.0), d)
            return r + min(BAND_MAX, max(HAT_LAYER, BAND_RIDE * t))
        return on

    trees = {}
    for hname, hat in hats.items():
        bare = fit_hat(hat, head, lift=0.009)
        inv = hat.matrix_world.inverted()
        overs = {name: fit_hat(hat, layer(hname, name)) for name in cuts}
        for v, p in zip(hat.data.vertices, bare): v.co = inv @ p
        for name, over in overs.items():
            tree = _tree(hat, over)
            trees[(hname, name)] = lambda d, t=tree: (lambda h: h[3] if h[0] is not None else None)(t.ray_cast(CENTER, d, 1.0))
            _set_key(hat, "OnHair_" + name, over)
        double_side(hat)
    for name, hair in cuts.items():
        col = hair.data.color_attributes.get("Col") or hair.data.color_attributes.new("Col", 'FLOAT_COLOR', 'POINT')
        holds = {}
        for hname in HATS:
            if (hname, name) not in trees: continue
            pos, hold = under_hat(hair, trees[(hname, name)], head)
            _set_key(hair, "Under_" + hname, pos)
            holds[hname] = hold
        for v in hair.data.vertices:
            c = list(col.data[v.index].color)
            for k, hname in enumerate(HATS):
                c[1 + k] = float(holds[hname][v.index]) if hname in holds else 0.0
            col.data[v.index].color = c
        report[name]["held"] = {h: int((holds[h] > 0.5).sum()) for h in holds}
    return scalp, report
