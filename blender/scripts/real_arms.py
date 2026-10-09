"""Real arms for the fitted avatar bodies.

The scanned avatars' own arms deform badly once their weights are copied onto the tennis
rig (the thin forearm shell bends in the wrong places), and cutting the open hands away
left the forearm ending short of the grip fist -- bent arms and hands that float off them.
This replaces everything below the sleeve with a clean arm grown along the rig's own arm
bones: a continuous tapered surface (deltoid inside the sleeve, biceps, a narrower elbow,
forearm swell, slim wrist) that runs on into the fist, weighted bone by bone with smooth
blends at the elbow and wrist, and coloured from the body's own skin texel so it is part of
the same mesh and material.

Used by fit_avatar.py (and anything that fits an avatar body) after fit_character.
"""
import math

import bmesh
import bpy
from mathutils import Vector


def _skin_like(rgb):
    r, g, b = rgb
    mx, mn = max(r, g, b), min(r, g, b)
    sat = (mx - mn) / mx if mx > 1e-4 else 0
    return r > g > b and sat > .25 and mx > .25


def _texel(px, uv):
    h, w = px.shape[:2]
    return px[min(h - 1, max(0, int(uv[1] * h))), min(w - 1, max(0, int(uv[0] * w)))][:3]


def real_arms(gender, rig, body, px, report):
    R = rig.matrix_world
    bone = lambda n: rig.data.bones[n]
    head = lambda n: R @ bone(n).head_local
    tail = lambda n: R @ bone(n).tail_local
    me = body.data
    mw = body.matrix_world
    names = {vg.index: vg.name for vg in body.vertex_groups}
    uv = me.uv_layers.active.data
    vert_uv = {}
    for loop in me.loops: vert_uv.setdefault(loop.vertex_index, uv[loop.index].uv.copy())

    def weight(v, name):
        return sum(g.weight for g in v.groups if names.get(g.group) == name)

    skin_uv, doomed, radii = None, set(), {}
    for s in "LR":
        up, lo, hd = f"UpperArm.{s}", f"LowerArm.{s}", f"Hand.{s}"
        a, e, w = head(up), head(lo), head(hd)
        fore, upper = [], []
        for v in me.vertices:
            wl = weight(v, lo) + weight(v, hd); wu = weight(v, up)
            if wl + wu < .5: continue
            col = _texel(px, vert_uv.get(v.index, (0, 0)))
            p = mw @ v.co
            if wl >= .5:
                doomed.add(v.index)
                if _skin_like(col):
                    fore.append(_dist_to_segment(p, e, w))
                    if skin_uv is None: skin_uv = vert_uv[v.index]
            elif _skin_like(col):
                # Bare upper-arm skin below the sleeve.
                doomed.add(v.index); upper.append(_dist_to_segment(p, a, e))
        fore.sort(); upper.sort()
        rf = fore[len(fore) // 2] if fore else .035
        ru = upper[len(upper) // 2] if upper else rf * 1.2
        # The upper arm reads a little heavier than the forearm; the measured value picks up
        # the inside of the sleeve, so keep it within a natural ratio.
        radii[s] = (min(max(ru, rf * 1.1), rf * 1.3), rf)
    if skin_uv is None:
        report.append("arms: no skin texel found; arms kept")
        return 0

    # Remove the old forearms/hands and bare upper-arm skin.
    bm = bmesh.new(); bm.from_mesh(me); bm.verts.ensure_lookup_table()
    bmesh.ops.delete(bm, geom=[bm.verts[i] for i in doomed], context="VERTS")
    bm.to_mesh(me); bm.free(); me.update()

    arms = []
    for s in "LR":
        ru, rf = radii[s]
        up, lo, hd = f"UpperArm.{s}", f"LowerArm.{s}", f"Hand.{s}"
        a, e, w = head(up), head(lo), head(hd)
        hand_dir = (tail(hd) - w).normalized()
        # Chain: shoulder (inside the sleeve), biceps, elbow, forearm swell, wrist, into the fist.
        pts = [a + (e - a) * .12, a + (e - a) * .5, e, e + (w - e) * .3, e + (w - e) * .72, w, w + hand_dir * rf * 1.6]
        rad = [(ru * 1.05, ru * .95), (ru, ru * .92), (rf * 1.02, rf * .92), (rf * 1.12, rf * 1.0),
               (rf * .9, rf * .8), (rf * .78, rf * .62), (rf * .7, rf * .55)]
        mesh = bpy.data.meshes.new(f"V4 real arm {s}")
        mesh.from_pydata([tuple(p) for p in pts], [(i, i + 1) for i in range(len(pts) - 1)], [])
        obj = bpy.data.objects.new(f"V4 real arm {s}", mesh)
        bpy.context.scene.collection.objects.link(obj)
        skin = obj.modifiers.new("Skin", "SKIN")
        for i, sv in enumerate(mesh.skin_vertices[0].data):
            sv.radius = rad[i]
            if i == 0: sv.use_root = True
        sub = obj.modifiers.new("Smooth", "SUBSURF"); sub.levels = 2; sub.render_levels = 2
        dg = bpy.context.evaluated_depsgraph_get()
        baked = bpy.data.meshes.new_from_object(obj.evaluated_get(dg))
        obj.modifiers.clear(); obj.data = baked
        for poly in baked.polygons: poly.use_smooth = True
        # Weights: nearest point along the chain, blended across the elbow and the wrist.
        groups = {n: obj.vertex_groups.new(name=n) for n in (up, lo, hd)}
        elbow_blend, wrist_blend = rf * 1.4, rf * .9
        for v in baked.vertices:
            p = v.co
            t_e = (p - e).dot((w - e).normalized())          # signed distance past the elbow
            t_w = (p - w).dot(hand_dir)                        # past the wrist
            if t_w > -wrist_blend:
                k = min(1, (t_w + wrist_blend) / (2 * wrist_blend))
                groups[hd].add([v.index], k, "REPLACE"); groups[lo].add([v.index], 1 - k, "REPLACE")
            elif t_e < elbow_blend:
                k = max(0, min(1, (t_e + elbow_blend) / (2 * elbow_blend)))
                groups[lo].add([v.index], k, "REPLACE"); groups[up].add([v.index], 1 - k, "REPLACE")
            else:
                groups[lo].add([v.index], 1.0, "REPLACE")
        # Skin colour: every corner samples the body's own skin texel.
        layer = baked.uv_layers.new(name=me.uv_layers.active.name)
        for d in layer.data: d.uv = skin_uv
        baked.materials.append(me.materials[0])
        arms.append(obj)

    # One mesh: join the arms into the body (vertex groups merge by name).
    for o in bpy.context.view_layer.objects: o.select_set(False)
    for o in arms: o.select_set(True)
    body.select_set(True); bpy.context.view_layer.objects.active = body
    bpy.ops.object.join()
    report.append(f"arms: rebuilt (upper r {radii['R'][0]:.3f}, forearm r {radii['R'][1]:.3f}), {len(doomed)} scanned arm vertices replaced")
    return len(doomed)


def _dist_to_segment(p, a, b):
    ab = b - a
    t = max(0, min(1, (p - a).dot(ab) / max(ab.length_squared, 1e-9)))
    return (a + ab * t - p).length
