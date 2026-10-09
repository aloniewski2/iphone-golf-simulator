"""Rebuild the avatars' torso, shoulders and arms as one connected deforming surface.

The previous arm generator joined capped arm objects into the body object without
connecting their surfaces. This generator unions shoulder volumes into the torso,
then skins the continuous surface across clavicle, upper arm, elbow and wrist.
The head, lower body, UV textures and expression atlas are retained.
Called from fit_avatar.py; dimensions follow the existing animation skeleton.
"""
import math
import bpy
import bmesh
from mathutils import Vector
from real_arms import _texel, _skin_like


def rebuild(gender, rig, body, px, report):
    rig.data.pose_position = 'REST'
    bpy.context.view_layer.update()
    matrix = rig.matrix_world
    joint = lambda n: matrix @ rig.data.bones[n].head_local
    origin = rig.location.copy()
    scale = (joint('UpperArm.L') - joint('UpperArm.R')).length / .49
    names = {g.index: g.name for g in body.vertex_groups}
    me = body.data
    uvmap = me.uv_layers.active
    uv_name = uvmap.name
    uv = {}
    for loop in me.loops: uv.setdefault(loop.vertex_index, uvmap.data[loop.index].uv.copy())
    skin_uv = white_uv = None
    doomed = []
    for v in me.vertices:
        if v.index not in uv:
            doomed.append(v.index)
            continue
        weights = {names[g.group]: g.weight for g in v.groups}
        arm = sum(w for n,w in weights.items() if n.startswith(('UpperArm','LowerArm','Hand','Shoulder')))
        torso = sum(weights.get(n,0) for n in ('Spine','Chest'))
        c = _texel(px, uv.get(v.index,(0,0)))
        if arm > .5 and _skin_like(c) and (skin_uv is None or sum((c[i]-[.85,.61,.40][i])**2 for i in range(3)) < sum((_texel(px,skin_uv)[i]-[.85,.61,.40][i])**2 for i in range(3))): skin_uv = uv[v.index]
        if torso > .65 and min(c) > .65 and max(c)-min(c)<.15: white_uv = uv[v.index]
        if arm > .35 or ((torso+weights.get('Neck',0)) > .35 and weights.get('Head',0)<.35): doomed.append(v.index)
    if skin_uv is None or white_uv is None: raise RuntimeError('Avatar skin/shirt texels missing')
    bm=bmesh.new();bm.from_mesh(me);bm.verts.ensure_lookup_table()
    bmesh.ops.delete(bm,geom=[bm.verts[i] for i in doomed],context='VERTS');bm.to_mesh(me);bm.free()
    verts=[];faces=[]
    def tube(points,radii,axes=None):
        start=len(verts); count=24
        for i,(p,r) in enumerate(zip(points,radii)):
            tangent=(points[min(i+1,len(points)-1)]-points[max(i-1,0)]).normalized()
            across=Vector((0,1,0)) if axes is None else axes
            other=tangent.cross(across).normalized()
            for k in range(count):
                theta=k*2*math.pi/count
                verts.append(tuple(p+across*(math.cos(theta)*r[1])+other*(math.sin(theta)*r[0])))
        faces.append(tuple(start+k for k in range(count-1,-1,-1)))
        for j in range(len(points)-1):
            for k in range(count): faces.append((start+j*count+k,start+j*count+(k+1)%count,start+(j+1)*count+(k+1)%count,start+(j+1)*count+k))
        faces.append(tuple(start+(len(points)-1)*count+k for k in range(count)))
    # An athletic shirt silhouette, with a real chest and sloping shoulder line.
    torso=[(.79,.185,.125),(.84,.19,.13),(.94,.19,.125),(1.06,.208,.13),(1.16,.224,.12),(1.205,.218,.10),(1.25,.115,.077),(1.275,.076,.069),(1.315,.068,.067),(1.355,.067,.065)]
    tube([origin+Vector((0,0,z*scale)) for z,_,_ in torso],[(w*scale,d*scale) for _,w,d in torso])
    chains={}
    for side in 'LR':
        a,e,w=joint('UpperArm.'+side),joint('LowerArm.'+side),joint('Hand.'+side)
        fist=next(o for o in bpy.data.collections[f'V4 {gender} Tennis | BODY'].objects if o.name.startswith('V4 grip hand '+side))
        evaluated=fist.evaluated_get(bpy.context.evaluated_depsgraph_get());fm=evaluated.to_mesh()
        palm=sum((fist.matrix_world @ v.co for v in fm.vertices),Vector())/len(fm.vertices)
        evaluated.to_mesh_clear()
        handdir=(matrix @ rig.data.bones['Hand.'+side].tail_local-w).normalized()
        desired=w+handdir*.052*scale
        # The legacy floating fist sits 7 cm inside the wrist. Place its palm along
        # the anatomical wrist axis so the forearm does not kink sideways into it.
        delta=fist.matrix_world.to_3x3().inverted() @ (desired-palm)
        fist.data=fist.data.copy()
        for v in fist.data.vertices:v.co+=delta
        fist.data.update()
        palm=desired
        chains[side]=(a,e,w,handdir)
        points=[a+(origin+Vector((0,0,a.z-origin.z))-a)*.36,a,a.lerp(e,.3),a.lerp(e,.7),e,e.lerp(w,.28),e.lerp(w,.68),w,palm]
        rr=[.089,.089,.075,.060,.048,.056,.046,.034,.033]
        tube(points,[(r*scale,r*.92*scale) for r in rr])
    mesh=bpy.data.meshes.new('Connected athletic upper body');mesh.from_pydata(verts,[],faces);mesh.update()
    obj=bpy.data.objects.new('Connected athletic upper body',mesh);bpy.context.scene.collection.objects.link(obj)
    bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
    # Union the shoulder sockets into the shirt. All arm and torso vertices now share topology.
    remesh=obj.modifiers.new('Union shoulder topology','REMESH');remesh.mode='VOXEL';remesh.voxel_size=.008*scale;remesh.use_smooth_shade=True
    bpy.ops.object.modifier_apply(modifier=remesh.name)
    smooth=obj.modifiers.new('Relax surface','SMOOTH');smooth.factor=1.0;smooth.iterations=5;bpy.ops.object.modifier_apply(modifier=smooth.name)
    # Remove only tiny enclosed voxel specks, never a detached limb.
    bm=bmesh.new();bm.from_mesh(obj.data);seen=set();islands=[]
    for root in bm.verts:
        if root in seen:continue
        island=[];stack=[root];seen.add(root)
        while stack:
            v=stack.pop();island.append(v)
            for edge in v.link_edges:
                other=edge.other_vert(v)
                if other not in seen:seen.add(other);stack.append(other)
        islands.append(island)
    islands.sort(key=len,reverse=True)
    assert sum(map(len,islands[1:]))<len(islands[0])*.005, 'Disconnected limb or shoulder'
    if len(islands)>1:bmesh.ops.delete(bm,geom=[v for part in islands[1:] for v in part],context='VERTS')
    bm.to_mesh(obj.data);bm.free()
    dec=obj.modifiers.new('Mobile triangle budget','DECIMATE');dec.ratio=.60;bpy.ops.object.modifier_apply(modifier=dec.name)
    mesh=obj.data
    groups={n:obj.vertex_groups.new(name=n) for n in ('Hips','Spine','Chest','Neck','Shoulder.L','Shoulder.R','UpperArm.L','UpperArm.R','LowerArm.L','LowerArm.R','Hand.L','Hand.R')}
    def smoothstep(a,b,x):
        t=max(0,min(1,(x-a)/(b-a)));return t*t*(3-2*t)
    def trunk(p):
        z=(p.z-origin.z)/scale
        if z>1.22:
            t=smoothstep(1.22,1.33,z);return {'Chest':1-t,'Neck':t}
        if z<.95:
            t=smoothstep(.83,.95,z);return {'Hips':1-t,'Spine':t}
        t=smoothstep(.98,1.15,z)
        return {'Spine':1-t,'Chest':t}
    for v in mesh.vertices:
        p=v.co;s='L' if p.x>origin.x else 'R';a,e,w,h=chains[s]
        # Blend through a broad deltoid, so the sleeve and chest share the same shoulder.
        out=abs(p.x-origin.x)/scale
        armshare=smoothstep(.17,.27,out)
        ax=(e-a).normalized();fore=(w-e).normalized()
        te=(p-e).dot(fore)/scale;tw=(p-w).dot(h)/scale
        low=smoothstep(-.055,.055,te);hand=smoothstep(-.025,.025,tw)
        clav=(1-smoothstep(.01,.11,(p-a).dot(ax)/scale))*.25
        weights={n:q*(1-armshare) for n,q in trunk(p).items()}
        weights['Shoulder.'+s]=armshare*(1-low)*clav
        weights['UpperArm.'+s]=armshare*(1-low)*(1-clav)
        weights['LowerArm.'+s]=armshare*low*(1-hand)
        weights['Hand.'+s]=armshare*low*hand
        weights=sorted(((n,q) for n,q in weights.items() if q>.0001),key=lambda x:-x[1])[:4]
        total=sum(q for _,q in weights)
        for n,q in weights: groups[n].add([v.index],q/total,'REPLACE')
    layer=mesh.uv_layers.new(name=uv_name)
    for p in mesh.polygons:
        center=sum((mesh.vertices[i].co for i in p.vertices),Vector())/len(p.vertices)
        side='L' if center.x>origin.x else 'R';a,e,w,h=chains[side]
        along=(center-a).dot((e-a).normalized())/scale
        skin=(abs(center.x-origin.x)>.20*scale and along>.102) or center.z-origin.z>1.27*scale
        for li in p.loop_indices: layer.data[li].uv=skin_uv if skin else white_uv
        p.use_smooth=True
    mesh.materials.append(me.materials[0])
    # Prove the generated upper body is one connected component, not merely one object.
    bm=bmesh.new();bm.from_mesh(mesh);bm.verts.ensure_lookup_table();seen=set();stack=[bm.verts[0]]
    while stack:
        v=stack.pop()
        if v in seen:continue
        seen.add(v);stack.extend(e.other_vert(v) for e in v.link_edges)
    assert len(seen)==len(bm.verts), 'Disconnected rebuilt upper body'
    assert not any(e.is_boundary for e in bm.edges), 'Open rebuilt shoulder/arm surface'
    bm.free()
    count=len(mesh.vertices)
    rig.data.pose_position='POSE'
    bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);body.select_set(True);bpy.context.view_layer.objects.active=body;bpy.ops.object.join()
    report.append(f'Connected torso/shoulders/arms: {count} vertices, one closed component, replaced {len(doomed)} source vertices')
