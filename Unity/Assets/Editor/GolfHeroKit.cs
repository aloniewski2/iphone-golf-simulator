using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using GolfArcade.Tennis;
using UnityEditor;
using UnityEngine;
namespace GolfArcade.EditorTools {
 public static class GolfHeroKit {
  public const string KitRoot="Assets/Characters/GolfHeroKit/";
  public static readonly string[] Pieces={"Kit_Top","Kit_Bottom","Kit_Head","Kit_Glove_L","Kit_Glove_R","Kit_Shoe_L","Kit_Shoe_R"};
  public static void Build() {
   try {
    Directory.CreateDirectory(KitRoot);
    string source=Environment.GetEnvironmentVariable("GOLF_KIT_SOURCE"); if(string.IsNullOrEmpty(source))throw new ArgumentException("GOLF_KIT_SOURCE required");
    foreach(string sex in new[]{"Male","Female"})File.Copy(Path.Combine(source,"Golf_"+sex+"_Kit.fbx"),KitRoot+sex+"_Kit.fbx",true);
    AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
    string matDir=KitRoot+"Materials/";Directory.CreateDirectory(matDir);
    var mats=new Dictionary<string,Material>();
    foreach(string role in new[]{MatchHeroLook.RoleShirt,MatchHeroLook.RoleShorts,MatchHeroLook.RoleShoe,MatchHeroLook.RoleGolfHead,MatchHeroLook.RoleGolfGlove,MatchHeroLook.RoleGolfHardware}) {
     var mat=AssetDatabase.LoadAssetAtPath<Material>(matDir+role+".mat");if(!mat){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,matDir+role+".mat");}
     mat.name=role;Color col=role==MatchHeroLook.RoleShorts?new Color(.065f,.069f,.077f):role==MatchHeroLook.RoleGolfHead?new Color(.035f,.035f,.0401f):role==MatchHeroLook.RoleGolfHardware?new Color(.72f,.73f,.74f):new Color(.93f,.93f,.93f);
     mat.SetColor("_BaseColor",col);mat.SetFloat("_Smoothness",.30f);mat.SetFloat("_Metallic",0);EditorUtility.SetDirty(mat);mats[role]=mat;
    }
    foreach(string sex in new[]{"Male","Female"}) {
     var root=PrefabUtility.LoadPrefabContents("Assets/Resources/Tennis/Customization/Player"+sex+".prefab");
     try {
      var look=root.GetComponent<MatchHeroLook>();look.golfKit=true;
      foreach(var r in root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r=>r.name.StartsWith("Kit_")).ToArray())UnityEngine.Object.DestroyImmediate(r.gameObject);
      var model=AssetDatabase.LoadAssetAtPath<GameObject>(KitRoot+sex+"_Kit.fbx");
      Debug.Log(MatchHeroKit.AttachVariant(root,sex,look,look.body,KitRoot+sex+"_Kit.fbx",Pieces,mats));
      if(sex=="Female") {
       var sourceHair=model.GetComponentsInChildren<MeshRenderer>(true).Single(r=>r.name=="Hair_F");
       var sourceHead=model.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Head");
       var head=root.transform.Find("Rig_"+sex).GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Head");
       var local=sourceHead.worldToLocalMatrix*sourceHair.transform.localToWorldMatrix;
       var hairNode=new GameObject("Hair_F");hairNode.transform.SetParent(head,false);
       hairNode.transform.localPosition=local.GetColumn(3);hairNode.transform.localRotation=local.rotation;hairNode.transform.localScale=local.lossyScale;
       hairNode.AddComponent<MeshFilter>().sharedMesh=sourceHair.GetComponent<MeshFilter>().sharedMesh;
       var hair=hairNode.AddComponent<MeshRenderer>();hair.sharedMaterials=sourceHair.sharedMaterials;
       hair.shadowCastingMode=look.body.shadowCastingMode;hair.receiveShadows=look.body.receiveShadows;
       Debug.Log("GOLF_HAIR rigid existing Head attachment: "+hair.GetComponent<MeshFilter>().sharedMesh.vertexCount+" vertices, unchanged source mesh");
      }
      Debug.Log("GOLF_CORRECTIVES "+sex+" shapes="+look.kit.Single(r=>r.name=="Kit_Top").sharedMesh.blendShapeCount);
      look.SetGolfHand(false);
      var corrective=root.GetComponent<GolfGarmentCorrectives>() ?? root.AddComponent<GolfGarmentCorrectives>();corrective.look=look;
      PrefabUtility.SaveAsPrefabAsset(root,"Assets/Resources/Tennis/Customization/Player"+sex+"GolfKit.prefab");
     }finally{PrefabUtility.UnloadPrefabContents(root);}
    }
    AssetDatabase.SaveAssets();if(Application.isBatchMode)EditorApplication.Exit(0);
   }catch(Exception e){Debug.LogException(e);if(Application.isBatchMode)EditorApplication.Exit(1);else throw;}
  }
 }
}
