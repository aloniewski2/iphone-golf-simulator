// DRAFT: P1 proof-tool support authorized; install and Unity-compile before use.
// No scene, mesh, material, pipeline, QualitySettings, or PlayerSettings asset is saved.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Profiling;

namespace GolfArcade.EditorTools
{
    public static class PostcardV3FrameProof
    {
        const int W=900, H=1600;
        [Serializable] public sealed class Config { public HoleDef[] holes; public string fairwayBand, greenBand; public int bandSize=1024; public float playY=6.56167979f; public string[] cliffPrefixes; }
        [Serializable] public sealed class HoleDef { public int hole; public Landmark[] landmarks; public float[] channelXZ; }
        [Serializable] public sealed class Landmark
        {
            public string name; public string[] prefixes, exactPaths; public float[] centreBox;
            public bool splitEachRenderer;
        }
        [Serializable] sealed class Member { public string path; public float[] centre, size; public string[] materials; }
        [Serializable] sealed class Mask
        {
            public string name, maskKind, status, reason, file; public bool exactMembership;
            public int pixels, heightPixels, largestComponentHeight, xMin=-1,xMax=-1,yMin=-1,yMax=-1;
            public Member[] members;
        }
        [Serializable] sealed class Raw { public long triangles, drawCalls, setPass; public bool valid; }
        [Serializable] sealed class CounterSample
        {
            public int frameBefore, frameAfter; public Raw unityBefore,unityAfter,profilerBefore,profilerAfter,unityDelta,profilerDelta;
        }
        [Serializable] sealed class Costs
        {
            public string status, reason, scope="Editor Metal, isolated synchronous 900x1600 camera render; includes shadow passes; no claim of device runtime cost";
            public long meshInstanceTriangles; public CounterSample[] samples;
            public long triangles,drawCalls,setPass;
            public string triangleCapStatus,drawCallCapStatus;
        }
        [Serializable] sealed class Bands
        {
            public string status, reason, file, controlRepeatFile, controlDriftFile, membership="actual interpolated UV0 and BaseMap_ST, exact generator primary band field; >=.9 light / <=.1 dark";
            public int fairwayLightPixels,fairwayDarkPixels,greenLightPixels,greenDarkPixels;
            public double fairwayLightMean,fairwayDarkMean,greenLightMean,greenDarkMean;
            public double fairwayGapOverMean,greenGapOverMean,fairwayGapOverLight,greenGapOverLight;
            public string fairwayCapStatus,greenCapStatus;
            public int excludedShadowPixels,excludedEdgePixels,controlDriftPixels,minimumPlateauPixels=512;
            public bool fairwayRendered,greenRendered;
        }
        [Serializable] sealed class Cliff
        {
            public string status, reason, file;
            public int cliffPixels, topPixels, leftHalfCliffPixels,rightHalfCliffPixels,leftAdjacentPixels,rightAdjacentPixels;
            public float playY; public bool leftSidePresent,rightSidePresent;
        }
        [Serializable] sealed class Frame
        {
            public string unityProject;public string tool="PostcardV3FrameProof",unity,graphicsAPI,shot,fullRgbSha256;
            public string idOcclusion="Conservative opaque depth: alpha cards occlude their full geometry; plant vertices use the live GolfPlants displacement. ID masks are underestimates where translucent cards overlap.";
            public int hole,width=W,height=H,frame; public float[] cameraPosition,cameraRotation; public float fov;
            public bool applicationIsPlaying;public float gameTime,timeScale;
            public Costs costs; public Mask[] landmarks; public Mask channel,basalt; public Cliff cliff; public Bands bands;
            public Member[] rendererInventory;
        }

        static string RendererPath(Renderer r,Transform model)
        {
            var parts=new List<string>(); var t=r.transform;
            while(t && t!=model) { parts.Add(t.name); t=t.parent; }
            parts.Reverse(); return string.Join("/",parts);
        }
        static Member Describe(Renderer r,Transform model) => new Member { path=RendererPath(r,model),
            centre=new[]{r.bounds.center.x,r.bounds.center.y,r.bounds.center.z}, size=new[]{r.bounds.size.x,r.bounds.size.y,r.bounds.size.z},
            materials=r.sharedMaterials.Select(m=>m?m.name:"NULL").ToArray() };
        static bool Prefix(string n,string p) => n.StartsWith(p,StringComparison.Ordinal);
        static bool Has(Material m,string p) => m && Prefix(m.name,p);
        static bool Select(Renderer r,Transform model,Landmark d)
        {
            bool byName=(d.prefixes??Array.Empty<string>()).Any(p=>Prefix(r.name,p));
            bool byPath=(d.exactPaths??Array.Empty<string>()).Contains(RendererPath(r,model));
            if(!byName&&!byPath) return false;
            if(d.centreBox!=null&&d.centreBox.Length==6)
            {
                var c=r.bounds.center; var b=d.centreBox;
                return c.x>=b[0]&&c.y>=b[1]&&c.z>=b[2]&&c.x<=b[3]&&c.y<=b[4]&&c.z<=b[5];
            }
            return true;
        }
        static Color32[] Render(Camera cam,RenderTexture rt,Texture2D tex)
        {
            var target=cam.targetTexture; var active=RenderTexture.active;
            try { cam.targetTexture=rt; cam.Render(); RenderTexture.active=rt; tex.ReadPixels(new Rect(0,0,W,H),0,0); tex.Apply(false); return tex.GetPixels32(); }
            finally { cam.targetTexture=target; RenderTexture.active=active; }
        }
        static bool White(Color32 c) => c.r>=250&&c.g>=250&&c.b>=250;
        static bool Red(Color32 c) => c.r>=250&&c.g<=5&&c.b<=5;
        static bool Green(Color32 c) => c.r<=5&&c.g>=250&&c.b<=5;
        static bool Blue(Color32 c) => c.r<=5&&c.g<=5&&c.b>=250;
        static bool Magenta(Color32 c) => c.r>=250&&c.g<=5&&c.b>=250;
        static Mask MaskStats(string name,bool[] mask,string file)
        {
            var m=new Mask { name=name,file=file,xMin=W,yMin=H };
            for(int i=0;i<mask.Length;i++) if(mask[i]) { int x=i%W,y=i/W; m.pixels++;m.xMin=Math.Min(m.xMin,x);m.xMax=Math.Max(m.xMax,x);m.yMin=Math.Min(m.yMin,y);m.yMax=Math.Max(m.yMax,y); }
            if(m.pixels==0) {m.xMin=m.yMin=-1;m.heightPixels=0;} else m.heightPixels=m.yMax-m.yMin+1;
            var seen=new bool[mask.Length];var q=new Queue<int>();
            for(int start=0;start<mask.Length;start++)
            {
                if(!mask[start]||seen[start])continue;
                seen[start]=true;q.Enqueue(start);int lo=H,hi=-1;
                while(q.Count>0) {int i=q.Dequeue(),x=i%W,y=i/W;lo=Math.Min(lo,y);hi=Math.Max(hi,y);
                    for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++) {int nx=x+dx,ny=y+dy;if(nx<0||nx>=W||ny<0||ny>=H)continue;int j=ny*W+nx;if(mask[j]&&!seen[j]){seen[j]=true;q.Enqueue(j);}}
                }
                m.largestComponentHeight=Math.Max(m.largestComponentHeight,hi-lo+1);
            }
            return m;
        }
        static void SaveMask(string path,bool[] mask)
        {
            var t=new Texture2D(W,H,TextureFormat.RGB24,false,true);
            try {t.SetPixels32(mask.Select(b=>b?new Color32(255,255,255,255):new Color32(0,0,0,255)).ToArray());t.Apply(false);File.WriteAllBytes(path,t.EncodeToPNG());}
            finally {UnityEngine.Object.DestroyImmediate(t);}
        }
        static Raw UnityRaw() => new Raw {valid=true,triangles=UnityStats.triangles,drawCalls=UnityStats.drawCalls,setPass=UnityStats.setPassCalls};
        static Raw ProfRaw(ProfilerRecorder tr,ProfilerRecorder dc,ProfilerRecorder sp) => new Raw {valid=tr.Valid&&dc.Valid&&sp.Valid,
            triangles=tr.Valid?tr.CurrentValue:0,drawCalls=dc.Valid?dc.CurrentValue:0,setPass=sp.Valid?sp.CurrentValue:0};
        static Raw Delta(Raw b,Raw a) => new Raw {valid=b.valid&&a.valid,triangles=a.triangles-b.triangles,drawCalls=a.drawCalls-b.drawCalls,setPass=a.setPass-b.setPass};
        static bool Usable(Raw d) => d.valid&&d.triangles>0&&d.drawCalls>0&&d.setPass>=0;
        static bool Equal(Raw a,Raw b) => a.triangles==b.triangles&&a.drawCalls==b.drawCalls&&a.setPass==b.setPass;
        static Costs Measure(Camera cam,RenderTexture rt,Texture2D tex,Renderer[] course)
        {
            var result=new Costs();
            foreach(var r in course) {var mf=r.GetComponent<MeshFilter>();var mesh=mf?mf.sharedMesh:(r is SkinnedMeshRenderer sk?sk.sharedMesh:null);
                if(mesh) for(int i=0;i<mesh.subMeshCount;i++) if(mesh.GetTopology(i)==MeshTopology.Triangles) result.meshInstanceTriangles+=(long)mesh.GetIndexCount(i)/3;
            }
            result.triangleCapStatus=result.meshInstanceTriangles<=300000?"PASS":"FAIL";
            var cams=UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Where(c=>c!=cam).ToArray();var enabled=cams.Select(c=>c.enabled).ToArray();
            using(var tr=new ProfilerRecorder(ProfilerCategory.Render,"Triangles Count",0))
            using(var dc=new ProfilerRecorder(ProfilerCategory.Render,"Draw Calls Count",0))
            using(var sp=new ProfilerRecorder(ProfilerCategory.Render,"SetPass Calls Count",0))
            {
                try {
                    foreach(var c in cams)c.enabled=false;
                    Render(cam,rt,tex); // warm target/materials, excluded from the measured slices
                    var samples=new List<CounterSample>();
                    for(int n=0;n<3;n++) {var s=new CounterSample {frameBefore=Time.frameCount,unityBefore=UnityRaw(),profilerBefore=ProfRaw(tr,dc,sp)};
                        Render(cam,rt,tex); // ReadPixels synchronizes the rendered target before the after snapshot
                        s.frameAfter=Time.frameCount;s.unityAfter=UnityRaw();s.profilerAfter=ProfRaw(tr,dc,sp);
                        s.unityDelta=Delta(s.unityBefore,s.unityAfter);s.profilerDelta=Delta(s.profilerBefore,s.profilerAfter);samples.Add(s);
                    }
                    result.samples=samples.ToArray();
                    bool sameFrame=samples.All(s=>s.frameBefore==s.frameAfter&&s.frameBefore==samples[0].frameBefore);
                    bool u=sameFrame&&samples.All(s=>Usable(s.unityDelta)&&Equal(s.unityDelta,samples[0].unityDelta));
                    bool p=sameFrame&&samples.All(s=>Usable(s.profilerDelta)&&Equal(s.profilerDelta,samples[0].profilerDelta));
                    bool agree=!u||!p||Equal(samples[0].unityDelta,samples[0].profilerDelta);
                    if((u||p)&&agree) {var v=u?samples[0].unityDelta:samples[0].profilerDelta;result.status="PASS";result.triangles=v.triangles;result.drawCalls=v.drawCalls;result.setPass=v.setPass;
                        result.reason="Three identical positive per-render counter deltas; source="+(u?"UnityStats":"ProfilerRecorder")+(u&&p?"; both sources agree":"; other source unavailable/unusable");}
                    else {result.status="FAIL";result.reason="Counter publication could not isolate three consistent positive single-render deltas; raw samples retained. No mesh/submesh estimate substituted.";}
                } finally {for(int i=0;i<cams.Length;i++) if(cams[i])cams[i].enabled=enabled[i];}
            }
            result.drawCallCapStatus=result.status=="PASS"&&result.drawCalls<=250?"PASS":"FAIL";return result;
        }
        static Texture2D LoadBand(string path,int n)
        {
            if(!File.Exists(path))throw new InvalidOperationException("Exact authored band field missing: "+path);
            var bytes=File.ReadAllBytes(path);if(bytes.Length!=n*n*4)throw new InvalidOperationException("Band field byte length mismatch");
            var t=new Texture2D(n,n,TextureFormat.RFloat,true,true) {hideFlags=HideFlags.HideAndDontSave,wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Trilinear};
            t.SetPixelData(bytes,0);t.Apply(true,false);return t;
        }
        static Color32[] IdRender(Camera cam,RenderTexture rt,Texture2D tex,Renderer[] all,
            Func<Renderer,Material,int> role,Config cfg,Texture2D fw=null,Texture2D gr=null,float[] channel=null)
        {
            var shader=Shader.Find("Hidden/GolfArcade/PostcardV3Proof");if(!shader)throw new InvalidOperationException("Proof shader missing");
            var saved=new Dictionary<Renderer,Material[]>();var temps=new List<Material>();bool fog=RenderSettings.fog;var clear=cam.clearFlags;var bg=cam.backgroundColor;
            try {
                foreach(var r in all) {var old=r.sharedMaterials;saved[r]=old;var slots=new Material[old.Length];
                    for(int i=0;i<old.Length;i++) {var original=old[i];int k=role(r,original);var m=new Material(shader) {hideFlags=HideFlags.HideAndDontSave};temps.Add(m);slots[i]=m;
                        m.SetFloat("_Role",k);m.SetFloat("_PlayY",cfg.playY);m.SetFloat("_Cull",original&&original.HasProperty("_Cull")?original.GetFloat("_Cull"):2);
                        m.SetFloat("_PlantSway",original&&original.shader&&original.shader.name=="GolfArcade/GolfPlants"?1:0);
                        m.SetVector("_ProofPlantWind",Shader.GetGlobalVector("_GolfWind"));
                        if(original&&original.HasProperty("_BaseMap")) {var sc=original.GetTextureScale("_BaseMap");var of=original.GetTextureOffset("_BaseMap");m.SetVector("_BaseMap_ST",new Vector4(sc.x,sc.y,of.x,of.y));}
                        if(k==4)m.SetTexture("_BandMap",fw);if(k==5)m.SetTexture("_BandMap",gr);
                        if(k==6&&channel!=null) {int count=channel.Length/2;if(count<3||count>16)throw new InvalidOperationException("Channel polygon must have 3..16 world-XZ vertices");var pp=new Vector4[16];for(int j=0;j<count;j++)pp[j]=new Vector4(channel[j*2],channel[j*2+1],0,0);m.SetFloat("_ChannelCount",count);m.SetVectorArray("_ChannelPoints",pp);}
                    }r.sharedMaterials=slots;
                }
                RenderSettings.fog=false;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=Color.black;return Render(cam,rt,tex);
            } finally {foreach(var pair in saved)if(pair.Key)pair.Key.sharedMaterials=pair.Value;RenderSettings.fog=fog;cam.clearFlags=clear;cam.backgroundColor=bg;foreach(var m in temps)UnityEngine.Object.DestroyImmediate(m);}
        }
        static double Luminance(Color32 c) => (.2126*c.r+.7152*c.g+.0722*c.b)/255.0;
        static Bands BandStats(Color32[] ids,Color32[] full,bool[] shadowMask,int controlDriftPixels,string file)
        {
            var b=new Bands {file=file,controlDriftPixels=controlDriftPixels};double[] sum=new double[4];int[] count=new int[4];
            Func<Color32,int> category=c=>Red(c)?0:Green(c)?1:Blue(c)?2:Magenta(c)?3:-1;
            for(int i=0;i<ids.Length;i++) {int k=category(ids[i]);if(k<0)continue;
                if(k<2)b.fairwayRendered=true;else b.greenRendered=true;
                if(shadowMask[i]){b.excludedShadowPixels++;continue;}
                int x=i%W,y=i/W;bool edge=x==0||x==W-1||y==0||y==H-1;
                for(int dy=-1;dy<=1&&!edge;dy++)for(int dx=-1;dx<=1;dx++)if(category(ids[(y+dy)*W+x+dx])!=k){edge=true;break;}
                if(edge){b.excludedEdgePixels++;continue;}count[k]++;sum[k]+=Luminance(full[i]);
            }
            b.fairwayLightPixels=count[0];b.fairwayDarkPixels=count[1];b.greenLightPixels=count[2];b.greenDarkPixels=count[3];
            b.fairwayLightMean=count[0]>0?sum[0]/count[0]:0;b.fairwayDarkMean=count[1]>0?sum[1]/count[1]:0;
            b.greenLightMean=count[2]>0?sum[2]/count[2]:0;b.greenDarkMean=count[3]>0?sum[3]/count[3]:0;
            b.fairwayGapOverMean=Math.Abs(b.fairwayLightMean-b.fairwayDarkMean)/Math.Max(1e-9,.5*(b.fairwayLightMean+b.fairwayDarkMean));
            b.greenGapOverMean=Math.Abs(b.greenLightMean-b.greenDarkMean)/Math.Max(1e-9,.5*(b.greenLightMean+b.greenDarkMean));
            b.fairwayGapOverLight=Math.Abs(b.fairwayLightMean-b.fairwayDarkMean)/Math.Max(1e-9,b.fairwayLightMean);
            b.greenGapOverLight=Math.Abs(b.greenLightMean-b.greenDarkMean)/Math.Max(1e-9,b.greenLightMean);
            // Each visible material requires both authored plateaus. An absent green is FAIL/unverified, never a zero-contrast PASS.
            b.fairwayCapStatus=controlDriftPixels==0&&count[0]>=512&&count[1]>=512&&b.fairwayGapOverLight<=.12?"PASS":"FAIL";
            b.greenCapStatus=controlDriftPixels==0&&count[2]>=512&&count[3]>=512&&b.greenGapOverLight<=.08?"PASS":"FAIL";
            b.status=(b.fairwayRendered||b.greenRendered)&&(!b.fairwayRendered||b.fairwayCapStatus=="PASS")&&(!b.greenRendered||b.greenCapStatus=="PASS")?"PASS":"FAIL";
            b.reason="THRESHOLDS.md fixed relative-to-light caps: fairway .12, green .08. Every rendered material needs >=512 pixels per authored plateau after controlled shadow-off exclusion and 1px erosion. Original repeat must be pixel-identical. Absent material retains an unverified/FAIL cap status but is not claimed as measured; an overall phase gate must require both materials proven in supported frames. No luminance-percentile selection.";return b;
        }
        static bool[] ShadowReceivers(Camera cam,RenderTexture rt,Texture2D tex,Color32[] full,string outDir,string shot,out int drift)
        {
            var control=Render(cam,rt,tex);drift=0;
            File.WriteAllBytes(Path.Combine(outDir,shot+".control_repeat.png"),tex.EncodeToPNG());
            var driftMask=new bool[full.Length];
            for(int i=0;i<full.Length;i++)if(full[i].r!=control[i].r||full[i].g!=control[i].g||full[i].b!=control[i].b){drift++;driftMask[i]=true;}
            SaveMask(Path.Combine(outDir,shot+".control_drift.png"),driftMask);
            var lights=UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None);var old=lights.Select(l=>l.shadows).ToArray();
            Color32[] free;
            try {foreach(var l in lights)l.shadows=LightShadows.None;free=Render(cam,rt,tex);File.WriteAllBytes(Path.Combine(outDir,shot+".no_shadows.png"),tex.EncodeToPNG());}
            finally {for(int i=0;i<lights.Length;i++)if(lights[i])lights[i].shadows=old[i];}
            var mask=new bool[full.Length];for(int i=0;i<full.Length;i++)mask[i]=full[i].r!=free[i].r||full[i].g!=free[i].g||full[i].b!=free[i].b;
            SaveMask(Path.Combine(outDir,shot+".shadow_receiver.png"),mask);return mask;
        }

        // Hook in PostcardProofMasks.Take after full.png and rends have been created, before its material mutations:
        // PostcardV3FrameProof.Capture(s.Name,s.Hole,cam,rt,tex,full,model,rends,outDir);
        // Can also be called by a generic isolated fixture renderer; it does not depend on HoleView.
        public static void Capture(string shot,int hole,Camera cam,RenderTexture rt,Texture2D tex,Color32[] full,
            Transform model,Renderer[] course,string outDir)
        {
            if(rt.width!=W||rt.height!=H||full.Length!=W*H)throw new InvalidOperationException("Proof must use the same 900x1600 pixels");
            string configPath=Environment.GetEnvironmentVariable("GOLF_V3_PROOF_CFG");if(!File.Exists(configPath))throw new InvalidOperationException("GOLF_V3_PROOF_CFG required");
            var cfg=JsonUtility.FromJson<Config>(File.ReadAllText(configPath));var hd=(cfg.holes??Array.Empty<HoleDef>()).FirstOrDefault(h=>h.hole==hole);
            var all=UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).Where(r=>r.enabled&&r.gameObject.activeInHierarchy).ToArray();
            var report=new Frame {unityProject=Directory.GetParent(Application.dataPath).FullName,unity=Application.unityVersion,graphicsAPI=SystemInfo.graphicsDeviceType.ToString(),shot=shot,hole=hole,frame=Time.frameCount,fov=cam.fieldOfView,
                applicationIsPlaying=Application.isPlaying,gameTime=Time.time,timeScale=Time.timeScale,
                cameraPosition=new[]{cam.transform.position.x,cam.transform.position.y,cam.transform.position.z},cameraRotation=new[]{cam.transform.eulerAngles.x,cam.transform.eulerAngles.y,cam.transform.eulerAngles.z},
                rendererInventory=course.Select(r=>Describe(r,model)).ToArray()};
            using(var sha=SHA256.Create()) {var rgb=new byte[full.Length*3];for(int i=0;i<full.Length;i++){rgb[3*i]=full[i].r;rgb[3*i+1]=full[i].g;rgb[3*i+2]=full[i].b;}report.fullRgbSha256=BitConverter.ToString(sha.ComputeHash(rgb)).Replace("-","").ToLowerInvariant();}
            report.costs=Measure(cam,rt,tex,course);
            var shadowMask=ShadowReceivers(cam,rt,tex,full,outDir,shot,out int controlDriftPixels);
            var landmarks=new List<Mask>();
            foreach(var d in hd?.landmarks??Array.Empty<Landmark>()) {
                var selected=course.Where(r=>Select(r,model,d)).ToArray();var sets=d.splitEachRenderer?selected.Select(r=>new[]{r}).ToArray():new[]{selected};
                if(sets.Length==0)sets=new[]{Array.Empty<Renderer>()};
                for(int n=0;n<sets.Length;n++) {var set=new HashSet<Renderer>(sets[n]);string name=d.name+(d.splitEachRenderer?"_"+n.ToString("D3"):"");string file=shot+".landmark_"+name+".png";
                    var ids=IdRender(cam,rt,tex,all,(r,m)=>set.Contains(r)?1:0,cfg);var mask=ids.Select(White).ToArray();SaveMask(Path.Combine(outDir,file),mask);
                    var stat=MaskStats(name,mask,file);stat.exactMembership=true;stat.maskKind="Explicit renderer ID, conservative opaque occlusion";stat.members=sets[n].Select(r=>Describe(r,model)).ToArray();
                    stat.status=set.Count>0&&stat.heightPixels>=64?"PASS":"FAIL";stat.reason="Fixed landmark height >=64 of1600 pixels (4%); exact selected renderer paths listed. Multiple separate stacks are measured individually.";landmarks.Add(stat);
                }
            }
            report.landmarks=landmarks.ToArray();
            if(report.landmarks.Length==0)report.landmarks=new[]{new Mask {name="landmark",status="FAIL",reason="No explicit landmark selection configured for this hole"}};
            if(hd?.channelXZ!=null&&hd.channelXZ.Length>=6) {var courseSet=new HashSet<Renderer>(course);var ids=IdRender(cam,rt,tex,all,(r,m)=>courseSet.Contains(r)&&(Has(m,"LK_WATER")||Has(m,"LK_LAVA"))?6:0,cfg,null,null,hd.channelXZ);
                string file=shot+".channel.png";var mask=ids.Select(White).ToArray();SaveMask(Path.Combine(outDir,file),mask);report.channel=MaskStats("channel",mask,file);report.channel.maskKind="Actual water/lava geometry inside explicit world-XZ polygon, conservative opaque occlusion";report.channel.exactMembership=true;
                report.channel.status=report.channel.heightPixels>=64?"PASS":"FAIL";report.channel.reason="Fixed 64px landmark threshold; polygon must be audited as the open channel between the two islands, never the whole ocean.";
            } else report.channel=new Mask {name="channel",status="FAIL",reason="No audited explicit channel polygon supplied"};
            var courseMembers=new HashSet<Renderer>(course);
            var basaltIds=IdRender(cam,rt,tex,all,(r,m)=>courseMembers.Contains(r)&&Has(m,"LK_BASALT")?1:0,cfg);
            string basaltFile=shot+".basalt.png";var basaltMask=basaltIds.Select(White).ToArray();SaveMask(Path.Combine(outDir,basaltFile),basaltMask);
            report.basalt=MaskStats("basalt",basaltMask,basaltFile);report.basalt.exactMembership=true;report.basalt.maskKind="Exact LK_BASALT material slots, conservative opaque depth";
            report.basalt.status=report.basalt.pixels>0?"PASS":"FAIL";report.basalt.reason="Mask support only; BASALT_JOINTS must be independently measured on the full image using this mask and frozen skeleton thresholds.";
            var cliffIds=IdRender(cam,rt,tex,all,(r,m)=>!courseMembers.Contains(r)?0:
                (Has(m,"LK_CLIFF")||((cfg.cliffPrefixes??new[]{"ROCK_SKIN","TERRAIN"}).Any(p=>Prefix(r.name,p))&&(Has(m,"LK_BASALT")||Has(m,"LK_ROCK"))))?2:
                (Has(m,"LK_FAIRWAY")||Has(m,"LK_GREEN")||Has(m,"LK_ROUGH")||Has(m,"LK_SCRUB"))?3:0,cfg);
            string cliffFile=shot+".cliff_top.png";File.WriteAllBytes(Path.Combine(outDir,cliffFile),tex.EncodeToPNG());var c=new Cliff {file=cliffFile,playY=cfg.playY};
            int stride=W+1;var integral=new int[stride*(H+1)];
            for(int y=0;y<H;y++)for(int x=0;x<W;x++)integral[(y+1)*stride+x+1]=(Green(cliffIds[y*W+x])?1:0)+integral[y*stride+x+1]+integral[(y+1)*stride+x]-integral[y*stride+x];
            for(int i=0;i<cliffIds.Length;i++) {if(Green(cliffIds[i]))c.topPixels++;if(!Red(cliffIds[i]))continue;c.cliffPixels++;int x=i%W,y=i/W;bool left=x<W/2;if(left)c.leftHalfCliffPixels++;else c.rightHalfCliffPixels++;
                int xlo=Math.Max(0,x-24),xhi=Math.Min(W-1,x+24),ylo=y+1,yhi=Math.Min(H-1,y+24);bool adjacent=ylo<=yhi&&
                    integral[(yhi+1)*stride+xhi+1]-integral[ylo*stride+xhi+1]-integral[(yhi+1)*stride+xlo]+integral[ylo*stride+xlo]>0;
                if(adjacent){if(left)c.leftAdjacentPixels++;else c.rightAdjacentPixels++;}
            }
            c.leftSidePresent=c.leftAdjacentPixels>0;c.rightSidePresent=c.rightAdjacentPixels>0;c.status=c.leftSidePresent||c.rightSidePresent?"PASS":"FAIL";
            c.reason="Below-play-surface vertical cliff-category pixels directly beneath visible playing top on at least one screen half. Counts supplied; binary side presence has no invented minimum-area threshold.";report.cliff=c;
            Texture2D fw=null,gr=null;
            try {fw=LoadBand(cfg.fairwayBand,cfg.bandSize);gr=LoadBand(cfg.greenBand,cfg.bandSize);var ids=IdRender(cam,rt,tex,all,(r,m)=>!courseMembers.Contains(r)?0:Has(m,"LK_FAIRWAY")?4:Has(m,"LK_GREEN")?5:0,cfg,fw,gr);
                string file=shot+".mow_bands.png";File.WriteAllBytes(Path.Combine(outDir,file),tex.EncodeToPNG());report.bands=BandStats(ids,full,shadowMask,controlDriftPixels,file);
                report.bands.controlRepeatFile=shot+".control_repeat.png";report.bands.controlDriftFile=shot+".control_drift.png";
            } catch(Exception e) {report.bands=new Bands {status="FAIL",reason=e.Message,fairwayCapStatus="FAIL",greenCapStatus="FAIL"};}
            finally {if(fw)UnityEngine.Object.DestroyImmediate(fw);if(gr)UnityEngine.Object.DestroyImmediate(gr);}
            File.WriteAllText(Path.Combine(outDir,shot+".v3_frame_proof.json"),JsonUtility.ToJson(report,true)+"\n");
            Debug.Log("[PostcardV3FrameProof] "+shot+" costs="+report.costs.status+" cliff="+report.cliff.status+" bands="+report.bands.status);
            // tex/rt are restored to the original photo for callers which subsequently write or analyze them.
            Render(cam,rt,tex);
        }
    }
}
