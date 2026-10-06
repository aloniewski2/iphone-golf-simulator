"""MakeHuman's CC0 hairs, fitted to Adnan's match heroes.

MakeHuman (makehumancommunity.org) ships hair as alpha-textured meshes with real strand detail, all CC0 (no credit owed; we give it anyway
in docs/matchhero-golfer.md). The ones used are in its "system assets" pack (hair/<name>/<name>.obj, <name>_diffuse.png);
blender/scripts/fetch_mhhair.py fetches just those files (35 MB of the pack's 267) into blender/mhhair/.

They are made for MakeHuman's own head, so each is fitted to the head it is put on:
  1. imported (OBJ: decimetres, Y up) and turned to this rig (metres, Z up, face -Y);
  2. scaled and moved (uniform scale, front/back, up/down) until the hair's inner surface lies the style's own distance off the scalp over the
     whole cranium: found by a search on rays out of the scalp (a gap that is too small or missing, a bald spot, costs most);
  3. pushed clear of the head, the ears and the shirt (nothing inside a collider), the push smoothed over its neighbours so it never spikes.
The texture is made grey and normalised (fetch_mhhair.py) so the game tints it any hair colour and keeps the strands.
"""
import bpy, bmesh, math, os, random, heapq
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree
from mathutils.kdtree import KDTree

from mhhair_sources import HAIRS


def _import(path):
    before = set(bpy.data.objects)
    bpy.ops.wm.obj_import(filepath=path, forward_axis='NEGATIVE_Z', up_axis='Y', global_scale=0.1)
    new = [o for o in bpy.data.objects if o not in before]
    o = next(o for o in new if o.type == 'MESH')
    for x in new:
        if x is not o: bpy.data.objects.remove(x, do_unlink=True)
    with bpy.context.temp_override(selected_editable_objects=[o], object=o, active_object=o):
        bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    return o


def colliders_bvh(objs):
    """One BVH of the objects' meshes in world space (the rig in its rest pose)."""
    verts, polys = [], []
    for o in objs:
        base = len(verts)
        verts += [o.matrix_world @ v.co for v in o.data.vertices]
        polys += [tuple(base + i for i in p.vertices) for p in o.data.polygons]
    return BVHTree.FromPolygons(verts, polys)


def _signed(tree, p):
    loc, n, idx, d = tree.find_nearest(p)
    if loc is None: return 1.0
    return d if (p - loc).dot(n) >= 0 else -d


class Fit:
    def __init__(self, sk, colliders):
        self.sk = sk
        self.cols = colliders
        # scalp sample points: the skull above the ears, with their normals (a ray out of each should meet the hair)
        rnd = random.Random(5)
        self.scalp = []
        for _ in range(900):
            az = rnd.uniform(-180, 180); el = rnd.uniform(18, 88)
            p, n = sk.hit(az, el, 0.0)
            if p is not None: self.scalp.append((p, n, az, el))
        self.scalp = self.scalp[:320]

    def tree_of(self, obj, M):
        verts = [M @ v.co for v in obj.data.vertices]
        return BVHTree.FromPolygons(verts, [tuple(p.vertices) for p in obj.data.polygons])

    def cost(self, obj, M, gap):
        tree = self.tree_of(obj, M)
        c = 0.0
        for p, n, az, el in self.scalp:
            o = p + n * 0.0005
            h = tree.ray_cast(o, n, 0.12)
            if h[0] is None:
                # nothing out of this part of the scalp: inside the hair, or none there. Count it unless the hair's own hairline leaves it bare
                c += 0.5 if el > 40 else 0.1
                continue
            g = h[3]
            c += (max(0.0, gap * 0.55 - g) * 40.0) ** 2 + (max(0.0, g - gap * 1.8) * 14.0) ** 2
        return c / max(1, len(self.scalp))

    def _frame(self, o):
        vs = [v.co for v in o.data.vertices]
        ys = [v.y for v in vs]
        return max(v.z for v in vs), (min(ys) + max(ys)) / 2

    def place(self, o, k, gap, steps=6):
        """The best front/back and up/down placement (ty, tz) of hair o at scale k: (cost, ty, tz)."""
        sk = self.sk
        top, cy = self._frame(o)
        ty, tz = sk.C.y - cy * k, sk.top + gap - top * k
        def M_of(ty, tz): return Matrix.Translation(Vector((0.0, ty, tz))) @ Matrix.Scale(k, 4)
        c_best = self.cost(o, M_of(ty, tz), gap)
        sy = sz = 0.012
        for _ in range(steps):
            improved = True
            while improved:
                improved = False
                for dy_, dz_ in ((sy, 0), (-sy, 0), (0, sz), (0, -sz)):
                    c = self.cost(o, M_of(ty + dy_, tz + dz_), gap)
                    if c < c_best - 1e-9: c_best, ty, tz = c, ty + dy_, tz + dz_; improved = True
            sy *= 0.5; sz *= 0.5
        return c_best, ty, tz

    def fit_scale(self, objs, gaps):
        """The one scale k all the hairs share (they were made on one head), found over the close cuts, each placed best at every k."""
        best = None
        k = 0.9
        while k < 1.6:
            c = sum(self.place(o, k, g, steps=3)[0] for o, g in zip(objs, gaps)) / len(objs)
            if best is None or c < best[0]: best = (c, k)
            k += 0.05
        c0, k0 = best
        for dk in (0.025, 0.0125):
            for kk in (k0 - dk, k0 + dk):
                c = sum(self.place(o, kk, g, steps=3)[0] for o, g in zip(objs, gaps)) / len(objs)
                if c < c0: c0, k0 = c, kk
        return k0, c0

    def push_out(self, obj, margin=0.0035, passes=3):
        """Nothing of the hair inside the head, the ears or the shirt: pushed out to `margin`, the push smoothed over its neighbours."""
        me = obj.data
        n = len(me.vertices)
        disp = [Vector() for _ in range(n)]
        z0 = self.sk.top - 0.17                 # about the chin: below it the hair lies on the shoulders and back, which move under it when he stands (it rides the head)
        def margin_at(z):
            s = max(0.0, min(1.0, (z0 - z) / 0.15))
            return margin + 0.03 * s * s * (3 - 2 * s)
        for v in me.vertices:
            loc, nm, idx, d = self.cols.find_nearest(v.co)
            if loc is None: continue
            m = margin_at(v.co.z)
            inside = (v.co - loc).dot(nm) < 0
            if inside or d < m:
                target = loc + nm * m
                disp[v.index] = target - v.co
        adj = [[] for _ in range(n)]
        for e in me.edges:
            a, b = e.vertices; adj[a].append(b); adj[b].append(a)
        for _ in range(passes):
            new = []
            for i in range(n):
                if adj[i]:
                    avg = sum((disp[j] for j in adj[i]), Vector()) / len(adj[i])
                    new.append(disp[i] * 0.55 + avg * 0.45)
                else:
                    new.append(disp[i])
            # a real push keeps most of its own size
            for i in range(n):
                if disp[i].length > new[i].length: new[i] = disp[i] * 0.85 + new[i] * 0.15
            disp = new
        moved = 0
        for v in me.vertices:
            if disp[v.index].length > 1e-6:
                v.co += disp[v.index]; moved += 1
        # and once more: whatever is still inside goes straight out
        for v in me.vertices:
            loc, nm, idx, d = self.cols.find_nearest(v.co)
            m = margin_at(v.co.z)
            if loc is not None and ((v.co - loc).dot(nm) < 0 or d < m * 0.6):
                v.co = loc + nm * m * 0.8
        return moved


def scalp_material():
    """Hair_Scalp: skin where the vertex colour's R is 0, the hair colour where it is 1 (the game's HeroKit shader does the same with _UseScalp). A preview here."""
    m = bpy.data.materials.get("Hair_Scalp")
    if m: return m
    m = bpy.data.materials.new("Hair_Scalp")
    m.use_nodes = True
    nt = m.node_tree
    for nd in list(nt.nodes):
        if nd.type != 'OUTPUT_MATERIAL': nt.nodes.remove(nd)
    out = nt.nodes["Material Output"]
    b = nt.nodes.new("ShaderNodeBsdfPrincipled")
    attr = nt.nodes.new("ShaderNodeVertexColor"); attr.layer_name = "Col"
    sep = nt.nodes.new("ShaderNodeSeparateColor")
    ramp = nt.nodes.new("ShaderNodeValToRGB")
    ramp.color_ramp.elements[0].position = 0.0; ramp.color_ramp.elements[0].color = (0.86, 0.50, 0.34, 1)      # (the preview's skin)
    ramp.color_ramp.elements[1].position = 1.0; ramp.color_ramp.elements[1].color = (0.19, 0.115, 0.065, 1)    # (the preview's hair)
    nt.links.new(attr.outputs["Color"], sep.inputs["Color"])
    nt.links.new(sep.outputs["Red"], ramp.inputs["Fac"])
    nt.links.new(ramp.outputs["Color"], b.inputs["Base Color"])
    nt.links.new(b.outputs["BSDF"], out.inputs["Surface"])
    b.inputs["Roughness"].default_value = 0.65
    return m


def smooth_boundary(obj, passes=16, factor=0.55):
    """The open edge of a hair (its hairline, the cut round an ear, the hem) drawn straighter along itself: the cards' own outline is a little ragged."""
    bm = bmesh.new(); bm.from_mesh(obj.data)
    for _ in range(passes):
        mv = {}
        for v in bm.verts:
            nb = [e.other_vert(v) for e in v.link_edges if e.is_boundary]
            if len(nb) == 2: mv[v] = ((nb[0].co + nb[1].co) / 2 - v.co) * factor
        for v, dv in mv.items(): v.co += dv
    bm.to_mesh(obj.data); bm.free()


def edge_alpha(obj, width=0.012):
    """The card's vertex colour: A = 0 on its open edge (hairline, round the ears, the hem) rising to 1 `width` metres inside it, so the shader can dissolve the edge
    (a soft hairline, not a cut line). Distance is along the mesh. R, G, B = 1."""
    bm = bmesh.new(); bm.from_mesh(obj.data); bm.verts.ensure_lookup_table()
    N = len(bm.verts)
    dist = [1e9] * N
    heap = []
    for v in bm.verts:
        if any(e.is_boundary for e in v.link_edges): dist[v.index] = 0.0; heap.append((0.0, v.index))
    heapq.heapify(heap)
    while heap:
        dd, i = heapq.heappop(heap)
        if dd > dist[i]: continue
        for e in bm.verts[i].link_edges:
            w = e.other_vert(bm.verts[i]).index; nd = dd + e.calc_length()
            if nd < dist[w] and nd < width: dist[w] = nd; heapq.heappush(heap, (nd, w))
    col = bm.verts.layers.float_color.get("Col") or bm.verts.layers.float_color.new("Col")
    for v in bm.verts:
        x = max(0.0, min(1.0, dist[v.index] / width)); a = x * x * (3 - 2 * x)
        v[col] = (1.0, 1.0, 1.0, a)
    bm.to_mesh(obj.data); bm.free()


def fade_cap(sk, hair, name, fade=0.012, lift=0.0022, limit=(38.0, -8.0, -40.0), close=0.016):
    """The scalp under a hair, in the hair colour, fading into the skin past the hair's edge: a hairline, a temple or a taper with no hard line.
    A thin skin of the head (the finer skull copy, laid on the real skin `lift` off it) over every part the hair covers (its own surface laid down on the scalp) and
    `fade` metres beyond, its vertex colour R = 1 under the hair, falling to 0 (the skin) at the edge of the fade. It stays above `limit` (front, side, back elevations
    of a line round the head: never down over the brows or the eyes), and fades out round the ears (a hair-coloured ear is not a haircut)."""
    bm = sk.fine.copy()
    bm.normal_update(); bm.verts.ensure_lookup_table()
    # what the hair covers: every point of its own surface (corners, edge midpoints, centres) within 2.5 cm of the scalp lays its patch of scalp down (a ray out of each
    # scalp vertex missed the card's rim, where the hair leaves the head: the fade began under the hair instead of past its edge)
    hv = [hair.matrix_world @ v.co for v in hair.data.vertices]
    samples = list(hv)
    for p_ in hair.data.polygons:
        ids = list(p_.vertices)
        samples.append(sum((hv[i] for i in ids), Vector()) / len(ids))
        for k in range(len(ids)): samples.append((hv[ids[k]] + hv[ids[(k + 1) % len(ids)]]) / 2)
    kd = KDTree(N_ := len(bm.verts))
    for v in bm.verts: kd.insert(v.co, v.index)
    kd.balance()
    b = (limit[0] - limit[2]) / 2; d = limit[0] - limit[1] - b
    line = lambda c: limit[1] + b * c + d * c * c
    N = N_
    allowed = [True] * N; ear = []; margin_el = [0.0] * N
    for v in bm.verts:
        az, el = sk.az_el(v.co)
        margin_el[v.index] = el - line(math.cos(math.radians(az)))
        if margin_el[v.index] < 0: allowed[v.index] = False
        if 78 < abs(az) < 128 and -30 < el < 32: ear.append(v.index)
    covered = set()
    for s in samples:
        loc, nm, idx, dd = sk.full.find_nearest(s)
        if loc is None or dd > 0.025: continue
        for co, i, dist in kd.find_range(loc, 0.0065): covered.add(i)
    cover = sorted(covered)

    def distance(sources, cap):
        dist = [1e9] * N
        heap = []
        for i in sources: dist[i] = 0.0; heap.append((0.0, i))
        heapq.heapify(heap)
        while heap:
            dd, i = heapq.heappop(heap)
            if dd > dist[i]: continue
            for e in bm.verts[i].link_edges:
                w = e.other_vert(bm.verts[i]).index; nd = dd + e.calc_length()
                if nd < dist[w] and nd < cap: dist[w] = nd; heapq.heappush(heap, (nd, w))
        return dist
    # a hair of loose locks (his own) has gaps between them: a gap narrower than 2 x close is hair too (dilate, then erode)
    if close > 0:
        grown = distance(cover, close)
        outside = [i for i in range(N) if grown[i] >= close]
        shrunk = distance(outside, close)
        cover = [i for i in range(N) if grown[i] < close and shrunk[i] >= close] or cover
    dc = distance(cover, fade * 1.5)
    de = distance(ear, 0.02)
    def smooth(x): x = max(0.0, min(1.0, x)); return x * x * (3 - 2 * x)
    R = [0.0] * N
    for i in range(N):
        if not allowed[i]: continue
        R[i] = smooth(1.0 - dc[i] / fade) * smooth(de[i] / 0.014) * smooth(margin_el[i] / 7.0)      # (and out toward the limit line: no cut where it is still hair-coloured)
    col = bm.verts.layers.float_color.new("Col")
    for v in bm.verts: v[col] = (R[v.index], 1.0, 1.0, 1.0)
    # only what shows: faces with some hair colour, none of them off the allowed region
    drop = [f for f in bm.faces if max(R[v.index] for v in f.verts) < 0.01 or not all(allowed[v.index] for v in f.verts)]
    bmesh.ops.delete(bm, geom=drop, context='FACES')
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
    for v in bm.verts:                                                  # laid on the real skin
        loc, nm, idx, dd = sk.full.find_nearest(v.co)
        if loc is not None: v.co = loc + nm * lift
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    for f in bm.faces: f.smooth = True
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new(name, me); bpy.context.scene.collection.objects.link(o)
    me.materials.append(scalp_material())
    return o


def join_into(obj, extra):
    """`extra` (another mesh object) merged into obj, which keeps its name and takes the material slots and the vertex colours."""
    with bpy.context.temp_override(active_object=obj, object=obj, selected_editable_objects=[obj, extra], selected_objects=[obj, extra]):
        bpy.ops.object.join()
    return obj


def make_material(name, tex_path):
    """A preview material: the grey strands tinted a hair colour, alpha cut-out (the game builds its own from the texture)."""
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    for nd in list(nt.nodes):
        if nd.type != 'OUTPUT_MATERIAL': nt.nodes.remove(nd)
    out = nt.nodes["Material Output"]
    b = nt.nodes.new("ShaderNodeBsdfPrincipled")
    tex = nt.nodes.new("ShaderNodeTexImage"); tex.image = bpy.data.images.load(tex_path)
    tex.image.alpha_mode = 'STRAIGHT'
    ramp = nt.nodes.new("ShaderNodeValToRGB")            # the grey strands -> a brown (the game tints them any hair colour)
    ramp.color_ramp.elements[0].position = 0.15; ramp.color_ramp.elements[0].color = (0.02, 0.012, 0.008, 1)
    ramp.color_ramp.elements[1].position = 0.80; ramp.color_ramp.elements[1].color = (0.30, 0.19, 0.11, 1)
    nt.links.new(tex.outputs["Color"], ramp.inputs["Fac"])
    nt.links.new(ramp.outputs["Color"], b.inputs["Base Color"])
    nt.links.new(tex.outputs["Alpha"], b.inputs["Alpha"])
    nt.links.new(b.outputs["BSDF"], out.inputs["Surface"])
    b.inputs["Roughness"].default_value = 0.65
    if "Specular IOR Level" in b.inputs: b.inputs["Specular IOR Level"].default_value = 0.2
    try: m.surface_render_method = 'DITHERED'
    except Exception: pass
    m.use_backface_culling = False
    return m


REFERENCE = ("Crop", "Fringe", "Mop", "Quiff")
# how far past a hair's edge its scalp fades into the skin (metres): the short cuts taper wide, the long ones are covered anyway
EDGE = {"Crop": 0.014, "Fringe": 0.012, "Mop": 0.012, "Quiff": 0.014, "SideBob": 0.010, "Bob": 0.010, "Long": 0.012, "Ponytail": 0.012, "Braid": 0.010}      # (how far the card's open edge dissolves: metres)
FADE = {"Crop": 0.018, "Fringe": 0.014, "Mop": 0.014, "Quiff": 0.018, "SideBob": 0.008, "Bob": 0.008, "Long": 0.008, "Ponytail": 0.014, "Braid": 0.012}        # the close cuts: their scalp gap is known, so they place the head


def build(sk, colliders, mhdir, texdir, names=None):
    """{game name: object} for the MakeHuman hairs, fitted to this head (skull `sk`), pushed clear of `colliders` (a BVH of head, body, kit).
    The textures (grey strands, alpha: fetch_mhhair.py makes them) are read from texdir as <Name>.png."""
    fit = Fit(sk, colliders)
    objs = {}
    for name, (folder, tex, gap) in HAIRS.items():
        path = os.path.join(mhdir, "hair", folder, folder + ".obj")
        if not os.path.exists(path): print(f"  (no {path})"); continue
        o = _import(path)
        o.name = o.data.name = "Hair_" + name
        objs[name] = o
    refs = [n for n in REFERENCE if n in objs]
    k, c = fit.fit_scale([objs[n] for n in refs], [HAIRS[n][2] for n in refs])
    print(f"  MakeHuman head -> this head: scale {k:.3f} (cost {c:.4f})")
    out = {}
    for name, o in objs.items():
        if names and name not in names:
            bpy.data.objects.remove(o, do_unlink=True); continue
        c, ty, tz = fit.place(o, k, HAIRS[name][2], steps=6)
        M = Matrix.Translation(Vector((0.0, ty, tz))) @ Matrix.Scale(k, 4)
        for v in o.data.vertices: v.co = M @ v.co
        smooth_boundary(o)
        moved = fit.push_out(o)
        mat = make_material("HairCard_" + name, os.path.join(texdir, name + ".png"))
        o.data.materials.clear(); o.data.materials.append(mat)
        for p in o.data.polygons: p.use_smooth = True
        edge_alpha(o, EDGE.get(name, 0.012))
        join_into(o, fade_cap(sk, o, "Scalp_" + name, fade=FADE.get(name, 0.012)))
        print(f"  MakeHuman {name} ({HAIRS[name][0]}): placed y {ty * 100:+.1f} z {tz * 100:+.1f} cm (cost {c:.4f}), {moved} verts pushed, {len(o.data.polygons)} polys")
        out[name] = o
    return out
