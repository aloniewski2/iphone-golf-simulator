// DRAFT P3: actual aerial masks against all edges of the one frozen Split SHORE ring.
// Frozen topology is one C-shaped connected polygon, NOT two closed island polygons.
// Measured semantic shoreline arcs never gain invented closing edges. No assets saved.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;
namespace GolfArcade.EditorTools
{
    public static class PostcardV3SplitOutline
    {
        const int W=900,H=1600;
        [Serializable] public sealed class Part {public string name;public int[] edgeStarts;}
        [Serializable] public sealed class Config {public float playY=6.56167979f,waterY=0;public int cliffNearPixels=2,surfBaseNearPixels=6;public float[] frozenShoreXZ;public Part[] parts;public string frozenBoundarySource,frozenBoundarySha256;}
        [Serializable] sealed class Piece {public string name,cliffGate="FAIL",surfBaseGate="FAIL";public int[] originalEdgeStarts,cliffNearestDistanceHistogram32,surfBaseNearestDistanceHistogram32;public int denominatorPixels,cliffAdjacentPixels,surfBaseAdjacentPixels,outOfFramePixels,behindCameraVertices;public double cliffOutlineFraction;public float[] projectedTopXY,projectedBaseXY;}
        [Serializable] sealed class Report {public string tool="PostcardV3SplitOutline",gate="SPLIT_CLIFF_ISLANDS",status="FAIL",arcMeasurementStatus="FAIL",reason,shot,configSha256,boundarySha256,topMask,cliffMask,surfMask;public int width=W,height=H,sourceRendererCount,topPixels,cliffPixels,surfPixels,surfConnectedComponents,controlDriftPixels,unclassifiedFrozenEdges;public bool twoIndependentIslandOutlinesProven;public int frozenClosedBoundaries=1;public Piece[] parts;public string denominator="Original directed edges of the one full frozen SHORE ring, grouped only by supplied source shoreline labels. Unique raster pixels per semantic group sampled at <=0.5px intervals. Hidden backsides and out-of-frame pixels remain in the denominator; no virtual arc closing edges.";public string masks="Actual whole-ground top/cliff/SURF masks, not invented per-island membership. All other geometry is black and depth writing. Opaque alpha-card occlusion is conservative and can underestimate visibility.";public string surfScope="RGB difference with SURF disabled intersected with actual SURF ID; alpha-zero card pixels do not count. Component data diagnostic; brokenness inspected separately.";}
        static bool Name(Material m,string s)=>m&&m.name.StartsWith(s,StringComparison.Ordinal);
        static bool Surf(Renderer r)=>r.name.StartsWith("WATER_SURF",StringComparison.Ordinal)||r.name.StartsWith("SURF",StringComparison.Ordinal)||r.sharedMaterials.Any(m=>Name(m,"LK_SURF"));
        static bool Terrain(Renderer r)=>r.name.StartsWith("TERRAIN",StringComparison.Ordinal);
        static bool Cliff(Renderer r,Material m)=>Name(m,"LK_CLIFF")||((Terrain(r)||r.name.StartsWith("ROCK_SKIN",StringComparison.Ordinal))&&(Name(m,"LK_BASALT")||Name(m,"LK_ROCK")));
        static bool Top(Renderer r,Material m)=>Terrain(r)&&(Name(m,"LK_ROUGH")||Name(m,"LK_SCRUB")||Name(m,"LK_FAIRWAY")||Name(m,"LK_GREEN"));
        static Color32[] Render(Camera cam,RenderTexture rt,Texture2D tex) {var prev=cam.targetTexture;var active=RenderTexture.active;try{cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,W,H),0,0);tex.Apply(false);return tex.GetPixels32();}finally{cam.targetTexture=prev;RenderTexture.active=active;}}
        static bool[] IDs(Camera cam,RenderTexture rt,Texture2D tex,Renderer[] all,HashSet<Renderer> course,Config cfg,int category)
        {
            var shader=Shader.Find("Hidden/GolfArcade/PostcardV3Proof");if(!shader)throw new InvalidOperationException("Proof shader missing");var saved=new Dictionary<Renderer,Material[]>();var temp=new List<Material>();var clear=cam.clearFlags;var background=cam.backgroundColor;bool fog=RenderSettings.fog;
            try {foreach(var r in all){var old=r.sharedMaterials;saved[r]=old;var slots=new Material[old.Length];
                    for(int k=0;k<old.Length;k++){var source=old[k];int role=0;if(course.Contains(r)){if(category==0&&Top(r,source))role=3;if(category==1&&Cliff(r,source))role=2;if(category==2&&Surf(r))role=1;}
                        var m=new Material(shader){hideFlags=HideFlags.HideAndDontSave};temp.Add(m);slots[k]=m;m.SetFloat("_Role",role);m.SetFloat("_PlayY",cfg.playY);m.SetFloat("_Cull",source&&source.HasProperty("_Cull")?source.GetFloat("_Cull"):2);m.SetFloat("_PlantSway",source&&source.shader&&source.shader.name=="GolfArcade/GolfPlants"?1:0);m.SetVector("_ProofPlantWind",Shader.GetGlobalVector("_GolfWind"));
                    }r.sharedMaterials=slots;
                }
                RenderSettings.fog=false;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=Color.black;var px=Render(cam,rt,tex);return px.Select(c=>category==0?c.g>=250&&c.r<=5&&c.b<=5:category==1?c.r>=250&&c.g<=5&&c.b<=5:c.r>=250&&c.g>=250&&c.b>=250).ToArray();
            }finally{foreach(var pair in saved)if(pair.Key)pair.Key.sharedMaterials=pair.Value;RenderSettings.fog=fog;cam.clearFlags=clear;cam.backgroundColor=background;foreach(var m in temp)UnityEngine.Object.DestroyImmediate(m);}
        }
        static void Save(string path,bool[] mask) {var t=new Texture2D(W,H,TextureFormat.RGB24,false,true);try{t.SetPixels32(mask.Select(x=>x?new Color32(255,255,255,255):new Color32(0,0,0,255)).ToArray());t.Apply(false);File.WriteAllBytes(path,t.EncodeToPNG());}finally{UnityEngine.Object.DestroyImmediate(t);}}
        static int Components(bool[] mask) {var seen=new bool[mask.Length];int count=0;var q=new Queue<int>();for(int start=0;start<mask.Length;start++){if(!mask[start]||seen[start])continue;count++;seen[start]=true;q.Enqueue(start);while(q.Count>0){int i=q.Dequeue(),x=i%W,y=i/W;for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++){int nx=x+dx,ny=y+dy;if(nx<0||nx>=W||ny<0||ny>=H)continue;int j=ny*W+nx;if(mask[j]&&!seen[j]){seen[j]=true;q.Enqueue(j);}}}}return count;}
        static Vector2[] Boundary(Camera cam,RenderTexture rt,Config cfg,Part part,float y,out float[] projected,out int behind)
        {
            var previous=cam.targetTexture;
            try {cam.targetTexture=rt; // project with the exact same900x1600 target aspect as the mask frames
            int n=cfg.frozenShoreXZ.Length/2;var vertices=new Vector2[n];projected=new float[n*2];var depths=new float[n];behind=0;
            for(int k=0;k<n;k++){var p=cam.WorldToViewportPoint(new Vector3(cfg.frozenShoreXZ[k*2],y,cfg.frozenShoreXZ[k*2+1]));depths[k]=p.z;vertices[k]=new Vector2(p.x*W,p.y*H);projected[k*2]=vertices[k].x;projected[k*2+1]=vertices[k].y;}
            var used=new HashSet<int>();var set=new HashSet<Vector2Int>();foreach(int k in part.edgeStarts){if(k<0||k>=n)throw new InvalidOperationException("Original shore edge index outside frozen ring");int next=(k+1)%n;used.Add(k);used.Add(next);var a=vertices[k];var b=vertices[next];int steps=Mathf.Max(1,Mathf.CeilToInt(Vector2.Distance(a,b)*2));if(steps>100000||set.Count>4000000)throw new InvalidOperationException("Invalid/unusable projected boundary; no clipping performed");for(int j=0;j<=steps;j++){var p=Vector2.Lerp(a,b,(float)j/steps);set.Add(new Vector2Int(Mathf.RoundToInt(p.x),Mathf.RoundToInt(p.y)));}}
            behind=used.Count(k=>depths[k]<=0);return set.Select(x=>new Vector2(x.x,x.y)).ToArray();
            }finally{cam.targetTexture=previous;}
        }
        static int NearestPixel(bool[] mask,Vector2 p,int radius) {int px=(int)p.x,py=(int)p.y,best=radius*radius+1;for(int y=Math.Max(0,py-radius);y<=Math.Min(H-1,py+radius);y++)for(int x=Math.Max(0,px-radius);x<=Math.Min(W-1,px+radius);x++){if(!mask[y*W+x])continue;int d=(x-px)*(x-px)+(y-py)*(y-py);if(d<best)best=d;}return best;}
        static int CountNear(bool[] mask,Vector2[] boundary,int radius)=>boundary.Count(p=>NearestPixel(mask,p,radius)<=radius*radius);
        static int[] Histogram(bool[] mask,Vector2[] boundary) {var counts=new int[34];foreach(var p in boundary){int d=NearestPixel(mask,p,32);int bin=d<=1024?Mathf.CeilToInt(Mathf.Sqrt(d)):33;counts[bin]++;}return counts;}
        static string Hash(byte[] bytes) {using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}
        public static void Capture(string shot,Camera cam,RenderTexture rt,Texture2D tex,Color32[] full,Transform model,Renderer[] course,string outDir,string configPath)
        {
            if(rt.width!=W||rt.height!=H||full.Length!=W*H)throw new InvalidOperationException("Split proof requires original 900x1600 aerial");var cfg=JsonUtility.FromJson<Config>(File.ReadAllText(configPath));var report=new Report{shot=shot,configSha256=Hash(File.ReadAllBytes(configPath))};var all=UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).Where(r=>r.enabled&&r.gameObject.activeInHierarchy).ToArray();
            try {
                if(cfg.frozenShoreXZ==null||cfg.frozenShoreXZ.Length<6||cfg.frozenShoreXZ.Length%2!=0||cfg.parts==null||cfg.parts.Length!=2||cfg.parts.Any(p=>p.edgeStarts==null||p.edgeStarts.Length==0))throw new InvalidOperationException("One full frozen SHORE plus two explicitly classified semantic arc edge lists required");
                if(cfg.cliffNearPixels!=2||cfg.surfBaseNearPixels!=6)throw new InvalidOperationException("Raster support must match recorded 2px cliff/6px surf conditions");if(!File.Exists(cfg.frozenBoundarySource))throw new InvalidOperationException("Frozen boundary source missing");report.boundarySha256=Hash(File.ReadAllBytes(cfg.frozenBoundarySource));if(report.boundarySha256!=cfg.frozenBoundarySha256)throw new InvalidOperationException("Frozen boundary SHA mismatch");
                var used=new HashSet<int>(cfg.parts.SelectMany(p=>p.edgeStarts));report.unclassifiedFrozenEdges=Enumerable.Range(0,cfg.frozenShoreXZ.Length/2).Count(k=>!used.Contains(k));if(report.unclassifiedFrozenEdges>0)throw new InvalidOperationException("Unclassified frozen shore edges would silently drop part of denominator");
                var repeated=Render(cam,rt,tex);for(int k=0;k<full.Length;k++)if(full[k].r!=repeated[k].r||full[k].g!=repeated[k].g||full[k].b!=repeated[k].b)report.controlDriftPixels++;
                var surf=course.Where(Surf).ToArray();var enable=surf.Select(x=>x.enabled).ToArray();Color32[] without;try{foreach(var r in surf)r.enabled=false;without=Render(cam,rt,tex);File.WriteAllBytes(Path.Combine(outDir,shot+".without_surf.png"),tex.EncodeToPNG());}finally{for(int k=0;k<surf.Length;k++)surf[k].enabled=enable[k];}
                var diff=new bool[full.Length];for(int k=0;k<full.Length;k++)diff[k]=Math.Abs(full[k].r-without[k].r)+Math.Abs(full[k].g-without[k].g)+Math.Abs(full[k].b-without[k].b)>=12;Save(Path.Combine(outDir,shot+".surf_actual_difference.png"),diff);
                var courseSet=new HashSet<Renderer>(course);report.sourceRendererCount=course.Length;var top=IDs(cam,rt,tex,all,courseSet,cfg,0);var cliff=IDs(cam,rt,tex,all,courseSet,cfg,1);var surfID=IDs(cam,rt,tex,all,courseSet,cfg,2);var actual=surfID.Select((x,k)=>x&&diff[k]).ToArray();
                report.topMask=shot+".split_ground_top.png";report.cliffMask=shot+".split_actual_cliff.png";report.surfMask=shot+".split_actual_surf.png";Save(Path.Combine(outDir,report.topMask),top);Save(Path.Combine(outDir,report.cliffMask),cliff);Save(Path.Combine(outDir,report.surfMask),actual);report.topPixels=top.Count(x=>x);report.cliffPixels=cliff.Count(x=>x);report.surfPixels=actual.Count(x=>x);report.surfConnectedComponents=Components(actual);
                var results=new List<Piece>();foreach(var part in cfg.parts){var piece=new Piece{name=part.name,originalEdgeStarts=part.edgeStarts};var boundary=Boundary(cam,rt,cfg,part,cfg.playY,out piece.projectedTopXY,out piece.behindCameraVertices);var baseBoundary=Boundary(cam,rt,cfg,part,cfg.waterY,out piece.projectedBaseXY,out int behindBase);piece.behindCameraVertices+=behindBase;piece.denominatorPixels=boundary.Length;piece.outOfFramePixels=boundary.Count(p=>p.x<0||p.x>=W||p.y<0||p.y>=H);piece.cliffAdjacentPixels=CountNear(cliff,boundary,cfg.cliffNearPixels);piece.surfBaseAdjacentPixels=CountNear(actual,baseBoundary,cfg.surfBaseNearPixels);piece.cliffOutlineFraction=boundary.Length>0?(double)piece.cliffAdjacentPixels/boundary.Length:0;piece.cliffNearestDistanceHistogram32=Histogram(cliff,boundary);piece.surfBaseNearestDistanceHistogram32=Histogram(actual,baseBoundary);piece.cliffGate=report.controlDriftPixels==0&&piece.behindCameraVertices==0&&report.topPixels>0&&piece.cliffOutlineFraction>=.8?"PASS":"FAIL";piece.surfBaseGate=report.controlDriftPixels==0&&piece.behindCameraVertices==0&&piece.surfBaseAdjacentPixels>0?"PASS":"FAIL";results.Add(piece);}
                report.parts=results.ToArray();report.arcMeasurementStatus=results.All(p=>p.cliffGate=="PASS"&&p.surfBaseGate=="PASS")?"PASS":"FAIL";report.twoIndependentIslandOutlinesProven=false;report.status="FAIL";report.reason="Actual semantic shoreline arc measurements retained at the unchanged >=80% cap. Frozen topology has one closed C-shaped boundary, so two independent island-outline masks are not proven. No virtual closing shoreline was invented. Broken SURF remains separately inspected; inherited foam cap unchanged.";
            }catch(Exception e){report.status="FAIL";report.reason=e.ToString();Debug.LogException(e);}
            finally{Render(cam,rt,tex);File.WriteAllText(Path.Combine(outDir,shot+".split_outline.json"),JsonUtility.ToJson(report,true)+"\n");Debug.Log("GATE: SPLIT_CLIFF_ISLANDS "+report.status+" - "+report.reason);}
        }
    }
}
