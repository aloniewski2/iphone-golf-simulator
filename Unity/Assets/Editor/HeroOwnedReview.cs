using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using GolfArcade.Tennis;
using Object=UnityEngine.Object;
namespace GolfArcade.EditorTools {
[InitializeOnLoad] public static class HeroOwnedReview {
 const string R=HeroLookImporter.Root;
 static string Out=>Path.GetFullPath("../ArtDir/screenshots/owned");
 static int frame; static bool started, posed, runtimeChecked; static double runtimeStart; static Vector3 runtimeHand; static float runtimeTravel; static List<string> checks=new List<string>();
 static readonly string[] Names={"ReadyIdle","Forehand","Backhand","Serve","Volley"};
 static HeroOwnedReview(){EditorApplication.update+=Tick;}
 [MenuItem("Golf Arcade/Art Direction/Capture Owned Tennis")]
 public static void Run(){try{
  Directory.CreateDirectory(Out);AssetDatabase.Refresh();
  EditorSceneManager.OpenScene("Assets/Scenes/Hero01Look.unity");
  var hero=Object.FindFirstObjectByType<ModularHeroLook>();if(!hero)throw new Exception("V4 scene hero missing");
  var clips=new List<AnimationClip>();
  foreach(var n in Names){
   string p=R+"Models/Hero_"+n+"_v1.fbx";
   var importer=(ModelImporter)AssetImporter.GetAtPath(p);var defs=importer.defaultClipAnimations;
   foreach(var c in defs){c.name="Hero_"+n+"_v1";c.loopTime=n=="ReadyIdle";c.loopPose=false;c.lockRootPositionXZ=true;c.lockRootRotation=true;c.lockRootHeightY=true;c.keepOriginalPositionXZ=true;c.keepOriginalPositionY=true;c.keepOriginalOrientation=true;}
   if(importer.clipAnimations.Length==0 || importer.clipAnimations[0].name!="Hero_"+n+"_v1"){importer.clipAnimations=defs;importer.SaveAndReimport();}
   var clip=AssetDatabase.LoadAllAssetsAtPath(p).OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview"));
   if(!clip.humanMotion)throw new Exception("Non-Humanoid clip "+n);clips.Add(clip);
  }
  hero.idle=clips[0];hero.forehand=clips[1];hero.backhand=clips[2];hero.serve=clips[3];hero.volley=clips[4];
  AddRacket(hero);
  var cam=Camera.main;cam.transform.position=new Vector3(13.35f,1.25f,4.75f);cam.transform.LookAt(new Vector3(12,.95f,0));cam.fieldOfView=37;
  var scenePosition=hero.transform.position;hero.transform.position=Vector3.zero;
  PrefabUtility.SaveAsPrefabAsset(hero.gameObject,R+"Prefabs/Hero_01_OwnedTennis.prefab");hero.transform.position=scenePosition;
  EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),"Assets/Scenes/Hero01OwnedTennis.unity");AssetDatabase.SaveAssets();
  SessionState.SetBool("HeroOwnedCapture",true);started=false;EditorApplication.isPlaying=true;
 }catch(Exception e){Fail(e);}}
 public static void FinalizePrefab(){
  string path=R+"Prefabs/Hero_01_OwnedTennis.prefab";var root=PrefabUtility.LoadPrefabContents(path);
  try{root.transform.position=Vector3.zero;var hero=root.GetComponent<ModularHeroLook>();
   if(!hero.idle||!hero.forehand||!hero.backhand||!hero.serve||!hero.volley||!hero.racketGrip)throw new Exception("Incomplete owned hero prefab");
   PrefabUtility.SaveAsPrefabAsset(root,path);AssetDatabase.SaveAssets();Debug.Log("OWNED_PREFAB_FINALIZED");
  }finally{PrefabUtility.UnloadPrefabContents(root);}
 }
 static Material Material(string name,Color color){string p=R+"Materials/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(p);if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",.2f);AssetDatabase.CreateAsset(m,p);}return m;}
 static void AddRacket(ModularHeroLook hero){
  var hand=hero.skeletonRoot.GetComponentsInChildren<Transform>(true).First(t=>t.name=="Hand.R");
  var old=hand.Find("Hero_Racket");if(old)Object.DestroyImmediate(old.gameObject);
  var grip=new GameObject("Hero_Racket").transform;grip.SetParent(hand,false);
  // FBX hand local Y points toward the fingertips; shaft extends past the thumb web.
  var unit=hand.lossyScale;grip.localScale=new Vector3(1/Mathf.Abs(unit.x),1/Mathf.Abs(unit.y),1/Mathf.Abs(unit.z));
  grip.localPosition=new Vector3(0,.055f/Mathf.Abs(unit.y),0);grip.localRotation=Quaternion.Euler(0,0,-90);hero.racketGrip=grip;
  var blue=Material("Hero_Racket_Blue",new Color(.08f,.40f,.85f));var white=Material("Hero_Racket_Strings",new Color(.83f,.91f,.97f));var dark=Material("Hero_Racket_Grip",new Color(.055f,.09f,.15f));
  Action<string,Vector3,Vector3,float,Material> rod=(name,a,b,r,mat)=>{
   var o=GameObject.CreatePrimitive(PrimitiveType.Cylinder);o.name=name;o.transform.SetParent(grip,false);o.transform.localPosition=(a+b)*.5f;o.transform.localRotation=Quaternion.FromToRotation(Vector3.up,(b-a).normalized);o.transform.localScale=new Vector3(r*2,(b-a).magnitude*.5f,r*2);o.GetComponent<Renderer>().sharedMaterial=mat;Object.DestroyImmediate(o.GetComponent<Collider>());
  };
  rod("Handle",new Vector3(0,-.06f,0),new Vector3(0,.10f,0),.017f,dark);rod("Throat",new Vector3(0,.10f,0),new Vector3(0,.24f,0),.013f,blue);
  for(int i=0;i<32;i++){float a=i*Mathf.PI*2/32,b=(i+1)*Mathf.PI*2/32;rod("Hoop",new Vector3(Mathf.Sin(a)*.135f,.395f+Mathf.Cos(a)*.18f,0),new Vector3(Mathf.Sin(b)*.135f,.395f+Mathf.Cos(b)*.18f,0),.012f,blue);}
  for(int i=-4;i<=4;i++){float x=i*.027f,y=.18f*Mathf.Sqrt(1-x*x/(.135f*.135f));rod("String",new Vector3(x,.395f-y,0),new Vector3(x,.395f+y,0),.0017f,white);}
  for(int i=-5;i<=5;i++){float y=i*.028f,x=.135f*Mathf.Sqrt(1-y*y/(.18f*.18f));rod("String",new Vector3(-x,.395f+y,0),new Vector3(x,.395f+y,0),.0017f,white);}
  var parts=grip.GetComponentsInChildren<MeshFilter>();var mats=new[]{blue,white,dark};var groups=new List<Mesh>();
  foreach(var mat in mats){var combine=parts.Where(p=>p.GetComponent<Renderer>().sharedMaterial==mat).Select(p=>new CombineInstance{mesh=p.sharedMesh,transform=grip.worldToLocalMatrix*p.transform.localToWorldMatrix}).ToArray();var mesh=new Mesh();mesh.CombineMeshes(combine,true,true);groups.Add(mesh);}
  var final=new Mesh{name="Hero_Racket"};final.CombineMeshes(groups.Select(m=>new CombineInstance{mesh=m,transform=Matrix4x4.identity}).ToArray(),false,false);
  Directory.CreateDirectory(R+"Props");string path=R+"Props/Hero_Racket.asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(existing){EditorUtility.CopySerialized(final,existing);Object.DestroyImmediate(final);final=existing;}else AssetDatabase.CreateAsset(final,path);
  foreach(var part in parts)Object.DestroyImmediate(part.gameObject);foreach(var m in groups)Object.DestroyImmediate(m);
  grip.gameObject.AddComponent<MeshFilter>().sharedMesh=final;grip.gameObject.AddComponent<MeshRenderer>().sharedMaterials=mats;
 }
 static void Tick(){
  if(!SessionState.GetBool("HeroOwnedCapture",false)||!EditorApplication.isPlaying)return;
  try{
   var hero=Object.FindFirstObjectByType<ModularHeroLook>();if(!hero||!hero.IdleRunning&&!started)return;
   if(!started){started=true;posed=false;runtimeChecked=false;runtimeStart=EditorApplication.timeSinceStartup;runtimeHand=hero.animator.GetBoneTransform(HumanBodyBones.RightHand).position;runtimeTravel=0;frame=0;checks.Clear();
    if(!hero.animator.isHuman||!hero.animator.avatar.isValid)throw new Exception("Invalid Humanoid");
    if(!hero.PlayStroke(ModularHeroLook.Stroke.Forehand))throw new Exception("Runtime stroke hook failed");
   }
   if(!runtimeChecked){
    var now=hero.animator.GetBoneTransform(HumanBodyBones.RightHand).position;runtimeTravel+=Vector3.Distance(runtimeHand,now);runtimeHand=now;
    if(EditorApplication.timeSinceStartup-runtimeStart<4.1)return;
    if(hero.StrokeRunning||runtimeTravel<.1f)throw new Exception("Automatic stroke playback/return to idle failed");
    checks.Add("Runtime GameTime forehand travelled "+runtimeTravel.ToString("F3")+"m and returned to ready without manual sampling.");runtimeChecked=true;
   }
   // Five clips, 30 fps deterministic Play Mode recording (108 frames each).
   int clipIndex=frame/108,local=frame%108;
   if(clipIndex>=5){
    File.WriteAllLines(Out+"/playmode_checks.txt",checks);SessionState.SetBool("HeroOwnedCapture",false);hero.enabled=false;
    if(Application.isBatchMode)EditorApplication.Exit(0);else EditorApplication.isPlaying=false;return;
   }
   // Let Unity update skinned render data after sampling, before rendering the next editor tick.
   if(!posed){hero.SampleForReview(clipIndex-1,local/30.0);posed=true;return;}
   posed=false;
   bool bound=hero.GetComponentsInChildren<SkinnedMeshRenderer>().All(r=>r.bones.All(b=>b&&b.IsChildOf(hero.skeletonRoot)));
   if(!bound)throw new Exception("Wardrobe bone escaped shared skeleton");
   var hand=hero.animator.GetBoneTransform(HumanBodyBones.RightHand);
   if(!hero.racketGrip.IsChildOf(hand))throw new Exception("Racket not parented to right hand");
   foreach(var r in hero.GetComponentsInChildren<SkinnedMeshRenderer>())if(float.IsNaN(r.bounds.center.x))throw new Exception("Invalid deformation bounds");
   Capture(Camera.main,Out+"/frame_"+frame.ToString("D4")+".png",960,960);
   if(local==32){
    var body=hero.GetComponentsInChildren<SkinnedMeshRenderer>().First(r=>r.name=="Body_Skin");var baked=new Mesh();body.BakeMesh(baked);int bi=Array.IndexOf(body.bones,hand);var weights=body.sharedMesh.boneWeights;var verts=baked.vertices;Vector3 center=Vector3.zero;int count=0;
    for(int k=0;k<weights.Length;k++){var w=weights[k];if((w.boneIndex0==bi&&w.weight0>.5f)||(w.boneIndex1==bi&&w.weight1>.5f)||(w.boneIndex2==bi&&w.weight2>.5f)||(w.boneIndex3==bi&&w.weight3>.5f)){center+=body.transform.TransformPoint(verts[k] / 100f);count++;}}
    checks.Add(Names[clipIndex]+" right hand="+hand.position.ToString("F4")+" grip="+hero.racketGrip.position.ToString("F4")+" palm="+(center/Mathf.Max(1,count)).ToString("F4")+" scale="+hand.lossyScale+" weighted vertices="+count);Object.DestroyImmediate(baked);
   }
   if(local==32){File.Copy(Out+"/frame_"+frame.ToString("D4")+".png",Path.GetFullPath("../ArtDir/screenshots/unity_owned_"+Names[clipIndex].ToLowerInvariant()+".png"),true);checks.Add(Names[clipIndex]+": Humanoid, shared wardrobe bones, right-hand racket, finite skin bounds; contact sample=1.0667s");}
   frame++;
  }catch(Exception e){Fail(e);}
 }
 static void Capture(Camera cam,string path,int w,int h){var rt=new RenderTexture(w,h,24,RenderTextureFormat.ARGB32);rt.antiAliasing=4;var prev=RenderTexture.active;var target=cam.targetTexture;cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;var tex=new Texture2D(w,h,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,w,h),0,0);tex.Apply();File.WriteAllBytes(path,tex.EncodeToPNG());cam.targetTexture=target;RenderTexture.active=prev;Object.DestroyImmediate(tex);rt.Release();Object.DestroyImmediate(rt);}
 static void Fail(Exception e){Debug.LogException(e);Directory.CreateDirectory(Out);File.WriteAllText(Out+"/error.txt",e.ToString());SessionState.SetBool("HeroOwnedCapture",false);if(Application.isBatchMode)EditorApplication.Exit(1);}
}}
