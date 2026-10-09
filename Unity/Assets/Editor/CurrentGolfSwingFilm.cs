#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using GolfArcade.Course;
using GolfArcade.Game;
using Unity.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;
namespace GolfArcade.EditorTools
{
    [InitializeOnLoad]
    public static class CurrentGolfSwingFilm
    {
        const string Flag="CurrentGolfSwingFilm.Active";
        static CurrentGolfSwingFilm(){EditorApplication.update+=Boot;}
        public static string Env(string k,string d)=>Environment.GetEnvironmentVariable(k)??d;
        public static void Run(){EditorSceneManager.OpenScene("Assets/Scenes/Golf.unity");SessionState.SetBool(Flag,true);EditorApplication.isPlaying=true;}
        static void Boot(){
            if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying||Object.FindFirstObjectByType<CurrentGolfSwingRecorder>())return;
            var g=Object.FindFirstObjectByType<GolfGame>();if(!g||!HoleView.Current)return;
            try{new GameObject("Current golf swing film").AddComponent<CurrentGolfSwingRecorder>().Begin(g);}catch(Exception e){Debug.LogException(e);End(1);}
        }
        public static void End(int code){SessionState.SetBool(Flag,false);Time.captureFramerate=0;Time.timeScale=1;if(Application.isBatchMode)EditorApplication.Exit(code);else EditorApplication.isPlaying=false;}
    }
    [DefaultExecutionOrder(10000)]
    public sealed class CurrentGolfSwingRecorder:MonoBehaviour
    {
        GolfGame game;Camera cam;string dir,course;int hole,tick,frames,stage,wait,post,flightFrames;bool filming,done,sawFlight,audio;
        int savedHoles,channels,rate;string savedCourse;readonly List<float> sound=new();StreamWriter trace;
        [Serializable]class Report{public string course,source="Live GolfGame.ShowBackswing + StrikeToward; actual game ball physics/camera/HUD",unity,graphics,finalState;public int hole,fps=30,width=1280,height=720,frames,flightSimulationFrames,impactFrame;public bool sawFlight,audioStarted;public float duration,audioSeconds;}
        Report report=new();
        public void Begin(GolfGame g){
            game=g;dir=Path.GetFullPath(CurrentGolfSwingFilm.Env("FILM_OUT","../work/reference-rebuild/gameplay-films31/golf"));Directory.CreateDirectory(dir);
            if(Directory.GetFiles(dir,"f_*.jpg").Length>0)throw new InvalidOperationException("Film output must be fresh: "+dir);
            course=CurrentGolfSwingFilm.Env("FILM_COURSE","cliffside");hole=int.Parse(CurrentGolfSwingFilm.Env("FILM_HOLE","12"));
            savedHoles=PlayerPrefs.GetInt("holes",0);savedCourse=PlayerPrefs.GetString("course","cliffside");
            Time.captureFramerate=60;Time.timeScale=1;UnityEngine.Random.InitState(3108);
            game.Demo=true;game.InstantReplays=false;game.ChooseHoles(0);game.Play();
            if(course=="meadow")typeof(GolfGame).GetField("course",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(game,GolfArcade.Course.Course.Meadow());
            game.JumpToHole(hole);game.DropBall(game.CurrentHole.Tee);
            cam=Camera.main;cam.aspect=1280f/720f;cam.rect=new Rect(0,0,1,1);
            report.course=course;report.hole=hole;report.unity=Application.unityVersion;report.graphics=SystemInfo.graphicsDeviceName;
            trace=new StreamWriter(Path.Combine(dir,"trace.csv"));trace.WriteLine("simulationFrame,videoFrame,time,stage,state,swingPhase,ballX,ballY,ballZ");
        }
        void LateUpdate(){if(done||!game)return;try{Step();}catch(Exception e){Debug.LogException(e);Finish(1);}}
        void Step(){
            cam.aspect=1280f/720f;cam.rect=new Rect(0,0,1,1);
            if(!filming){if(++wait<60||game.Current!=GolfGame.State.Aim)return;filming=true;wait=0;StartAudio();}
            tick++;
            if(stage==0 && tick>=60){stage=1;wait=0;}
            if(stage==1){game.ShowBackswing(.9*Mathf.SmoothStep(0,1,++wait/54f));if(wait>=54){stage=2;wait=0;}}
            else if(stage==2 && ++wait>=12){
                // Plan a genuine playable tee shot; the live game chooses the club,
                // plays the downswing and launches its own computed shot at impact.
                var target=game.GreenTarget(5,18,7);
                if(!target.HasValue){var h=game.CurrentHole;double distance=Math.Min(h.Length*.70,195);double a=h.Tee.HeadingTo(h.Pin)*Math.PI/180;target=new CoursePoint(h.Tee.X+Math.Sin(a)*distance,h.Tee.D+Math.Cos(a)*distance);}
                report.impactFrame=frames;game.StrikeToward(target.Value);stage=3;wait=0;
            }
            else if(stage==3){
                if(game.Current==GolfGame.State.Flight){sawFlight=true;flightFrames++;}
                if(sawFlight&&game.Current!=GolfGame.State.Flight){stage=4;post=0;}
                if(++wait>1800)throw new TimeoutException("Golf ball flight did not complete");
            }
            else if(stage==4 && ++post>=120){Finish(sawFlight?0:2);return;}
            if(tick%2==0){GameCapture.Save(Path.Combine(dir,$"f_{frames:D5}.jpg"),1280,720);frames++;}
            if(audio){int n=AudioRenderer.GetSampleCountForCaptureFrame();if(n>0){using var b=new NativeArray<float>(n*channels,Allocator.Temp);AudioRenderer.Render(b);sound.AddRange(b);}}
            var ball=GameObject.Find("Ball");var p=ball?ball.transform.position:Vector3.zero;
            trace.WriteLine(string.Join(",",tick,frames,Time.time.ToString("F5",CultureInfo.InvariantCulture),stage,game.Current,game.Swing.Phase,p.x.ToString("F5",CultureInfo.InvariantCulture),p.y.ToString("F5",CultureInfo.InvariantCulture),p.z.ToString("F5",CultureInfo.InvariantCulture)));
        }
        void StartAudio(){if(CurrentGolfSwingFilm.Env("FILM_AUDIO","1")=="0")return;channels=AudioSettings.speakerMode==AudioSpeakerMode.Mono?1:2;rate=AudioSettings.outputSampleRate;audio=AudioRenderer.Start();report.audioStarted=audio;}
        void Finish(int code){if(done)return;done=true;trace?.Dispose();if(audio){AudioRenderer.Stop();audio=false;if(sound.Count>0)WriteWav(Path.Combine(dir,"sound.wav"));}
            report.frames=frames;report.duration=frames/30f;report.sawFlight=sawFlight;report.flightSimulationFrames=flightFrames;report.finalState=game.Current.ToString();report.audioSeconds=rate>0?sound.Count/(float)(rate*channels):0;
            File.WriteAllText(Path.Combine(dir,"film.json"),JsonUtility.ToJson(report,true));
            PlayerPrefs.SetInt("holes",savedHoles);PlayerPrefs.SetString("course",savedCourse);PlayerPrefs.Save();
            Debug.Log("[CurrentGolfSwingFilm] "+JsonUtility.ToJson(report));CurrentGolfSwingFilm.End(code);
        }
        void WriteWav(string path){using var w=new BinaryWriter(File.Create(path));int bytes=sound.Count*2;w.Write("RIFF".ToCharArray());w.Write(36+bytes);w.Write("WAVE".ToCharArray());w.Write("fmt ".ToCharArray());w.Write(16);w.Write((short)1);w.Write((short)channels);w.Write(rate);w.Write(rate*channels*2);w.Write((short)(channels*2));w.Write((short)16);w.Write("data".ToCharArray());w.Write(bytes);foreach(float s in sound)w.Write((short)Mathf.Clamp(Mathf.RoundToInt(s*32767),-32768,32767));}
    }
}
#endif
