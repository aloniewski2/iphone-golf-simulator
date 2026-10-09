using System;using System.Collections.Generic;using System.Linq;using UnityEngine;
namespace GolfArcade.Tennis {
 /// A Face-local height field of the actual weighted head bind surface. Paint is
 /// fitted to this surface rather than averaging old decal depths that penetrate
 /// the head. All feature bounds and eye surfaces remain where authored.
 public sealed class HeroFaceProjection {
  const float Cell=.004f;
  readonly Vector3 right,up,forward;readonly Vector3[] positions,normals;readonly int[] triangles;
  readonly Dictionary<Vector2Int,List<int>> grid=new();
  public HeroFaceProjection(MatchHeroLook hero){
   HeroFaceBasis.Get(hero,out right,out up,out forward);var body=hero.body;var mesh=body.sharedMesh;var head=hero.Bone(HumanBodyBones.Head);int index=Array.IndexOf(body.bones,head);
   // Mixed Head/Neck weights are part of the real face surface. Project the
   // currently evaluated weighted skin, not an assumed all-Head rigid copy.
   var maps=body.bones.Select((bone,i)=>hero.face.transform.worldToLocalMatrix*bone.localToWorldMatrix*mesh.bindposes[i]).ToArray();var nm=maps.Select(m=>m.inverse.transpose).ToArray();var raw=mesh.vertices;var oldNormals=mesh.normals;var weights=mesh.boneWeights;positions=new Vector3[raw.Length];normals=new Vector3[raw.Length];
   for(int i=0;i<raw.Length;i++){var w=weights[i];positions[i]=maps[w.boneIndex0].MultiplyPoint3x4(raw[i])*w.weight0+maps[w.boneIndex1].MultiplyPoint3x4(raw[i])*w.weight1+maps[w.boneIndex2].MultiplyPoint3x4(raw[i])*w.weight2+maps[w.boneIndex3].MultiplyPoint3x4(raw[i])*w.weight3;normals[i]=(nm[w.boneIndex0].MultiplyVector(oldNormals[i])*w.weight0+nm[w.boneIndex1].MultiplyVector(oldNormals[i])*w.weight1+nm[w.boneIndex2].MultiplyVector(oldNormals[i])*w.weight2+nm[w.boneIndex3].MultiplyVector(oldNormals[i])*w.weight3).normalized;}triangles=mesh.triangles;
   var faceMesh=hero.face.GetComponent<MeshFilter>().sharedMesh;var faceMaterials=hero.face.sharedMaterials;var eyePoints=new List<Vector3>();for(int s=0;s<Mathf.Min(faceMesh.subMeshCount,faceMaterials.Length);s++)if(faceMaterials[s]&&faceMaterials[s].name.Contains("Sclera"))eyePoints.AddRange(faceMesh.GetTriangles(s).Select(i=>faceMesh.vertices[i]));var eyeCentre=eyePoints.Aggregate(Vector3.zero,(sum,p)=>sum+p)/eyePoints.Count;
   float ex=X(eyeCentre),ey=Y(eyeCentre),ez=Z(eyeCentre);
   for(int t=0;t<triangles.Length;t+=3){int a=triangles[t],b=triangles[t+1],c=triangles[t+2];var centre=(positions[a]+positions[b]+positions[c])/3;if(Mathf.Abs(X(centre)-ex)>.145f||Y(centre)<ey-.21f||Y(centre)>ey+.15f||Z(centre)<ez-.18f)continue;float ax=X(positions[a]),bx=X(positions[b]),cx=X(positions[c]),ay=Y(positions[a]),by=Y(positions[b]),cy=Y(positions[c]);
    for(int x=Mathf.FloorToInt(Mathf.Min(ax,bx,cx)/Cell);x<=Mathf.FloorToInt(Mathf.Max(ax,bx,cx)/Cell);x++)for(int y=Mathf.FloorToInt(Mathf.Min(ay,by,cy)/Cell);y<=Mathf.FloorToInt(Mathf.Max(ay,by,cy)/Cell);y++){var key=new Vector2Int(x,y);if(!grid.TryGetValue(key,out var list))grid[key]=list=new List<int>();list.Add(t);}
   }
  }
  float X(Vector3 p)=>Vector3.Dot(p,right);float Y(Vector3 p)=>Vector3.Dot(p,up);float Z(Vector3 p)=>Vector3.Dot(p,forward);
  public bool Fit(float x,float y,float fallback,out Vector3 point,out Vector3 normal){
   point=right*x+up*y+forward*fallback;normal=forward;bool found=false;float depth=-float.MaxValue;
   if(!grid.TryGetValue(new Vector2Int(Mathf.FloorToInt(x/Cell),Mathf.FloorToInt(y/Cell)),out var list))return false;
   foreach(int t in list){int ia=triangles[t],ib=triangles[t+1],ic=triangles[t+2];var a=positions[ia];var b=positions[ib];var c=positions[ic];float ax=X(a),ay=Y(a),bx=X(b),by=Y(b),cx=X(c),cy=Y(c);float denominator=(by-cy)*(ax-cx)+(cx-bx)*(ay-cy);if(Mathf.Abs(denominator)<1e-12f)continue;
    float wa=((by-cy)*(x-cx)+(cx-bx)*(y-cy))/denominator,wb=((cy-ay)*(x-cx)+(ax-cx)*(y-cy))/denominator,wc=1-wa-wb;if(Mathf.Min(wa,wb,wc)<-.0001f)continue;float z=wa*Z(a)+wb*Z(b)+wc*Z(c);if(z<depth||Mathf.Abs(z-fallback)>.060f)continue;depth=z;point=right*x+up*y+forward*(z+.00012f);normal=(normals[ia]*wa+normals[ib]*wb+normals[ic]*wc).normalized;found=true;
   }return found;
  }
 }
}
