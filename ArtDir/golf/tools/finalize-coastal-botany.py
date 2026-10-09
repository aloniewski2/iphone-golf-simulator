import bpy,math,json,zlib,struct,sys
from pathlib import Path
import argparse
R=Path(__file__).resolve().parents[3]
p=argparse.ArgumentParser();p.add_argument('--cache',type=Path,default=R/'ArtDir/golf/vendor/polyhaven');p.add_argument('--out',type=Path,default=R/'ArtDir/golf/source/coastal-botany-generated')
a=p.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
V=a.cache.resolve();OUT=a.out.resolve();OUT.mkdir(parents=True,exist_ok=True)

bpy.ops.wm.open_mainfile(filepath=str(OUT/'CoastalBotany.blend'))
for asset,role in [('shrub_02','Shrub'),('fern_02','Fern')]:
 im=bpy.data.images.load(str(V/asset/'textures'/(asset+'_nor_gl_1k.exr')),check_existing=True);im.colorspace_settings.name='Non-Color'
 import numpy as np
 pixels=np.array(im.pixels[:],dtype=np.float32).reshape((im.size[1],im.size[0],4));rgb=(np.clip(pixels[::-1,:,:3],0,1)*255+.5).astype(np.uint8)
 w,h=im.size;raw=b''.join(b'\0'+row.tobytes() for row in rgb)
 def chunk(t,d):return struct.pack('>I',len(d))+t+d+struct.pack('>I',zlib.crc32(t+d)&0xffffffff)
 (OUT/'maps'/(role+'_N.png')).write_bytes(b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('>IIBBBBB',w,h,8,2,0,0,0))+chunk(b'IDAT',zlib.compress(raw,7))+chunk(b'IEND',b''))
for old in list(bpy.data.objects):
 if old.name.startswith('CONIFER') and old.name.endswith('_FAR'):bpy.data.objects.remove(old,do_unlink=True)
# Four crossed directional images, with exact baked root and camera padding.
for l in 'ABCD':
 near=bpy.data.objects['CONIFER_'+l];height=max(v.co.z for v in near.data.vertices);h=height*1.08;w=h*.75;bottom=height*.5-h*.5
 vs=[];fs=[];uvs=[]
 for i in range(4):
  a=i*math.pi*.5;c=math.cos(a);s=math.sin(a);start=len(vs)
  for x,z in [(-w*.5,bottom),(w*.5,bottom),(w*.5,bottom+h),(-w*.5,bottom+h)]:vs.append((c*x,s*x,z))
  fs.append((start,start+1,start+2,start+3));u=(i%2)*.5;v=(1-i//2)*.5;uvs.extend([(u,v),(u+.5,v),(u+.5,v+.5),(u,v+.5)])
 m=bpy.data.meshes.new('Artist fir distant four-view');m.from_pydata(vs,[],fs);m.update();layer=m.uv_layers.new(name='UVMap')
 for data,uv in zip(layer.data,uvs):data.uv=uv
 far=bpy.data.objects.new('CONIFER_'+l+'_FAR',m);bpy.context.collection.objects.link(far)
 mat=bpy.data.materials.new('COAST_FIR_VIEW_'+l);mat.use_nodes=True;nt=mat.node_tree;bsdf=nt.nodes.get('Principled BSDF');im=bpy.data.images.load(str(OUT/'maps'/('FirView_'+l+'_C.png')),check_existing=True);t=nt.nodes.new('ShaderNodeTexImage');t.image=im;nt.links.new(t.outputs['Color'],bsdf.inputs['Base Color']);nt.links.new(t.outputs['Alpha'],bsdf.inputs['Alpha']);bsdf.inputs['Roughness'].default_value=.9;m.materials.append(mat)
# Turn texture paths into packed editable source data. Runtime references small
# independently imported maps, so FBX never embeds dozens of vendor textures.
for im in bpy.data.images:
 if im.source=='FILE':
  candidate=Path(bpy.path.abspath(im.filepath))
  if not candidate.exists():
   found=list(V.rglob(Path(im.filepath).name))+list((OUT/'maps').glob(Path(im.filepath).name))
   assert found,'Missing editable source map '+im.filepath
   candidate=found[0]
  im.filepath=str(candidate.resolve());im.reload();im.pack()
objects=[o for o in bpy.data.objects if o.type=='MESH'];audit=[]
for o in objects:
 o.data.calc_loop_triangles();audit.append({'name':o.name,'triangles':len(o.data.loop_triangles),'materials':[m.name if m else None for m in o.data.materials],'uvCount':len(o.data.uv_layers),'dimensions':list(o.dimensions)})
assert all(o.data.uv_layers for o in objects),'Missing artist source UVs'
assert all(a['triangles']<=20000 for a in audit if a['name'].startswith('CONIFER')),'Near tree budget'
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'CoastalBotany.blend'))
bpy.ops.object.select_all(action='DESELECT')
for o in objects:o.select_set(True)
bpy.ops.export_scene.fbx(filepath=str(OUT/'CoastalBotany.fbx'),use_selection=True,axis_forward='-Z',axis_up='Y',apply_unit_scale=True,use_mesh_modifiers=True,path_mode='STRIP',bake_anim=False,add_leaf_bones=False)
j=json.load(open(OUT/'source-manifest.json'));j['meshes']=audit;j['distantRepresentation']='Four directional alpha-tested source bakes, eight triangles, no shadow casting';(OUT/'source-manifest.json').write_text(json.dumps(j,indent=2))
print(json.dumps(audit,indent=2))
