"""Export the same fitted Tripo meshes for Unity and the native character editor."""
import bpy, json, math, shutil, sys
import numpy as np
from pathlib import Path
from mathutils import Vector, Matrix
R=Path(__file__).resolve().parents[2]
U=R/'Unity/Assets/Resources/Tennis/Customization'; N=R/'GolfArcade/Unity/CharacterAssets'
U.mkdir(parents=True,exist_ok=True); N.mkdir(parents=True,exist_ok=True)

def reset(): bpy.ops.wm.read_factory_settings(use_empty=True)
def load(path): bpy.ops.import_scene.fbx(filepath=str(path))
def save_json(name,data):
 text=json.dumps(data,separators=(',',':'))
 for dest in ([N] if name.startswith('CustomBody') else [U,N]): (dest/(name+'.json')).write_text(text)
def image_file(img,name):
 if 'Mask' in name:
  w,h=img.size; raw=np.array(img.pixels[:]).reshape(h,w,4)[::-1]
  (N/(name+'.mask')).write_bytes((np.clip(raw,0,1)*255).astype(np.uint8).tobytes())
 img.filepath_raw=str(U/(name+'.png')); img.file_format='PNG'; img.save(); shutil.copyfile(U/(name+'.png'),N/(name+'.png'))
def emit(obj,name,texture='',mask='',reference=None,cut=False):
 me=obj.data; me.calc_loop_triangles(); p=[]; n=[]; uv=[]; ix=[]
 normals=obj.matrix_world.to_3x3().inverted().transposed()
 groups={g.index:g.name for g in obj.vertex_groups}
 def head(v): return sum(g.weight for g in v.groups if groups.get(g.group)=='Head')>.5
 for t in me.loop_triangles:
  if cut and sum(head(me.vertices[i]) for i in t.vertices)>=2: continue
  for vi,li in zip(t.vertices,t.loops):
   v=obj.matrix_world@me.vertices[vi].co; no=normals@me.vertices[vi].normal; no.normalize()
   p.extend([round(v.x,6),round(v.z,6),round(-v.y,6)])
   n.extend([round(no.x,5),round(no.z,5),round(-no.y,5)])
   u=me.uv_layers.active.data[li].uv if me.uv_layers.active else (0,0)
   uv.extend([round(u[0],6),round(u[1],6)]); ix.append(len(ix))
 save_json(name,dict(positions=p,normals=n,uv=uv,triangles=ix,texture=texture,mask=mask,reference=reference or [.08,.08,.28,.27]))
 print('EXPORTED',name,len(ix)//3)

for who,suffix in [('Avatar','M'),('AvatarF','F')]:
 reset();load(R/f'Unity/Assets/Resources/Tennis/Opponents/{who}.fbx')
 obj=next(o for o in bpy.data.objects if o.type=='MESH' and o.name.startswith('V4 Higgs body'))
 tex='Higgs'+who+'_Color'; mask='Higgs'+who+'_Mask'
 for fn in [tex,mask]:
  img=bpy.data.images.load(str(R/f'Unity/Assets/Resources/Tennis/Characters/{fn}.png'))
  img.scale(512,512); image_file(img,fn)
 ref=json.loads((R/f'Unity/Assets/Resources/Tennis/Characters/Higgs{who}_Mask.json').read_text())
 emit(obj,'CustomBody'+suffix,tex,mask,[ref[k] for k in ['shirt','shorts','accent','skin']],True)

for name in ['head','quiff','curls','bob']:
 files=list((R/f'SportsLibrary/Customization/Tripo/{name}').rglob('model.fbx'))
 if not files: print('PENDING',name); continue
 reset();load(files[0]);objects=[o for o in bpy.data.objects if o.type=='MESH']
 # Tripo defaults to +X forward, Blender character convention is -Y forward.
 rot=Matrix.Rotation(-math.pi/2,4,'Z')
 for o in objects:
  o.data.transform(rot@o.matrix_world);o.matrix_world=Matrix.Identity(4)
 bpy.ops.object.select_all(action='DESELECT')
 for o in objects:o.select_set(True)
 bpy.context.view_layer.objects.active=objects[0]
 if len(objects)>1:bpy.ops.object.join()
 o=bpy.context.object; vs=o.data.vertices
 lo=Vector(tuple(min(v.co[i] for v in vs) for i in range(3)));hi=Vector(tuple(max(v.co[i] for v in vs) for i in range(3)))
 dimensions={'head':(.43,.43,.54),'quiff':(.48,.46,.25),'curls':(.53,.49,.32),'bob':(.50,.48,.46)}[name]
 bottom={'head':1.14,'quiff':1.48,'curls':1.44,'bob':1.23}[name]
 for v in vs:
  q=(v.co-lo);v.co=Vector(((q.x/(hi.x-lo.x)-.5)*dimensions[0],(q.y/(hi.y-lo.y)-.5)*dimensions[1]-.11,q.z/(hi.z-lo.z)*dimensions[2]+bottom-1.22))
 o.data.update()
 tex='Custom'+name.capitalize(); mask=''
 if name=='head':
  images=[node.image for mat in o.data.materials if mat and mat.use_nodes for node in mat.node_tree.nodes if node.type=='TEX_IMAGE' and node.image]
  image=next((im for im in images if 'normal' not in im.name.lower()),None)
  if image:
   image.scale(512,512); image_file(image,tex)
   pixels=np.array(image.pixels[:]).reshape(-1,4);rgb=pixels[:,:3]
   skin=(rgb[:,0]>rgb[:,2]*1.15)&(rgb[:,0]>rgb[:,1]*1.03)&(rgb[:,0]>.15)&((rgb.max(1)-rgb.min(1))>.07)
   data=np.zeros_like(pixels);data[:,3]=skin.astype(float)
   mask=tex+'Mask';im=bpy.data.images.new(mask,512,512,alpha=True);im.pixels[:]=data.ravel().tolist();image_file(im,mask)
  else:tex=''
 else:tex=''
 emit(o,'Custom'+name.capitalize(),tex,mask)
 bpy.ops.wm.save_as_mainfile(filepath=str(R/f'SportsLibrary/Customization/{name}-fitted.blend'))
