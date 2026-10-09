#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using GolfArcade.Tennis;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;
namespace GolfArcade.EditorTools {
// Each render changes one joint only. No production grip helper is invoked.
public static class HeroFingerProbe {
 static string[] joints={"Proximal","Intermediate","Distal"};
 public static void Run() {
  EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
  string output=Environment.GetEnvironmentVariable("HFP_OUT")??Path.GetFullPath("../proof/full-visual-overhaul/finger-joint-probe-1");Directory.CreateDirectory(output);
  var audit=new StringBuilder();var camera=new GameObject("Finger diagnostic").AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.22f,.27f,.34f);camera.orthographic=true;camera.orthographicSize=.12f;
  var key=new GameObject("Key").AddComponent<Light>();key.type=LightType.Directional;key.intensity=1.5f;key.transform.rotation=Quaternion.Euler(30,-30,0);RenderSettings.ambientLight=Color.white*.4f;
  foreach(string sex in new[]{"Male","Female"})foreach(string digit in new[]{"Index","Thumb"})foreach(string mode in new[]{"baseline","rest","Proximal_plus","Proximal_minus","Intermediate_plus","Intermediate_minus","Distal_plus","Distal_minus"}) {
   var root=Object.Instantiate(Resources.Load<GameObject>("Tennis/Customization/Player"+sex));var hero=root.GetComponent<MatchHeroLook>();var driver=root.GetComponent<HeroTennisDriver>();driver.enabled=false;
   driver.slots.First(s=>s.id==HeroTennisDriver.Clip.Ready).clip.SampleAnimation(root,.6f);TennisPerformanceBuild.Pose(root.transform,hero,HeroTennisDriver.Clip.MatchWin,1,false);
   var hand=hero.Bone(HumanBodyBones.LeftHand);var body=hero.body;body.sharedMesh=HeroHandWeights.Prepare(body,hero.female);var palette=body.bones;var binds=body.sharedMesh.bindposes;
   var matrices=new Dictionary<Transform,Matrix4x4>();for(int b=0;b<palette.Length;b++)matrices[palette[b]]=binds[b].inverse;
   Transform B(string d,string j)=>hero.Bone((HumanBodyBones)Enum.Parse(typeof(HumanBodyBones),"Left"+d+j));
   var chain=joints.Select(j=>B(digit,j)).ToArray();var baseline=chain.Select(b=>b.localRotation).ToArray();
   var toHand=matrices[hand].inverse;Vector3 H(Transform b)=>toHand.MultiplyPoint3x4(matrices[b].GetColumn(3));
   // Palm sign measured against actual finger tip direction in hand frame; both signs have proof.
   var palm=Vector3.Cross((H(B("Index","Proximal"))-H(B("Little","Proximal"))).normalized,H(B("Middle","Proximal")).normalized).normalized*-1;
   audit.AppendLine(sex+" "+digit+" "+mode);
   for(int j=0;j<3;j++) {
    var b=chain[j];var relative=matrices[b.parent].inverse*matrices[b];var q=relative.rotation;var fh=toHand*matrices[b];
    var tipDirection=j<2?H(chain[j+1])-H(b):fh.MultiplyVector(Vector3.up);
    var hinge=fh.inverse.MultiplyVector(Vector3.Cross(tipDirection.normalized,palm).normalized).normalized;
    var reconstructed=Matrix4x4.TRS(relative.GetColumn(3),q,b.localScale);float error=0;for(int k=0;k<16;k++)error=Mathf.Max(error,Mathf.Abs(relative[k]-reconstructed[k]));
    audit.AppendLine(b.name+" baseline="+baseline[j]+" bindRest="+q+" angle="+Quaternion.Angle(baseline[j],q)+" scale="+b.localScale+" determinant="+relative.determinant+" recompose="+error+" hinge="+hinge+" relative="+relative);
    if(mode=="rest")b.localRotation=q;
    else if(mode.StartsWith(joints[j]+"_"))b.localRotation=baseline[j]*Quaternion.AngleAxis(mode.EndsWith("plus")?30:-30,hinge);
   }
   var baked=new Mesh();body.BakeMesh(baked,true);var node=new GameObject("Actual evaluated body");node.transform.SetParent(body.transform,false);node.AddComponent<MeshFilter>().sharedMesh=baked;node.AddComponent<MeshRenderer>().sharedMaterials=body.sharedMaterials;
   foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))if(renderer.gameObject!=node)renderer.enabled=false;
   var centre=(chain[0].position+chain[2].position)*.5f;
   foreach(var shot in new[]{("front",new Vector3(-.35f,.04f,.65f)),("side",new Vector3(-.65f,.08f,.12f))}) {
    camera.transform.SetPositionAndRotation(centre+shot.Item2,Quaternion.LookRotation(-shot.Item2));var rt=RenderTexture.GetTemporary(640,640,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Default,4);camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
    var tex=new Texture2D(640,640,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,640,640),0,0);tex.Apply();File.WriteAllBytes(Path.Combine(output,sex+"_"+digit+"_"+mode+"_"+shot.Item1+".png"),tex.EncodeToPNG());camera.targetTexture=null;RenderTexture.active=null;RenderTexture.ReleaseTemporary(rt);Object.DestroyImmediate(tex);
   }
   Object.DestroyImmediate(root);Object.DestroyImmediate(baked);
  }
  File.WriteAllText(Path.Combine(output,"joint-matrices.txt"),audit.ToString());Debug.Log("HERO_FINGER_PROBE_COMPLETE "+output);if(Application.isBatchMode)EditorApplication.Exit(0);
 }
}}
#endif
