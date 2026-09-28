using System;using System.IO;using System.Linq;using System.Collections.Generic;
using UnityEngine;using UnityEditor;using UnityEditor.SceneManagement;using GolfArcade.Tennis;using Object=UnityEngine.Object;
namespace GolfArcade.EditorTools {
/// Animator_RestoreMotion_GripFace_BHVolley review build + Play Mode capture.
/// Eyes-mocap v5 strokes, restored GameplayFixed runs, finger-rig grip, one shared racket socket.
[InitializeOnLoad] public static class HeroRestoreMotionReview {
 const string R=HeroLookImporter.Root;const string Key="HeroRestoreMotion";
 static string Out=>Path.GetFullPath("../ArtDir/screenshots/restore_motion_playmode");
 static string Cfg=>Path.GetFullPath("../ArtDir/anims/restore_motion/unity_review_config.json");
 [Serializable] public class ClipInfo{public string name;public float seconds;public float contact;public int worst;}
 [Serializable] public class HandFrame{public Vector3 rf,rw,lf,lw;}
 [Serializable] public class HandClip{public string name;public HandFrame[] frames;}
 [Serializable] public class HandRef{public HandClip[] clips;}
 static HandRef handRef;static List<float> errR,errL;
 [Serializable] public class Config{public ModularHeroLook.FingerPose[] gripRight,gripLeft;public Vector3 socketOrigin,socketY,socketZ;public float[] strokeGripRoll;public ClipInfo[] clips;}
 // Montage order: Ready, Serve, Forehand, Backhand, RunF, RunR, RunL, Volley, Smash
 static readonly string[] Names={"Ready","Serve","Forehand","Backhand","RunForward","RunRight","RunLeft","Volley","Smash"};
 static readonly int[] Inputs={-1,2,0,1,4,5,6,3,7};
 static int slot,local,global_,runtimeSlot;static bool started,posed;static double runtimeStart;static Vector3 runtimeOrigin;static float runtimeMotion,motion;static Vector3 firstHand;
 static List<string> report,runtime,manifest;static Config cfg;static float minLeftGap;static List<float> faceDots;static float minHead,minLeg;
 static HeroRestoreMotionReview(){EditorApplication.update+=Tick;}
 static int Frames(int s){var c=Clip(Names[s]);return c==null?120:Mathf.RoundToInt(c.seconds*30)+1;}
 static ClipInfo Clip(string n)=>cfg.clips.FirstOrDefault(c=>c.name==n);
 static AnimationClip Load(string path,string name,bool loop,bool configure){
  if(configure){var importer=(ModelImporter)AssetImporter.GetAtPath(path);var defs=importer.defaultClipAnimations;
   foreach(var c in defs){c.name=name;c.loopTime=loop;c.loopPose=false;c.lockRootPositionXZ=true;c.lockRootHeightY=true;c.lockRootRotation=true;c.keepOriginalPositionXZ=true;c.keepOriginalPositionY=true;c.keepOriginalOrientation=true;}
   importer.clipAnimations=defs;importer.SaveAndReimport();}
  var clip=AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview"));if(!clip.humanMotion)throw new Exception("Not Humanoid: "+path);return clip;
 }
 public static void Run(){try{
  cfg=JsonUtility.FromJson<Config>(File.ReadAllText(Cfg));
  Directory.CreateDirectory(Out);foreach(var f in Directory.GetFiles(Out,"*.png"))File.Delete(f);
  AssetDatabase.Refresh();EditorSceneManager.OpenScene("Assets/Scenes/Hero01Look.unity");var hero=Object.FindFirstObjectByType<ModularHeroLook>();
  string M=R+"Models/RestoreMotion/";
  var clips=new Dictionary<string,AnimationClip>();
  foreach(var n in new[]{"Ready","Forehand","Backhand","Serve","Volley","Smash"})clips[n]=Load(M+"Hero_"+n+"_v5.fbx","Hero_"+n+"_v5",n=="Ready",true);
  // Runs restored to the untouched GameplayFixed originals (pre off-arm / backhand-run / anim-repair edits). Import settings untouched.
  // Runs: GameplayFixed originals with ONLY the left arm moved out of the torso volume (Hero_Run*_v5).
  foreach(var n in new[]{"RunForward","RunRight","RunLeft"})clips[n]=Load(M+"Hero_"+n+"_v5.fbx","Hero_"+n+"_v5",false,true);
  File.WriteAllLines(Out+"/slot_bindings.txt",clips.Select(k=>k.Key+" -> "+AssetDatabase.GetAssetPath(k.Value)+" / "+k.Value.name+" / "+k.Value.length.ToString("F3")+"s"));
  // Finger-rig body: same V4 mesh + knee weights; Hand weights split onto 20 new finger bones.
  var src=AssetDatabase.LoadAssetAtPath<GameObject>(M+"Hero_01_FingerBody.fbx");var srcBody=src.GetComponentsInChildren<SkinnedMeshRenderer>().First(x=>x.name=="Body_Skin");
  var bones=hero.skeletonRoot.GetComponentsInChildren<Transform>().GroupBy(t=>t.name).ToDictionary(g=>g.Key,g=>g.First());
  int Depth(Transform x){int d=0;while(x.parent){d++;x=x.parent;}return d;}
  foreach(var sb in srcBody.bones.OrderBy(Depth)){
   if(bones.ContainsKey(sb.name))continue;
   var parent=bones[sb.parent.name];var t=new GameObject(sb.name).transform;t.SetParent(parent,false);t.localPosition=sb.localPosition;t.localRotation=sb.localRotation;t.localScale=sb.localScale;bones[sb.name]=t;
  }
  var body=hero.GetComponentsInChildren<SkinnedMeshRenderer>().First(x=>x.name=="Body_Skin");
  body.sharedMesh=srcBody.sharedMesh;body.bones=srcBody.bones.Select(b=>bones[b.name]).ToArray();body.quality=SkinQuality.Bone4;
  hero.gripRight=cfg.gripRight;hero.gripLeft=cfg.gripLeft;hero.strokeGripRoll=cfg.strokeGripRoll;hero.runCarry=.85f;hero.swingGripMesh=null;
  hero.idle=clips["Ready"];hero.forehand=clips["Forehand"];hero.backhand=clips["Backhand"];hero.serve=clips["Serve"];hero.volley=clips["Volley"];hero.smash=clips["Smash"];hero.runForward=clips["RunForward"];hero.runRight=clips["RunRight"];hero.runLeft=clips["RunLeft"];
  // One shared socket, computed from the same geometric hand frame as the grip.
  var hand=bones["Hand.R"];foreach(Transform c in hand)if(c.name.StartsWith("Hero_Racket"))Object.DestroyImmediate(c.gameObject);
  Vector3 L(string n)=>hand.InverseTransformPoint(bones[n].position);
  var F=(L("Middle2.R")-L("Middle1.R")).normalized;var W=Vector3.ProjectOnPlane(L("Pinky1.R")-L("Index1.R"),F).normalized;var N=Vector3.Cross(W,F).normalized;if(Vector3.Dot(N,L("Thumb1.R")-L("Index1.R"))<0)N=-N;
  Vector3 Fr(Vector3 c)=>hand.TransformDirection(W*c.x+F*c.y+N*c.z);
  var prop=new GameObject("Hero_Racket_GripSocket").transform;prop.SetParent(hand,false);
  prop.position=hand.position+Fr(cfg.socketOrigin);prop.rotation=Quaternion.LookRotation(Fr(cfg.socketZ),Fr(cfg.socketY));
  var unit=hand.lossyScale;prop.localScale=new Vector3(1/Mathf.Abs(unit.x),1/Mathf.Abs(unit.y),1/Mathf.Abs(unit.z));
  prop.gameObject.AddComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(R+"Props/Hero_Racket.asset");prop.gameObject.AddComponent<MeshRenderer>().sharedMaterials=new[]{"Hero_Racket_Blue","Hero_Racket_Strings","Hero_Racket_Grip"}.Select(n=>AssetDatabase.LoadAssetAtPath<Material>(R+"Materials/"+n+".mat")).ToArray();
  var wrap=GameObject.CreatePrimitive(PrimitiveType.Cylinder);wrap.name="TwoHand_GripWrap";wrap.transform.SetParent(prop,false);wrap.transform.localPosition=new Vector3(0,.151f,0);wrap.transform.localScale=new Vector3(.030f,.054f,.030f);wrap.GetComponent<Renderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(R+"Materials/Hero_Racket_Grip.mat");Object.DestroyImmediate(wrap.GetComponent<Collider>());
  hero.racketGrip=prop;hero.swingGripWrap=null;
  File.WriteAllText(Out+"/socket.txt","parent=Hand.R\nlocalPosition="+prop.localPosition.ToString("F7")+" (bone units; x100 = hand-local metres)\nlocalRotation="+prop.localRotation.ToString("F6")+"\nlocalEuler="+prop.localEulerAngles.ToString("F3")+"\nhand-frame origin (W,F,N m)="+cfg.socketOrigin.ToString("F4")+"\nracket Y (W,F,N)="+cfg.socketY.ToString("F4")+"\nstring normal (W,F,N)="+cfg.socketZ.ToString("F4")+"\n");
  var pos=hero.transform.position;hero.transform.position=Vector3.zero;PrefabUtility.SaveAsPrefabAsset(hero.gameObject,R+"Prefabs/Hero_01_RestoreMotion.prefab");hero.transform.position=pos;
  var cam=Camera.main;cam.transform.position=hero.transform.position+new Vector3(1.25f,1.4f,5.5f);cam.transform.LookAt(hero.transform.position+new Vector3(0,1.0f,0));cam.fieldOfView=40;
  EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),"Assets/Scenes/Hero01RestoreMotion.unity");AssetDatabase.SaveAssets();
  SessionState.SetBool(Key,true);started=false;EditorApplication.isPlaying=true;
 }catch(Exception e){Fail(e);}}
 static void Tick(){if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying)return;try{
  var hero=Object.FindFirstObjectByType<ModularHeroLook>();if(!hero||(!started&&!hero.IdleRunning))return;
  if(!started){cfg=JsonUtility.FromJson<Config>(File.ReadAllText(Cfg));handRef=JsonUtility.FromJson<HandRef>(File.ReadAllText(Path.GetFullPath("../ArtDir/anims/restore_motion/unity_hand_ref.json")));errR=new List<float>();errL=new List<float>();started=true;slot=local=global_=runtimeSlot=0;runtimeStart=0;posed=false;report=new List<string>();runtime=new List<string>();manifest=new List<string>();faceDots=new List<float>();
   var a=hero.animator;report.Add("facing check: right hand x in hero space="+hero.transform.InverseTransformPoint(a.GetBoneTransform(HumanBodyBones.RightHand).position).x.ToString("F3"));}
  if(runtimeSlot<8){
   if(runtimeStart==0){if(!hero.PlayStroke((ModularHeroLook.Stroke)runtimeSlot))throw new Exception("Runtime slot failed: "+runtimeSlot);runtimeStart=EditorApplication.timeSinceStartup;runtimeOrigin=hero.animator.GetBoneTransform(HumanBodyBones.RightHand).position;runtimeMotion=0;return;}
   runtimeMotion=Mathf.Max(runtimeMotion,Vector3.Distance(runtimeOrigin,hero.animator.GetBoneTransform(HumanBodyBones.RightHand).position));
   if(EditorApplication.timeSinceStartup-runtimeStart<1.6)return;
   if(runtimeMotion<.05f)throw new Exception("Runtime PlayStroke is frozen: "+runtimeSlot);
   runtime.Add(((ModularHeroLook.Stroke)runtimeSlot)+": real GameTime PlayStroke hand travel="+runtimeMotion.ToString("F3")+"m");runtimeSlot++;runtimeStart=0;
   if(runtimeSlot==8){File.WriteAllLines(Out+"/runtime_slot_checks.txt",runtime);hero.ReturnToReady();}return;
  }
  if(slot>=Names.Length){File.WriteAllLines(Out+"/verification.txt",report);File.WriteAllLines(Out+"/manifest.txt",manifest);SessionState.SetBool(Key,false);hero.enabled=false;if(Application.isBatchMode)EditorApplication.Exit(0);else EditorApplication.isPlaying=false;return;}
  int n=Frames(slot);double t=local/30.0;
  if(!posed){hero.SampleForReview(Inputs[slot],t);posed=true;return;}posed=false;
  var an=hero.animator;var handR=an.GetBoneTransform(HumanBodyBones.RightHand);
  if(!an.isHuman||!an.avatar.isValid)throw new Exception("Humanoid Avatar invalid");if(hero.racketGrip.parent!=handR)throw new Exception("Racket lost hand parent");
  if(local==0){firstHand=handR.position;motion=0;minLeftGap=999;minHead=minLeg=999;}
  motion=Mathf.Max(motion,Vector3.Distance(handR.position,firstHand));
  string name=Names[slot];var info=Clip(name);var tr=hero.transform;
  // left hand clearance from torso (glued off-arm check)
  var lh=an.GetBoneTransform(HumanBodyBones.LeftHand);var hips=an.GetBoneTransform(HumanBodyBones.Hips);var chest=an.GetBoneTransform(HumanBodyBones.Chest);
  minLeftGap=Mathf.Min(minLeftGap,Capsule(lh.position,hips.position,chest.position+Vector3.up*.06f,.17f)-.055f);
  if(info!=null&&info.contact>0){
   int c=Mathf.RoundToInt(info.contact*30);
   if(Mathf.Abs(local-c)<=3){float d=Mathf.Abs(Vector3.Dot(hero.racketGrip.forward,tr.forward));faceDots.Add(d);
    if(local==c+3){report.Add(name+" |string normal . net| frames c-3..c+3: "+string.Join(" ",faceDots.Select(x=>x.ToString("F2")))+"  AT CONTACT="+faceDots[3].ToString("F3")+" (1=strings square to net, 0=edge-on)");faceDots.Clear();}
    if(local==c){var lhp=lh.position;var axis=hero.racketGrip.up;var p0=hero.racketGrip.position;float gap=Vector3.Cross(axis,lhp-p0).magnitude;
     report.Add(name+" contact: racket head height="+hero.racketGrip.TransformPoint(new Vector3(0,.395f,0)).y.ToString("F2")+"m; left-hand bone to handle axis="+gap.ToString("F3")+"m");}
   }
  }
  // racket hoop clearance to head (serve/smash) and to right leg (runs), min tracked with auto-still
  {var head=an.GetBoneTransform(HumanBodyBones.Head);var hc=head.position+Vector3.up*.12f;float dh=999,dl=999;
   var ul=an.GetBoneTransform(HumanBodyBones.RightUpperLeg).position;var ll=an.GetBoneTransform(HumanBodyBones.RightLowerLeg).position;var ft=an.GetBoneTransform(HumanBodyBones.RightFoot).position;
   for(int k=0;k<24;k++){float a=k*Mathf.PI*2/24;var hp=hero.racketGrip.TransformPoint(new Vector3(Mathf.Sin(a)*.135f,.395f+Mathf.Cos(a)*.18f,0));
    dh=Mathf.Min(dh,Vector3.Distance(hp,hc)-.26f);dl=Mathf.Min(dl,Mathf.Min(Capsule(hp,ul,ll,.10f),Capsule(hp,ll,ft,.08f)));}
   var o=tr.position;
   if((name=="Serve"||name=="Smash")&&dh<minHead){minHead=dh;Stills2(hero,name+"_HEAD_CLEARANCE",o+tr.forward*1.6f-tr.right*2.4f+Vector3.up*1.4f,o+Vector3.up*1.35f);}
   if((name=="RunForward"||name=="RunRight")&&dl<minLeg){minLeg=dl;Stills2(hero,name+"_LEG_CLEARANCE",o+tr.forward*2.6f+tr.right*1.6f+Vector3.up*.9f,o+Vector3.up*.6f);}
   if(local==n-1){if(name=="Serve"||name=="Smash")report.Add(name+": min racket-hoop to head-sphere gap="+minHead.ToString("F3")+"m");if(name.StartsWith("Run")&&name!="RunLeft")report.Add(name+": min racket-hoop to right-leg capsule gap="+minLeg.ToString("F3")+"m");}}
  if(info!=null&&local==info.worst&&info.worst>0){var o2=tr.position;Stills2(hero,name+"_formerWorst_front",o2+tr.forward*2.8f+Vector3.up*1.1f,o2+Vector3.up*.9f);Stills2(hero,name+"_formerWorst_side",o2-tr.right*2.8f+tr.forward*.6f+Vector3.up*1.1f,o2+Vector3.up*.9f);}
  {var hc=handRef.clips.FirstOrDefault(x=>x.name==name);
   if(hc!=null&&local<hc.frames.Length){var bonesAll=hero.skeletonRoot.GetComponentsInChildren<Transform>();Transform B(string b)=>bonesAll.First(x=>x.name==b);
    foreach(var sd in new[]{"R","L"}){var h=an.GetBoneTransform(sd=="R"?HumanBodyBones.RightHand:HumanBodyBones.LeftHand);
     var fd=tr.InverseTransformDirection((B("Middle1."+sd).position-h.position).normalized);var wd=tr.InverseTransformDirection((B("Pinky1."+sd).position-B("Index1."+sd).position).normalized);
     var rf=sd=="R"?hc.frames[local].rf:hc.frames[local].lf;var rw=sd=="R"?hc.frames[local].rw:hc.frames[local].lw;
     float e=Mathf.Max(Vector3.Angle(fd,rf),Vector3.Angle(wd,rw));(sd=="R"?errR:errL).Add(e);}}
   if(local==n-1&&errR.Count>0){string S(List<float> l){var o=l.OrderBy(x=>x).ToList();return "median "+o[o.Count/2].ToString("F0")+"deg, max "+o.Last().ToString("F0")+"deg, frames>35deg "+o.Count(x=>x>35);}
    report.Add(name+" Unity-vs-authored hand orientation: RIGHT "+S(errR)+" | LEFT "+S(errL));errR.Clear();errL.Clear();}}
  Capture(Out+"/frame_"+global_.ToString("D4")+".png",null);manifest.Add(global_+" "+name+" "+local+" "+n);
  Stills(hero,name,local,info);
  local++;global_++;
  if(local>=n){if(slot!=0&&motion<.05f)throw new Exception("Frozen motion in slot "+name);
   report.Add(name+": "+n+" frames ("+((n-1)/30f).ToString("F2")+"s) sampled, right-hand travel="+motion.ToString("F3")+"m, min left-hand/torso proxy gap="+minLeftGap.ToString("F3")+"m");slot++;local=0;}
 }catch(Exception e){Fail(e);}}
 static void Stills(ModularHeroLook hero,string name,int local,ClipInfo info){
  var tr=hero.transform;var an=hero.animator;Vector3 fwd=tr.forward,right=tr.right,up=Vector3.up,o=tr.position;
  int c=info!=null&&info.contact>0?Mathf.RoundToInt(info.contact*30):-1;
  void Shot(string file,Vector3 camPos,Vector3 target,float fov){var cam=Camera.main;var cp=cam.transform.position;var cr=cam.transform.rotation;var f=cam.fieldOfView;cam.transform.position=camPos;cam.transform.LookAt(target);cam.fieldOfView=fov;Capture(Out+"/"+file+".png",null);cam.transform.SetPositionAndRotation(cp,cr);cam.fieldOfView=f;}
  var grip=hero.racketGrip.TransformPoint(new Vector3(0,.02f,0));
  var hipsP=an.GetBoneTransform(HumanBodyBones.Hips).position;var nrm=hero.racketGrip.forward;if(Vector3.Dot(nrm,grip-hipsP)<0)nrm=-nrm;
  var outward=Vector3.ProjectOnPlane(grip-hipsP,Vector3.up).normalized;
  if(name=="Ready"&&local==20){
   Shot("Grip_closeup_palm",grip+(nrm*.6f+outward*.4f).normalized*.45f+up*.06f,grip,30);
   Shot("Grip_closeup_front",grip+(fwd*.8f+outward*.5f).normalized*.45f+up*.10f,grip,30);
   Shot("Grip_closeup_top",grip+(up*.8f+outward*.4f+fwd*.3f).normalized*.45f,grip,30);
   Shot("Ready_front",o+fwd*3.0f+up*1.0f,o+up*.9f,34);
   var lhp=an.GetBoneTransform(HumanBodyBones.LeftHand).position;var mid=(lhp+grip)/2;
   Shot("Ready_hands_front",mid+fwd*.75f+up*.15f,mid,34);Shot("Ready_hands_top",mid+up*.7f+fwd*.35f,mid,34);Shot("Ready_hands_left",mid-right*.7f+fwd*.35f+up*.1f,mid,34);
  }
  if(name=="Forehand"&&local==c){Shot("Forehand_CONTACT",o+fwd*2.8f+right*1.6f+up*1.1f,o+up*.9f,36);Shot("Grip_closeup_forehand_contact",grip+(nrm*.6f+outward*.4f).normalized*.45f+up*.06f,grip,30);}
  if(name=="Serve"&&local==c)Shot("Serve_CONTACT",o+fwd*3.0f+right*1.2f+up*1.3f,o+up*1.2f,40);
  if(name=="Smash"&&local==c)Shot("Smash_CONTACT",o+fwd*3.0f+right*1.2f+up*1.3f,o+up*1.2f,40);
  if(name=="Backhand"){
   if(local==c-12){Shot("Backhand_LOAD",o+fwd*3.0f-right*1.4f+up*1.1f,o+up*.9f,36);Shot("Backhand_LOAD_side",o-right*3.2f+up*1.1f,o+up*.9f,36);}
   if(local==c-6)Shot("Backhand_DROP",o+fwd*3.0f-right*1.4f+up*1.1f,o+up*.9f,36);
   if(local==c-12){var lh2=an.GetBoneTransform(HumanBodyBones.LeftHand).position;Shot("Backhand_LOAD_hands",(lh2+grip)/2+fwd*.7f-right*.3f+up*.1f,(lh2+grip)/2,34);}
   if(local==c){Shot("Backhand_CONTACT",o+fwd*3.0f-right*1.4f+up*1.1f,o+up*.9f,36);Shot("Backhand_CONTACT_hands",grip+fwd*.55f-right*.25f+up*.12f,grip+hero.racketGrip.up*.06f,34);}
   if(local==c+15)Shot("Backhand_WRAP",o+fwd*3.0f-right*1.4f+up*1.1f,o+up*.9f,36);
  }
  if(name=="Volley"){
   if(local==c-8)Shot("Volley_PREP",o+fwd*2.8f+right*1.6f+up*1.1f,o+up*.95f,36);
   if(local==c)Shot("Volley_CONTACT",o+fwd*2.8f+right*1.6f+up*1.1f,o+up*.95f,36);
   if(local==c+10)Shot("Volley_RECOVER",o+fwd*2.8f+right*1.6f+up*1.1f,o+up*.95f,36);
  }
 }
 static void Stills2(ModularHeroLook hero,string file,Vector3 camPos,Vector3 target){var cam=Camera.main;var cp=cam.transform.position;var cr=cam.transform.rotation;var f=cam.fieldOfView;cam.transform.position=camPos;cam.transform.LookAt(target);cam.fieldOfView=36;Capture(Out+"/"+file+".png",null);cam.transform.SetPositionAndRotation(cp,cr);cam.fieldOfView=f;}
 static float Capsule(Vector3 p,Vector3 a,Vector3 b,float r){var ab=b-a;float t=Mathf.Clamp01(Vector3.Dot(p-a,ab)/Mathf.Max(1e-5f,ab.sqrMagnitude));return Vector3.Distance(p,a+t*ab)-r;}
 static void Capture(string path,Camera c){var cam=c?c:Camera.main;var rt=new RenderTexture(960,960,24,RenderTextureFormat.ARGB32);rt.antiAliasing=4;var prev=RenderTexture.active;var old=cam.targetTexture;cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;var tex=new Texture2D(960,960,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,960,960),0,0);tex.Apply();File.WriteAllBytes(path,tex.EncodeToPNG());cam.targetTexture=old;RenderTexture.active=prev;Object.DestroyImmediate(tex);rt.Release();Object.DestroyImmediate(rt);}
 static void Fail(Exception e){Debug.LogException(e);Directory.CreateDirectory(Out);File.WriteAllText(Out+"/error.txt",e.ToString());SessionState.SetBool(Key,false);if(Application.isBatchMode)EditorApplication.Exit(1);}
}}
