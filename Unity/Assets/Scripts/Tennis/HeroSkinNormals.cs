using System.Collections.Generic;
using UnityEngine;
namespace GolfArcade.Tennis
{
    /// Lossless shading correction: positions/topology/weights stay identical. Average
    /// noisy head normals within 18mm, retaining opposing and sharp anatomical normals.
    public static class HeroSkinNormals
    {
        static readonly Dictionary<Mesh,Mesh> cache=new();
        static readonly Dictionary<Mesh,Mesh> continuousCache=new();
        static readonly Dictionary<Mesh,Mesh> softCache=new();
        static readonly Dictionary<Mesh,Mesh> areaCache=new();
        public static Mesh Prepare(SkinnedMeshRenderer body,Transform head,bool continuousByDefault=false)
        {
            if(!body||!head||!body.sharedMesh.isReadable)return body ? body.sharedMesh:null;
            var source=body.sharedMesh;
            if(System.Environment.GetEnvironmentVariable("VISUAL_CHARACTER_NORMALS")=="continuous" ||
               (continuousByDefault && string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("VISUAL_CHARACTER_NORMALS"))))
                return Continuous(body,head,source);
            if(System.Environment.GetEnvironmentVariable("VISUAL_CHARACTER_NORMALS")=="soft")
                return Continuous(body,head,source,true);
            if(cache.TryGetValue(source,out var ready))return ready;
            var p=source.vertices;var old=source.normals;var n=(Vector3[])old.Clone();var weights=source.boneWeights;
            int headIndex=System.Array.FindIndex(body.bones,b=>b==head);if(headIndex<0)return source;
            bool neckField=System.Environment.GetEnvironmentVariable("VISUAL_SKIN_NORMAL_FIELD")=="head-neck";
            int neckIndex=System.Array.FindIndex(body.bones,b=>b==head.parent);
            const float radius=.018f;var grid=new Dictionary<Vector3Int,List<int>>();var selected=new List<int>();
            Vector3Int Cell(Vector3 v)=>new(Mathf.FloorToInt(v.x/radius),Mathf.FloorToInt(v.y/radius),Mathf.FloorToInt(v.z/radius));
            for(int i=0;i<p.Length;i++){
                var w=weights[i];float h=(w.boneIndex0==headIndex?w.weight0:0)+(w.boneIndex1==headIndex?w.weight1:0)+(w.boneIndex2==headIndex?w.weight2:0)+(w.boneIndex3==headIndex?w.weight3:0);
                float neck=(w.boneIndex0==neckIndex?w.weight0:0)+(w.boneIndex1==neckIndex?w.weight1:0)+(w.boneIndex2==neckIndex?w.weight2:0)+(w.boneIndex3==neckIndex?w.weight3:0);
                if(neckField ? h+neck<.5f : h<.75f)continue;selected.Add(i);var cell=Cell(p[i]);if(!grid.TryGetValue(cell,out var list))grid[cell]=list=new List<int>();list.Add(i);
            }
            float largest=0;
            foreach(int i in selected){
                var cell=Cell(p[i]);var sum=old[i]*.35f;float count=.35f;
                for(int x=-1;x<=1;x++)for(int y=-1;y<=1;y++)for(int z=-1;z<=1;z++)if(grid.TryGetValue(cell+new Vector3Int(x,y,z),out var list))foreach(int j in list){
                    float d=(p[i]-p[j]).sqrMagnitude;if(d>=radius*radius||Vector3.Dot(old[i],old[j])<.35f)continue;
                    float w=1-d/(radius*radius);sum+=old[j]*w;count+=w;
                }
                n[i]=(sum/count).normalized;largest=Mathf.Max(largest,Vector3.Angle(old[i],n[i]));
            }
            ready=Object.Instantiate(source);ready.name=source.name+" (refined skin normals)";ready.hideFlags=HideFlags.DontSave;ready.normals=n;cache[source]=ready;
            Debug.Log("[HeroSkinNormals] "+source.name+" field="+(neckField?"head-neck":"head")+" vertices="+selected.Count+" max normal correction="+largest+"deg; geometry unchanged");return ready;
        }

        // Use the final, already-faired mesh as the geometric
        // authority. A continuous anatomical region includes the mixed Head/Neck
        // jaw vertices omitted by the legacy 75-percent Head-weight threshold.
        // Only the normal array is replaced; the complete source mesh is cloned.
        static Mesh Continuous(SkinnedMeshRenderer body,Transform head,Mesh source,bool soft=false)
        {
            // Spatial filtering integrates represented surface area, rather than
            // giving each tessellation vertex equal influence. Explicit 0 keeps
            // the legacy diagnostic comparison available.
            bool areaWeighted=!soft && System.Environment.GetEnvironmentVariable("VISUAL_CHARACTER_NORMAL_AREA")!="0";
            string suffix=areaWeighted?" (area continuous skin normals)":soft?" (soft skin normals)":" (continuous skin normals)";
            var activeCache=areaWeighted?areaCache:soft?softCache:continuousCache;
            if(source.name.EndsWith(suffix))return source;
            if(activeCache.TryGetValue(source,out var cached)&&cached)return cached;
            var p=source.vertices;var old=source.normals;
            if(old.Length!=p.Length)return source;
            int hi=System.Array.FindIndex(body.bones,b=>b==head);
            int ni=System.Array.FindIndex(body.bones,b=>b==head.parent);
            var binds=source.bindposes;
            if(hi<0||ni<0||hi>=binds.Length||ni>=binds.Length)return source;
            Vector3 headPoint=binds[hi].inverse.MultiplyPoint3x4(Vector3.zero);
            Vector3 neckPoint=binds[ni].inverse.MultiplyPoint3x4(Vector3.zero);
            Vector3 up=(headPoint-neckPoint).normalized;
            if(up.sqrMagnitude<.9f)return source;
            float radius=soft?.018f:areaWeighted?.012f:.008f,angleCap=soft?30f:areaWeighted?45f:20f;
            float neckHeight=Vector3.Dot(neckPoint-headPoint,up);
            float Smooth(float a,float b,float v){float t=Mathf.Clamp01((v-a)/(b-a));return t*t*(3-2*t);}
            Vector3Int PositionKey(Vector3 v)=>new(Mathf.RoundToInt(v.x*1000000),Mathf.RoundToInt(v.y*1000000),Mathf.RoundToInt(v.z*1000000));
            Vector3Int Cell(Vector3 v)=>new(Mathf.FloorToInt(v.x/radius),Mathf.FloorToInt(v.y/radius),Mathf.FloorToInt(v.z/radius));
            var weld=new Dictionary<Vector3Int,int>();var id=new int[p.Length];var unique=new List<Vector3>();
            for(int i=0;i<p.Length;i++){
                var key=PositionKey(p[i]);if(!weld.TryGetValue(key,out int j)){j=unique.Count;weld.Add(key,j);unique.Add(p[i]);}id[i]=j;
            }
            // Area-weighted geometric normals are shared at exact positional
            // seams, preventing UV/corner splits from creating lighting seams.
            var geometric=new Vector3[unique.Count];var representedArea=new float[unique.Count];var triangles=source.triangles;
            for(int t=0;t<triangles.Length;t+=3){
                int a=triangles[t],b=triangles[t+1],c=triangles[t+2];
                Vector3 area=Vector3.Cross(p[b]-p[a],p[c]-p[a]);
                geometric[id[a]]+=area;geometric[id[b]]+=area;geometric[id[c]]+=area;
                float size=area.magnitude;representedArea[id[a]]+=size;representedArea[id[b]]+=size;representedArea[id[c]]+=size;
            }
            var strength=new float[unique.Count];var grid=new Dictionary<Vector3Int,List<int>>();int selected=0;
            for(int i=0;i<unique.Count;i++){
                Vector3 delta=unique[i]-headPoint;float height=Vector3.Dot(delta,up);
                float radial=(delta-up*height).magnitude;
                // Fade below the neck and outside the head. No bone-weight
                // threshold can split the middle of the cheek or mandible.
                strength[i]=Smooth(neckHeight-.032f,neckHeight+.004f,height)*(1-Smooth(.145f,.185f,radial));
                geometric[i]=geometric[i].normalized;
                if(strength[i]<.001f||geometric[i].sqrMagnitude<.9f)continue;
                selected++;var cell=Cell(unique[i]);if(!grid.TryGetValue(cell,out var list))grid[cell]=list=new List<int>();list.Add(i);
            }
            var field=new Vector3[unique.Count];
            for(int i=0;i<unique.Count;i++){
                if(strength[i]<.001f)continue;
                // Preserve the small lip roll while averaging larger cheek and
                // forehead planes. The same continuous field covers the jaw.
                var localHead=binds[hi].MultiplyPoint3x4(unique[i]);
                float mouthMask=(1-Smooth(.040f,.065f,Mathf.Abs(localHead.x)))*(1-Smooth(.014f,.036f,Mathf.Abs(localHead.y+.007f)))*Smooth(.065f,.095f,localHead.z);
                float localRadius=areaWeighted?Mathf.Lerp(radius,.006f,mouthMask):radius;
                float noseMask=Mathf.Exp(-2*Mathf.Pow(localHead.x/.030f,2)-2*Mathf.Pow((localHead.y-.027f)/.023f,2))*Smooth(.105f,.120f,localHead.z);
                float earMask=Smooth(.078f,.096f,Mathf.Abs(localHead.x))*(1-Smooth(.030f,.060f,localHead.z))*(1-Smooth(.070f,.110f,Mathf.Abs(localHead.y-.040f)));
                if(areaWeighted){localRadius=Mathf.Lerp(localRadius,.005f,noseMask);localRadius=Mathf.Lerp(localRadius,.0035f,earMask);}
                var cell=Cell(unique[i]);float seed=areaWeighted?representedArea[i]:1;Vector3 sum=geometric[i]*seed;float count=seed;
                for(int x=-1;x<=1;x++)for(int y=-1;y<=1;y++)for(int z=-1;z<=1;z++)
                    if(grid.TryGetValue(cell+new Vector3Int(x,y,z),out var neighbours))foreach(int j in neighbours){
                        float d=(unique[i]-unique[j]).sqrMagnitude;if(d>=localRadius*localRadius)continue;
                        float agreement=Vector3.Dot(geometric[i],geometric[j]);if(agreement<.5f)continue;
                        float falloff=1-d/(localRadius*localRadius);float weight=falloff*falloff*agreement*agreement;
                        if(areaWeighted)weight*=representedArea[j];
                        sum+=geometric[j]*weight;count+=weight;
                    }
                field[i]=(sum/Mathf.Max(count,1e-12f)).normalized;
            }
            var normals=(Vector3[])old.Clone();float largest=0;int changed=0;
            for(int i=0;i<p.Length;i++){
                int j=id[i];if(strength[j]<.001f||field[j].sqrMagnitude<.9f)continue;
                Vector3 original=old[i].normalized;
                if(original.sqrMagnitude<.5f)original=field[j];
                Vector3 target=Vector3.RotateTowards(original,field[j],angleCap*Mathf.Deg2Rad,0).normalized;
                normals[i]=Vector3.Slerp(original,target,strength[j]).normalized;
                float angle=Vector3.Angle(original,normals[i]);largest=Mathf.Max(largest,angle);if(angle>.01f)changed++;
            }
            var result=Object.Instantiate(source);result.name=source.name+suffix;
            result.hideFlags=HideFlags.DontSave;result.normals=normals;
            var after=result.vertices;float vertexDelta=0;for(int i=0;i<p.Length;i++)vertexDelta=Mathf.Max(vertexDelta,(after[i]-p[i]).sqrMagnitude);
            activeCache[source]=result;
            Debug.Log("[HeroSkinNormals] field="+(areaWeighted?"area-continuous":soft?"soft":"continuous")+" source="+source.name+" selected="+selected+" changed="+changed+" radiusMetres="+radius+" capDegrees="+angleCap+" maxNormalDegrees="+largest+" maxVertexDeltaSquared="+vertexDelta+"; source topology/weights/UVs/morphs cloned unchanged");
            return result;
        }
    }
}
