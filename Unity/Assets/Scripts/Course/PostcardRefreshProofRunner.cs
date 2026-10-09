#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Unity.Profiling;
using GolfArcade.Game;
using GolfArcade.Course;
using GolfArcade.Tennis;

namespace GolfArcade.EditorTools
{
    [InitializeOnLoad]
    public static class PostcardRefreshProof
    {
        const string Key="PostcardRefreshProof";
        static PostcardRefreshProof(){EditorApplication.update+=Tick;}
        public static void Run(){SessionState.SetBool(Key,true);EditorSceneManager.OpenScene("Assets/Scenes/Golf.unity");EditorApplication.isPlaying=true;}
        static void Tick(){if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying)return;SessionState.SetBool(Key,false);new GameObject("Postcard refresh proof").AddComponent<PostcardRefreshProofRunner>();}
    }
    public sealed class PostcardRefreshProofRunner:MonoBehaviour
    {
        [Serializable] public sealed class Row
        {
            public int hole,drawCalls,triangles,instancedPlantDraws,instancedPlants,grassCells,volumes;
            public string pose;public bool profilePrivate;
            public bool hdr,post,rendererResources,aces,bloom,physicsExact;
            public float fogStart,heroFill;public string gate;
        }
        [Serializable] public sealed class Report {public string scope="Metal Editor, actual gameplay camera with HUD and minimap, completed automatic frames; no phone FPS claim.";public string gate="PASS";public int shoreSamples,shoreMismatches;public List<Row> holes=new();}
        readonly Report report=new();
        int[] holes={9,10,12,16,17,18,21,22,23};
        GolfGame game;CameraRig rig;Camera cam;int index=-1,pose=-1,warm,frame;bool placed,queued;
        ProfilerRecorder draws,triangles;int peakDraw,peakTriangles;string output;
        void Start(){var selection=Environment.GetEnvironmentVariable("POSTCARD_REFRESH_PROOF_HOLES");if(!string.IsNullOrEmpty(selection))holes=Array.ConvertAll(selection.Split(','),int.Parse);output=Environment.GetEnvironmentVariable("POSTCARD_REFRESH_PROOF_OUT");if(string.IsNullOrEmpty(output))throw new Exception("POSTCARD_REFRESH_PROOF_OUT required");Directory.CreateDirectory(output);draws=ProfilerRecorder.StartNew(ProfilerCategory.Render,"Draw Calls Count",32);triangles=ProfilerRecorder.StartNew(ProfilerCategory.Render,"Triangles Count",32);}
        void Update()
        {
            try{
                if(!game){if(++warm<35||!HoleView.Current)return;game=FindFirstObjectByType<GolfGame>();rig=FindFirstObjectByType<CameraRig>();cam=rig.Camera;
                    cam.targetTexture=new RenderTexture(900,1600,24,RenderTextureFormat.ARGB32){antiAliasing=4};cam.aspect=9f/16;cam.enabled=true;
                    foreach(var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None)){canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=cam;canvas.planeDistance=1;}
                    game.Play();Next();return;}
                int elapsed=Time.frameCount-frame;
                if(!placed){
                    if(elapsed<12)return;
                    var hole=game.CurrentHole;
                    var at=pose==0?hole.Tee:pose==1?hole.Centerline[Math.Max(0,hole.Centerline.Length-2)]:new CoursePoint(hole.Pin.X,hole.Pin.D-6);
                    if(pose==1&&hole.Centerline.Length==2){
                        var direction=new Vector2((float)(hole.Pin.X-hole.Tee.X),(float)(hole.Pin.D-hole.Tee.D)).normalized;
                        at=new CoursePoint(hole.Pin.X-direction.x*18,hole.Pin.D-direction.y*18);
                    }
                    if(!queued){game.DropBall(at);queued=true;frame=Time.frameCount;return;}
                    // Let the real game produce its aim controls, map labels and markers
                    // before freezing the camera for the completed-frame measurement.
                    var target=pose==0?hole.RecommendedTarget(at):hole.Pin;
                    rig.FrameAddress(game.BallPosition,new Vector3((float)(target.X-at.X),0,(float)(target.D-at.D)).normalized,pose==2);
                    rig.SnapNext();rig.ApplyFrame();rig.enabled=false;game.enabled=false;
                    placed=true;frame=Time.frameCount;return;
                }
                if(elapsed<6)return;
                peakDraw=Mathf.Max(peakDraw,(int)draws.LastValue);peakTriangles=Mathf.Max(peakTriangles,(int)triangles.LastValue);
                if(elapsed<12)return;
                var renderer=Resources.Load<UniversalRendererData>("Tennis/Rendering/TennisURP_Renderer");var owner=cam.GetComponent<SportsPostProcessing>();var volume=owner?owner.Volume:null;
                bool aces=volume&&volume.profile.TryGet(out Tonemapping tone)&&tone.active&&tone.mode.value==TonemappingMode.ACES;
                bool bloom=volume&&volume.profile.TryGet(out Bloom glow)&&glow.active&&glow.intensity.value>0;
                var turf=HoleView.Current.GetComponentInChildren<GolfCoastalTurf>();
                var row=new Row{hole=holes[index],pose=new[]{"tee","approach","putting"}[pose],drawCalls=peakDraw,triangles=peakTriangles,hdr=cam.allowHDR,post=cam.GetUniversalAdditionalCameraData().renderPostProcessing,rendererResources=renderer&&renderer.postProcessData,
                    aces=aces,bloom=bloom,fogStart=RenderSettings.fogStartDistance,heroFill=Shader.GetGlobalFloat("_HeroPresentationFill"),grassCells=turf?turf.CellCount:0,volumes=cam.GetComponentsInChildren<Volume>().Length};
                foreach(var cluster in HoleView.Current.GetComponentsInChildren<GolfBotanicalInstances>()){row.instancedPlantDraws+=cluster.LastDrawCalls;row.instancedPlants+=cluster.LastInstances;}
                foreach(var cluster in HoleView.Current.GetComponentsInChildren<GolfCoastalInstances>()){row.instancedPlantDraws+=cluster.LastDrawCalls;row.instancedPlants+=cluster.LastInstances;}
                row.profilePrivate=volume&&volume.sharedProfile&&volume.profile!=volume.sharedProfile;
                if(row.profilePrivate)for(int c=0;c<volume.profile.components.Count;c++)
                    row.profilePrivate &= volume.profile.components[c]!=volume.sharedProfile.components[c];
                if(row.hole==9&&pose==0)CheckShore();
                // Captured by the existing before/after dressing witness at the model boundary.
                row.physicsExact=GolfVisualPhysicsGate.Reports.TryGetValue(row.hole,out var physics)&&physics.gate=="PASS";
                if(physics!=null) File.WriteAllText(Path.Combine(output,$"hole{row.hole:00}-physics.json"),JsonUtility.ToJson(physics,true));
                row.gate=draws.Valid&&peakDraw>0&&peakDraw<=250&&row.hdr&&row.post&&row.rendererResources&&aces&&bloom&&row.volumes==1&&row.profilePrivate&&report.shoreMismatches==0&&row.fogStart<=150&&(pose!=0||row.grassCells>0)&&row.physicsExact?"PASS":"FAIL";
                if(row.gate=="FAIL")report.gate="FAIL";report.holes.Add(row);
                var previous=RenderTexture.active;RenderTexture.active=cam.targetTexture;
                var image=new Texture2D(900,1600,TextureFormat.RGB24,false);
                image.ReadPixels(new Rect(0,0,900,1600),0,0);image.Apply();RenderTexture.active=previous;
                File.WriteAllBytes(Path.Combine(output,$"hole{row.hole:00}-{row.pose}.png"),image.EncodeToPNG());Destroy(image);
                File.WriteAllText(Path.Combine(output,"render-gates.json"),JsonUtility.ToJson(report,true));
                Debug.Log($"[PostcardRefreshProof] hole {row.hole} {row.pose} {row.gate}: {row.drawCalls} draws, {row.triangles} triangles, post={row.post}, turf={row.grassCells}");Next();
            }catch(Exception e){Debug.LogException(e);File.WriteAllText(Path.Combine(output,"error.txt"),e.ToString());EditorApplication.Exit(1);}
        }
        void CheckShore()
        {
            var hole=game.CurrentHole;
            for(int z=4;z<125;z+=2)for(int x=-75;x<25;x+=2){
                var point=new CoursePoint(x,z);float edge=float.PositiveInfinity;
                for(int i=0;i<hole.Shore.Length;i++){
                    var a=hole.Shore[i];var b=hole.Shore[(i+1)%hole.Shore.Length];
                    var start=new Vector2((float)a.X,(float)a.D);var line=new Vector2((float)(b.X-a.X),(float)(b.D-a.D));
                    float t=Mathf.Clamp01(Vector2.Dot(new Vector2(x,z)-start,line)/Mathf.Max(.0001f,line.sqrMagnitude));
                    edge=Mathf.Min(edge,(new Vector2(x,z)-(start+line*t)).magnitude);
                }
                if(edge<2)continue; // tessellated coast margins, not playable interior
                report.shoreSamples++;
                bool physicalLand=HoleView.GroundHeight(point)>3;
                if(physicalLand!=hole.OnLand(point))report.shoreMismatches++;
            }
        }
        void Next()
        {
            if(++pose>=3){pose=0;index++;}
            if(index<0)index=0;
            if(index==holes.Length){EditorApplication.Exit(report.gate=="PASS"?0:1);return;}
            game.enabled=true;rig.enabled=true;
            if(pose==0)game.JumpToHole(holes[index]);
            frame=Time.frameCount;placed=queued=false;peakDraw=peakTriangles=0;
        }
        void OnDestroy(){draws.Dispose();triangles.Dispose();}
    }
}
#endif
