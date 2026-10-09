using System;using System.Linq;using System.Collections.Generic;using UnityEngine;
namespace GolfArcade.Tennis {
 /// A narrow rounded skin rim on the actual Body aperture. No eye/iris anchors or
 /// head silhouette move; the rim follows closure and disappears before full blink.
 public static class HeroEyeRim {
  public static Mesh Append(MatchHeroLook hero,Mesh face){
   var body=hero.body.sharedMesh;int hi=Array.IndexOf(hero.body.bones,hero.Bone(HumanBodyBones.Head));if(hi<0)return null;
   HeroFaceBasis.Get(hero,out var right,out var up,out var forward);var map=hero.face.transform.worldToLocalMatrix*hero.body.bones[hi].localToWorldMatrix*body.bindposes[hi];var p=body.vertices;var n=body.normals;var bw=body.boneWeights;var maps=hero.body.bones.Select((bone,i)=>hero.face.transform.worldToLocalMatrix*bone.localToWorldMatrix*body.bindposes[i]).ToArray();var normalMaps=maps.Select(m=>m.inverse.transpose).ToArray();
   Vector3Int Key(Vector3 v)=>new(Mathf.RoundToInt(v.x*100000),Mathf.RoundToInt(v.y*100000),Mathf.RoundToInt(v.z*100000));var unique=new List<Vector3>();var normals=new List<Vector3>();var ids=new int[p.Length];var keys=new Dictionary<Vector3Int,int>();
   for(int i=0;i<p.Length;i++){var w=bw[i];var point=maps[w.boneIndex0].MultiplyPoint3x4(p[i])*w.weight0+maps[w.boneIndex1].MultiplyPoint3x4(p[i])*w.weight1+maps[w.boneIndex2].MultiplyPoint3x4(p[i])*w.weight2+maps[w.boneIndex3].MultiplyPoint3x4(p[i])*w.weight3;var normal=(normalMaps[w.boneIndex0].MultiplyVector(n[i])*w.weight0+normalMaps[w.boneIndex1].MultiplyVector(n[i])*w.weight1+normalMaps[w.boneIndex2].MultiplyVector(n[i])*w.weight2+normalMaps[w.boneIndex3].MultiplyVector(n[i])*w.weight3).normalized;var key=Key(point);if(!keys.TryGetValue(key,out int id)){id=unique.Count;keys[key]=id;unique.Add(point);normals.Add(normal);}ids[i]=id;}
   var edges=new Dictionary<(int,int),int>();var source=body.triangles;for(int t=0;t<source.Length;t+=3){for(int e=0;e<3;e++){int a=ids[source[t+e]],b=ids[source[t+(e+1)%3]];if(a==b)continue;var key=a<b?(a,b):(b,a);edges[key]=edges.GetValueOrDefault(key)+1;}}
   var adjacency=new Dictionary<int,List<int>>();foreach(var edge in edges.Where(e=>e.Value==1).Select(e=>e.Key)){if(!adjacency.ContainsKey(edge.Item1))adjacency[edge.Item1]=new();if(!adjacency.ContainsKey(edge.Item2))adjacency[edge.Item2]=new();adjacency[edge.Item1].Add(edge.Item2);adjacency[edge.Item2].Add(edge.Item1);}
   var remaining=new HashSet<int>(adjacency.Keys);var loops=new List<List<int>>();while(remaining.Count>0){var todo=new Stack<int>();todo.Push(remaining.First());var loop=new List<int>();while(todo.Count>0){int id=todo.Pop();if(!remaining.Remove(id))continue;loop.Add(id);foreach(int other in adjacency[id])todo.Push(other);}if(loop.Count>20)loops.Add(loop);}
   var eyePoints=new List<Vector3>();var fm=hero.face.sharedMaterials;for(int s=0;s<Mathf.Min(face.subMeshCount,fm.Length);s++)if(fm[s]&&fm[s].name.Contains("Sclera"))eyePoints.AddRange(face.GetTriangles(s).Select(i=>face.vertices[i]));if(eyePoints.Count<6)return null;float mid=eyePoints.Average(v=>Vector3.Dot(v,right));
   var chosen=new List<List<int>>();foreach(bool left in new[]{true,false}){var eyes=eyePoints.Where(v=>(Vector3.Dot(v,right)<mid)==left).ToArray();var centre=eyes.Aggregate(Vector3.zero,(a,v)=>a+v)/eyes.Length;var nearest=loops.Where(l=>!chosen.Contains(l)).OrderBy(l=>(l.Aggregate(Vector3.zero,(a,i)=>a+unique[i])/l.Count-centre).sqrMagnitude).FirstOrDefault();if(nearest==null)return null;float error=Vector3.Distance(nearest.Aggregate(Vector3.zero,(a,i)=>a+unique[i])/nearest.Count,centre);if(error>.035f)throw new InvalidOperationException("Skin eye rim aperture registration "+error);chosen.Add(nearest);}
   var projection=new HeroFaceProjection(hero);var eyeTriangles=new List<int>();for(int sub=0;sub<Mathf.Min(face.subMeshCount,fm.Length);sub++)if(fm[sub]&&fm[sub].name.Contains("Sclera"))eyeTriangles.AddRange(face.GetTriangles(sub));var facePoints=face.vertices;
   float GlassDepth(Vector3 sample){float x=Vector3.Dot(sample,right),y=Vector3.Dot(sample,up),best=-float.MaxValue;for(int t=0;t<eyeTriangles.Count;t+=3){var a=facePoints[eyeTriangles[t]];var b=facePoints[eyeTriangles[t+1]];var c=facePoints[eyeTriangles[t+2]];float ax=Vector3.Dot(a,right),ay=Vector3.Dot(a,up),bx=Vector3.Dot(b,right),by=Vector3.Dot(b,up),cx=Vector3.Dot(c,right),cy=Vector3.Dot(c,up),den=(by-cy)*(ax-cx)+(cx-bx)*(ay-cy);if(Mathf.Abs(den)<1e-12f)continue;float wa=((by-cy)*(x-cx)+(cx-bx)*(y-cy))/den,wb=((cy-ay)*(x-cx)+(ax-cx)*(y-cy))/den,wc=1-wa-wb;if(Mathf.Min(wa,wb,wc)<-.0001f)continue;best=Mathf.Max(best,Vector3.Dot(a*wa+b*wb+c*wc,forward));}return best==-float.MaxValue?Vector3.Dot(sample,forward):best;}
   var vertices=face.vertices.ToList();var outNormals=face.normals.ToList();var uv=face.uv.Length==face.vertexCount?face.uv.ToList():Enumerable.Repeat(Vector2.zero,face.vertexCount).ToList();var triangles=new List<int>();
   const int rows=8;
   foreach(var loop in chosen){
    var centre=loop.Aggregate(Vector3.zero,(a,i)=>a+unique[i])/loop.Count;
    var order=loop.OrderBy(i=>Mathf.Atan2(Vector3.Dot(unique[i]-centre,up),Vector3.Dot(unique[i]-centre,right))).ToArray();int start=vertices.Count;
    foreach(int id in order){
     var point=unique[id];var radial=(right*Vector3.Dot(point-centre,right)+up*Vector3.Dot(point-centre,up)).normalized;
     float innerWidth=hero.female?.0020f:.00075f;var inner=point-radial*innerWidth;float glassDepth=GlassDepth(inner);inner+=forward*(glassDepth-Vector3.Dot(inner,forward)+.00040f);
     var outer=point+radial*.0055f;
     projection.Fit(Vector3.Dot(outer,right),Vector3.Dot(outer,up),Vector3.Dot(outer,forward),out outer,out var outerNormal);
     if(Vector3.Dot(outerNormal,forward)<.15f)outerNormal=normals[id];
     for(int row=0;row<=rows;row++){
      float t=(float)row/rows;var q=Vector3.Lerp(inner,outer,t)+forward*(.00085f*Mathf.Sin(t*Mathf.PI));
      vertices.Add(q);outNormals.Add(Vector3.Slerp(forward,outerNormal,t).normalized);uv.Add(new Vector2(t,0));
     }
    }
    for(int i=0;i<order.Length;i++)for(int row=0;row<rows;row++){int a=start+i*(rows+1)+row,b=start+((i+1)%order.Length)*(rows+1)+row,c=b+1,d=a+1;int[] tr={a,b,c,a,c,d};for(int k=0;k<6;k+=3)if(Vector3.Dot(Vector3.Cross(vertices[tr[k+1]]-vertices[tr[k]],vertices[tr[k+2]]-vertices[tr[k]]),forward)<0)(tr[k+1],tr[k+2])=(tr[k+2],tr[k+1]);triangles.AddRange(tr);}
   }
   var smooth=new Vector3[vertices.Count];for(int t=0;t<triangles.Count;t+=3){int a=triangles[t],b=triangles[t+1],c=triangles[t+2];var normal=Vector3.Cross(vertices[b]-vertices[a],vertices[c]-vertices[a]);smooth[a]+=normal;smooth[b]+=normal;smooth[c]+=normal;}
   for(int i=face.vertexCount;i<vertices.Count;i++)if((i-face.vertexCount)%(rows+1)!=0&&(i-face.vertexCount)%(rows+1)!=rows&&smooth[i].sqrMagnitude>1e-12f)outNormals[i]=smooth[i].normalized;
   var result=UnityEngine.Object.Instantiate(face);result.hideFlags=HideFlags.DontSave;result.name=face.name+" (rounded eye skin rim)";result.vertices=vertices.ToArray();result.normals=outNormals.ToArray();result.uv=uv.ToArray();result.subMeshCount=face.subMeshCount+1;result.SetTriangles(triangles,face.subMeshCount);result.RecalculateBounds();Debug.Log("[HeroEyeRim] actual aperture loops="+string.Join(",",chosen.Select(l=>l.Count))+" new vertices="+(result.vertexCount-face.vertexCount)+" 5.5mm Body-fitted outer/1.5–3mm cornea-overlapping inner modeled lids; original eye anchors exact");return result;
  }
 }
}
