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
[InitializeOnLoad] public static class HeroLegacyReview {
 const string R=HeroLookImporter.Root;
 static string Out=>Path.GetFullPath("../ArtDir/screenshots/v4_legacy");
 static readonly string[] Names={"Ready","JumpServe","Forehand","Backhand","RunForward","RunRight","RunLeft","Volley","Smash"};
 static ModularHeroLook hero;static TennisActor actor;static Transform sourceModel;static int frame,segment=-1;static bool posed;
 static Dictionary<string,Transform> target,source;static Dictionary<string,Quaternion> sourceRest,targetRest;static Dictionary<string,Vector3> sourcePos,targetPos;
 sealed class ReviewSkin { public SkinnedMeshRenderer source;public Mesh mesh;public Vector3[] vertices,normals;public BoneWeight[] weights;public Matrix4x4[] bind; }
 static List<ReviewSkin> reviewSkins;
 static void PrepareReviewSkins(){reviewSkins=new List<ReviewSkin>();foreach(var skin in hero.GetComponentsInChildren<SkinnedMeshRenderer>()){
  var go=new GameObject("Review baked "+skin.name);go.transform.SetParent(skin.transform,false);var mesh=Object.Instantiate(skin.sharedMesh);mesh.MarkDynamic();go.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterials=skin.sharedMaterials;renderer.shadowCastingMode=skin.shadowCastingMode;renderer.receiveShadows=skin.receiveShadows;
  reviewSkins.Add(new ReviewSkin{source=skin,mesh=mesh,vertices=skin.sharedMesh.vertices,normals=skin.sharedMesh.normals,weights=skin.sharedMesh.boneWeights,bind=skin.sharedMesh.bindposes});skin.enabled=false;
 }}
 static void BakeReviewPose(){foreach(var s in reviewSkins){
  var matrices=s.source.bones.Select((b,i)=>s.source.transform.worldToLocalMatrix*b.localToWorldMatrix*s.bind[i]).ToArray();var verts=new Vector3[s.vertices.Length];var norms=new Vector3[verts.Length];
  for(int i=0;i<verts.Length;i++){var w=s.weights[i];var p=s.vertices[i];var n=s.normals[i];
   verts[i]=matrices[w.boneIndex0].MultiplyPoint3x4(p)*w.weight0+matrices[w.boneIndex1].MultiplyPoint3x4(p)*w.weight1+matrices[w.boneIndex2].MultiplyPoint3x4(p)*w.weight2+matrices[w.boneIndex3].MultiplyPoint3x4(p)*w.weight3;
   norms[i]=(matrices[w.boneIndex0].MultiplyVector(n)*w.weight0+matrices[w.boneIndex1].MultiplyVector(n)*w.weight1+matrices[w.boneIndex2].MultiplyVector(n)*w.weight2+matrices[w.boneIndex3].MultiplyVector(n)*w.weight3).normalized;
  }s.mesh.vertices=verts;s.mesh.normals=norms;s.mesh.RecalculateBounds();
 }}
 static float scale;static List<string> report;static float jumpMin,jumpMax;
 static HeroLegacyReview(){EditorApplication.update+=Tick;}
 public static void Run(){Directory.CreateDirectory(Out);EditorSceneManager.OpenScene("Assets/Scenes/Hero01OwnedTennis.unity");SessionState.SetBool("HeroLegacyCapture",true);EditorApplication.isPlaying=true;}
 static Dictionary<string,Transform> Bones(Transform root)=>root.GetComponentsInChildren<Transform>(true).GroupBy(t=>t.name).ToDictionary(g=>g.Key,g=>g.First());
 static void Setup(){
  hero=Object.FindFirstObjectByType<ModularHeroLook>();hero.enabled=false;hero.animator.enabled=false;
  foreach(var skin in hero.GetComponentsInChildren<SkinnedMeshRenderer>()){skin.updateWhenOffscreen=true;skin.forceMatrixRecalculationPerRender=true;}
  QualitySettings.skinWeights=SkinWeights.FourBones;
  target=Bones(hero.skeletonRoot);
  var srcAsset=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/StandardCharacters/standard_male_tennis.fbx");var dstAsset=AssetDatabase.LoadAssetAtPath<GameObject>(R+"Models/Hero_01_Mixamo_Bind.fbx");
  var src=Bones(srcAsset.transform);var dst=Bones(dstAsset.transform);sourceRest=new Dictionary<string,Quaternion>();targetRest=new Dictionary<string,Quaternion>();sourcePos=new Dictionary<string,Vector3>();targetPos=new Dictionary<string,Vector3>();
  foreach(var n in new[]{"Root"}.Concat(HeroLookImporter.Mapping().Select(b=>b.boneName))){
   sourceRest[n]=Quaternion.Inverse(srcAsset.transform.rotation)*src[n].rotation;targetRest[n]=Quaternion.Inverse(dstAsset.transform.rotation)*dst[n].rotation;
   sourcePos[n]=srcAsset.transform.InverseTransformPoint(src[n].position);targetPos[n]=dstAsset.transform.InverseTransformPoint(dst[n].position);
  }
  // The Generic FBX's prefab transforms can contain its first animation frame.
  // Recover actual bind matrices from the skin, rather than treating Ready as rest.
  foreach(var n in sourceRest.Keys.ToArray()){
   foreach(var skin in srcAsset.GetComponentsInChildren<SkinnedMeshRenderer>()){
    int i=Array.FindIndex(skin.bones,b=>b&&b.name==n);if(i<0)continue;
    var bind=srcAsset.transform.worldToLocalMatrix*skin.transform.localToWorldMatrix*skin.sharedMesh.bindposes[i].inverse;
    sourceRest[n]=bind.rotation;sourcePos[n]=bind.GetColumn(3);break;
   }
  }
  scale=Vector3.Distance(targetPos["UpperLeg.R"],targetPos["Foot.R"])/Vector3.Distance(sourcePos["UpperLeg.R"],sourcePos["Foot.R"]);
  PrepareReviewSkins();frame=0;segment=-1;posed=false;report=new List<string>();
  var cam=Camera.main;cam.transform.position=new Vector3(13.25f,1.4f,5.5f);cam.transform.LookAt(new Vector3(12,1.08f,0));cam.fieldOfView=38;
 }
 static void StartSegment(int index){
  if(actor){report.Add(Names[segment]+" target hip vertical range="+(jumpMax-jumpMin).ToString("F4")+"m");Object.DestroyImmediate(actor.gameObject);}
  actor=new GameObject("Legacy motion driver (hidden)").AddComponent<TennisActor>();actor.Build(false,new Color(.95f,.62f,.26f),false);
  sourceModel=actor.transform.GetChild(0);source=Bones(sourceModel);
  foreach(var r in actor.GetComponentsInChildren<Renderer>())r.enabled=false;
  for(int i=0;i<30;i++){actor.Advance(1f/60,0);actor.Pose();}
  segment=index;jumpMin=float.MaxValue;jumpMax=float.MinValue;
 }
 static void Tick(){if(!SessionState.GetBool("HeroLegacyCapture",false)||!EditorApplication.isPlaying)return;
  try{
   if(!hero)Setup();int index=frame/120,local=frame%120;
   if(index>=Names.Length){report.Add(Names[segment]+" target hip vertical range="+(jumpMax-jumpMin).ToString("F4")+"m");File.WriteAllLines(Out+"/capture_report.txt",report);SessionState.SetBool("HeroLegacyCapture",false);if(Application.isBatchMode)EditorApplication.Exit(0);else EditorApplication.isPlaying=false;return;}
   if(index!=segment)StartSegment(index);
   if(!posed){
    if(local==15){switch(index){case 1:actor.Serve(.9f);break;case 2:actor.Swing(.8f,false,TennisActor.Stroke.Drive);break;case 3:actor.Swing(.8f,true,TennisActor.Stroke.Drive);break;case 7:actor.Swing(.8f,false,TennisActor.Stroke.Volley);break;case 8:actor.Swing(.9f,false,TennisActor.Stroke.Smash);break;}}
    float side=index==5?4.5f:index==6?-4.5f:0,forward=index==4?5f:0;
    actor.transform.position+=new Vector3(side,0,forward)/30;actor.Advance(1f/30,side,forward);actor.Pose();
    hero.skeletonRoot.localRotation=sourceModel.localRotation;
    hero.skeletonRoot.localPosition=sourceModel.localPosition*scale;
    // Copy rest-relative world rotations, preserving every V4 bone length and skin bind.
    foreach(var n in sourceRest.Keys){target[n].rotation=hero.skeletonRoot.rotation*(Quaternion.Inverse(sourceModel.rotation)*source[n].rotation*Quaternion.Inverse(sourceRest[n]))*targetRest[n];
     if(n=="Root"||n=="Hips")target[n].position=hero.skeletonRoot.TransformPoint(targetPos[n]+(sourceModel.InverseTransformPoint(source[n].position)-sourcePos[n])*scale);
    }
    // Anatomical segment directions bridge different A-pose angles and bone rolls.
    // Preserve V4 lengths instead of transferring the old arm system's translations.
    foreach(var sideName in new[]{"L","R"}){
     foreach(var limb in new[]{new[]{"UpperArm.","LowerArm.","Hand."},new[]{"UpperLeg.","LowerLeg.","Foot."}}){
      string a=limb[0]+sideName,b=limb[1]+sideName,c=limb[2]+sideName;var endRotation=target[c].rotation;
      var upperDirection=hero.skeletonRoot.TransformDirection(sourceModel.InverseTransformDirection(source[b].position-source[a].position));
      var lowerDirection=hero.skeletonRoot.TransformDirection(sourceModel.InverseTransformDirection(source[c].position-source[b].position));
      target[a].rotation=Quaternion.FromToRotation(target[b].position-target[a].position,upperDirection)*target[a].rotation;
      target[b].rotation=Quaternion.FromToRotation(target[c].position-target[b].position,lowerDirection)*target[b].rotation;
      target[c].rotation=limb[0]=="UpperArm."?target[b].rotation*Quaternion.Inverse(targetRest[b])*targetRest[c]:endRotation;
     }
    }
    // Same runtime string-bed orientation, fitted to the V4 hand/socket and existing blue prop.
    hero.racketGrip.rotation=hero.skeletonRoot.rotation*Quaternion.Inverse(sourceModel.rotation)*Quaternion.LookRotation(actor.StringNormal,actor.StringUp);
    float hip=target["Hips"].position.y;jumpMin=Mathf.Min(jumpMin,hip);jumpMax=Mathf.Max(jumpMax,hip);
    if(hero.GetComponentsInChildren<SkinnedMeshRenderer>().Any(r=>r.bones.Any(b=>!b||!b.IsChildOf(hero.skeletonRoot))))throw new Exception("Wardrobe binding lost");
    BakeReviewPose();posed=true;return;
   }
   posed=false;Capture(Out+"/frame_"+frame.ToString("D4")+".png");frame++;
  }catch(Exception e){Debug.LogException(e);File.WriteAllText(Out+"/error.txt",e.ToString());SessionState.SetBool("HeroLegacyCapture",false);if(Application.isBatchMode)EditorApplication.Exit(1);}
 }
 static void Capture(string path){var cam=Camera.main;var rt=new RenderTexture(960,960,24,RenderTextureFormat.ARGB32);rt.antiAliasing=4;var prev=RenderTexture.active;var target=cam.targetTexture;cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;var tex=new Texture2D(960,960,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,960,960),0,0);tex.Apply();File.WriteAllBytes(path,tex.EncodeToPNG());cam.targetTexture=target;RenderTexture.active=prev;Object.DestroyImmediate(tex);rt.Release();Object.DestroyImmediate(rt);}
}}
