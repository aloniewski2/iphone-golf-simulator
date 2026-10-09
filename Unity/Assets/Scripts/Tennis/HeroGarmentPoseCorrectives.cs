using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace GolfArcade.Tennis {
 /// Authored sleeve-envelope shapes, driven by invariant segment directions.
 /// Uses the existing rig and static blendshapes; no runtime cloth simulation.
 [DefaultExecutionOrder(1200), DisallowMultipleComponent]
 public sealed class HeroGarmentPoseCorrectives : MonoBehaviour {
  [Serializable] public sealed class Anchor { public string shape;public float[] feature,deltas,normalDeltas; }
  [Serializable] public sealed class Profile { public int vertexCount;public float[] vertices,normals;public Anchor[] anchors; }
  const string Prefix="TailoredSleeve_";
  static readonly string[,] Segments={{"Spine","Chest"},{"Chest","Neck"},{"LeftShoulder","LeftUpperArm"},{"LeftUpperArm","LeftLowerArm"},{"LeftLowerArm","LeftHand"},{"RightShoulder","RightUpperArm"},{"RightUpperArm","RightLowerArm"},{"RightLowerArm","RightHand"}};
  static readonly Dictionary<bool,Profile> profiles=new();
  static readonly Dictionary<(Mesh,bool),Mesh> prepared=new();
  public MatchHeroLook look;
  Profile profile;SkinnedMeshRenderer top;Mesh previous;Transform[,] joints;int[] indices;float[] weights;
  readonly Vector3[] directions=new Vector3[8];readonly float[] features=new float[28];
  static Profile Load(bool female){
   if(profiles.TryGetValue(female,out var p))return p;
   var text=Resources.Load<TextAsset>("Tennis/Correctives/SleeveFit_"+(female?"Female":"Male"));if(!text)return null;
   p=JsonUtility.FromJson<Profile>(text.text);if(p?.anchors==null||p.vertexCount<=0)return null;profiles[female]=p;return p;
  }
  public static Mesh Prepare(MatchHeroLook hero,SkinnedMeshRenderer r,Mesh src){
   if(!hero||hero.golfKit||!r||r.name!="Kit_Top"||!src||!src.isReadable)return src;
   if(src.name.Contains("(sleeve envelope anchors)"))return src;
   var p=Load(hero.female);if(p==null)return src;
   if(!hero.GetComponent<HeroGarmentPoseCorrectives>())hero.gameObject.AddComponent<HeroGarmentPoseCorrectives>().look=hero;
   if(prepared.TryGetValue((src,hero.female),out var found))return found;
   var positions=src.vertices;var normals=src.normals;int[] transfer=new int[positions.Length];
   if(positions.Length==p.vertexCount){
    float basisError=0;for(int i=0;i<transfer.Length;i++){transfer[i]=i;var source=new Vector3(p.vertices[i*3],p.vertices[i*3+1],p.vertices[i*3+2]);basisError=Mathf.Max(basisError,(source-positions[i]).magnitude);}
    if(basisError>.00005f)throw new InvalidOperationException("Sleeve envelope indexed source mismatch: "+basisError+"m; counts alone cannot certify transfer");
    Debug.Log("[HeroGarmentPoseCorrectives] indexed bind basis exact within "+basisError+"m");
   }
   else {
    // Bounded distance mesh preserves the same garment shell. Transfer only
    // from the nearest aligned source surface, protecting its opposed lining.
    var source=new Vector3[p.vertexCount];var ns=new Vector3[p.vertexCount];
    var grid=new Dictionary<Vector3Int,List<int>>();const float cell=.006f;
    Vector3Int Cell(Vector3 v)=>new(Mathf.FloorToInt(v.x/cell),Mathf.FloorToInt(v.y/cell),Mathf.FloorToInt(v.z/cell));
    for(int i=0;i<source.Length;i++){source[i]=new Vector3(p.vertices[i*3],p.vertices[i*3+1],p.vertices[i*3+2]);ns[i]=new Vector3(p.normals[i*3],p.normals[i*3+1],p.normals[i*3+2]);var key=Cell(source[i]);if(!grid.TryGetValue(key,out var list))grid[key]=list=new List<int>();list.Add(i);}
    int misses=0;float maximum=0;
    for(int i=0;i<positions.Length;i++){
     var key=Cell(positions[i]);float best=float.MaxValue;int nearest=-1;
     for(int x=-2;x<=2;x++)for(int y=-2;y<=2;y++)for(int z=-2;z<=2;z++)if(grid.TryGetValue(key+new Vector3Int(x,y,z),out var list))foreach(int j in list){if(normals.Length==positions.Length&&Vector3.Dot(normals[i],ns[j])<.3f)continue;float d=(positions[i]-source[j]).sqrMagnitude;if(d<best){best=d;nearest=j;}}
     transfer[i]=nearest;if(nearest<0)misses++;else maximum=Mathf.Max(maximum,Mathf.Sqrt(best));
    }
    Debug.Log($"[HeroGarmentPoseCorrectives] distance transfer v={positions.Length} misses={misses} max={maximum:R}m; unmatched vertices exact");
   }
   var result=UnityEngine.Object.Instantiate(src);result.name=src.name+" (sleeve envelope anchors)";result.hideFlags=HideFlags.DontSave;
   if(positions.Length==p.vertexCount&&p.normals?.Length==p.vertexCount*3){
    // The shape normal deltas were authored against this exact fitted source
    // normal basis; preserve that basis after geometric sleeve shortening.
    var basisNormals=new Vector3[p.vertexCount];for(int i=0;i<basisNormals.Length;i++)basisNormals[i]=new Vector3(p.normals[i*3],p.normals[i*3+1],p.normals[i*3+2]);result.normals=basisNormals;
   }
   foreach(var a in p.anchors){
    if(a.deltas?.Length!=p.vertexCount*3||a.normalDeltas?.Length!=p.vertexCount*3||a.feature?.Length!=28)throw new InvalidOperationException("Invalid sleeve envelope anchor: "+a.shape);
    var dv=new Vector3[positions.Length];var dn=new Vector3[positions.Length];
    for(int i=0;i<dv.Length;i++){int j=transfer[i];if(j<0)continue;dv[i]=new Vector3(a.deltas[j*3],a.deltas[j*3+1],a.deltas[j*3+2]);dn[i]=new Vector3(a.normalDeltas[j*3],a.normalDeltas[j*3+1],a.normalDeltas[j*3+2]);}
    result.AddBlendShapeFrame(Prefix+a.shape,100,dv,dn,null);
   }
   HeroGarmentLocalFit.Prepare(hero.female,result,p,transfer);
   prepared[(src,hero.female)]=result;Debug.Log($"[HeroGarmentPoseCorrectives] {(hero.female?"Female":"Male")} Top v={src.vertexCount}, {p.anchors.Length} garment-only anchors; rest geometry/UVs/weights/rig exact");return result;
  }
  bool Init(){
   if(!look)look=GetComponent<MatchHeroLook>();if(!look||look.golfKit)return false;
   profile??=Load(look.female);if(profile==null)return false;
   top??=look.kit?.FirstOrDefault(r=>r&&r.name=="Kit_Top");if(!top||!top.sharedMesh)return false;
   if(joints==null){var all=look.GetComponentsInChildren<Transform>(true);joints=new Transform[8,2];for(int i=0;i<8;i++)for(int j=0;j<2;j++){joints[i,j]=all.FirstOrDefault(t=>t.name==Segments[i,j]);if(!joints[i,j])return false;}weights=new float[profile.anchors.Length];}
   if(previous!=top.sharedMesh){previous=top.sharedMesh;indices=profile.anchors.Select(a=>previous.GetBlendShapeIndex(Prefix+a.shape)).ToArray();}
   return true;
  }
  public void Apply(){
   if(!Init())return;
   for(int i=0;i<8;i++)directions[i]=(joints[i,1].position-joints[i,0].position).normalized;
   int f=0;for(int i=0;i<8;i++)for(int j=i+1;j<8;j++)features[f++]=Vector3.Dot(directions[i],directions[j]);
   HeroGarmentLocalFit.Apply(look.female,top,features);
   double sum=0,nearest=double.MaxValue;
   for(int a=0;a<weights.Length;a++){double d=0;for(int k=0;k<features.Length;k++){double e=features[k]-profile.anchors[a].feature[k];d+=e*e;}d/=features.Length;nearest=Math.Min(nearest,d);weights[a]=(float)(1/Math.Pow(Math.Max(d,1e-8),2));sum+=weights[a];}
   float confidence=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.025f,.30f,(float)nearest));
   bool applyEnvelope=true;
#if UNITY_EDITOR
   // Isolate the authored envelope from a proposed deformation repair in actual pose captures.
   applyEnvelope=Environment.GetEnvironmentVariable("VISUAL_SLEEVE_ANCHORS")!="0";
#endif
   for(int a=0;a<weights.Length;a++)if(indices[a]>=0)top.SetBlendShapeWeight(indices[a],applyEnvelope?(float)(100*confidence*weights[a]/sum):0);
   HeroGarmentGusset.Apply(look.female,look.transform,top);
  }
  void LateUpdate(){
   if(!look)look=GetComponent<MatchHeroLook>();
   var driver=look?look.GetComponent<HeroTennisDriver>():null;
   // The live driver applies once after its graph and contact/foot adjustments.
   // Disabled drivers and replay/manual samplers retain the standalone path.
   if(driver&&driver.isActiveAndEnabled&&driver.actor&&!(driver.game&&driver.game.ReplayPlaying))return;
   Apply();
  }
 }
}
