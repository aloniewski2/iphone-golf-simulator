import bpy
for o in bpy.data.objects:
    print('OBJ', o.name, o.type, len(o.data.vertices) if o.type=='MESH' else '', [m.name if m else None for m in o.data.materials] if o.type=='MESH' else '', list(o.data.attributes.keys()) if o.type=='MESH' else '', [u.name for u in o.data.uv_layers] if o.type=='MESH' else '')
