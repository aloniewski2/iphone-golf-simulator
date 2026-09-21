using System;
using System.Collections;
using System.Runtime.InteropServices;
using System.Text;
using GolfArcade.Tennis;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GolfArcade.Game
{
    public sealed class NativeSportsSession : MonoBehaviour
    {
        [Serializable] public class Message {
            public int version; public string session, action, sport, playerID, playerName;
            public bool female,left,sound=true,haptics=true,touch,external; public int skin=2; public float value;
        }
        [Serializable] public class Sample {
            public int version,swing; public string session; public double time; public float target,power,aim; public bool valid;
            public float qx,qy,qz,qw,rx,ry,rz,gx,gy,gz;
        }
        [Serializable] class Event {
            public int version=1; public string session,type,message; public float stamina=1;
        }
        public static bool Active { get; private set; }
        public static bool Left { get; private set; }
        string session; bool loading,paused=true,touch; int lastSwing;
        double lastSample=-1; float target, nextFeedback;
        TennisGame tennis; GolfGame golf;
        readonly StringBuilder buffer=new(8192);
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] static extern int SportsPollInput(StringBuilder value,int capacity);
        [DllImport("__Internal")] static extern void SportsEmit(string value);
        [DllImport("__Internal")] static extern double SportsClock();
#else
        static int SportsPollInput(StringBuilder value,int capacity)=>0;
        static void SportsEmit(string value) {}
        static double SportsClock()=>Time.realtimeSinceStartupAsDouble;
#endif
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot() {
#if UNITY_IOS && !UNITY_EDITOR
            var go=new GameObject("NativeSportsSession"); DontDestroyOnLoad(go); go.AddComponent<NativeSportsSession>();
#endif
        }
        void Start()=>Emit("boot","");
        public void Receive(string json) {
            try {
                var m=JsonUtility.FromJson<Message>(json);
                if(m==null || m.version!=1 || string.IsNullOrEmpty(m.session)) return;
                if(m.action=="start") {
                    if(loading || (Active && m.session==session)) return;
                    if(m.sport!="golf" && m.sport!="tennis") return;
                    StartCoroutine(Load(m)); return;
                }
                if(m.session!=session || !Active) return;
                switch(m.action) {
                    case "pause": SetPaused(true); break;
                    case "resume": SetPaused(false); break;
                    case "end": SetPaused(true); Emit("exit",tennis ? $"Rally hits: {tennis.Hits}" : "Golf session ended"); Active=false; Left=false; break;
                    case "refeed": if(tennis) tennis.Refeed(); break;
                    case "aim": if(tennis) tennis.AimInput=Mathf.Clamp(m.value,-1,1); if(golf) golf.NativeAim(m.value); break;
                    case "club": if(golf) golf.NativeClub(m.value<0?-1:1); break;
                    case "sound": AudioListener.volume=m.value>0?1:0; break;
                    case "haptics": Haptics.Enabled=m.value>0; Haptics.Release(); break;
                    case "display": ConfigureDisplay(true); break;
                    case "touch": touch=true; if(golf) golf.Swing.Detector.Reset(); break;
                    case "recalibrate": if(golf) golf.Swing.Detector.Reset(); break;
                }
            } catch(Exception e) { Emit("error",e.Message); }
        }
        IEnumerator Load(Message m) {
            loading=true; Active=true; session=m.session; Left=m.left; lastSwing=0; lastSample=-1; target=0;
            touch=m.touch;
            GolferStyle.Body=m.female?GolferStyle.BodyKind.Female:GolferStyle.BodyKind.Male;
            GolferStyle.SkinTone=m.skin; AudioListener.volume=m.sound?1:0; Haptics.Enabled=m.haptics;
            Time.timeScale=1; Application.targetFrameRate=60;
            Screen.orientation=m.external ? ScreenOrientation.Portrait : ScreenOrientation.LandscapeLeft;
            yield return SceneManager.LoadSceneAsync(m.sport=="tennis"?"Tennis":"Golf");
            tennis=FindFirstObjectByType<TennisGame>(); golf=FindFirstObjectByType<GolfGame>();
            if(tennis) { tennis.NativeControlled=true; tennis.SelectCharacter(m.female); }
            if(golf && m.touch) golf.Swing.Armed=false;
            loading=false; SetPaused(true);
            if(ConfigureDisplay(m.external)) Emit("ready",m.playerName);
        }
        bool ConfigureDisplay(bool external) {
            int index=external?1:0;
            if(Display.displays.Length<=index) { Emit("error","This route does not expose an independent Unity display. Use a supported AirPlay receiver/wired display, or choose on-phone preview."); return false; }
            if(external && !Display.displays[index].active) Display.displays[index].Activate();
            foreach(var camera in FindObjectsByType<Camera>(FindObjectsSortMode.None)) camera.targetDisplay=index;
            foreach(var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None)) canvas.targetDisplay=index;
            return true;
        }
        void SetPaused(bool value) { paused=value; Time.timeScale=value?0:1; Haptics.Release(); if(value && golf) golf.Swing.Detector.Reset(); }
        void Update() {
            if(!Active || loading) return;
            for(int i=0;i<64 && SportsPollInput(buffer,8192)>0;i++) {
                var sample=JsonUtility.FromJson<Sample>(buffer.ToString()); buffer.Clear();
                if(!AcceptSample(sample,session,lastSample,SportsClock())) continue;
                lastSample=sample.time;
                if(paused || !sample.valid) continue;
                target=Mathf.Clamp(sample.target,-1,1);
                if(golf && !touch) golf.NativeMotion(sample);
                if(sample.swing>lastSwing) {
                    lastSwing=sample.swing;
                    if(tennis) tennis.RequestSwing(Mathf.Clamp01(sample.power));
                    if(golf && touch) golf.NativeSwing(Mathf.Clamp01(sample.power));
                }
            }
            if(!paused && lastSample>0 && SportsClock()-lastSample>.3) {
                SetPaused(true); Emit("error","Motion input stopped. Recalibrate or select touch controls.");
            }
            if(!paused && tennis && tennis.Player) {
                float delta=target*3.6f-tennis.Player.transform.position.x;
                tennis.SetLateralInput(Mathf.Clamp(delta*2,-1,1),Mathf.Abs(delta)>.7f);
            }
            if(Time.unscaledTime>=nextFeedback) {
                nextFeedback=Time.unscaledTime+.1f;
                Emit("feedback",tennis?$"Hits {tennis.Hits} · {tennis.Feedback}":golf?golf.NativeFeedback():"",tennis?tennis.Stamina:1);
            }
        }
        void Emit(string type,string message,float stamina=1)=>SportsEmit(JsonUtility.ToJson(new Event {session=session,type=type,message=message,stamina=stamina}));
        public static bool AcceptSample(Sample s,string expected,double previous,double now) =>
            s!=null && s.version==1 && s.session==expected && s.time>previous && s.time>=now-.25 && s.time<=now+.05 &&
            !float.IsNaN(s.target) && !float.IsInfinity(s.target) && !float.IsNaN(s.power) && !float.IsInfinity(s.power);
        void OnDestroy() { Active=false; Left=false; Time.timeScale=1; }
    }
}
