using UnityEngine;
namespace GolfArcade.Tennis {
/// Fits the existing gameplay wrist path to V4. No root, torso, leg, timing or hit changes.
public sealed class V4GameplayArmRacket : MonoBehaviour {
 public Transform model,upper,lower,hand,racket;
 public Vector3 socketPosition; // Bone-local import units, measured from the bound hand skin.
 public Vector3 socketEuler=new Vector3(0,0,-90);
 Quaternion upperRest,lowerRest,handRest,chestRest;
 Vector3 upperAxis,lowerAxis;float upperLength,lowerLength;
 public float WristAngle {get;private set;} public float ForearmTwist {get;private set;}
 public void Configure(Transform root,Transform a,Transform b,Transform c,Transform prop,
   Quaternion ar,Quaternion br,Quaternion cr,Quaternion chestBind,Vector3 upperBind,Vector3 lowerBind,Vector3 socket){
  model=root;upper=a;lower=b;hand=c;racket=prop;upperRest=ar;lowerRest=br;handRest=cr;chestRest=chestBind;upperAxis=upperBind.normalized;lowerAxis=lowerBind.normalized;
  upperLength=Vector3.Distance(a.position,b.position);lowerLength=Vector3.Distance(b.position,c.position);socketPosition=socket;
  racket.SetParent(hand,false);racket.localPosition=socketPosition;racket.localRotation=Quaternion.Euler(socketEuler);
  var unit=hand.lossyScale;racket.localScale=new Vector3(1/Mathf.Abs(unit.x),1/Mathf.Abs(unit.y),1/Mathf.Abs(unit.z));
 }
 public void Fit(Transform chest,Vector3 wrist,Vector3 sourceElbow,Quaternion racketWorld,float carry,bool running){
  Vector3 shoulder=upper.position;
  if(carry>0){
   Vector3 clear=running?wrist+model.right*.045f+model.forward*.075f:chest.position+model.right*.31f+model.forward*.29f-model.up*.10f;
   wrist=Vector3.Lerp(wrist,clear,carry);
   var readyUp=(model.up*.94f+model.forward*.34f).normalized;
   racketWorld=Quaternion.Slerp(racketWorld,Quaternion.LookRotation(Vector3.ProjectOnPlane(model.forward,readyUp),readyUp),carry);
  }
  float minimumDistance=Mathf.Sqrt(upperLength*upperLength+lowerLength*lowerLength-2*upperLength*lowerLength*Mathf.Cos(55*Mathf.Deg2Rad));
  Vector3 axis=wrist-shoulder;float distance=Mathf.Clamp(axis.magnitude,minimumDistance,upperLength+lowerLength-.012f);axis.Normalize();wrist=shoulder+axis*distance;
  Vector3 pole=sourceElbow-shoulder;pole=Vector3.ProjectOnPlane(pole,axis);
  Vector3 outward=Vector3.ProjectOnPlane(model.right+model.forward*.30f,axis).normalized;
  if(pole.sqrMagnitude<.0001f)pole=outward;
  pole=Vector3.Slerp(pole.normalized,outward,carry*.85f).normalized;
  float along=(upperLength*upperLength-lowerLength*lowerLength+distance*distance)/(2*distance);
  Vector3 elbow=shoulder+axis*along+pole*Mathf.Sqrt(Mathf.Max(.000001f,upperLength*upperLength-along*along));
  Vector3 u=(elbow-shoulder).normalized,l=(wrist-elbow).normalized;
  Quaternion chestFrame=chest.rotation*Quaternion.Inverse(chestRest);
  Quaternion qa=Quaternion.FromToRotation(chestFrame*upperAxis,u)*chestFrame*upperRest;
  Quaternion qb=Quaternion.FromToRotation(chestFrame*lowerAxis,l)*chestFrame*lowerRest;
  Quaternion neutral=qb*Quaternion.Inverse(lowerRest)*handRest;
  Quaternion desired=racketWorld*Quaternion.Inverse(Quaternion.Euler(socketEuler));
  Quaternion difference=desired*Quaternion.Inverse(neutral);
  if(difference.w<0)difference=new Quaternion(-difference.x,-difference.y,-difference.z,-difference.w);
  float projection=Vector3.Dot(new Vector3(difference.x,difference.y,difference.z),l);
  float twist=Mathf.Clamp(2*Mathf.Atan2(projection,difference.w)*Mathf.Rad2Deg,-55,55);
  ForearmTwist=twist*.75f;
  upper.rotation=Quaternion.AngleAxis(twist*.25f,u)*qa;
  lower.rotation=Quaternion.AngleAxis(ForearmTwist,l)*qb;
  neutral=lower.rotation*Quaternion.Inverse(lowerRest)*handRest;
  // Wrist never folds back to satisfy an incompatible old racket animation.
  hand.rotation=Quaternion.RotateTowards(neutral,desired,28);
  WristAngle=Quaternion.Angle(neutral,hand.rotation);
  // Single socket for every pose. No world-space racket override follows this.
  racket.localPosition=socketPosition;racket.localRotation=Quaternion.Euler(socketEuler);
 }
}
}
