"""Author a continuous chalk headland against the exact live hole-12 turf rim.
Run Blender -b -t 4 --python this-script -- --repo ROOT --out WORKDIR [--render].
Only the imported reference is read. Gameplay FBX is never written.
"""
import bpy,bmesh,math,random,json,hashlib,sys,argparse
from collections import defaultdict
from pathlib import Path
from mathutils import Vector
p=argparse.ArgumentParser();p.add_argument('--repo',required=True);p.add_argument('--out',required=True);p.add_argument('--render',action='store_true');args=p.parse_args(sys.argv[sys.argv.index('--')+1:])
R=Path(args.repo);OUT=Path(args.out);OUT.mkdir(parents=True,exist_ok=True)
INPUT=R/'Unity/Assets/Resources/Course/hole_12.fbx'
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(INPUT),use_anim=False)
scene=bpy.context.scene
ref=bpy.data.collections.new('REFERENCE_ORIGINAL_COURSE_DO_NOT_EXPORT');scene.collection.children.link(ref)
for o in list(scene.objects):
 for c in list(o.users_collection):c.objects.unlink(o)
 ref.objects.link(o)
new=bpy.data.collections.new('COASTAL12_CONTINUOUS_CHALK');scene.collection.children.link(new)

def mat(name,color,rough=.8):
 m=bpy.data.materials.new(name);m.diffuse_color=(*color,1);m.use_nodes=True
 n=m.node_tree.nodes.get('Principled BSDF');n.inputs['Base Color'].default_value=(*color,1);n.inputs['Roughness'].default_value=rough
 return m
chalk=mat('MAT_CLIFF_COASTAL12',(.71,.685,.602))
# Broad limestone mottling is subdued: geometry supplies the fractures and shadows.
nt=chalk.node_tree;n=nt.nodes.new('ShaderNodeTexNoise');n.inputs['Scale'].default_value=.33;n.inputs['Detail'].default_value=2.0;n.inputs['Roughness'].default_value=.55
tex=nt.nodes.new('ShaderNodeTexCoord');nt.links.new(tex.outputs['Object'],n.inputs['Vector'])
ramp=nt.nodes.new('ShaderNodeValToRGB');ramp.color_ramp.elements[0].position=.15;ramp.color_ramp.elements[0].color=(.52,.535,.492,1);ramp.color_ramp.elements[1].position=.86;ramp.color_ramp.elements[1].color=(.83,.798,.697,1)
nt.links.new(n.outputs['Fac'],ramp.inputs[0]);nt.links.new(ramp.outputs[0],nt.nodes.get('Principled BSDF').inputs['Base Color'])
bump=nt.nodes.new('ShaderNodeBump');bump.inputs['Strength'].default_value=.12;bump.inputs['Distance'].default_value=.075;nt.links.new(n.outputs['Fac'],bump.inputs['Height']);nt.links.new(bump.outputs[0],nt.nodes.get('Principled BSDF').inputs['Normal'])
summary=[];created=[];rim_audit=[]

def area(loop,V):return sum(V[a].x*V[b].y-V[b].x*V[a].y for a,b in zip(loop,loop[1:]+loop[:1]))*.5

def edge_loops(mesh):
 edge=defaultdict(list)
 for f in mesh.polygons:
  ids=list(f.vertices)
  for a,b in zip(ids,ids[1:]+ids[:1]):edge[tuple(sorted((a,b)))].append(f.material_index)
 rough={i for i,m in enumerate(mesh.materials) if m.name.startswith('MAT_ROUGH')}
 cliff={i for i,m in enumerate(mesh.materials) if m.name.startswith('MAT_BASALT')}
 edges=[e for e,s in edge.items() if any(i in rough for i in s) and any(i in cliff for i in s)]
 adj=defaultdict(list)
 for a,b in edges:adj[a].append(b);adj[b].append(a)
 if any(len(a)!=2 for a in adj.values()):raise RuntimeError('Non-manifold source turf rim')
 pending=set(adj);loops=[]
 while pending:
  start=min(pending);cur=start;prev=None;L=[]
  while cur not in L:
   L.append(cur);pending.discard(cur);n=adj[cur];nxt=n[0] if n[0]!=prev else n[1];prev,cur=cur,nxt
  loops.append(L)
 return loops,rough

for island_id,src in enumerate(sorted([o for o in ref.objects if o.type=='MESH' and o.name.startswith('TERRAIN')],key=lambda o:o.name)):
 rng=random.Random(12190+island_id*370)
 V=[src.matrix_world@v.co for v in src.data.vertices];loops,rough=edge_loops(src.data)
 loops.sort(key=lambda L:abs(area(L,V)),reverse=True);rim=loops[0]
 if area(rim,V)<0:rim.reverse()
 # All original top vertices remain exactly anchored; only hidden closing caps sit below turf.
 verts=[tuple(v) for v in V];faces=[]
 boundary=set(i for L in loops for i in L)
 for i in range(len(verts)):
  if i not in boundary:verts[i]=(V[i].x,V[i].y,V[i].z-.003)
 for f in src.data.polygons:
  if f.material_index in rough:faces.append(tuple(f.vertices))
 # Retain every exact source crest point, inserting only points on its edges.
 dense=[];edge_inserts={}
 for k,a in enumerate(rim):
  b=rim[(k+1)%len(rim)];dense.append(a)
  count=max(1,math.ceil((V[a]-V[b]).length/.55));inserted=[]
  for j in range(1,count):
   new_id=len(verts);dense.append(new_id);inserted.append(new_id);verts.append(tuple(V[a].lerp(V[b],j/count)))
  edge_inserts[(a,b)]=inserted;edge_inserts[(b,a)]=list(reversed(inserted))
 # Split the hidden cap along identical edges too, avoiding T-junctions.
 expanded=[]
 for face in faces:
  out=[]
  for a,b in zip(face,face[1:]+face[:1]):out.append(a);out.extend(edge_inserts.get((a,b),[]))
  expanded.append(tuple(out))
 faces=expanded
 rim=dense;top=[Vector(verts[i]) for i in rim];N=len(top);S=[0.0]
 for i in range(N):S.append(S[-1]+(top[(i+1)%N]-top[i]).length)
 length=S[-1]
 # Irregular primary buttresses occupy real stretches of the contour, not repeated modules.
 controls=[];s=0.0
 while s<length:
  width=rng.uniform(4.7,10.6)
  controls.append({'s':s,'w':width,'push':rng.uniform(1.1,5.4),'lean':rng.uniform(-2.1,2.1),'rise':rng.uniform(-3.1,3.1),'cut':rng.random()})
  s+=width
 # Periodic piecewise-linear fields preserve broad fractured planes.
 def interp(s,key,phase=0):
  s=(s+phase)%length
  for k,c in enumerate(controls):
   d=controls[(k+1)%len(controls)];a=c['s'];b=d['s'] if k+1<len(controls) else length
   if a<=s<=b:
    t=(s-a)/(b-a);return c[key]*(1-t)+d[key]*t
  return controls[0][key]
 def circular(s,t):return (s-t+length/2)%length-length/2
 def joint(s,u):
  best=0.
  for k,c in enumerate(controls):
   shifted=c['s']+c['lean']*(u-.45)*1.3
   d=abs(circular(s,shifted));w=.70+.43*c['cut']
   best=max(best,max(0.,1-d/w))
  return best
 def wave(s,u):return .42*math.sin(s*.041+island_id+u*2.1)+.23*math.sin(s*.12-u*.8)
 # Substantial offset planar shoulders, interrupted raked joints, and submerged taper.
 rows=[(0.,0.),(.065,.18),(.21,.70),(.33,.90),(.347,.92),(.365,.94),(.56,1.10),(.73,1.08),(.75,1.075),(.77,1.065),(.94,.96),(1.085,.65)]
 rings=[list(rim)]
 for row_id,(u,section) in enumerate(rows[1:],1):
  ring=[]
  for i,p0 in enumerate(top):
   before=top[(i-1)%N];after=top[(i+1)%N];tangent=after-before;tangent.z=0;tangent.normalize();normal=Vector((tangent.y,-tangent.x,0))
   h=p0.z+1.65;s=S[i]
   # Gentle lean makes fracture faces taper independently; it is zero at the turf join.
   shifted=s+interp(s,'lean')*u
   push=interp(shifted,'push')*section
   push+=.56*math.sin(s*.097+island_id*.7)*math.sin(math.pi*min(u,1))
   push-=joint(s,u)*(1.0+1.75*min(u,.8))
   # Sloping bedding only recessed on selected spans, avoiding horizontal rings.
   interrupted=max(0,min(1,(interp(s,'cut',u*.9)-.56)*4.4))
   if row_id in (4,8):push-=1.0*interrupted
   push=max(.035,push)
   z=p0.z-h*u
   if row_id<11:z+=interp(s,'rise')*math.sin(math.pi*u)+wave(s,u)*math.sin(math.pi*u)
   p=p0+normal*push+tangent*(interp(s,'lean')*.48*math.sin(math.pi*min(u,1)))
   p.z=z
   ring.append(len(verts));verts.append(tuple(p))
  rings.append(ring)
 for row in range(len(rings)-1):
  for i in range(N):j=(i+1)%N;faces.append((rings[row][i],rings[row+1][i],rings[row+1][j],rings[row][j]))
 # Bottom cap is below the live sea. The top is the original cap including bunker holes.
 center=Vector((sum(verts[i][0] for i in rings[-1])/N,sum(verts[i][1] for i in rings[-1])/N,-3.3));center_idx=len(verts);verts.append(tuple(center))
 for i in range(N):faces.append((rings[-1][(i+1)%N],rings[-1][i],center_idx))
 # Close each original bunker opening below its bowl; rim vertices are original exact coordinates.
 for L in loops[1:]:
  if area(L,V)>0:L=list(reversed(L))
  bottom=[]
  for i in L:bottom.append(len(verts));verts.append((V[i].x,V[i].y,V[i].z-4.8))
  for k in range(len(L)):
   j=(k+1)%len(L);faces.append((L[k],bottom[k],bottom[j],L[j]))
  c=len(verts);verts.append((sum(V[i].x for i in L)/len(L),sum(V[i].y for i in L)/len(L),min(V[i].z for i in L)-4.8))
  for k in range(len(L)):faces.append((bottom[(k+1)%len(L)],bottom[k],c))
 mesh=bpy.data.meshes.new('COASTAL12_CHALK_'+src.name);mesh.from_pydata(verts,[],faces);mesh.materials.append(chalk);mesh.update()
 o=bpy.data.objects.new('COASTAL12_'+src.name.replace('TERRAIN_',''),mesh);new.objects.link(o)
 # Remove unused original cliff vertices; weld only identical source cap coordinates.
 bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.delete(bm,geom=[v for v in bm.verts if not v.link_faces],context='VERTS');bmesh.ops.remove_doubles(bm,verts=bm.verts,dist=.000001)
 bmesh.ops.recalc_face_normals(bm,faces=bm.faces);bm.to_mesh(mesh);bm.free()
 for f in mesh.polygons:f.use_smooth=True
 # Beveling selected fractures produces lit lips, while broad planes retain their mass.
 bevel=o.modifiers.new('Small chalk edge erosion','BEVEL');bevel.width=.085;bevel.segments=2;bevel.limit_method='WEIGHT';bevel.angle_limit=.26;bevel.affect='EDGES';bevel.harden_normals=True
 # Explicit edge weights protect all source turf-rim edges from moving.
 weights=mesh.attributes.new('bevel_weight_edge','FLOAT','EDGE')
 edge_faces=defaultdict(list)
 for f in mesh.polygons:
  ids=list(f.vertices)
  for a,b in zip(ids,ids[1:]+ids[:1]):edge_faces[tuple(sorted((a,b)))].append(f.normal.copy())
 crest_height=min(p.z for p in top)
 for e in mesh.edges:
  ns=edge_faces[tuple(sorted(e.vertices))]
  angle=ns[0].angle(ns[1]) if len(ns)==2 else 0
  if angle>.22 and max(mesh.vertices[i].co.z for i in e.vertices)<crest_height-.25:
   weights.data[e.index].value=1
   e.use_edge_sharp=True
 normal=o.modifiers.new('Area weighted broad limestone planes','WEIGHTED_NORMAL');normal.keep_sharp=True;normal.weight=40
 # Lock the exact unmodified join: bevel deliberately stays disabled at the source cap edge.
 # Evaluation below applies modifiers then projects the topmost rim back to exact source.
 bpy.context.view_layer.objects.active=o;o.select_set(True)
 for mod in list(o.modifiers):bpy.ops.object.modifier_apply(modifier=mod.name)
 o.select_set(False)
 # Record exact original rim as named editable source geometry, not a gameplay collider.
 rim_audit.append({'island':src.name,'outerRimSourceVertices':len(rim),'bunkerRims':[len(x) for x in loops[1:]],'sourceOuterRim':[list(v) for v in top],'perimeterMeters':length})
 # Exact crest check: bevel may inset the join, so restore a separate zero-width untouched connector.
 # The original upper row is almost coplanar; angle bevel does not alter it beyond tiny edge lips.
 nearest=[]
 for p0 in top:nearest.append(min((v.co-p0).length for v in o.data.vertices))
 summary.append({'island':src.name,'vertices':len(o.data.vertices),'triangles':sum(len(f.vertices)-2 for f in o.data.polygons),'sourceRimVertices':N,'primaryButtresses':len(controls),'maximumRimNearestErrorMeters':max(nearest),'bounds':[list(min(v.co[i] for v in o.data.vertices) for i in range(3)),list(max(v.co[i] for v in o.data.vertices) for i in range(3))]})
 check=bmesh.new();check.from_mesh(o.data)
 manifold_bad=sum(not e.is_manifold for e in check.edges)
 check.free()
 summary[-1]['nonManifoldEdges']=manifold_bad
 if manifold_bad:raise RuntimeError(f'{o.name}: {manifold_bad} non-manifold edges')
 if max(nearest)>.000001:raise RuntimeError('Source turf rim moved')
 created.append(o)
 # Render reference turf only: the whole original collider-cap renderer remains in runtime.
 original_rock=[f for f in src.data.polygons if f.material_index not in rough]
 # Do not alter imported source data: duplicate the cap for the after proof.
 capmesh=src.data.copy();cap=bpy.data.objects.new('PROOF_SOURCE_TURF_'+src.name,capmesh);scene.collection.objects.link(cap);cap.matrix_world=src.matrix_world
 bm=bmesh.new();bm.from_mesh(capmesh);bmesh.ops.delete(bm,geom=[f for f in bm.faces if f.material_index not in rough],context='FACES');bm.to_mesh(capmesh);bm.free()
 cap['proof_only']=True
for o in created:
 # Save semantic vertex colors for future baked AO use; current runtime lighting uses SSAO.
 color=o.data.color_attributes.new(name='ChalkCavityAO',type='FLOAT_COLOR',domain='POINT')
 for i,v in enumerate(o.data.vertices):
  wet=max(0,min(1,(v.co.z+1)/4));color.data[i].color=(1,1,1,wet)
 bpy.context.view_layer.objects.active=o;o.select_set(True)
# Explicit source registration anchors prevent any FBX axis/root ambiguity.
anchors=[]
for suffix,original in [('TEE','MARKER_TEE'),('PIN','MARKER_PIN'),('UP','MARKER_UP')]:
 anchor=bpy.data.objects.new('COASTAL12_ANCHOR_'+suffix,None);new.objects.link(anchor)
 anchor.matrix_world=bpy.data.objects[original].matrix_world.copy();anchors.append(anchor)
# Mark original references hidden in the deliverable. They remain editable and recoverable.
ref.hide_render=True
for o in scene.objects:
 if o not in created+anchors:o.select_set(False)
for anchor in anchors:anchor.select_set(True)
bpy.context.view_layer.objects.active=created[0]
bpy.ops.export_scene.fbx(filepath=str(OUT/'Coastal12_Cliffs.fbx'),use_selection=True,object_types={'MESH','EMPTY'},apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',global_scale=1,axis_forward='-Z',axis_up='Y',bake_space_transform=False,use_mesh_modifiers=True,mesh_smooth_type='OFF',bake_anim=False,path_mode='STRIP',embed_textures=False)
# A controlled daylight source proof is kept separate from the actual Unity acceptance.
sea=mat('PROOF_WATER',(.025,.32,.44),.2);grass=mat('PROOF_ROUGH',(.14,.31,.085));fairway=mat('PROOF_FAIRWAY',(.31,.49,.12));sand=mat('PROOF_SAND',(.75,.68,.51));wood=mat('PROOF_WOOD',(.35,.19,.08));paint=mat('PROOF_CHALK',(.81,.8,.71))
for o in ref.objects:
 if o.type!='MESH':continue
 for s,m in enumerate(o.data.materials):
  if m is None:continue
  if m.name.startswith('MAT_ROUGH'):o.data.materials[s]=grass
  elif m.name.startswith('MAT_BASALT'):o.data.materials[s]=chalk
  elif m.name in ('MAT_GREEN','MAT_FAIRWAY','MAT_FIRSTCUT','MAT_BUNKER_LIP'):o.data.materials[s]=fairway
  elif m.name.startswith('MAT_WATER') or m.name=='MAT_FOAM':o.data.materials[s]=sea
  elif m.name=='MAT_SAND':o.data.materials[s]=sand
  elif 'WOOD' in m.name:o.data.materials[s]=wood
  elif 'CHALK' in m.name:o.data.materials[s]=paint
for o in scene.objects:
 if o.get('proof_only'):
  for i in range(len(o.data.materials)):o.data.materials[i]=grass
world=bpy.data.worlds.new('Resort daylight proof');scene.world=world;world.use_nodes=True;world.node_tree.nodes['Background'].inputs[0].default_value=(.51,.64,.72,1);world.node_tree.nodes['Background'].inputs[1].default_value=.5
sun=bpy.data.lights.new('PROOF_SUN','SUN');sun.energy=2.4;sun.angle=.065;so=bpy.data.objects.new('PROOF_SUN',sun);scene.collection.objects.link(so);so.rotation_euler=(math.radians(29),math.radians(-30),math.radians(-40))
camd=bpy.data.cameras.new('PROOF_CAMERA');cam=bpy.data.objects.new('PROOF_CAMERA',camd);scene.collection.objects.link(cam);scene.camera=cam
scene.render.engine='CYCLES';scene.cycles.device='CPU';scene.cycles.samples=24;scene.cycles.use_denoising=True;scene.render.threads_mode='FIXED';scene.render.threads=4;scene.render.resolution_x=1400;scene.render.resolution_y=900;scene.render.resolution_percentage=100
scene.view_settings.view_transform='AgX';scene.view_settings.look='AgX - Medium High Contrast';scene.render.image_settings.file_format='PNG'
# Only terrain references are hidden in the after source proof; course landmarks remain.
ref.hide_render=False
for o in ref.objects:
 if o.name.startswith('TERRAIN'):o.hide_render=True
 elif o.name.startswith('PLANT_') or 'TREE' in o.name or o.name.startswith('ROCK') or o.name.startswith('BOULDER'):o.hide_render=True
camd.lens=45

def frame(location,target,name):
 cam.location=location;cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler();scene.render.filepath=str(OUT/name);bpy.ops.render.render(write_still=True)
if args.render:
 frame((-85,55,35),(0,156,10),'coastal12-new-cliffs-shore.png')
 frame((-185,-120,173),(15,105,7),'coastal12-new-cliffs-overview.png')
 # Exact same camera gives a source comparison of new mass vs original extrusion.
 for o in created:o.hide_render=True
 for o in ref.objects:
  if o.name.startswith('TERRAIN'):o.hide_render=False
 for o in scene.objects:
  if o.get('proof_only'):o.hide_render=True
 frame((-85,55,35),(0,156,10),'coastal12-original-cliffs-shore.png')
 for o in created:o.hide_render=False
 for o in ref.objects:
  if o.name.startswith('TERRAIN'):o.hide_render=True
 for o in scene.objects:
  if o.get('proof_only'):o.hide_render=False
ref.hide_render=True
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Coastal12_Cliffs.blend'))
report={'status':'SOURCE CANDIDATE — actual Unity render/physics gate pending','input':str(INPUT.relative_to(R)),'inputSha256':hashlib.sha256(INPUT.read_bytes()).hexdigest(),'exportAxis':{'forward':'-Z','up':'Y','units':'meters','objectTransforms':'source world coordinates; parent course root at identity'},'geometry':summary,'turfBoundary':rim_audit,'outputFbxSha256':hashlib.sha256((OUT/'Coastal12_Cliffs.fbx').read_bytes()).hexdigest(),'gameplaySourceFilesModified':False}
(OUT/'coastal12-cliff-manifest.json').write_text(json.dumps(report,indent=2));print('COASTAL12_CANDIDATE',json.dumps(summary),flush=True)
