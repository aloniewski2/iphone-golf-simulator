using System.IO;using System.Linq;using System.Text;using UnityEditor;using UnityEngine;
namespace GolfArcade.EditorTools {
 public static class HeroReferenceImportAudit {
  public static void Run(){
   var s=new StringBuilder();
   foreach(string path in new[]{"Tennis/Customization/PlayerMale","Tennis/Customization/PlayerFemale","Tennis/HeroReference/Male","Tennis/HeroReference/Female"}){
    var go=Resources.Load<GameObject>(path);s.AppendLine(path+" exists="+(bool)go);if(!go)continue;
    foreach(var r in go.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r=>r.name.StartsWith("Body")||r.name=="ReferenceFeatures")){
     var m=r.sharedMesh;s.AppendLine(r.name+" bounds="+m.bounds+" local="+r.transform.localToWorldMatrix.ToString("F4")+" verts="+m.vertexCount+" weights="+m.boneWeights.Length+" bones="+r.bones.Length);
     for(int i=0;i<r.bones.Length;i++)if(r.bones[i]&&new[]{"Head","Neck","Hips","LeftHand"}.Contains(r.bones[i].name))s.AppendLine("bone "+i+" "+r.bones[i].name+" world="+r.bones[i].localToWorldMatrix.ToString("F4")+" bind="+m.bindposes[i].ToString("F4"));
    }
   }
   File.WriteAllText("../work/reference-rebuild/import-audit.txt",s.ToString());Debug.Log("REFERENCE_IMPORT_AUDIT_DONE");EditorApplication.Exit(0);
  }
 }
}
