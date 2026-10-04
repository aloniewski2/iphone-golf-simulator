using System;using System.IO;using System.Linq;using System.Collections.Generic;
using UnityEngine;using UnityEditor;using UnityEditor.SceneManagement;using GolfArcade.Tennis;using Object=UnityEngine.Object;
namespace GolfArcade.EditorTools {
/// TennisComplete_CustomizeSafe (characters only): builds the Hero01 gameplay prefab (driver + cosmetics),
/// cosmetic assets, cosmetics stills, and a live gameplay capture. Gameplay code/tuning is not modified.
[InitializeOnLoad] public static class HeroGameplayBuild {
 const string R=HeroLookImporter.Root;const string M=R+"Models/RestoreMotion/";const string C=R+"Models/Cosmetics/";
 const string PrefabOut="Assets/Resources/Tennis/Hero/Hero_01_Tennis.prefab";
 static string Out=>Path.GetFullPath("../ArtDir/screenshots/tennis_complete");
 static string Cfg=>Path.GetFullPath("../ArtDir/anims/restore_motion/unity_gameplay_config.json");
 [Serializable] class Contact{public string name;public float contact;}
 [Serializable] class Trophy{public float[] origin,axis,normal;}
 [Serializable] class Config{public Contact[] contacts;public string[] juice;public Trophy trophy;}
 static HeroGameplayBuild(){EditorApplication.update+=Tick;}
 static AnimationClip Clip(string path,string name,bool loop,bool configure){
  if(configure){var im=(ModelImporter)AssetImporter.GetAtPath(path);var defs=im.defaultClipAnimations;
   foreach(var c in defs){c.name=name;c.loopTime=loop;c.loopPose=false;c.lockRootPositionXZ=true;c.lockRootHeightY=true;c.lockRootRotation=true;c.keepOriginalPositionXZ=true;c.keepOriginalPositionY=true;c.keepOriginalOrientation=true;}
   im.clipAnimations=defs;im.SaveAndReimport();}
  var clip=AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview"));if(!clip.humanMotion)throw new Exception("Not Humanoid: "+path);return clip;
 }
 static Material Mat(string name,Material src,Color color,float metal=-1,float smooth=-1){
  string path=R+"Materials/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
  if(!m){m=new Material(src);AssetDatabase.CreateAsset(m,path);}else m.CopyPropertiesFromMaterial(src);
  m.SetColor("_BaseColor",color);m.SetColor("_Color",color);if(metal>=0)m.SetFloat("_Metallic",metal);if(smooth>=0)m.SetFloat("_Smoothness",smooth);EditorUtility.SetDirty(m);return m;
 }
 static Texture2D Readable(Texture src){var rt=RenderTexture.GetTemporary(src.width,src.height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);Graphics.Blit(src,rt);var prev=RenderTexture.active;RenderTexture.active=rt;var t=new Texture2D(src.width,src.height,TextureFormat.RGBA32,false);t.ReadPixels(new Rect(0,0,src.width,src.height),0,0);t.Apply();RenderTexture.active=prev;RenderTexture.ReleaseTemporary(rt);return t;}
 /// Trim variant: recolour the shared cloth atlas navy->teal, orange->sunny yellow; whites/neutrals untouched.
 static Material TrimCloth(Material cloth){
  string png=R+"Textures/Hero_01_MatteCloth_TrimTeal.png";
  var src=Readable(cloth.GetTexture("_BaseMap")??cloth.mainTexture);var px=src.GetPixels();
  for(int i=0;i<px.Length;i++){Color.RGBToHSV(px[i],out float h,out float s,out float v);
   if(s>.28f&&h>.54f&&h<.74f){h=.47f;s=Mathf.Min(1,s*1.05f);v=Mathf.Min(1,v*1.45f+.05f);}
   else if(s>.42f&&(h<.11f||h>.97f)){h=.135f;v=Mathf.Min(1,v*1.05f);}
   else continue;var c=Color.HSVToRGB(h,s,v);c.a=px[i].a;px[i]=c;}
  src.SetPixels(px);src.Apply();Directory.CreateDirectory(Path.GetDirectoryName(png));File.WriteAllBytes(png,src.EncodeToPNG());AssetDatabase.ImportAsset(png);
  var imp=(TextureImporter)AssetImporter.GetAtPath(png);imp.sRGBTexture=true;imp.SaveAndReimport();
  var tex=AssetDatabase.LoadAssetAtPath<Texture2D>(png);var m=Mat("Hero_01_MatteCloth_TrimTeal",cloth,Color.white);m.SetTexture("_BaseMap",tex);m.mainTexture=tex;EditorUtility.SetDirty(m);return m;
 }
 static void ConfigureEnvV4(){
  const string E="Assets/Resources/Tennis/EnvV4/";
  var ti=AssetImporter.GetAtPath(E+"EnvV4_Palette.png") as TextureImporter;
  if(ti&&(ti.mipmapEnabled||ti.wrapMode!=TextureWrapMode.Clamp||ti.filterMode!=FilterMode.Bilinear)){ti.mipmapEnabled=false;ti.wrapMode=TextureWrapMode.Clamp;ti.filterMode=FilterMode.Bilinear;ti.textureCompression=TextureImporterCompression.Uncompressed;ti.SaveAndReimport();}
  foreach(var g in AssetDatabase.FindAssets("t:Model",new[]{E.TrimEnd('/')})){var p=AssetDatabase.GUIDToAssetPath(g);var mi=(ModelImporter)AssetImporter.GetAtPath(p);
   if(mi.materialImportMode!=ModelImporterMaterialImportMode.None||mi.importAnimation){mi.materialImportMode=ModelImporterMaterialImportMode.None;mi.importAnimation=false;mi.animationType=ModelImporterAnimationType.None;mi.SaveAndReimport();}}
 }
 public static void Build(){try{ConfigureEnvV4();
  var cfg=JsonUtility.FromJson<Config>(File.ReadAllText(Cfg));
  AssetDatabase.Refresh();
  foreach(var p in new[]{C+"Hero_01_Hat_Cap.fbx",C+"Hero_01_Hat_Sweatband.fbx",C+"Hero_Prop_Trophy.fbx"}){var im=(ModelImporter)AssetImporter.GetAtPath(p);im.materialImportMode=ModelImporterMaterialImportMode.None;im.SaveAndReimport();}
  var src=AssetDatabase.LoadAssetAtPath<GameObject>(R+"Prefabs/Hero_01_RestoreMotion.prefab");
  var hero=(GameObject)PrefabUtility.InstantiatePrefab(src);PrefabUtility.UnpackPrefabInstance(hero,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
  hero.name="Hero_01_Tennis";var look=hero.GetComponent<ModularHeroLook>();look.externalAnimation=true;
  var driver=hero.AddComponent<HeroTennisDriver>();driver.look=look;
  var slots=new List<HeroTennisDriver.ClipSlot>();
  float Ct(string n)=>cfg.contacts.FirstOrDefault(c=>c.name==n)?.contact??0;
  void Add(HeroTennisDriver.Clip id,string file,bool loop,bool configure,float contact=0){slots.Add(new HeroTennisDriver.ClipSlot{id=id,clip=Clip(file,Path.GetFileNameWithoutExtension(file),loop,configure),contact=contact});}
  // Core approved set: bound as-is (already imported; not re-authored).
  Add(HeroTennisDriver.Clip.Ready,M+"Hero_Ready_v5.fbx",true,false);
  Add(HeroTennisDriver.Clip.Forehand,M+"Hero_Forehand_v5.fbx",false,false,Ct("Forehand"));
  Add(HeroTennisDriver.Clip.Backhand,M+"Hero_Backhand_v5.fbx",false,false,Ct("Backhand"));
  Add(HeroTennisDriver.Clip.Serve,M+"Hero_Serve_v5.fbx",false,false,Ct("Serve"));
  Add(HeroTennisDriver.Clip.Volley,M+"Hero_Volley_v5.fbx",false,false,Ct("Volley"));
  Add(HeroTennisDriver.Clip.Smash,M+"Hero_Smash_v5.fbx",false,false,Ct("Smash"));
  Add(HeroTennisDriver.Clip.RunForward,M+"Hero_RunForward_v5.fbx",false,false);
  Add(HeroTennisDriver.Clip.RunRight,M+"Hero_RunRight_v5.fbx",false,false);
  Add(HeroTennisDriver.Clip.RunLeft,M+"Hero_RunLeft_v5.fbx",false,false);
  // New party/juice layer.
  Add(HeroTennisDriver.Clip.Idle,M+"Hero_Idle_v5.fbx",true,true);
  Add(HeroTennisDriver.Clip.Walk,M+"Hero_Walk_v5.fbx",true,true);
  foreach(var j in new[]{"HitPerfect","MissWhiff","CelebratePoint","SadPointLost","MatchWin","MatchLose"})Add((HeroTennisDriver.Clip)Enum.Parse(typeof(HeroTennisDriver.Clip),j),M+"Hero_"+j+"_v5.fbx",false,true);
  driver.slots=slots.ToArray();
  // Cosmetics (materials derived from the hero's own URP materials: no pink shaders).
  var orange=AssetDatabase.LoadAssetAtPath<Material>(R+"Materials/Hero_01_V5Orange.mat");var white=AssetDatabase.LoadAssetAtPath<Material>(R+"Materials/Hero_01_V5White.mat");var cloth=AssetDatabase.LoadAssetAtPath<Material>(R+"Materials/Hero_01_MatteCloth.mat");
  var cos=hero.AddComponent<HeroCosmetics>();cos.look=look;driver.cosmetics=cos;
  cos.visorAsset=AssetDatabase.LoadAssetAtPath<GameObject>(R+"Models/Hero_01_Hat_Visor.fbx");cos.capAsset=AssetDatabase.LoadAssetAtPath<GameObject>(C+"Hero_01_Hat_Cap.fbx");cos.sweatbandAsset=AssetDatabase.LoadAssetAtPath<GameObject>(C+"Hero_01_Hat_Sweatband.fbx");
  cos.capMaterial=Mat("Cos_Cap_Orange",orange,new Color(1,.42f,.24f));cos.bandWhite=Mat("Cos_Band_White",white,white.GetColor("_BaseColor"));cos.bandOrange=Mat("Cos_Band_Orange",orange,orange.GetColor("_BaseColor"));
  cos.clothDefault=cloth;cos.clothTrim=TrimCloth(cloth);cos.pipingDefault=orange;cos.pipingTrim=Mat("Hero_01_V5Yellow",orange,new Color(1,.82f,.2f));
  cos.trophyMesh=AssetDatabase.LoadAllAssetsAtPath(C+"Hero_Prop_Trophy.fbx").OfType<Mesh>().First();cos.trophyMaterial=Mat("Cos_Trophy_Gold",orange,new Color(1,.78f,.22f),.85f,.65f);
  Vector3 V(float[] a)=>new Vector3(a[0],a[1],a[2]);cos.trophyOrigin=V(cfg.trophy.origin);cos.trophyAxis=V(cfg.trophy.axis);cos.trophyNormal=V(cfg.trophy.normal);
  Directory.CreateDirectory(Path.GetDirectoryName(PrefabOut));hero.transform.position=Vector3.zero;PrefabUtility.SaveAsPrefabAsset(hero,PrefabOut);Object.DestroyImmediate(hero);
  AssetDatabase.SaveAssets();Debug.Log("HERO_GAMEPLAY_PREFAB_BUILT "+PrefabOut);
 }catch(Exception e){Fail(e);}}
 // ------------------------------------------------------------------ cosmetics stills (review stage)
 static Camera faceCam;static int swingCamN;static string lastRS;static int strokeShots;static string lastStrokeState="";static List<(HeroTennisDriver.Clip,float,string)> stripJobs;static string stripOut;static int mode,step,juiceIdx,juiceT;static bool sampled,whiffDone;static int armDbg;static List<string> log;static GameObject rev;
 static readonly (string name,Action<HeroCosmetics> apply)[] Looks={("Default_V4",c=>c.ResetToDefault()),("HatA_Cap",c=>{c.ResetToDefault();c.EquipHat(HeroCosmetics.Hat.Cap);}),("HatB_Sweatband",c=>{c.ResetToDefault();c.EquipHat(HeroCosmetics.Hat.Sweatband);}),("Trim_TealYellow",c=>{c.ResetToDefault();c.SetTrim(true);}),("Cap_Trim_Trophy",c=>{c.ResetToDefault();c.EquipHat(HeroCosmetics.Hat.Cap);c.SetTrim(true);c.SetTrophy(true);})};
 static readonly (HeroTennisDriver.Clip clip,float t)[] Poses={(HeroTennisDriver.Clip.Ready,.5f),(HeroTennisDriver.Clip.Idle,1f),(HeroTennisDriver.Clip.Forehand,-1),(HeroTennisDriver.Clip.Backhand,-1),(HeroTennisDriver.Clip.Serve,-1),(HeroTennisDriver.Clip.Smash,-1),(HeroTennisDriver.Clip.MissWhiff,.5f),(HeroTennisDriver.Clip.CelebratePoint,.45f),(HeroTennisDriver.Clip.MatchWin,.55f),(HeroTennisDriver.Clip.SadPointLost,.8f),(HeroTennisDriver.Clip.RunForward,.6f),(HeroTennisDriver.Clip.HitPerfect,.3f)};
 public static void Stills(){try{Build();Directory.CreateDirectory(Out);EditorSceneManager.OpenScene("Assets/Scenes/Hero01RestoreMotion.unity");SessionState.SetInt("HeroGP",1);mode=1;step=0;EditorApplication.isPlaying=true;}catch(Exception e){Fail(e);}}
 public static void Juice(){try{Build();Directory.CreateDirectory(Out+"/juice");foreach(var f in Directory.GetFiles(Out+"/juice","*.png"))File.Delete(f);EditorSceneManager.OpenScene("Assets/Scenes/Hero01RestoreMotion.unity");SessionState.SetInt("HeroGP",3);step=0;EditorApplication.isPlaying=true;}catch(Exception e){Fail(e);}}
 public static void ClipStrip(){try{Build();EditorSceneManager.OpenScene("Assets/Scenes/Hero01RestoreMotion.unity");SessionState.SetInt("HeroGP",6);step=0;EditorApplication.isPlaying=true;}catch(Exception e){Fail(e);}}
 public static void FeetProbe(){try{Build();EditorSceneManager.OpenScene("Assets/Scenes/Hero01RestoreMotion.unity");SessionState.SetInt("HeroGP",5);step=0;EditorApplication.isPlaying=true;}catch(Exception e){Fail(e);}}
 public static void Gameplay(){try{Build();Directory.CreateDirectory(Out+"/gameplay");var gdir=Out+"/gameplay"+(HeroTennisDriver.Baseline?"_baseline":"");Directory.CreateDirectory(gdir);foreach(var f in Directory.GetFiles(gdir,"*.png"))File.Delete(f);EditorSceneManager.OpenScene("Assets/Scenes/Tennis.unity");SessionState.SetInt("HeroGP",2);mode=2;step=0;EditorApplication.isPlaying=true;}catch(Exception e){Fail(e);}}
 static void Tick(){int m=SessionState.GetInt("HeroGP",0);if(m==0||!EditorApplication.isPlaying)return;try{
  AudioListener.volume=0;AudioListener.pause=true;   // capture runs are silent (no game audio on the user's speakers)
  if(m==1){
   if(step==0){var old=Object.FindFirstObjectByType<ModularHeroLook>();var pos=old.transform.position;var rot=old.transform.rotation;old.gameObject.SetActive(false);
    rev=Object.Instantiate(Resources.Load<GameObject>(LegacyHero01.PrefabPath),pos,rot);rev.GetComponent<HeroTennisDriver>().Build();log=new List<string>();step=1;return;}
   int total=Looks.Length*Poses.Length;int k=step-1;
   if(k>=total){File.WriteAllLines(Out+"/stills_log.txt",log);Finish();return;}
   var d=rev.GetComponent<HeroTennisDriver>();var cos=rev.GetComponent<HeroCosmetics>();var (ln,apply)=Looks[k/Poses.Length];var (clip,t)=Poses[k%Poses.Length];
   if(k%Poses.Length==0)apply(cos);
   float time=t<0?d.ContactOf(clip):t;d.Sample(clip,time);
   if(!sampled){sampled=true;return;}sampled=false;   // skinning updates in the player loop: capture on the next tick
   var cam=Camera.main;var o=rev.transform.position;var fwd=rev.transform.forward;var right=rev.transform.right;
   cam.transform.position=o+fwd*3.1f+right*1.2f+Vector3.up*1.15f;cam.transform.LookAt(o+Vector3.up*.95f);cam.fieldOfView=38;
   Capture(cam,Out+$"/{ln}__{clip}.png",720,720);
   var pink=rev.GetComponentsInChildren<Renderer>().SelectMany(r=>r.sharedMaterials).Where(x=>!x||x.shader==null||x.shader.name.Contains("Error")||x.shader.name=="Hidden/InternalErrorShader").Count();
   {var an=look(rev).animator;var hr=an.GetBoneTransform(HumanBodyBones.RightHand);var hl=an.GetBoneTransform(HumanBodyBones.LeftHand);var rg=look(rev).racketGrip;
    log.Add($"POS {ln} {clip} handR={rev.transform.InverseTransformPoint(hr.position)} handL={rev.transform.InverseTransformPoint(hl.position)} racketParent={rg.parent.name} racket={rev.transform.InverseTransformPoint(rg.position)} bone0={an.GetBoneTransform(HumanBodyBones.RightUpperArm).name}");}
   log.Add($"{ln} {clip} t={time:0.00} hat={cos.CurrentHat} trim={cos.Trim} trophy={cos.Trophy} missingOrErrorMaterials={pink} avatarValid={look(rev).animator.avatar.isValid}");
   step++;return;
  }
  if(m==6){
   // Env HERO_STRIP="Clip:t0:t1:dt;..." HERO_STRIP_VIEWS="front,side,back" HERO_STRIP_OUT=dir : frames per clip/view for review.
   if(step==0){var old=Object.FindFirstObjectByType<ModularHeroLook>();old.gameObject.SetActive(false);rev=Object.Instantiate(Resources.Load<GameObject>(LegacyHero01.PrefabPath),Vector3.zero,Quaternion.identity);rev.GetComponent<HeroTennisDriver>().Build();
    stripJobs=new List<(HeroTennisDriver.Clip,float,string)>();var views=(Environment.GetEnvironmentVariable("HERO_STRIP_VIEWS")??"front,side").Split(',');
    foreach(var job in Environment.GetEnvironmentVariable("HERO_STRIP").Split(';')){var q=job.Split(':');var c=(HeroTennisDriver.Clip)Enum.Parse(typeof(HeroTennisDriver.Clip),q[0]);float t0=float.Parse(q[1]),t1=float.Parse(q[2]),dt=float.Parse(q[3]);
     for(float t=t0;t<=t1+1e-4f;t+=dt)foreach(var v in views)stripJobs.Add((c,t,v));}
    stripOut=Environment.GetEnvironmentVariable("HERO_STRIP_OUT");Directory.CreateDirectory(stripOut);{var hs=Environment.GetEnvironmentVariable("HERO_STYLE");if(hs!=null){var q=hs.Split(',');HeroKit.Apply(rev.GetComponent<HeroTennisDriver>(),HeroKit.Style.From(int.Parse(q[0]),int.Parse(q[1]),int.Parse(q[2]),TennisLook.Kit.From(q[3],q[4],q[5],q[6],int.Parse(q[0]))));}}{var hide=Environment.GetEnvironmentVariable("HERO_HIDE");if(hide!=null)foreach(var rr in rev.GetComponentsInChildren<Renderer>(true)){foreach(var h in hide.Split(','))if(h.StartsWith("sub:")){if(rr.name=="Body_Skin"){var ms=rr.sharedMaterials;int si=int.Parse(h.Substring(4));var inv=new Material(Shader.Find("Universal Render Pipeline/Unlit"));inv.SetColor("_BaseColor",new Color(0,1,0));ms[si]=inv;rr.sharedMaterials=ms;}}else if(rr.name.StartsWith(h))rr.enabled=false;}}if(Environment.GetEnvironmentVariable("HERO_STRIP_ISOLATE")=="1"){foreach(var rr in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))if(!rr.transform.IsChildOf(rev.transform))rr.enabled=false;var mc=Camera.main;mc.clearFlags=CameraClearFlags.SolidColor;mc.backgroundColor=new Color(1,0,1,1);}log=new List<string>();log.Add("PROXY "+rev.GetComponent<HeroTennisDriver>().BodyProxyInfo);step=1;return;}
   int k=step-1;if(k>=stripJobs.Count){File.WriteAllLines(stripOut+"/strip_log.txt",log);Finish();return;}
   var d=rev.GetComponent<HeroTennisDriver>();var (sc,st,sv)=stripJobs[k];d.Sample(sc,st);
   if(!sampled){sampled=true;return;}sampled=false;
   var cam=Camera.main;var o=rev.transform.position;var f=rev.transform.forward;var r=rev.transform.right;
   Vector3 dir=sv=="front"?f:sv=="side"?-r:sv=="back"?-f:sv=="side2"?r:(f-r).normalized;
   if(sv.StartsWith("head")||sv.StartsWith("ear")||sv.StartsWith("orb")||sv=="torso"){var hd=look(rev).animator.GetBoneTransform(HumanBodyBones.Head);Vector3 hdir=sv.StartsWith("orb")?(Quaternion.AngleAxis(float.Parse(sv.Split('_')[1]),Vector3.up)*Quaternion.AngleAxis(float.Parse(sv.Split('_')[2]),r)*f).normalized:sv=="torso"?(f+r*.35f).normalized:sv=="earR"?r:sv=="earL"?-r:sv=="earRback"?(r-f*.8f).normalized:sv=="earLback"?(-r-f*.8f).normalized:sv=="earRlow"?(r+Vector3.down*.5f).normalized:sv=="earLlow"?(-r+Vector3.down*.5f).normalized:sv=="headfront"?f:sv=="headfront3q"?(f+r*.6f).normalized:sv=="headback"?-f:sv=="headback3q"?(-f+r*.7f).normalized:sv=="headbackL"?(-f-r*.7f).normalized:-r;
    float hdist=sv=="torso"?1.4f:sv.StartsWith("ear")?.75f:sv.StartsWith("orb")?(sv.Split('_').Length>3?float.Parse(sv.Split('_')[3]):.62f):1.3f;cam.transform.position=hd.position+Vector3.up*.08f+hdir*hdist;cam.transform.LookAt(hd.position+Vector3.up*.08f);cam.fieldOfView=30;}
   else{cam.transform.position=o+dir*3.4f+Vector3.up*1.0f;cam.transform.LookAt(o+Vector3.up*.8f);cam.fieldOfView=36;}
   Capture(cam,stripOut+$"/{k:D4}_{sc}_{st:0.00}_{sv}.png",480,480);log.Add($"{k} {sc} {st:0.00} {sv} {d.ProbeArms()}");step++;return;
  }
  if(m==5){
   if(step==0){var old=Object.FindFirstObjectByType<ModularHeroLook>();old.gameObject.SetActive(false);rev=Object.Instantiate(Resources.Load<GameObject>(LegacyHero01.PrefabPath),Vector3.zero,Quaternion.identity);rev.GetComponent<HeroTennisDriver>().Build();log=new List<string>();step=1;return;}
   var d=rev.GetComponent<HeroTennisDriver>();var an=look(rev).animator;
   foreach(HeroTennisDriver.Clip c in Enum.GetValues(typeof(HeroTennisDriver.Clip))){
    float len=d.LengthOf(c);if(len<=0)continue;float worst=0,wt=0;float hipsMin=9;
    for(float t=0;t<=len;t+=.1f){d.Sample(c,t);
     float h=Mathf.Min(Mathf.Min(an.GetBoneTransform(HumanBodyBones.LeftFoot).position.y-.165f,an.GetBoneTransform(HumanBodyBones.RightFoot).position.y-.165f),Mathf.Min(an.GetBoneTransform(HumanBodyBones.LeftToes).position.y-.066f,an.GetBoneTransform(HumanBodyBones.RightToes).position.y-.066f));
     if(-h>worst){worst=-h;wt=t;}hipsMin=Mathf.Min(hipsMin,an.GetBoneTransform(HumanBodyBones.Hips).position.y);}
    log.Add($"PROBE {c} maxSink={worst:0.000} at t={wt:0.0} hipsMin={hipsMin:0.000}");
    for(float tt=0;tt<=len;tt+=.05f){d.Sample(c,tt);log.Add($"   ARMS {c} t={tt:0.00} {d.ProbeArms()}");}}
   File.WriteAllLines(Out+"/feet_probe.txt",log);Finish();return;
  }
  if(m==3){
   if(step==0){var old=Object.FindFirstObjectByType<ModularHeroLook>();var pos=old.transform.position;var rot=old.transform.rotation;old.gameObject.SetActive(false);
    rev=Object.Instantiate(Resources.Load<GameObject>(LegacyHero01.PrefabPath),pos,rot);rev.GetComponent<HeroTennisDriver>().Build();log=new List<string>();step=1;juiceIdx=0;juiceT=0;return;}
   var jc=new[]{HeroTennisDriver.Clip.Idle,HeroTennisDriver.Clip.HitPerfect,HeroTennisDriver.Clip.MissWhiff,HeroTennisDriver.Clip.CelebratePoint,HeroTennisDriver.Clip.SadPointLost,HeroTennisDriver.Clip.MatchWin,HeroTennisDriver.Clip.MatchLose};
   if(juiceIdx>=jc.Length){File.WriteAllLines(Out+"/juice_manifest.txt",log);Finish();return;}
   var d=rev.GetComponent<HeroTennisDriver>();var c=jc[juiceIdx];
   if(juiceT==0&&c==HeroTennisDriver.Clip.MatchWin){var cc=rev.GetComponent<HeroCosmetics>();cc.SetTrophy(true);}
   d.Sample(c,juiceT/30f);
   if(!sampled){sampled=true;return;}sampled=false;
   var cam=Camera.main;var o=rev.transform.position;cam.transform.position=o+rev.transform.forward*3.3f+rev.transform.right*1.1f+Vector3.up*1.2f;cam.transform.LookAt(o+Vector3.up*1.0f);cam.fieldOfView=40;
   Capture(cam,Out+$"/juice/j_{step:D4}.png",720,720);log.Add($"{step} {c} {juiceT}");step++;juiceT++;
   if(juiceT/30f>d.LengthOf(c)){juiceIdx++;juiceT=0;}
   return;
  }
  if(m==2){
   var game=Object.FindFirstObjectByType<TennisGame>();if(!game||!game.Initialized)return;
   if(step==0){var hs=Environment.GetEnvironmentVariable("HERO_STYLE");if(hs!=null){var q=hs.Split(',');var pdh=GameObject.Find("Hero01 (player visual)").GetComponent<HeroTennisDriver>();
     HeroKit.Apply(pdh,HeroKit.Style.From(int.Parse(q[0]),int.Parse(q[1]),int.Parse(q[2]),TennisLook.Kit.From(q[3],q[4],q[5],q[6],int.Parse(q[0]))));}}
   if(step==0){game.AutoPlay=true;Time.captureFramerate=int.Parse(Environment.GetEnvironmentVariable("HERO_FPS")??"30");log=new List<string>();if(Environment.GetEnvironmentVariable("HERO_PLAN")?.StartsWith("2")==true)TennisGame.AutoPlayTimingJitter=.08f;
    var p=GameObject.Find("Hero01 (player visual)");if(Environment.GetEnvironmentVariable("HERO_PLAN")==null)p.GetComponent<HeroCosmetics>().EquipHat(HeroCosmetics.Hat.Cap);
    TennisGame.ScoreChanged+=s=>log.Add($"SCORE f={step} {s} WHY={game.Feedback?.Replace("\n"," ")}");TennisGame.Whiffed+=()=>log.Add($"WHIFF f={step}");TennisGame.ContactMade+=(f,g,sp)=>log.Add($"CONTACT f={step} {g}");}
   // One deliberately mistimed (early) swing through the real input API, to show the whiff reaction.
   int wf=int.Parse(Environment.GetEnvironmentVariable("HERO_WHIFF_AFTER")??"1150");
   if(step>wf&&!whiffDone&&game.Flow==TennisGame.Phase.Rally&&!(game.GetComponent<TennisJuice>()&&game.GetComponent<TennisJuice>().ShotName!="None")&&game.BallVelocity.z<-1&&game.BallPosition.z>3.5f&&!game.Player.Swinging){game.AutoPlay=false;game.RequestSwing(.9f);whiffDone=true;log.Add($"INJECT early swing f={step}");}
   if(whiffDone&&!game.AutoPlay&&game.Flow!=TennisGame.Phase.Rally){game.AutoPlay=true;log.Add($"AUTOPLAY on f={step}");}
   var pd=GameObject.Find("Hero01 (player visual)").GetComponent<HeroTennisDriver>();var od=GameObject.Find("Hero01 (rival visual)").GetComponent<HeroTennisDriver>();
   {var ea=Environment.GetEnvironmentVariable("HERO_ENV_AUDIT");if(ea!=null&&step==int.Parse(Environment.GetEnvironmentVariable("HERO_ENV_STEP")??"60")){EnvAudit(game,ea);}}
   log.Add($"F {step} player={pd.State} rival={od.State} flow={game.Flow} sink={pd.SinkBeforeGround:0.000}/{od.SinkBeforeGround:0.000} lift={pd.LastGroundLift:0.000}/{od.LastGroundLift:0.000} armRaw={pd.ArmBeforeClear:0.000}/{od.ArmBeforeClear:0.000} fsink={pd.FinalSink:0.000}/{od.FinalSink:0.000} arm={pd.FinalArmPenetration:0.000}/{od.FinalArmPenetration:0.000} skate={pd.FootSkate:0.00}/{od.FootSkate:0.00} wsum={pd.WeightSum:0.00} move={new Vector2(game.Player.Speed,game.Player.ForwardSpeed).magnitude:0.00} honest={game.HonestMisses} gap={game.LastContactGap:0.000} vis={Vector3.Distance(pd.StringCentre,game.BallPosition):0.000} ovis={Vector3.Distance(od.StringCentre,game.BallPosition):0.000} px={game.Player.transform.position.x:0.00} pz={game.Player.transform.position.z:0.00} ox={game.Opponent.transform.position.x:0.00} ball={game.BallPosition.x:0.00},{game.BallPosition.y:0.00},{game.BallPosition.z:0.00} sbc={pd.ServeBallClearance:0.000}/{od.ServeBallClearance:0.000} emo={pd.PointReactionsSuppressed}/{od.PointReactionsSuppressed} rk={pd.RacketBeforeClear:0.000}>{pd.FinalRacketPenetration:0.000}({pd.FinalRacketPart})/{od.RacketBeforeClear:0.000}>{od.FinalRacketPenetration:0.000} yaw={pd.BodyYaw().x:0}/{pd.BodyYaw().y:0} beat={(game.GetComponent<TennisJuice>()?game.GetComponent<TennisJuice>().Current.ToString():"-")} ts={Time.timeScale:0.00} pswing={(game.Player.Swinging?1:0)} shot={(game.GetComponent<TennisJuice>()?game.GetComponent<TennisJuice>().ShotName:"-")} ult={game.PlayerUltimate:0.00}/{game.RivalUltimate:0.00} emo2={pd.LastEmote}/{od.LastEmote} {pd.MotionProbe()}");
   if(step==300)foreach(var cs in Object.FindObjectsByType<ContactShadow>(FindObjectsSortMode.None)){var rr=cs.GetComponent<Renderer>();log.Add($"CSH {cs.name} pos={cs.transform.position:F3} scale={cs.transform.localScale:F2} rot={cs.transform.eulerAngles:F0} shader={rr.sharedMaterial.shader.name} sup={rr.sharedMaterial.shader.isSupported} q={rr.sharedMaterial.renderQueue} en={rr.enabled} act={cs.gameObject.activeInHierarchy} vis={rr.isVisible} str={(rr.sharedMaterial.HasProperty("_Strength")?rr.sharedMaterial.GetFloat("_Strength"):-1)} surf={(cs.Surface?cs.Surface.name:"none")}");}
   if(game.LastRivalStrike!=null&&game.LastRivalStrike!=lastRS){lastRS=game.LastRivalStrike;log.Add($"RSTRIKE f={step} {lastRS}");}
   if(pd.FinalArmPenetration>.1f&&armDbg<25){armDbg++;log.Add($"ARMDBG f={step} {pd.State} {pd.ProbeArms()} actionW? move={new Vector2(game.Player.Speed,game.Player.ForwardSpeed).magnitude:0.00}");}
   var plan=Environment.GetEnvironmentVariable("HERO_PLAN");if(plan=="2B"||plan=="2C")Plan2BStills(game,pd,od);else if(plan=="2")Plan2Stills(game,pd,od);else if(plan=="1B")Plan1BStills(game,pd,od);else Plan1Stills(game,pd,od);
   {var stf=Environment.GetEnvironmentVariable("HERO_SHADOWTEST");if(stf!=null&&step==int.Parse(stf)){
     var urp=(UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset)UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;var sun=Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Where(l=>l.type==LightType.Directional).ToArray();
     log.Add($"SHADOW urp={urp?.name} dist={urp?.shadowDistance} casc={urp?.shadowCascadeCount} q={QualitySettings.shadows} suns={string.Join(",",sun.Select(l=>l.name+":"+l.shadows+":"+l.intensity+":"+l.transform.forward))} camDist={Vector3.Distance(game.GameplayCamera.transform.position,game.Opponent.transform.position):0.0}");
     foreach(var r in game.Opponent.GetComponentsInChildren<Renderer>(true))log.Add($"  RIV {r.name} en={r.enabled} vis={r.isVisible} cast={r.shadowCastingMode} layer={r.gameObject.layer} {r.GetType().Name}");
     var d0=Out+"/shadowtest";Directory.CreateDirectory(d0);Capture(game.GameplayCamera,d0+"/a_default.png",1280,720);
     float od0=urp.shadowDistance;int oc=urp.shadowCascadeCount;urp.shadowCascadeCount=1;Capture(game.GameplayCamera,d0+"/b_casc1.png",1280,720);
     urp.shadowCascadeCount=oc;urp.shadowDistance=80;Capture(game.GameplayCamera,d0+"/c_dist80.png",1280,720);urp.shadowDistance=od0;}}
  {var fc=Environment.GetEnvironmentVariable("HERO_FACECAM");if(fc!=null&&step%6==0&&step<int.Parse(Environment.GetEnvironmentVariable("HERO_FACECAM_TO")??"2400")){Directory.CreateDirectory(fc);var pt=game.Player.transform;if(!faceCam){faceCam=Object.Instantiate(game.GameplayCamera);faceCam.enabled=false;foreach(var l in faceCam.GetComponents<AudioListener>())Object.DestroyImmediate(l);}var side=faceCam;var hd=look(GameObject.Find("Hero01 (player visual)")).animator.GetBoneTransform(HumanBodyBones.Head);var hf=GameObject.Find("Hero01 (player visual)").GetComponent<HeroFace>();
    side.fieldOfView=26;side.transform.position=hd.position+pt.forward*1.1f+pt.right*.25f+Vector3.up*.12f;side.transform.LookAt(hd.position+Vector3.up*.08f);Capture(side,fc+$"/f_{step:D5}.png",360,360);log.Add($"FACE f={step} mood={(hf?hf.mood.ToString():"-")} close={(hf?hf.Close:0):0.00} state={pd.State}");}}
   {var so=Environment.GetEnvironmentVariable("HERO_SWINGCAM");if(so!=null){var st=pd.State;bool sw=(st.StartsWith("Forehand")||st.StartsWith("Backhand")||st.StartsWith("Serve"))&&!st.Contains("prepare 0.0");
     if(sw&&swingCamN<int.Parse(Environment.GetEnvironmentVariable("HERO_SWINGCAM_N")??"900")){Directory.CreateDirectory(so);var pt2=game.Player.transform;if(!faceCam){faceCam=Object.Instantiate(game.GameplayCamera);faceCam.enabled=false;foreach(var l in faceCam.GetComponents<AudioListener>())Object.DestroyImmediate(l);}
      faceCam.fieldOfView=38;faceCam.transform.position=pt2.position+pt2.forward*3.2f+pt2.right*1.6f+Vector3.up*1.2f;faceCam.transform.LookAt(pt2.position+Vector3.up*.85f);Capture(faceCam,so+$"/s_{step:D5}.png",540,540);swingCamN++;log.Add($"SWINGCAM f={step} {st}");}}}
  {var pt=game.Player.transform;var o=pt.position;if(!faceCam){faceCam=Object.Instantiate(game.GameplayCamera);faceCam.enabled=false;foreach(var l in faceCam.GetComponents<AudioListener>())Object.DestroyImmediate(l);}var side=faceCam;var orb=Environment.GetEnvironmentVariable("HERO_ORBIT_OUT");if(orb!=null){int o0=int.Parse(Environment.GetEnvironmentVariable("HERO_ORBIT_FROM")??"90"),o1=int.Parse(Environment.GetEnvironmentVariable("HERO_ORBIT_TO")??"200");
    if(step>=o0&&step<=o1&&step%3==0){Directory.CreateDirectory(orb);foreach(var (vn,dir) in new[]{("behind",-pt.forward),("right",pt.right),("front",pt.forward),("left",-pt.right)}){side.fieldOfView=34;side.transform.position=o+dir*3.4f+Vector3.up*1.1f;side.transform.LookAt(o+Vector3.up*.85f);Capture(side,orb+$"/o_{step:D4}_{vn}.png",420,420);}
     log.Add($"ORBIT f={step} {pd.State} {pd.ProbeArms()}");}}}
   Capture(game.GameplayCamera,Out+"/gameplay"+(HeroTennisDriver.Baseline?"_baseline":"")+$"/g_{step:D5}.png",1280,720);
   step++;
   if(step>=int.Parse(Environment.GetEnvironmentVariable("HERO_FRAMES")??"1800")){log.Add($"GAPS player {game.PlayerGaps} opponent {game.OpponentGaps} honestMisses={game.HonestMisses} rivalWhiffs={game.RivalWhiffs}"+(game.GetComponent<TennisJuice>()?$" perfects={game.GetComponent<TennisJuice>().Perfects} smashes={game.GetComponent<TennisJuice>().Smashes} whiffs={game.GetComponent<TennisJuice>().Whiffs}":""));File.WriteAllLines(Out+(HeroTennisDriver.Baseline?"/gameplay_log_baseline.txt":"/gameplay_log.txt"),log);Finish();}
  }
 }catch(Exception e){Fail(e);}}
 static ModularHeroLook look(GameObject g)=>g.GetComponent<ModularHeroLook>();
 static HashSet<string> shot=new HashSet<string>();static Camera side;static string prevState="",prevRival="";static TennisGame.Phase prevFlow;static int lastWhiff=-999,lastScore=-999,runEnd=-999,contactFrame=-999;static bool hooked;
 static string P1=>Path.GetFullPath("../ArtDir/screenshots/plan1_fluidity");
 static void Plan1Stills(TennisGame game,HeroTennisDriver pd,HeroTennisDriver od){
  if(!hooked){hooked=true;shot.Clear();TennisGame.Whiffed+=()=>lastWhiff=step;TennisGame.ScoreChanged+=x=>lastScore=step;TennisGame.ContactMade+=(a,b,c)=>contactFrame=step;}
  if(!side){side=Object.Instantiate(game.GameplayCamera);side.name="Plan1 side camera";side.enabled=false;foreach(var l in side.GetComponents<AudioListener>())Object.DestroyImmediate(l);}
  string suf=HeroTennisDriver.Baseline?"_before":"";Directory.CreateDirectory(P1);
  void Take(string n,Transform who,Vector3 camOff,float h,float fov){if(shot.Contains(n))return;shot.Add(n);var o=who.position;side.fieldOfView=fov;side.transform.position=o+who.right*camOff.x+Vector3.up*camOff.y+who.forward*camOff.z;side.transform.LookAt(o+Vector3.up*h);Capture(side,P1+"/"+n+suf+".png",960,960);log.Add($"STILL {n} f={step}");}
  var pt=game.Player.transform;var ot=game.Opponent.transform;float mv=new Vector2(game.Player.Speed,game.Player.ForwardSpeed).magnitude;
  if(step>150&&pd.State=="Ready"&&mv<.05f)Take("01_feet_ready_side",pt,new Vector3(3.2f,.55f,.3f),.5f,30);
  if(step>150&&pd.State=="Run"&&mv>2.5f)Take("02_feet_run_mid",pt,new Vector3(3.4f,.6f,.6f),.55f,32);
  if(prevFlow==TennisGame.Phase.PlayerServeToss&&game.Flow!=TennisGame.Phase.PlayerServeToss)Take("03_serve_contact_frame",pt,new Vector3(2.2f,1.6f,1.2f),1.75f,34);
  if(contactFrame==step&&step>150)Take("04_rally_contact_frame",pt,new Vector3(2.0f,1.1f,2.4f),.8f,36);
  if(step-lastWhiff==14)Take("05_whiff_no_clip",pt,new Vector3(1.6f,1.2f,2.6f),.9f,38);
  if(prevState=="Run"&&pd.State!="Run")runEnd=step;
  if(step-runEnd==5)Take("06_runstop_no_clip",pt,new Vector3(1.4f,1.1f,2.6f),.9f,38);
  if(step-lastWhiff==70)Take("07_postwhiff_recover",pt,new Vector3(1.6f,1.2f,2.6f),.9f,38);
  if(step-lastScore>60&&step-lastScore<400&&game.Flow==TennisGame.Phase.PlayerServeHold&&od.State=="Ready"&&lastScore>0)Take("08_ai_nextpoint_ready",ot,new Vector3(1.6f,1.3f,3.0f),.9f,38);
  prevState=pd.State;prevFlow=game.Flow;
 }
 static string P1B=>Path.GetFullPath("../ArtDir/screenshots/plan1b_serve_whiff");
 static int serveStart=-999,tossStart=-999;

 static void Plan1BStills(TennisGame game,HeroTennisDriver pd,HeroTennisDriver od){
  if(!hooked){hooked=true;shot.Clear();TennisGame.Whiffed+=()=>lastWhiff=step;TennisGame.ScoreChanged+=x=>lastScore=step;TennisGame.ContactMade+=(a,b,c)=>contactFrame=step;}
  if(!side){side=Object.Instantiate(game.GameplayCamera);side.name="Plan1B camera";side.enabled=false;foreach(var l in side.GetComponents<AudioListener>())Object.DestroyImmediate(l);}
  Directory.CreateDirectory(P1B);
  void TakeAt(string n,Vector3 camPos,Vector3 look,float fov){if(shot.Contains(n))return;shot.Add(n);side.fieldOfView=fov;side.transform.position=camPos;side.transform.LookAt(look);Capture(side,P1B+"/"+n+".png",960,960);log.Add($"STILL {n} f={step}");}
  var pt=game.Player.transform;var ot=game.Opponent.transform;var o=pt.position;
  bool hold=game.Flow==TennisGame.Phase.PlayerServeHold;
  if(hold&&(prevFlow!=TennisGame.Phase.PlayerServeHold||serveStart<0))serveStart=step;
  if(game.Flow==TennisGame.Phase.PlayerServeToss&&prevFlow!=TennisGame.Phase.PlayerServeToss)tossStart=step;
  // dribble: ball in a bounce (below the hold), seen from the chest side of the side-on stance
  if(hold&&pd.State.StartsWith("Serve")&&game.BallPosition.y<o.y+.45f&&game.BallPosition.y>o.y+.15f&&step>60)TakeAt("01_dribble_stance_side",o+pt.right*2.6f+pt.forward*.4f+Vector3.up*.9f,o+Vector3.up*.62f,34);
  if(serveStart>=0&&serveStart<400&&step>=serveStart+20&&(tossStart<serveStart||step<=tossStart+40)&&step%2==0){Directory.CreateDirectory(P1B+"/seq");side.fieldOfView=30;side.transform.position=o+pt.right*3.0f+pt.forward*.8f+Vector3.up*1.0f;side.transform.LookAt(o+Vector3.up*.8f);Capture(side,P1B+$"/seq/s_{step:D4}.png",640,640);
   side.transform.position=o-pt.forward*3.2f+pt.right*.6f+Vector3.up*2.4f;side.transform.LookAt(o+Vector3.up*.9f);Capture(side,P1B+$"/seq/b_{step:D4}.png",640,640);}
  if(tossStart>0&&step-tossStart==3)TakeAt("02_hand_toss",o+pt.right*2.3f+pt.forward*1.2f+Vector3.up*1.3f,o+pt.right*.3f+Vector3.up*1.25f,36);
  if(prevFlow==TennisGame.Phase.PlayerServeToss&&game.Flow!=TennisGame.Phase.PlayerServeToss)TakeAt("03_serve_contact",o+pt.right*2.2f+pt.forward*1.2f+Vector3.up*1.6f,o+Vector3.up*1.75f,34);
  if(hold&&step-serveStart==45){var hd=o+Vector3.up*1.25f;TakeAt("04_head_back_no_eyes",hd-pt.forward*1.5f+Vector3.up*1.1f,hd,30);}
  foreach(var (wf,wn) in new[]{(6,"a"),(12,"b"),(18,"c"),(28,"d"),(36,"e"),(44,"f")})if(step-lastWhiff==wf){TakeAt("05_whiff_"+wn+"_L",o-pt.right*1.8f+pt.forward*2.4f+Vector3.up*1.1f,o+Vector3.up*.75f,40);shot.Remove("x");TakeAt("05_whiff_"+wn+"_R",o+pt.right*1.8f+pt.forward*2.4f+Vector3.up*1.1f,o+Vector3.up*.75f,40);}
  if(step-lastWhiff==58)TakeAt("06_whiff_to_ready_blend",o+pt.right*1.4f+pt.forward*2.8f+Vector3.up*1.1f,o+Vector3.up*.8f,40);
  if(game.Flow==TennisGame.Phase.Rally&&step>400&&od.State=="Ready"){var gc=game.GameplayCamera.transform.position;TakeAt("07_ai_shadow",gc,ot.position+Vector3.up*.4f,9);}
  if(lastScore>0&&step-lastScore==25){TakeAt("10a_no_emote_point_end_player",o+pt.forward*3f+pt.right*1.2f+Vector3.up*1.2f,o+Vector3.up*.8f,40);TakeAt("10b_no_emote_point_end_rival",ot.position+ot.forward*3f+ot.right*1.2f+Vector3.up*1.2f,ot.position+Vector3.up*.8f,40);}
  {var so=Environment.GetEnvironmentVariable("HERO_STROKE_OUT");if(so!=null&&(pd.State.StartsWith("Forehand")||pd.State.StartsWith("Backhand"))&&!pd.State.Contains("prepare")&&strokeShots<8&&step%2==0){
    if(pd.State!=lastStrokeState&&pd.State.EndsWith("swing"))strokeShots++;lastStrokeState=pd.State;Directory.CreateDirectory(so);
    foreach(var (vn,dir) in new[]{("front",pt.forward),("right",pt.right),("behind",-pt.forward)}){side.fieldOfView=34;side.transform.position=o+dir*3.2f+Vector3.up*1.1f;side.transform.LookAt(o+Vector3.up*.8f);Capture(side,so+$"/k_{step:D4}_{vn}.png",400,400);}
    log.Add($"STROKE f={step} {pd.State} rk={pd.RacketBeforeClear:0.000}>{pd.FinalRacketPenetration:0.000} arm={pd.FinalArmPenetration:0.000}");}}
  {var fr=Environment.GetEnvironmentVariable("HERO_FRAMING_OUT");if(fr!=null&&(step==300||step==420)){Directory.CreateDirectory(fr);var cam=game.GameplayCamera;var P=pt.position;float lead=Mathf.Clamp(P.x,-5f,5f);
    var cands=new[]{("a_current",new Vector3(lead*.55f,3.5f,P.z-4.4f),new Vector3(lead*.35f,1.15f,P.z+8f),56f),("b_back_narrow",new Vector3(lead*.5f,4.2f,P.z-6.4f),new Vector3(lead*.25f,1.0f,P.z+11f),44f),("c_higher_tele",new Vector3(lead*.45f,5.0f,P.z-7.5f),new Vector3(lead*.2f,.9f,P.z+12.5f),38f),("d_mid",new Vector3(lead*.5f,4.0f,P.z-5.6f),new Vector3(lead*.3f,1.05f,P.z+10.5f),46f),("e_rivalbias",new Vector3(lead*.55f,3.6f,P.z-4.6f),Vector3.Lerp(new Vector3(lead*.35f,1.15f,P.z+8f),ot.position+Vector3.up*.9f,.55f),40f),("f_rivalbias_tight",new Vector3(lead*.5f,3.8f,P.z-4.8f),Vector3.Lerp(new Vector3(lead*.35f,1.15f,P.z+8f),ot.position+Vector3.up*.9f,.7f),34f)};
    var sp=side;foreach(var (n,pos,lk,fov) in cands){sp.transform.position=pos;sp.transform.LookAt(lk);sp.fieldOfView=fov;Capture(sp,fr+$"/{step}_{n}.png",1280,720);
     var rs=sp.WorldToScreenPoint(ot.position);var rh=sp.WorldToScreenPoint(ot.position+Vector3.up*1.45f);var ps=sp.WorldToScreenPoint(P);var ph=sp.WorldToScreenPoint(P+Vector3.up*1.45f);
     log.Add($"FRAMING f={step} {n} rivalPx={(rh.y-rs.y)*720f/sp.pixelHeight:0} playerPx={(ph.y-ps.y)*720f/sp.pixelHeight:0} playerBottomY={ps.y*720f/sp.pixelHeight:0}");}}}
  if(Environment.GetEnvironmentVariable("HERO_BLOBTEST")!=null&&step==302){TakeAt("zz_blob_player",o+Vector3.up*3f+pt.forward*.5f,o,40);TakeAt("zz_blob_rival",ot.position+Vector3.up*3f+ot.forward*.5f,ot.position,40);}
  prevState=pd.State;prevFlow=game.Flow;
 }
 static string P2=>Path.GetFullPath("../ArtDir/screenshots/plan2_fun_feel");
 static int swingStart=-999,lastGradeF=-999,rivalHit=-999,beatStart=-999;static string lastGrade="";static bool p2hooked;static TennisJuice.Beat prevBeat;
 static void Plan2Stills(TennisGame game,HeroTennisDriver pd,HeroTennisDriver od){
  if(!p2hooked){p2hooked=true;shot.Clear();TennisGame.Whiffed+=()=>lastWhiff=step;TennisGame.ScoreChanged+=x=>lastScore=step;TennisGame.ContactMade+=(a,b,c)=>{contactFrame=step;lastGrade=b.ToString()+(c?"+super":"");lastGradeF=step;log.Add($"P2CONTACT f={step} grade={lastGrade} kind={game.Player.Kind}");};TennisGame.OpponentStruck+=()=>{rivalHit=step;log.Add($"P2RIVAL f={step}");};}
  if(!side){side=Object.Instantiate(game.GameplayCamera);side.name="Plan2 camera";side.enabled=false;foreach(var l in side.GetComponents<AudioListener>())Object.DestroyImmediate(l);}
  Directory.CreateDirectory(P2);
  void TakeAt(string n,Vector3 camPos,Vector3 look,float fov){if(shot.Contains(n))return;shot.Add(n);side.fieldOfView=fov;side.transform.position=camPos;side.transform.LookAt(look);Capture(side,P2+"/"+n+".png",960,960);log.Add($"STILL {n} f={step}");}
  void Game(string n){if(shot.Contains(n))return;shot.Add(n);Capture(game.GameplayCamera,P2+"/"+n+".png",1280,720);log.Add($"STILL {n} f={step}");}
  var pt=game.Player.transform;var ot=game.Opponent.transform;var o=pt.position;var juice=game.GetComponent<TennisJuice>();
  Vector3 sideCam=o+pt.right*2.4f+pt.forward*1.8f+Vector3.up*1.2f;
  if(game.Player.Swinging&&!wasSw&&game.Flow==TennisGame.Phase.Rally&&step>150)swingStart=step;
  // input vs contact: the frame the swing starts (input) and the contact frame of the same stroke
  if(swingStart==step&&!shot.Contains("04a_input_no_fx")){TakeAt("04a_input_no_fx",sideCam,o+Vector3.up*.8f,40);inputShotF=step;}
  if(contactFrame==step&&shot.Contains("04a_input_no_fx")&&!shot.Contains("04b_contact_fx")&&step-inputShotF<25)TakeAt("04b_contact_fx",sideCam,o+Vector3.up*.8f,40);
  if(contactFrame==step&&step>150&&lastGrade!="Perfect"&&!lastGrade.Contains("super")&&game.Player.Kind!=TennisActor.Stroke.Smash){TakeAt("01_routine_hit_contact",sideCam,o+Vector3.up*.8f,40);Game("01b_routine_hit_gamecam");}
  if(juice&&juice.Current!=prevBeat&&juice.Current!=TennisJuice.Beat.None){beatStart=step;log.Add($"P2BEAT f={step} {juice.Current}");}
  if(juice&&(juice.Current==TennisJuice.Beat.Perfect||juice.Current==TennisJuice.Beat.Smash)){if(step-beatStart==1)TakeAt("02_perfect_or_smash_contact",sideCam,o+Vector3.up*.9f,40);if(step-beatStart==6)Game("02b_perfect_or_smash_gamecam_beat");}
  if(step-lastWhiff==5){TakeAt("03_whiff_pop",o+pt.right*1.6f+pt.forward*2.6f+Vector3.up*1.1f,o+Vector3.up*.8f,40);}
  if(step-lastWhiff==8)Game("03b_whiff_gamecam_beat");
  if(rivalHit==step&&step>150)TakeAt("05_ai_contact_honest",ot.position+ot.right*-2.4f+ot.forward*1.8f+Vector3.up*1.2f,ot.position+Vector3.up*.8f,40);
  wasSw=game.Player.Swinging;prevBeat=juice?juice.Current:TennisJuice.Beat.None;
 }
 static bool wasSw;static int inputShotF;
 static string P2B=>Path.GetFullPath(Environment.GetEnvironmentVariable("HERO_PLAN")=="2C"?"../ArtDir/screenshots/plan2c_camera_ux":"../ArtDir/screenshots/plan2b_spectacle");static int serveLaunchF=-999,ultEndF=-999,ultIdx;static string ultWho="";static TennisGame.Phase prevFlow2;static string prevShot="None";static int shotStart,ultCount,reactCount;
 static void Plan2BStills(TennisGame game,HeroTennisDriver pd,HeroTennisDriver od){
  Directory.CreateDirectory(P2B);var j=game.GetComponent<TennisJuice>();if(!j)return;
  if(!p2hooked){p2hooked=true;TennisGame.Whiffed+=()=>lastWhiff=step;TennisGame.ScoreChanged+=x=>lastScore=step;TennisGame.ContactMade+=(a,b,c)=>log.Add($"P2CONTACT f={step} grade={b}{(c?"+super":"")} kind={game.Player.Kind}");TennisGame.OpponentStruck+=()=>log.Add($"P2RIVAL f={step}");}
  string sh=j.ShotName+(j.Current!=TennisJuice.Beat.None?"_"+j.Current:"");
  if(prevShot.StartsWith("Ultimate")&&!sh.StartsWith("Ultimate")){ultEndF=step;ultIdx++;ultWho=game.PlayerUltimate<.01f&&game.RivalUltimate>.01f?"player":"rival";}
  if(prevFlow2==TennisGame.Phase.PlayerServeToss&&game.Flow==TennisGame.Phase.Rally)serveLaunchF=step;
  if(sh!=prevShot){shotStart=step;log.Add($"P2BSHOT f={step} {sh}");if(sh.StartsWith("Ultimate"))ultCount++;if(sh.StartsWith("Reaction"))reactCount++;}
  int k=step-shotStart;void G(string n){var p=P2B+"/"+n+".png";if(File.Exists(p))return;Capture(game.GameplayCamera,p,1280,720);log.Add($"STILL {n} f={step}");}
  if(sh=="Cut_Perfect"&&k==3)G("01_perfect_cut");if(sh=="Cut_Smash"&&k==3)G("01_smash_cut");
  if(sh=="BallTrack"&&k==4)G("02_ball_track");
  if(sh=="Toss"&&k==12)G("02_serve_toss_cam");
  if(sh=="Cut_Whiff"&&k==6)G("03_whiff_crash_zoom");
  if(sh=="Reaction"&&reactCount<=2){if(k==18)G($"05_reaction{reactCount}_winner");if(k==50)G($"05_reaction{reactCount}_loser");}
  if(sh=="Ultimate"&&ultCount<=2){foreach(var (fr,n) in new[]{(4,"a_freeze_void"),(22,"b_orbit"),(44,"c_orbit_late"),(58,"d_release")})if(k==fr)G($"06_ultimate{ultCount}_{n}");}
  if(sh=="None"&&game.Flow==TennisGame.Phase.Rally&&step>200&&j!=null&&game.BallPosition.z>6&&game.BallVelocity.z>0)G("04_rival_readable_midrally");
  if(Environment.GetEnvironmentVariable("HERO_PLAN")=="2C"){
   foreach(var (d,n) in new[]{(0,"a_contact"),(8,"b_flight"),(16,"c_flight_late")})if(serveLaunchF>0&&step-serveLaunchF==d&&serveLaunchF<600)G($"07_serve_{n}_playcam");
   foreach(var (d,n) in new[]{(0,"a_live"),(8,"b_live"),(16,"c_live")})if(ultEndF>0&&step-ultEndF==d&&ultIdx<=3)G($"08_after_ultimate{ultIdx}_{ultWho}_{n}");}
  prevShot=sh;prevFlow2=game.Flow;
 }
 static void EnvAudit(TennisGame game,string dir){
  Directory.CreateDirectory(dir);var lines=new List<string>();
  var arena=GameObject.Find("Tropical tennis resort v3 — live arena");var island=GameObject.Find("Tropical island");
  foreach(var root in new[]{arena,island}){if(!root)continue;
   foreach(var r in root.GetComponentsInChildren<Renderer>(true)){var mf=r.GetComponent<MeshFilter>();int tris=mf&&mf.sharedMesh?mf.sharedMesh.triangles.Length/3:0;var b=r.bounds;
    lines.Add($"R\t{root.name.Substring(0,6)}\t{r.name}\t{string.Join(",",r.sharedMaterials.Select(m=>m?m.name:"null"))}\t{b.center.x:0.0}\t{b.center.y:0.0}\t{b.center.z:0.0}\t{b.size.x:0.0}\t{b.size.y:0.0}\t{b.size.z:0.0}\t{tris}\t{r.enabled}\t{(r.sharedMaterials.Any(m=>m&&m.shader&&m.shader.name.Contains("Error"))?"PINK":"")}");}}
  File.WriteAllLines(dir+"/renderers.tsv",lines);
  {var kitRoot=GameObject.Find("EnvV4 postcard kit");int kt=0,kn=0;if(kitRoot)foreach(var mf in kitRoot.GetComponentsInChildren<MeshFilter>()){kn++;kt+=mf.sharedMesh.triangles.Length/3;}
   int hidden=0,hiddenTris=0;foreach(var r in arena.GetComponentsInChildren<MeshRenderer>(true))if(!r.enabled){var mf=r.GetComponent<MeshFilter>();hidden++;if(mf&&mf.sharedMesh)hiddenTris+=mf.sharedMesh.triangles.Length/3;}
   var mats=new HashSet<Material>();if(kitRoot)foreach(var r in kitRoot.GetComponentsInChildren<Renderer>())mats.Add(r.sharedMaterial);
   File.WriteAllText(dir+"/kit_stats.txt",$"kit objects={kn} kit tris={kt} kit materials={mats.Count} hidden arena renderers={hidden} hidden tris={hiddenTris}");}
  var cam=Object.Instantiate(game.GameplayCamera);foreach(var l in cam.GetComponents<AudioListener>())Object.DestroyImmediate(l);cam.enabled=false;
  var P=game.Player.transform.position;var O=game.Opponent.transform.position;
  var views=new (string,Vector3,Vector3,float)[]{
   ("01_gameplay",game.GameplayCamera.transform.position,game.GameplayCamera.transform.position+game.GameplayCamera.transform.forward*10,game.GameplayCamera.fieldOfView),
   ("02_react_player_bg",P+new Vector3(.9f,1.15f,3f),P+Vector3.up*.85f,38),
   ("03_react_rival_bg",O+new Vector3(-.9f,1.15f,-3f),O+Vector3.up*.85f,38),
   ("04_left_side_low",P+new Vector3(-2.2f,.5f,2.5f),P+new Vector3(-8,2,0),50),
   ("05_right_side_low",P+new Vector3(2.2f,.5f,2.5f),P+new Vector3(8,2,0),50),
   ("06_ultimate_orbit_left",P+new Vector3(-2.3f,1.2f,.4f),P+Vector3.up*1f,40),
   ("07_ultimate_orbit_back",P+new Vector3(.3f,1.4f,-2.2f),P+Vector3.up*1f,40),
   ("08_rival_orbit",O+new Vector3(2.2f,1.2f,.3f),O+Vector3.up*1f,40),
   ("09_toss_up",P+new Vector3(-.5f,.4f,-1.4f),P+new Vector3(0,4,1.5f),48),
   ("10_aerial_flyover",new Vector3(-60,45,-70),new Vector3(0,0,0),45),
   ("11_aerial_other",new Vector3(70,40,60),new Vector3(0,0,0),45),
   ("12_top_down",new Vector3(0,120,0.01f),Vector3.zero,60),
   ("13_house_from_court",new Vector3(6,3.5f,-6),new Vector3(23,4,5),50),
   ("14_house_front_close",new Vector3(10,4,5),new Vector3(23,4,5),60),
   ("15_left_lawn_close",new Vector3(-16,2.2f,-8),new Vector3(-27,2,6),55),
   ("16_south_behind_player",new Vector3(0,2.5f,-16),new Vector3(0,3,-45),60)};
  foreach(var (n,pos,look,fov) in views){cam.transform.position=pos;cam.transform.LookAt(look);cam.fieldOfView=fov;cam.farClipPlane=800;Capture(cam,dir+"/"+n+".png",960,540);}
  log.Add("ENVAUDIT done");Finish();
 }
 static void Finish(){SessionState.SetInt("HeroGP",0);Time.captureFramerate=0;if(Application.isBatchMode)EditorApplication.Exit(0);else EditorApplication.isPlaying=false;}
 static void Capture(Camera cam,string path,int w,int h){var rt=new RenderTexture(w,h,24,RenderTextureFormat.ARGB32){antiAliasing=4};var prev=RenderTexture.active;var old=cam.targetTexture;cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;var tex=new Texture2D(w,h,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,w,h),0,0);tex.Apply();File.WriteAllBytes(path,tex.EncodeToPNG());cam.targetTexture=old;RenderTexture.active=prev;Object.DestroyImmediate(tex);rt.Release();Object.DestroyImmediate(rt);}
 static void Fail(Exception e){Debug.LogException(e);Directory.CreateDirectory(Out);File.WriteAllText(Out+"/error.txt",e.ToString());SessionState.SetInt("HeroGP",0);if(Application.isBatchMode)EditorApplication.Exit(1);}
}}
