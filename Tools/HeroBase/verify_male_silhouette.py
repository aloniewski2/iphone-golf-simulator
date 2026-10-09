"""Read-only verification of the saved male blockout and its proof contract."""
import bpy,json,hashlib
from pathlib import Path
R=Path(__file__).resolve().parents[2];D=R/'ArtDir/hero/base_lock';P=D/'proof/male_silhouette'
W=R/'work/male-silhouette'
O=Path('/Users/adnanyonathan/Documents/Codex/2026-09-30/open-3/outputs/Male_Silhouette')
def connectivity(me):
    return hashlib.sha256(json.dumps([list(f.vertices) for f in me.polygons],separators=(',',':')).encode()).hexdigest()
def nonbody_signature():
    out={}
    for o in bpy.data.objects:
        if o.type=='MESH':continue
        entry={'type':o.type,'matrix':list(sum((list(row) for row in o.matrix_world),[])),
               'location':list(o.location),'rotation':list(o.rotation_euler),'scale':list(o.scale),
               'locks':[list(o.lock_location),list(o.lock_rotation),list(o.lock_scale)],
               'hide_select':o.hide_select,'hide_render':o.hide_render,'props':dict(o.items())}
        if o.type=='CAMERA':
            entry['camera']={'type':o.data.type,'ortho_scale':o.data.ortho_scale,'lens':o.data.lens,
                'shift_x':o.data.shift_x,'shift_y':o.data.shift_y,'clip_start':o.data.clip_start,'clip_end':o.data.clip_end,
                'backgrounds':[{'image':b.image.name,'sha256':hashlib.sha256(bytes(b.image.packed_file.data)).hexdigest(),
                                'alpha':b.alpha,'display_depth':b.display_depth,'offset':list(b.offset),'scale':b.scale,'rotation':b.rotation}
                               for b in o.data.background_images]}
        out[o.name]=entry
    return out
base=json.loads((W/'micro1-baseline/invariants.json').read_text())
bpy.ops.wm.open_mainfile(filepath=str(W/'micro1-baseline/HeroBase_Male_Silhouette.blend'))
original=[o for o in bpy.data.objects if o.type=='MESH'][0]
base_vertices=[tuple(v.co) for v in original.data.vertices]
base_camera=bpy.context.scene.camera.name;base_render_path=bpy.context.scene.render.filepath
base_material=[tuple(m.diffuse_color) for m in original.data.materials]
assert nonbody_signature()==base['nonbody_objects']
blend=D/'blender/HeroBase_Male_Silhouette.blend';bpy.ops.wm.open_mainfile(filepath=str(blend))
scene=bpy.context.scene;meshes=[o for o in bpy.data.objects if o.type=='MESH'];assert len(meshes)==1 and meshes[0].name=='Body_M'
assert scene.unit_settings.system=='METRIC' and scene.unit_settings.scale_length==1
assert not any(o.type=='ARMATURE' for o in bpy.data.objects)
obj=meshes[0];assert tuple(obj.scale)==(1,1,1) and tuple(obj.location)==(0,0,0) and tuple(obj.rotation_euler)==(0,0,0)
me=obj.data;me.calc_loop_triangles();height=max(v.co.z for v in me.vertices)-min(v.co.z for v in me.vertices);assert 1.65<=height<=1.75 and len(me.loop_triangles)<=50000
assert len(me.vertices)==base['vertices'] and connectivity(me)==base['connectivity_sha256']
assert all(v.co.y==co[1] for v,co in zip(me.vertices,base_vertices))
assert nonbody_signature()==base['nonbody_objects']
assert list(sum((list(row) for row in obj.matrix_world),[]))==base['body_matrix']
assert scene.camera.name==base_camera and scene.render.filepath==base_render_path
assert [tuple(m.diffuse_color) for m in me.materials]==base_material
assert len(me.materials)==1 and me.materials[0].name=='Blockout_Grey'
assert not me.materials[0].node_tree or not any(n.type=='TEX_IMAGE' for n in me.materials[0].node_tree.nodes)
refs=json.loads((O/'reference-cameras.json').read_text())
for v in refs['views']:
    cam=bpy.data.objects['Locked_'+v['name']]
    assert cam.hide_select and all(cam.lock_location) and all(cam.lock_rotation) and all(cam.lock_scale)
    assert cam.data.type=='ORTHO' and abs(cam.data.ortho_scale-1280*v['metres_per_pixel'])<1e-5
    assert abs(cam['source_centre_x']-v['centre_x'])<1e-8
    assert cam['reference_sha256']==v['sha256']==hashlib.sha256((D/v['file']).read_bytes()).hexdigest()
    assert cam.data.background_images[0].image.packed_file
images=[im for im in bpy.data.images if im.source=='FILE'];assert len(images)==3 and all(im.packed_file for im in images)
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
assert sha(blend)==sha(O/blend.name)==json.loads((O/'source-audit.json').read_text())['blend_sha256']
assert sha(D/'proof/01_silhouette_overlay_m.png')==sha(O/'01_silhouette_overlay_m.png')
assert sha(D/'proof/MALE_SIL_RESULTS.md')==sha(O/'MALE_SIL_RESULTS.md')
assert sha(W/'micro1-baseline/HeroBase_Male_Silhouette.blend')==base['blend_sha256']
edge={tuple(sorted(e.vertices)):0 for e in me.edges}
for f in me.polygons:
    for e in f.edge_keys:edge[tuple(sorted(e))]+=1
metrics=json.loads((O/'silhouette-metrics.json').read_text())
assert metrics['gate_authority']==['bald_front','bald_back'] and metrics['threshold']==.05
primary=[r for r in metrics['measurements'] if r['gate_authority']]
assert len(primary)==2 and all(r['symmetric_difference_over_union']<=.05 for r in primary)
assert metrics['gate']=='MALE_SIL PASS'
report={'saved_source_verification':'PASS','one_plain_grey_male_blockout':True,'metres_unit_scale':1,'height_metres':height,'triangles':len(me.loop_triangles),'vertices':len(me.vertices),'topology_matches_micro1':True,'camera_and_reference_transforms_match_micro1':True,'body_origin_and_transform_match_micro1':True,'depth_coordinates_match_micro1':True,'locked_reference_cameras':6,'packed_authority_images':3,'hair':False,'stubble':False,'facial_textures_or_polish':False,'rig':False,'boundary_edges':sum(n==1 for n in edge.values()),'nonmanifold_edges_more_than_two_faces':sum(n>2 for n in edge.values()),'delivered_blend_and_proof_hashes_match':True,'silhouette_gate':metrics['gate'],'gate_authority':['bald_front','bald_back']}
(P/'saved-blockout-verification.json').write_text(json.dumps(report,indent=2));(O/'saved-blockout-verification.json').write_text(json.dumps(report,indent=2));print(json.dumps(report,indent=2))
verified=W/'verified-save';verified.mkdir(exist_ok=True)
for ref in refs['views'][:2]:
    scene.camera=bpy.data.objects['Locked_'+ref['name']]
    scene.render.filepath=str(verified/(ref['name']+'.png'));bpy.ops.render.render(write_still=True)
