import bpy,bmesh,json,math,bisect,random
from pathlib import Path
from mathutils import Vector
R=Path.cwd();W=R/'work/reference-rebuild';out=W/'coastal-erosion5';out.mkdir(parents=True,exist_ok=True)
ident='coastal_cliff_02'
# The editable canonical blend contains the undistorted reduced scan as a
# fake-user mesh plus packed artist textures. Rebuild does not depend on work/.
canonical=R/'ArtDir/golf/source/ReferenceCliffs12.blend'
if canonical.exists():
    bpy.ops.wm.open_mainfile(filepath=str(canonical))
    embedded=next((m for m in bpy.data.meshes if m.use_fake_user and m.name.startswith(ident)),None)
    if embedded is None:raise RuntimeError('Canonical cliff source lacks embedded unwarped scan template')
    template=embedded.copy();template.use_fake_user=True
else:
    bpy.ops.wm.open_mainfile(filepath=str(W/'vendor'/ident/(ident+'_2k.blend')))
    src=bpy.data.objects[ident+'_LOD3']
    bpy.ops.object.select_all(action='DESELECT');src.select_set(True);bpy.context.view_layer.objects.active=src
    bm=bmesh.new();bm.from_mesh(src.data);bmesh.ops.remove_doubles(bm,verts=bm.verts,dist=.00005);bm.to_mesh(src.data);bm.free()
    dec=src.modifiers.new('Game silhouette detail','DECIMATE');dec.ratio=.18
    bpy.ops.object.modifier_apply(modifier=dec.name)
    template=src.data.copy();template.use_fake_user=True
for img in bpy.data.images:
    if img.source=='FILE':
        p=W/'vendor'/ident/'textures'/Path(img.filepath).name
        if p.exists():img.filepath=str(p);img.reload()
for o in list(bpy.data.objects):bpy.data.objects.remove(o,do_unlink=True)
bpy.ops.import_scene.fbx(filepath=str(R/'Unity/Assets/Resources/Course/hole_12.fbx'),use_anim=False)
scene=bpy.context.scene; originals=list(scene.objects)
rimdata=json.loads((R/'ArtDir/golf/coastal12-cliff-manifest.json').read_text())['turfBoundary']
def warp(y):
    if y<=8 or y>=110:return y
    return 8+(y-8)*6/52 if y<=60 else 14+(y-60)*96/50
def mapped(p):return Vector((p.x,warp(p.y),p.z))
lo=Vector(tuple(min(v.co[k] for v in template.vertices) for k in range(3)))
hi=Vector(tuple(max(v.co[k] for v in template.vertices) for k in range(3)))
envelope=[-1e9]*65
for v in template.vertices:
    i=min(64,int((v.co.x-lo.x)/(hi.x-lo.x)*64));envelope[i]=max(envelope[i],v.co.z)
for i in range(65):
    if envelope[i]<-1e8:envelope[i]=hi.z
def smooth(a,b,x):
    t=max(0.,min(1.,(x-a)/max(1e-8,b-a)));return t*t*(3-2*t)
erosion_fields={}
def erosion_profile(t,h,island,length):
    if island not in erosion_fields:
        rng=random.Random(121260+island*67);count=max(3,round(length/34))
        erosion_fields[island]=[(length*(k+.18+rng.random()*.60)/count, rng.uniform(7,13), rng.uniform(.48,.69), rng.uniform(1.4,3.2),rng.uniform(-.055,.055)) for k in range(count)]
    t=t%length;crest_weight=1-smooth(.86,.94,h)
    # Irregular attached buttresses occupy local spans, never a continuous ring
    # of terraces. Between them the scan retains broad steep fractured planes.
    buttress=0.
    for center,width,level,reach,slope in erosion_fields[island]:
        d=(t-center+length*.5)%length-length*.5
        gate=math.exp(-2.2*(d/width)**2)
        sloped_level=level+slope*d/width
        buttress+=reach*gate*(1-smooth(sloped_level-.17,sloped_level+.08,h))
    # Modest convex foot is strongest at sea level. It tapers continuously
    # into the steep wall instead of stretching a texture across large shelves.
    foot=(3.6+.75*math.sin(t*math.tau*3/length+island))*max(0,1-h)**1.25
    toe=.55*math.exp(-((h-.07)/.105)**2)
    shear=2.8*math.sin(t*math.tau*4/length+island+.8)*math.sin(math.pi*h)*(1-h)**.8*crest_weight
    return (buttress+foot+toe)*crest_weight,shear
created=[]
for island,record in enumerate(rimdata):
    rim=[Vector(p) for p in record['sourceOuterRim']];s=[0.]
    for i,p in enumerate(rim):s.append(s[-1]+(rim[(i+1)%len(rim)]-p).length)
    length=s[-1]
    def sample(t):
        t=t%length;i=min(len(rim)-1,bisect.bisect_right(s,t)-1)
        a,b=rim[i],rim[(i+1)%len(rim)];p=a.lerp(b,(t-s[i])/max(.0001,s[i+1]-s[i]))
        tangent=b-a;tangent.z=0;tangent.normalize()
        return p,Vector((tangent.y,-tangent.x,0))
    count=max(3,round(length/48));span=length/count
    for k in range(count):
        mesh=template.copy();obj=bpy.data.objects.new(f'REFERENCE_CLIFF_{island}_{k:02}',mesh);scene.collection.objects.link(obj)
        for v in mesh.vertices:
            q=v.co.copy();u=(q.x-lo.x)/(hi.x-lo.x)
            raw_t=(k+u*1.10)*span
            p,n=sample(raw_t)
            e=u*64;i=min(63,int(e));top=envelope[i]*(1-(e-i))+envelope[i+1]*(e-i)
            z=min(1,max(0,(q.z-lo.z)/max(.01,top-lo.z)))
            # The scan is allowed to protrude below the crest; the existing turf
            # still owns the playable surface. Every module has a submerged foot.
            depth=(hi.y-q.y)*.52-1.1
            shoulder,shear=erosion_profile(raw_t,z,island,length)
            # Preserve the closed scan's recessed back; add real mass to the
            # exposed face. UVs retain the downloaded scan's actual fractures.
            exposed=smooth(.06,.52,(hi.y-q.y)/max(.001,hi.y-lo.y))
            p,n=sample(raw_t+shear*exposed)
            depth+=shoulder*exposed
            pos=p+n*depth;pos.z=-2+(p.z+1.92)*z
            v.co=mapped(pos)
        for p in mesh.polygons:p.use_smooth=True
        mesh.update();created.append(obj)
    # Recessed opaque backing closes tiny joins behind overlapping natural faces.
    vs=[mapped(p-Vector((0,0,.04))) for p in rim]+[mapped(Vector((p.x,p.y,-2.5))) for p in rim]
    fs=[(i,(i+1)%len(rim),(i+1)%len(rim)+len(rim),i+len(rim)) for i in range(len(rim))]
    m=bpy.data.meshes.new('Reference interior backing');m.from_pydata(vs,[],fs);m.update()
    o=bpy.data.objects.new(f'REFERENCE_CLIFF_CORE_{island}',m);scene.collection.objects.link(o);created.append(o)
anchors=[]
for suffix in ['TEE','PIN','UP']:
    old=bpy.data.objects['MARKER_'+suffix];o=bpy.data.objects.new('COASTAL12_ANCHOR_'+suffix,None);scene.collection.objects.link(o);o.matrix_world=old.matrix_world.copy();o.location=mapped(o.location);anchors.append(o)
for o in originals:o.hide_render=True
bpy.ops.object.select_all(action='DESELECT')
for o in created+anchors:o.select_set(True)
bpy.context.view_layer.objects.active=created[0]
bpy.ops.export_scene.fbx(filepath=str(out/'ReferenceCliffs12.fbx'),use_selection=True,object_types={'MESH','EMPTY'},apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',axis_forward='-Z',axis_up='Y',bake_anim=False,path_mode='STRIP')

proof_mat=bpy.data.materials.new('PROOF_INSTALLED_CHALK_RANGE');proof_mat.use_nodes=True
nt=proof_mat.node_tree;bsdf=nt.nodes.get('Principled BSDF')
diff=nt.nodes.new('ShaderNodeTexImage');diff.image=next(img for img in bpy.data.images if '_diff' in img.name)
rgb=nt.nodes.new('ShaderNodeRGBToBW');nt.links.new(diff.outputs['Color'],rgb.inputs[0])
subtract=nt.nodes.new('ShaderNodeMath');subtract.operation='SUBTRACT';subtract.inputs[1].default_value=.22;nt.links.new(rgb.outputs[0],subtract.inputs[0])
gain=nt.nodes.new('ShaderNodeMath');gain.operation='MULTIPLY';gain.inputs[1].default_value=1.8;nt.links.new(subtract.outputs[0],gain.inputs[0])
mid=nt.nodes.new('ShaderNodeMath');mid.operation='ADD';mid.inputs[1].default_value=.5;mid.use_clamp=True;nt.links.new(gain.outputs[0],mid.inputs[0])
palette=nt.nodes.new('ShaderNodeMixRGB');palette.blend_type='MIX';palette.inputs[1].default_value=(.46,.48,.45,1);palette.inputs[2].default_value=(.91,.86,.74,1);nt.links.new(mid.outputs[0],palette.inputs[0]);nt.links.new(palette.outputs[0],bsdf.inputs['Base Color']);bsdf.inputs['Roughness'].default_value=.86
normal=nt.nodes.new('ShaderNodeTexImage');normal.image=next(img for img in bpy.data.images if '_nor_gl' in img.name);normal.image.colorspace_settings.name='Non-Color'
normal_decode=nt.nodes.new('ShaderNodeNormalMap');normal_decode.inputs['Strength'].default_value=.65;nt.links.new(normal.outputs['Color'],normal_decode.inputs['Color']);nt.links.new(normal_decode.outputs[0],bsdf.inputs['Normal'])
for o in created:
    if o.name.startswith('REFERENCE_CLIFF_CORE'):continue
    o.data.materials.clear();o.data.materials.append(proof_mat)
# A sea plane makes the rounded grounded toe legible. Source display only.
sea_mesh=bpy.data.meshes.new('Proof open sea');sea_mesh.from_pydata([(-600,-350,0),(600,-350,0),(600,600,0),(-600,600,0)],[],[(0,1,2,3)]);sea_mesh.update()
sea=bpy.data.objects.new('PROOF_SEA_ONLY',sea_mesh);scene.collection.objects.link(sea)
sea_mat=bpy.data.materials.new('Proof turquoise sea');sea_mat.use_nodes=True;sea_mat.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value=(.025,.22,.32,1);sea_mat.node_tree.nodes['Principled BSDF'].inputs['Roughness'].default_value=.27;sea.data.materials.append(sea_mat)

scene.use_nodes=False;scene.render.film_transparent=False;scene.view_settings.view_transform='AgX';scene.view_settings.look='AgX - Medium High Contrast';scene.view_settings.exposure=0;scene.view_settings.gamma=1
scene.render.engine='CYCLES';scene.cycles.samples=16;scene.cycles.use_denoising=True;scene.render.threads_mode='FIXED';scene.render.threads=4
scene.render.resolution_x=1400;scene.render.resolution_y=900;scene.render.resolution_percentage=100
world=bpy.data.worlds.new('Coastal source daylight');scene.world=world;world.use_nodes=True;world.node_tree.nodes['Background'].inputs[0].default_value=(.45,.62,.79,1);world.node_tree.nodes['Background'].inputs[1].default_value=.8
sun=bpy.data.lights.new('Coastal sunlight','SUN');sun.energy=2.4;sun.angle=.07;so=bpy.data.objects.new('Coastal sunlight',sun);scene.collection.objects.link(so);so.rotation_euler=(-Vector((-.48,-.65,.59))).to_track_quat('-Z','Y').to_euler()
cam=bpy.data.objects.new('Reference cliff camera',bpy.data.cameras.new('Reference cliff camera'));scene.collection.objects.link(cam);scene.camera=cam;cam.data.lens=44
cam.location=(-49,9,24);target=Vector((0,155,7));cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler()
scene.render.filepath=str(out/'scanned-cliff-erosion-source.png');bpy.ops.render.render(write_still=True)
bpy.ops.file.pack_all();bpy.ops.wm.save_as_mainfile(filepath=str(out/'ReferenceCliffs12.blend'))
report={'source':'https://polyhaven.com/a/coastal_cliff_02','author':'Rob Tuytel','license':'CC0','sourceLod':'LOD3 reduced .18','moduleCount':len(created),'triangles':sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in created),'warpMetres':[[8,8],[60,14],[110,110]],'status':'OFF-ASSETS EROSION CANDIDATE; actual Unity visual acceptance pending','structuralChange':'Continuous crest-fixed erosion sweep: localized asymmetric attached buttresses and convex sea-level foot, with height-dependent tangential shear. Same scan/UV/module count; broad scan planes retained between buttresses.'}
(out/'reference-cliffs-manifest.json').write_text(json.dumps(report,indent=2));print(report,flush=True)
cam.location=(-4,-8,21);target=Vector((0,128,11));cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();scene.render.filepath=str(out/'scanned-cliff-erosion-tee.png');bpy.ops.render.render(write_still=True)
