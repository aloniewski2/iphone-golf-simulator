// Catalog-wide evidence through real automatic frames; no mesh estimates substitute for counters.
// Real frame-published render counters: a sole automatic Game camera, no Camera.Render().
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Profiling;
using GolfArcade.Course;
using GolfArcade.Game;

namespace GolfArcade.EditorTools
{
    [InitializeOnLoad]
    public static class VisualOverhaulFrameCosts
    {
        const string Flag="VisualOverhaulFrameCosts";const int W=900,H=1600;
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        [Serializable] sealed class Config {public Shot[] shots;}
        [Serializable] sealed class Shot {public string name,kind;public int hole;public float[] ball,aim,pos,look;public float fov;public bool putting;}
        [Serializable] sealed class Event {public int frame,targetBegin,targetEnd,otherCameraEvents;public string[] otherCameras;}
        [Serializable] sealed class Raw {public bool valid;public long triangles,drawCalls,setPass;}
        [Serializable] sealed class Sample {public int readInPlayerFrame,completedCameraFrame,renderedFrameCount,triangleSamples,drawSamples,setPassSamples;public Raw profiler,unityStats;public Event cameras;}
        [Serializable] sealed class Report
        {
            public string unityProject;public string tool="VisualOverhaulFrameCosts",shot,unity,graphicsAPI,pipeline,status="FAIL",reason,drawCallGate="FAIL",photo;
            public string scope="Play mode, sole automatically rendered Game camera, 900x1600 target; frame-published ProfilerRecorder; includes shadow/render passes; Editor Metal only";
            public int hole,width=W,height=H,automaticFrames,activeModelRenderers,waitedPlayerFrames;public bool teeDrawCallCapApplies;
            public float[] cameraPosition,cameraRotation;public float fov;
            public float gameTime,timeScale;
            public long triangles,drawCalls,setPass;public Sample[] samples;
        }
        enum State {Waiting,Loading,Sampling,Completed}
        static State state;static string outDir;static Shot[] shots;static int index,currentHole=-1,warm,lastLoadFrame,warmFrames,lastSampleFrame=-1,lastCount=-1,lastRenderFrame=-1;
        static int stageApplied=-1;static int automaticFrames;static float deadline;static bool recordersOpen,hadFailure;static string isolationError;static Action restoreWind;
        static float savedTimeScale;static bool timeScaleCaptured;
        static Camera cam;static CameraRig rig;static GolfGame game;static RenderTexture rt,previousTarget;static Texture2D tex;
        static Camera[] otherCameras;static bool[] cameraEnabled;static bool previousEnabled;
        static ProfilerRecorder tr,dc,sp;static readonly List<Sample> samples=new();static readonly Dictionary<int,Event> events=new();
        static readonly Dictionary<int,List<string>> otherNames=new();
        static VisualOverhaulFrameCosts() {EditorApplication.update+=Tick;RenderPipelineManager.beginCameraRendering+=BeginCamera;RenderPipelineManager.endCameraRendering+=EndCamera;}
        public static void Run()
        {
            var output=Environment.GetEnvironmentVariable("GOLF_COST_OUT");
            if(string.IsNullOrWhiteSpace(output))throw new ArgumentException("GOLF_COST_OUT required");
            output=Path.GetFullPath(output);Directory.CreateDirectory(output);
            var selected=Environment.GetEnvironmentVariable("GOLF_COST_HOLES");
            var numbers=string.IsNullOrWhiteSpace(selected)?null:selected.Split(',').Select(int.Parse).ToArray();
            var catalog=GolfArcade.Course.Course.All().SelectMany(c=>c.Holes).Concat(GolfArcade.Course.Course.Meadow().Holes).Where(h=>numbers==null||numbers.Contains(h.Number));
            var cfg=new Config{shots=catalog.Select(h=>new Shot{name="hole"+h.Number.ToString("00")+"_tee",kind="tee",hole=h.Number,
                ball=new[]{(float)h.Tee.X,(float)h.Tee.D},aim=new[]{(float)h.RecommendedTarget(h.Tee).X,(float)h.RecommendedTarget(h.Tee).D},fov=60}).ToArray()};
            string config=Path.Combine(output,"capture-config.json");File.WriteAllText(config,JsonUtility.ToJson(cfg,true));
            foreach(SceneView view in SceneView.sceneViews.ToArray())view.Close();
            var gameViewType=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("UnityEditor.GameView")).First(t=>t!=null);
            var gameView=EditorWindow.GetWindow(gameViewType);gameView.Show();gameView.Focus();
            Directory.CreateDirectory(output);SessionState.SetString(Flag+"out",output);SessionState.SetString(Flag+"cfg",Path.GetFullPath(config));SessionState.SetBool(Flag,true);
            EditorSceneManager.OpenScene("Assets/Scenes/Golf.unity");EditorApplication.isPlaying=true;
        }
        static void Init()
        {
            outDir=SessionState.GetString(Flag+"out","");var cfg=JsonUtility.FromJson<Config>(File.ReadAllText(SessionState.GetString(Flag+"cfg","")));
            if(cfg?.shots==null||cfg.shots.Length==0)throw new InvalidOperationException("No cost shots");
            shots=cfg.shots.OrderBy(s=>s.hole).ToArray();
            foreach(var s in shots)if(ResolveHole(s.hole)==null||string.IsNullOrEmpty(s.name)||Path.GetFileName(s.name)!=s.name||s.ball==null||s.ball.Length<2||s.aim==null||s.aim.Length<2||s.kind=="landmark")
                throw new InvalidOperationException("Costs require valid catalog address shots with ball/aim and simple names; use the exact gameplay shot config");
            game=UnityEngine.Object.FindFirstObjectByType<GolfGame>();rig=UnityEngine.Object.FindFirstObjectByType<CameraRig>();cam=rig?rig.GetComponentInChildren<Camera>():Camera.main;
            if(!game||!rig||!cam)throw new InvalidOperationException("GolfGame/CameraRig/Game camera missing");
            game.ChooseHoles(0);game.Play();cam.aspect=W/(float)H;
            previousEnabled=cam.enabled;previousTarget=cam.targetTexture;
            var savedWind=GolfWindSway.ForcedWind;var savedTime=GolfWindSway.FreezeTime;var savedGlobal=Shader.GetGlobalVector("_GolfWind");
            restoreWind=()=>{GolfWindSway.ForcedWind=savedWind;GolfWindSway.FreezeTime=savedTime;Shader.SetGlobalVector("_GolfWind",savedGlobal);};
            string sway=Environment.GetEnvironmentVariable("GOLF_STILLS_SWAY");
            if(!string.IsNullOrWhiteSpace(sway)) {var f=sway.Split(',').Select(v=>float.Parse(v,System.Globalization.CultureInfo.InvariantCulture)).ToArray();if(f.Length!=3)throw new InvalidOperationException("GOLF_STILLS_SWAY needs mph,deg,seconds");GolfWindSway.ForcedWind=new Wind(f[0],f[1]);GolfWindSway.FreezeTime=f[2];GolfWindSway.Push();}
            if(!Application.isPlaying)throw new InvalidOperationException("Frame-cost proof clock requires actual Play mode");
            savedTimeScale=Time.timeScale;timeScaleCaptured=true;Time.timeScale=0;
            Debug.Log("[FrameCosts] proof clock frozen at Time.time="+Time.time+"; previous timeScale="+savedTimeScale+"; automatic player/rendered-frame counters still advance");
            foreach(var c in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))c.enabled=false;
            rt=new RenderTexture(W,H,24,RenderTextureFormat.ARGB32) {antiAliasing=4};rt.Create();tex=new Texture2D(W,H,TextureFormat.RGB24,false);
            VisualOverhaulCostDriver.Install();
            index=0;currentHole=-1;stageApplied=-1;state=State.Loading;deadline=Time.realtimeSinceStartup+30;
        }
        static bool Settled() => Time.frameCount-lastLoadFrame>=5&&UnityEngine.Object.FindObjectsByType<HoleView>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length==1;
        static void StartSampling(Shot s)
        {
            var golfer=(GolferView)typeof(GolfGame).GetField("golfer",Private).GetValue(game);if(golfer)golfer.SetVisible(false);
            var line=(LineRenderer)typeof(GolfGame).GetField("aimLine",Private).GetValue(game);if(line)line.enabled=false;
            var marker=(Transform)typeof(GolfGame).GetField("landingMarker",Private).GetValue(game);if(marker)marker.gameObject.SetActive(false);
            var ball=(Transform)typeof(GolfGame).GetField("ball",Private).GetValue(game);if(!ball)throw new InvalidOperationException("Actual golf ball missing");
            var point=new CoursePoint(s.ball[0],s.ball[1]);ball.gameObject.SetActive(true);ball.position=HoleView.ToWorld(point,.06);
            var aim=HoleView.ToWorld(new CoursePoint(s.aim[0],s.aim[1]),0)-HoleView.ToWorld(point,0);aim.y=0;aim.Normalize();
            rig.FrameAddress(ball.position,aim,s.putting);rig.SnapNext();rig.ApplyFrame();game.enabled=false;rig.enabled=false;cam.fieldOfView=s.fov>1?s.fov:60;
            otherCameras=UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Where(c=>c!=cam).ToArray();cameraEnabled=otherCameras.Select(c=>c.enabled).ToArray();foreach(var c in otherCameras)c.enabled=false;
            cam.targetTexture=rt;cam.enabled=true;
            // StartNew actually enables collection and keeps per-frame samples. Capacity0
            // CurrentValue snapshots failed to publish in the initial synchronous proof.
            tr=ProfilerRecorder.StartNew(ProfilerCategory.Render,"Triangles Count",16);
            dc=ProfilerRecorder.StartNew(ProfilerCategory.Render,"Draw Calls Count",16);
            sp=ProfilerRecorder.StartNew(ProfilerCategory.Render,"SetPass Calls Count",16);recordersOpen=true;
            samples.Clear();events.Clear();otherNames.Clear();warmFrames=0;automaticFrames=0;lastSampleFrame=-1;lastCount=-1;lastRenderFrame=-1;isolationError=null;
            state=State.Sampling;deadline=Time.realtimeSinceStartup+30;
        }
        static Event EventFor(int frame)
        {
            if(!events.TryGetValue(frame,out var e)) {e=new Event {frame=frame};events[frame]=e;}return e;
        }
        static void BeginCamera(ScriptableRenderContext context,Camera c)
        {
            if(state!=State.Sampling)return;var e=EventFor(Time.frameCount);
            if(c==cam) {if(c.cameraType!=CameraType.Game||c.targetTexture!=rt)isolationError="FAIL: measurement Game camera/900x1600 target changed";e.targetBegin++;}
            else {e.otherCameraEvents++;if(!otherNames.TryGetValue(Time.frameCount,out var names))otherNames[Time.frameCount]=names=new List<string>();names.Add(c.name+"/"+c.cameraType);}
        }
        static void EndCamera(ScriptableRenderContext context,Camera c)
        {
            if(state!=State.Sampling||c!=cam)return;EventFor(Time.frameCount).targetEnd++;lastRenderFrame=Time.frameCount;automaticFrames++;
        }
        static Raw RecorderRaw() => new Raw {valid=tr.Valid&&dc.Valid&&sp.Valid,triangles=tr.Valid?tr.LastValue:0,drawCalls=dc.Valid?dc.LastValue:0,setPass=sp.Valid?sp.LastValue:0};
        static Raw StatsRaw() => new Raw {valid=true,triangles=UnityStats.triangles,drawCalls=UnityStats.drawCalls,setPass=UnityStats.setPassCalls};
        static bool Positive(Raw r) => r.valid&&r.triangles>0&&r.drawCalls>0&&r.setPass>=0;
        static bool Same(Raw a,Raw b) => a.triangles==b.triangles&&a.drawCalls==b.drawCalls&&a.setPass==b.setPass;
        // Called at Update of the NEXT player frame, before that frame's automatic
        // camera rendering. The previous frame's target camera has completed.
        public static void PlayerUpdate()
        {
            if(state!=State.Sampling||!recordersOpen||lastRenderFrame<0||lastRenderFrame>=Time.frameCount||lastSampleFrame==lastRenderFrame)return;
            if(!events.TryGetValue(lastRenderFrame,out var e))return;
            lastSampleFrame=lastRenderFrame;warmFrames++;
            if(warmFrames<=3)return;
            int count=tr.Valid?tr.Count:0;
            if(count<=lastCount)return;lastCount=count;
            var sample=new Sample {readInPlayerFrame=Time.frameCount,completedCameraFrame=lastRenderFrame,renderedFrameCount=Time.renderedFrameCount,
                triangleSamples=count,drawSamples=dc.Valid?dc.Count:0,setPassSamples=sp.Valid?sp.Count:0,profiler=RecorderRaw(),unityStats=StatsRaw(),cameras=e};
            e.otherCameras=otherNames.TryGetValue(e.frame,out var names)?names.ToArray():Array.Empty<string>();samples.Add(sample);
            if(samples.Count==3) {cam.enabled=false;state=State.Completed;}
        }
        static void CloseRecorders() {if(recordersOpen){tr.Dispose();dc.Dispose();sp.Dispose();recordersOpen=false;}}
        static void RestoreCameras()
        {
            if(cam){cam.targetTexture=previousTarget;cam.enabled=previousEnabled;}
            if(otherCameras!=null)for(int i=0;i<otherCameras.Length;i++)if(otherCameras[i])otherCameras[i].enabled=cameraEnabled[i];
            otherCameras=null;cameraEnabled=null;
        }
        static void Write(Shot s,string forcedReason=null)
        {
            var pipeline=QualitySettings.renderPipeline?QualitySettings.renderPipeline:GraphicsSettings.currentRenderPipeline;
            // Authored routes nest under Course model; procedural Meadow routes
            // render their real terrain directly under HoleView.
            var model=HoleView.Current?HoleView.Current.transform:null;
            var hole=ResolveHole(s.hole);
            bool tee=s.ball[0]==(float)hole.Tee.X&&s.ball[1]==(float)hole.Tee.D;
            var r=new Report {unityProject=Directory.GetParent(Application.dataPath).FullName,shot=s.name,hole=s.hole,unity=Application.unityVersion,graphicsAPI=SystemInfo.graphicsDeviceType.ToString(),pipeline=pipeline?pipeline.name:"none",
                gameTime=Time.time,timeScale=Time.timeScale,
                teeDrawCallCapApplies=tee,drawCallGate=tee?"FAIL":null,
                fov=cam.fieldOfView,cameraPosition=new[]{cam.transform.position.x,cam.transform.position.y,cam.transform.position.z},cameraRotation=new[]{cam.transform.eulerAngles.x,cam.transform.eulerAngles.y,cam.transform.eulerAngles.z},
                automaticFrames=automaticFrames,waitedPlayerFrames=Time.frameCount-lastLoadFrame,activeModelRenderers=model?model.GetComponentsInChildren<Renderer>(false).Count(x=>x.enabled&&x.gameObject.activeInHierarchy):0,samples=samples.ToArray()};
            bool valid=forcedReason==null&&samples.Count==3&&r.activeModelRenderers>0&&samples.All(x=>Positive(x.profiler)&&x.triangleSamples>0&&x.drawSamples>0&&x.setPassSamples>0&&
                x.cameras.targetBegin==1&&x.cameras.targetEnd==1&&x.cameras.otherCameraEvents==0&&x.completedCameraFrame==x.readInPlayerFrame-1)&&
                samples.Skip(1).All(x=>Same(x.profiler,samples[0].profiler))&&samples.All(x=>!Positive(x.unityStats)||Same(x.unityStats,x.profiler));
            valid=valid&&samples.Skip(1).Select((x,i)=>x.completedCameraFrame==samples[i].completedCameraFrame+1&&x.triangleSamples==samples[i].triangleSamples+1&&x.drawSamples==samples[i].drawSamples+1&&x.setPassSamples==samples[i].setPassSamples+1).All(v=>v);
            if(valid) {var v=samples[0].profiler;r.status="PASS";r.triangles=v.triangles;r.drawCalls=v.drawCalls;r.setPass=v.setPass;if(tee)r.drawCallGate=v.drawCalls<=250?"PASS":"FAIL";
                r.reason="Three consecutive completed sole-camera automatic frames; positive published render counters agree and are stable; any positive UnityStats source also agrees.";}
            else r.reason=forcedReason??"Frame publication/isolation/positive counter support/stability could not be proven. Raw samples retained; no renderer or submesh estimate substituted.";
            if(r.status!="PASS"||(tee&&r.drawCallGate!="PASS"))hadFailure=true;
            CloseRecorders();cam.enabled=false;
            // Read the already automatically rendered target. Never render manually.
            var active=RenderTexture.active;try {RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,W,H),0,0);tex.Apply(false);r.photo=s.name+".cost_frame.png";File.WriteAllBytes(Path.Combine(outDir,r.photo),tex.EncodeToPNG());}finally {RenderTexture.active=active;}
            File.WriteAllText(Path.Combine(outDir,s.name+".frame_costs.json"),JsonUtility.ToJson(r,true)+"\n");Debug.Log("[FrameCosts] "+s.name+" measurement="+r.status+" drawcalls="+r.drawCalls+" cap="+r.drawCallGate+" SetPass="+r.setPass+"; "+r.reason);
            RestoreCameras();index++;state=State.Loading;deadline=Time.realtimeSinceStartup+30;
        }
        static void Tick()
        {
            if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying)return;
            try {
                if(shots==null) {if(!HoleView.Current||++warm<90)return;Init();}
                if(index>=shots.Length) {Finish(hadFailure?1:0);return;}
                var s=shots[index];
                if(state==State.Loading) {
                    if(s.hole!=currentHole) {game.enabled=true;rig.enabled=true;lastLoadFrame=Time.frameCount;
                        if(s.hole is >=1 and <=3)typeof(GolfGame).GetField("course",Private).SetValue(game,GolfArcade.Course.Course.Meadow());
                        game.JumpToHole(s.hole);currentHole=s.hole;deadline=Time.realtimeSinceStartup+30;return;}
                    if(!Settled()) {if(Time.realtimeSinceStartup>deadline)throw new InvalidOperationException("Hole did not settle before cost capture");return;}
                    StartSampling(s);return;
                }
                if(state==State.Completed) {Write(s);return;}
                if(state==State.Sampling&&isolationError!=null) {Write(s,isolationError);return;}
                if(state==State.Sampling&&Time.realtimeSinceStartup>deadline) {Write(s,"FAIL: automatic sole Game-camera frames / frame-published counter samples did not arrive within30s. Batchmode automatic rendering may be unsupported; unavailable metrics are not zero-cost PASS.");}
            } catch(Exception e) {Debug.LogException(e);hadFailure=true;Finish(1);}
        }
        static Hole ResolveHole(int number) => (GolfArcade.Course.Course.Containing(number) ?? (number is >=1 and <=3 ? GolfArcade.Course.Course.Meadow() : null))?.Holes.FirstOrDefault(h=>h.Number==number);
        static void Finish(int code)
        {
            state=State.Waiting;
            try {VisualOverhaulCostDriver.Restore();restoreWind?.Invoke();restoreWind=null;
                CloseRecorders();RestoreCameras();if(rt){rt.Release();UnityEngine.Object.DestroyImmediate(rt);}if(tex)UnityEngine.Object.DestroyImmediate(tex);}
            catch(Exception e){Debug.LogException(e);code=1;}
            finally {if(timeScaleCaptured){Time.timeScale=savedTimeScale;timeScaleCaptured=false;}SessionState.SetBool(Flag,false);}
            EditorApplication.Exit(code);
        }
    }
}
