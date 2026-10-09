"""Editable opaque broadleaf sports-resort crowns; no Unity/shared golf files written."""
import bpy,math,random,json
from pathlib import Path
from mathutils import Vector
import argparse,sys,bmesh
parser=argparse.ArgumentParser();parser.add_argument('--out',type=Path,required=True)
args=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
W=args.out;W.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
code=(Path(__file__).resolve().parent/'botanical_primitives.py').read_text()
exec(code)
# Tree envelope is approximately unit radius/height. Unequal forked clusters
# overlap as a canopy; the core stays opaque at distance, leaf relief softens it nearby.
def crown(c,size,role,seed,near):
    c=Vector(c);rx,ry,rz=size;rings,sides=(10,20) if near else (5,12);start=len(verts)
    for ring in range(rings+1):
        phi=math.pi*ring/rings;sin=math.sin(phi)
        for j in range(sides):
            a=math.tau*j/sides
            lobes=1+.058*math.cos(a*5+seed)+.038*math.sin(a*3-seed*.7)
            z=math.cos(phi)*rz*(1+.05*math.sin(a*4+seed)*sin)
            p=c+Vector((sin*math.cos(a)*rx*lobes,sin*math.sin(a)*ry*lobes,z))
            vertex(p,.12+.23*sin)
    for ring in range(rings):
        for j in range(sides):
            q=start+ring*sides+j;n=start+ring*sides+(j+1)%sides
            face((q,n,n+sides,q+sides),role)
    if True:
        # Opaque small rounded leaf pads follow the crown's shoulder instead of
        # broad pointed cards. Their normals remain smooth at phone silhouette.
        for k in range(13):
            a=k*2.399963+seed*.4;t=.36+.38*((k*7)%13)/12
            z=rz*(.23+.50*math.sin(t*math.pi));rad=math.sqrt(max(0,1-(z/rz)**2))
            root=c+Vector((math.cos(a)*rx*rad*.94,math.sin(a)*ry*rad*.94,z))
            leaf(root,a,.14+.035*math.sin(k+seed),.045,.025,1+(k+seed)%3,3 if near else 2)

clusters=[((-.36,-.21,.63),(.32,.27,.20),1),((.02,-.28,.72),(.33,.29,.20),2),((.38,-.11,.66),(.32,.29,.20),2),((.29,.27,.75),(.33,.28,.20),3),((-.07,.34,.71),(.35,.29,.22),2),((-.38,.24,.62),(.29,.28,.19),1),((-.08,.02,.85),(.34,.31,.17),3)]
for near,name in [(True,'CANOPY'),(False,'CANOPY_FAR')]:
    reset();last=Vector((0,0,0))
    for k in range(10):
        t=(k+1)/10;at=Vector((.040*math.sin(t*1.8),-.025*t*t,.52*t))
        tube(last,at,.052*(1-k/15),.052*(1-(k+1)/15),0,10 if near else 7);last=at
    for i,(at,size,role) in enumerate(clusters):
        end=Vector(at)-Vector((0,0,.08));mid=Vector((end.x*.44,end.y*.44,.46))
        tube((0,0,.30+(i%3)*.025),mid,.027,.018,0,8 if near else 6);tube(mid,end,.018,.010,0,8 if near else 6)
        crown(at,size,role,i,near)
    ob=mesh(name)
    # Shared pole vertices and more shoulder rings keep far crowns rounded;
    # the former three-ring core produced obvious flat green wedges.
    bm=bmesh.new();bm.from_mesh(ob.data)
    bmesh.ops.remove_doubles(bm,verts=bm.verts,dist=.000001)
    bmesh.ops.dissolve_degenerate(bm,edges=bm.edges,dist=.000001)
    bmesh.ops.recalc_face_normals(bm,faces=bm.faces)
    bm.to_mesh(ob.data);bm.free()
    uv=ob.data.uv_layers.new(name='LeafVerticalPivot')
    for p in ob.data.polygons:
        pivot=.58 if p.material_index else -1
        for loop in p.loop_indices:uv.data[loop].uv=(pivot,0)
    # First UV slot is intentionally available; importer UV2 carries the pivot.
    base=ob.data.uv_layers.new(name='BaseUV');ob.data.uv_layers.active=base
    for p in ob.data.polygons:
        for loop in p.loop_indices:
            vi=ob.data.loops[loop].vertex_index;v=ob.data.vertices[vi].co;base.data[loop].uv=(v.x*.5+.5,v.z)
    # FBX exports UV layers by list order, not active index.
    # Recreate in base,pivot order to keep the shared leaf-pivot contract exact.
    values=[tuple(v.uv) for v in uv.data];basevals=[tuple(v.uv) for v in base.data]
    ob.data.uv_layers.remove(uv);ob.data.uv_layers.remove(base)
    a=ob.data.uv_layers.new(name='BaseUV');b=ob.data.uv_layers.new(name='LeafVerticalPivot')
    for q,v in zip(a.data,basevals):q.uv=v
    for q,v in zip(b.data,values):q.uv=v
    ob.data.uv_layers.active=a
objects=[o for o in bpy.context.scene.objects if o.type=='MESH'];report={}
for o in objects:
    o.data.calc_loop_triangles();report[o.name]={'triangles':len(o.data.loop_triangles),'vertices':len(o.data.vertices),'envelope':[list(o.bound_box[0]),list(o.bound_box[6])]}
bpy.ops.object.select_all(action='DESELECT')
for o in objects:o.select_set(True)
bpy.context.view_layer.objects.active=objects[0]
bpy.ops.export_scene.fbx(filepath=str(W/'TennisCanopy.fbx'),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_anim=False,add_leaf_bones=False,path_mode='AUTO',mesh_smooth_type='FACE')
bpy.ops.wm.save_as_mainfile(filepath=str(W/'TennisCanopy.blend'))
(W/'canopy-manifest.json').write_text(json.dumps(report,indent=2)+'\n')
print('TENNIS_CANOPY_STAGED',json.dumps(report))
