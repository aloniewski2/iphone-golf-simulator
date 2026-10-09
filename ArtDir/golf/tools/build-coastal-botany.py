"""Adapt approved CC0 Poly Haven fir sources. No procedural needle invention.
Canonical inputs are downloaded/MD5-verified by fetch-coastal-artists.py.
"""
import bpy,bmesh,json,pathlib,math,sys
from pathlib import Path
import argparse
R=Path(__file__).resolve().parents[3]
p=argparse.ArgumentParser();p.add_argument('--cache',type=Path,default=R/'ArtDir/golf/vendor/polyhaven');p.add_argument('--out',type=Path,default=R/'ArtDir/golf/source/coastal-botany-generated')
a=p.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
V=a.cache.resolve();OUT=a.out.resolve();OUT.mkdir(parents=True,exist_ok=True)

bpy.ops.wm.read_factory_settings(use_empty=True)
items=[]
for asset,requests in [('fir_sapling_medium',[('a','A'),('b','B'),('c','C')]),('fir_tree_01',[('c','D')])]:
 path=V/asset/(asset+'_1k.blend')
 for sourceLetter,letter in requests:
  name=asset+'_'+sourceLetter+('_LOD1' if 'sapling' in asset else '_LOD2')
  with bpy.data.libraries.load(str(path),link=False) as (a,b):b.objects=[name]
  o=b.objects[0];bpy.context.collection.objects.link(o);o.location=(0,0,0);o.name='CONIFER_'+letter
  uv=o.data.attributes.get('UVMap')
  if uv and not o.data.uv_layers:
   vals=[tuple(v.vector) for v in uv.data];o.data.attributes.remove(uv);layer=o.data.uv_layers.new(name='UVMap')
   for v,u in zip(layer.data,vals):v.uv=u[:2]
  # Source artist twig cards remain exact. Only woody surfaces are welded
  # before bounded simplification to avoid disconnected-face shards.
  if asset=='fir_tree_01':
   pieces=[]
   for slot,mat in enumerate(o.data.materials):
    if 'dead' in mat.name:continue
    part=o.copy();part.data=o.data.copy();bpy.context.collection.objects.link(part)
    bm=bmesh.new();bm.from_mesh(part.data);bmesh.ops.delete(bm,geom=[f for f in bm.faces if f.material_index!=slot],context='FACES');bmesh.ops.delete(bm,geom=[v for v in bm.verts if not v.link_faces],context='VERTS')
    if 'twig' not in mat.name:bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.00002)
    bm.to_mesh(part.data);bm.free();bpy.context.view_layer.objects.active=part
    count=sum(len(f.vertices)-2 for f in part.data.polygons);target=12500 if 'twig' in mat.name else 1100
    if count>target:
     d=part.modifiers.new('Bounded artist wood','DECIMATE');d.ratio=target/count;d.use_collapse_triangulate=True;bpy.ops.object.modifier_apply(modifier=d.name)
    pieces.append(part)
   bpy.ops.object.select_all(action='DESELECT')
   for q in pieces:q.select_set(True)
   bpy.context.view_layer.objects.active=pieces[0];bpy.ops.object.join();p=pieces[0];bpy.data.objects.remove(o,do_unlink=True);o=p;o.name='CONIFER_'+letter
  zmin=min(v.co.z for v in o.data.vertices);zmax=max(v.co.z for v in o.data.vertices)
  # Actual native proportions, roots at0. Chosen uniform source metres
  # avoids stretching needle cards independently from the native boughs.
  scale={'A':13.0,'B':11.5,'C':15.0,'D':17.0}[letter]/(zmax-zmin)
  for v in o.data.vertices:v.co=(v.co-__import__('mathutils').Vector((0,0,zmin)))*scale
  if letter=='C':
   # Fill the reference crown using only selected original needle boughs.
   # Retain their own UV/leaf scale, with an offset azimuth through open gaps.
   bm=bmesh.new();bm.from_mesh(o.data)
   twigslots={i for i,m in enumerate(o.data.materials) if 'twig' in m.name}
   chosen=[f for f in bm.faces if f.material_index in twigslots and f.calc_center_median().z<11.5 and f.calc_center_median().z>1.1]
   result=bmesh.ops.duplicate(bm,geom=chosen)
   for v in result['geom']:
    if isinstance(v,bmesh.types.BMVert):
     x,y=v.co.x,v.co.y;ang=math.radians(37);v.co.x=(math.cos(ang)*x-math.sin(ang)*y)*.93;v.co.y=(math.sin(ang)*x+math.cos(ang)*y)*.93;v.co.z+=.055
   bm.to_mesh(o.data);bm.free()
  o.data.calc_loop_triangles();items.append(o)
# Actual texture materials, not source generator field nodes.
for mat in bpy.data.materials:
 if not mat.name.startswith(('fir_tree_01','fir_sapling_medium')):continue
 asset='fir_sapling_medium' if mat.name.startswith('fir_sapling_medium') else 'fir_tree_01'
 role='twigs' if 'twigs' in mat.name else 'twig' if 'twig' in mat.name else 'trunk_c' if 'trunk' in mat.name else 'branches' if asset=='fir_sapling_medium' else 'bark'
 mat.use_nodes=True;nt=mat.node_tree;nt.nodes.clear();out=nt.nodes.new('ShaderNodeOutputMaterial');b=nt.nodes.new('ShaderNodeBsdfPrincipled');nt.links.new(b.outputs['BSDF'],out.inputs['Surface']);b.inputs['Roughness'].default_value=.86
 def tex(kind,linear=False):
  im=bpy.data.images.load(str(V/asset/'textures'/(asset+'_'+role+'_'+kind+'_1k.png')),check_existing=True)
  if linear:im.colorspace_settings.name='Non-Color'
  n=nt.nodes.new('ShaderNodeTexImage');n.image=im;return n
 c=tex('diff');nt.links.new(c.outputs['Color'],b.inputs['Base Color'])
 n=tex('nor_gl',True);normal=nt.nodes.new('ShaderNodeNormalMap');normal.inputs['Strength'].default_value=.4;nt.links.new(n.outputs['Color'],normal.inputs['Color']);nt.links.new(normal.outputs['Normal'],b.inputs['Normal'])
 if 'twig' in role:
  alpha=tex('alpha',True);nt.links.new(alpha.outputs['Color'],b.inputs['Alpha']);b.inputs['Subsurface Weight'].default_value=.035
# Small native artist card models replace the procedural leaf wedges.
for asset,original,name,height in [('shrub_02','shrub_02_b_LOD2','SHRUB_A',1.05),('shrub_02','shrub_02_c_LOD2','SHRUB_B',.95),('fern_02','fern_02_a','FERN',.45)]:
 with bpy.data.libraries.load(str(V/asset/(asset+'_1k.blend')),link=False) as (a,b):b.objects=[original]
 o=b.objects[0];bpy.context.collection.objects.link(o);o.location=(0,0,0);o.name=name+'_SOURCE'
 zmin=min(v.co.z for v in o.data.vertices);zmax=max(v.co.z for v in o.data.vertices);scale=height/(zmax-zmin)
 for v in o.data.vertices:v.co=(v.co-__import__('mathutils').Vector((0,0,zmin)))*scale
 if name.startswith('SHRUB'):
  # A small irregular clump is composed from the actual artist stems,
  # retaining native leaf cards and UVs instead of enlarging leaf polygons.
  bm=bmesh.new();bm.from_mesh(o.data);original=list(bm.faces)
  for angle,dx,dy in ([(109,.19,-.09),(247,-.16,.13)] if name=='SHRUB_A' else [(73,.27,-.15)]):
   result=bmesh.ops.duplicate(bm,geom=original)
   for v in result['geom']:
    if isinstance(v,bmesh.types.BMVert):
     x,y=v.co.x,v.co.y;a=math.radians(angle);v.co.x=math.cos(a)*x-math.sin(a)*y+dx;v.co.y=math.sin(a)*x+math.cos(a)*y+dy
  bm.to_mesh(o.data);bm.free()
 for tier,target in [('NEAR',999999),('FAR',1400 if name!='FERN' else 280)]:
  p=o.copy();p.data=o.data.copy();bpy.context.collection.objects.link(p);p.name=name+('' if tier=='NEAR' else '_FAR');items.append(p)
  if tier=='FAR':
   bpy.context.view_layer.objects.active=p;count=sum(len(f.vertices)-2 for f in p.data.polygons)
   d=p.modifiers.new('Bounded far artist cards','DECIMATE');d.ratio=min(1,target/count);d.use_collapse_triangulate=True;bpy.ops.object.modifier_apply(modifier=d.name)
 bpy.data.objects.remove(o,do_unlink=True)
for mat in bpy.data.materials:
 if mat.name.startswith(('shrub_02','fern_02')):
  asset='shrub_02' if mat.name.startswith('shrub_02') else 'fern_02';mat.use_nodes=True;nt=mat.node_tree;nt.nodes.clear();out=nt.nodes.new('ShaderNodeOutputMaterial');b=nt.nodes.new('ShaderNodeBsdfPrincipled');nt.links.new(b.outputs['BSDF'],out.inputs['Surface']);b.inputs['Roughness'].default_value=.83
  for kind,extension,linear in [('diff','jpg',False),('alpha','png',True),('nor_gl','exr',True)]:
   im=bpy.data.images.load(str(V/asset/'textures'/(asset+'_'+kind+'_1k.'+extension)),check_existing=True)
   if linear:im.colorspace_settings.name='Non-Color'
   t=nt.nodes.new('ShaderNodeTexImage');t.image=im
   if kind=='diff':nt.links.new(t.outputs['Color'],b.inputs['Base Color'])
   if kind=='alpha':nt.links.new(t.outputs['Color'],b.inputs['Alpha'])
   if kind=='nor_gl':n=nt.nodes.new('ShaderNodeNormalMap');n.inputs['Strength'].default_value=.4;nt.links.new(t.outputs['Color'],n.inputs['Color']);nt.links.new(n.outputs['Normal'],b.inputs['Normal'])
for o in items:
 if 'FINAL_NAME' in o:o.name=o['FINAL_NAME'];del o['FINAL_NAME']
for o in items:o.hide_render=False;o.hide_set(False)
audit=[]
for o in items:
 o.data.calc_loop_triangles();audit.append({'name':o.name,'triangles':len(o.data.loop_triangles),'dimensions':list(o.dimensions),'materials':[m.name if m else None for m in o.data.materials]})
(OUT/'source-manifest.json').write_text(json.dumps({'sources':['https://polyhaven.com/a/fir_sapling_medium','https://polyhaven.com/a/fir_tree_01','https://polyhaven.com/a/shrub_02','https://polyhaven.com/a/fern_02'],'license':'CC0','artists':['Rico Cilliers','Rob Tuytel'],'meshes':audit},indent=2))
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'CoastalBotany.blend'))
print(json.dumps(audit,indent=2))
