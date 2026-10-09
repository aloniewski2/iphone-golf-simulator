using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using GolfArcade.Tennis;
using Object=UnityEngine.Object;
namespace GolfArcade.EditorTools {
public static class HeroV6PolishImport {
 public const string Dir="Assets/ArtDirection/HeroV6Polish", Model=Dir+"/Hero_V6_Polish.fbx", Prefab=Dir+"/Hero_V6_Polish.prefab";
 public static void Run(){
  AssetDatabase.Refresh();var imp=(ModelImporter)AssetImporter.GetAtPath(Model);imp.animationType=ModelImporterAnimationType.Generic;imp.importAnimation=false;imp.isReadable=true;imp.importBlendShapes=true;imp.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;imp.SaveAndReimport();
  var root=PrefabUtility.LoadPrefabContents("Assets/Resources/Tennis/Hero/Hero_01_Tennis.prefab");root.name="Hero_V6_Polish";
  var look=root.GetComponent<ModularHeroLook>();var driver=root.GetComponent<HeroTennisDriver>();var cosmetics=root.GetComponent<HeroCosmetics>();
  var original=root.GetComponentsInChildren<SkinnedMeshRenderer>(true).ToDictionary(r=>r.name,r=>r.sharedMaterials);
  var matLookup=root.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials).Where(m=>m).GroupBy(m=>m.name.Replace(" (Instance)","")).ToDictionary(g=>g.Key,g=>g.First());
  var model=AssetDatabase.LoadAssetAtPath<GameObject>(Model);var mats=new Dictionary<string,Material>();
  foreach(var r in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))foreach(var m in r.sharedMaterials){if(!m||mats.ContainsKey(m.name))continue;
   Material src=matLookup.TryGetValue(m.name,out var found)?found:null;
   if(!src&&original.TryGetValue(r.name,out var old)){int ix=Array.IndexOf(r.sharedMaterials,m);if(ix>=0&&ix<old.Length&&!m.name.StartsWith("Hero_V6"))src=old[ix];}
   var copy=src?new Material(src):new Material(Shader.Find("Universal Render Pipeline/Lit"));copy.name=m.name;
   if(!src){Color c=m.color;if(m.name.Contains("Hair")){ColorUtility.TryParseHtmlString("#DDB36D",out c);copy.name="Hero_01_HairTuft_V6_"+m.name;}else if(m.name.Contains("Scalp")){ColorUtility.TryParseHtmlString("#FFE0C2",out c);copy.name="Hero neck skin V6";}else if(m.name.Contains("Navy")){ColorUtility.TryParseHtmlString("#1E2A5A",out c);}else if(m.name.Contains("Orange")){ColorUtility.TryParseHtmlString("#FF6B3D",out c);}copy.SetColor("_BaseColor",c);copy.SetFloat("_Smoothness",.30f);}
   if(m.name.StartsWith("skin_BlondHair")){copy.SetTexture("_BaseMap",null);copy.SetColor("_BaseColor",new Color(1,.878f,.761f,1));copy.name="Hero neck skin V6 scalp";}
   if(m.name=="Hero_V6_FoundationSkin"){copy.SetTexture("_BaseMap",null);copy.SetColor("_BaseColor",new Color(1,.878f,.761f,1));copy.name="Hero neck skin V6 foundation";}
   copy.SetFloat("_Surface",0);copy.SetFloat("_AlphaClip",0);copy.SetFloat("_Cull",2);copy.SetFloat("_Metallic",0);copy.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");copy.DisableKeyword("_ALPHATEST_ON");copy.renderQueue=2000;
   string path=Dir+"/Hero_V6_"+m.name.Replace("/","_")+".mat";var existing=AssetDatabase.LoadAssetAtPath<Material>(path);if(existing){EditorUtility.CopySerialized(copy,existing);Object.DestroyImmediate(copy);copy=existing;}else AssetDatabase.CreateAsset(copy,path);mats[m.name]=copy;
  }
  Func<string,bool> body=n=>n.StartsWith("Body_");
  var slots=new[]{ModularHeroLook.Slot.Hair,ModularHeroLook.Slot.Hat,ModularHeroLook.Slot.Shirt,ModularHeroLook.Slot.Shorts,ModularHeroLook.Slot.Shoes};
  string[] prefixes={"Hair_","Hat_","Shirt_","Shorts_","Shoes_"};var wardrobe=new List<ModularHeroLook.WardrobeSlot>();
  for(int i=0;i<slots.Length;i++){
   var part=Object.Instantiate(model);part.name="Hero_V6_"+slots[i];foreach(var r in part.GetComponentsInChildren<SkinnedMeshRenderer>(true)){
    if(!r.name.StartsWith(prefixes[i])||r.name.Contains("_LOD")){Object.DestroyImmediate(r.gameObject);continue;}
    r.sharedMaterials=r.sharedMaterials.Select(m=>mats[m.name]).ToArray();r.updateWhenOffscreen=true;r.quality=SkinQuality.Bone4;
    if(i==0){var lod=r.gameObject.AddComponent<HeroV6PolishHairLOD>();lod.lod0=r.sharedMesh;var all=model.GetComponentsInChildren<SkinnedMeshRenderer>(true);lod.lod1=all.First(x=>x.name==r.name+"_LOD1").sharedMesh;lod.lod2=all.First(x=>x.name==r.name+"_LOD2").sharedMesh;}
   }
   if(i==0){var sample=part.GetComponentsInChildren<SkinnedMeshRenderer>(true).First();var bald=new GameObject("Hair_Bald").AddComponent<SkinnedMeshRenderer>();bald.transform.SetParent(sample.transform.parent,false);var empty=new Mesh{name="Hero_V6_EmptyHair"};string ep=Dir+"/Hero_V6_EmptyHair.asset";var existingMesh=AssetDatabase.LoadAssetAtPath<Mesh>(ep);if(!existingMesh){AssetDatabase.CreateAsset(empty,ep);existingMesh=empty;}bald.sharedMesh=existingMesh;bald.bones=new Transform[0];bald.rootBone=sample.rootBone;bald.sharedMaterial=mats.Values.First(x=>x.name.StartsWith("Hero neck skin"));bald.enabled=false;}
   var prefab=PrefabUtility.SaveAsPrefabAsset(part,Dir+"/Hero_V6_"+slots[i]+".prefab");wardrobe.Add(new ModularHeroLook.WardrobeSlot{slot=slots[i],asset=prefab});Object.DestroyImmediate(part);
  }
  look.defaults=wardrobe.ToArray();
  // Replace only skin/eyes on the duplicated original rig. Wardrobe rebuild retains existing slot enums.
  foreach(var r in root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r=>body(r.name)).ToArray())Object.DestroyImmediate(r.gameObject);
  var bodySlot=new GameObject("Hero_V6_Body").transform;bodySlot.SetParent(root.transform,false);
  foreach(var r in model.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r=>body(r.name))){var target=TennisCustomization.RebindCosmetic(r,model.transform,look.skeletonRoot,bodySlot);target.sharedMaterials=r.sharedMaterials.Select(m=>mats[m.name]).ToArray();}
  look.RebuildDefaultWardrobe();driver.closedHairV5=true;
  if(!root.GetComponent<HeroV6KneeCorrectives>())root.AddComponent<HeroV6KneeCorrectives>();
  foreach(var r in root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r=>r.name=="Hair_Default"||r.name=="Hair_Default_Free")){var lod=r.gameObject.AddComponent<HeroV6PolishHairLOD>();lod.lod0=r.sharedMesh;var all=model.GetComponentsInChildren<SkinnedMeshRenderer>(true);lod.lod1=all.First(x=>x.name==r.name+"_LOD1").sharedMesh;lod.lod2=all.First(x=>x.name==r.name+"_LOD2").sharedMesh;}
  if(cosmetics){cosmetics.visorAsset=wardrobe[1].asset;}
  // Contract aliases preserve existing canonical Slot_* bindings and the original racket grip transform.
  foreach(var pair in new[]{("Hair","Slot_Hair"),("Visor","Slot_Hat"),("Top","Slot_Shirt"),("Bottom","Slot_Shorts"),("Shoes","Slot_Shoes")}){
   var target=root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name==pair.Item2);if(target&&!target.Find(pair.Item1)){var anchor=new GameObject(pair.Item1).transform;anchor.SetParent(target,false);}
  }
  if(look.racketGrip&&!look.racketGrip.Find("Racket")){new GameObject("Racket").transform.SetParent(look.racketGrip,false);}
  var contract=root.GetComponent<HeroV6PolishContract>()??root.AddComponent<HeroV6PolishContract>();var sourceMeshes=model.GetComponentsInChildren<SkinnedMeshRenderer>(true).ToDictionary(x=>x.name,x=>x.sharedMesh);contract.default0=sourceMeshes["Hair_Default"];contract.default1=sourceMeshes["Hair_Default_LOD1"];contract.default2=sourceMeshes["Hair_Default_LOD2"];contract.free0=sourceMeshes["Hair_Default_Free"];contract.free1=sourceMeshes["Hair_Default_Free_LOD1"];contract.free2=sourceMeshes["Hair_Default_Free_LOD2"];
  PrefabUtility.SaveAsPrefabAsset(root,Prefab);var log=new List<string>{"Original Humanoid Avatar and complete HeroTennisDriver clip bindings retained. Imported mesh-only FBX is Generic; it is rebound to original Humanoid rig, never used as a Generic animation avatar.","Slots: Hair=Slot_Hair, Visor=Slot_Hat, Top=Slot_Shirt, Bottom=Slot_Shorts, Shoes=Slot_Shoes; racketGrip unchanged."};
  foreach(var r in root.GetComponentsInChildren<SkinnedMeshRenderer>(true)){if(r.sharedMesh.bindposes.Length!=r.bones.Length||r.bones.Any(b=>!b))throw new Exception("Invalid V6 binding "+r.name);log.Add(r.name+" tris="+r.sharedMesh.triangles.Length/3+" shapes="+r.sharedMesh.blendShapeCount);}
  Directory.CreateDirectory("../ArtDir/hero/v6_proof");File.WriteAllLines("../ArtDir/hero/v6_proof/unity-import.txt",log);PrefabUtility.UnloadPrefabContents(root);AssetDatabase.SaveAssets();Debug.Log("V6_POLISH_IMPORTED");
 }
}}
