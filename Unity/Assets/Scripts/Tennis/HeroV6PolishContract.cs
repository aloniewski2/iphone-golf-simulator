using System.Linq;
using System.Collections.Generic;
using UnityEngine;
namespace GolfArcade.Tennis {
 /// Additive adapter only on the isolated V6 prefab. Existing rig/animation driver is retained.
 public sealed class HeroV6PolishContract:MonoBehaviour {
  public Mesh default0,default1,default2,free0,free1,free2;
  HeroCosmetics cosmetics;
  ModularHeroLook look;
  readonly Dictionary<Material,Material> kitCopies=new Dictionary<Material,Material>();
  readonly HashSet<Material> ownedKit=new HashSet<Material>();
  RenderTexture kitAtlas;
  int cachedHeadwear=-1;bool cachedShortHair;
  public int OwnedKitMaterialCount=>ownedKit.Count;
  public RenderTexture KitAtlas=>kitAtlas;
  public Material OwnKitMaterial(Material source){
   if(!source||ownedKit.Contains(source))return source;
   if(kitCopies.TryGetValue(source,out var existing))return existing;
   var copy=new Material(source){name=source.name.Replace(" (Instance)","")+" (V6 kit)"};
   kitCopies[source]=copy;ownedKit.Add(copy);return copy;
  }
  public RenderTexture AcquireKitAtlas(Texture source){
   if(kitAtlas&&kitAtlas.width==source.width&&kitAtlas.height==source.height)return kitAtlas;
   if(kitAtlas){kitAtlas.Release();Destroy(kitAtlas);}
   kitAtlas=new RenderTexture(source.width,source.height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB){name="Hero V6 kit atlas"};return kitAtlas;
  }
  public bool ReuseHeadwear(int desired,bool shortHair)=>cachedHeadwear==desired&&cachedShortHair==shortHair;
  public void RecordHeadwear(int desired,bool shortHair){cachedHeadwear=desired;cachedShortHair=shortHair;}

  void Start(){cosmetics=GetComponent<HeroCosmetics>();look=GetComponent<ModularHeroLook>();if(cosmetics)cosmetics.Changed+=RefreshSlots;if(look)look.WardrobeChanged+=OnWardrobeChanged;RefreshSlots();}
  void OnDestroy(){if(cosmetics)cosmetics.Changed-=RefreshSlots;if(look)look.WardrobeChanged-=OnWardrobeChanged;foreach(var m in ownedKit)if(m)Destroy(m);if(kitAtlas){kitAtlas.Release();Destroy(kitAtlas);}}
  void OnWardrobeChanged(ModularHeroLook.Slot slot){if(slot==ModularHeroLook.Slot.Hat)cachedHeadwear=-1;RefreshSlots();}
  public void RefreshSlots(){
   // The authored V6 head is closed. The legacy star-hull filler now intersects the repaired neck.
   var oldCore=GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="Head occluder (inner shell)");
   if(oldCore)oldCore.gameObject.SetActive(false);
   foreach(var r in GetComponentsInChildren<SkinnedMeshRenderer>(true)){
    if(r.name!="Hair_Default"&&r.name!="Hair_Default_Free")continue;
    var l=r.GetComponent<HeroV6PolishHairLOD>()??r.gameObject.AddComponent<HeroV6PolishHairLOD>();bool free=r.name.EndsWith("_Free");l.lod0=free?free0:default0;l.lod1=free?free1:default1;l.lod2=free?free2:default2;
   }
   foreach(var p in new[]{("Hair","Slot_Hair"),("Visor","Slot_Hat"),("Top","Slot_Shirt"),("Bottom","Slot_Shorts"),("Shoes","Slot_Shoes")}){
    var t=GetComponentsInChildren<Transform>(true).FirstOrDefault(x=>x.name==p.Item2&&x.gameObject.activeInHierarchy);if(t&&!t.Find(p.Item1))new GameObject(p.Item1).transform.SetParent(t,false);
   }
  }
 }
}
