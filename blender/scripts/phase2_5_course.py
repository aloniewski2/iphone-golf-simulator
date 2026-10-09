import bpy, bmesh, math, random, sys, importlib
import os, bpy; sys.path.insert(0, os.path.join(bpy.path.abspath("//"), "scripts"))
import hole07_lib as H
importlib.reload(H)
import hole07_design as D
importlib.reload(D)
from hole07_design import *
from mathutils import Vector

sc = bpy.context.scene

# ============================================================ materials (blockout colours)
MAT_ROUGH   = H.get_material("MAT_ROUGH",   H.rgb(62, 150, 40))
MAT_FAIRWAY = H.get_material("MAT_FAIRWAY", H.rgb(118, 205, 58))
MAT_GREEN   = H.get_material("MAT_GREEN",   H.rgb(150, 224, 70))
MAT_FAIRWAY_STRIPE = H.get_material("MAT_FAIRWAY_STRIPE", H.rgb(100, 190, 50))
MAT_FIRSTCUT = H.get_material("MAT_FIRSTCUT", H.rgb(90, 180, 48))
MAT_BUNKER_LIP = H.get_material("MAT_BUNKER_LIP", H.rgb(160, 226, 84))
MAT_SAND    = H.get_material("MAT_SAND",    H.rgb(236, 214, 160))
MAT_WATER   = H.get_material("MAT_WATER",   H.rgb(18, 78, 178), roughness=0.15)
MAT_WATER_SHALLOW = H.get_material("MAT_WATER_SHALLOW", H.rgb(46, 150, 222), roughness=0.15)
MAT_FOAM = H.get_material("MAT_FOAM", H.rgb(222, 242, 250), roughness=0.6)
MAT_CLIFF   = H.get_material("MAT_CLIFF",   H.rgb(112, 116, 124))
MAT_CLIFF_DARK = H.get_material("MAT_CLIFF_DARK", H.rgb(86, 90, 98))

# ============================================================ island terrain + cliffs
def build_island():
    outline, rnd = island_outline()

    verts, faces = H.fill_polygon(outline, 7.0, height, 0.0, margin=3.0)
    ob = H.new_mesh_object("TERRAIN_ISLAND", "COURSE")
    me = ob.data
    me.from_pydata(verts, [], faces)
    me.validate(); me.update()

    bm = bmesh.new()
    bm.from_mesh(me)
    bm.verts.ensure_lookup_table()
    n_top = len(bm.faces)
    bm.faces.ensure_lookup_table()
    top_faces = set(bm.faces[i] for i in range(n_top))

    # boundary loop (ordered) from boundary edges
    boundary = [e for e in bm.edges if e.is_boundary]
    nxt = {}
    for e in boundary:
        a, b = e.verts
        # orient consistently with face winding
        f = e.link_faces[0]
        loop = [l for l in f.loops if l.edge == e][0]
        nxt[loop.vert] = loop.link_loop_next.vert
    start = boundary[0].verts[0]
    ring = [start]
    v = nxt[start]
    while v != start and len(ring) < len(boundary) + 2:
        ring.append(v)
        v = nxt[v]

    # cliff rings: grassy lip, then stacked rocky bands with coherent bulges,
    # down to a base below the waterline. Push is radial from the island centre
    # blended with the local outward normal so coves and headlands stay readable.
    center = sum((v.co.xy for v in ring), Vector((0, 0))) / len(ring)
    n_ring = len(ring)
    normals = []
    for i, tv in enumerate(ring):
        p0 = ring[i - 1].co.xy; p1 = ring[(i + 1) % n_ring].co.xy
        t = (p1 - p0).normalized()
        nrm = Vector((t.y, -t.x))  # outward for CCW ring
        if nrm.dot(tv.co.xy - center) < 0:
            nrm = -nrm
        normals.append(nrm)
    def bulge(i, k):
        return (0.55 * math.sin(i * 0.61 + k * 1.7) + 0.45 * math.sin(i * 0.17 + k * 0.9)
                + 0.3 * math.sin(i * 1.9 + k))
    rings = [ring]
    specs = [  # (z fraction, base push, bulge amplitude, z jitter)
        (0.965, 0.9, 0.3, 0.0),   # lip
        (0.84,  3.2, 1.8, 1.0),   # upper band (overhang under the lip)
        (0.64,  4.0, 2.6, 1.2),
        (0.44,  3.6, 2.8, 1.2),
        (0.24,  4.8, 2.4, 1.0),
        (None,  6.5, 2.0, 0.0),   # base at z=-6
    ]
    for k, (frac, base, amp, zj) in enumerate(specs, 1):
        new_ring = []
        for i, tv in enumerate(ring):
            d = normals[i]
            push = base + amp * bulge(i, k) + rnd.uniform(-0.5, 0.5)
            push = max(0.3, push)
            top_z = tv.co.z
            z = top_z * frac + rnd.uniform(-zj, zj) if frac is not None else -6.0
            nv = bm.verts.new((tv.co.x + d.x * push, tv.co.y + d.y * push, z))
            new_ring.append(nv)
        rings.append(new_ring)
    # bridge rings
    cliff_faces = []
    for r0, r1 in zip(rings[:-1], rings[1:]):
        n = len(r0)
        for i in range(n):
            a, b = r0[i], r0[(i + 1) % n]
            c2, d2 = r1[(i + 1) % n], r1[i]
            try:
                f = bm.faces.new((a, b, c2, d2))
                cliff_faces.append(f)
            except ValueError:
                pass
    # bottom cap
    try:
        cap = bm.faces.new(list(reversed(rings[-1])))
        cliff_faces.append(cap)
    except ValueError:
        pass
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    # material slots: 0 rough (top), 1 cliff, 2 cliff dark (lower bands)
    for f in bm.faces:
        if f in top_faces:
            f.material_index = 0
            f.smooth = True
        else:
            f.material_index = 1
            f.smooth = False
    # lower bands darker
    lip = set(rings[0]) | set(rings[1])
    lower = set()
    for r in rings[4:]:
        lower.update(r)
    for f in cliff_faces:
        if all(v in lip for v in f.verts):
            f.material_index = 0
            f.smooth = True
        elif all(v in lower for v in f.verts):
            f.material_index = 2
    me.materials.clear()
    me.materials.append(MAT_ROUGH)
    me.materials.append(MAT_CLIFF)
    me.materials.append(MAT_CLIFF_DARK)
    bm.to_mesh(me)
    bm.free()
    me.update()
    return ob, outline

island, island_outline = build_island()

# ============================================================ fairway
fairway = H.surface_object("FAIRWAY", "COURSE", fairway_outline(), 5.0, height, 0.14, MAT_FAIRWAY)
fairway.data.materials.append(MAT_FAIRWAY_STRIPE)
for poly in fairway.data.polygons:
    c = poly.center
    poly.material_index = stripe_index(c.x, c.y) % 2
firstcut = H.surface_object("FAIRWAY_FIRSTCUT", "COURSE", fairway_outline(5.0), 6.0, height, 0.08, MAT_FIRSTCUT)

# ============================================================ green, tee, bunkers
green_out = green_outline()
green = H.surface_object("GREEN", "COURSE", green_out, 4.0, height, 0.26, MAT_GREEN)

# collar / fringe ring around the green so it reads like the mockup's lighter apron
apron_out = H.ellipse(GREEN["cx"], GREEN["cy"], GREEN["rx"] + 5, GREEN["ry"] + 5, GREEN["rot"], 40, wobble=0.04, seed=3)
apron = H.surface_object("GREEN_APRON", "COURSE", H.chaikin(apron_out, 1), 5.0, height, 0.18, MAT_FAIRWAY)

tee_out = tee_outline()
tee = H.surface_object("TEE_BOX", "COURSE", tee_out, 3.0, height, 0.24, MAT_GREEN)

def bunker_lip(name, outline, sand_z, lip_z):
    """band between the sand edge and a slightly larger outer ring, raised into a rim."""
    inner = [Vector((p.x, p.y)) for p in outline]
    c = sum(inner, Vector((0, 0))) / len(inner)
    outer = [c + (p - c) * 1.22 for p in inner]
    ob = H.new_mesh_object(name, "COURSE")
    n = len(inner)
    verts = [(p.x, p.y, height(p.x, p.y) + sand_z + 0.02) for p in inner]
    verts += [(p.x, p.y, height(p.x, p.y) + lip_z) for p in outer]
    mid = [c + (p - c) * 1.10 for p in inner]
    verts += [(p.x, p.y, height(p.x, p.y) + lip_z + 0.35) for p in mid]
    faces = []
    for i in range(n):
        j = (i + 1) % n
        faces.append((i, j, 2 * n + j, 2 * n + i))          # inner -> mid (rising)
        faces.append((2 * n + i, 2 * n + j, n + j, n + i))  # mid -> outer (falling)
    H.build_mesh(ob, verts, faces, smooth=True)
    H.assign(ob, MAT_BUNKER_LIP)
    return ob

for i, (cx, cy, rx, ry, rot, seed) in enumerate(BUNKERS, 1):
    out = H.blob(cx, cy, rx, ry, math.radians(rot), seed=seed, n=9, wobble=0.22, smooth=2)
    H.surface_object(f"BUNKER_{i:02d}", "COURSE", out, 2.5, height, 0.30, MAT_SAND)
    bunker_lip(f"BUNKER_{i:02d}_LIP", out, 0.30, 0.22)

# ============================================================ water
H.remove_object("WATER_OCEAN")
bpy.ops.mesh.primitive_plane_add(size=1, location=(0, 10, 0))
water = bpy.context.active_object
water.name = "WATER_OCEAN"
water.scale = (4000, 4000, 1)
H.link_to(water, "ENVIRONMENT")
water.parent = H.get_root()
H.assign(water, MAT_WATER)

# Shallow turquoise band hugging the cliffs, then a thin foam line right at the cliff foot.
shallow_in = offset_outline(island_outline, 2.0)
shallow_out = offset_outline(island_outline, 24.0, wobble=7.0, freq=0.35, seed=2)
band_object("WATER_SHALLOW", shallow_in, shallow_out, 0.04, MAT_WATER_SHALLOW)
foam_in = offset_outline(island_outline, 2.0)
foam_out = offset_outline(island_outline, 10.5, wobble=2.2, freq=0.9, seed=5)
band_object("WATER_FOAM", foam_in, foam_out, 0.08, MAT_FOAM)
# a second, broken foam ring further out (wave crests) for the mockup's white wave lines
crest_in = offset_outline(island_outline, 15.0, wobble=3.0, freq=0.6, seed=9)
crest_out = offset_outline(island_outline, 16.6, wobble=3.0, freq=0.6, seed=9)
crest = band_object("WATER_FOAM_CREST", crest_in, crest_out, 0.09, MAT_FOAM)
# break the crest ring into dashes by deleting every other run of faces
import bmesh as _bm
bm = _bm.new(); bm.from_mesh(crest.data); bm.faces.ensure_lookup_table()
kill = [f for f in bm.faces if (f.index // 9) % 3 == 1]
_bm.ops.delete(bm, geom=kill, context='FACES')
bm.to_mesh(crest.data); bm.free()

# ============================================================ done
path = H.save()
counts = {o.name: (len(o.data.vertices), len(o.data.polygons)) for o in bpy.data.objects if o.type == 'MESH'}
result = {"saved": path, "counts": counts}
