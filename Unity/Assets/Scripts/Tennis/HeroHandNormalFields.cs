using System;using System.Linq;using System.Collections.Generic;using UnityEngine;
namespace GolfArcade.Tennis {
 /// Baked normal targets for the authored pose-space fist corrections. These are
 /// inverse-skinned geometry normals, not normals recalculated on warped bind geometry.
 public static class HeroHandNormalFields {
  [Serializable] sealed class Entry {public float[] p,n;public float strength;}
  [Serializable] sealed class Stage {public string shape;public Entry[] entries;}
  [Serializable] sealed class Fields {public string version;public Entry[] left,right;public Stage[] stages;}
  static readonly Dictionary<(Mesh,bool),Mesh> cache=new();
  public static Mesh Prepare(Mesh source,bool female){
   if(!source||source.name.Contains("(authored fist normals)"))return source;
   if(cache.TryGetValue((source,female),out var hit)&&hit)return hit;
   string sex=female?"Female":"Male";var asset=Resources.Load<TextAsset>("Tennis/Premium/HandNormals/"+sex);
   if(!asset)throw new InvalidOperationException("Authored hand normal fields missing "+sex);
   var data=JsonUtility.FromJson<Fields>(asset.text);if(data.version!=HeroThumbRigMigration.Version)throw new InvalidOperationException("Authored normal/rest version mismatch "+data.version);
   Vector3 Point(float[] a)=>new(a[0],a[1],a[2]);
   Vector3Int Key(Vector3 p)=>new(Mathf.RoundToInt(p.x*100000),Mathf.RoundToInt(p.y*100000),Mathf.RoundToInt(p.z*100000));
   Vector3 zero=HeroAuthoredHandPose.SourceToImportedBody(female,Vector3.zero);
   Dictionary<Vector3Int,List<(Vector3 p,Vector3 n,float strength)>> Lookup(Entry[] entries){
    var map=new Dictionary<Vector3Int,List<(Vector3 p,Vector3 n,float strength)>>();
    foreach(var e in entries){var p=HeroAuthoredHandPose.SourceToImportedBody(female,Point(e.p));var n=(HeroAuthoredHandPose.SourceToImportedBody(female,Point(e.n))-zero).normalized;var key=Key(p);if(!map.TryGetValue(key,out var list))map[key]=list=new();list.Add((p,n,e.strength));}return map;
   }
   var fields=new Dictionary<string,Dictionary<Vector3Int,List<(Vector3 p,Vector3 n,float strength)>>>{
    ["Hero_Fist_Left"]=Lookup(data.left),["Hero_Fist_Right"]=Lookup(data.right)
   };
   foreach(var stage in data.stages??Array.Empty<Stage>()){
    if(string.IsNullOrEmpty(stage.shape)||stage.entries==null||fields.ContainsKey(stage.shape))throw new InvalidOperationException("Invalid authored hand normal stage "+sex);
    fields[stage.shape]=Lookup(stage.entries);
   }
   var positions=source.vertices;var normals=source.normals;var result=UnityEngine.Object.Instantiate(source);result.hideFlags=HideFlags.DontSave;result.name=source.name+" (authored fist normals)";result.ClearBlendShapes();var counts=new Dictionary<string,int>();
   for(int shape=0;shape<source.blendShapeCount;shape++){
    string name=source.GetBlendShapeName(shape);var field=fields.Keys.FirstOrDefault(k=>name.EndsWith(k,StringComparison.Ordinal));
    bool authored=field!=null;
    if(!authored&&name.Contains("Hero_Fist_"))throw new InvalidOperationException("Missing authored hand normal stage "+sex+" "+name);
    var map=authored?fields[field]:null;int matched=0;
    for(int frame=0;frame<source.GetBlendShapeFrameCount(shape);frame++){
     var dp=new Vector3[positions.Length];var dn=new Vector3[positions.Length];var dt=new Vector3[positions.Length];source.GetBlendShapeFrameVertices(shape,frame,dp,dn,dt);
     if(authored){
      // Outside the authored local hand region these corrective normals are zero,
      // preserving the body's established skin/face normal field exactly.
      Array.Clear(dn,0,dn.Length);
      for(int i=0;i<positions.Length;i++){
       var key=Key(positions[i]);float best=.000005f*.000005f;bool found=false;Vector3 wanted=Vector3.zero;float strength=0;
       for(int x=-1;x<=1;x++)for(int y=-1;y<=1;y++)for(int z=-1;z<=1;z++)if(map.TryGetValue(key+new Vector3Int(x,y,z),out var list))foreach(var e in list){float error=(positions[i]-e.p).sqrMagnitude;if(error>best)continue;best=error;wanted=e.n;strength=e.strength;found=true;}
       if(!found)continue;dn[i]=(wanted-normals[i])*strength;matched++;
      }
     }
     result.AddBlendShapeFrame(name,source.GetBlendShapeFrameWeight(shape,frame),dp,dn,dt);
    }
    if(authored){counts[field]=matched;if(matched<1000)throw new InvalidOperationException("Authored normal/import stage mapping incomplete "+sex+" "+field+" "+matched);}
   }
   if(!counts.ContainsKey("Hero_Fist_Left")||!counts.ContainsKey("Hero_Fist_Right"))throw new InvalidOperationException("Authored full fist normal targets missing "+sex);
   cache[(source,female)]=result;Debug.Log("[HeroHandNormalFields] "+sex+" stage mappings="+string.Join(", ",counts.Select(k=>k.Key+":"+k.Value))+"; positions, weights, binds and outside-hand normals retained");return result;
  }
 }
}
