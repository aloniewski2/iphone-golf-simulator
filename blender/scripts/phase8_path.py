import bpy, math, sys, importlib
import os, bpy; sys.path.insert(0, os.path.join(bpy.path.abspath("//"), "scripts"))
import hole07_lib as H; importlib.reload(H)
import hole07_design as D; importlib.reload(D)
from hole07_design import *
from mathutils import Vector

MAT_PATH = H.get_material("MAT_PATH", H.rgb(196, 198, 200), roughness=0.95)
MAT_PATH_EDGE = H.get_material("MAT_PATH_EDGE", H.rgb(150, 154, 156), roughness=0.95)

def ribbon(name, cl, half_w, z_off, mat, col="STRUCTURES", rows=2):
    """strip mesh along a polyline; `rows` subdivisions across so it hugs the terrain."""
    ob = H.new_mesh_object(name, col)
    verts, faces = [], []
    across = rows + 1
    for i, p in enumerate(cl):
        p0 = cl[max(0, i - 1)]; p1 = cl[min(len(cl) - 1, i + 1)]
        t = (p1 - p0).normalized()
        nrm = Vector((-t.y, t.x))
        for k in range(across):
            s = -1.0 + 2.0 * k / rows
            q = p + nrm * (half_w * s)
            verts.append((q.x, q.y, height(q.x, q.y) + z_off))
    for i in range(len(cl) - 1):
        for k in range(rows):
            a = i * across + k
            faces.append((a, a + 1, a + across + 1, a + across))
    H.build_mesh(ob, verts, faces, smooth=True)
    H.assign(ob, mat)
    return ob

cl = path_points(16)
# spur from the path to the tee box and a short apron at the clubhouse
tee_spur = H.catmull_rom([(-26, -216), (-18, -208), (-16, -198)], 8)

ribbon("CART_PATH", cl, 2.3, 0.16, MAT_PATH)
ribbon("CART_PATH_EDGE", cl, 3.0, 0.12, MAT_PATH_EDGE)
ribbon("CART_PATH_TEE_SPUR", tee_spur, 1.8, 0.16, MAT_PATH)
H.remove_object("CART_PATH_CLUB")

H.save()
result = {"path_faces": len(bpy.data.objects["CART_PATH"].data.polygons)}
