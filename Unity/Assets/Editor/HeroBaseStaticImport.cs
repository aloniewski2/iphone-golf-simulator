using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;
namespace GolfArcade.EditorTools {
[InitializeOnLoad] public static class HeroBaseStaticImport {
 const string Dir="Assets/Characters/HeroBase", Key="HeroBaseStaticProof";
 static int step=-1, startFrame; static GameObject root; static Camera camera;
 static string Proof=>Path.GetFullPath("../ArtDir/hero/base_lock/proof");
 static readonly List<string> audit=new List<string>();
 static HeroBaseStaticImport(){EditorApplication.update+=Tick;}
 [Serializable] class Attachment { public string body_fbx,default_hair_fbx; public float[] hair_anchor_metres; public bool body_contains_hair; public float bald_height_m; }
 public static void Run(){
  try {
   SessionState.SetString(Key+"PriorPipeline",AssetDatabase.GetAssetPath(QualitySettings.renderPipeline));
   audit.Clear();
   foreach(var folder in new[]{"Models","Materials","Textures","Bodies","Hair","Previews"})Directory.CreateDirectory(Dir+"/"+folder);
   Directory.CreateDirectory(Proof+"/unity");ConfigureStudioPipeline();
   foreach(var sex in new[]{"Male","Female"}){
    foreach(var filename in new[]{"HeroBase_"+sex+"_Body.fbx","Hair_Default_"+sex+".fbx"})File.Copy("../ArtDir/hero/base_lock/unity_import/"+filename,Dir+"/Models/"+filename,true);
    File.Copy("../ArtDir/hero/base_lock/unity_import/Textures/Head_"+sex+"_Albedo.png",Dir+"/Textures/Head_"+sex+"_Albedo.png",true);
   }
   AssetDatabase.Refresh();
   foreach(var sex in new[]{"Male","Female"}){
    var ti=(TextureImporter)AssetImporter.GetAtPath(Dir+"/Textures/Head_"+sex+"_Albedo.png");ti.sRGBTexture=true;ti.crunchedCompression=false;ti.textureCompression=TextureImporterCompression.Uncompressed;ti.maxTextureSize=2048;ti.mipmapEnabled=true;ti.wrapMode=TextureWrapMode.Clamp;ti.SaveAndReimport();
    var attachment=JsonUtility.FromJson<Attachment>(File.ReadAllText("../ArtDir/hero/base_lock/unity_import/HeroBase_"+sex+"_Attachments.json"));
    if(attachment.body_contains_hair||attachment.hair_anchor_metres.Length!=3)throw new Exception("Invalid independent hair contract: "+sex);
    SavePart(sex,attachment.body_fbx,BodyPath(sex),false);
    SavePart(sex,attachment.default_hair_fbx,HairPath(sex),true);
    var assembled=new GameObject("HeroBase_"+sex);
    var body=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(BodyPath(sex)));body.transform.SetParent(assembled.transform,false);var bodyMesh=body.GetComponentInChildren<MeshFilter>().sharedMesh;
    var hairRoot=new GameObject("HairRoot");hairRoot.transform.SetParent(assembled.transform,false);var a=attachment.hair_anchor_metres;hairRoot.transform.localPosition=new Vector3(a[0],a[1],a[2]);
    var hair=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(HairPath(sex)));hair.transform.SetParent(hairRoot.transform,false);
    PrefabUtility.SaveAsPrefabAsset(assembled,Dir+"/Previews/HeroBase_"+sex+"_DefaultHair.prefab");
    Object.DestroyImmediate(hair);
    PrefabUtility.SaveAsPrefabAsset(assembled,Dir+"/HeroBase_"+sex+".prefab");
    // Actually delete the optional hairstyle, then verify that the independent body is unchanged.
    Object.DestroyImmediate(hairRoot);
    var after=assembled.GetComponentsInChildren<MeshRenderer>();
    if(after.Length!=1||after[0].GetComponent<MeshFilter>().sharedMesh!=bodyMesh)throw new Exception("Deleting hairstyle changed body: "+sex);
    audit.Add(sex+" REMOVE_HAIR_ROOT body_mesh_unchanged=True body_renderers="+after.Length);
    Object.DestroyImmediate(assembled);
    var dependencies=AssetDatabase.GetDependencies(BodyPath(sex),true);
    if(dependencies.Any(p=>p.StartsWith(Dir+"/Hair/")||p.StartsWith(Dir+"/Models/Hair_")||p.StartsWith(Dir+"/Materials/Hair_")))throw new Exception("Body prefab depends on hair: "+sex);
    audit.Add(sex+" BODY_PREFAB hair_dependencies=0 path="+BodyPath(sex)+" scalp_height="+attachment.bald_height_m+" hair_anchor="+new Vector3(a[0],a[1],a[2]));
    var baseDependencies=AssetDatabase.GetDependencies(Dir+"/HeroBase_"+sex+".prefab",true);
    if(baseDependencies.Any(p=>p.StartsWith(Dir+"/Hair/")||p.StartsWith(Dir+"/Models/Hair_")||p.StartsWith(Dir+"/Materials/Hair_")))throw new Exception("Bald base depends on hair: "+sex);
    audit.Add(sex+" BASE_PREFAB bald=True hair_dependencies=0");
   }
   // Remove only the obsolete generated combined models, after replacing their prefab references.
   foreach(var sex in new[]{"Male","Female"}){string legacy=Dir+"/Models/HeroBase_"+sex+".fbx";if(AssetDatabase.LoadMainAssetAtPath(legacy))AssetDatabase.DeleteAsset(legacy);}
   File.WriteAllLines(Proof+"/unity-mesh-material-audit.txt",audit);AssetDatabase.SaveAssets();
   if(File.Exists(Proof+"/unity-error.txt"))File.Delete(Proof+"/unity-error.txt");
   SessionState.SetBool(Key,true);step=-1;EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);SetupStudio();EditorApplication.isPlaying=true;
  } catch(Exception ex){Fail(ex);}
 }
 static void ConfigureStudioPipeline(){
  Directory.CreateDirectory(Dir+"/Rendering");
  const string rendererPath=Dir+"/Rendering/HeroBaseStudio_Renderer.asset", pipelinePath=Dir+"/Rendering/HeroBaseStudioURP.asset";
  var source=AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Resources/Tennis/Rendering/TennisURP_Renderer.asset");
  var renderer=AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
  if(!renderer){
   renderer=Object.Instantiate(source);renderer.name="HeroBaseStudio_Renderer";renderer.rendererFeatures.Clear();AssetDatabase.CreateAsset(renderer,rendererPath);
   foreach(var original in source.rendererFeatures){var feature=Object.Instantiate(original);feature.name=original.name;AssetDatabase.AddObjectToAsset(feature,renderer);renderer.rendererFeatures.Add(feature);}
  }
  foreach(var feature in renderer.rendererFeatures){
   if(!(feature is ScreenSpaceAmbientOcclusion))continue;
   var so=new SerializedObject(feature);var settings=so.FindProperty("m_Settings");
   settings.FindPropertyRelative("Radius").floatValue=.025f;settings.FindPropertyRelative("Intensity").floatValue=.75f;settings.FindPropertyRelative("DirectLightingStrength").floatValue=.12f;
   settings.FindPropertyRelative("Downsample").boolValue=false;settings.FindPropertyRelative("Samples").intValue=0;settings.FindPropertyRelative("NormalSamples").intValue=2;settings.FindPropertyRelative("BlurQuality").intValue=0;
   so.ApplyModifiedPropertiesWithoutUndo();feature.SetActive(true);EditorUtility.SetDirty(feature);
  }
  var rso=new SerializedObject(renderer);var map=rso.FindProperty("m_RendererFeatureMap");map.arraySize=renderer.rendererFeatures.Count;
  for(int i=0;i<renderer.rendererFeatures.Count;i++){AssetDatabase.TryGetGUIDAndLocalFileIdentifier(renderer.rendererFeatures[i],out _,out long id);map.GetArrayElementAtIndex(i).longValue=id;}
  rso.ApplyModifiedPropertiesWithoutUndo();renderer.SetDirty();EditorUtility.SetDirty(renderer);
  var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelinePath);
  if(!pipeline){pipeline=Object.Instantiate(AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Resources/Tennis/Rendering/TennisURP.asset"));pipeline.name="HeroBaseStudioURP";AssetDatabase.CreateAsset(pipeline,pipelinePath);}
  var pso=new SerializedObject(pipeline);var list=pso.FindProperty("m_RendererDataList");list.arraySize=1;list.GetArrayElementAtIndex(0).objectReferenceValue=renderer;pso.FindProperty("m_DefaultRendererIndex").intValue=0;pso.ApplyModifiedPropertiesWithoutUndo();
  pipeline.msaaSampleCount=4;pipeline.maxAdditionalLightsCount=4;EditorUtility.SetDirty(pipeline);AssetDatabase.SaveAssets();
  audit.Add("STUDIO_PIPELINE="+pipelinePath+" SSAO_radius_metres=.025 intensity=.75 downsample=False samples=12 additional_lights=4 MSAA=4");
 }
 static string BodyPath(string sex)=>Dir+"/Bodies/HeroBase_"+sex+"_Body.prefab";
 static string HairPath(string sex)=>Dir+"/Hair/Hair_Default_"+sex+".prefab";
 static void SavePart(string sex,string filename,string prefabPath,bool isHair){
  string path=Dir+"/Models/"+filename;var imp=(ModelImporter)AssetImporter.GetAtPath(path);
  imp.globalScale=1;imp.useFileScale=true;imp.bakeAxisConversion=true;imp.animationType=ModelImporterAnimationType.None;imp.importAnimation=false;imp.importCameras=false;imp.importLights=false;imp.meshCompression=ModelImporterMeshCompression.Off;imp.importNormals=ModelImporterNormals.Import;imp.importTangents=ModelImporterTangents.CalculateMikk;imp.isReadable=true;imp.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;imp.SaveAndReimport();
  var imported=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
  // Keep FBX axis conversion on an internal mesh child. Public body/style roots are identity transforms.
  var instance=new GameObject(Path.GetFileNameWithoutExtension(prefabPath));imported.transform.SetParent(instance.transform,false);
  var renderers=instance.GetComponentsInChildren<MeshRenderer>();if(renderers.Length!=1)throw new Exception("Expected one independent mesh in "+path);
  var renderer=renderers[0];renderer.name=(isHair?"Hair_":"Body_")+(sex=="Male"?"M":"F");
  renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>MakeMaterial(m.name,sex)).ToArray();
  if(renderer.sharedMaterials.Any(m=>m.name.StartsWith("Hair_"))!=isHair)throw new Exception("Hair material leaked across asset boundary: "+path);
  foreach(var mat in renderer.sharedMaterials)audit.Add(sex+" "+renderer.name+" material="+mat.name+" shader="+mat.shader.name+" surface="+mat.GetFloat("_Surface")+" queue="+mat.renderQueue+" baseMap="+(mat.GetTexture("_BaseMap")?AssetDatabase.GetAssetPath(mat.GetTexture("_BaseMap")):"flat"));
  var mesh=renderer.GetComponent<MeshFilter>().sharedMesh;audit.Add(sex+" "+renderer.name+" vertices="+mesh.vertexCount+" tris="+mesh.triangles.Length/3+" bounds="+mesh.bounds+" normals="+mesh.normals.Length);
  foreach(var animator in instance.GetComponentsInChildren<Animator>())Object.DestroyImmediate(animator);
  PrefabUtility.SaveAsPrefabAsset(instance,prefabPath);
  if(instance.transform.localPosition!=Vector3.zero||instance.transform.localRotation!=Quaternion.identity||instance.transform.localScale!=Vector3.one)throw new Exception("Nonidentity modular prefab root: "+prefabPath);
  audit.Add(sex+" PART_PREFAB="+prefabPath+" root_position=0 root_rotation=0 root_scale=1");Object.DestroyImmediate(instance);
  audit.Add(sex+" importer="+filename+" globalScale="+imp.globalScale+" fileScale="+imp.fileScale+" meshCompression="+imp.meshCompression+" normals="+imp.importNormals+" animator=None");
 }
 static Material MakeMaterial(string name,string sex){
  string clean=name.Replace(" (Instance)","");Color c;float smooth=.38f;
  switch(clean){
   case "Base_Grey_M": c=new Color(.48f,.48f,.48f);break;
   case "Base_Grey_F": c=new Color(.74f,.74f,.74f);break;
   case "Skin_M":c=new Color(.705f,.465f,.305f);break;
   case "Skin_F":c=new Color(.735f,.515f,.355f);break;
   case "Eye_White":c=new Color(.88f,.87f,.82f);smooth=.70f;break;
   case "Iris_Dark":c=new Color(.18f,.095f,.042f);smooth=.48f;break;
   case "Pupil":c=new Color(.006f,.006f,.005f);smooth=.60f;break;
   case "Brow":c=new Color(.075f,.064f,.053f);break;
   case "Mouth":c=sex=="Male"?new Color(.43f,.245f,.16f):new Color(.50f,.29f,.22f);break;
   case "Teeth":c=new Color(.91f,.88f,.80f);smooth=.64f;break;
   case "Lip_M":c=new Color(.7469f,.48025f,.33575f);break;
   case "Lip_F":c=new Color(.8342f,.5865f,.4345f);break;
   case "Ear_Shadow_M":c=new Color(.6776f,.4972f,.374f);break;
   case "Ear_Shadow_F":c=new Color(.7568f,.6072f,.484f);break;
   default:if(clean.StartsWith("Hair_")){c=new Color(.045f,.038f,.032f);smooth=.36f;}else throw new Exception("Unmapped material "+clean);break;
  }
  string materialName=clean=="Mouth"?clean+"_"+sex:clean;string path=Dir+"/Materials/"+materialName+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);if(!material){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));material.name=materialName;AssetDatabase.CreateAsset(material,path);}
  if(clean.StartsWith("Skin_")){material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Dir+"/Textures/Head_"+sex+"_Albedo.png"));c=sex=="Male"?GolfArcade.Game.GolferStyle.SkinTones[2]:Color.white;}   // male atlas is a neutral tintable base (2026-10-02): BaseColor carries the tone; the female atlas still carries its own pigment
  material.SetColor("_BaseColor",c);material.SetFloat("_Metallic",0);material.SetFloat("_Smoothness",smooth);material.SetFloat("_Surface",0);material.SetFloat("_AlphaClip",0);material.SetFloat("_Cull",2);material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");material.DisableKeyword("_ALPHATEST_ON");material.renderQueue=2000;bool receive=!(clean.StartsWith("Skin_")||clean=="Eye_White"||clean=="Iris_Dark"||clean=="Pupil");material.SetFloat("_ReceiveShadows",receive?1:0);if(receive)material.DisableKeyword("_RECEIVE_SHADOWS_OFF");else material.EnableKeyword("_RECEIVE_SHADOWS_OFF");bool spec=!(clean=="Iris_Dark"||clean=="Pupil"||clean.StartsWith("Hair_"));material.SetFloat("_SpecularHighlights",spec?1:0);if(spec)material.DisableKeyword("_SPECULARHIGHLIGHTS_OFF");else material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");return material;
 }
 static void SetupStudio(){
  var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(Dir+"/Rendering/HeroBaseStudioURP.asset");if(!pipeline)throw new Exception("Character studio URP pipeline missing");QualitySettings.renderPipeline=pipeline;
  RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.48f,.48f,.48f);RenderSettings.ambientIntensity=1;RenderSettings.reflectionIntensity=.15f;RenderSettings.fog=false;DynamicGI.UpdateEnvironment();
  var co=new GameObject("HeroBase_ProofCamera");camera=co.AddComponent<Camera>();camera.orthographic=true;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.47f,.47f,.47f,1);camera.allowHDR=false;camera.allowMSAA=true;camera.nearClipPlane=.01f;camera.farClipPlane=20;
  var cd=camera.GetUniversalAdditionalCameraData();cd.renderPostProcessing=false;cd.antialiasing=AntialiasingMode.None;
  Light("Neutral_Key",new Vector3(-2.8f,4f,-3.4f),.88f);Light("Neutral_Fill",new Vector3(2.6f,2f,-1.8f),.52f);Light("Neutral_Back",new Vector3(0,3f,2.6f),.28f);Light("Neutral_Bounce",new Vector3(0,-1f,-3f),.40f);
 }
 static void Light(string name,Vector3 pos,float power){var o=new GameObject(name);var l=o.AddComponent<Light>();l.type=LightType.Directional;l.color=Color.white;l.intensity=power;l.shadows=LightShadows.Soft;o.transform.position=pos;o.transform.LookAt(new Vector3(0,1,0));}
 static void AlignStudio(float angle){
  var turn=Quaternion.Euler(0,-angle*Mathf.Rad2Deg,0);
  foreach(var item in new[]{("Neutral_Key",new Vector3(-2.8f,4f,-3.4f)),("Neutral_Fill",new Vector3(2.6f,2f,-1.8f)),("Neutral_Back",new Vector3(0,3f,2.6f)),("Neutral_Bounce",new Vector3(0,-1f,-3f))}){
   var light=GameObject.Find(item.Item1).transform;light.position=turn*item.Item2;light.LookAt(new Vector3(0,1,0));
  }
 }
 static void Tick(){
  if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying)return;
  try {
   if(step<0){camera=Object.FindFirstObjectByType<Camera>();step=0;startFrame=Time.frameCount;return;}
   if(Time.frameCount-startFrame<4)return;
   if(step>0)Capture(step-1);
   if(step==34){if(root)Object.DestroyImmediate(root);File.WriteAllLines(Proof+"/unity-import.txt",audit);SessionState.SetBool(Key,false);EditorApplication.isPlaying=false;EditorApplication.delayCall+=SaveSceneAndExit;return;}
   Shot(step,out string sex,out _,out _,out float a,out string prefabPath);
   bool optionalHair=step>=12&&step<18;bool bald=!optionalHair;
   if(step<18?step%3==0:(step-18)%8==0){
    if(root)Object.DestroyImmediate(root);root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath));root.name="HeroBase_"+sex+(bald?"_Bald":"");
    var bounds=new Bounds();bool first=true;foreach(var renderer in root.GetComponentsInChildren<Renderer>()){if(first){bounds=renderer.bounds;first=false;}else bounds.Encapsulate(renderer.bounds);}
    if(bald&&root.GetComponentsInChildren<MeshRenderer>().Any(r=>r.name.StartsWith("Hair_")))throw new Exception("Bald proof contains hair");
    audit.Add(sex+(optionalHair?" OPTIONAL_HAIR":step>=6?" BALD":" BASE_BALD")+" WORLD_BOUNDS="+bounds+" HEIGHT="+bounds.size.y+" scale="+root.transform.lossyScale+" renderers="+root.GetComponentsInChildren<Renderer>().Length);
   }
   float sp=1.7f/(sex=="Male"?593f:655f);int sole=sex=="Male"?654:679;
   AlignStudio(a);camera.backgroundColor=sex=="Male"?new Color(.16f,.16f,.16f,1):new Color(.75f,.75f,.75f,1);camera.orthographicSize=360*sp;float h=(sole-360)*sp;camera.transform.position=new Vector3(Mathf.Sin(a)*5,h,-Mathf.Cos(a)*5);camera.transform.LookAt(new Vector3(0,h,0));step++;startFrame=Time.frameCount;
  } catch(Exception ex){Fail(ex);}
 }
 static void Shot(int index,out string sex,out string suffix,out string view,out float angle,out string prefabPath){
  if(index<18){
   sex=(index/3)%2==0?"Male":"Female";suffix=sex=="Male"?"M":"F";
   if(index>=12)suffix+="_withhair";else if(index>=6)suffix+="_bald";
   view=new[]{"front","back","threequarter"}[index%3];angle=index%3==0?0:index%3==1?Mathf.PI:Mathf.PI/5;
   prefabPath=index>=12?Dir+"/Previews/HeroBase_"+sex+"_DefaultHair.prefab":index>=6?BodyPath(sex):Dir+"/HeroBase_"+sex+".prefab";
  }else{
   int orbit=index-18;sex=orbit/8==0?"Male":"Female";suffix=(sex=="Male"?"M":"F")+"_allaround";
   view=new[]{"front","front_right","right","back_right","back","back_left","left","front_left"}[orbit%8];angle=(orbit%8)*Mathf.PI/4;
   prefabPath=Dir+"/HeroBase_"+sex+".prefab";
  }
 }
 static void Capture(int index){
  Shot(index,out _,out string suffix,out string view,out _,out _);var rt=new RenderTexture(1280,720,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);rt.antiAliasing=4;rt.Create();camera.targetTexture=rt;camera.Render();var old=RenderTexture.active;RenderTexture.active=rt;var tex=new Texture2D(1280,720,TextureFormat.RGBA32,false);tex.ReadPixels(new Rect(0,0,1280,720),0,0);tex.Apply();File.WriteAllBytes(Proof+"/unity/"+suffix+"_"+view+".png",tex.EncodeToPNG());var saved=root.GetComponentsInChildren<MeshRenderer>().Select(r=>(r,r.sharedMaterials)).ToArray();var maskMat=new Material(Shader.Find("Universal Render Pipeline/Unlit"));maskMat.SetColor("_BaseColor",Color.white);var bg=camera.backgroundColor;
  var renderer=AssetDatabase.LoadAssetAtPath<UniversalRendererData>(Dir+"/Rendering/HeroBaseStudio_Renderer.asset");var features=renderer.rendererFeatures.Where(f=>f is ScreenSpaceAmbientOcclusion).ToArray();foreach(var f in features)f.SetActive(false);
  var maskRt=new RenderTexture(1280,720,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);maskRt.antiAliasing=4;maskRt.Create();camera.targetTexture=maskRt;camera.backgroundColor=Color.clear;
  foreach(var pair in saved)pair.r.sharedMaterials=pair.sharedMaterials.Select(_=>maskMat).ToArray();camera.Render();RenderTexture.active=maskRt;var maskTex=new Texture2D(1280,720,TextureFormat.RGBA32,false,true);maskTex.ReadPixels(new Rect(0,0,1280,720),0,0);maskTex.Apply();
  var pixels=maskTex.GetPixels32();if(pixels[0].a!=0)throw new Exception("Geometry-mask background alpha is not zero");for(int i=0;i<pixels.Length;i++){byte coverage=pixels[i].a;pixels[i]=new Color32(coverage,coverage,coverage,255);}maskTex.SetPixels32(pixels);maskTex.Apply();File.WriteAllBytes(Proof+"/unity/"+suffix+"_"+view+"_mask.png",maskTex.EncodeToPNG());
  foreach(var pair in saved)pair.r.sharedMaterials=pair.sharedMaterials;foreach(var f in features)f.SetActive(true);camera.backgroundColor=bg;Object.Destroy(maskMat);camera.targetTexture=null;RenderTexture.active=old;rt.Release();maskRt.Release();Object.Destroy(maskTex);Object.Destroy(tex);Object.Destroy(maskRt);Object.Destroy(rt);
 }
 static void SaveSceneAndExit(){
  // Play-mode captures are transient. Save a standalone static preview from the persisted prefabs.
  try{EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);SetupStudio();foreach(var sex in new[]{"Male","Female"}){var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Dir+"/HeroBase_"+sex+".prefab"));go.transform.position=new Vector3(sex=="Male"?-.75f:.75f,0,0);}camera.transform.position=new Vector3(0,.85f,-5);camera.transform.LookAt(new Vector3(0,.85f,0));camera.orthographicSize=1.05f;EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),Dir+"/HeroBase_Studio.unity");AssetDatabase.SaveAssets();Directory.CreateDirectory("/Users/adnanyonathan/Documents/Codex/2026-09-30/open-3/outputs");AssetDatabase.ExportPackage(Dir,"/Users/adnanyonathan/Documents/Codex/2026-09-30/open-3/outputs/HeroBase_Candidate.unitypackage",ExportPackageOptions.Recurse);QualitySettings.renderPipeline=AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(SessionState.GetString(Key+"PriorPipeline",""));Debug.Log("HEROBASE_STATIC_PROOF_COMPLETE");EditorApplication.Exit(0);}catch(Exception ex){Fail(ex);}
 }
 static void Fail(Exception ex){Debug.LogException(ex);Directory.CreateDirectory(Proof);File.WriteAllText(Proof+"/unity-error.txt",ex.ToString());SessionState.SetBool(Key,false);EditorApplication.Exit(1);}
}}
