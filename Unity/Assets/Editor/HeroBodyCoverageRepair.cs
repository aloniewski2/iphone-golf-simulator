using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using GolfArcade.Tennis;
using Object=UnityEngine.Object;

namespace GolfArcade.EditorTools {
 /// Recover original lower-body triangles hidden by the old garment coverage
 /// cut. Positions, skin weights, UVs, bind poses and facial/hand geometry stay
 /// on the current body. The full authored source supplies indices only.
 public static class HeroBodyCoverageRepair {
  public static void Audit(){Run(false);}
  public static void Build(){Run(true);}
  static void Run(bool repair){
   var lines=new List<string>();
   foreach(var sex in new[]{"Male","Female"}) {
    var original=AssetDatabase.LoadAllAssetsAtPath("Assets/Characters/MatchHeroes/"+sex+"/"+sex+"_ReadyIdle.fbx").OfType<Mesh>().First(m=>m.name=="Body_"+(sex=="Female"?"F":"M"));
    var originals=original.vertices;
    foreach(bool golf in new[]{false,true}) {
     string path="Assets/Resources/Tennis/Customization/Player"+sex+(golf?"GolfKit":"")+".prefab";
     var instance=PrefabUtility.LoadPrefabContents(path);
     try {
      var r=instance.GetComponent<MatchHeroLook>().body;var current=r.sharedMesh;var v=current.vertices;
      lines.Add($"{sex} {(golf?"Golf":"Tennis")} original={original.name}:{original.vertexCount}/{original.triangles.Length/3} current={current.name}:{current.vertexCount}/{current.triangles.Length/3} submeshes={original.subMeshCount}/{current.subMeshCount}");
      if(v.Length!=originals.Length||original.subMeshCount!=current.subMeshCount){lines.Add("SKIP original index topology differs; source-based coverage patch required");continue;}
      float max=0;for(int i=0;i<v.Length;i++)max=Mathf.Max(max,(v[i]-originals[i]).magnitude);
      // The currently authored hands may differ. Only the exact original
      // lower torso/legs are used for the restoration contract.
      float lowerMax=0;for(int i=0;i<v.Length;i++)if(originals[i].z>.52f&&originals[i].z<1.16f)lowerMax=Mathf.Max(lowerMax,(v[i]-originals[i]).magnitude);
      lines.Add($"wholePositionMax={max:R} lowerPositionMax={lowerMax:R}");
      if(lowerMax>.00002f){lines.Add("SKIP lower body positions do not match original");continue;}
      var mesh=Object.Instantiate(current);int restored=0;
      for(int sub=0;sub<current.subMeshCount;sub++) {
       var keep=new List<int>(current.GetTriangles(sub));var known=new HashSet<(int,int,int)>();
       for(int i=0;i<keep.Count;i+=3)known.Add((keep[i],keep[i+1],keep[i+2]));
       var source=original.GetTriangles(sub);
       for(int i=0;i<source.Length;i+=3){int a=source[i],b=source[i+1],c=source[i+2];
        bool lower=new[]{a,b,c}.All(n=>originals[n].z>.52f&&originals[n].z<1.16f);
        if(!lower||known.Contains((a,b,c)))continue;
        keep.Add(a);keep.Add(b);keep.Add(c);restored++;
       }
       mesh.SetTriangles(keep,sub,false);
      }
      lines.Add("original lower triangles restored="+restored+"; geometry/UV/weights/rig unchanged");
      if(repair&&restored>0&&golf&&sex=="Female"){
       mesh.name=sex+(golf?" golf":" tennis")+" body (original lower coverage restored)";
       string output="Assets/Resources/"+(golf?"Golf/":"Tennis/")+"Covered_"+sex+"_Body.asset";
       var existing=AssetDatabase.LoadAssetAtPath<Mesh>(output);
       if(existing){EditorUtility.CopySerialized(mesh,existing);EditorUtility.SetDirty(existing);Object.DestroyImmediate(mesh);}else{AssetDatabase.CreateAsset(mesh,output);existing=mesh;}
       r.sharedMesh=existing;PrefabUtility.SaveAsPrefabAsset(instance,path);
      }else Object.DestroyImmediate(mesh);
     }finally{PrefabUtility.UnloadPrefabContents(instance);}
    }
   }
   if(repair)AssetDatabase.SaveAssets();
   string report=string.Join("\n",lines);Debug.Log(report);
   var outputPath=Environment.GetEnvironmentVariable("HERO_COVERAGE_REPORT");if(!string.IsNullOrEmpty(outputPath))File.WriteAllText(outputPath,report);
  }
 }
}
