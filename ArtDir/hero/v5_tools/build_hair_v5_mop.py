"""Hero V5 hair: a closed, opaque cap fitted over the skull plus chunky tapered locks on a flow field.
Replaces the mesh of object 'Hair_Default' in the open .blend (keeps the object, its rig parent/armature
modifier and the material slot name), weights 100% to Head. Nothing is transparent; the cap is continuous
over the whole hair region and tucked into the skin at its edge, so no camera can see through the hair.
Run inside Blender with the V5 working .blend open (see build_v5.py)."""
import bpy, bmesh, math, random
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree

FWD = Vector((0, -1, 0)); UP = Vector((0, 0, 1)); RIGHT = Vector((1, 0, 0))   # Blender: hero faces -Y

def skull_tree(body, dg):
    me = body.evaluated_get(dg).to_mesh()
    vs = [body.matrix_world @ v.co for v in me.vertices]
    polys = [list(p.vertices) for p in me.polygons if all(vs[i].z > 1.22 for i in p.vertices)]
    return BVHTree.FromPolygons(vs, polys), vs

def dirv(lat, lon):
    """lat/lon in degrees; lon 0 = forward (-Y), +90 = hero's left (+X)."""
    la, lo = math.radians(lat), math.radians(lon)
    return (FWD * (math.cos(la) * math.cos(lo)) + RIGHT * (math.cos(la) * math.sin(lo)) + UP * math.sin(la)).normalized()

def hairline(lon):
    """Lowest latitude the hair covers at this longitude (deg). Front: above the brows (fringe goes lower as locks);
    sides: above the ears; back: down to the nape."""
    a = abs(((lon + 180) % 360) - 180)          # 0 front .. 180 back
    if a < 40: return 30 - 4 * (a / 40)       # forehead
    if a < 75: return 26 - 10 * ((a - 40) / 35)   # temple
    if a < 110: return 16 + 0 * a             # above the ear
    if a < 140: return 16 - 30 * ((a - 110) / 30)
    return -14 - 16 * ((a - 140) / 40)        # back of head / nape (down to -30)

def build(context, hair_obj, body, head_bone_name='Head', seed=7, cap_off=.013, n_locks=95, report=None):
    rnd = random.Random(seed)
    dg = context.evaluated_depsgraph_get()
    tree, bvs = skull_tree(body, dg)
    # the V4 hair shell = the volume the visor, silhouette and locker were fitted to: the V5 cap fills it solid
    ome = hair_obj.evaluated_get(dg).to_mesh(); ovs = [hair_obj.matrix_world @ v.co for v in ome.vertices]
    old_tree = BVHTree.FromPolygons(ovs, [list(p.vertices) for p in ome.polygons])
    hv = [v for v in bvs if v.z > 1.30]
    C = Vector((0, sum(v.y for v in hv) / len(hv), 1.405))
    def skull_r(d):
        hit = tree.ray_cast(C + d * .5, -d, .6)          # from outside inward: first skin surface
        return (hit[0] - C).length if hit[0] else None
    def shell_r(d):
        hit = old_tree.ray_cast(C + d * .5, -d, .6)
        return (hit[0] - C).length if hit[0] else None
    def cap_r(d):
        s = skull_r(d) or .105; o = shell_r(d)
        return max(s + cap_off, (o - .014) if o else 0)
    # ---------------- cap: lat-long grid over the covered region, radius = skull + offset, tucked at the edge
    LON = 48; LAT = 18
    lat_top = 90
    grid = []
    for j in range(LON):
        lon = j * 360 / LON
        lo_lat = hairline(lon) - 6                         # extend below the hairline, then tuck in
        row = []
        for i in range(LAT + 1):
            lat = lo_lat + (lat_top - lo_lat) * (i / LAT) ** .85
            d = dirv(min(lat, 89.5), lon)
            tuck = min(1, max(0, (hairline(lon) - lat)) / 6)   # 0 at hairline .. 1 at the bottom row
            r = cap_r(d) * (1 - tuck) + ((skull_r(d) or .105) - .006) * tuck   # edge sinks 6 mm into the skin
            row.append(C + d * r)
        grid.append(row)
    bm = bmesh.new()
    vgrid = [[bm.verts.new(p) for p in row] for row in grid]
    for j in range(LON):
        j2 = (j + 1) % LON
        for i in range(LAT):
            bm.faces.new((vgrid[j][i], vgrid[j2][i], vgrid[j2][i + 1], vgrid[j][i + 1]))
    top = bm.verts.new(C + UP * cap_r(UP))
    for j in range(LON): bm.faces.new((vgrid[j][LAT], vgrid[(j + 1) % LON][LAT], top))
    # ---------------- locks: swept clumps that lie on the hair volume and flick up only at the tip
    def tangent_flow(lat, lon):
        """(surface direction the lock sweeps along, tip flick 0..1, length scale) for a root at (lat, lon)."""
        a = ((lon + 180) % 360) - 180               # 0 front, + = hero's left
        d = dirv(lat, lon)
        def tang(v): v = v - d * v.dot(d); return v.normalized() if v.length > 1e-4 else FWD
        back = tang(-FWD); down = tang(-UP); left = tang(RIGHT)
        if lat > 62:                     # crown: everything sweeps back, tips flick up
            return (back + left * .15 * (1 if a > 0 else -1)).normalized(), .9, 1.15
        if abs(a) < 55 and lat > 34:     # front above the band: swept up and back over the top
            return (tang(UP) * .7 + back * .5).normalized(), 1.0, 1.1
        if abs(a) < 60:                  # fringe: down over the forehead, swept to the hero's right (-X)
            return (down * 1.0 - left * (.55 if a > -20 else .2)).normalized(), .15, 1.0
        if abs(a) < 125:                 # sides: down and back over the ear tops
            return (down * .85 + back * .5).normalized(), .35, .9
        return (down * 1.0 + left * .1 * (1 if a > 0 else -1)).normalized(), .3, 1.0   # back: layers to the nape
    roots = []
    tries = 0
    while len(roots) < n_locks and tries < 30000:
        tries += 1
        lon = rnd.uniform(0, 360); hl = hairline(lon)
        a = abs(((lon + 180) % 360) - 180)
        lo = hl + 1 if a >= 60 else 17          # fringe roots sit just above the brow line under the band
        lat = lo + (88 - lo) * (rnd.random() ** 1.1)
        d = dirv(lat, lon)
        if any((d - r[2]).length < .2 for r in roots): continue
        roots.append((lat, lon, d))
    locks_made = 0
    for lat, lon, d in roots:
        base = C + d * (cap_r(d) - .01)
        fl, flick, lscale = tangent_flow(lat, lon)
        a = abs(((lon + 180) % 360) - 180)
        band = 30 < lat < 50 and a < 150                   # under the visor band: short and flat
        fringe = a < 60 and lat < 34
        crown = lat > 55
        length = rnd.uniform(.11, .15) * lscale * (.45 if band else 1) * (1.15 if crown else 1) * (.75 if fringe else 1)
        width = rnd.uniform(.07, .095) * (.85 if crown else 1)
        thick = width * rnd.uniform(.45, .55)
        rings, sides = 7, 6
        pts = []; p = base.copy(); t = fl.copy()
        for k in range(rings + 1):
            s = k / rings
            pts.append(p.copy())
            out = (p - C).normalized()
            t = (t - out * t.dot(out)).normalized()                     # stay on the surface ...
            lift = flick * max(0, (s - .55) / .45) ** 1.5 * (.9 if not band else .2)
            if fringe: lift = -.05
            t = (t + out * lift).normalized()                            # ... then flick out at the tip
            p = p + t * (length / rings)
            rr = (p - C).length; rmin = cap_r((p - C).normalized()) + (.002 if fringe else -.002)
            if rr < rmin: p = C + (p - C).normalized() * rmin
            if fringe and math.degrees(math.asin(max(-1, min(1, (p - C).normalized().z)))) < 13:   # stop at the brow line
                break
        if len(pts) < 4: continue
        rings = len(pts) - 1
        ring_verts = []
        for k, q in enumerate(pts):
            s = k / rings
            tng = (pts[min(k + 1, rings)] - pts[max(k - 1, 0)]).normalized()
            out = (q - C).normalized()
            bx = tng.cross(out).normalized(); by = bx.cross(tng).normalized()
            w = width * (1 - s ** 2.2) * (.8 + .2 * math.sin(math.pi * min(1, s * 2)))
            h = thick * (1 - s ** 1.6)
            ring = []
            for m in range(sides):
                ang = 2 * math.pi * m / sides
                ring.append(bm.verts.new(q + bx * (math.cos(ang) * w * .5) + by * (math.sin(ang) * h * .5 + h * .15)))
            ring_verts.append(ring)
        for k in range(rings):
            for m in range(sides):
                m2 = (m + 1) % sides
                bm.faces.new((ring_verts[k][m], ring_verts[k][m2], ring_verts[k + 1][m2], ring_verts[k + 1][m]))
        tip = bm.verts.new(pts[-1] + (pts[-1] - pts[-2]).normalized() * .01)
        for m in range(sides): bm.faces.new((ring_verts[-1][m], ring_verts[-1][(m + 1) % sides], tip))
        cap_c = bm.verts.new(pts[0] - (pts[1] - pts[0]).normalized() * .006)
        for m in range(sides): bm.faces.new((ring_verts[0][(m + 1) % sides], ring_verts[0][m], cap_c))
        locks_made += 1
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    # ---------------- write into Hair_Default (world -> object space), smooth shading, Head weights
    inv = hair_obj.matrix_world.inverted()
    for v in bm.verts: v.co = inv @ v.co
    me = bpy.data.meshes.new('Hair_Default')
    bm.to_mesh(me); bm.free()
    for p in me.polygons: p.use_smooth = True
    old = hair_obj.data
    mats = list(old.materials)
    hair_obj.data = me
    for m in mats: me.materials.append(m)
    hair_obj.vertex_groups.clear()
    vg = hair_obj.vertex_groups.new(name=head_bone_name); vg.add(list(range(len(me.vertices))), 1.0, 'REPLACE')
    if old.users == 0: bpy.data.meshes.remove(old)
    me.name = 'Hair_Default'
    if report is not None:
        report.update(hair_verts=len(me.vertices), hair_tris=sum(len(p.vertices) - 2 for p in me.polygons), locks=locks_made, head_centre=list(C))
    return C
