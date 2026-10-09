#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GolfArcade.Tennis;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Unity.Profiling;
using Object=UnityEngine.Object;

namespace GolfArcade.EditorTools
{
    /// Completed automatic Game-camera frames, observed at the following Player.Update.
    /// The same frozen production scene is measured with spectator instancing off/on.
    /// No Camera.Render, mesh-count estimate, or CPU timing is used as GPU evidence.
    [InitializeOnLoad] public static class TennisRenderBreakdown
    {
        const string Flag="TennisRenderBreakdown";const int W=960,H=540;
        enum State {Waiting,Settling,Sampling,Completed}
        [Serializable] sealed class RendererRecord
        {public string name,path,type;public bool enabled,visible;public string shadows;public string[] materials;public int sourceTriangles;public Vector3 boundsCentre,boundsSize;public string note="Mesh index inventory only; actual phase differences are the GPU-submitted automatic-frame counters.";}
        static string PathOf(Transform t){string p=t.name;while(t.parent){t=t.parent;p=t.name+"/"+p;}return p;}
        static bool IsTree(Renderer r)=>r.name.StartsWith("Tennis trees ",StringComparison.Ordinal);
        static bool IsUnderstory(Renderer r)=>r.name.StartsWith("Tennis foliage ",StringComparison.Ordinal);
        static bool IsSeated(Renderer r)=>PathOf(r.transform).Contains("Seated sporting fan ");
        static bool IsWalker(Renderer r)=>PathOf(r.transform).Contains("Resort walker ")||PathOf(r.transform).Contains("Premium promenade visitor ");
        static bool IsHero(Renderer r)=>r.GetComponentInParent<MatchHeroLook>();
        static bool IsFaceString(Renderer r)=>IsHero(r)&&(r.name.StartsWith("Face",StringComparison.Ordinal)||r.name.IndexOf("StringBed",StringComparison.OrdinalIgnoreCase)>=0||r.sharedMaterials.Any(m=>m&&m.name.IndexOf("String",StringComparison.OrdinalIgnoreCase)>=0));
        static bool IsOriginal(Renderer r)=>r.sharedMaterials.Any(m=>m&&(m.name.StartsWith("TropicalV3_")||m.name=="Resort sea"));
        static bool IsCoastCraft(Renderer r){var p=PathOf(r.transform);return !IsTree(r)&&!IsUnderstory(r)&&(p.Contains("Authored resort coast")||p.Contains("Premium tennis craft"));}
        static void RestoreRenderStates()
        {
            if(urp)urp.shadowCascadeCount=savedCascadeCount;
            if(sceneRenderers!=null)for(int i=0;i<sceneRenderers.Length;i++)if(sceneRenderers[i]){sceneRenderers[i].enabled=rendererEnabled[i];sceneRenderers[i].shadowCastingMode=rendererShadows[i];}
            if(sceneLights!=null)for(int i=0;i<sceneLights.Length;i++)if(sceneLights[i])sceneLights[i].shadows=lightShadows[i];
            if(garments!=null)foreach(var g in garments)if(g)g.enabled=true;
            if(seatedCrowd)seatedCrowd.SuppressRendering=false;
        }
        static void ApplyPhase()
        {
            RestoreRenderStates();string label=Phases[phase];
            foreach(var m in spectatorMaterials)m.enableInstancing=true;if(seatedCrowd)seatedCrowd.SetInstancedSubmission(true);
            if(label=="all_shadows_off")foreach(var l in sceneLights)l.shadows=LightShadows.None;
            if(label=="one_shadow_cascade"&&urp)urp.shadowCascadeCount=1;
            if(label=="without_seated_fans"&&seatedCrowd)seatedCrowd.SuppressRendering=true;
            if(label=="without_main_heroes")foreach(var g in garments)g.enabled=false;
            foreach(var r in sceneRenderers)
            {
                bool shadow=label=="face_string_shadows_off"&&IsFaceString(r)||label=="tree_shadows_off"&&IsTree(r)||label=="understory_shadows_off"&&IsUnderstory(r)||label=="crowd_shadows_off"&&(IsSeated(r)||IsWalker(r));
                if(shadow)r.shadowCastingMode=ShadowCastingMode.Off;
                bool hidden=label=="without_trees"&&IsTree(r)||label=="without_understory"&&IsUnderstory(r)||label=="without_original_arena"&&IsOriginal(r)||label=="without_coast_and_craft"&&IsCoastCraft(r)||label=="without_legacy_walkers"&&IsWalker(r)||label=="without_seated_fans"&&IsSeated(r)||label=="without_main_heroes"&&IsHero(r);
                if(hidden)r.enabled=false;
            }
        }
        [Serializable] sealed class CameraEvent {public int frame,targetBegin,targetEnd,otherCameraEvents;public string[] otherCameras;}
        [Serializable] sealed class Counters {public bool valid;public long triangles,drawCalls,setPass;}
        [Serializable] sealed class Sample {public int readInPlayerFrame,completedCameraFrame,renderedFrameCount,triangleSamples,drawSamples,setPassSamples;public Counters profiler,unityStats;public CameraEvent cameras;}
        [Serializable] sealed class Report
        {
            public string tool="TennisRenderBreakdown",venue,phase,status="FAIL",reason,photo,unityProject,unity,graphicsAPI,pipeline;
            public string scope="Frozen production scene, sole automatically rendered GameplayCamera960x540. Each named diagnostic phase subtracts its actual renderers/shadow passes. Counter differences are observed submissions, are not additive per-mesh estimates, and are not device FPS.";
            public long baselineMinusTriangles,baselineMinusDraws,baselineMinusSetPass;public RendererRecord[] visibleRenderers;
            public int width=W,height=H,mainHeroes,seatedFans,promenadeVisitors,explicitSeatedGroups,activeWorldRenderers,lodGroups,automaticFrames,shadowCascades;public TennisNpcInventory.Report npcInventory;public bool spectatorInstancing;
            public float gameTime,timeScale,fov;public float[] cameraPosition,cameraRotation;public long triangles,drawCalls,setPass;public Sample[] samples;
        }
        static readonly string[] Phases=PhaseList();
        static string[] PhaseList()
        {
            var only=Environment.GetEnvironmentVariable("TCB_ONLY");
            return string.IsNullOrWhiteSpace(only)?new[]{"baseline","one_shadow_cascade","face_string_shadows_off","all_shadows_off","tree_shadows_off","understory_shadows_off","crowd_shadows_off","without_trees","without_understory","without_original_arena","without_coast_and_craft","without_legacy_walkers","without_seated_fans","without_main_heroes"}:new[]{"baseline"}.Concat(only.Split(',').Where(s=>s!="baseline")).ToArray();
        }
        static UniversalRenderPipelineAsset urp;static int savedCascadeCount;
        static Renderer[] sceneRenderers;static bool[] rendererEnabled;static ShadowCastingMode[] rendererShadows;
        static Light[] sceneLights;static LightShadows[] lightShadows;static HeroGarmentLOD[] garments;
        static long baselineTriangles,baselineDraws,baselinePasses;
        static State state;static TennisGame game;static Camera cam;static RenderTexture rt,previousTarget;static Texture2D image;
        static string outDir;static int phase,settleFrame,warmFrames,lastSampleFrame=-1,lastRenderFrame=-1,lastCount=-1,automaticFrames;static float deadline;
        static bool configured,recordersOpen,hadFailure,previousCameraEnabled,clockSaved;static float savedTimeScale;
        static Camera[] otherCameras;static bool[] cameraEnabled;
        static MonoBehaviour[] stopped;static Canvas[] canvases;static bool[] canvasEnabled;
        static TennisStandsCrowd seatedCrowd;static bool savedExplicitSubmission;
        static Material[] spectatorMaterials;static bool[] priorInstancing;
        static ProfilerRecorder triangles,draws,passes;static readonly List<Sample> samples=new();static readonly Dictionary<int,CameraEvent> events=new();static readonly Dictionary<int,List<string>> otherNames=new();
        static string isolationError;static PlayerLoopSystem originalLoop;static bool loopInstalled;
        static TennisRenderBreakdown(){EditorApplication.update+=Tick;RenderPipelineManager.beginCameraRendering+=BeginCamera;RenderPipelineManager.endCameraRendering+=EndCamera;}
        public static void Run()
        {
            string output=Environment.GetEnvironmentVariable("TCB_OUT");if(string.IsNullOrWhiteSpace(output))throw new ArgumentException("TCB_OUT required");
            output=Path.GetFullPath(output);Directory.CreateDirectory(output);
            foreach(SceneView view in SceneView.sceneViews.ToArray())view.Close();
            var gameViewType=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("UnityEditor.GameView")).First(t=>t!=null);
            var viewWindow=EditorWindow.GetWindow(gameViewType);viewWindow.Show();viewWindow.Focus();
            SessionState.SetString(Flag+"out",output);SessionState.SetBool(Flag,true);EditorSceneManager.OpenScene("Assets/Scenes/Tennis.unity");EditorApplication.isPlaying=true;
        }
        static void Configure()
        {
            outDir=SessionState.GetString(Flag+"out","");game.SelectCharacter(false);game.ConfigureMatch(TennisGame.Mode.Campaign,"Nadia","Nadia","FRAME COST PROOF");
            var presentation=game.GetComponent<TennisPresentation>();if(presentation)presentation.Finish();game.AutoPlay=false;
            configured=true;settleFrame=Time.frameCount;state=State.Settling;deadline=Time.realtimeSinceStartup+30;
        }
        static void FreezeScene()
        {
            urp=(QualitySettings.renderPipeline?QualitySettings.renderPipeline:GraphicsSettings.currentRenderPipeline) as UniversalRenderPipelineAsset;if(urp)savedCascadeCount=urp.shadowCascadeCount;
            cam=game.GameplayCamera;if(!cam||!game.Player||!game.Opponent)throw new InvalidOperationException("Production gameplay camera or main actors missing");
            if(!game.Player.GetComponentInChildren<MatchHeroLook>()||!game.Opponent.GetComponentInChildren<MatchHeroLook>())throw new InvalidOperationException("Both main Generic heroes required");
            int fanCount=Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Count(t=>t.name.StartsWith("Seated sporting fan "));
            if(TennisVenue.IsResort&&fanCount!=36)throw new InvalidOperationException("Resort requires all36 production seated fans, found "+fanCount);
            seatedCrowd=game.GetComponent<TennisStandsCrowd>();savedExplicitSubmission=seatedCrowd&&seatedCrowd.InstancedSubmission;
            var inventory=TennisNpcInventory.Inspect();Debug.Log("[TennisRenderBreakdown] independent NPC visual gate "+inventory.status+"; seated="+inventory.seatedFans+" promenade="+inventory.promenadeVisitors+" legacyWalkers="+inventory.legacyWalkerObjects);
            previousTarget=cam.targetTexture;previousCameraEnabled=cam.enabled;cam.aspect=W/(float)H;
            foreach(var lod in Object.FindObjectsByType<HeroGarmentLOD>(FindObjectsSortMode.None))lod.Prepare(cam);
            foreach(var skin in Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsSortMode.None))skin.updateWhenOffscreen=true;
            savedTimeScale=Time.timeScale;clockSaved=true;Time.timeScale=0;
            // Ordinary engine LODGroups, frustum culling and shadow passes still run.
            // Pose/camera scripts stop at the settled gameplay pose; garment camera LOD remains active.
            stopped=Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).Where(b=>b.enabled&&!(b is HeroGarmentLOD)&&!(b is TennisStandsCrowd)).ToArray();foreach(var b in stopped)b.enabled=false;
            canvases=Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);canvasEnabled=canvases.Select(c=>c.enabled).ToArray();foreach(var c in canvases)c.enabled=false;
            otherCameras=Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Where(c=>c!=cam).ToArray();cameraEnabled=otherCameras.Select(c=>c.enabled).ToArray();foreach(var c in otherCameras)c.enabled=false;
            spectatorMaterials=Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).SelectMany(r=>r.sharedMaterials).Where(m=>m&&m.shader&&m.shader.name=="GolfArcade/TennisSpectator").Distinct().ToArray();priorInstancing=spectatorMaterials.Select(m=>m.enableInstancing).ToArray();
            rt=new RenderTexture(W,H,24,RenderTextureFormat.ARGB32){antiAliasing=4};rt.Create();image=new Texture2D(W,H,TextureFormat.RGB24,false);cam.targetTexture=rt;
            originalLoop=PlayerLoop.GetCurrentPlayerLoop();var changed=Copy(originalLoop);if(!Insert(ref changed))throw new InvalidOperationException("Actual Player.Update loop missing");PlayerLoop.SetPlayerLoop(changed);loopInstalled=true;
            sceneRenderers=Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);rendererEnabled=sceneRenderers.Select(r=>r.enabled).ToArray();rendererShadows=sceneRenderers.Select(r=>r.shadowCastingMode).ToArray();
            sceneLights=Object.FindObjectsByType<Light>(FindObjectsSortMode.None);lightShadows=sceneLights.Select(l=>l.shadows).ToArray();garments=Object.FindObjectsByType<HeroGarmentLOD>(FindObjectsSortMode.None);
            phase=0;StartSampling();
        }
        static void StartSampling()
        {
            ApplyPhase();
            triangles=ProfilerRecorder.StartNew(ProfilerCategory.Render,"Triangles Count",16);draws=ProfilerRecorder.StartNew(ProfilerCategory.Render,"Draw Calls Count",16);passes=ProfilerRecorder.StartNew(ProfilerCategory.Render,"SetPass Calls Count",16);recordersOpen=true;
            samples.Clear();events.Clear();otherNames.Clear();warmFrames=automaticFrames=0;lastSampleFrame=lastRenderFrame=lastCount=-1;isolationError=null;state=State.Sampling;deadline=Time.realtimeSinceStartup+30;cam.enabled=true;
        }
        static CameraEvent EventFor(int frame){if(!events.TryGetValue(frame,out var e)){e=new CameraEvent{frame=frame};events[frame]=e;}return e;}
        static void BeginCamera(ScriptableRenderContext context,Camera c)
        {
            if(state!=State.Sampling)return;var e=EventFor(Time.frameCount);
            if(c==cam){if(c.cameraType!=CameraType.Game||c.targetTexture!=rt)isolationError="Measurement camera/target changed";e.targetBegin++;}
            else{e.otherCameraEvents++;if(!otherNames.TryGetValue(Time.frameCount,out var names))otherNames[Time.frameCount]=names=new List<string>();names.Add(c.name+"/"+c.cameraType);}
        }
        static void EndCamera(ScriptableRenderContext context,Camera c){if(state!=State.Sampling||c!=cam)return;EventFor(Time.frameCount).targetEnd++;lastRenderFrame=Time.frameCount;automaticFrames++;}
        static Counters RecorderRaw()=>new Counters{valid=triangles.Valid&&draws.Valid&&passes.Valid,triangles=triangles.Valid?triangles.LastValue:0,drawCalls=draws.Valid?draws.LastValue:0,setPass=passes.Valid?passes.LastValue:0};
        static Counters StatsRaw()=>new Counters{valid=true,triangles=UnityStats.triangles,drawCalls=UnityStats.drawCalls,setPass=UnityStats.setPassCalls};
        static bool Positive(Counters c)=>c.valid&&c.triangles>0&&c.drawCalls>0&&c.setPass>=0;
        static bool Same(Counters a,Counters b)=>a.triangles==b.triangles&&a.drawCalls==b.drawCalls&&a.setPass==b.setPass;
        public static void PlayerUpdate()
        {
            if(state!=State.Sampling||!recordersOpen||lastRenderFrame<0||lastRenderFrame>=Time.frameCount||lastSampleFrame==lastRenderFrame)return;
            if(!events.TryGetValue(lastRenderFrame,out var e))return;lastSampleFrame=lastRenderFrame;if(++warmFrames<=3)return;
            int count=triangles.Valid?triangles.Count:0;if(count<=lastCount)return;lastCount=count;
            e.otherCameras=otherNames.TryGetValue(e.frame,out var names)?names.ToArray():Array.Empty<string>();
            samples.Add(new Sample{readInPlayerFrame=Time.frameCount,completedCameraFrame=lastRenderFrame,renderedFrameCount=Time.renderedFrameCount,triangleSamples=count,drawSamples=draws.Valid?draws.Count:0,setPassSamples=passes.Valid?passes.Count:0,profiler=RecorderRaw(),unityStats=StatsRaw(),cameras=e});
            if(samples.Count==3){cam.enabled=false;state=State.Completed;}
        }
        static void CloseRecorders(){if(recordersOpen){triangles.Dispose();draws.Dispose();passes.Dispose();recordersOpen=false;}}
        static void Write(string forcedReason=null)
        {
            var pipeline=QualitySettings.renderPipeline?QualitySettings.renderPipeline:GraphicsSettings.currentRenderPipeline;
            string venue=TennisVenue.Current.ToString().ToLowerInvariant(),label=venue+"_"+Phases[phase];
            var r=new Report{venue=venue,phase=Phases[phase],spectatorInstancing=true,unityProject=Directory.GetParent(Application.dataPath).FullName,unity=Application.unityVersion,graphicsAPI=SystemInfo.graphicsDeviceType.ToString(),pipeline=pipeline?pipeline.name:"none",mainHeroes=2,seatedFans=Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Count(t=>t.name.StartsWith("Seated sporting fan ")),activeWorldRenderers=Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).Count(x=>x.enabled&&x.gameObject.activeInHierarchy),lodGroups=Object.FindObjectsByType<LODGroup>(FindObjectsSortMode.None).Length,automaticFrames=automaticFrames,gameTime=Time.time,timeScale=Time.timeScale,fov=cam.fieldOfView,cameraPosition=new[]{cam.transform.position.x,cam.transform.position.y,cam.transform.position.z},cameraRotation=new[]{cam.transform.eulerAngles.x,cam.transform.eulerAngles.y,cam.transform.eulerAngles.z},samples=samples.ToArray()};
            r.visibleRenderers=sceneRenderers.Where(x=>x&&x.enabled&&x.gameObject.activeInHierarchy&&x.isVisible).Select(x=>{
                var filter=x.GetComponent<MeshFilter>();var mesh=x is SkinnedMeshRenderer skinned?skinned.sharedMesh:filter?filter.sharedMesh:null;int count=0;if(mesh)for(int sub=0;sub<mesh.subMeshCount;sub++)count+=(int)mesh.GetIndexCount(sub)/3;
                return new RendererRecord{name=x.name,path=PathOf(x.transform),type=x.GetType().Name,enabled=x.enabled,visible=x.isVisible,shadows=x.shadowCastingMode.ToString(),materials=x.sharedMaterials.Select(m=>m?m.name:"null").ToArray(),sourceTriangles=count,boundsCentre=x.bounds.center,boundsSize=x.bounds.size};}).ToArray();
            r.shadowCascades=urp?urp.shadowCascadeCount:0;
            r.npcInventory=TennisNpcInventory.Save(outDir,label+".npc-inventory");r.promenadeVisitors=r.npcInventory.promenadeVisitors;r.explicitSeatedGroups=r.npcInventory.explicitSeatedGroups;
            bool valid=forcedReason==null&&samples.Count==3&&samples.All(s=>Positive(s.profiler)&&s.triangleSamples>0&&s.drawSamples>0&&s.setPassSamples>0&&s.cameras.targetBegin==1&&s.cameras.targetEnd==1&&s.cameras.otherCameraEvents==0&&s.completedCameraFrame==s.readInPlayerFrame-1)&&samples.Skip(1).All(s=>Same(s.profiler,samples[0].profiler))&&samples.All(s=>Positive(s.unityStats)&&Same(s.unityStats,s.profiler));
            valid=valid&&samples.Skip(1).Select((s,i)=>s.completedCameraFrame==samples[i].completedCameraFrame+1&&s.triangleSamples==samples[i].triangleSamples+1&&s.drawSamples==samples[i].drawSamples+1&&s.setPassSamples==samples[i].setPassSamples+1).All(x=>x);
            if(valid){var c=samples[0].profiler;r.status="PASS";r.triangles=c.triangles;r.drawCalls=c.drawCalls;r.setPass=c.setPass;r.reason="Three consecutive completed sole-camera automatic frames; positive published render counters are stable and exactly agree with UnityStats.";}
            if(valid&&phase==0){baselineTriangles=r.triangles;baselineDraws=r.drawCalls;baselinePasses=r.setPass;}
            r.baselineMinusTriangles=baselineTriangles-r.triangles;r.baselineMinusDraws=baselineDraws-r.drawCalls;r.baselineMinusSetPass=baselinePasses-r.setPass;
            if(!valid){hadFailure=true;r.reason=forcedReason??"Automatic frame publication, sole-camera isolation, positive counters, UnityStats agreement or stability not proven. Raw samples retained; no estimates substituted.";}
            CloseRecorders();cam.enabled=false;
            var active=RenderTexture.active;try{RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,W,H),0,0);image.Apply(false);r.photo=label+".cost_frame.png";File.WriteAllBytes(Path.Combine(outDir,r.photo),image.EncodeToPNG());}finally{RenderTexture.active=active;}
            File.WriteAllText(Path.Combine(outDir,label+".frame_costs.json"),JsonUtility.ToJson(r,true)+"\n");Debug.Log("[TennisRenderBreakdown] "+label+" "+r.status+" triangles="+r.triangles+" draws="+r.drawCalls+" setPass="+r.setPass+"; "+r.reason);
            if(++phase<Phases.Length)StartSampling();else Finish(hadFailure?1:0);
        }
        static void Tick()
        {
            if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying)return;
            try{
                if(!game)game=Object.FindFirstObjectByType<TennisGame>();if(!game||!game.Initialized)return;
                if(!configured){Configure();return;}
                if(state==State.Settling){if(Time.frameCount-settleFrame<35){if(Time.realtimeSinceStartup>deadline)throw new InvalidOperationException("Tennis did not settle");return;}FreezeScene();return;}
                if(state==State.Completed){Write();return;}
                if(state==State.Sampling&&isolationError!=null){Write(isolationError);return;}
                if(state==State.Sampling&&Time.realtimeSinceStartup>deadline)Write("Automatic camera frames / published samples did not arrive within30s. Unsupported or unavailable counters are not zero-cost PASS.");
            }catch(Exception e){Debug.LogException(e);Finish(1);}
        }
        static PlayerLoopSystem Copy(PlayerLoopSystem source){var result=source;if(source.subSystemList!=null){result.subSystemList=new PlayerLoopSystem[source.subSystemList.Length];for(int i=0;i<source.subSystemList.Length;i++)result.subSystemList[i]=Copy(source.subSystemList[i]);}return result;}
        static bool Insert(ref PlayerLoopSystem loop)
        {
            if(loop.type==typeof(UnityEngine.PlayerLoop.Update)){var old=loop.subSystemList??Array.Empty<PlayerLoopSystem>();var next=new PlayerLoopSystem[old.Length+1];next[0]=new PlayerLoopSystem{type=typeof(TennisRenderBreakdown),updateDelegate=PlayerUpdate};Array.Copy(old,0,next,1,old.Length);loop.subSystemList=next;return true;}
            if(loop.subSystemList!=null)for(int i=0;i<loop.subSystemList.Length;i++){var child=loop.subSystemList[i];if(Insert(ref child)){loop.subSystemList[i]=child;return true;}}return false;
        }
        static void Finish(int code)
        {
            state=State.Waiting;try{RestoreRenderStates();if(loopInstalled){PlayerLoop.SetPlayerLoop(originalLoop);loopInstalled=false;}CloseRecorders();if(spectatorMaterials!=null)for(int i=0;i<spectatorMaterials.Length;i++)if(spectatorMaterials[i])spectatorMaterials[i].enableInstancing=priorInstancing[i];if(seatedCrowd)seatedCrowd.SetInstancedSubmission(savedExplicitSubmission);if(stopped!=null)foreach(var b in stopped)if(b)b.enabled=true;if(canvases!=null)for(int i=0;i<canvases.Length;i++)if(canvases[i])canvases[i].enabled=canvasEnabled[i];if(cam){cam.targetTexture=previousTarget;cam.enabled=previousCameraEnabled;}if(otherCameras!=null)for(int i=0;i<otherCameras.Length;i++)if(otherCameras[i])otherCameras[i].enabled=cameraEnabled[i];if(rt){rt.Release();Object.DestroyImmediate(rt);}if(image)Object.DestroyImmediate(image);}
            catch(Exception e){Debug.LogException(e);code=1;}finally{if(clockSaved)Time.timeScale=savedTimeScale;SessionState.SetBool(Flag,false);}EditorApplication.Exit(code);
        }
    }
}
#endif
