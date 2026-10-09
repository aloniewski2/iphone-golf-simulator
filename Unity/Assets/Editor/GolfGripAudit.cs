#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using GolfArcade.Game;
using GolfArcade.Tennis;
using UnityEngine;
using UnityEditor;
namespace GolfArcade.EditorTools {
 public static class GolfGripAudit {
  [Serializable] public class Row {public string body,clip,hand;public int frame;public float time,beforeAngle,afterAngle,wristShift,elbowShift,segmentError,clubError,digitError,repeatError;}
  [Serializable] public class Report {public List<Row> samples=new();public int poses;public string status;}
  public static void Run(){try{Execute();EditorApplication.Exit(0);}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
  public static void Execute(){
   var report=new Report();
   foreach(bool female in new[]{false,true}){
    var hero=HeroGolfer.Build(null,new HeroLook{Female=female,Haircut=0,Skin=new Color(.95f,.82f,.69f)});var look=hero.Root.GetComponent<MatchHeroLook>();
    try{
     var transforms=hero.Root.GetComponentsInChildren<Transform>(true);var club=transforms.First(t=>t.name=="Club");
     var digits=transforms.Where(t=>new[]{"Thumb","Index","Middle","Ring","Little"}.Any(d=>t.name.Contains(d))).ToArray();
     var skins=hero.Root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r=>r.name.StartsWith("CLUB_")).ToArray();
     var clips=Resources.LoadAll<AnimationClip>(HeroGolfer.PathFor(female));
     foreach(var name in new[]{"Drive","IronSwing","HalfSwing","Chip","Putt"}){
      string equipment=name=="Drive"?"DRIVER":name=="Putt"?"PUTTER":name=="IronSwing"?"IRON":"WEDGE";
      foreach(var s in skins)s.gameObject.SetActive(s.name.Equals("CLUB_"+equipment,StringComparison.OrdinalIgnoreCase));
      var clip=clips.First(c=>c.name==name);int count=Mathf.CeilToInt(clip.length*120);
      for(int frame=0;frame<=count;frame++){
       float time=Mathf.Min(frame/120f,clip.length);clip.SampleAnimation(hero.Root,time);
       var beforeClub=club.localToWorldMatrix;var digitRot=digits.Select(t=>t.localRotation).ToArray();var digitPos=digits.Select(t=>t.localPosition).ToArray();
       var rows=new List<Row>();var original=new List<Vector3[]>();
       foreach(bool left in new[]{false,true}){
        string side=left?"Left":"Right";var u=hero.Bones[side+"UpperArm"];var l=hero.Bones[side+"LowerArm"];var h=hero.Bones[side+"Hand"];var m=hero.Bones[side+"MiddleProximal"];
        rows.Add(new Row{body=female?"female":"male",clip=name,hand=side,frame=frame,time=time,beforeAngle=Vector3.Angle(h.position-l.position,m.position-h.position)});
        original.Add(new[]{u.position,l.position,h.position});
       }
       HeroGripPolish.ApplyGrip(look);var output=transforms.Select(t=>t.localToWorldMatrix).ToArray();
       float ce=0,de=0;for(int k=0;k<16;k++)ce=Mathf.Max(ce,Mathf.Abs(beforeClub[k]-club.localToWorldMatrix[k]));
       for(int k=0;k<digits.Length;k++){de=Mathf.Max(de,Quaternion.Angle(digitRot[k],digits[k].localRotation));if(Vector3.Distance(digitPos[k],digits[k].localPosition)>.00001f)throw new Exception("Digit position changed");}
       int index=0;foreach(bool left in new[]{false,true}){
        string side=left?"Left":"Right";var u=hero.Bones[side+"UpperArm"];var l=hero.Bones[side+"LowerArm"];var h=hero.Bones[side+"Hand"];var m=hero.Bones[side+"MiddleProximal"];var o=original[index];var row=rows[index++];
        row.afterAngle=Vector3.Angle(h.position-l.position,m.position-h.position);row.wristShift=Vector3.Distance(o[2],h.position);row.elbowShift=Vector3.Distance(o[1],l.position);
        row.segmentError=Mathf.Max(Mathf.Abs(Vector3.Distance(o[0],o[1])-Vector3.Distance(u.position,l.position)),Mathf.Abs(Vector3.Distance(o[1],o[2])-Vector3.Distance(l.position,h.position)));row.clubError=ce;row.digitError=de;
        if(row.segmentError>.001f||ce>.00002f||de>.1f||row.afterAngle>row.beforeAngle+.15f)throw new Exception($"Grip invariant failed {row.body} {name} {frame} {side}: length={row.segmentError} club={ce} digits={de} angle={row.beforeAngle}/{row.afterAngle}");
        report.samples.Add(row);
       }
       HeroGripPolish.ApplyGrip(look);float repeat=0;for(int b=0;b<transforms.Length;b++)for(int k=0;k<16;k++)repeat=Mathf.Max(repeat,Mathf.Abs(output[b][k]-transforms[b].localToWorldMatrix[k]));
       foreach(var row in rows)row.repeatError=repeat;if(repeat>.00002f)throw new Exception("Repeated grip solve accumulated movement "+repeat);
       report.poses++;
      }
     }
    }finally{UnityEngine.Object.DestroyImmediate(hero.Root);}
   }
   report.status="PASS";string path=Environment.GetEnvironmentVariable("GRIP_AUDIT_OUT");if(!string.IsNullOrEmpty(path))File.WriteAllText(path,JsonUtility.ToJson(report,true));Debug.Log("[GolfGripAudit] PASS "+report.poses+" poses, both bodies and all five swing families at 120 Hz; club/digits/segment lengths/idempotence checked");
  }
 }
}
#endif
