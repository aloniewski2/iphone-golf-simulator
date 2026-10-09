using System;using System.Collections.Generic;using System.Linq;using UnityEngine;
namespace GolfArcade.Tennis {
 /// Editable surface construction on the existing approved facial feature bounds.
 /// Preserve the bald body/head silhouette, eye glass and all feature centers;
 /// rebuild the brows as smooth arches and fit a fine liner to the actual cornea.
 public static class HeroFaceCraft {
  static readonly Dictionary<Mesh,Mesh> cache=new();
  public static Mesh Prepare(MatchHeroLook hero,Mesh source,Material[] materials){
   if(!source||!source.isReadable||source.name.Contains("(crafted face)"))return source;
   foreach(var material in materials)if(material&&material.name.Contains("Brow")&&!material.name.Contains("BrowSoft"))material.SetFloat("_CraftedPaint",0);
   if(cache.TryGetValue(source,out var ready)&&ready)return ready;
   HeroFaceBasis.Get(hero,out var right,out var up,out var forward);var projection=new HeroFaceProjection(hero);var raw=source.vertices;var oldNormals=source.normals;var oldUV=source.uv;
   var vertices=raw.ToList();var normals=oldNormals.ToList();var uv=(oldUV.Length==raw.Length?oldUV:Enumerable.Repeat(Vector2.zero,raw.Length).ToArray()).ToList();
   var submeshes=Enumerable.Range(0,source.subMeshCount).Select(i=>source.GetTriangles(i)).ToArray();
   float X(Vector3 p)=>Vector3.Dot(p,right);float Y(Vector3 p)=>Vector3.Dot(p,up);float Z(Vector3 p)=>Vector3.Dot(p,forward);
   var eyes=new List<Vector3>();for(int s=0;s<Mathf.Min(materials.Length,source.subMeshCount);s++)if(materials[s]&&materials[s].name.Contains("Sclera"))eyes.AddRange(submeshes[s].Select(i=>raw[i]));
   if(eyes.Count<6)return source;float mid=eyes.Average(X);
   int added=0,projected=0,missing=0;float projectionDelta=0;
   for(int s=0;s<Mathf.Min(materials.Length,source.subMeshCount);s++){
    string role=materials[s]?materials[s].name:"";
    if(role.Contains("BrowSoft")){submeshes[s]=Array.Empty<int>();continue;}
    // The source liner/crease meshes include detached and backward fragments.
    // The modeled aperture supplies the new continuous eyelid surround.
    if(role.Contains("LidLine")||role.Contains("LidLower")||role.Contains("LidCrease")){submeshes[s]=Array.Empty<int>();continue;}
    if(role.Contains("LipUp")){submeshes[s]=Array.Empty<int>();continue;}
    if(role.Contains("Face_Lip")){
     var original=submeshes[s].Distinct().Select(i=>raw[i]).ToArray();float mouthX=(original.Min(X)+original.Max(X))*.5f;float halfWidth=(original.Max(X)-original.Min(X))*.5f*(hero.female?.74f:.80f);float fallback=original.Average(Z);
     var seam=new List<Vector3>();for(int j=0;j<materials.Length&&j<submeshes.Length;j++)if(materials[j]&&materials[j].name.Contains("Seam"))seam.AddRange(submeshes[j].Select(i=>raw[i]));float lineY=seam.Count>0?(seam.Max(Y)+seam.Min(Y))*.5f:original.Max(Y)-.001f;
     var lipTriangles=new List<int>();const int columns=64,rowsLip=8;
     foreach(bool upper in new[]{true,false}){int start=vertices.Count;for(int col=0;col<=columns;col++){float a=(float)col/columns,u=a*2-1,x=mouthX+halfWidth*u,shape=Mathf.Pow(Mathf.Max(0,1-u*u),.70f);float line=lineY+.0024f*u*u;float height=(upper?(hero.female?.0019f:.0017f):(hero.female?.0027f:.0023f))*shape;
       if(upper)height*=.80f+.20f*Mathf.Sin(Mathf.Abs(u)*Mathf.PI);
       for(int row=0;row<=rowsLip;row++){float t=(float)row/rowsLip,y=line+(upper?1:-1)*height*t;projection.Fit(x,y,fallback,out var fitted,out var normal);if(Vector3.Dot(normal,forward)<.1f)normal=forward;vertices.Add(fitted+normal*(.00050f+.00020f*Mathf.Sin(t*Mathf.PI)));normals.Add(normal);uv.Add(new Vector2(a,t));}
      }
      for(int col=0;col<columns;col++)for(int row=0;row<rowsLip;row++){int a=start+col*(rowsLip+1)+row,b=a+rowsLip+1,c=b+1,d=a+1;int[] t={a,b,c,a,c,d};for(int k=0;k<6;k+=3)if(Vector3.Dot(Vector3.Cross(vertices[t[k+1]]-vertices[t[k]],vertices[t[k+2]]-vertices[t[k]]),forward)<0)(t[k+1],t[k+2])=(t[k+2],t[k+1]);lipTriangles.AddRange(t);}
     }submeshes[s]=lipTriangles.ToArray();continue;
    }
    if(!role.Contains("Brow")||role.Contains("BrowSoft"))continue;
    var triangles=new List<int>();
    foreach(bool left in new[]{true,false}){
     var ids=submeshes[s].Distinct().Where(i=>(X(raw[i])<mid)==left).ToArray();if(ids.Length<3)continue;var glass=eyes.Where(p=>(X(p)<mid)==left).ToArray();float eyeMin=glass.Min(X),eyeMax=glass.Max(X),eyeY=(glass.Max(Y)+glass.Min(Y))*.5f,eyeHalf=(glass.Max(Y)-glass.Min(Y))*.5f,eyeZ=glass.Average(Z);float width=(eyeMax-eyeMin)*(hero.female?.80f:.98f),eyeX=(eyeMin+eyeMax)*.5f;float xmin=eyeX-width*.5f,xmax=eyeX+width*.5f;
     // Author a continuous arch in the true eye/head frame, then fit every
     // sample to the same refined skin surface as the surrounding face.
     const int segments=64,rows=8;int start=vertices.Count;
     for(int i=0;i<=segments;i++){
      float a=(float)i/segments;float arch=Mathf.Max(0,Mathf.Sin(a*Mathf.PI));float x=Mathf.Lerp(xmin,xmax,a);
      float centre=eyeY+eyeHalf+.0080f+(hero.female?.0035f:.0030f)*arch;
      float thickness=(hero.female?.0025f:.0040f)*Mathf.Pow(arch,.30f)+.00003f;
      for(int j=0;j<=rows;j++){float t=(float)j/rows;float y=centre+Mathf.Lerp(-thickness,thickness,t),z=eyeZ-.015f;
       if(projection.Fit(x,y,z,out var fitted,out var fittedNormal)){projected++;projectionDelta=Mathf.Max(projectionDelta,Mathf.Abs(Vector3.Dot(fitted,forward)-z));}else missing++;if(Vector3.Dot(fittedNormal,forward)<.1f)fittedNormal=forward;float crown=.00065f*Mathf.Sin(t*Mathf.PI);vertices.Add(fitted+fittedNormal*(.0010f+crown));normals.Add(fittedNormal);uv.Add(new Vector2(a,t));
      }
     }
     for(int i=0;i<segments;i++)for(int j=0;j<rows;j++){int a=start+i*(rows+1)+j,b=a+rows+1,c=b+1,d=a+1;int[] t={a,b,c,a,c,d};for(int k=0;k<6;k+=3)if(Vector3.Dot(Vector3.Cross(vertices[t[k+1]]-vertices[t[k]],vertices[t[k+2]]-vertices[t[k]]),forward)<0)(t[k+1],t[k+2])=(t[k+2],t[k+1]);triangles.AddRange(t);}
     added+=(segments+1)*(rows+1);
    }
    submeshes[s]=triangles.ToArray();materials[s].SetFloat("_CraftedPaint",0);
   }
   // Project skin-following paint independently of eyes and their closure shell.
   for(int s=0;s<Mathf.Min(materials.Length,source.subMeshCount);s++){
    var role=materials[s]?materials[s].name:"";if(!role.Contains("Nostril"))continue;
    foreach(int i in submeshes[s].Distinct())if(projection.Fit(X(vertices[i]),Y(vertices[i]),Z(vertices[i]),out var fitted,out var fittedNormal)){vertices[i]=fitted;normals[i]=fittedNormal;}
   }
   for(int i=0;i<vertices.Count;i++)if(float.IsNaN(vertices[i].x)||float.IsNaN(vertices[i].y)||float.IsNaN(vertices[i].z)||float.IsInfinity(vertices[i].x)||float.IsInfinity(vertices[i].y)||float.IsInfinity(vertices[i].z))throw new InvalidOperationException("Crafted face non-finite vertex "+i);
   var mesh=UnityEngine.Object.Instantiate(source);mesh.name=source.name+" (crafted face)";mesh.hideFlags=HideFlags.DontSave;mesh.vertices=vertices.ToArray();mesh.normals=normals.ToArray();mesh.uv=uv.ToArray();for(int s=0;s<source.subMeshCount;s++)mesh.SetTriangles(submeshes[s],s);mesh.RecalculateBounds();cache[source]=mesh;
   Debug.Log("[HeroFaceCraft] Body-fitted="+projected+" missing="+missing+" maxDepthDelta="+projectionDelta+" smooth brow vertices="+added+" fitted1mm upper liner; original bald silhouette/eyes/feature centers unchanged");return mesh;
  }
 }
}
