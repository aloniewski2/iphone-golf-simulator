"""Extract supplied character sheets, optimize and bind the player bases to the shared sports rig.
Run Blender --background --python blender/scripts/export_player_bases.py.
The source sheets are preserved; the first front-facing body and the separate hand are used.
"""
import bpy,bmesh,json,math,shutil,sys
sys.path.insert(0,str(__import__("pathlib").Path(__file__).resolve().parent))
from build_sportswear import rebuild
import numpy as np
from pathlib import Path
from mathutils import Vector,Matrix
R=Path(__file__).resolve().parents[2];U=R/'Unity/Assets/Resources/Tennis/Customization';N=R/'GolfArcade/Unity/CharacterAssets';S=R/'SportsLibrary/Customization/Bases'

def crop(source,name,predicate,faces):
 o=source.copy();o.data=source.data.copy();bpy.context.collection.objects.link(o);o.name=name
 bm=bmesh.new();bm.from_mesh(o.data);bmesh.ops.delete(bm,geom=[v for v in bm.verts if not predicate(v.co)],context='VERTS');bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.000002);bm.to_mesh(o.data);bm.free()
 bpy.context.view_layer.objects.active=o
 d=o.modifiers.new('Mobile topology','DECIMATE');d.ratio=min(1,faces/max(1,len(o.data.polygons)));bpy.ops.object.modifier_apply(modifier=d.name)
 for p in o.data.polygons:p.use_smooth=True
 return o

def emit(o,name,material_index,tex='',mask='',ref=.7):
 me=o.data;me.calc_loop_triangles();p=[];n=[];uv=[];ix=[]
 for t in me.loop_triangles:
  if t.material_index!=material_index:continue
  for vi,li in zip(t.vertices,t.loops):
   v=me.vertices[vi].co;no=me.vertices[vi].normal
   p.extend([round(v.x,6),round(v.z,6),round(-v.y,6)]);n.extend([round(no.x,5),round(no.z,5),round(-no.y,5)])
   q=me.uv_layers.active.data[li].uv;uv.extend([round(q.x,6),round(q.y,6)]);ix.append(len(ix))
 if ix:
  data=dict(positions=p,normals=n,uv=uv,triangles=ix,texture=tex,mask=mask,reference=[.4,.2,.6,ref])
  if me.shape_keys:
   for label in ['Slim','Broad']:
    key=me.shape_keys.key_blocks[label];points=[]
    for t in me.loop_triangles:
     if t.material_index!=material_index:continue
     for vi in t.vertices:
      v=key.data[vi].co;points.extend([round(v.x,6),round(v.z,6),round(-v.y,6)])
    data['positions'+label]=points
  (N/(name+'.json')).write_text(json.dumps(data,separators=(',',':')))

for sex in ['male','female']:
 bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.gltf(filepath=str(S/f'{sex}-source.glb'))
 source=next(o for o in bpy.data.objects if o.type=='MESH');source.data.transform(source.matrix_world);source.matrix_world=Matrix.Identity(4)
 body=crop(source,'V4 Higgs body Player'+sex.title(),lambda p:p.x<-.33 and p.z>.28,12000)
 hand=crop(source,'Extracted hand',lambda p:-.235<p.x<-.06 and p.z<.24,1800)
 # Keep the largest connected hand component (the crop can catch the edge of a neighbouring sheet view).
 bm=bmesh.new();bm.from_mesh(hand.data);todo=set(bm.verts);groups=[]
 while todo:
  seed=todo.pop();part={seed};stack=[seed]
  while stack:
   vertex=stack.pop()
   for edge in vertex.link_edges:
    neighbor=edge.other_vert(vertex)
    if neighbor in todo:todo.remove(neighbor);part.add(neighbor);stack.append(neighbor)
  groups.append(part)
 keep=max(groups,key=len);bmesh.ops.delete(bm,geom=[v for part in groups if part is not keep for v in part],context='VERTS');bm.to_mesh(hand.data);bm.free()
 # Fit the complete source silhouette; preserve the face instead of replacing it.
 a=np.array([v.co[:] for v in body.data.vertices]);lo=a.min(0);hi=a.max(0);mid=(lo+hi)/2;h=hi[2]-lo[2]
 for v in body.data.vertices:
  q=(np.array(v.co[:])-lo)/h
  z=float(np.interp(q[2],[0,.12,.31,.48,.64,.72,1],[0,.12,.36,.64,1.08,1.22,1.75]))
  v.co=((v.co.x-mid[0])/h*1.68,(v.co.y-mid[1])/h*1.48,z)
 # Split triangles at garment hems so the colour boundary is a clean continuous edge.
 bm=bmesh.new();bm.from_mesh(body.data)
 for z in [.105,.42,.69,1.13]:
  bmesh.ops.bisect_plane(bm,geom=list(bm.verts)+list(bm.edges)+list(bm.faces),dist=.00001,plane_co=(0,0,z),plane_no=(0,0,1),clear_inner=False,clear_outer=False)
 bm.to_mesh(body.data);bm.free()
 # Skin texture plus a mask that leaves brows, eyes, and mouth intact.
 srcmat=body.data.materials[0];im=next(n.image for n in srcmat.node_tree.nodes if n.type=='TEX_IMAGE' and n.image and 'normal' not in n.image.name.lower())
 # Give the retained face the full texture resolution instead of a tiny area of the six-view sheet.
 head_uv=[body.data.uv_layers.active.data[li].uv[:] for p in body.data.polygons for li in p.loop_indices if body.data.vertices[body.data.loops[li].vertex_index].co.z>1.19]
 limits=np.array(head_uv);uvlo=np.maximum(0,limits.min(0)-.003);uvhi=np.minimum(1,limits.max(0)+.003)
 iw,ih=im.size; x0,y0=np.floor(uvlo*[iw,ih]).astype(int);x1,y1=np.ceil(uvhi*[iw,ih]).astype(int)
 uvlo=np.array([x0/iw,y0/ih]);uvhi=np.array([x1/iw,y1/ih])
 patch=np.array(im.pixels[:]).reshape(ih,iw,4)[y0:y1,x0:x1].copy()
 faceim=bpy.data.images.new('Dedicated player face',x1-x0,y1-y0,alpha=True);faceim.pixels[:]=patch.ravel().tolist();im=faceim;im.scale(512,512)
 for loop in body.data.uv_layers.active.data:loop.uv=(np.array(loop.uv[:])-uvlo)/(uvhi-uvlo)
 pixels=np.array(im.pixels[:]).reshape(-1,4);rgb=pixels[:,:3]
 skin=(rgb[:,0]>.18)&(rgb[:,0]>rgb[:,2]*1.12)&(rgb[:,0]>rgb[:,1]*1.02)
 mask=np.zeros_like(pixels);mask[:,3]=skin
 base='Player'+sex.title();im.filepath_raw=str(U/(base+'Color.png'));im.file_format='PNG';im.save();shutil.copyfile(U/(base+'Color.png'),N/(base+'Color.png'))
 mi=bpy.data.images.new(base+'Mask',512,512,alpha=True);mi.pixels[:]=mask.ravel().tolist();mi.filepath_raw=str(U/(base+'Mask.png'));mi.file_format='PNG';mi.save();shutil.copyfile(U/(base+'Mask.png'),N/(base+'Mask.png'));(N/(base+'Mask.mask')).write_bytes((mask.reshape(512,512,4)[::-1]*255).astype(np.uint8).tobytes())
 ref=float(np.mean(rgb[skin]@np.array([.2126,.7152,.0722])))
 (U/(base+'Mask.json')).write_text(json.dumps(dict(shirt=.4,shorts=.2,accent=.6,skin=ref)))
 mats=[]
 for label,color in [('skin',(1,1,1,1)),('top',(.12,.48,.68,1)),('bottom',(.035,.08,.17,1)),('shoes',(.9,.93,.95,1)),('limb',(.72,.43,.25,1)),('trim',(.94,.95,.91,1))]:
  mat=bpy.data.materials.new('Player '+label);mat.diffuse_color=color;mat.use_nodes=True;bs=mat.node_tree.nodes.get('Principled BSDF');bs.inputs['Base Color'].default_value=color;bs.inputs['Roughness'].default_value=.7
  if label=='skin':node=mat.node_tree.nodes.new('ShaderNodeTexImage');node.image=im;mat.node_tree.links.new(node.outputs['Color'],bs.inputs['Base Color'])
  mats.append(mat)
 body.data.materials.clear()
 for m in mats:body.data.materials.append(m)
 for p in body.data.polygons:
  z=sum(body.data.vertices[i].co.z for i in p.vertices)/len(p.vertices)
  p.material_index=1 if .69<z<1.13 else 2 if .42<z<=.69 else 3 if z<.105 else 0
 # Sports skeleton remains unchanged: clips and racket/club contact markers keep their timing.
 existing=set(bpy.data.objects);bpy.ops.import_scene.fbx(filepath=str(R/f'Unity/Assets/Resources/StandardCharacters/standard_{sex}_tennis.fbx'))
 rig=next(o for o in bpy.data.objects if o not in existing and o.type=='ARMATURE');rig.animation_data_clear();rig.data.pose_position='REST'
 for o in list(bpy.data.objects):
  if o not in existing and o is not rig:bpy.data.objects.remove(o,do_unlink=True)
 groups={b.name:body.vertex_groups.new(name=b.name) for b in rig.data.bones}
 for v in body.data.vertices:
  z=v.co.z;side='L' if v.co.x>0 else 'R'
  if z>=1.22:weights={'Head':1}
  elif z>1.12:t=(z-1.12)/.10;weights={'Head':t,'Chest':1-t}
  elif z>.76:t=min(1,(z-.76)/.24);weights={'Spine':1-t,'Chest':t}
  elif z>.60:t=(z-.60)/.16;weights={'Hips':1-t,'Spine':t}
  elif z>.41:t=min(1,(z-.41)/.23);weights={'UpperLeg.'+side:1-t,'Hips':t}
  elif z>.31:t=(z-.31)/.10;weights={'UpperLeg.'+side:t,'LowerLeg.'+side:1-t}
  elif z>.16:weights={'LowerLeg.'+side:1}
  elif z>.09:t=(z-.09)/.07;weights={'LowerLeg.'+side:t,'Foot.'+side:1-t}
  else:weights={'Foot.'+side:1}
  # Keep the connected pelvis on the hips; a hard L/R split here tears the shorts
  # when the golf legs rotate in opposite directions.
  if .31<z<.64:
   center=max(0,1-abs(v.co.x)/.12)*.9*max(0,min(1,(z-.31)/.16))
   weights={k:w*(1-center) for k,w in weights.items()}
   weights['Hips']=weights.get('Hips',0)+center
  for key,w in weights.items():
   if w>0:groups[key].add([v.index],w,'REPLACE')
 # Smooth weights over connected topology around pelvis and knees; preserve the rigid head.
 names=list(groups)
 weights=np.zeros((len(body.data.vertices),len(names)))
 for v in body.data.vertices:
  for g in v.groups:weights[v.index,g.group]=g.weight
 adjacent=[[] for v in body.data.vertices]
 for e in body.data.edges:
  a,b=e.vertices;adjacent[a].append(b);adjacent[b].append(a)
 movable=[v.index for v in body.data.vertices if .18<v.co.z<.80]
 for iteration in range(12):
  updated=weights.copy()
  for i in movable:
   if adjacent[i]:updated[i]=weights[i]*.35+weights[adjacent[i]].mean(axis=0)*.65
  weights=updated
 for i in movable:
  for vg in body.vertex_groups:vg.remove([i])
  values=weights[i];keep=values.argsort()[-4:];total=values[keep].sum()
  for j in keep:
   if values[j]>.0001:groups[names[j]].add([i],float(values[j]/total),'REPLACE')
 mod=body.modifiers.new('Sports rig','ARMATURE');mod.object=rig
 rebuild(body,rig,mats)
 # The source hand is an open neutral hand. Bind it rigidly to each existing wrist socket.
 a=np.array([v.co[:] for v in hand.data.vertices]);lo=a.min(0);hi=a.max(0);mid=(lo+hi)/2
 for v in hand.data.vertices:
  v.co=((v.co.x-mid[0])/(hi[0]-lo[0])*.105,(v.co.y-mid[1])/(hi[1]-lo[1])*.072,(v.co.z-mid[2])/(hi[2]-lo[2])*.16)
 hands=[]
 for side,sign in [('L',1),('R',-1)]:
  o=hand.copy();o.data=hand.data.copy();bpy.context.collection.objects.link(o);o.name='V4 grip hand '+side+' Player';o.data.materials.clear();o.data.materials.append(mats[4]);b=rig.data.bones['Hand.'+side]
  center=rig.matrix_world@b.head_local+Vector((sign*.006,-.027,-.055))
  for v in o.data.vertices:v.co=Vector((v.co.x*sign,v.co.y,v.co.z))+center
  vg=o.vertex_groups.new(name='Hand.'+side);vg.add(list(range(len(o.data.vertices))),1,'REPLACE');mod=o.modifiers.new('Sports rig','ARMATURE');mod.object=rig;hands.append(o)
 for i,label in enumerate(['Skin','Top','Bottom','Shoes','Limb','Trim']):emit(body,base+label,i,base+'Color' if i==0 else '',base+'Mask' if i==0 else '',ref)
 for o,side in zip(hands,['L','R']):emit(o,base+'Hand'+side,0,'','',ref)
 bpy.data.objects.remove(source,do_unlink=True);bpy.data.objects.remove(hand,do_unlink=True)
 bpy.ops.object.select_all(action='DESELECT')
 for o in [rig,body]+hands:o.select_set(True)
 bpy.context.view_layer.objects.active=rig
 bpy.ops.export_scene.fbx(filepath=str(U/(base+'.fbx')),use_selection=True,object_types={'ARMATURE','MESH'},apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',axis_forward='-Z',axis_up='Y',add_leaf_bones=False,armature_nodetype='NULL',bake_anim=False,path_mode='AUTO')
 bpy.ops.wm.save_as_mainfile(filepath=str(S/(base+'-rigged.blend')))
 print('BASE EXPORTED',base,len(body.data.polygons),'triangles',ref,flush=True)
