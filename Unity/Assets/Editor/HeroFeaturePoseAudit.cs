using System;using System.IO;using System.Linq;using UnityEngine;using GolfArcade.Tennis;
namespace GolfArcade.EditorTools {
 public static class HeroFeaturePoseAudit {
  [Serializable] public class Part {public string name;public float[] positions,normals;public int[] triangles;public float role,paintLift;}
  [Serializable] public class Report {public string label;public float[] right,up,forward;public Part body;public Part[] face;}
  public static void Save(MatchHeroLook hero,string label,string directory){
   Directory.CreateDirectory(directory);var bake=new Mesh();hero.body.BakeMesh(bake);var toBody=hero.body.transform.worldToLocalMatrix*hero.face.transform.localToWorldMatrix;var nm=toBody.inverse.transpose;var f=hero.face.GetComponent<MeshFilter>().sharedMesh;var p=f.vertices.Select(toBody.MultiplyPoint3x4).ToArray();var n=f.normals.Select(q=>nm.MultiplyVector(q).normalized).ToArray();
   HeroFaceBasis.Get(hero,out var right,out var up,out var forward);Vector3 Map(Vector3 v)=>toBody.MultiplyVector(v).normalized;float[] V(Vector3[] a)=>a.SelectMany(q=>new[]{q.x,q.y,q.z}).ToArray();
   var parts=hero.face.sharedMaterials.Select((m,i)=>new Part{name=m?m.name:"none",positions=V(p),normals=V(n),triangles=i<f.subMeshCount?f.GetTriangles(i):new int[0],role=m&&m.HasProperty("_FaceRole")?m.GetFloat("_FaceRole"):0,paintLift=m&&m.HasProperty("_PaintLift")?m.GetFloat("_PaintLift"):0}).ToArray();
   var report=new Report{label=label,right=V(new[]{Map(right)}),up=V(new[]{Map(up)}),forward=V(new[]{Map(forward)}),body=new Part{name=hero.body.sharedMesh.name,positions=V(bake.vertices),normals=V(bake.normals),triangles=bake.triangles},face=parts};
   File.WriteAllText(Path.Combine(directory,label+".json"),JsonUtility.ToJson(report));UnityEngine.Object.DestroyImmediate(bake);
  }
 }
}
