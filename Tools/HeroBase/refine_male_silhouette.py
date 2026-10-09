"""Continue the saved Micro1 blockout with coordinate edits only."""
import bpy,json,hashlib,math
from pathlib import Path
from bisect import bisect_right
R=Path(__file__).resolve().parents[2];D=R/'ArtDir/hero/base_lock';W=R/'work/male-silhouette';P=D/'proof/male_silhouette'
blend=D/'blender/HeroBase_Male_Silhouette.blend'
baseline=W/'micro1-baseline/HeroBase_Male_Silhouette.blend'
def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()
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
# Read the archived saved source for invariant checks, not for rebuilding it.
bpy.ops.wm.open_mainfile(filepath=str(baseline))
original=[o for o in bpy.data.objects if o.type=='MESH'][0]
base_vertices=[tuple(v.co) for v in original.data.vertices]
base={'blend_sha256':sha(baseline),'vertices':len(original.data.vertices),
      'connectivity_sha256':connectivity(original.data),'nonbody_objects':nonbody_signature(),
      'body_matrix':list(sum((list(row) for row in original.matrix_world),[])),
      'material_names':[m.name for m in original.data.materials]}
(W/'micro1-baseline/invariants.json').write_text(json.dumps(base,indent=2))
bpy.ops.wm.open_mainfile(filepath=str(blend))
obj=[o for o in bpy.data.objects if o.type=='MESH'][0];me=obj.data
assert len(me.vertices)==base['vertices'] and connectivity(me)==base['connectivity_sha256']
assert nonbody_signature()==base['nonbody_objects']
assert list(sum((list(row) for row in obj.matrix_world),[]))==base['body_matrix']
assert [m.name for m in me.materials]==base['material_names']
obj.name='Body_M';me.name='Body_M'
cfg=json.loads((W/'contour-revision.json').read_text());s=cfg['metres_per_canonical_pixel']
def lerp_profile(rows,y):
    i=bisect_right([r[0] for r in rows],y)-1;i=max(0,min(len(rows)-2,i))
    a,b=rows[i],rows[i+1];t=max(0,min(1,(y-a[0])/(b[0]-a[0])))
    return [p+(q-p)*t for p,q in zip(a[1:],b[1:])]
def smoothstep(a,b,v):
    t=max(0,min(1,(v-a)/(b-a)));return t*t*(3-2*t)
def map_x(x,anchors):
    for (a,p),(b,q) in zip(anchors,anchors[1:]):
        if x<=b:return p+(q-p)*(x-a)/(b-a)
    a,p=anchors[-1];return x+p-a
changed=0;max_move=0
for v,co in zip(me.vertices,base_vertices):
    # Each trial is a deformation of the same original coordinates and topology.
    x,depth,z=co;y=654-z/s;ax=abs(x)/s
    newax=ax
    if 64<=y<=379:
        outercur,outertar=lerp_profile(cfg['outer'],y)
        newax=ax*outertar/outercur
        if y>=219:
            bodycur,bodytar=lerp_profile(cfg['body'],y)
            inn,out,tinn,tout=lerp_profile(cfg['arms'],y)
            # Preserve the axilla transition; lateral changes fade in below it.
            armx=map_x(ax,[(0,0),(bodycur,bodytar),(inn,tinn),(out,tout)])
            w=smoothstep(219,240,y)
            newax=newax*(1-w)+armx*w
    if y>=380:
        inn,out,tinn,tout=lerp_profile(cfg['legs'],y)
        legx=map_x(ax,[(0,0),(inn,tinn),(out,tout)])
        w=smoothstep(380,397,y)
        newax=ax*(1-w)+legx*w
    # Small crown extension fades out before the forehead; no facial polish.
    newz=z+s*.6*(1-smoothstep(59,82,y)) if y<82 else z
    # No edits to depth, origin, transforms, topology, material or camera data.
    newx=math.copysign(newax*s,x)
    move=math.sqrt((newx-x)**2+(newz-z)**2)
    if move>1e-7:changed+=1
    max_move=max(max_move,move);v.co=(newx,depth,newz)
me.update();me.calc_loop_triangles()
assert connectivity(me)==base['connectivity_sha256'] and nonbody_signature()==base['nonbody_objects']
scene=bpy.context.scene;active=scene.camera;render_path=scene.render.filepath
for ref in json.loads((W/'reference-cameras.json').read_text())['views']:
    scene.camera=bpy.data.objects['Locked_'+ref['name']]
    scene.render.filepath=str(P/(ref['name']+'.png'));bpy.ops.render.render(write_still=True)
scene.camera=active;scene.render.filepath=render_path
bpy.ops.wm.save_as_mainfile(filepath=str(blend))
height=max(v.co.z for v in me.vertices)-min(v.co.z for v in me.vertices)
audit=json.loads((P/'source-audit.json').read_text())
audit.update({'stage':'Micro1 continuation: male bald front/back silhouette only','height_metres':height,
              'vertices':len(me.vertices),'triangles':len(me.loop_triangles),'meshes':[obj.name],
              'blend_sha256':sha(blend),'continuation_source_sha256':base['blend_sha256'],
              'connectivity_sha256':connectivity(me),'original_connectivity_sha256':base['connectivity_sha256'],
              'nonbody_objects_unchanged':nonbody_signature()==base['nonbody_objects'],
              'body_transform_unchanged':list(sum((list(row) for row in obj.matrix_world),[]))==base['body_matrix'],
              'changed_vertices':changed,'maximum_vertex_displacement_metres':max_move,
              'depth_coordinates_unchanged':all(v.co.y==co[1] for v,co in zip(me.vertices,base_vertices)),
              'gate_authority':['bald_front','bald_back'],'excluded_from_gate':['sheet_front','sheet_back','left','right']})
(P/'source-audit.json').write_text(json.dumps(audit,indent=2))
print(json.dumps({k:audit[k] for k in ['height_metres','triangles','changed_vertices','maximum_vertex_displacement_metres','nonbody_objects_unchanged']},indent=2))
