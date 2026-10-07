using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using GolfArcade.Tennis;
namespace GolfArcade.EditorTools {
 /// Repairs invalid exported shirt-fit deltas (up to 6.5 metres) from neighbouring valid
 /// garment vertices. Rest geometry, skinning, normals, UVs and every valid delta stay intact.
 public static class GolfGarmentRepair {
  public static void Build() {
   var report=new StringBuilder();
   foreach(var sex in new[]{"Male","Female"}) {
    CoverBody(sex);
    var root=Resources.Load<GameObject>("Tennis/Customization/Player"+sex+"GolfKit");
    var src=root.GetComponent<MatchHeroLook>().kit.Single(r=>r.name=="Kit_Top").sharedMesh;
    var mesh=UnityEngine.Object.Instantiate(src);mesh.name=sex+" golf polo (repaired fit)";mesh.ClearBlendShapes();
    var neighbours=Enumerable.Range(0,src.vertexCount).Select(_=>new HashSet<int>()).ToArray();
    var tri=src.triangles;for(int t=0;t<tri.Length;t+=3)for(int a=0;a<3;a++)for(int b=0;b<3;b++)if(a!=b)neighbours[tri[t+a]].Add(tri[t+b]);
    // UV/normal splits on a seam share the same spatial correction.
    var groups=new Dictionary<Vector3Int,List<int>>();var vertices=src.vertices;
    for(int i=0;i<vertices.Length;i++){var key=Vector3Int.RoundToInt(vertices[i]*100000);if(!groups.TryGetValue(key,out var list))groups[key]=list=new List<int>();list.Add(i);}
    foreach(var list in groups.Values)foreach(int a in list)foreach(int b in list)if(a!=b)neighbours[a].Add(b);
    for(int s=0;s<src.blendShapeCount;s++)for(int f=0;f<src.GetBlendShapeFrameCount(s);f++) {
     var dv=new Vector3[src.vertexCount];var dn=new Vector3[src.vertexCount];var dt=new Vector3[src.vertexCount];src.GetBlendShapeFrameVertices(s,f,dv,dn,dt);
     bool[] bad=dv.Select(v=>!float.IsFinite(v.sqrMagnitude)||v.magnitude>.12f).ToArray();int count=bad.Count(v=>v);float max=dv.Max(v=>v.magnitude);
     for(int pass=0;pass<32&&bad.Any(v=>v);pass++) {
      var repaired=new List<int>();
      for(int i=0;i<bad.Length;i++)if(bad[i]){var valid=neighbours[i].Where(k=>!bad[k]).ToArray();if(valid.Length==0)continue;
       dv[i]=dn[i]=dt[i]=Vector3.zero;foreach(int j in valid){dv[i]+=dv[j];dn[i]+=dn[j];dt[i]+=dt[j];}dv[i]/=valid.Length;dn[i]/=valid.Length;dt[i]/=valid.Length;repaired.Add(i);}
      if(repaired.Count==0)break;foreach(int i in repaired)bad[i]=false;
     }
     if(bad.Any(v=>v))throw new InvalidOperationException("Unresolved correction island in "+sex+" "+src.GetBlendShapeName(s));
     mesh.AddBlendShapeFrame(src.GetBlendShapeName(s),src.GetBlendShapeFrameWeight(s,f),dv,dn,dt);
     report.AppendLine($"{sex} {src.GetBlendShapeName(s)}: repaired {count}/{src.vertexCount} deltas; maximum {max:F4} -> {dv.Max(v=>v.magnitude):F4} m");
    }
    string path="Assets/Resources/Golf/Fitted_"+sex+"_Top.asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
    if(existing){EditorUtility.CopySerialized(mesh,existing);EditorUtility.SetDirty(existing);UnityEngine.Object.DestroyImmediate(mesh);}else AssetDatabase.CreateAsset(mesh,path);
   }
   AssetDatabase.SaveAssets();var output=Environment.GetEnvironmentVariable("GOLF_REPAIR_REPORT");if(!string.IsNullOrEmpty(output))File.WriteAllText(output,report.ToString());Debug.Log(report.ToString());
  }
  // Keep the original body intact; omit only triangles enclosed by the golf polo and waistband.
  // Sleeve and ankle skin remain available when the golf cut differs from the old tennis kit.
  static void CoverBody(string sex) {
   string prefabPath="Assets/Resources/Tennis/Customization/Player"+sex+"GolfKit.prefab";
   var instance=PrefabUtility.LoadPrefabContents(prefabPath);
   try {
    var body=instance.GetComponent<MatchHeroLook>().body;var src=body.sharedMesh;
    var mesh=UnityEngine.Object.Instantiate(src);mesh.name=sex+" golf body (clothing coverage)";
    var v=src.vertices;var weights=src.boneWeights;var allowed=new HashSet<string>{"Hips","Spine","Chest","UpperChest","LeftUpperLeg","RightUpperLeg"};
    float Torso(int i,float w)=>allowed.Contains(body.bones[i].name)?w:0;
    float Weight(BoneWeight w,HashSet<string> names)=> (names.Contains(body.bones[w.boneIndex0].name)?w.weight0:0)+(names.Contains(body.bones[w.boneIndex1].name)?w.weight1:0)+(names.Contains(body.bones[w.boneIndex2].name)?w.weight2:0)+(names.Contains(body.bones[w.boneIndex3].name)?w.weight3:0);
    var torso=new HashSet<string>{"Hips","Spine","Chest","UpperChest"};
    bool Sleeve(int i,string side){int upper=Array.FindIndex(body.bones,b=>b.name==side+"UpperArm"),lower=Array.FindIndex(body.bones,b=>b.name==side+"LowerArm");var a=src.bindposes[upper].inverse.MultiplyPoint3x4(Vector3.zero);var b=src.bindposes[lower].inverse.MultiplyPoint3x4(Vector3.zero);float along=Vector3.Dot(v[i]-a,b-a)/(b-a).sqrMagnitude;return along<.46f&&Weight(weights[i],new HashSet<string>{side+"Shoulder",side+"UpperArm"})>.45f;}
    bool Covered(int i){var w=weights[i];bool waist=v[i].z>(sex=="Female"?.615f:.70f)&&v[i].z<1.115f&&Torso(w.boneIndex0,w.weight0)+Torso(w.boneIndex1,w.weight1)+Torso(w.boneIndex2,w.weight2)+Torso(w.boneIndex3,w.weight3)>.85f;bool chest=v[i].z>1.08f&&v[i].z<1.40f&&Weight(w,torso)>.5f;return waist||chest||Sleeve(i,"Left")||Sleeve(i,"Right");}
    int removed=0;
    for(int sub=0;sub<src.subMeshCount;sub++){var triangles=src.GetTriangles(sub);var keep=new List<int>();for(int i=0;i<triangles.Length;i+=3){if(Covered(triangles[i])&&Covered(triangles[i+1])&&Covered(triangles[i+2])){removed++;continue;}keep.Add(triangles[i]);keep.Add(triangles[i+1]);keep.Add(triangles[i+2]);}mesh.SetTriangles(keep,sub,false);}
    string path="Assets/Resources/Golf/Fitted_"+sex+"_Body.asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
    if(existing){EditorUtility.CopySerialized(mesh,existing);EditorUtility.SetDirty(existing);UnityEngine.Object.DestroyImmediate(mesh);}else{AssetDatabase.CreateAsset(mesh,path);existing=mesh;}
    body.sharedMesh=existing;PrefabUtility.SaveAsPrefabAsset(instance,prefabPath);Debug.Log("WAIST_COVERAGE "+sex+" removed="+removed+" body bounds="+src.bounds);
   }finally{PrefabUtility.UnloadPrefabContents(instance);}
  }

 }
}
