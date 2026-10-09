"""Separate fuller arched coconut candidate; never writes to live Assets.

Blender --background --python build-coconut-palm.py -- --out <stage>
PALM/PALM_FAR share the golf kit's seven semantic roles, WindWeight colour,
and UV2 leaf pivot .90. Trunk height and crown proportions remain separate.
"""
import argparse, json, math, random, sys
from pathlib import Path
import bpy, bmesh
from mathutils import Vector

parser=argparse.ArgumentParser()
parser.add_argument('--out',type=Path,required=True)
args=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
args.out.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
palette=[('RESORT_BARK',(.28,.16,.065)),('RESORT_LEAF_DARK',(.07,.24,.075)),
         ('RESORT_LEAF_MID',(.16,.37,.085)),('RESORT_LEAF_LIGHT',(.32,.47,.10)),
         ('RESORT_CORAL',(.88,.23,.20)),('RESORT_GOLD',(.8,.5,.1)),('RESORT_STONE',(.45,.46,.37))]
materials=[]
for name,colour in palette:
    mat=bpy.data.materials.new(name);mat.diffuse_color=(*colour,1);mat.use_nodes=True
    shader=mat.node_tree.nodes.get('Principled BSDF');shader.inputs['Base Color'].default_value=mat.diffuse_color
    shader.inputs['Roughness'].default_value=.59
    materials.append(mat)

def palm(name,far=False):
    vertices=[];faces=[];roles=[];pivots=[];winds=[]
    def vertex(point,pivot=-1,wind=0):
        vertices.append(tuple(point));pivots.append(pivot);winds.append(wind);return len(vertices)-1
    def face(indices,role):faces.append(indices);roles.append(role)
    # A closed folded strip. Each blade has width, crease, rounded volume and
    # a restrained downturned tip; its broad proximal surface fills crown gaps.
    def blade(points,widths,role,wind,span,fold=.12):
        rings=[]
        for j,(point,width) in enumerate(zip(points,widths)):
            thickness=max(.00010,width*.008)
            rings.append([vertex(point-span*width,.90,wind),
                          vertex(point+Vector((0,0,width*fold+thickness)),.90,wind),
                          vertex(point+span*width,.90,wind),
                          vertex(point-Vector((0,0,thickness)),.90,wind)])
        for a,b in zip(rings,rings[1:]):
            for k in range(4):face((a[k],a[(k+1)%4],b[(k+1)%4],b[k]),role)
        face(tuple(reversed(rings[0])),role);face(tuple(rings[-1]),role)
    trunk_sides=16 if not far else 12;trunk_rings=67 if not far else 33;trunk=[]
    for j in range(trunk_rings):
        z=.90*j/(trunk_rings-1);centre=Vector((.145*(z/.90)**1.65,.014*math.sin(z*math.pi),z))
        radius=(.033-.012*z/.90)*(1+.050*math.sin(z*175))
        trunk.append([vertex(centre+Vector((math.cos(i*math.tau/trunk_sides)*radius,
                                          math.sin(i*math.tau/trunk_sides)*radius,0))) for i in range(trunk_sides)])
    for a,b in zip(trunk,trunk[1:]):
        for k in range(trunk_sides):face((a[k],a[(k+1)%trunk_sides],b[(k+1)%trunk_sides],b[k]),0)
    face(tuple(reversed(trunk[0])),0);face(tuple(trunk[-1]),0)
    centre=Vector((.145,0,.90));fronds=18 if not far else 14
    pairs=22 if not far else 16;sections=5 if not far else 4
    rng=random.Random(21073)
    for n in range(fronds):
        angle=n*math.tau/fronds+rng.uniform(-.16,.16)
        outward=Vector((math.cos(angle),math.sin(angle),0));side=Vector((-math.sin(angle),math.cos(angle),0))
        layer=n%5
        length=(.76+.21*rng.random())*(.77 if layer==0 else 1)
        pitch=rng.random()
        rise=(.27 if layer==0 else .09+.22*pitch)+rng.uniform(-.035,.035)
        drop=(-.15 if layer==0 else .12+.27*(1-pitch))+rng.uniform(-.035,.035)
        origin=centre+outward*rng.uniform(.002,.019)+Vector((0,0,rng.uniform(-.023,.025)))
        lateral=rng.uniform(-.047,.047)
        def axis(t):
            return origin+outward*(length*t)+side*(math.sin(t*math.pi)*lateral)+Vector((0,0,rise*math.sin(t*math.pi)-drop*t*t))
        # The inner folded lamina is actual opaque geometry, not a card: the
        # featherlets emerge from its rounded spine and overlap near the base.
        samples=[j/8 for j in range(9)]
        blade([axis(t) for t in samples],[.002+.015*math.sin(math.pi*t)**.7 for t in samples],
              2 if layer==0 else 1,.30,outward.cross(Vector((0,0,1))).normalized(),.04)
        for j in range(pairs):
            t=.04+.91*(j+.5)/pairs
            leaflet=(.080+.205*math.sin(math.pi*t)**.65)*(1-.18*t)
            for sign in (-1,1):
                direction=(side*sign+outward*(.35+.20*t)).normalized()
                points=[];widths=[]
                for k in range(sections):
                    u=k/(sections-1)
                    point=axis(t)+direction*leaflet*u+outward*(.027*math.sin(u*math.pi))
                    point.z+=.027*math.sin(u*math.pi)-(.026+.065*t)*u*u
                    points.append(point)
                    widths.append(max(.0012,(.029 if not far else .042)*(math.sin(u*math.pi)**.68)))
                role=1 if (n+j)%5==0 else 3 if (n+j)%7==0 else 2
                blade(points,widths,role,.20+.76*t,Vector((-direction.y,direction.x,0)),.045)
    mesh=bpy.data.meshes.new(name);mesh.from_pydata(vertices,[],faces);mesh.update()
    normals=bmesh.new();normals.from_mesh(mesh)
    bmesh.ops.recalc_face_normals(normals,faces=normals.faces)
    normals.to_mesh(mesh);normals.free()
    for material in materials:mesh.materials.append(material)
    for polygon,role in zip(mesh.polygons,roles):polygon.material_index=role;polygon.use_smooth=True
    # Keep the thin blade upper/lower planes separate at their sharp rim;
    # averaging those opposing normals inflated each featherlet into a hose.
    mesh.set_sharp_from_angle(angle=math.radians(60))
    uv=mesh.uv_layers.new(name='UVMap');pivot_uv=mesh.uv_layers.new(name='LeafPivot')
    colours=mesh.color_attributes.new(name='WindWeight',type='FLOAT_COLOR',domain='POINT')
    for i,item in enumerate(colours.data):item.color=(winds[i],winds[i],winds[i],1)
    for polygon in mesh.polygons:
        for loop_index in polygon.loop_indices:
            index=mesh.loops[loop_index].vertex_index;point=vertices[index]
            uv.data[loop_index].uv=(point[0],point[2]);pivot_uv.data[loop_index].uv=(pivots[index],0)
    obj=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(obj)
    mesh.calc_loop_triangles();bm=bmesh.new();bm.from_mesh(mesh)
    boundary=sum(e.is_boundary for e in bm.edges);nonmanifold=sum(not e.is_manifold for e in bm.edges);bm.free()
    return obj,{'mesh':name,'triangles':len(mesh.loop_triangles),'vertices':len(mesh.vertices),
                'boundaryEdges':boundary,'nonmanifoldEdges':nonmanifold,'leafPivot':.90,
                'fronds':fronds,'pairedFeatherlets':pairs,'crownArch':'irregular compact spear/arched fans; overlapping inner featherlet bases form a connected crown silhouette; split thin rim normals'}

near,near_audit=palm('PALM');far,far_audit=palm('PALM_FAR',True)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(filepath=str(args.out/'TennisCoconutPalm.fbx'),use_selection=True,
    object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,
    apply_scale_options='FBX_SCALE_UNITS',mesh_smooth_type='FACE',bake_anim=False,add_leaf_bones=False)
bpy.ops.wm.save_as_mainfile(filepath=str(args.out/'TennisCoconutPalm.blend'))
(args.out/'palm-manifest.json').write_text(json.dumps({'status':'source candidate, actual Unity visual review required',
    'schema':'PALM/PALM_FAR; seven shared golf material roles; UV2.x leaf pivot .90; WindWeight r for tip breeze',
    'representations':[near_audit,far_audit]},indent=2)+'\n')
print(json.dumps([near_audit,far_audit],indent=2))
