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
// Skin-liveliness proof: renders the HeroBase male in the same neutral studio as HeroBaseMaleFormProof (4 directional lights + trilight ambient)
// with RUNTIME materials described by a JSON case list (no assets are created or modified), and writes PNGs for colour measurement.
//   Unity -batchmode -projectPath Unity -executeMethod GolfArcade.EditorTools.HeroBaseSkinProof.Run -logFile ...
// Config: ../work/male-nape-pelvis-skin/skin/unity_cases.json   Output: ../work/male-nape-pelvis-skin/skin/unity/<case>__<view>.png
[InitializeOnLoad] public static class HeroBaseSkinProof {
 const string Dir="Assets/Characters/HeroBase", Key="HeroBaseSkinProof";
 static string Root=>Path.GetFullPath("../work/male-nape-pelvis-skin/skin");
 [Serializable] class PropF {public string k;public float v;}
 [Serializable] class PropC {public string k;public float[] v;}
 [Serializable] class Case {public string name;public string asset="";public int tone=-1;public bool setColor=false;public string shader="Universal Render Pipeline/Lit";public float[] color=new[]{.74f,.555f,.435f,1f};public string baseMap="";public PropF[] floats=new PropF[0];public PropC[] colors=new PropC[0];public string[] keywords=new string[0];}
 [Serializable] class Cases {public Case[] cases;public string[] views;}
 // name -> yaw, pitch, target (Unity metres), ortho half-height
 static readonly Dictionary<string,(float yaw,float pitch,Vector3 target,float size)> View=new Dictionary<string,(float,float,Vector3,float)>{
  {"head_front",(0f,0f,new Vector3(0f,1.50f,-0.05f),.30f)},{"head_side",(90f,0f,new Vector3(0f,1.50f,-0.03f),.30f)},{"head_q",(35f,2f,new Vector3(0f,1.50f,-0.05f),.30f)},{"head_back",(180f,0f,new Vector3(0f,1.50f,-0.03f),.30f)},
  {"head_qr",(-35f,2f,new Vector3(0f,1.50f,-0.05f),.30f)},{"face_front",(0f,0f,new Vector3(0f,1.565f,-0.05f),.16f)},{"body_front",(0f,0f,new Vector3(0f,0.88f,-0.05f),.95f)},{"body_back",(180f,0f,new Vector3(0f,0.88f,-0.05f),.95f)},
  {"torso_q",(35f,0f,new Vector3(0f,1.15f,-0.05f),.42f)},{"hand_front",(0f,0f,new Vector3(0.60f,0.88f,-0.10f),.15f)}};
 static int caseIdx,viewIdx,startFrame;static bool inited;static Camera camera;static GameObject root;static MeshRenderer bodyR;static Material cur;
 static HeroBaseSkinProof(){EditorApplication.update+=Tick;}
 static Cases Load(){return JsonUtility.FromJson<Cases>(File.ReadAllText(Root+"/unity_cases.json"));}
 public static void Run(){try{
  Directory.CreateDirectory(Root+"/unity");
  SessionState.SetString(Key+"PriorPipeline",AssetDatabase.GetAssetPath(QualitySettings.renderPipeline));
  EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);SetupStudio();
  SessionState.SetInt(Key+"Case",0);SessionState.SetInt(Key+"View",0);SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;
 }catch(Exception ex){Fail(ex);}}
 static void SetupStudio(){
  var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(Dir+"/Rendering/HeroBaseStudioURP.asset");if(!pipeline)throw new Exception("Studio URP missing");QualitySettings.renderPipeline=pipeline;
  RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.46f,.48f,.54f);RenderSettings.ambientEquatorColor=new Color(.38f,.38f,.40f);RenderSettings.ambientGroundColor=new Color(.26f,.23f,.21f);RenderSettings.ambientIntensity=1;RenderSettings.reflectionIntensity=.15f;RenderSettings.fog=false;
  var cam=new GameObject("SkinProofCamera").AddComponent<Camera>();cam.orthographic=true;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.16f,.16f,.16f,1);cam.allowHDR=false;cam.allowMSAA=true;cam.nearClipPlane=.01f;cam.farClipPlane=20;
  cam.GetUniversalAdditionalCameraData().renderPostProcessing=false;
  foreach(var item in new[]{("Key",new Vector3(-2.8f,2.6f,-3.8f),1.0f),("Fill",new Vector3(2.6f,1.4f,-2.4f),.48f),("Back",new Vector3(0,3,2.6f),.55f),("Bounce",new Vector3(0,-1,-3),.30f)}){
   var light=new GameObject("SkinProof_"+item.Item1).AddComponent<Light>();light.type=LightType.Directional;light.color=Color.white;light.intensity=item.Item3;light.shadows=LightShadows.Soft;light.transform.position=item.Item2;light.transform.LookAt(new Vector3(0,1,0));
  }
 }
 static Material Make(Case c){
  Material m;
  if(!string.IsNullOrEmpty(c.asset)){var src=AssetDatabase.LoadAssetAtPath<Material>(c.asset);if(!src)throw new Exception("material asset missing: "+c.asset);m=new Material(src){name=c.name};}   // runtime copy: the asset itself is never modified
  else{var sh=Shader.Find(c.shader);if(!sh)throw new Exception("Shader not found: "+c.shader);m=new Material(sh){name=c.name};}
  Color col=c.tone>=0?GolfArcade.Game.GolferStyle.SkinTones[c.tone]:new Color(c.color[0],c.color[1],c.color[2],1);
  if(c.tone>=0||c.setColor||string.IsNullOrEmpty(c.asset)){if(m.HasProperty("_BaseColor"))m.SetColor("_BaseColor",col);if(m.HasProperty("_Color"))m.SetColor("_Color",col);}
  if(!string.IsNullOrEmpty(c.baseMap)){var t=AssetDatabase.LoadAssetAtPath<Texture2D>(c.baseMap);if(!t)throw new Exception("baseMap missing: "+c.baseMap);m.SetTexture("_BaseMap",t);}
  foreach(var f in c.floats)if(m.HasProperty(f.k))m.SetFloat(f.k,f.v);else throw new Exception("float property missing on "+m.shader.name+": "+f.k);
  foreach(var p in c.colors)if(m.HasProperty(p.k))m.SetColor(p.k,new Color(p.v[0],p.v[1],p.v[2],p.v.Length>3?p.v[3]:1));else throw new Exception("colour property missing on "+m.shader.name+": "+p.k);
  foreach(var k in c.keywords)m.EnableKeyword(k);
  return m;
 }
 static void Configure(string view){
  var cv=View[view];float yr=cv.yaw*Mathf.Deg2Rad,pr=cv.pitch*Mathf.Deg2Rad;var dir=new Vector3(Mathf.Sin(yr)*Mathf.Cos(pr),Mathf.Sin(pr),-Mathf.Cos(yr)*Mathf.Cos(pr));
  camera.orthographicSize=cv.size;camera.transform.position=cv.target+dir*5f;camera.transform.LookAt(cv.target);
  var turn=Quaternion.Euler(0,-cv.yaw,0);
  foreach(var item in new[]{("Key",new Vector3(-2.8f,2.6f,-3.8f)),("Fill",new Vector3(2.6f,1.4f,-2.4f)),("Back",new Vector3(0,3,2.6f)),("Bounce",new Vector3(0,-1,-3))}){
   var light=GameObject.Find("SkinProof_"+item.Item1).transform;light.position=turn*item.Item2;light.LookAt(new Vector3(0,1,0));
  }
 }
 static void Tick(){if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying)return;try{
  var cfg=Load();int ci=SessionState.GetInt(Key+"Case",0),vi=SessionState.GetInt(Key+"View",0);
  var views=cfg.views!=null&&cfg.views.Length>0?cfg.views:new[]{"head_front","head_side","head_q","head_back"};
  if(!inited||!camera||!root){camera=Object.FindFirstObjectByType<Camera>();root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Dir+"/HeroBase_Male.prefab"));
   foreach(var r in root.GetComponentsInChildren<MeshRenderer>())if(r.name=="Body_M")bodyR=r;
   if(!bodyR)throw new Exception("Body_M not found");inited=true;startFrame=Time.frameCount;cur=null;return;}
  if(cur==null){cur=Make(cfg.cases[ci]);bodyR.sharedMaterial=cur;Configure(views[vi]);startFrame=Time.frameCount;return;}
  if(Time.frameCount-startFrame<4)return;
  Capture(cfg.cases[ci].name+"__"+views[vi]);
  vi++;bool newCase=false;if(vi>=views.Length){vi=0;ci++;newCase=true;}
  SessionState.SetInt(Key+"Case",ci);SessionState.SetInt(Key+"View",vi);
  if(ci>=cfg.cases.Length){SessionState.SetBool(Key,false);EditorApplication.isPlaying=false;EditorApplication.delayCall+=Finish;return;}
  if(newCase){Object.DestroyImmediate(cur);cur=null;return;}
  Configure(views[vi]);startFrame=Time.frameCount;
 }catch(Exception ex){Fail(ex);}}
 static void Capture(string name){
  int W=900,H=900;var rt=new RenderTexture(W,H,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB){antiAliasing=4};rt.Create();camera.targetTexture=rt;camera.Render();var old=RenderTexture.active;RenderTexture.active=rt;
  var tex=new Texture2D(W,H,TextureFormat.RGBA32,false);tex.ReadPixels(new Rect(0,0,W,H),0,0);tex.Apply();File.WriteAllBytes(Root+"/unity/"+name+".png",tex.EncodeToPNG());
  camera.targetTexture=null;RenderTexture.active=old;rt.Release();Object.Destroy(rt);Object.Destroy(tex);
 }
 static void Finish(){QualitySettings.renderPipeline=AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(SessionState.GetString(Key+"PriorPipeline",""));File.WriteAllText(Root+"/unity_done.txt",DateTime.Now.ToString("s"));Debug.Log("SKIN_PROOF_COMPLETE");EditorApplication.Exit(0);}
 static void Fail(Exception ex){Debug.LogException(ex);Directory.CreateDirectory(Root);File.WriteAllText(Root+"/unity_error.txt",ex.ToString());SessionState.SetBool(Key,false);EditorApplication.Exit(1);}
}}
