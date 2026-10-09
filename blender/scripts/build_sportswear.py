"""Original sportswear study: authored profiles at slim / medium / broad sizes.

All profiles share topology. Clothes have their own silhouette, thickness and hems;
arms follow the existing skeleton and body size never scales equipment sockets.
"""
import bpy, bmesh, math
from mathutils import Vector

def rebuild(body, rig, mats):
    # Retain the supplied face, replacing all anatomy below the neck with a clothed body.
    bm = bmesh.new(); bm.from_mesh(body.data)
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if v.co.z < 1.19], context='VERTS')
    bm.to_mesh(body.data); bm.free()
    for v in body.data.vertices:
        v.co.x *= .92; v.co.y *= .92; v.co.z = 1.22 + (v.co.z-1.22)*.92
    for p in body.data.polygons: p.material_index = 0
    positions = [v.co.copy() for v in body.data.vertices]
    targets = {key: [v.copy() for v in positions] for key in ['Slim','Broad']}
    faces = [tuple(p.vertices) for p in body.data.polygons]
    materials = [0 for p in faces]
    uvs = [[tuple(body.data.uv_layers.active.data[i].uv) for i in p.loop_indices] for p in body.data.polygons]
    weights = [{'Head':1} for v in positions]
    sides = 32

    def add_mesh(versions, polygons, material, vertex_weights):
        start = len(positions)
        positions.extend(Vector(p) for p in versions[1])
        targets['Slim'].extend(Vector(p) for p in versions[0])
        targets['Broad'].extend(Vector(p) for p in versions[2])
        faces.extend(tuple(start+i for i in p) for p in polygons)
        materials.extend([material]*len(polygons))
        uvs.extend([[(0,0)]*len(p) for p in polygons])
        weights.extend(vertex_weights)

    def loft(profiles, material, weight_fn, cap=True):
        # profile row: z, center x/y, x/y radii; same rows at all three sizes.
        versions=[]
        for rows in profiles:
            verts=[]
            for z,x,y,rx,ry in rows:
                for j in range(sides):
                    a=2*math.pi*j/sides
                    verts.append((x+rx*math.cos(a),y+ry*math.sin(a),z))
            versions.append(verts)
        rings=len(profiles[1]); polys=[]
        for i in range(rings-1):
            for j in range(sides):
                a=i*sides+j; b=i*sides+(j+1)%sides
                polys.append((a,b,b+sides,a+sides))
        if cap:
            polys.append(tuple(reversed(range(sides))))
            polys.append(tuple((rings-1)*sides+j for j in range(sides)))
        add_mesh(versions,polys,material,[weight_fn(Vector(v)) for v in versions[1]])

    def torso_weights(v):
        if v.z>1.09:return {'Chest':1}
        if v.z>.85:
            t=(v.z-.85)/.24;return {'Chest':t,'Spine':1-t}
        t=max(0,min(1,(v.z-.66)/.19));return {'Spine':t,'Hips':1-t}

    def leg_weights(v,side):
        if v.z>.56:
            t=min(1,(v.z-.56)/.13);return {'Hips':t,'UpperLeg.'+side:1-t}
        if v.z>.40:return {'UpperLeg.'+side:1}
        if v.z>.30:
            t=(v.z-.30)/.1;return {'UpperLeg.'+side:t,'LowerLeg.'+side:1-t}
        if v.z>.15:return {'LowerLeg.'+side:1}
        t=max(0,min(1,(v.z-.10)/.05));return {'LowerLeg.'+side:t,'Foot.'+side:1-t}

    # Real crew-neck opening and generous single chest surface. Body size fills
    # the waist and abdomen rather than multiplying the original bust and hips.
    shirt=[]
    for width,depth in [(.163,.099),(.19,.12),(.229,.155)]:
        shirt.append([
            (.655,0,0,width,depth),(.67,0,0,width+.002,depth+.002),
            (.72,0,0,width,depth),(.83,0,-.002,width*.98,depth),
            (.94,0,0,width*.99,depth),
            (1.045,0,0,.188+(width-.163)*.38,.109+(depth-.099)*.3),
            (1.10,0,0,.187+(width-.163)*.3,.097+(depth-.099)*.22),
            (1.145,0,0,.132,.076),(1.166,0,0,.065,.053)])
    loft(shirt,1,torso_weights,False)
    # Collar and hem are raised bands, not a height-based paint assignment.
    loft([[ (z,0,0,rx,ry) for z,rx,ry in [(1.156,.067,.055),(1.172,.066,.054)]]]*3,5,lambda v:{'Chest':1},False)
    loft([[ (z,0,0,rows[0][3]+.009,rows[0][4]+.009) for z in [.657,.67]] for rows in shirt],5,torso_weights,False)
    loft([[(1.13,0,0,.053,.048),(1.22,0,0,.057,.050)]]*3,4,lambda v:{'Chest':max(0,(1.22-v.z)/.09),'Head':min(1,(v.z-1.13)/.09)})

    # Connected shorts: branch a shared waist ring into two leg openings with a
    # welded crotch. Shared vertices prevent a split pelvis in the golf swing.
    versions=[]
    for w,d in [(.164,.106),(.19,.128),(.232,.16)]:
        verts=[]
        for z in [.69,.66,.60]:
            for j in range(32):
                a=2*math.pi*j/32;verts.append((w*math.cos(a),d*math.sin(a),z))
        # Crotch strip connects front/back seam at a comfortable inseam height.
        for j in range(1,8):verts.append((0,d*math.cos(math.pi*j/8),.52))
        for sign in [1,-1]:
            for z in [.49,.445,.432]:
                for j in range(24):
                    a=2*math.pi*j/24
                    verts.append((sign*.11+(w-.097)*math.cos(a),d*.87*math.sin(a),z))
        versions.append(verts)
    polys=[]
    for i in range(2):
        for j in range(32):polys.append((i*32+j,i*32+(j+1)%32,(i+1)*32+(j+1)%32,(i+1)*32+j))
    # Ring order counterclockwise for each leg. 17 outer waist vertices and
    # seven shared inner vertices form each upper opening.
    right=[64+j%32 for j in range(24,41)]+[96+j for j in range(7)]
    left=[64+j for j in range(8,25)]+[96+j for j in reversed(range(7))]
    # These start at the bottom / top of the waist; rotate the lower rings to match.
    for index,top in enumerate([right,left]):
        offset=103+index*72;shift=18 if index==0 else 6
        for j in range(24):polys.append((top[j],top[(j+1)%24],offset+(j+1+shift)%24,offset+(j+shift)%24))
        for r in range(2):
            for j in range(24):polys.append((offset+r*24+j,offset+r*24+(j+1)%24,offset+(r+1)*24+(j+1)%24,offset+(r+1)*24+j))
    # Geometry above was authored from waist downward: reverse its winding.
    polys=[tuple(reversed(p)) for p in polys]
    ws=[]
    for v in versions[1]:
        v=Vector(v); side='L' if v.x>0 else 'R'; w=leg_weights(v,side)
        center=max(0,1-abs(v.x)/.085)*max(0,min(1,(v.z-.44)/.14))
        w={k:val*(1-center) for k,val in w.items()};w['Hips']=w.get('Hips',0)+center;ws.append(w)
    add_mesh(versions,polys,2,ws)

    for side,sign in [('L',1),('R',-1)]:
        # Visible legs beneath the shorts; no source anatomy pokes through clothing.
        profiles=[]
        for radius in [.048,.057,.07]:
            profiles.append([(z,sign*x,0,radius*rx,radius*ry) for z,x,rx,ry in [
                (.12,.125,.65,.75),(.18,.124,.72,.85),(.25,.12,.91,.99),
                (.34,.117,.88,.93),(.40,.114,1,1.05),(.47,.108,1.1,1.13)]])
        loft(profiles,4,lambda v,s=side:leg_weights(v,s))
        # Sock and footwear: flattened outsole, rounded toe box and raised heel.
        loft([[(.10,sign*.125,0,.041,.041),(.17,sign*.124,0,.041,.04)]]*3,5,lambda v,s=side:leg_weights(v,s))
        shoe=[(.018,sign*.125,-.06,.071,.143),(.029,sign*.125,-.06,.077,.149),
              (.058,sign*.125,-.06,.077,.147),(.082,sign*.125,-.055,.071,.134),
              (.115,sign*.125,-.027,.054,.096),(.14,sign*.125,.002,.043,.049)]
        loft([shoe]*3,3,lambda v,s=side:{'Foot.'+s:1})
        loft([[row for row in shoe[:3]]]*3,5,lambda v,s=side:{'Foot.'+s:1})
        # Continuous tapered arm with a cloth sleeve around the upper section.
        shoulder=rig.data.bones['UpperArm.'+side].head_local.copy()
        elbow=rig.data.bones['LowerArm.'+side].head_local.copy()
        wrist=rig.data.bones['Hand.'+side].head_local.copy()
        def arm_surface(sleeve):
            paths=[]; ws=[]
            ts=[-.16,0,.18,.38,.52] if sleeve else [.40+i*.60/16 for i in range(17)]
            for size in range(3):
                verts=[]
                for t in ts:
                    center=shoulder.lerp(elbow,t*2) if t<=.5 else elbow.lerp(wrist,(t-.5)*2)
                    if sleeve and t<0:center=Vector((sign*.13,0,1.095))
                    elif sleeve:center.z-=.042*max(0,1-t/.38)
                    tangent=(elbow-shoulder if t<.5 else wrist-elbow).normalized()
                    front=Vector((0,-1,0));cross=tangent.cross(front).normalized()
                    radius=(.070+(size-1)*.009)*(1-t*.36) if sleeve else (.050+(size-1)*.006)*(1-t*.45)
                    if t<0:radius*=.76
                    for j in range(24):
                        a=2*math.pi*j/24;verts.append(center+radius*(front*math.cos(a)+cross*math.sin(a)))
                paths.append(verts)
            polygons=[]
            for r in range(len(ts)-1):
                for j in range(24):polygons.append((r*24+j,r*24+(j+1)%24,(r+1)*24+(j+1)%24,(r+1)*24+j))
            polygons.append(tuple(reversed(range(24))))
            polygons.append(tuple((len(ts)-1)*24+j for j in range(24)))
            for t in ts:
                # Blend across the elbow and sleeve/shoulder connection.
                chest=max(0,min(1,(-t+.06)/.22)) if sleeve else 0
                lower=max(0,min(1,t*2));hand=max(0,min(1,(t-.5)*2))
                # Endpoint weights follow the solved elbow/wrist translations. The runtime
                # lengthens the arms, so a rigid forearm would stop short of the hand.
                w={'UpperArm.'+side:(1-lower)*(1-chest),
                   'LowerArm.'+side:lower*(1-hand)*(1-chest),
                   'Hand.'+side:hand*(1-chest),'Chest':chest}
                ws.extend([w]*24)
            add_mesh(paths,polygons,1 if sleeve else 4,ws)
        arm_surface(False);arm_surface(True)

    mesh=bpy.data.meshes.new('Sportswear body with authored sizes')
    mesh.from_pydata(positions,[],faces);mesh.update()
    for m in mats:mesh.materials.append(m)
    layer=mesh.uv_layers.new()
    for p,mi,uv in zip(mesh.polygons,materials,uvs):
        p.material_index=mi;p.use_smooth=True
        for li,q in zip(p.loop_indices,uv):layer.data[li].uv=q
    body.data=mesh;body.vertex_groups.clear()
    groups={b.name:body.vertex_groups.new(name=b.name) for b in rig.data.bones}
    for i,ws in enumerate(weights):
        total=sum(ws.values())
        for name,w in ws.items():
            if w>0:groups[name].add([i],w/total,'REPLACE')
    # Recalculate closed garment normals after the authored branching seam.
    bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(mesh);bm.free()
    body.shape_key_add(name='Basis')
    for name,vertices in targets.items():
        key=body.shape_key_add(name=name)
        for v,co in zip(key.data,vertices):v.co=co
    return body
