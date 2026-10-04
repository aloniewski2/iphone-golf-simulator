"""Restore winding on every retained original face; new closed neck retains its deliberate outward normals."""
import bpy,bmesh,json
from pathlib import Path
R=Path(__file__).resolve().parents[4];O=R/'ArtDir/hero/v6_polish';bpy.ops.wm.open_mainfile(filepath=str(O/'Hero_V6_Polish.blend'));o=bpy.data.objects['Body_Skin']
with bpy.data.libraries.load(str(O/'checkpoints/pre-foundation.blend')) as (a,b):b.objects=['Body_Skin']
src=b.objects[0]
def key(points):return tuple(sorted(tuple(round(c,6) for c in p) for p in points))
lookup={key(src.data.vertices[i].co for i in f.vertices):f.normal.copy() for f in src.data.polygons};bm=bmesh.new();bm.from_mesh(o.data);bm.normal_update();restored=0;matched=0
for f in bm.faces:
 ref=lookup.get(key(v.co for v in f.verts))
 if ref is None:continue
 matched+=1
 if f.normal.dot(ref)<0:f.normal_flip();restored+=1
bm.to_mesh(o.data);bm.free();o.data.update();bpy.data.objects.remove(src,do_unlink=True)
(O/'surface-winding.json').write_text(json.dumps({'matched_retained_faces':matched,'winding_restored_faces':restored,'vertex_positions_changed':0},indent=2));bpy.ops.wm.save_as_mainfile(filepath=str(O/'Hero_V6_Polish.blend'));print('WINDING',matched,restored)
