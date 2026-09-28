"""Hero short haircuts on the REBUILT head (head_rebuild.py runs first; the head is one real skull now).
  Hair_Bald  = nothing to add: the bare head IS bald. A single tiny triangle inside the skull keeps the slot.
  Hair_Buzz / Hair_Waves = the head's own surface inside a real hairline (above the brows, round the temples,
               above the ears, down to the nape), pushed a few mm out along the normals, tucked into the skin at
               the hairline edge, hair material x a tileable detail texture (spherical UVs round the crown: a
               horizontal wave texture becomes the concentric rings of 360 waves), matte. 100% Head bone.
Called from build_v5.py ('scalp' step, after 'head')."""
import bpy, bmesh, math
from mathutils import Vector
from pathlib import Path
TEX = Path(__file__).resolve().parents[3] / 'Unity/Assets/ArtDirection/Hero01/Textures'
O = Vector((0, .035, 1.425))          # head centre (head_rebuild.SKULL)

def hairline(lon):
    """Lowest latitude (deg, from the head centre) a short cut covers: forehead, temple recess, above the ear, nape."""
    a = abs(((lon + 180) % 360) - 180)
    if a < 30: return 24 - 2 * (a / 30)
    if a < 60: return 22 + 4 * ((a - 30) / 30)       # slight temple recess
    if a < 110: return 26 - 14 * ((a - 60) / 50)     # round the temple, above the ear
    if a < 150: return 12 - 34 * ((a - 110) / 40)
    return -22 - 8 * ((a - 150) / 30)                # nape

def latlon(p):
    d = (p - O).normalized()
    return math.degrees(math.asin(max(-1, min(1, d.z)))), math.degrees(math.atan2(d.x, -d.y))

def shell(name, body, off, style, hair_mat):
    me = body.data; mw = body.matrix_world
    hg = body.vertex_groups['Head'].index
    bm = bmesh.new(); bm.from_mesh(me); dl = bm.verts.layers.deform.active
    skin_i = next(i for i, m in enumerate(me.materials) if m and m.name.startswith('skin_WarmSkin'))
    def inside(f):
        if f.material_index != skin_i or not all(v[dl].get(hg, 0) > .5 for v in f.verts): return False
        la, lo = latlon(mw @ f.calc_center_median()); return la > hairline(lo)
    keep = set(f for f in bm.faces if inside(f))
    bmesh.ops.delete(bm, geom=[f for f in bm.faces if f not in keep], context='FACES')
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
    for v in bm.verts: v.co = mw @ v.co
    bm.normal_update()
    edge = set(v for v in bm.verts if v.is_boundary); ring = {v: 0 for v in edge}; front = list(edge); d = 0
    while front and d < 3:
        d += 1; nxt = []
        for v in front:
            for e in v.link_edges:
                w = e.other_vert(v)
                if w not in ring: ring[w] = d; nxt.append(w)
        front = nxt
    for v in bm.verts:
        k = min(1.0, ring.get(v, 3) / 2.5)
        v.co = v.co + v.normal * (off * k - .001 * (1 - k))      # tucked 1 mm into the skin at the hairline
    uv = bm.loops.layers.uv.verify()
    for f in bm.faces:
        f.smooth = True; f.material_index = 0
        for l in f.loops:
            p = l.vert.co - O
            lon = math.atan2(p.x, -p.y) / (2 * math.pi) + .5
            lat = math.acos(max(-1, min(1, p.z / max(p.length, 1e-6)))) / math.pi
            l[uv].uv = (lon * 6, lat * 7)
    m2 = bpy.data.meshes.new(name); bm.to_mesh(m2); bm.free()
    o = bpy.data.objects.new(name, m2); bpy.context.scene.collection.objects.link(o)
    mat = hair_mat.copy(); mat.name = 'Hero_01_HairTuft_' + style
    if mat.use_nodes:
        nt = mat.node_tree; bs = nt.nodes.get('Principled BSDF')
        img = bpy.data.images.load(str(TEX / ('Hair_' + style + '.png')), check_existing=True)
        ti = nt.nodes.new('ShaderNodeTexImage'); ti.image = img
        mix = nt.nodes.new('ShaderNodeMix'); mix.data_type = 'RGBA'; mix.blend_type = 'MULTIPLY'; mix.inputs['Factor'].default_value = 1
        mix.inputs[7].default_value = bs.inputs['Base Color'].default_value
        nt.links.new(ti.outputs['Color'], mix.inputs[6]); nt.links.new(mix.outputs[2], bs.inputs['Base Color'])
        bs.inputs['Roughness'].default_value = 1.0                  # matte: hair, not a helmet
    o.data.materials.append(mat)
    return o

def weight_head(o, rig):
    o.vertex_groups.clear(); vg = o.vertex_groups.new(name='Head'); vg.add(list(range(len(o.data.vertices))), 1.0, 'REPLACE')
    o.parent = rig; m = o.modifiers.new('Armature', 'ARMATURE'); m.object = rig

def build(context, report):
    body = bpy.data.objects['Body_Skin']; rig = bpy.data.objects['Hero_01_Rig']; hair = bpy.data.objects['Hair_Default']
    for n in ('Hair_Bald', 'Hair_Buzz', 'Hair_Waves'):
        if n in bpy.data.objects: bpy.data.objects.remove(bpy.data.objects[n])
    me = bpy.data.meshes.new('Hair_Bald')
    me.from_pydata([O, O + Vector((.001, 0, 0)), O + Vector((0, .001, 0))], [], [(0, 1, 2)])
    bald = bpy.data.objects.new('Hair_Bald', me); context.scene.collection.objects.link(bald)
    for m in hair.data.materials: bald.data.materials.append(m)
    weight_head(bald, rig)
    for name, off, style in (('Hair_Buzz', .003, 'Buzz'), ('Hair_Waves', .0045, 'Waves')):
        o = shell(name, body, off, style, hair.data.materials[0])
        weight_head(o, rig)
        report[name + '_tris'] = sum(len(p.vertices) - 2 for p in o.data.polygons)
