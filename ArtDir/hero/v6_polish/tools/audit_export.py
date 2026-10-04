import bpy,bmesh,json
from pathlib import Path
R=Path(__file__).resolve().parents[4];O=R/'ArtDir/hero/v6_polish'
bpy.ops.wm.open_mainfile(filepath=str(O/'Hero_V6_Export.blend'))
report={}
for o in bpy.data.objects:
 if o.type!='MESH':continue
 bm=bmesh.new();bm.from_mesh(o.data)
 report[o.name]={'vertices':len(bm.verts),'triangles':sum(len(f.verts)-2 for f in bm.faces),'boundary_edges':sum(e.is_boundary for e in bm.edges),'nonmanifold_edges':sum(not e.is_manifold for e in bm.edges),'uv_layers':[l.name for l in o.data.uv_layers],'uv_overlap_verified':False}
 bm.free()
(R/'ArtDir/hero/v6_proof/export-geometry-audit.json').write_text(json.dumps(report,indent=2))
