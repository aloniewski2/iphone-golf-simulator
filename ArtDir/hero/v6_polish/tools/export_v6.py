import bpy,bmesh,json,math
from pathlib import Path
from mathutils import Matrix
R=Path(__file__).resolve().parents[4];O=R/'ArtDir/hero/v6_polish';D=R/'Unity/Assets/ArtDirection/HeroV6Polish';D.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(O/'Hero_V6_Polish.blend'));rig=bpy.data.objects['Hero_01_Rig'];rig.animation_data_clear()
for p in rig.pose.bones:p.matrix_basis=Matrix.Identity(4)
def mask_bake(o):
 for m in list(o.modifiers):
  if m.type!='MASK':continue
  gi=o.vertex_groups[m.vertex_group].index;bm=bmesh.new();bm.from_mesh(o.data);dl=bm.verts.layers.deform.active
  kill=[v for v in bm.verts if ((v[dl].get(gi,0)>m.threshold)==m.invert_vertex_group)]
  bmesh.ops.delete(bm,geom=kill,context='VERTS');bm.to_mesh(o.data);bm.free();o.modifiers.remove(m)
# Merge skin sections into the canonical Body_Skin renderer (all remain skin, no clothing baked).
body=bpy.data.objects['Body_Skin'];legs=bpy.data.objects.get('Body_Legs');scalp=bpy.data.objects.get('Body_Scalp')
sections=[o for o in [body,legs,scalp] if o]
for ob in sections:mask_bake(ob)
if len(sections)>1:
 bpy.ops.object.select_all(action='DESELECT')
 for ob in sections:ob.select_set(True)
 bpy.context.view_layer.objects.active=body;bpy.ops.object.join()
for o in list(bpy.data.objects):
 if o.type=='MESH' and o.name=='Body_Legs_Full':bpy.data.objects.remove(o,do_unlink=True)
# Store material properties explicitly for deterministic URP import.
mats={}
for o in bpy.data.objects:
 if o.type!='MESH':continue
 for m in o.data.materials:
  if not m or m.name in mats:continue
  b=m.node_tree.nodes.get('Principled BSDF') if m.use_nodes else None
  mats[m.name]={'color':list(b.inputs['Base Color'].default_value) if b else list(m.diffuse_color),'roughness':b.inputs['Roughness'].default_value if b else .65}
# Hair LODs are closed decimated versions, no transparency or cards; body/outfit remain same to preserve morphs.
for name in ['Hair_Default','Hair_Default_Free']:
 src=bpy.data.objects[name]
 for level,ratio in [(1,.58),(2,.30)]:
  ob=src.copy();ob.data=src.data.copy();bpy.context.collection.objects.link(ob);ob.name=name+'_LOD'+str(level)
  if ob.data.shape_keys:ob.shape_key_clear()
  bpy.context.view_layer.objects.active=ob;d=ob.modifiers.new('Opaque lock LOD','DECIMATE');d.ratio=ratio
  bpy.ops.object.modifier_apply(modifier=d.name);ob.hide_render=True
meshes=[o for o in bpy.data.objects if o.type=='MESH'];counts={o.name:sum(len(p.vertices)-2 for p in o.data.polygons) for o in meshes}
(D/'Hero_V6_Materials.json').write_text(json.dumps(mats,indent=2));(O/'export-report.json').write_text(json.dumps({'triangles':counts,'default_lod0_triangles':sum(n for k,n in counts.items() if '_LOD' not in k and not k.endswith('_Free')),'materials':mats},indent=2))
bpy.ops.object.select_all(action='DESELECT');rig.select_set(True)
for o in meshes:o.hide_set(False);o.select_set(True)
for o in bpy.data.objects:
 if o.type=='EMPTY':o.select_set(True)
bpy.context.view_layer.objects.active=rig
bpy.ops.export_scene.fbx(filepath=str(D/'Hero_V6_Polish.fbx'),use_selection=True,object_types={'ARMATURE','MESH','EMPTY'},add_leaf_bones=False,bake_anim=False,axis_forward='-Z',axis_up='Y',use_mesh_modifiers=True,path_mode='COPY',embed_textures=True)
bpy.ops.wm.save_as_mainfile(filepath=str(O/'Hero_V6_Export.blend'))
print('V6_EXPORT',counts)
