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
[InitializeOnLoad] public static class HeroBaseMaleFormProof {
 const string Dir="Assets/Characters/HeroBase", Key="HeroBaseMaleFormProof";
 const int ExpectedTriangles=66158; // 66,158 since legs/feet FORM2 forefoot toes (was 60358 Micro1h graded head densify; 63,022/63,638 hands FORM2/3); nape+pelvis, torso FORM3 and skin liveliness are position/material edits only
 static readonly Color SkinColor=GolfArcade.Game.GolferStyle.SkinTones[2];  // skin liveliness 2026-10-02: the palette's mid Tan (neutral albedo, tone-driven) instead of a one-off RGB
 const float SkinSmoothness=.40f;
 // Micro1g: Face_M feature layers (slot order = face_layers.COLORS); sRGB colours. Micro1i: + BrowSoft, LidCrease; softer lip tints. Micro1l r2 (2026-10-02, user: lips too feminine): Lip (.76,.52,.43)/.42 -> (.94,.64,.48)/.30, LipUp -> (.757,.493,.333)/.30, Seam (.30,.15,.11) -> (.42,.18,.10)/.28 = the plate's lip/cheek colour ratios (upper .90/.83/.82, lower 1.11/1.09/1.18 of the cheek); earlier Micro1l: LipUp (.60,.38,.31) -> (.72,.48,.37): the plate-measured upper lip band is taller now, the old dark tint overpowered it (plate upper lip ~ (184,122,88))
 static readonly (string name,Color c,float smooth)[] FaceSlots={("Skin",GolfArcade.Game.GolferStyle.SkinTones[2],.40f),("Brow",new Color(.17f,.14f,.115f),.34f),("Sclera",new Color(.80f,.77f,.72f),.62f),
  ("Iris",new Color(.30f,.17f,.085f),.20f),("Pupil",new Color(.025f,.018f,.012f),.25f),("Limbal",new Color(.09f,.045f,.025f),.22f),("Catch",Color.white,.9f),("LidLine",new Color(.115f,.07f,.05f),.40f),("Lip",new Color(.94f,.64f,.48f),.30f),("LipUp",new Color(.757f,.493f,.333f),.30f),
  ("Seam",new Color(.42f,.18f,.10f),.28f),("Nostril",new Color(.26f,.13f,.09f),.25f),("BrowSoft",new Color(.22f,.175f,.14f),.30f),("LidCrease",new Color(.34f,.20f,.14f),.30f)};
 static Material FaceMat(string n,Color c,float sm){string path=Dir+"/Materials/Male_Face_"+n+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
  if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
  m.SetColor("_BaseColor",c);m.SetTexture("_BaseMap",null);m.SetFloat("_Metallic",0);m.SetFloat("_Smoothness",sm);m.SetFloat("_Surface",0);m.SetFloat("_Cull",2);
  if(n=="Sclera"){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",new Color(.10f,.095f,.085f));m.globalIlluminationFlags=MaterialGlobalIlluminationFlags.BakedEmissive;}
  EditorUtility.SetDirty(m);return m;}
 static string Proof=>Path.GetFullPath("../ArtDir/hero/base_lock/proof");
 static int step=-1,startFrame;static Camera camera;static GameObject root;
 static readonly List<string> audit=new List<string>();
 static readonly string[] Views={"front","back","left","right","threequarter","head_side","foot_side","chin_front","chin_threequarter","hand_front","hand_side","hand_threequarter","handL_front","face_front","face_threequarter","face_profile","face_threequarter_back","head_front","head_threequarter","head_profile","ear_side","ear_back","eye_front"};
 // name -> yaw degrees (0 = front, +90 = camera on +x side), pitch degrees, look-at target (Unity metres: x=Blender x, y=Blender z, z=Blender y), ortho half-height
 static readonly Dictionary<string,(float yaw,float pitch,Vector3 target,float size)> Close=new Dictionary<string,(float yaw,float pitch,Vector3 target,float size)>{
  {"chin_front",(0f,0f,new Vector3(0f,1.50f,-0.05f),.19f)},{"chin_threequarter",(35f,-4f,new Vector3(0f,1.49f,-0.05f),.19f)},{"face_front",(0f,0f,new Vector3(0f,1.565f,-0.05f),.16f)},{"face_threequarter",(38f,2f,new Vector3(0f,1.565f,-0.05f),.16f)},{"face_profile",(90f,0f,new Vector3(0f,1.565f,-0.03f),.16f)},{"face_threequarter_back",(140f,4f,new Vector3(0f,1.565f,-0.03f),.16f)},
  {"head_front",(0f,0f,new Vector3(0f,1.58f,-0.05f),.30f)},{"head_threequarter",(32.5f,0f,new Vector3(0f,1.58f,-0.05f),.30f)},{"head_profile",(90f,0f,new Vector3(0f,1.58f,-0.05f),.30f)},
  {"ear_side",(90f,0f,new Vector3(0.09f,1.5628f,-0.0115f),.11f)},{"ear_back",(150f,8f,new Vector3(0.09f,1.5628f,-0.0115f),.12f)},{"eye_front",(0f,0f,new Vector3(0.036f,1.585f,-0.10f),.07f)},
  {"hand_front",(0f,0f,new Vector3(0.60f,0.88f,-0.10f),.15f)},{"hand_side",(90f,0f,new Vector3(0.60f,0.88f,-0.10f),.15f)},{"hand_threequarter",(35f,-12f,new Vector3(0.60f,0.88f,-0.10f),.15f)},
  {"handL_front",(0f,0f,new Vector3(-0.60f,0.88f,-0.10f),.15f)}};
 [Serializable] class Reference {public string name;public float centre_x,sole_y,metres_per_pixel,angle_degrees;}
 [Serializable] class References {public Reference[] views;}
 static HeroBaseMaleFormProof(){EditorApplication.update+=Tick;}
 static string Hash(string path){using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant();}
 public static void VerifySaved(){SessionState.SetBool(Key+"Verified",false);try{
  string path=Dir+"/Models/HeroBase_Male_Body.fbx";var imp=(ModelImporter)AssetImporter.GetAtPath(path);
  var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Dir+"/HeroBase_Male.prefab");if(!prefab)throw new Exception("Saved male prefab missing");
  var instance=Object.Instantiate(prefab);var rs=instance.GetComponentsInChildren<MeshRenderer>();
  if(rs.Length!=2||instance.GetComponentsInChildren<Animator>().Length!=0)throw new Exception("Saved male prefab must contain Body_M + Face_M static meshes");
  var rb=rs[0].name=="Body_M"?rs[0]:rs[1];var mesh=rb.GetComponent<MeshFilter>().sharedMesh;var mat=rb.sharedMaterial;
  if(mesh.triangles.Length/3!=ExpectedTriangles||!mat||mat.GetTexture("_BaseMap")||mat.shader.name!="Universal Render Pipeline/Lit"||mat.GetFloat("_Surface")!=0)throw new Exception("Saved male mesh/material differs from contract");
  if(instance.transform.position!=Vector3.zero||instance.transform.rotation!=Quaternion.identity||instance.transform.localScale!=Vector3.one)throw new Exception("Saved male origin/scale changed");
  if(Hash(path)!=Hash("../ArtDir/hero/base_lock/unity_import/HeroBase_Male_Body.fbx"))throw new Exception("Saved Unity model is stale");
  if(imp.globalScale!=1||imp.fileScale!=1||imp.meshCompression!=ModelImporterMeshCompression.Off||imp.importNormals!=ModelImporterNormals.Import)throw new Exception("Saved import settings differ");
  var bounds=rs[0].bounds;if(bounds.size.y<1.65f||bounds.size.y>1.75f)throw new Exception("Saved male height out of range");
  var lines=new List<string>{"MALE_FORM_SAVED_UNITY_VERIFY=PASS","mesh="+mesh.name+" triangles="+mesh.triangles.Length/3+" vertices="+mesh.vertexCount+" height_metres="+bounds.size.y+" renderers=2(Body_M+Face_M) root_identity=True hair=False face_texture=False",
   "FBX_SHA256="+Hash(path),"SOURCE_FBX_SHA256="+Hash("../ArtDir/hero/base_lock/unity_import/HeroBase_Male_Body.fbx"),"PREFAB_SHA256="+Hash(Dir+"/HeroBase_Male.prefab"),
   "globalScale="+imp.globalScale+" fileScale="+imp.fileScale+" compression="+imp.meshCompression+" normals="+imp.importNormals};
  foreach(var view in Views){
   string file=Proof+"/male_form_d/unity/"+view+".png";if(!File.Exists(file))throw new Exception("Unity capture missing: "+view);lines.Add("CAPTURE="+view+" SHA256="+Hash(file));
  }
  File.WriteAllLines(Proof+"/male-form-unity-audit.txt",lines);Object.DestroyImmediate(instance);SessionState.SetBool(Key+"Verified",true);Debug.Log("MALE_FORM_SAVED_UNITY_VERIFY_PASS");
 }catch(Exception ex){Fail(ex);}}
 public static void Run(){try{
  audit.Clear();Directory.CreateDirectory(Proof+"/male_form_d/unity");Directory.CreateDirectory("../work/male-form/baseline/unity");
  SessionState.SetString(Key+"PriorPipeline",AssetDatabase.GetAssetPath(QualitySettings.renderPipeline));
  foreach(var relative in new[]{"HeroBase_Male.prefab","Bodies/HeroBase_Male_Body.prefab","Models/HeroBase_Male_Body.fbx"}){
   string backup="../work/male-form/baseline/unity/"+Path.GetFileName(relative);if(File.Exists(Dir+"/"+relative)&&!File.Exists(backup))File.Copy(Dir+"/"+relative,backup);
  }
  string path=Dir+"/Models/HeroBase_Male_Body.fbx";File.Copy("../ArtDir/hero/base_lock/unity_import/HeroBase_Male_Body.fbx",path,true);
  AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
  var imp=(ModelImporter)AssetImporter.GetAtPath(path);imp.globalScale=1;imp.useFileScale=true;imp.bakeAxisConversion=true;
  imp.animationType=ModelImporterAnimationType.None;imp.importAnimation=false;imp.importCameras=false;imp.importLights=false;
  imp.meshCompression=ModelImporterMeshCompression.Off;imp.importNormals=ModelImporterNormals.Import;imp.importTangents=ModelImporterTangents.CalculateMikk;
  imp.isReadable=true;imp.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;imp.materialLocation=ModelImporterMaterialLocation.InPrefab;imp.SaveAndReimport();
  // Micro1f: warm soft-plastic skin (art-bible roughness 0.55-0.70 -> smoothness ~0.38), plate skin tone, no texture.
  var material=AssetDatabase.LoadAssetAtPath<Material>(Dir+"/Materials/Male_Form_Skin.mat");
  if(!material){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,Dir+"/Materials/Male_Form_Skin.mat");}
  material.SetColor("_BaseColor",SkinColor);material.SetTexture("_BaseMap",null);
  material.SetFloat("_Metallic",0);material.SetFloat("_Smoothness",SkinSmoothness);material.SetFloat("_Surface",0);material.SetFloat("_AlphaClip",0);material.SetFloat("_Cull",2);
  material.SetFloat("_SpecularHighlights",1);material.SetFloat("_EnvironmentReflections",1);
  material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");material.renderQueue=2000;EditorUtility.SetDirty(material);
  var body=new GameObject("HeroBase_Male_Body");var imported=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));imported.transform.SetParent(body.transform,false);
  var rs=body.GetComponentsInChildren<MeshRenderer>();if(rs.Length!=2)throw new Exception("Male form requires Body_M + Face_M meshes, found "+rs.Length);
  MeshRenderer bodyR=null,faceR=null;foreach(var r in rs){if(r.GetComponent<MeshFilter>().sharedMesh.name.StartsWith("Face"))faceR=r;else bodyR=r;}
  if(!bodyR||!faceR)throw new Exception("Body_M / Face_M not found");
  bodyR.name="Body_M";bodyR.sharedMaterials=new[]{material};faceR.name="Face_M";
  var fm=new Material[faceR.sharedMaterials.Length];for(int i=0;i<fm.Length;i++){var slotName=faceR.sharedMaterials[i]?faceR.sharedMaterials[i].name:"";var fs=FaceSlots[0];bool found=false;foreach(var cand in FaceSlots)if(slotName.EndsWith("_"+cand.name)||slotName=="Face_"+cand.name){fs=cand;found=true;break;}if(!found)throw new Exception("Unknown face material slot: "+slotName);fm[i]=FaceMat(fs.name,fs.c,fs.smooth);audit.Add("FACE_SLOT "+i+" "+slotName+" -> "+fs.name);}faceR.sharedMaterials=fm;
  foreach(var a in body.GetComponentsInChildren<Animator>())Object.DestroyImmediate(a);
  var mesh=bodyR.GetComponent<MeshFilter>().sharedMesh;if(mesh.triangles.Length/3!=ExpectedTriangles)throw new Exception("FBX triangle count differs from saved source");
  audit.Add("FACE_LAYERS mesh="+faceR.GetComponent<MeshFilter>().sharedMesh.name+" triangles="+faceR.GetComponent<MeshFilter>().sharedMesh.triangles.Length/3+" submeshes="+fm.Length);
  PrefabUtility.SaveAsPrefabAsset(body,Dir+"/Bodies/HeroBase_Male_Body.prefab");Object.DestroyImmediate(body);
  var main=new GameObject("HeroBase_Male");var child=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Dir+"/Bodies/HeroBase_Male_Body.prefab"));child.transform.SetParent(main.transform,false);
  // Preserve the pre-existing empty customization anchor without a hairstyle.
  var old=AssetDatabase.LoadAssetAtPath<GameObject>(Dir+"/HeroBase_Male.prefab");var oldAnchor=old?old.transform.Find("HairRoot"):null;
  if(oldAnchor){var anchor=new GameObject("HairRoot").transform;anchor.SetParent(main.transform,false);anchor.localPosition=oldAnchor.localPosition;anchor.localRotation=oldAnchor.localRotation;anchor.localScale=oldAnchor.localScale;}
  PrefabUtility.SaveAsPrefabAsset(main,Dir+"/HeroBase_Male.prefab");Object.DestroyImmediate(main);AssetDatabase.SaveAssets();
  root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Dir+"/HeroBase_Male.prefab"));
  var bounds=root.GetComponentInChildren<MeshRenderer>().bounds;if(bounds.size.y<1.65f||bounds.size.y>1.75f)throw new Exception("Male form height out of range");
  if(root.transform.position!=Vector3.zero||root.transform.rotation!=Quaternion.identity||root.transform.localScale!=Vector3.one)throw new Exception("Male root origin/scale changed");
  audit.Add("MALE_FORM_IMPORTED mesh=Body_M triangles="+mesh.triangles.Length/3+" vertices="+mesh.vertexCount+" height_metres="+bounds.size.y+" renderers=2(Body_M+Face_M) root_identity=True hair=False face_texture=False");
  audit.Add("FBX_SHA256="+Hash(path));audit.Add("SOURCE_FBX_SHA256="+Hash("../ArtDir/hero/base_lock/unity_import/HeroBase_Male_Body.fbx"));
  audit.Add("PREFAB_SHA256="+Hash(Dir+"/HeroBase_Male.prefab"));audit.Add("globalScale="+imp.globalScale+" fileScale="+imp.fileScale+" compression="+imp.meshCompression+" normals="+imp.importNormals);
  Object.DestroyImmediate(root);root=null;
  EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);SetupStudio();
  SessionState.SetBool(Key,true);step=-1;EditorApplication.isPlaying=true;
 }catch(Exception ex){Fail(ex);}}
 static void SetupStudio(){
  var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(Dir+"/Rendering/HeroBaseStudioURP.asset");if(!pipeline)throw new Exception("Existing studio URP missing");QualitySettings.renderPipeline=pipeline;
  // Micro1f: neutral studio with a soft sky/ground split so the warm skin shows form instead of flat fill.
  RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.46f,.48f,.54f);RenderSettings.ambientEquatorColor=new Color(.38f,.38f,.40f);RenderSettings.ambientGroundColor=new Color(.26f,.23f,.21f);RenderSettings.ambientIntensity=1;RenderSettings.reflectionIntensity=.15f;RenderSettings.fog=false;
  camera=new GameObject("Male_Form_ProofCamera").AddComponent<Camera>();camera.orthographic=true;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.16f,.16f,.16f,1);camera.allowHDR=false;camera.allowMSAA=true;camera.nearClipPlane=.01f;camera.farClipPlane=20;
  camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;
  foreach(var item in new[]{("Key",new Vector3(-2.8f,2.6f,-3.8f),1.0f),("Fill",new Vector3(2.6f,1.4f,-2.4f),.48f),("Back",new Vector3(0,3,2.6f),.55f),("Bounce",new Vector3(0,-1,-3),.30f)}){
   var light=new GameObject("MaleForm_"+item.Item1).AddComponent<Light>();light.type=LightType.Directional;light.color=Color.white;light.intensity=item.Item3;light.shadows=LightShadows.Soft;light.transform.position=item.Item2;light.transform.LookAt(new Vector3(0,1,0));
  }
 }
 static void Configure(int index){
  if(Close.TryGetValue(Views[index],out var cv)){
   float yr=cv.yaw*Mathf.Deg2Rad,pr=cv.pitch*Mathf.Deg2Rad;var dir=new Vector3(Mathf.Sin(yr)*Mathf.Cos(pr),Mathf.Sin(pr),-Mathf.Cos(yr)*Mathf.Cos(pr));
   camera.orthographicSize=cv.size;camera.transform.position=cv.target+dir*5f;camera.transform.LookAt(cv.target);
   var turnC=Quaternion.Euler(0,-cv.yaw,0);
   foreach(var item in new[]{("Key",new Vector3(-2.8f,2.6f,-3.8f)),("Fill",new Vector3(2.6f,1.4f,-2.4f)),("Back",new Vector3(0,3,2.6f)),("Bounce",new Vector3(0,-1,-3))}){
    var light=GameObject.Find("MaleForm_"+item.Item1).transform;light.position=turnC*item.Item2;light.LookAt(new Vector3(0,1,0));
   }
   return;
  }
  var refs=JsonUtility.FromJson<References>(File.ReadAllText("../work/male-silhouette/reference-cameras.json"));
  float angle=index==0?0:index==1?Mathf.PI:index==3?-Mathf.PI/2:index==4?Mathf.PI/4:Mathf.PI/2;
  float sp=index<2?refs.views[index].metres_per_pixel:1.7f/593;
  float cx=index<2?refs.views[index].centre_x:640;float sole=index<2?refs.views[index].sole_y:654;
  float offset=(640-cx)*sp,h=index<2?(sole-360)*sp:index==5?1.49f:index==6?.12f:.84f;
  var target=new Vector3(Mathf.Cos(angle)*offset,h,-Mathf.Sin(angle)*offset);
  camera.orthographicSize=index>=5?.26f:360*sp;camera.transform.position=target+new Vector3(Mathf.Sin(angle)*5,0,-Mathf.Cos(angle)*5);camera.transform.LookAt(target);
  // Keep neutral studio illumination relative to the view so back forms remain
  // as inspectable as front forms. Camera framing and source geometry stay fixed.
  var turn=Quaternion.Euler(0,-angle*Mathf.Rad2Deg,0);
  foreach(var item in new[]{("Key",new Vector3(-2.8f,2.6f,-3.8f)),("Fill",new Vector3(2.6f,1.4f,-2.4f)),("Back",new Vector3(0,3,2.6f)),("Bounce",new Vector3(0,-1,-3))}){
   var light=GameObject.Find("MaleForm_"+item.Item1).transform;light.position=turn*item.Item2;light.LookAt(new Vector3(0,1,0));
  }
 }
 static void Tick(){if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying)return;try{
  if(step<0){camera=Object.FindFirstObjectByType<Camera>();root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Dir+"/HeroBase_Male.prefab"));step=0;startFrame=Time.frameCount;Configure(0);return;}
  if(Time.frameCount-startFrame<5)return;Capture(Views[step]);step++;
  if(step==Views.Length){SessionState.SetBool(Key,false);EditorApplication.isPlaying=false;File.WriteAllLines(Proof+"/male-form-unity-audit.txt",audit);EditorApplication.delayCall+=Finish;return;}
  Configure(step);startFrame=Time.frameCount;
 }catch(Exception ex){Fail(ex);}}
 static void Capture(string name){
  var rt=new RenderTexture(1280,720,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB){antiAliasing=4};rt.Create();camera.targetTexture=rt;camera.Render();var old=RenderTexture.active;RenderTexture.active=rt;
  var tex=new Texture2D(1280,720,TextureFormat.RGBA32,false);tex.ReadPixels(new Rect(0,0,1280,720),0,0);tex.Apply();File.WriteAllBytes(Proof+"/male_form_d/unity/"+name+".png",tex.EncodeToPNG());
  var rs=root.GetComponentsInChildren<MeshRenderer>();var mats=rs.Select(r=>r.sharedMaterials).ToArray();var maskMat=new Material(Shader.Find("Universal Render Pipeline/Unlit"));maskMat.SetColor("_BaseColor",Color.white);
  var renderer=AssetDatabase.LoadAssetAtPath<UniversalRendererData>(Dir+"/Rendering/HeroBaseStudio_Renderer.asset");var features=renderer.rendererFeatures.Where(f=>f is ScreenSpaceAmbientOcclusion&&f.isActive).ToArray();foreach(var f in features)f.SetActive(false);
  var mr=new RenderTexture(1280,720,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear){antiAliasing=4};mr.Create();camera.targetTexture=mr;var bg=camera.backgroundColor;camera.backgroundColor=Color.clear;
  foreach(var r in rs)r.sharedMaterials=new[]{maskMat};camera.Render();RenderTexture.active=mr;var mt=new Texture2D(1280,720,TextureFormat.RGBA32,false,true);mt.ReadPixels(new Rect(0,0,1280,720),0,0);mt.Apply();
  var pixels=mt.GetPixels32();if(pixels[0].a!=0)throw new Exception("Mask background is not clear");for(int i=0;i<pixels.Length;i++){byte a=pixels[i].a;pixels[i]=new Color32(a,a,a,255);}mt.SetPixels32(pixels);mt.Apply();File.WriteAllBytes(Proof+"/male_form_d/unity/"+name+"_mask.png",mt.EncodeToPNG());
  for(int i=0;i<rs.Length;i++)rs[i].sharedMaterials=mats[i];foreach(var f in features)f.SetActive(true);camera.backgroundColor=bg;camera.targetTexture=null;RenderTexture.active=old;rt.Release();mr.Release();Object.Destroy(rt);Object.Destroy(mr);Object.Destroy(tex);Object.Destroy(mt);Object.Destroy(maskMat);
  audit.Add("CAPTURE="+name+" native=1280x720 locked_reference_framing=True");
 }
 static void Finish(){VerifySaved();if(!SessionState.GetBool(Key+"Verified",false))return;QualitySettings.renderPipeline=AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(SessionState.GetString(Key+"PriorPipeline",""));Debug.Log("MALE_FORM_UNITY_PROOF_COMPLETE");EditorApplication.Exit(0);}
 static void Fail(Exception ex){Debug.LogException(ex);Directory.CreateDirectory(Proof);File.WriteAllText(Proof+"/male-form-unity-error.txt",ex.ToString());SessionState.SetBool(Key,false);EditorApplication.Exit(1);}
}}
