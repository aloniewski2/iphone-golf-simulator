using System;
using System.Collections.Generic;
using UnityEngine;
namespace GolfArcade.Tennis
{
    /// Editable Blender patches built from the approved body's exact eye-hole boundaries.
    /// Append one skin-only submesh; open face geometry and the 53-bone rest remain exact.
    public static class HeroLidGeometry
    {
        [Serializable] sealed class Patch { public float[] positions,normals,sourceBoundsMin,sourceBoundsMax,sourceEyeMin,sourceEyeMax,sourceEyeNormal;public int[] triangles; }
        static readonly int[][] axes={new[]{0,1,2},new[]{0,2,1},new[]{1,0,2},new[]{1,2,0},new[]{2,0,1},new[]{2,1,0}};
        public static Mesh Append(MatchHeroLook hero,Mesh face,Material[] materials)
        {
            var asset=Resources.Load<TextAsset>("Tennis/HeroDetail/FaceLids_"+(hero.female ? "Female":"Male"));
            if(!asset||!face||!face.isReadable)return null;
            var data=JsonUtility.FromJson<Patch>(asset.text);
            if(data.positions.Length!=data.normals.Length||data.positions.Length%3!=0)throw new InvalidOperationException("Fitted lid data is invalid");
            // FBX converts authoring axes. Solve its signed permutation against the actual
            // original Face bounds, requiring sub-millimetre agreement rather than guessing.
            Vector3 srcMin=new(data.sourceEyeMin[0],data.sourceEyeMin[1],data.sourceEyeMin[2]);
            Vector3 srcMax=new(data.sourceEyeMax[0],data.sourceEyeMax[1],data.sourceEyeMax[2]);
            var centre=(srcMin+srcMax)*.5f;var size=srcMax-srcMin;
            var original=face.vertices;var originalNormals=face.normals;var eyeBounds=new Bounds();bool found=false;Vector3 targetNormal=Vector3.zero;
            for(int sub=0;sub<materials.Length;sub++)if(materials[sub]&&materials[sub].name.Contains("Sclera"))foreach(int i in face.GetTriangles(sub)){
                if(!found){eyeBounds=new Bounds(original[i],Vector3.zero);found=true;}else eyeBounds.Encapsulate(original[i]);targetNormal+=originalNormals[i];
            }
            if(!found)throw new InvalidOperationException("Sclera registration anchors missing");targetNormal.Normalize();
            var sourceNormal=new Vector3(data.sourceEyeNormal[0],data.sourceEyeNormal[1],data.sourceEyeNormal[2]);
            var targetUp=hero.face.transform.InverseTransformDirection(hero.transform.up).normalized;
            Matrix4x4 best=Matrix4x4.identity;float error=float.MaxValue;
            foreach(var a in axes)for(int signs=0;signs<8;signs++){
                var m=Matrix4x4.zero;m[3,3]=1;for(int d=0;d<3;d++)m[d,a[d]]=(signs&(1<<d))==0 ? 1:-1;
                Vector3 dimensions=new(size[a[0]],size[a[1]],size[a[2]]);
                float e=(dimensions-eyeBounds.size).sqrMagnitude+(1-Vector3.Dot(m.MultiplyVector(sourceNormal),targetNormal))*.001f+(1-Vector3.Dot(m.MultiplyVector(Vector3.forward),targetUp))*.001f;
                if(e<error){error=e;best=m;}
            }
            var offset=eyeBounds.center-best.MultiplyPoint3x4(centre);
            var vertices=new List<Vector3>(face.vertices);var normals=new List<Vector3>(face.normals);int start=vertices.Count;
            // Fit a real Body lid morph to the imported aperture. Retain the verified
            // authored cap as a tiny seal; the Body itself now closes the socket.
            HeroActualLidFit.Build(hero,out var actualVertices,out var actualNormals,out var actualTriangles);
            bool actual=false;
            if(actual){vertices.AddRange(actualVertices);normals.AddRange(actualNormals);best=Matrix4x4.identity;}
            else for(int i=0;i<data.positions.Length;i+=3){
                vertices.Add(best.MultiplyPoint3x4(new Vector3(data.positions[i],data.positions[i+1],data.positions[i+2]))+offset);
                normals.Add(best.MultiplyVector(new Vector3(data.normals[i],data.normals[i+1],data.normals[i+2])).normalized);
            }
            var triangles=actual ? actualTriangles:(int[])data.triangles.Clone();
            for(int i=0;i<triangles.Length;i+=3){
                for(int k=0;k<3;k++)triangles[i+k]+=start;
                if(best.determinant<0)(triangles[i+1],triangles[i+2])=(triangles[i+2],triangles[i+1]);
            }
            // Unity FBX's source triangle convention is authoritative. A raw Blender
            // patch must adopt that convention, including FBX's handedness reversal.
            float sourceSign=0,patchSign=0;
            for(int sub=0;sub<materials.Length;sub++)if(materials[sub]&&materials[sub].name.Contains("Sclera")){
                var idx=face.GetTriangles(sub);for(int k=0;k<idx.Length;k+=3){int a=idx[k],b=idx[k+1],c=idx[k+2];sourceSign+=Vector3.Dot(Vector3.Cross(vertices[b]-vertices[a],vertices[c]-vertices[a]),normals[a]+normals[b]+normals[c]);}
            }
            for(int k=0;k<triangles.Length;k+=3){int a=triangles[k],b=triangles[k+1],c=triangles[k+2];patchSign+=Vector3.Dot(Vector3.Cross(vertices[b]-vertices[a],vertices[c]-vertices[a]),normals[a]+normals[b]+normals[c]);}
            if(sourceSign*patchSign<0)for(int i=0;i<triangles.Length;i+=3)(triangles[i+1],triangles[i+2])=(triangles[i+2],triangles[i+1]);
            var copy=UnityEngine.Object.Instantiate(face);copy.name=face.name+" (authored lids)";copy.hideFlags=HideFlags.DontSave;
            copy.vertices=vertices.ToArray();copy.normals=normals.ToArray();copy.subMeshCount=face.subMeshCount+1;
            copy.SetTriangles(triangles,face.subMeshCount);copy.RecalculateBounds();
            Debug.Log("[HeroLidGeometry] "+(hero.female ? "Female":"Male")+" exact aperture patch vertices="+(data.positions.Length/3)+" actual sclera registration offset="+offset+" shape/axis error="+Mathf.Sqrt(error)+" source winding="+sourceSign+" patch winding="+patchSign+" reversed="+(sourceSign*patchSign<0));
            return copy;
        }
    }
}
