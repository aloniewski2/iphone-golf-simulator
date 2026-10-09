import bpy,json,hashlib,math
from pathlib import Path
from mathutils import Vector
P=Path(__file__).resolve().parent
bpy.ops.wm.open_mainfile(filepath=str(P/'Hero_01_Assembled.blend'))
C=bpy.data.collections['Hero_01'];rig=next(o for o in C.objects if o.type=='ARMATURE');body=bpy.data.objects['Body_Skin'];mask=bpy.data.images.load(str(P/'Hero_01_SkinMask.png'));mask.name='Hero_01_SkinMask';mask.colorspace_settings.name='Non-Color';mask.pack();ref=json.loads((P/'Hero_01_SkinMask.json').read_text())['skin']
# Remove unused material slots; duplicate skin instances so cosmetics cannot be tinted.
for o in C.objects:
 if o.type!='MESH':continue
 ids=sorted({p.material_index for p in o.data.polygons});old=list(o.data.materials);pids=[p.material_index for p in o.data.polygons];o.data.materials.clear()
 for i in ids:
  m=old[i]
  if o==body and 'Covered' not in m.name:m=m.copy();m.name='skin_'+old[i].name.replace('Hero_01_','')
  o.data.materials.append(m)
 for p,i in zip(o.data.polygons,pids):p.material_index=ids.index(i)
# Blender-only preview of the SAME existing KitRecolor math; no runtime code added.
g=bpy.data.node_groups.new('Hero_01_SkinTint_Controls','ShaderNodeTree');g.interface.new_socket(name='Tone',in_out='OUTPUT',socket_type='NodeSocketColor');g.interface.new_socket(name='Strength',in_out='OUTPUT',socket_type='NodeSocketFloat');out=g.nodes.new('NodeGroupOutput');tone=g.nodes.new('ShaderNodeRGB');tone.name='Skin Tone';tone.label='Skin target colour';strength=g.nodes.new('ShaderNodeValue');strength.name='Tint Strength';strength.label='0 = approved original; 1 = selected tone';strength.outputs[0].default_value=0;g.links.new(tone.outputs[0],out.inputs['Tone']);g.links.new(strength.outputs[0],out.inputs['Strength'])
def linear(x):return x/12.92 if x<=.04045 else ((x+.055)/1.055)**2.4
def sethex(h):tone.outputs[0].default_value=tuple(linear(int(h[i:i+2],16)/255) for i in (0,2,4))+(1,)
sethex('F2C093');export_links=[]
for m in body.data.materials:
 ns=m.node_tree.nodes;ls=m.node_tree.links;bs=ns.get('Principled BSDF');original=bs.inputs['Base Color'].links[0].from_socket if bs.inputs['Base Color'].links else None;originalcolor=tuple(bs.inputs['Base Color'].default_value);ctrl=ns.new('ShaderNodeGroup');ctrl.node_tree=g
 mix=ns.new('ShaderNodeMixRGB');mix.blend_type='MIX';mix.label='KitRecolor-compatible skin tint';ls.new(mix.outputs[0],bs.inputs['Base Color'])
 if original:
  ls.new(original,mix.inputs[1]);bw=ns.new('ShaderNodeRGBToBW');ls.new(original,bw.inputs[0]);div=ns.new('ShaderNodeMath');div.operation='DIVIDE';ls.new(bw.outputs[0],div.inputs[0]);div.inputs[1].default_value=ref
  low=ns.new('ShaderNodeMath');low.operation='MAXIMUM';ls.new(div.outputs[0],low.inputs[0]);low.inputs[1].default_value=.25
  high=ns.new('ShaderNodeMath');high.operation='MINIMUM';ls.new(low.outputs[0],high.inputs[0]);high.inputs[1].default_value=1.35
  mult=ns.new('ShaderNodeMixRGB');mult.blend_type='MULTIPLY';mult.inputs[0].default_value=1;ls.new(ctrl.outputs['Tone'],mult.inputs[1]);ls.new(high.outputs[0],mult.inputs[2]);ls.new(mult.outputs[0],mix.inputs[2])
  masknode=ns.new('ShaderNodeTexImage');masknode.image=mask;fac=ns.new('ShaderNodeMath');fac.operation='MULTIPLY';ls.new(masknode.outputs['Alpha'],fac.inputs[0]);ls.new(ctrl.outputs['Strength'],fac.inputs[1]);ls.new(fac.outputs[0],mix.inputs[0])
 else:
  mix.inputs[1].default_value=originalcolor;ls.new(ctrl.outputs['Tone'],mix.inputs[2]);ls.new(ctrl.outputs['Strength'],mix.inputs[0])
 export_links.append((m,bs,original,originalcolor,mix))
rig['SkinTint_Control']='Shader Editor > Hero_01_SkinTint_Controls > Skin Tone / Tint Strength';rig['SkinTint_Default']='Strength=0 preserves approved loadout';rig['SkinTint_Examples']='#F2C093, #B77950, #6A4030';rig['Modular_Runtime']='Rebind renderer bones by name to ONE shared rig; use existing KitRecolor alpha skin mask.'
# Export the default atlas through direct Principled inputs; FBX cannot serialize the
# Blender control graph. Mask + JSON accompany the FBX for the existing runtime tint path.
for m,bs,orig,col,mix in export_links:
 for l in list(bs.inputs['Base Color'].links):m.node_tree.links.remove(l)
 if orig:m.node_tree.links.new(orig,bs.inputs['Base Color'])
 else:bs.inputs['Base Color'].default_value=col
spec={'Hero_01_Body.fbx':['Body_Skin','Body_EyeSphere_L','Body_EyeSphere_R'],'Hero_01_Hair_Default.fbx':['Hair_Default'],'Hero_01_Hat_Visor.fbx':['Hat_Visor'],'Hero_01_Shirt_Default.fbx':['Shirt_Default'],'Hero_01_Shorts_Default.fbx':['Shorts_Default'],'Hero_01_Shoes_Default.fbx':['Shoes_Default']}
spec['Hero_01_Body_DefaultCoverage.fbx']=spec['Hero_01_Body.fbx']
coverage=body.modifiers.get('Default outfit coverage')
for filename,names in spec.items():
 coverage.show_viewport=filename!='Hero_01_Body.fbx';coverage.show_render=coverage.show_viewport
 bpy.ops.object.select_all(action='DESELECT');rig.select_set(True)
 for n in names:bpy.data.objects[n].select_set(True)
 if filename in ['Hero_01_Body.fbx','Hero_01_Body_DefaultCoverage.fbx']:
  for n in ['Hat','Hand_R','Hand_L','Back','FaceExtra']:bpy.data.objects[n].select_set(True)
 bpy.context.view_layer.objects.active=rig;bpy.ops.export_scene.fbx(filepath=str(P/filename),use_selection=True,object_types={'MESH','ARMATURE','EMPTY'},add_leaf_bones=False,bake_anim=False,axis_forward='-Z',axis_up='Y',path_mode='COPY',embed_textures=True)
coverage.show_viewport=True;coverage.show_render=True
for m,bs,orig,col,mix in export_links:m.node_tree.links.new(mix.outputs[0],bs.inputs['Base Color'])
# Copy default source textures into this standalone output folder and pack everything.
for name,file in [('Hero_01_BaseColor','Hero_01_BaseColor.png'),('Hero_01_Normal','Hero_01_Normal.png'),('Hero_01_Iris','Hero_01_Iris.png')]:
 import shutil
 shutil.copy2(P.parent/file,P/file)
 im=bpy.data.images.get(name)
 if im:im.filepath='//'+file
scene=bpy.context.scene;scene.cycles.samples=64;scene.render.resolution_percentage=100;scene.render.filepath=str(P/'modular_preview.png');bpy.ops.render.render(write_still=True)
# Preview three example skin tones; default is restored before saving.
scene.cycles.samples=24;scene.render.resolution_percentage=45;strength.outputs[0].default_value=1
for h in ['F2C093','B77950','6A4030']:
 sethex(h);scene.render.filepath=str(P/('tint_'+h+'.png'));bpy.ops.render.render(write_still=True)
strength.outputs[0].default_value=0;sethex('F2C093');scene.cycles.samples=64;scene.render.resolution_percentage=100;scene.render.filepath=str(P/'modular_preview.png')
# Fixed names, one shared armature, no detached roots in assembled scene.
bpy.ops.object.select_all(action='DESELECT');rig.select_set(True);bpy.context.view_layer.objects.active=rig;bpy.ops.wm.save_as_mainfile(filepath=str(P/'Hero_01_Assembled.blend'))
report={'exports':spec,'bones':[b.name for b in rig.data.bones],'slots':{},'sockets':{},'skin_tint_examples':['#F2C093','#B77950','#6A4030']}
for o in C.objects:
 if o.type=='MESH':
  o.data.calc_loop_triangles();report['slots'][o.name]={'triangles':len(o.data.loop_triangles),'vertices':len(o.data.vertices),'materials':[m.name for m in o.data.materials]}
for n in ['Hat','Hand_R','Hand_L','Back','FaceExtra']:
 o=bpy.data.objects[n];report['sockets'][n]={'bone':o.parent_bone,'position':list(o.matrix_world.translation)}
(P/'modular_manifest.json').write_text(json.dumps(report,indent=2));print('MODULAR_EXPORT_DONE')
