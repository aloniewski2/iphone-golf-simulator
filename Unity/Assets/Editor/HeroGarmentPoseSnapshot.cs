using System;
using System.IO;
using System.Linq;
using UnityEngine;
using GolfArcade.Tennis;
using Object=UnityEngine.Object;
namespace GolfArcade.EditorTools {
 /// Optional source data for a corrective. Never modifies the live pose.
 public static class HeroGarmentPoseSnapshot {
  [Serializable] public class Part {public string name;public float[] vertices,normals,posedVertices,posedNormals,uv,weights,bindposes,skinMatrices,rendererToRoot;public int[] triangles,indices;public string[] bones;public float bakeOracleMaxMetres;}
  [Serializable] public class Report {public string label,sex,sport;public Part[] parts;}
  [Serializable] public class MatrixPart {public string name;public float[] skinMatrices,shapeWeights,rendererToRoot;public string[] shapeNames;}
  [Serializable] public class MatrixReport {public string label;public float time;public MatrixPart[] parts;}
  public static void SaveMatrices(MatchHeroLook hero,string label,float time,string output){
   Directory.CreateDirectory(output);
   var parts=new[]{hero.body}.Concat(hero.kit.Where(r=>r&&(r.name=="Kit_Top"||r.name=="Kit_Bottom"))).Where(r=>r&&r.sharedMesh.isReadable).Select(r=>{
    var mesh=r.sharedMesh;var toRoot=hero.transform.worldToLocalMatrix*r.transform.localToWorldMatrix;
    return new MatrixPart{name=r.name,skinMatrices=r.bones.SelectMany((bone,index)=>{
     var matrix=r.transform.worldToLocalMatrix*bone.localToWorldMatrix*mesh.bindposes[index];
     return Enumerable.Range(0,16).Select(i=>matrix[i/4,i%4]);
    }).ToArray(),shapeNames=Enumerable.Range(0,mesh.blendShapeCount).Select(mesh.GetBlendShapeName).ToArray(),shapeWeights=Enumerable.Range(0,mesh.blendShapeCount).Select(r.GetBlendShapeWeight).ToArray(),rendererToRoot=Enumerable.Range(0,16).Select(i=>toRoot[i/4,i%4]).ToArray()};
   }).ToArray();
   File.WriteAllText(Path.Combine(output,label+".json"),JsonUtility.ToJson(new MatrixReport{label=label,time=time,parts=parts}));
  }
  public static void Save(MatchHeroLook hero,string label,string output){
   if(string.IsNullOrEmpty(output)||!hero)return;
   Directory.CreateDirectory(output);
   var renderers=new[]{hero.body}.Concat(hero.kit.Where(r=>r&&(r.name=="Kit_Top"||r.name=="Kit_Bottom"))).Where(r=>r&&r.sharedMesh.isReadable).ToArray();
   var report=new Report{label=label,sex=hero.female?"Female":"Male",sport=hero.golfKit?"Golf":"Tennis",parts=renderers.Select(Read).ToArray()};
   File.WriteAllText(Path.Combine(output,label+".json"),JsonUtility.ToJson(report));
  }
  public static void SaveFace(MatchHeroLook hero,string label,string output){
   Directory.CreateDirectory(output);
   var renderers=new[]{hero.body,hero.face as SkinnedMeshRenderer}.Where(r=>r&&r.sharedMesh.isReadable);
   var report=new Report{label=label,sex=hero.female?"Female":"Male",sport=hero.golfKit?"Golf":"Tennis",parts=renderers.Select(Read).ToArray()};
   File.WriteAllText(Path.Combine(output,label+".json"),JsonUtility.ToJson(report));
  }
  static Part Read(SkinnedMeshRenderer r){
   var m=r.sharedMesh;var bake=new Mesh();r.BakeMesh(bake);var p=m.vertices;var w=m.boneWeights;
   var matrices=r.bones.Select((b,i)=>r.transform.worldToLocalMatrix*b.localToWorldMatrix*m.bindposes[i]).ToArray();
   float max=0;var expected=bake.vertices;
   var shapePositions=(Vector3[])p.Clone();
   for(int s=0;s<m.blendShapeCount;s++){
    float amount=r.GetBlendShapeWeight(s)/m.GetBlendShapeFrameWeight(s,m.GetBlendShapeFrameCount(s)-1);if(Mathf.Abs(amount)<.000001f)continue;
    var delta=new Vector3[p.Length];m.GetBlendShapeFrameVertices(s,m.GetBlendShapeFrameCount(s)-1,delta,null,null);for(int i=0;i<p.Length;i++)shapePositions[i]+=delta[i]*amount;
   }
   for(int i=0;i<p.Length;i++){
    var b=w[i];var predicted=matrices[b.boneIndex0].MultiplyPoint3x4(shapePositions[i])*b.weight0+matrices[b.boneIndex1].MultiplyPoint3x4(shapePositions[i])*b.weight1+matrices[b.boneIndex2].MultiplyPoint3x4(shapePositions[i])*b.weight2+matrices[b.boneIndex3].MultiplyPoint3x4(shapePositions[i])*b.weight3;
    max=Mathf.Max(max,(predicted-expected[i]).magnitude);
   }
   float[] V(Vector3[] v)=>v.SelectMany(q=>new[]{q.x,q.y,q.z}).ToArray();
   float[] M(Matrix4x4[] array)=>array.SelectMany(q=>Enumerable.Range(0,16).Select(i=>q[i/4,i%4])).ToArray();
   var owner=r.GetComponentInParent<MatchHeroLook>();var toRoot=owner.transform.worldToLocalMatrix*r.transform.localToWorldMatrix;
   var part=new Part{name=r.name,vertices=V(p),normals=V(m.normals),posedVertices=V(expected),posedNormals=V(bake.normals),uv=m.uv.SelectMany(q=>new[]{q.x,q.y}).ToArray(),triangles=m.triangles,weights=w.SelectMany(q=>new[]{q.weight0,q.weight1,q.weight2,q.weight3}).ToArray(),indices=w.SelectMany(q=>new[]{q.boneIndex0,q.boneIndex1,q.boneIndex2,q.boneIndex3}).ToArray(),bones=r.bones.Select(b=>b.name).ToArray(),bindposes=M(m.bindposes),skinMatrices=M(matrices),rendererToRoot=M(new[]{toRoot}),bakeOracleMaxMetres=max};
   Debug.Log("[HeroGarmentPoseSnapshot] "+r.name+" exact LBS/bake oracle="+max+"m");Object.DestroyImmediate(bake);return part;
  }
 }
}
