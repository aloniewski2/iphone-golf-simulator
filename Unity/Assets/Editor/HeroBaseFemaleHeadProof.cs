using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;
namespace GolfArcade.EditorTools {
// MicroF1 female head proof: stages the female FBX (Body_F + Face_F) in its OWN folder (never touches the male paths or the shared female prefab),
// builds a staging prefab, and captures plate-aligned head close-ups with the locked HeroBase studio rig.
[InitializeOnLoad] public static class HeroBaseFemaleHeadProof {
 const string Dir="Assets/Characters/HeroBase", Stage="Assets/Characters/HeroBase/FemaleHeadF1", Key="HeroBaseFemaleHeadProof";
 const string SourceFbx="../work/female-micro-f1-head/export/HeroBase_Female_Body.fbx";
 static readonly Color SkinColor=new Color(.90f,.70f,.585f); const float SkinSmoothness=.40f;
 static readonly Color GreyColor=new Color(.80f,.80f,.82f);
 // Face_F layer materials (slot order = facef.MATS); sRGB colours
 static readonly (string name,Color c,float smooth)[] FaceSlots={("Brow",new Color(.20f,.135f,.095f),.34f),("BrowSoft",new Color(.33f,.23f,.17f),.30f),("Sclera",new Color(.86f,.85f,.84f),.62f),
  ("IrisIn",new Color(.37f,.21f,.105f),.20f),("Iris",new Color(.30f,.17f,.085f),.20f),("Limbal",new Color(.12f,.06f,.03f),.22f),("Pupil",new Color(.02f,.015f,.012f),.25f),("Catch",Color.white,.9f),
  ("LidLine",new Color(.07f,.05f,.04f),.40f),("LidLower",new Color(.30f,.17f,.13f),.35f),("Lip",new Color(.93f,.66f,.55f),.42f),("LipUp",new Color(.80f,.51f,.41f),.40f),("Seam",new Color(.50f,.25f,.19f),.30f),("Nostril",new Color(.33f,.15f,.11f),.15f)};
 static string Proof=>Path.GetFullPath("../ArtDir/hero/base_lock/proof");
 static string OutDir=>Proof+"/female_head_f1/unity";
 static int step=-1,startFrame;static Camera camera;static GameObject root;
 static readonly List<string> audit=new List<string>();
 // name -> yaw degrees (0 = front, +90 = camera on +x side, -90 = -x side), pitch, look-at target (Unity metres: x=Blender x, y=Blender z, z=Blender y), ortho half-height
 static readonly string[] Views={"head_front","head_left","head_right","head_q3L","head_q3R","head_back","head_top","face_front","face_q3","face_profile","ear_side","ear_back","eye_front","eye_q3","mouth_front","body_front","body_side"};
 static readonly Dictionary<string,(float yaw,float pitch,Vector3 target,float size)> Close=new Dictionary<string,(float yaw,float pitch,Vector3 target,float size)>{
  {"head_front",(0f,0f,new Vector3(0f,1.56f,-0.01f),.19f)},{"head_left",(90f,0f,new Vector3(0f,1.56f,-0.04f),.19f)},{"head_right",(-90f,0f,new Vector3(0f,1.56f,-0.02f),.19f)},
  {"head_q3L",(-23.5f,0f,new Vector3(0f,1.56f,-0.01f),.19f)},{"head_q3R",(-29.5f,0f,new Vector3(0f,1.56f,-0.01f),.19f)},{"head_back",(180f,0f,new Vector3(0f,1.56f,-0.01f),.19f)},{"head_top",(0f,90f,new Vector3(0f,1.7f,-0.01f),.19f)},
  {"face_front",(0f,0f,new Vector3(0f,1.535f,-0.05f),.11f)},{"face_q3",(-28f,0f,new Vector3(0f,1.535f,-0.05f),.11f)},{"face_profile",(90f,0f,new Vector3(0f,1.535f,-0.05f),.11f)},
  {"ear_side",(90f,0f,new Vector3(0.10f,1.54f,0.016f),.07f)},{"ear_back",(150f,6f,new Vector3(0.10f,1.54f,0.016f),.08f)},
  {"eye_front",(0f,0f,new Vector3(-0.047f,1.557f,-0.11f),.045f)},{"eye_q3",(-30f,0f,new Vector3(-0.047f,1.557f,-0.11f),.05f)},{"mouth_front",(0f,0f,new Vector3(0f,1.478f,-0.12f),.045f)},
  {"body_front",(0f,0f,new Vector3(0f,0.86f,0f),.92f)},{"body_side",(90f,0f,new Vector3(0f,0.86f,0f),.92f)}};
 static HeroBaseFemaleHeadProof(){EditorApplication.update+=Tick;}
 static string Hash(string path){using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant();}
 static Material LitMat(string path,Color c,float smooth,bool sclera=false){
  var m=AssetDatabase.LoadAssetAtPath<Material>(path);
  if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
  m.SetColor("_BaseColor",c);m.SetTexture("_BaseMap",null);m.SetFloat("_Metallic",0);m.SetFloat("_Smoothness",smooth);m.SetFloat("_Surface",0);m.SetFloat("_AlphaClip",0);m.SetFloat("_Cull",2);
  m.SetFloat("_SpecularHighlights",1);m.SetFloat("_EnvironmentReflections",1);m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");m.renderQueue=2000;
  if(sclera){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",new Color(.10f,.095f,.085f));m.globalIlluminationFlags=MaterialGlobalIlluminationFlags.BakedEmissive;}
  EditorUtility.SetDirty(m);return m;}
 public static void Run(){try{
  audit.Clear();Directory.CreateDirectory(OutDir);
  if(!AssetDatabase.IsValidFolder(Stage)){AssetDatabase.CreateFolder(Dir,"FemaleHeadF1");}
  if(!AssetDatabase.IsValidFolder(Stage+"/Materials"))AssetDatabase.CreateFolder(Stage,"Materials");
  SessionState.SetString(Key+"PriorPipeline",AssetDatabase.GetAssetPath(QualitySettings.renderPipeline));
  string path=Stage+"/HeroBase_Female_Body_F1.fbx";File.Copy(SourceFbx,path,true);
  AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
  var imp=(ModelImporter)AssetImporter.GetAtPath(path);imp.globalScale=1;imp.useFileScale=true;imp.bakeAxisConversion=true;
  imp.animationType=ModelImporterAnimationType.None;imp.importAnimation=false;imp.importCameras=false;imp.importLights=false;
  imp.meshCompression=ModelImporterMeshCompression.Off;imp.importNormals=ModelImporterNormals.Import;imp.importTangents=ModelImporterTangents.CalculateMikk;
  imp.isReadable=true;imp.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;imp.materialLocation=ModelImporterMaterialLocation.InPrefab;imp.SaveAndReimport();
  var skin=LitMat(Stage+"/Materials/Female_Form_Skin.mat",SkinColor,SkinSmoothness);
  var grey=LitMat(Stage+"/Materials/Female_Form_Grey.mat",GreyColor,.30f);
  var body=new GameObject("HeroBase_Female_Body_F1");var imported=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));imported.transform.SetParent(body.transform,false);
  var rs=body.GetComponentsInChildren<MeshRenderer>();if(rs.Length!=2)throw new Exception("Female head proof needs Body_F + Face_F meshes, found "+rs.Length);
  MeshRenderer bodyR=null,faceR=null;foreach(var r in rs){if(r.GetComponent<MeshFilter>().sharedMesh.name.StartsWith("Face"))faceR=r;else bodyR=r;}
  if(!bodyR||!faceR)throw new Exception("Body_F / Face_F not found");
  bodyR.name="Body_F";faceR.name="Face_F";
  var bm=new Material[bodyR.sharedMaterials.Length];for(int i=0;i<bm.Length;i++){var n=bodyR.sharedMaterials[i]?bodyR.sharedMaterials[i].name:"";bm[i]=n.Contains("Grey")?grey:skin;audit.Add("BODY_SLOT "+i+" "+n+" -> "+(n.Contains("Grey")?"grey":"skin"));}bodyR.sharedMaterials=bm;
  var fm=new Material[faceR.sharedMaterials.Length];for(int i=0;i<fm.Length;i++){var slotName=faceR.sharedMaterials[i]?faceR.sharedMaterials[i].name:"";var fs=FaceSlots[0];bool found=false;foreach(var cand in FaceSlots)if(slotName=="Face_"+cand.name){fs=cand;found=true;break;}
   if(!found)throw new Exception("Unknown face material slot: "+slotName);fm[i]=LitMat(Stage+"/Materials/Female_Face_"+fs.name+".mat",fs.c,fs.smooth,fs.name=="Sclera");audit.Add("FACE_SLOT "+i+" "+slotName+" -> "+fs.name);}faceR.sharedMaterials=fm;
  foreach(var a in body.GetComponentsInChildren<Animator>())Object.DestroyImmediate(a);
  var mesh=bodyR.GetComponent<MeshFilter>().sharedMesh;int faceTris=faceR.GetComponent<MeshFilter>().sharedMesh.triangles.Length/3,faceVerts=faceR.GetComponent<MeshFilter>().sharedMesh.vertexCount;
  PrefabUtility.SaveAsPrefabAsset(body,Stage+"/HeroBase_Female_Body_F1.prefab");Object.DestroyImmediate(body);
  var main=new GameObject("HeroBase_Female_F1");var child=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Stage+"/HeroBase_Female_Body_F1.prefab"));child.transform.SetParent(main.transform,false);
  PrefabUtility.SaveAsPrefabAsset(main,Stage+"/HeroBase_Female_F1.prefab");Object.DestroyImmediate(main);AssetDatabase.SaveAssets();
  root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Stage+"/HeroBase_Female_F1.prefab"));
  var bounds=root.GetComponentsInChildren<MeshRenderer>().Select(r=>r.bounds).Aggregate((a,b)=>{a.Encapsulate(b);return a;});
  audit.Add("FEMALE_HEAD_F1_IMPORTED body_mesh="+mesh.name+" body_triangles="+mesh.triangles.Length/3+" body_vertices="+mesh.vertexCount+" face_triangles="+faceTris+" face_vertices="+faceVerts+" height_metres="+bounds.size.y+" renderers=2(Body_F+Face_F) root_identity="+(root.transform.position==Vector3.zero&&root.transform.rotation==Quaternion.identity&&root.transform.localScale==Vector3.one)+" hair=False face_texture=False");
  audit.Add("SOURCE_FBX_SHA256="+Hash(SourceFbx));audit.Add("STAGED_FBX_SHA256="+Hash(path));audit.Add("PREFAB_SHA256="+Hash(Stage+"/HeroBase_Female_F1.prefab"));audit.Add("globalScale="+imp.globalScale+" fileScale="+imp.fileScale+" compression="+imp.meshCompression+" normals="+imp.importNormals);
  Object.DestroyImmediate(root);root=null;
  EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);SetupStudio();
  SessionState.SetBool(Key,true);step=-1;EditorApplication.isPlaying=true;
 }catch(Exception ex){Fail(ex);}}
 static void SetupStudio(){
  var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(Dir+"/Rendering/HeroBaseStudioURP.asset");if(!pipeline)throw new Exception("Existing studio URP missing");QualitySettings.renderPipeline=pipeline;
  RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.46f,.48f,.54f);RenderSettings.ambientEquatorColor=new Color(.38f,.38f,.40f);RenderSettings.ambientGroundColor=new Color(.26f,.23f,.21f);RenderSettings.ambientIntensity=1;RenderSettings.reflectionIntensity=.15f;RenderSettings.fog=false;
  camera=new GameObject("FemaleHead_ProofCamera").AddComponent<Camera>();camera.orthographic=true;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.16f,.16f,.16f,1);camera.allowHDR=false;camera.allowMSAA=true;camera.nearClipPlane=.01f;camera.farClipPlane=20;
  camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;
  foreach(var item in new[]{("Key",new Vector3(-2.8f,2.6f,-3.8f),1.0f),("Fill",new Vector3(2.6f,1.4f,-2.4f),.48f),("Back",new Vector3(0,3,2.6f),.55f),("Bounce",new Vector3(0,-1,-3),.30f)}){
   var light=new GameObject("FemaleHead_"+item.Item1).AddComponent<Light>();light.type=LightType.Directional;light.color=Color.white;light.intensity=item.Item3;light.shadows=LightShadows.Soft;light.transform.position=item.Item2;light.transform.LookAt(new Vector3(0,1,0));
  }
 }
 static void Configure(int index){
  var cv=Close[Views[index]];
  float yr=cv.yaw*Mathf.Deg2Rad,pr=cv.pitch*Mathf.Deg2Rad;var dir=new Vector3(Mathf.Sin(yr)*Mathf.Cos(pr),Mathf.Sin(pr),-Mathf.Cos(yr)*Mathf.Cos(pr));
  camera.orthographicSize=cv.size;camera.transform.position=cv.target+dir*5f;
  if(cv.pitch>=89f)camera.transform.rotation=Quaternion.Euler(90f,0,0);else camera.transform.LookAt(cv.target);
  var turnC=Quaternion.Euler(0,-cv.yaw,0);
  foreach(var item in new[]{("Key",new Vector3(-2.8f,2.6f,-3.8f)),("Fill",new Vector3(2.6f,1.4f,-2.4f)),("Back",new Vector3(0,3,2.6f)),("Bounce",new Vector3(0,-1,-3))}){
   var light=GameObject.Find("FemaleHead_"+item.Item1).transform;light.position=turnC*item.Item2;light.LookAt(new Vector3(0,1,0));
  }
 }
 static void Tick(){if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying)return;try{
  if(step<0){camera=Object.FindFirstObjectByType<Camera>();root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Stage+"/HeroBase_Female_F1.prefab"));step=0;startFrame=Time.frameCount;Configure(0);return;}
  if(Time.frameCount-startFrame<5)return;Capture(Views[step]);step++;
  if(step==Views.Length){SessionState.SetBool(Key,false);EditorApplication.isPlaying=false;File.WriteAllLines(Proof+"/female-head-f1-unity-audit.txt",audit);EditorApplication.delayCall+=Finish;return;}
  Configure(step);startFrame=Time.frameCount;
 }catch(Exception ex){Fail(ex);}}
 static void Capture(string name){
  int w=1280,h=1000;
  var rt=new RenderTexture(w,h,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB){antiAliasing=4};rt.Create();camera.targetTexture=rt;camera.Render();var old=RenderTexture.active;RenderTexture.active=rt;
  var tex=new Texture2D(w,h,TextureFormat.RGBA32,false);tex.ReadPixels(new Rect(0,0,w,h),0,0);tex.Apply();File.WriteAllBytes(OutDir+"/"+name+".png",tex.EncodeToPNG());
  // alpha-coverage mask (SSAO off, unlit white) for silhouette metrics
  var rs=root.GetComponentsInChildren<MeshRenderer>();var mats=rs.Select(r=>r.sharedMaterials).ToArray();var maskMat=new Material(Shader.Find("Universal Render Pipeline/Unlit"));maskMat.SetColor("_BaseColor",Color.white);
  var renderer=AssetDatabase.LoadAssetAtPath<UniversalRendererData>(Dir+"/Rendering/HeroBaseStudio_Renderer.asset");var features=renderer.rendererFeatures.Where(f=>f is ScreenSpaceAmbientOcclusion&&f.isActive).ToArray();foreach(var f in features)f.SetActive(false);
  var mr=new RenderTexture(w,h,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear){antiAliasing=4};mr.Create();camera.targetTexture=mr;var bg=camera.backgroundColor;camera.backgroundColor=Color.clear;
  foreach(var r in rs){var arr=new Material[r.sharedMaterials.Length];for(int i=0;i<arr.Length;i++)arr[i]=maskMat;r.sharedMaterials=arr;}camera.Render();RenderTexture.active=mr;var mt=new Texture2D(w,h,TextureFormat.RGBA32,false,true);mt.ReadPixels(new Rect(0,0,w,h),0,0);mt.Apply();
  var pixels=mt.GetPixels32();if(pixels[0].a!=0)audit.Add("NOTE mask corner pixel covered in view "+name+" (close-up fills the frame)");for(int i=0;i<pixels.Length;i++){byte a=pixels[i].a;pixels[i]=new Color32(a,a,a,255);}mt.SetPixels32(pixels);mt.Apply();File.WriteAllBytes(OutDir+"/"+name+"_mask.png",mt.EncodeToPNG());
  for(int i=0;i<rs.Length;i++)rs[i].sharedMaterials=mats[i];foreach(var f in features)f.SetActive(true);camera.backgroundColor=bg;camera.targetTexture=null;RenderTexture.active=old;rt.Release();mr.Release();Object.Destroy(rt);Object.Destroy(mr);Object.Destroy(tex);Object.Destroy(mt);Object.Destroy(maskMat);
  audit.Add("CAPTURE="+name+" native="+w+"x"+h);
 }
 static void Finish(){QualitySettings.renderPipeline=AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(SessionState.GetString(Key+"PriorPipeline",""));Debug.Log("FEMALE_HEAD_F1_UNITY_PROOF_COMPLETE");EditorApplication.Exit(0);}
 static void Fail(Exception ex){Debug.LogException(ex);Directory.CreateDirectory(Proof);File.WriteAllText(Proof+"/female-head-f1-unity-error.txt",ex.ToString());SessionState.SetBool(Key,false);EditorApplication.Exit(1);}
}}
