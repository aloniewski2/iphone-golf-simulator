using System;using System.Linq;using UnityEngine;
namespace GolfArcade.Tennis {
 /// Authored complete local hand surface, with the original outside-hand Body and
 /// wrist boundary retained. Imported original rest is validated before migration.
 public static class HeroContinuousHandMesh {
  public static void Apply(MatchHeroLook hero){
   if(!hero||!hero.body||hero.body.sharedMesh.name.Contains("(continuous hands)"))return;
   var model=Resources.Load<GameObject>("Tennis/Premium/Hands/"+(hero.female?"Female":"Male"));if(!model)throw new InvalidOperationException("Authored continuous hand source missing");
   var source=model.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(r=>r.name.StartsWith("Body"));
   var live=hero.body;var expected=live.bones.Select((b,i)=>new{b.name,bind=live.sharedMesh.bindposes[i]}).ToDictionary(b=>b.name,b=>b.bind);float error=0;
   for(int b=0;b<source.bones.Length;b++){if(!expected.TryGetValue(source.bones[b].name,out var matrix))throw new InvalidOperationException("Continuous hand unknown bind "+source.bones[b].name);for(int k=0;k<16;k++)error=Mathf.Max(error,Mathf.Abs(matrix[k]-source.sharedMesh.bindposes[b][k]));}
   if(error>.0002f)throw new InvalidOperationException("Continuous hand original rest bind drift "+error);
   var byName=hero.GetComponentsInChildren<Transform>(true).GroupBy(b=>b.name).ToDictionary(g=>g.Key,g=>g.First());
   var copy=UnityEngine.Object.Instantiate(source.sharedMesh);copy.hideFlags=HideFlags.DontSave;copy.name=source.sharedMesh.name+" (continuous hands)";live.sharedMesh=copy;live.bones=source.bones.Select(b=>byName[b.name]).ToArray();live.rootBone=byName[source.rootBone.name];
   Debug.Log("[HeroContinuousHandMesh] "+(hero.female?"Female":"Male")+" old-rest bind error="+error+" vertices="+copy.vertexCount+"; deliberate local hand geometry change, outside wrists retained");
  }
 }
}
