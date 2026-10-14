"""GLOVE: numpy part dict <-> Blender object (face attrs zone/kind/fabric/sub + float point attrs), preview materials."""
import os, sys
sys.path.insert(0, os.path.dirname(__file__))
import numpy as np, bpy, bmesh
import acc_common as A, acc_geo as G


def to_object(name, part, fix=True):
    P = np.asarray(part['pos']); T = np.asarray(part['tris'], np.int64)
    me = bpy.data.meshes.new(name)
    me.vertices.add(len(P)); me.vertices.foreach_set('co', P.astype(np.float32).ravel())
    me.loops.add(T.size); me.loops.foreach_set('vertex_index', T.ravel().astype(np.int32))
    me.polygons.add(len(T)); me.polygons.foreach_set('loop_start', np.arange(0, T.size, 3, dtype=np.int32))
    me.update(calc_edges=True); me.validate(clean_customdata=False)
    me.shade_smooth()
    ob = bpy.data.objects.new(name, me); bpy.context.scene.collection.objects.link(ob)
    for k in ('zone', 'kind', 'fabric', 'sub'):
        a = me.attributes.new(k, 'INT', 'FACE'); a.data.foreach_set('value', np.asarray(part[k], np.int32))
    for k, v in part.get('fields', {}).items():
        a = me.attributes.new(k, 'FLOAT', 'POINT'); a.data.foreach_set('value', np.asarray(v, np.float32))
    if fix: G.fix_normals(ob)
    return ob


def read_back(ob):
    me = ob.data
    n = len(me.vertices)
    P = np.zeros(n * 3); me.vertices.foreach_get('co', P); P = P.reshape(-1, 3)
    li = np.zeros(len(me.loops), np.int32); me.loops.foreach_get('vertex_index', li)
    return P, li.reshape(-1, 3)
