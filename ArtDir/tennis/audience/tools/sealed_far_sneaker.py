"""Blender helper: sealed low-cost sneakers from actual fitted exterior meshes.

The input mesh uses evaluated, world-space coordinates and original material
slots. It is never edited. No convex hull spans toe, heel and ankle. Call
build_sealed_far_sneaker(mesh, body_bvh, name, target=280) from the audience bake.
The returned mesh preserves source coordinates and Kit_Shoe/Kit_Sole roles.
"""
import bpy
import bmesh
import math
from mathutils import Vector
from mathutils.bvhtree import BVHTree


def _seal(mesh):
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=0.000005)
    boundary = [edge for edge in bm.edges if edge.is_boundary]
    if boundary:
        bmesh.ops.holes_fill(bm, edges=boundary, sides=0)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(mesh)
    bm.free()
    mesh.update()


def build_sealed_far_sneaker(src_mesh, body_bvh, name, target=280):
    """Return (mesh, triangles, audit); an opaque closed shoe with no calf hull."""
    coords = [vertex.co.copy() for vertex in src_mesh.vertices]
    source_bounds = [[min(v[i] for v in coords), max(v[i] for v in coords)] for i in range(3)]
    polygons = []
    for poly in src_mesh.polygons:
        centre = sum((coords[i] for i in poly.vertices), Vector()) / len(poly.vertices)
        hit = body_bvh.find_nearest(centre) if body_bvh else None
        if not body_bvh or (hit and hit[0] is not None and poly.normal.dot(hit[1]) > -0.05):
            polygons.append(poly)
    used = sorted({index for poly in polygons for index in poly.vertices})
    remap = {index: new for new, index in enumerate(used)}
    mesh = bpy.data.meshes.new(name + '_sealed_exterior')
    mesh.from_pydata([coords[index] for index in used], [],
                     [[remap[index] for index in poly.vertices] for poly in polygons])
    mesh.update()
    for material in src_mesh.materials:
        mesh.materials.append(material)
    _seal(mesh)
    ob = bpy.data.objects.new(name + '_working', mesh)
    bpy.context.collection.objects.link(ob)
    bpy.context.view_layer.objects.active = ob
    # Consolidate the actual exterior and its ankle closure into one solid.
    # A tiny voxel cell removes collapsed inner shell and lace/sliver fragments
    # while retaining the shoe's curved heel, broad toe and separate sole plane.
    remesh = ob.modifiers.new('Closed authored sneaker volume', 'REMESH')
    remesh.mode = 'VOXEL'
    remesh.voxel_size = 0.004
    remesh.use_smooth_shade = True
    bpy.ops.object.modifier_apply(modifier=remesh.name)
    smooth = ob.modifiers.new('Retain softly rounded sneaker', 'SMOOTH')
    smooth.factor = 0.17
    smooth.iterations = 2
    bpy.ops.object.modifier_apply(modifier=smooth.name)
    ob.data.calc_loop_triangles()
    before = len(ob.data.loop_triangles)
    # Retopologize the closed measured volume into cross sections. Collapsing
    # 30k voxel triangles directly to280 spends vertices on lace/cuff slivers
    # and leaves jagged toe/sole silhouettes. The sections allocate a coherent
    # rounded heel and toe, all sampled from the real authored shoe volume.
    volume = ob.data
    positions = [vertex.co.copy() for vertex in volume.vertices]
    bvh = BVHTree.FromPolygons(positions, [list(poly.vertices) for poly in volume.polygons])
    sole = next((i for i, material in enumerate(src_mesh.materials)
                 if material and material.name.startswith('Kit_Sole')), None)
    sole_points = [coords[index].z for poly in polygons if poly.material_index == sole
                   for index in poly.vertices]
    floor, roof = source_bounds[2]
    sole_top = max(sole_points) if sole_points else floor + (roof - floor) * 0.18
    sole_top = min(sole_top, floor + (roof - floor) * 0.23)
    ymin = min(p.y for p in positions)
    ymax = max(p.y for p in positions)
    sections, ring_size = max(8, (int(target) - 18) // 22 + 1), 11
    vertices, faces, face_roles, centres = [], [], [], []
    for section in range(sections):
        t = (section + 0.16) / (sections - 1 + 0.32)
        y = ymin + (ymax - ymin) * t
        slab = [p for p in positions if abs(p.y - y) < 0.0045]
        if not slab:
            slab = sorted(positions, key=lambda p: abs(p.y - y))[:30]
        centre = Vector(((min(p.x for p in slab) + max(p.x for p in slab)) * 0.5,
                         y, (min(p.z for p in slab) + max(p.z for p in slab)) * 0.5))
        def radial_points(origin):
            result = []
            for j in range(7):
                theta = j * math.pi / 6
                direction = Vector((math.cos(theta), 0, math.sin(theta)))
                result.append(bvh.ray_cast(origin + direction * 0.4, -direction, 0.8)[0])
            return result
        ring = radial_points(centre)
        if any(point is None for point in ring):
            # Near the rounded toe, the full slab's rectangular mid-point can
            # lie beyond the small real volume. Move it just inside the actual
            # nearest surface, then sample a complete surface section there.
            nearest = bvh.find_nearest(centre)
            centre = nearest[0] - nearest[1] * 0.004
            ring = radial_points(centre)
        if any(point is None for point in ring):
            raise RuntimeError('Measured sneaker cross section did not intersect its closed volume: ' + str(section))
        # Explicit bottom and sole-top edges preserve the actual rubber-band
        # silhouette and isolate its normals from the rounded leather upper.
        # Material-by-centroid on coarse faces makes a jagged diagonal rim.
        right, left = max(p.x for p in slab), min(p.x for p in slab)
        yy = centre.y
        ring = [Vector((right, yy, floor)), Vector((right, yy, sole_top))] + ring + [
            Vector((left, yy, sole_top)), Vector((left, yy, floor))]
        centres.append(centre)
        vertices.extend(ring)
        if section:
            for j in range(ring_size):
                a = (section - 1) * ring_size + j
                b = (section - 1) * ring_size + (j + 1) % ring_size
                c = section * ring_size + (j + 1) % ring_size
                d = section * ring_size + j
                faces.append([a, b, c, d])
                face_roles.append(sole if sole is not None and j in [0, 9, 10] else 0)
    for section in [0, sections - 1]:
        base = section * ring_size
        faces.append([base, base + 1, base + 9, base + 10]); face_roles.append(sole if sole is not None else 0)
        faces.append(list(range(base + 1, base + 10))); face_roles.append(0)
    mesh = bpy.data.meshes.new(name + '_measured_rounded_sections')
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    for material in src_mesh.materials:
        mesh.materials.append(material)
    ob.data = mesh
    _seal(mesh)
    for poly, role in zip(mesh.polygons, face_roles):
        poly.material_index = role
        poly.use_smooth = role != sole
    mesh.calc_loop_triangles()
    bm = bmesh.new()
    bm.from_mesh(mesh)
    audit = {
        'source_bounds': source_bounds,
        'bounds': [[min(v.co[i] for v in mesh.vertices), max(v.co[i] for v in mesh.vertices)] for i in range(3)],
        'source_exterior_polygons': len(polygons),
        'voxel_triangles': before,
        'triangles': len(mesh.loop_triangles),
        'boundary_edges': sum(edge.is_boundary for edge in bm.edges),
        'nonmanifold_edges': sum(not edge.is_manifold for edge in bm.edges),
        'signed_volume': bm.calc_volume(signed=True),
        'sole_top': sole_top,
    }
    bm.free()
    bpy.data.objects.remove(ob, do_unlink=True)
    if audit['boundary_edges'] or audit['nonmanifold_edges'] or audit['signed_volume'] <= 0:
        raise RuntimeError('Sneaker volume is not sealed and outward oriented: ' + str(audit))
    return mesh, audit['triangles'], audit
