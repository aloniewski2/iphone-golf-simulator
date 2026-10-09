import bpy,bmesh,math,pathlib,json,bisect
from mathutils import Vector
R=pathlib.Path.cwd();O=R/'work/reference-rebuild/characters';inputs=json.loads((O/'graft-inputs.json').read_text());allreports={}
def smo(a,b,x):
 t=max(0,min(1,(x-a)/(b-a)));return t*t*(3-2*t)
def ring_edges(bm,z):
 vs=[v for v in bm.verts if abs(v.co.z-z)<1e-5 and v.is_boundary];c=sum((v.co for v in vs),Vector())/len(vs);return sorted(vs,key=lambda v:math.atan2(v.co.y-c.y,v.co.x-c.x)),c
def angular(v,c):return math.atan2(v.co.y-c.y,v.co.x-c.x)
for sex in ('Male','Female'):
 ss=sex.lower();bpy.ops.wm.open_mainfile(filepath=str(O/f'{ss}_reference_expression2.blend'));sourceHead=next(o for o in bpy.data.objects if o.type=='MESH' and o.name.startswith('GEO-') and not any(x in o.name for x in ('.eye.','.iris.','.sclera.')));dg=bpy.context.evaluated_depsgraph_get();cached=[]
 for o in list(bpy.data.objects):
  if o.type!='MESH':continue
  if o!=sourceHead and not any(t in o.name for t in ('.iris.','.sclera.','.eye.','Eye_Iris_Candidate','Brow_Candidate')):continue
  for m in o.modifiers:
   if m.type=='SUBSURF':m.levels=0 if (o!=sourceHead or sex=='Male') else 1;m.render_levels=m.levels
  bpy.context.view_layer.update();me=bpy.data.meshes.new_from_object(o.evaluated_get(dg));me.transform(o.matrix_world);me.update();cached.append({'head':o==sourceHead,'name':o.name,'vertices':[v.co.copy() for v in me.vertices],'faces':[tuple(p.vertices) for p in me.polygons],'mi':[p.material_index for p in me.polygons],'mats':[(m.name,tuple(m.diffuse_color)) for m in me.materials]})
 bpy.ops.wm.open_mainfile(filepath=str(R/f'work/match-anim-set/export/HeroBase_{sex}_MatchAnims.blend'));rig=next(o for o in bpy.data.objects if o.type=='ARMATURE');rig.data.pose_position='REST';body=next(o for o in bpy.data.objects if o.type=='MESH' and o.name.startswith('Body_'));bpy.context.view_layer.update();assert max(abs(body.matrix_world[i][j]-(1 if i==j else 0)) for i in range(4) for j in range(4))<1e-6
 eye=Vector(inputs[sex]['eyeMidpointWorld']);scale=1.0 if sex=='Male' else .84;cut=inputs[sex]['bonePositions']['Neck']['head'][2]-.012;sourceCut=-.145 if sex=='Male' else -.160
 original={tuple(round(c,7) for c in v.co):[(body.vertex_groups[g.group].name,g.weight) for g in v.groups] for v in body.data.vertices if v.co.z<cut-1e-5}
 mats=[]
 def material(name):
  if name in mats:return mats.index(name)
  mats.append(name);m=bpy.data.materials.get(name)
  if not m:m=bpy.data.materials.new(name)
  return len(mats)-1
 skinName='ReferenceSkin';material(skinName);bpy.data.materials[skinName].use_nodes=True;p=bpy.data.materials[skinName].node_tree.nodes.get('Principled BSDF');p.inputs['Base Color'].default_value=(.44,.24,.12,1) if sex=='Male' else (.62,.36,.21,1);p.inputs['Roughness'].default_value=.52;p.inputs['Specular IOR Level'].default_value=.30
 bm=bmesh.new();bm.from_mesh(body.data);deform=bm.verts.layers.deform.verify();bmesh.ops.bisect_plane(bm,geom=list(bm.verts)+list(bm.edges)+list(bm.faces),dist=1e-7,plane_co=(0,0,cut),plane_no=(0,0,1),clear_outer=True,clear_inner=False);oldRing,oldCentre=ring_edges(bm,cut);oldAngles=[angular(v,oldCentre) for v in oldRing]
 groupHead=body.vertex_groups.get('Head') or body.vertex_groups.new(name='Head');groupNeck=body.vertex_groups.get('Neck') or body.vertex_groups.new(name='Neck')
 headCache=next(d for d in cached if d['head']);hm=bpy.data.meshes.new('AuthoredHead');hm.from_pydata(headCache['vertices'],[],headCache['faces']);hb=bmesh.new();hb.from_mesh(hm);bmesh.ops.bisect_plane(hb,geom=list(hb.verts)+list(hb.edges)+list(hb.faces),dist=1e-7,plane_co=(0,0,sourceCut),plane_no=(0,0,1),clear_inner=True,clear_outer=False);hRing,hCentre=ring_edges(hb,sourceCut)
 # Source head is complete anatomy; only a smooth neck transition is newly lofted.
 hmap={}
 for v in hb.verts:
  nv=bm.verts.new(eye+v.co*scale);mass=smo(sourceCut,-.105,v.co.z);nv[deform][groupHead.index]=mass;nv[deform][groupNeck.index]=1-mass;hmap[v]=nv
 for f in hb.faces:
  nf=bm.faces.new([hmap[v] for v in f.verts]);nf.material_index=0;nf.smooth=True
 newRing=[hmap[v] for v in hRing];newCentre=sum((v.co for v in newRing),Vector())/len(newRing);angles=[angular(v,newCentre) for v in newRing]
 # Map the upper ring's angular samples to the exact old neck polygon.
 def old_sample(a):
  ix=bisect.bisect_right(oldAngles,a)-1;j=(ix+1)%len(oldRing);a0=oldAngles[ix];a1=oldAngles[j]
  if j==0:a1+=math.tau
  if a<a0:a+=math.tau
  t=(a-a0)/(a1-a0);v0,v1=oldRing[ix],oldRing[j];co=v0.co.lerp(v1.co,t);weights={k:v0[deform].get(k,0)*(1-t)+v1[deform].get(k,0)*t for k in set(v0[deform].keys())|set(v1[deform].keys())};return co,weights
 loft=[]
 for t in (.25,.50,.75):
  ring=[]
  for v,a in zip(newRing,angles):
   lower,weights=old_sample(a);nv=bm.verts.new(lower.lerp(v.co,t));keys=set(weights)|{groupNeck.index,groupHead.index}
   for k in keys:nv[deform][k]=weights.get(k,0)*(1-t)+v[deform].get(k,0)*t
   ring.append(nv)
  loft.append(ring)
 # Zipper triangulation accommodates differing authored/canonical ring densities.
 upper=loft[0];ni,nj=len(oldRing),len(upper);i=j=0
 while i<ni or j<nj:
  ai=oldAngles[(i+1)%ni]+(math.tau if i+1>=ni else 0) if i<ni else 99;aj=angles[(j+1)%nj]+(math.tau if j+1>=nj else 0) if j<nj else 99
  if ai<=aj:f=bm.faces.new((oldRing[i%ni],oldRing[(i+1)%ni],upper[j%nj]));i+=1
  else:f=bm.faces.new((oldRing[i%ni],upper[(j+1)%nj],upper[j%nj]));j+=1
  f.material_index=0;f.smooth=True
 for lower,upper in zip(loft,loft[1:]+[newRing]):
  for i in range(len(lower)):
   j=(i+1)%len(lower);f=bm.faces.new((lower[i],lower[j],upper[j],upper[i]));f.material_index=0;f.smooth=True
 bm.normal_update();me=bpy.data.meshes.new('ReferenceBody_'+sex);bm.to_mesh(me);bm.free();hb.free();me.materials.append(bpy.data.materials[skinName]);groupNames=[g.name for g in body.vertex_groups];body.data=me
 for name in groupNames:body.vertex_groups.new(name=name)
 body.name='Body'
 for p in me.polygons:p.material_index=0
 # Exact original below-neck coordinates and all weights, including hands/feet.
 live={tuple(round(c,7) for c in v.co):[(body.vertex_groups[g.group].name,g.weight) for g in v.groups] for v in body.data.vertices if v.co.z<cut-1e-5};missing=[k for k in original if k not in live];weightError=max((max((abs(dict(live[k]).get(n,0)-w) for n,w in ws),default=0) for k,ws in original.items() if k in live),default=0);assert not missing and weightError<1e-6,(len(missing),weightError)
 # Features are one skin-bound object; all existing skeleton names/rest matrices remain unchanged.
 vertices=[];faces=[];roles=[];featureMats=[]
 for d in cached:
  if d['head']:continue
  offset=len(vertices);vertices.extend([eye+p*scale for p in d['vertices']]);mapping=[]
  for name,color in d['mats']:
   if name not in featureMats:featureMats.append(name)
   mapping.append(featureMats.index(name))
  for f,mi in zip(d['faces'],d['mi']):faces.append(tuple(offset+i for i in f));roles.append(mapping[mi])
 fm=bpy.data.meshes.new('ReferenceFeatures_'+sex);fm.from_pydata(vertices,[],faces)
 for name in featureMats:
  m=bpy.data.materials.get(name) or bpy.data.materials.new(name);m.use_nodes=True;n=m.node_tree.nodes.get('Principled BSDF');c=(.82,.84,.78) if 'Sclera' in name else ((.135,.053,.018) if 'Iris' in name else ((.004,.003,.002) if 'Pupil' in name else (.035,.018,.008)));n.inputs['Base Color'].default_value=(*c,1);n.inputs['Roughness'].default_value=.28 if ('Iris' in name or 'Sclera' in name) else (.15 if 'Pupil' in name else .7);fm.materials.append(m)
 for p,mi in zip(fm.polygons,roles):p.material_index=mi;p.use_smooth=True
 feat=bpy.data.objects.new('ReferenceFeatures',fm);bpy.context.scene.collection.objects.link(feat);feat.parent=rig;feat.vertex_groups.new(name='Head').add(list(range(len(fm.vertices))),1,'REPLACE');mod=feat.modifiers.new('Armature','ARMATURE');mod.object=rig
 for o in list(bpy.data.objects):
  if o not in (rig,body,feat):bpy.data.objects.remove(o,do_unlink=True)
 rig['ReferenceHead']=True;rig['ReferenceHeadSource']='Blender CC0 human-base-meshes-bundle-v1.4.1';body['ReferenceHead']=True;body['ReferenceNeckCutZ']=cut;body['ReferenceEyeMidpoint']=list(eye);body['ReferenceSourceScale']=scale;body['ReferenceFeatures']='ReferenceFeatures'
 # Editable complete canonical rig/source plus static pilot FBX for root's runtime swap.
 for o in bpy.context.scene.objects:o.select_set(o in (rig,body,feat))
 bpy.context.view_layer.objects.active=body;bpy.ops.wm.save_as_mainfile(filepath=str(O/f'{sex}_ReferenceBody.blend'));bpy.ops.export_scene.fbx(filepath=str(O/f'{sex}_ReferenceBody.fbx'),use_selection=True,object_types={'ARMATURE','MESH'},add_leaf_bones=False,bake_anim=False,use_mesh_modifiers=False,axis_forward='-Z',axis_up='Y')
 # Source portrait of the actual graft at the correct body registration.
 sc=bpy.context.scene;sc.use_nodes=False;sc.render.film_transparent=False;world=bpy.data.worlds.new('GraftStudio');world.use_nodes=True;world.node_tree.nodes['Background'].inputs[0].default_value=(.32,.36,.42,1);world.node_tree.nodes['Background'].inputs[1].default_value=.3;sc.world=world;target=eye+Vector((0,0,-.030))
 for name,delta,power,size in [('Key',(-.55,-.7,.75),25,1),('Fill',(.55,-.7,.2),18,1),('Rim',(.3,.35,.55),40,.75)]:
  ld=bpy.data.lights.new(name,'AREA');ld.energy=power;ld.size=size;lo=bpy.data.objects.new(name,ld);sc.collection.objects.link(lo);lo.location=target+Vector(delta);lo.rotation_euler=(target-lo.location).to_track_quat('-Z','Y').to_euler()
 cd=bpy.data.cameras.new('GraftPortrait');cam=bpy.data.objects.new('GraftPortrait',cd);sc.collection.objects.link(cam);sc.camera=cam;cd.type='ORTHO';cd.ortho_scale=.42;sc.render.engine='CYCLES';sc.cycles.device='CPU';sc.cycles.samples=32;sc.cycles.use_denoising=True;sc.render.threads_mode='FIXED';sc.render.threads=4;sc.render.resolution_x=600;sc.render.resolution_y=700;sc.render.resolution_percentage=100;sc.view_settings.view_transform='AgX';sc.view_settings.look='AgX - Medium High Contrast';sc.view_settings.exposure=-.6
 for angle,delta in [('front',(0,-1.1,.01)),('threequarter',(.58,-1.1,.025))]:
  cam.location=target+Vector(delta);cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();sc.render.filepath=str(O/f'{ss}_graft_{angle}.png');bpy.ops.render.render(write_still=True)
 me.calc_loop_triangles();fm.calc_loop_triangles();allreports[sex]={'body':'Body','features':'ReferenceFeatures','rig':rig.name,'boneCount':len(rig.data.bones),'bodyVertices':len(me.vertices),'bodyTriangles':len(me.loop_triangles),'featureTriangles':len(fm.loop_triangles),'bodyBindBasis':'identical originalBodymatrixWorld identity','eyeMidpointBlenderWorld':list(eye),'sourceScale':scale,'canonicalNeckCutZ':cut,'oldBoundaryVertices':len(oldRing),'newBoundaryVertices':len(newRing),'outsideNeckOriginalVertices':len(original),'outsideNeckMissing':len(missing),'outsideNeckWeightMaxError':weightError,'morphs':'expression2 baked into pilot; authoredBlink/Smile pending look acceptance','featuresAllHeadWeight':True,'bypass':['HeroHeadVolume','HeroSkinNormals','HeroFaceCraft','HeroEyeRim','HeroActualLidFit','oldFaceRenderer']};(O/'graft-manifest.json').write_text(json.dumps(allreports,indent=2));print('GRAFT',sex,allreports[sex])
