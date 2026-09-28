"""Targeted knee weights and pose correction. Original V4 mesh coordinates stay locked."""
import bpy,json,math,bmesh
from pathlib import Path
from mathutils import Matrix,Vector,Quaternion
P=Path(__file__).resolve().parents[1];A=P.parent
bpy.ops.wm.open_mainfile(filepath=str(A/'Hero_01_Mixamo_QA.blend'))
rig=bpy.data.objects['Hero_01_Rig'];body=bpy.data.objects['Body_Skin'];scene=bpy.context.scene
rig.animation_data_clear()
for pb in rig.pose.bones:pb.matrix_basis=Matrix.Identity(4)
bpy.context.view_layer.update()
report={'topology_changed':False,'rest_vertices_changed':0,'DQ':False,'weight_groups':['UpperLeg.L','LowerLeg.L','UpperLeg.R','LowerLeg.R'],'weights':{}}
def smooth(t):t=max(0,min(1,t));return t*t*(3-2*t)
# Existing knee surface is dense. Reweight every surface at the same anatomical height,
# including inner foundation, instead of only the disconnected visible leg patch.
for side in ['L','R']:
 upper='UpperLeg.'+side;lower='LowerLeg.'+side;count=0
 for v in body.data.vertices:
  old={body.vertex_groups[g.group].name:g.weight for g in v.groups if body.vertex_groups[g.group].name in rig.data.bones}
  total=old.get(upper,0)+old.get(lower,0)
  if total<.8 or not .29<v.co.z<.59:continue
  t=smooth((v.co.z-.31)/.26)
  body.vertex_groups[upper].add([v.index],total*t,'REPLACE');body.vertex_groups[lower].add([v.index],total*(1-t),'REPLACE');count+=1
 report['weights'][side]={'vertices':count,'falloff_z_m':[.31,.57]}
# Keep normal edits local, preserving face/wardrobe normals.
me=body.data;old=[n.vector.copy() for n in me.corner_normals];bm=bmesh.new();bm.from_mesh(me);bm.normal_update();bm.verts.ensure_lookup_table()
ns={v.index:v.normal.copy() for v in bm.verts};bm.free()
me.normals_split_custom_set([ns[l.vertex_index] if .29<me.vertices[l.vertex_index].co.z<.59 else old[l.index] for l in me.loops])
# Keep LBS for preview parity with the shipping renderer; Blender DQ does not transfer via FBX.
bpy.ops.object.select_all(action='DESELECT');rig.select_set(True);body.select_set(True);bpy.context.view_layer.objects.active=rig
bpy.ops.export_scene.fbx(filepath=str(P/'Hero_01_KneeBody.fbx'),use_selection=True,object_types={'MESH','ARMATURE'},add_leaf_bones=False,bake_anim=False,axis_forward='-Z',axis_up='Y')
bpy.ops.wm.save_as_mainfile(filepath=str(P/'Hero_01_Knees_QA.blend'))
data=json.loads((A/'gameplay_fixed/gameplay_pose_bake.json').read_text());names=data['bones'];C=Matrix(((-1,0,0),(0,0,1),(0,-1,0)));CI=C.inverted()
def q(v):return Quaternion((v['w'],v['x'],v['y'],v['z']))
def vec(v):return Vector((v['x'],v['y'],v['z']))
def outq(v):return dict(x=v.x,y=v.y,z=v.z,w=v.w)
def outv(v):return dict(x=v.x,y=v.y,z=v.z)
rest={n:q(v) for n,v in zip(names,data['restRotations'])}
bindpos={b.name:C@b.head_local for b in rig.data.bones}
def swing(base,axis,target):return (base@axis).rotation_difference(target.normalized())@base
stats={}
for clip in data['clips']:
 maxright=0;maxwrist=0;kneeshift=0;minclear=100;previousWrist=None;previousElbow=None;previousHand=None
 for frame in clip['frames']:
  ps={n:vec(v) for n,v in zip(names,frame['positions'])};qs={n:q(v) for n,v in zip(names,frame['rotations'])}
  hipframe=qs['Hips']@rest['Hips'].inverted();chestframe=qs['Chest']@rest['Chest'].inverted()
  # Keep the original hip and both ankle targets. Reconstruct knee pole toward foot heading.
  for side in ['L','R']:
   a,b,c='UpperLeg.'+side,'LowerLeg.'+side,'Foot.'+side
   root=ps[a];end=ps[c];axis=(end-root).normalized();la=(bindpos[b]-bindpos[a]).length;lb=(bindpos[c]-bindpos[b]).length
   d=min((end-root).length,la+lb-.0001);along=(la*la-lb*lb+d*d)/(2*d);height=math.sqrt(max(0,la*la-along*along))
   footframe=qs[c]@rest[c].inverted();forward=footframe@Vector((0,0,1));forward.y=0
   if forward.length<.01:forward=hipframe@Vector((0,0,1))
   pole=forward-axis*forward.dot(axis);pole.normalize()
   sourcepole=ps[b]-root;sourcepole-=axis*sourcepole.dot(axis)
   if sourcepole.length>.001:
    sourcepole.normalize();angle=sourcepole.angle(pole);amount=min(.8,math.radians(28)/max(.0001,angle))
    pole=sourcepole.lerp(pole,amount).normalized()
   knee=root+axis*along+pole*height;kneeshift=max(kneeshift,(knee-ps[b]).length);ps[b]=knee
   qs[a]=swing(hipframe,bindpos[b]-bindpos[a],knee-root)@rest[a]
   qs[b]=swing(hipframe,bindpos[c]-bindpos[b],end-knee)@rest[b]
  # Clearance in chest coordinates: torso capsule surface plus a toy fist gap.
  if True:
   a,b,c='UpperArm.L','LowerArm.L','Hand.L';root=ps[a];oldhand=ps[c].copy();wrist=oldhand.copy()
   forward=chestframe@Vector((0,0,1));left=chestframe@Vector((-1,0,0));up=chestframe@Vector((0,1,0))
   # Turn left clavicle 12 degrees forward; keep its length and shared hierarchy.
   clav=Quaternion(up,math.radians(12));shoulder=ps['Shoulder.L'];root=shoulder+clav@(root-shoulder)
   qs['Shoulder.L']=clav@qs['Shoulder.L'];ps[a]=root
   center=(ps['Hips']+ps['Chest'])*.5
   rel=wrist-center;x=rel.dot(left);z=rel.dot(forward)
   # Front half ellipse: avoid chest penetration while retaining across-body path.
   need=.345*math.sqrt(max(0,1-(x/.40)**2))
   if z<need:wrist+=forward*(need-z)
   if previousWrist is not None and clip['name']!='Backhand':
    delta=wrist-previousWrist
    if delta.length>.065:wrist=previousWrist+delta.normalized()*.065
    rel=wrist-center;x=rel.dot(left);z=rel.dot(forward);need=.345*math.sqrt(max(0,1-(x/.40)**2))
    if z<need:wrist+=forward*(need-z)
   la=(bindpos[b]-bindpos[a]).length;lb=(bindpos[c]-bindpos[b]).length
   axis=wrist-root;d=min(max(axis.length,.19),la+lb-.014);axis.normalize();wrist=root+axis*d
   pole=ps[b]-root;pole-=axis*pole.dot(axis)
   if pole.length<.001:pole=left-axis*left.dot(axis)
   pole.normalize();along=(la*la-lb*lb+d*d)/(2*d);height=math.sqrt(max(0,la*la-along*along))
   torsoA=ps['Hips']+up*.06;torsoB=ps['Chest']+up*.06;torsoAxis=torsoB-torsoA
   def clearance(pt):
    t=max(0,min(1,(pt-torsoA).dot(torsoAxis)/torsoAxis.length_squared))
    return (pt-torsoA-t*torsoAxis).length-.17-.05
   best=None
   for degrees in range(-180,181,5):
    testpole=Quaternion(axis,math.radians(degrees))@pole;testelbow=root+axis*along+testpole*height
    gap=min(clearance(testelbow.lerp(wrist,t)) for t in [0,.25,.5,.75,1])
    cost=max(0,.045-gap)*1000+abs(degrees)*.001
    if previousElbow is not None and clip['name']!='Backhand':cost+=(testelbow-previousElbow).length*8
    if best is None or cost<best[0]:best=(cost,testelbow,gap)
   elbow=best[1];minclear=min(minclear,best[2])
   oldlower=qs[b].copy();qs[a]=swing(chestframe,bindpos[b]-bindpos[a],elbow-root)@rest[a]
   qs[b]=swing(chestframe,bindpos[c]-bindpos[b],wrist-elbow)@rest[b]
   qs[c]=qs[b]@oldlower.inverted()@qs[c]
   if previousHand is not None:
    angle=previousHand.rotation_difference(qs[c]).angle
    qs[c]=previousHand.slerp(qs[c],min(1,math.radians(30)/max(.0001,angle)))
   ps[b]=elbow;ps[c]=wrist;previousWrist=wrist.copy();previousElbow=elbow.copy();previousHand=qs[c].copy()
   maxwrist=max(maxwrist,(wrist-oldhand).length)
   # Preserve hand/end bone position inherited from corrected wrist; no fingers in this rig.
  for n in ['Shoulder.R','UpperArm.R','LowerArm.R','Hand.R']:
   idx=names.index(n);maxright=max(maxright,qs[n].rotation_difference(q(frame['rotations'][idx])).angle,(ps[n]-vec(frame['positions'][idx])).length)
  frame['positions']=[outv(ps[n]) for n in names];frame['rotations']=[outq(qs[n]) for n in names]
 stats[clip['name']]={'max_knee_pole_shift_m':kneeshift,'max_left_wrist_shift_m':maxwrist,'right_arm_change':maxright,'min_offarm_proxy_clearance_m':minclear}
(P/'gameplay_pose_bake.json').write_text(json.dumps(data));report['clips']=stats;(P/'fix_report.json').write_text(json.dumps(report,indent=2))
import sys
if '--poses-only' in sys.argv:sys.exit(0)
# Reuse the verified Unity→Blender bake with the newly weighted bind and corrected poses.
code=(A/'gameplay_fixed/tools/bake_gameplay.py').read_text().replace("A/'Hero_01_Mixamo_QA.blend'","P/'Hero_01_Knees_QA.blend'")
exec(compile(code,str(P/'tools/bake.py'),'exec'),{'__file__':str(P/'tools/bake.py')})
