using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using GolfArcade.Tennis;

namespace GolfArcade.EditorTools {
public static class HeroIntegrityAudit {
 const string Source="Assets/Resources/Tennis/Hero/Hero_01_Tennis.prefab";
 const string Destination="Assets/ArtDirection/HeroV5AstraTex";
 public static void ImportRepair() {
  string exports=Path.GetFullPath("../ArtDir/hero/integrity_v1/export");
  foreach(var file in Directory.GetFiles(exports,"*.fbx")) {
   string to=Destination+"/Hero_V5_AstraTex_"+Path.GetFileName(file);
   if(!File.Exists(to)) throw new Exception("Missing baseline mesh copy: "+to);
   File.Copy(file,to,true);AssetDatabase.ImportAsset(to,ImportAssetOptions.ForceSynchronousImport|ImportAssetOptions.ForceUpdate);
  }
  var root=PrefabUtility.LoadPrefabContents(Destination+"/Hero_V5_AstraTex_Baseline.prefab");var look=root.GetComponent<ModularHeroLook>();
  var bones=look.skeletonRoot.GetComponentsInChildren<Transform>(true).GroupBy(t=>t.name).ToDictionary(g=>g.Key,g=>g.First());
  foreach(var r in root.GetComponentsInChildren<SkinnedMeshRenderer>(true)) {
   string path=r.name.StartsWith("Hair_")?Destination+"/Hero_V5_AstraTex_Hero_01_Hair_Default.fbx":r.name=="Body_Skin"?Destination+"/Hero_V5_AstraTex_Hero_01_FingerBody.fbx":r.name=="Shirt_Default"?Destination+"/Hero_V5_AstraTex_Hero_01_Shirt_Default.fbx":null;
   if(string.IsNullOrEmpty(path))continue;
   var model=AssetDatabase.LoadAssetAtPath<GameObject>(path);var source=model.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(x=>x.name==r.name);
   if(!source)throw new Exception("Repaired mesh missing "+r.name);
   r.sharedMesh=source.sharedMesh;r.bones=source.bones.Select(b=>bones[b.name]).ToArray();r.rootBone=bones[source.rootBone.name];r.quality=SkinQuality.Bone4;r.updateWhenOffscreen=true;
  }
  var shortsModel=AssetDatabase.LoadAssetAtPath<GameObject>(Destination+"/Hero_V5_AstraTex_Hero_01_Shorts_Default.fbx");
  foreach(var r in root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r=>r.name=="Shorts_Default")) {
   var source=shortsModel.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(x=>x.name==r.name);r.sharedMesh=source.sharedMesh;r.bones=source.bones.Select(b=>bones[b.name]).ToArray();r.quality=SkinQuality.Bone4;
  }
  var bodyModel=AssetDatabase.LoadAssetAtPath<GameObject>(Destination+"/Hero_V5_AstraTex_Hero_01_FingerBody.fbx");
  var neckSource=bodyModel.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(x=>x.name=="Body_NeckSeal");
  var existing=root.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(x=>x.name=="Body_NeckSeal");if(existing)UnityEngine.Object.DestroyImmediate(existing.gameObject);
  var body=root.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(x=>x.name=="Body_Skin");
  var neck=TennisCustomization.RebindCosmetic(neckSource,bodyModel.transform,look.skeletonRoot,body.transform.parent);
  string materialPath=Destination+"/Hero_V5_AstraTex_Hero neck skin.mat";var skin=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
  if(!skin){skin=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(skin,materialPath);}skin.name="Hero neck skin";skin.SetColor("_BaseColor",look.skinTone);skin.SetFloat("_Smoothness",.30f);skin.SetFloat("_Surface",0);skin.SetFloat("_Cull",2);neck.sharedMaterial=skin;EditorUtility.SetDirty(skin);
  foreach(var r in root.GetComponentsInChildren<Renderer>(true))foreach(var mat in r.sharedMaterials)if(mat&&AssetDatabase.GetAssetPath(mat).StartsWith(Destination)){
   mat.name=mat.name.Replace("Hero_V5_AstraTex_","");EditorUtility.SetDirty(mat);
  }
  PrefabUtility.SaveAsPrefabAsset(root,Destination+"/Hero_V5_AstraTex_Baseline.prefab");PrefabUtility.UnloadPrefabContents(root);AssetDatabase.SaveAssets();
 }
 public static void PromoteIntegrity() {
  const string models="Assets/ArtDirection/Hero01/Models/";
  string exports=Path.GetFullPath("../ArtDir/hero/integrity_v1/export"),archive=Path.GetFullPath("../ArtDir/hero/integrity_v1/before/assets");Directory.CreateDirectory(archive);
  foreach(string name in new[]{"Hero_01_FingerBody.fbx","Hero_01_Hair_Default.fbx","Hero_01_Shirt_Default.fbx","Hero_01_Shorts_Default.fbx"}){
   string path=models+(name=="Hero_01_FingerBody.fbx"?"RestoreMotion/":"")+name;
   string backup=archive+"/"+name;if(!File.Exists(backup))File.Copy(path,backup);
   File.Copy(exports+"/"+name,path,true);AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate|ImportAssetOptions.ForceSynchronousImport);
  }
  foreach(string prefab in new[]{Source,"Assets/ArtDirection/Hero01/Prefabs/Hero_01_RestoreMotion.prefab"}){
   string backup=archive+"/"+Path.GetFileName(prefab);if(!File.Exists(backup))File.Copy(prefab,backup);
   var root=PrefabUtility.LoadPrefabContents(prefab);var look=root.GetComponent<ModularHeroLook>();
   var boneGroups=look.skeletonRoot.GetComponentsInChildren<Transform>(true).Where(t=>!t.GetComponent<Renderer>()).GroupBy(t=>t.name).ToDictionary(g=>g.Key,g=>g.ToArray());
   foreach(var r in root.GetComponentsInChildren<SkinnedMeshRenderer>(true)){
    string path=r.name.StartsWith("Hair_")?models+"Hero_01_Hair_Default.fbx":r.name=="Body_Skin"?models+"RestoreMotion/Hero_01_FingerBody.fbx":r.name=="Shirt_Default"?models+"Hero_01_Shirt_Default.fbx":r.name=="Shorts_Default"?models+"Hero_01_Shorts_Default.fbx":null;
    if(path==null)continue;var model=AssetDatabase.LoadAssetAtPath<GameObject>(path);var source=model.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(x=>x.name==r.name);
    var bones=source.bones.Select(b=>boneGroups.TryGetValue(b.name,out var matches)&&matches.Length==1?matches[0]:throw new Exception("Ambiguous/missing hero bone "+b.name)).ToArray();
    if(source.sharedMesh.bindposes.Length!=bones.Length)throw new Exception("Bind count mismatch "+r.name);
    r.sharedMesh=source.sharedMesh;r.bones=bones;r.rootBone=boneGroups[source.rootBone.name][0];r.quality=SkinQuality.Bone4;r.updateWhenOffscreen=true;r.localBounds=source.localBounds;
   }
   var bodyModel=AssetDatabase.LoadAssetAtPath<GameObject>(models+"RestoreMotion/Hero_01_FingerBody.fbx");var sourceNeck=bodyModel.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(x=>x.name=="Body_NeckSeal");
   var old=root.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(x=>x.name=="Body_NeckSeal");if(old)UnityEngine.Object.DestroyImmediate(old.gameObject);
   var body=root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(x=>x.name=="Body_Skin");var neck=TennisCustomization.RebindCosmetic(sourceNeck,bodyModel.transform,look.skeletonRoot,body.transform.parent);
   const string matPath="Assets/ArtDirection/Hero01/Materials/Hero_NeckSkin.mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(matPath);if(!mat){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,matPath);}mat.name="Hero neck skin";mat.SetColor("_BaseColor",look.skinTone);mat.SetFloat("_Smoothness",.3f);mat.SetFloat("_Cull",2);neck.sharedMaterial=mat;EditorUtility.SetDirty(mat);
   PrefabUtility.SaveAsPrefabAsset(root,prefab);PrefabUtility.UnloadPrefabContents(root);
  }
  AssetDatabase.SaveAssets();Debug.Log("HERO_INTEGRITY_PROMOTED: meshes and weights only; existing Avatar/clip/socket references retained");
 }
 public static void Run() {
  AssetDatabase.Refresh();Directory.CreateDirectory(Destination);AssetDatabase.Refresh();
  var root=PrefabUtility.LoadPrefabContents(Source);var log=new StringBuilder();
  var anim=root.GetComponentInChildren<Animator>(true);
  log.AppendLine("Source: "+Source);log.AppendLine($"Avatar: {AssetDatabase.GetAssetPath(anim.avatar)} valid={anim.avatar.isValid} human={anim.avatar.isHuman}");
  var paths=new Dictionary<string,string>();var meshes=new Dictionary<Mesh,Mesh>();var materials=new Dictionary<Material,Material>();
  foreach(var r in root.GetComponentsInChildren<SkinnedMeshRenderer>(true)) {
   var mesh=r.sharedMesh;if(!mesh)continue;string path=AssetDatabase.GetAssetPath(mesh);
   var model=AssetDatabase.LoadAssetAtPath<GameObject>(path);
   var src=model?model.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(x=>x.sharedMesh==mesh):null;
   int mismatches=0;int missing=r.bones.Count(x=>!x);float bindError=0;
   if(src) {
    for(int i=0;i<Math.Min(src.bones.Length,r.bones.Length);i++) if(!src.bones[i]||!r.bones[i]||src.bones[i].name!=r.bones[i].name)mismatches++;
    var bp=mesh.bindposes;
    for(int i=0;i<Math.Min(bp.Length,r.bones.Length);i++)if(r.bones[i]) {
     var skin=r.transform.worldToLocalMatrix*r.bones[i].localToWorldMatrix*bp[i];
     bindError=Mathf.Max(bindError,skin.MultiplyPoint3x4(Vector3.zero).magnitude);
    }
   }
   log.AppendLine($"{r.name}: mesh={mesh.name} path={path} vertices={mesh.vertexCount} bindposes={mesh.bindposes.Length} bones={r.bones.Length} missing={missing} source_order_mismatch={mismatches} rest_origin_error={bindError:F5}m quality={r.quality}");
   foreach(var m in r.sharedMaterials) if(m)log.AppendLine($"  material {m.name} shader={m.shader.name} surface={(m.HasProperty("_Surface")?m.GetFloat("_Surface"):-1)} queue={m.renderQueue} cull={(m.HasProperty("_Cull")?m.GetFloat("_Cull"):-1)}");
   if(!paths.ContainsKey(path)&&path.EndsWith(".fbx",StringComparison.OrdinalIgnoreCase)) {
    string to=Destination+"/Hero_V5_AstraTex_"+Path.GetFileName(path);if(!File.Exists(to))AssetDatabase.CopyAsset(path,to);paths[path]=to;
   }
   if(paths.TryGetValue(path,out var target)) {
    var copy=AssetDatabase.LoadAllAssetsAtPath(target).OfType<Mesh>().FirstOrDefault(x=>x.name==mesh.name);
    if(copy){meshes[mesh]=copy;r.sharedMesh=copy;}
   }
   var own=r.sharedMaterials;
   for(int i=0;i<own.Length;i++)if(own[i]) {
    var original=own[i];
    if(!materials.TryGetValue(original,out var copy)) {
     string to=Destination+"/Hero_V5_AstraTex_"+original.name+".mat";copy=AssetDatabase.LoadAssetAtPath<Material>(to);
     if(!copy){copy=new Material(original);AssetDatabase.CreateAsset(copy,to);}materials[original]=copy;
    }
    own[i]=copy;
   }
   r.sharedMaterials=own;
  }
  var look=root.GetComponent<ModularHeroLook>();
  log.AppendLine("Skeleton root: "+(look.skeletonRoot?look.skeletonRoot.name:"MISSING"));
  log.AppendLine("Socket racket: "+(look.racketGrip?look.racketGrip.name:"MISSING"));
  foreach(Transform t in root.GetComponentsInChildren<Transform>(true))if(t.name.StartsWith("Slot_")||new[]{"Hat","Hand_R","Hand_L","Back","FaceExtra"}.Contains(t.name))log.AppendLine("Attach: "+t.name+" parent="+(t.parent?t.parent.name:"root"));
  foreach(var slot in root.GetComponent<HeroTennisDriver>().slots)log.AppendLine($"Clip {slot.id}: {(slot.clip?AssetDatabase.GetAssetPath(slot.clip):"NULL")} human={(slot.clip&&slot.clip.humanMotion)} contact={slot.contact:F3}");
  // Patch serialized references to meshes/materials copied above, including grip meshes and wardrobe assets.
  foreach(var c in root.GetComponentsInChildren<Component>(true))if(c) {
   var so=new SerializedObject(c);var it=so.GetIterator();while(it.Next(true))if(it.propertyType==SerializedPropertyType.ObjectReference) {
    if(it.objectReferenceValue is Mesh m && meshes.TryGetValue(m,out var replacement))it.objectReferenceValue=replacement;
    else if(it.objectReferenceValue is Material material && materials.TryGetValue(material,out var own))it.objectReferenceValue=own;
    else if(it.objectReferenceValue is GameObject go && paths.TryGetValue(AssetDatabase.GetAssetPath(go),out var copyPath))it.objectReferenceValue=AssetDatabase.LoadAssetAtPath<GameObject>(copyPath);
   }so.ApplyModifiedPropertiesWithoutUndo();
  }
  PrefabUtility.SaveAsPrefabAsset(root,Destination+"/Hero_V5_AstraTex_Baseline.prefab");PrefabUtility.UnloadPrefabContents(root);AssetDatabase.SaveAssets();
  string outPath=Path.GetFullPath("../ArtDir/hero/v5_astra/baseline/unity-audit.txt");Directory.CreateDirectory(Path.GetDirectoryName(outPath));File.WriteAllText(outPath,log.ToString());
  Debug.Log(log.ToString());
 }
}
}
