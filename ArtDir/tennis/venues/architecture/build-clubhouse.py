"""Preserve the authored resort footprint; add manufactured finish to its house."""
import bpy,bmesh,math,json,argparse,sys
from mathutils import Vector
from pathlib import Path
root=Path(__file__).resolve().parents[4]
parser=argparse.ArgumentParser();parser.add_argument('--out',type=Path,required=True)
args=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
args.out.mkdir(parents=True,exist_ok=True)
source=root/'ArtDir/env_v4/tools/build_env_v4.py'
namespace={'__file__':str(source)}
code=source.read_text().split('\nreset()\nos.makedirs')[0]
a=code.index('    def window(cx, cz, w=1.3')
b=code.index('    for i in range(6): window',a)
code=code[:a]+"""    def window(cx, cz, w=1.3, h=2.0, y=-D / 2 - .03):
        # One closed arched glazing panel replaces coincident box/disc faces.
        # The projecting cream reveal has actual depth around the whole arch.
        def contour(radius,base):
            return [(cx-radius,base),(cx+radius,base)]+[(cx+math.cos(k*math.pi/16)*radius,cz+h/2+math.sin(k*math.pi/16)*radius) for k in range(17)]
        def ring(outline,depth):
            return [bm.verts.new((px,depth,pz)) for px,pz in outline]
        outline=contour(w/2,cz-h/2)
        front=ring(outline,y-.062);back=ring(outline,y+.060)
        faces=[bm.faces.new(front),bm.faces.new(list(reversed(back)))]
        for j in range(len(front)):
            k=(j+1)%len(front);faces.append(bm.faces.new((front[j],back[j],back[k],front[k])))
        P(GLASS,faces)
        outer=ring(contour(w/2+.085,cz-h/2-.085),y-.115)
        inner=ring(outline,y-.115);outer_back=ring(contour(w/2+.085,cz-h/2-.085),y-.052)
        inner_back=ring(outline,y-.052);reveal=[]
        for j in range(len(front)):
            k=(j+1)%len(front)
            reveal.extend([bm.faces.new((outer[j],outer[k],inner[k],inner[j])),
                           bm.faces.new((outer[j],outer_back[j],outer_back[k],outer[k])),
                           bm.faces.new((inner[j],inner[k],inner_back[k],inner_back[j]))])
        P(WHITE,reveal)
        for side in (-1,1):P(TEAL,add_box(bm,(cx+side*(w/2+.34),y-.02,cz),(.55,.1,h+.15)))
"""+code[b:]
exec(code,namespace)
for key in ['reset','beach_house','paint','add_box','add_cyl']:
 globals()[key]=namespace[key]
reset()
ob=beach_house('TennisClubhouse')
bm=bmesh.new();bm.from_mesh(ob.data)
TEAL,WOOD,STONE,TRIM=5,6,11,12
def box(cell,c,s):paint(bm,cell,add_box(bm,c,s))
# The existing identity / envelope stays intact. Small details have actual depth:
# shutter louvres, window sills, squared column capitals, eave fascia, roof ridge.
for i in range(6):
 x=-9+i*3.6
 for side in [-1,1]:
  sx=x+side*.97
  for row in range(8):box(TEAL,(sx,-5.64,1.4+row*.23),(.50,.055,.045))
 box(STONE,(x,-5.70,1.26),(1.47,.27,.12))
for i in range(5):
 x=-8.2+i*3.35
 for side in [-1,1]:
  for row in range(7):box(TEAL,(x+side*.87,-3.50,5.16+row*.20),(.50,.065,.035))
 box(STONE,(x,-3.62,5.08),(1.29,.27,.10))
for i in range(7):
 x=-10+i*20/6
 box(STONE,(x,-8,1.0),(.54,.54,.24))
 box(TRIM,(x,-8,4.20),(.54,.54,.18))
box(TRIM,(-1.5,-3.83,7.94),(18.23,.12,.23))
box(TRIM,(-1.5,5.63,7.94),(18.23,.12,.23))
box(WOOD,(-1.5,.9,10.19),(9.85,.25,.18))
for i in range(13):box(STONE,(-9.3+i*1.3,-6.82,.79),(.025,2.47,.035))
# Overlapping curved terracotta tiles follow the actual existing hip roof
# slopes. This retains the landmark silhouette but replaces flat roof panels
# with manufactured scale, warm highlights and small shadowed row edges.
roof_faces=[]
# Keep the tile field's curved sections and row lips out of the global joinery
# bevel. Only the original manufactured building edges need that modifier.
bevel_weights=bm.edges.layers.float.new('bevel_weight_edge')
bm.normal_update()
for edge in bm.edges:edge[bevel_weights]=1 if edge.calc_face_angle(0)>math.radians(34) else 0
uv_layer=bm.loops.layers.uv.active
for f in bm.faces:
    f.normal_update()
    cell=int(sum(l[uv_layer].uv.x for l in f.loops)/len(f.loops)*16)
    if cell==4 and .15<f.normal.z<.95 and min(v.co.z for v in f.verts)>7.7:
        roof_faces.append(f)
for face0 in roof_faces:
    low=sorted(face0.verts,key=lambda v:v.co.z)[:2]
    high=sorted(face0.verts,key=lambda v:v.co.z)[-2:]
    low0,low1=[v.co.copy() for v in low]
    high0,high1=[v.co.copy() for v in high]
    if (low0-high0).length+(low1-high1).length>(low0-high1).length+(low1-high0).length:
        high0,high1=high1,high0
    width=(low1-low0).length;length=((high0+high1-low0-low1)*.5).length
    rows=max(4,round(length/.65));columns=max(6,round(width/.44))
    normal=face0.normal.copy()
    for row in range(rows):
        t0=row/rows;t1=min(1,(row+1.10)/rows)
        for col in range(columns):
            u0=col/columns;u1=(col+1)/columns
            made=[];rings=[]
            for along in (t0,t1):
                a=low0.lerp(high0,along);b=low1.lerp(high1,along)
                ring=[]
                for k in range(4):
                    u=u0+(u1-u0)*k/3
                    p=a.lerp(b,u)+normal*(.028+.057*math.sin(k/3*math.pi))
                    ring.append(bm.verts.new(p))
                rings.append(ring)
            for k in range(3):made.append(bm.faces.new((rings[0][k],rings[0][k+1],rings[1][k+1],rings[1][k])))
            # The short row lip has real depth, with no tube-like round end.
            lip=[bm.verts.new(v.co-normal*.037) for v in rings[0]]
            for k in range(3):made.append(bm.faces.new((lip[k],lip[k+1],rings[0][k+1],rings[0][k])))
            paint(bm,4,made,zmin=min(low0.z,low1.z),zmax=max(high0.z,high1.z))
# Finer staggered eave mouldings and bevelled timber beam ends preserve the
# two-storey footprint, rather than adding another disconnected building.
for y in (-3.92,5.72):
    box(TRIM,(-1.5,y,7.83),(18.48,.18,.18))
    box(WOOD,(-1.5,y,7.65),(18.24,.17,.19))
for i in range(18):
    x=-10.2+i*1.025
    box(WOOD,(x,-3.96,7.47),(.10,.28,.21))
bmesh.ops.recalc_face_normals(bm,faces=bm.faces)
bm.to_mesh(ob.data);bm.free()
# Split by existing semantic palette UVs so glass, plaster, timber, roof and
# ceramic receive independent physical responses while keeping the atlas intact.
uv=ob.data.uv_layers.active
for i in range(16):
 m=bpy.data.materials.new('CLUBHOUSE_'+str(i).zfill(2));m.diffuse_color=(1,1,1,1)
 ob.data.materials.append(m)
for p in ob.data.polygons:
 u=sum(uv.data[j].uv.x for j in p.loop_indices)/len(p.loop_indices)
 p.material_index=max(0,min(15,int(u*16)))
bpy.context.view_layer.objects.active=ob;ob.select_set(True)
bevel=ob.modifiers.new('Cut edges and rounded joinery','BEVEL');bevel.width=.028
bevel.segments=2;bevel.limit_method='WEIGHT';bevel.angle_limit=math.radians(34)
bpy.ops.object.modifier_apply(modifier=bevel.name)
# Flat panels remain flat, the tiny chamfers catch a clean highlight.
for p in ob.data.polygons:p.use_smooth=False
bpy.ops.export_scene.fbx(filepath=str(args.out/'TennisClubhouse.fbx'),use_selection=True,
 object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,
 apply_scale_options='FBX_SCALE_UNITS',mesh_smooth_type='FACE',bake_anim=False,add_leaf_bones=False)
bpy.ops.wm.save_as_mainfile(filepath=str(args.out/'TennisClubhouse.blend'))
ob.data.calc_loop_triangles()
report={'triangles':len(ob.data.loop_triangles),'vertices':len(ob.data.vertices),
 'materials':len(ob.data.materials),'dimensions':list(ob.dimensions),
 'identity':'original two-storey and lookout landmark; overlapping curved tile roofing and crafted eave joinery; no colliders'}
(args.out/'clubhouse-manifest.json').write_text(json.dumps(report,indent=2))
print(report)
