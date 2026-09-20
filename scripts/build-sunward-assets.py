"""Editable Sunward source. Run in Blender; REST is injected from the game's pose export.
Coordinates enter as rig units, then convert to metres (0.32 yards per unit).
Geometry is original; Higgsfield art-direction references are documented in provenance.
"""
import bpy, math
from mathutils import Vector
if 'REST' not in globals():
 import json
 from pathlib import Path
 REST=json.loads(Path(__file__).with_name('sunward-rest-pose.json').read_text())

SCALE = 0.292608
LINKS = [('root','neck'),('neck','nose'),('leftShoulder','leftElbow'),
 ('leftElbow','leftWrist'),('rightShoulder','rightElbow'),('rightElbow','rightWrist'),
 ('leftHip','leftKnee'),('leftKnee','leftAnkle'),('rightHip','rightKnee'),
 ('rightKnee','rightAnkle'),('leftHip','rightHip'),('leftShoulder','rightShoulder')]
def xyz(p): return Vector((p[0]*SCALE,-p[2]*SCALE,p[1]*SCALE))
def point(name): return Vector(REST[name])
def material(name,color,roughness=.78):
 m=bpy.data.materials.new(name); m.use_nodes=True
 p=m.node_tree.nodes.get('Principled BSDF'); p.inputs['Base Color'].default_value=(*color,1)
 p.inputs['Roughness'].default_value=roughness; m.diffuse_color=(*color,1)
 return m
M={name:material(name,color) for name,color in {
 'shirt':(.045,.47,.50),'cuff':(.025,.29,.33),'trousers':(.065,.105,.17),
 'skin':(.69,.39,.21),'ivory':(.94,.91,.79),'hair':(.07,.035,.019),
 'leaf':(.10,.37,.18),'leafLight':(.25,.49,.21),'bark':(.29,.125,.052),
 'sandstone':(.77,.65,.46),'coral':(.87,.28,.17),'gold':(.91,.62,.17)}.items()}

armData=bpy.data.armatures.new('SunwardPoseRig')
rig=bpy.data.objects.new('SunwardPoseRig',armData); bpy.context.collection.objects.link(rig)
bpy.context.view_layer.objects.active=rig; rig.select_set(True); bpy.ops.object.mode_set(mode='EDIT')
for i,(a,b) in enumerate(LINKS):
 bone=armData.edit_bones.new('link_'+str(i)); bone.head=xyz(point(a)); bone.tail=xyz(point(b))
bpy.ops.object.mode_set(mode='OBJECT'); rig.select_set(False)
rig['source']='Original Sunward sculpted cartoon; Higgsfield Direction A'
rig['rig_units_to_metres']=SCALE

def mesh(name,vertices,faces,mat,weights=None):
 data=bpy.data.meshes.new(name); data.from_pydata([xyz(p) for p in vertices],[],faces); data.update()
 obj=bpy.data.objects.new(name,data); bpy.context.collection.objects.link(obj); data.materials.append(M[mat])
 for p in data.polygons: p.use_smooth=True
 if weights:
  for index in range(len(LINKS)): obj.vertex_groups.new(name='link_'+str(index))
  for vertex,ws in enumerate(weights):
   for index,value in ws.items():
    if value>0: obj.vertex_groups[index].add([vertex],value,'REPLACE')
  mod=obj.modifiers.new('Live golf pose','ARMATURE'); mod.object=rig; obj.parent=rig
 return obj

def rings(name,centers,radii,mat,bone_weights,segments=28):
 vertices=[]; faces=[]; weights=[]
 for row,(center,radius) in enumerate(zip(centers,radii)):
  axis=Vector(centers[min(row+1,len(centers)-1)])-Vector(centers[max(0,row-1)])
  axis.normalize(); u=Vector((1,0,0)); u=(u-axis*u.dot(axis)).normalized(); v=axis.cross(u).normalized()
  for col in range(segments):
   angle=col*2*math.pi/segments
   vertices.append(Vector(center)+u*(math.cos(angle)*radius[0])+v*(math.sin(angle)*radius[1]))
   weights.append(bone_weights[row])
  if row:
   for c in range(segments):
    a=(row-1)*segments+c; b=(row-1)*segments+(c+1)%segments
    faces.append((a,b,b+segments,a+segments))
 faces.append(tuple(reversed(range(segments))))
 faces.append(tuple((len(centers)-1)*segments+c for c in range(segments)))
 return mesh(name,vertices,faces,mat,weights)

# Authored torso profile: broad shoulders, tailored waist, covered back and collar opening.
profiles=[(-.04,.43,.68),(0,.51,.76),(.08,.53,.77),(.30,.51,.76),(.52,.53,.80),
 (.72,.54,.86),(.86,.48,.89),(.94,.36,.69),(1,.23,.29),(1.045,.22,.27)]
a,b=point('root'),point('neck')
rings('body_polo',[a+(b-a)*t for t,_,_ in profiles],[(d,w) for _,d,w in profiles],
 'shirt',[{0:1} for _ in profiles],36)
# Hip bridge is rounded and concealed slightly beneath the polo hem.
rings('body_waist',[a+Vector((0,y,0)) for y in [-.29,-.2,0,.12]],
 [(.29,.54),(.40,.69),(.44,.72),(.43,.70)],'trousers',[{10:1}]*4)
for side,upper,lower,hip,knee,ankle in [('left',2,3,6,7,'leftAnkle'),('right',4,5,8,9,'rightAnkle')]:
 shoulder=point(side+'Shoulder'); elbow=point(side+'Elbow'); wrist=point(side+'Wrist')
 centers=[]; radii=[]; weights=[]
 for j in range(25):
  t=j/24; pos=shoulder.lerp(elbow,t*2) if t<=.5 else elbow.lerp(wrist,(t-.5)*2)
  r=.23*(1-t)+.135*t+.025*math.sin(t*math.pi)
  blend=max(0,min(1,(t-.40)/.20)); blend=blend*blend*(3-2*blend)
  centers.append(pos); radii.append((r,r)); weights.append({upper:1-blend,lower:blend})
 rings('body_'+side+'_arm',centers,radii,'skin',weights)
 sleeveTs=[-.23,-.12,0,.12,.32,.54,.60,.62]
 rings('body_'+side+'_sleeve',[shoulder.lerp(elbow,t) for t in sleeveTs],
  [(.08,.08),(.25,.25),(.34,.34),(.37,.37),(.35,.35),(.31,.31),(.29,.29),(.285,.285)],'shirt',[{upper:1}]*8)
 rings('body_'+side+'_cuff',[shoulder.lerp(elbow,t) for t in [.535,.55,.605,.625]],
  [(.302,.302),(.302,.302),(.291,.291),(.28,.28)],'cuff',[{upper:1}]*4)
 top=point(side+'Hip'); mid=point(side+'Knee'); end=point(ankle)
 centers=[]; radii=[]; weights=[]
 for j in range(27):
  t=j/26; p=top.lerp(mid,t*2) if t<=.5 else mid.lerp(end,(t-.5)*2)
  radius=.345*(1-t)+.205*t+.015*math.sin(t*math.pi*2)
  blend=max(0,min(1,(t-.36)/.28)); blend=blend*blend*(3-2*blend)
  centers.append(p); radii.append((radius*.95,radius)); weights.append({hip:1-blend,knee:blend})
 rings('body_'+side+'_trouser',centers,radii,'trousers',weights)
rings('body_neck',[point('neck')+Vector((0,y,0)) for y in [-.08,.1,.36]],
 [(.215,.235),(.205,.22),(.195,.205)],'skin',[{1:1}]*3)

def ball(name,center,scale,mat,segments=24,rings_count=16):
 bpy.ops.mesh.primitive_uv_sphere_add(segments=segments,ring_count=rings_count,location=xyz(center))
 obj=bpy.context.object; obj.name=name; obj.scale=(scale[0]*SCALE,scale[2]*SCALE,scale[1]*SCALE)
 obj.data.materials.append(M[mat]);
 for p in obj.data.polygons:p.use_smooth=True
 return obj
def cylinder(name,a,b,radius,mat):
 aa,bb=xyz(a),xyz(b); delta=bb-aa
 bpy.ops.mesh.primitive_cone_add(vertices=12,radius1=radius*SCALE,radius2=radius*SCALE*.72,depth=delta.length,location=(aa+bb)/2)
 o=bpy.context.object; o.name=name;o.rotation_euler=delta.to_track_quat('Z','Y').to_euler();o.data.materials.append(M[mat]);return o

# A reference head in the editable workshop; game facial accessories remain procedural and tintable.
head=point('nose')
ball('preview_head',head,(.69,.77,.72),'skin')
ball('preview_hair',head+Vector((-.15,.19,0)),(.62,.61,.70),'hair')
ball('preview_cap',head+Vector((0,.43,0)),(.77,.40,.79),'ivory')
ball('preview_brim',head+Vector((.60,.34,0)),(.65,.07,.76),'ivory')
for side in [-1,1]:
 ball('preview_ear',head+Vector((0,-.02,side*.70)),(.15,.23,.13),'skin')
 ball('preview_eye_white',head+Vector((.626,.10,side*.28)),(.11,.17,.17),'ivory')
 ball('preview_eye',head+Vector((.723,.10,side*.28)),(.045,.12,.105),'hair')
ball('preview_nose',head+Vector((.74,-.05,0)),(.15,.13,.15),'skin')

# Portable branching tree. Separate named pieces remain editable in the .blend.
offset=Vector((-5.5,0,0))
def tp(p):return offset+Vector(p)
cylinder('prop_tree_trunk',tp((0,0,0)),tp((.15,5,0)),.42,'bark')
for i in range(5):
 angle=i*2.4; end=Vector((math.cos(angle)*1.8,4.1+i*.47,math.sin(angle)*1.7))
 cylinder('prop_tree_branch_'+str(i),tp((.05,2+i*.36,0)),tp(end),.19,'bark')
 o=ball('prop_tree_crown_'+str(i),tp(end+Vector((0,1.1,0))),(2.15,1.30,1.90),'leafLight' if i%2 else 'leaf',20,12)
 for v in o.data.vertices:
  p=v.co; a=math.atan2(p.y,p.x); p*=1+.08*math.sin(a*5+i)*math.sin(math.acos(max(-1,min(1,p.z))))
for i,(p,s) in enumerate([((3.5,.5,-.8),(.8,.5,.7)),((4.1,.75,0),(.65,.75,.55)),((3.6,.32,.7),(.6,.32,.5))]):
 bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2,radius=1,location=xyz(p))
 o=bpy.context.object;o.name='prop_rock_'+str(i);o.scale=(s[0]*SCALE,s[2]*SCALE,s[1]*SCALE);o.data.materials.append(M['sandstone'])

scene=bpy.context.scene;scene.unit_settings.system='METRIC';scene.render.engine='BLENDER_EEVEE'
scene.render.resolution_x=960;scene.render.resolution_y=720;scene.render.resolution_percentage=100
scene.render.fps=60;scene.frame_start=1;scene.frame_end=300
if scene.world is None:
    scene.world=bpy.data.worlds.new('SunwardWorld')
scene.world.color=(.22,.25,.26)
for name,kind,loc,energy,color in [('Sunward key','SUN',(6,-4,10),2.5,(1,.90,.74)),('Cool fill','POINT',(-3,-4,5),100,(.65,.81,1))]:
 data=bpy.data.lights.new(name,kind);data.energy=energy;data.color=color
 obj=bpy.data.objects.new(name,data);bpy.context.collection.objects.link(obj);obj.location=loc
 obj.rotation_euler=(.45,-.5,-.65)
cameraData=bpy.data.cameras.new('Asset delivery camera');camera=bpy.data.objects.new('Asset delivery camera',cameraData)
bpy.context.collection.objects.link(camera);camera.location=(4,-6,3.4)
camera.rotation_euler=(Vector((-.2,0,1.05))-camera.location).to_track_quat('-Z','Y').to_euler();cameraData.type='ORTHO';cameraData.ortho_scale=6.2;scene.camera=camera
result={'meshes':len([o for o in bpy.data.objects if o.type=='MESH']), 'bones':len(armData.bones),'unitsToMetres':SCALE}
