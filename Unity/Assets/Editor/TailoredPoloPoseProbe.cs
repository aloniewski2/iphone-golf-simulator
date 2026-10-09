using System;
using System.IO;
using System.Linq;
using UnityEngine;
using GolfArcade.Tennis;
namespace GolfArcade.EditorTools {
 internal static class TailoredPoloPoseProbe {
  static HeroGarmentPoseCorrectives.Profile profile;
  public static void Apply(MatchHeroLook hero,string pose){
   var path=Environment.GetEnvironmentVariable("VISUAL_CHARACTER_TEST_PROFILE");if(string.IsNullOrEmpty(path))return;
   profile??=JsonUtility.FromJson<HeroGarmentPoseCorrectives.Profile>(File.ReadAllText(path));
   var top=hero.kit.First(r=>r.name=="Kit_Top");var mesh=top.sharedMesh;
   if(mesh.vertexCount!=profile.vertexCount)throw new InvalidOperationException("Polo probe basis count");
   for(int a=0;a<profile.anchors.Length;a++){
    var anchor=profile.anchors[a];string name="Probe_"+anchor.shape;int index=mesh.GetBlendShapeIndex(name);
    if(index<0){
     var p=mesh.vertices;var dv=new Vector3[p.Length];var dn=new Vector3[p.Length];
     for(int i=0;i<p.Length;i++){
      var basis=new Vector3(profile.vertices[i*3],profile.vertices[i*3+1],profile.vertices[i*3+2]);
      if((basis-p[i]).sqrMagnitude>1e-10f)throw new InvalidOperationException("Polo probe basis mismatch");
      dv[i]=new Vector3(anchor.deltas[i*3],anchor.deltas[i*3+1],anchor.deltas[i*3+2]);
      dn[i]=new Vector3(anchor.normalDeltas[i*3],anchor.normalDeltas[i*3+1],anchor.normalDeltas[i*3+2]);
     }
     mesh.AddBlendShapeFrame(name,100,dv,dn,null);index=mesh.GetBlendShapeIndex(name);
    }
    top.SetBlendShapeWeight(index,anchor.shape==pose?100:0);
   }
  }
 }
}
