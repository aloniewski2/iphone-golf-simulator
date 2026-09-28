import bpy,json
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree
P=Path(__file__).resolve().parent
bpy.ops.wm.open_mainfile(filepath=str(P/'polish_before/hands_fixed.blend'))
rig=bpy.data.objects['Hero_01_Rig'];rig.animation_data.action=None
for b in rig.pose.bones:b.matrix_basis.identity()
# Small surface-mounted catchlights, joined into the existing eye slots.
m=bpy.data.materials.new('Hero_01_Catchlight');m.use_nodes=True
bs=m.node_tree.nodes.get('Principled BSDF');bs.inputs['Base Color'].default_value=(1,1,1,1);bs.inputs['Roughness'].default_value=.3;bs.inputs['Emission Color'].default_value=(1,1,1,1);bs.inputs['Emission Strength'].default_value=.3
for side,sgn in [('L',1),('R',-1)]:
 eye=bpy.data.objects['Body_EyeSphere_'+side]
 tree=BVHTree.FromPolygons([v.co for v in eye.data.vertices],[list(f.vertices) for f in eye.data.polygons])
 loc,normal,idx,d=tree.ray_cast(Vector((sgn*.074,-.3,1.443)),Vector((0,1,0)))
 if loc is None:raise RuntimeError('Catchlight ray missed eye')
 bpy.ops.mesh.primitive_uv_sphere_add(segments=12,ring_count=6,radius=.0048,location=loc+Vector((0,-.0007,0)))
 glint=bpy.context.object;glint.scale=(1,.2,1.1);glint.data.materials.append(m);glint.vertex_groups.new(name='Head').add(list(range(len(glint.data.vertices))),1,'REPLACE')
 for f in glint.data.polygons:f.use_smooth=True
 bpy.ops.object.select_all(action='DESELECT');eye.select_set(True);glint.select_set(True);bpy.context.view_layer.objects.active=eye;bpy.ops.object.join()
# Isolate hair material treatment without tinting the polo or skin atlas users.
hair=bpy.data.objects['Hair_Default']
for i,old in enumerate(list(hair.data.materials)):
 new=old.copy();new.name='HairPolish_'+old.name;hair.data.materials[i]=new
# Use flat, saturated color for existing orange shoe accent faces (no new shoe shape).
shoe=bpy.data.objects['Shoes_Default'];accent=bpy.data.materials.new('Hero_01_ShoeOrange');accent.use_nodes=True
bs=accent.node_tree.nodes.get('Principled BSDF');bs.inputs['Base Color'].default_value=(1,.147,.047,1);bs.inputs['Roughness'].default_value=.7
shoe.data.materials.append(accent);slot=len(shoe.data.materials)-1
im=bpy.data.images['Hero_01_BaseColor'];pixels=list(im.pixels);width,height=im.size;uv=shoe.data.uv_layers.active.data;count=0
for f in shoe.data.polygons:
 center=sum((uv[i].uv for i in f.loop_indices),Vector((0,0)))/len(f.loop_indices)
 x=min(width-1,max(0,int(center.x*width)));y=min(height-1,max(0,int(center.y*height)));r,g,b=pixels[(y*width+x)*4:(y*width+x)*4+3]
 if r>.38 and r>g*1.8 and g>b*1.5 and b<.22:
  f.material_index=slot;count+=1
rig.animation_data.action=bpy.data.actions['Idle'];bpy.context.scene.frame_set(1)
bpy.ops.wm.save_as_mainfile(filepath=str(P/'Hero_01_Mixamo_QA.blend'))
(P/'face-material-polish.json').write_text(json.dumps({'catchlights':'two surface glints joined to existing eye meshes; Head weighted','shoe_orange_faces':count},indent=2))
