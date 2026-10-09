import os,sys
os.environ['JUICE']='Idle'
src=open(os.path.join(os.path.dirname(os.path.abspath(__file__)),'author_juice.py')).read()
src=src[:src.index("ready_frames,_=build2")]
exec(src)
met={}
Q,dp=pose_at(CLIPS['Idle']['keys'],0,met);hh=dp+HEAD['Hips'];pos=fk(Q,hh)
print('DBG hand R',tuple(round(x,3) for x in pos['Hand.R']),'shoulder',tuple(round(x,3) for x in pos['UpperArm.R']),'chest',tuple(round(x,3) for x in pos['Chest']))
Rk=(Q['Hand.R'].to_matrix()@SR)
print('DBG racketY',tuple(round(x,2) for x in Rk.col[1]),'normal',tuple(round(x,2) for x in Rk.col[2]))
k=CLIPS['Idle']['keys'][0];print('DBG key0',{a:k[a] for a in ('handR','elbowR','rdir','handL') if a in k})
g=lambda ch,d:interp(CLIPS['Idle']['keys'],0,ch,d)
print('DBG interp handR',g('handR',None))
Q2={n:RQ[n] for n in ORDER};pos2=fk(Q2,HEAD['Hips'])
t=pos2['Chest']+Vector(g('handR',None));m,e=two_bone(pos2['UpperArm.R'],t,LEN['UpperArm.R'],LEN['LowerArm.R'],Vector((-.4,.8,-.2)))
limb(Q2,'UpperArm.R','LowerArm.R',m-pos2['UpperArm.R'],e-m,Vector((0,-1,0)),None);p3=fk(Q2,HEAD['Hips'])
print('DBG ik target',tuple(round(x,3) for x in t),'ik end',tuple(round(x,3) for x in e),'fk hand',tuple(round(x,3) for x in p3['Hand.R']),'fk elbow',tuple(round(x,3) for x in p3['LowerArm.R']),'mid',tuple(round(x,3) for x in m))
