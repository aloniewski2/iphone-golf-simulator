using System;
using System.Collections;
using System.Runtime.InteropServices;
using GolfArcade.Tennis;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;

namespace GolfArcade.Game
{
    /// Runs before gameplay scripts so the newest phone sample is applied in the same frame
    /// it arrives, rather than whenever Unity happened to order this after TennisGame.
    [DefaultExecutionOrder(-1000)]
    public sealed class NativeSportsSession : MonoBehaviour
    {
        [Serializable] public class Message {
            public int version; public string session, action, sport, playerID, playerName,reason;
            public bool female,left,sound=true,haptics=true,touch,external,bench; public int skin=2,token,fps=60; public float value,value2,difficulty=-1;
            // Tennis menu choice: "campaign" or "training", and the campaign round's opponent.
            public string mode,opponent,opponentName,round;
            // The match format (sets to win, games per set) and the coach's changeover lines, "|"-separated.
            public int sets=1,games=3; public string coach;
            // The player's kit colours (hex, "" = the kit's own), coaching tips, TV edge margin.
            public string shirt,shorts,accent,racket; public bool tips=true; public float overscan;
        }
        /// One motion sample, read straight out of native memory. This used to be JSON text
        /// decoded into a new string and a new object 100 times a second -- steady garbage
        /// that shows up as periodic collection hitches. Layout mirrors `SportsSample` in
        /// SportsRuntime.h and SportsBridge.mm exactly; change all three together.
        [StructLayout(LayoutKind.Sequential)] public struct Sample {
            public int version, session; public double time; public float target,power,aim;
            public int swing, swingStart, swingAbort, flags;
            public float handSide,lift,strokeFacing;
            public float qx,qy,qz,qw,rx,ry,rz,gx,gy,gz;
            public const int Version = 2;
            public bool valid => (flags & 1) != 0;
            /// World tracking is blurred or limited; the rally continues with a warning.
            public bool degraded => (flags & 2) != 0;
        }
        [Serializable] class Event {
            public int version=1; public string session,type,message; public float stamina=1,playerX; public int frame; public bool paused; public double inputAge;
        }
        public static bool Active { get; private set; }
        public static bool Left { get; private set; }
        public static string PauseReason { get; private set; }
        /// True while input comes from on-screen controls rather than phone motion. Serving
        /// cannot demand a raised-arm gesture from a player who is tapping a button.
        public static bool Touch { get; private set; }
        /// Non-empty while world tracking is degraded. The rally keeps running; this is the
        /// on-screen explanation for why the player briefly stopped responding to steps.
        public static string TrackingWarning { get; private set; }
        string session; int token; bool loading,paused=true,touch; int lastSwing,lastSwingStart,lastSwingAbort;
        double lastSample=-1,resumedAt; float target, nextFeedback;
        TennisGame tennis; GolfGame golf;
        Camera gameplayCamera;
        bool frameRendered;
        int outputDisplay;
        public bool Ready { get; private set; }
        public Camera GameplayCamera => gameplayCamera;
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] static extern int SportsPollSample(out Sample sample);
        [DllImport("__Internal")] static extern void SportsEmit(string value);
        [DllImport("__Internal")] static extern double SportsClock();
        [DllImport("__Internal")][return: MarshalAs(UnmanagedType.I1)] static extern bool SportsPresentExternalDisplay();
#else
        static int SportsPollSample(out Sample sample) { sample=default; return 0; }
        static void SportsEmit(string value) {}
        static double SportsClock()=>Time.realtimeSinceStartupAsDouble;
        static bool SportsPresentExternalDisplay()=>true;
#endif
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot() {
#if UNITY_IOS && !UNITY_EDITOR
            var go=new GameObject("NativeSportsSession"); DontDestroyOnLoad(go); go.AddComponent<NativeSportsSession>();
#endif
        }
        void Start()
        {
            Emit("boot","");
            // What the phone's controller and campaign screens need from a tennis match.
            TennisGame.MatchFinished+=(won,score)=>Emit("matchOver",(won?"won|":"lost|")+score);
            TennisGame.ScoreChanged+=line=>Emit("score",line);
            TennisGame.PhaseChanged+=phase=>Emit("phase",phase);
            // The timing check's result, milliseconds, or "failed".
            TennisGame.TimingChecked+=lag=>Emit("timing",lag<0 ? "failed" : (lag*1000).ToString("0",System.Globalization.CultureInfo.InvariantCulture));
            TennisGame.ContactMade+=(face,grade,super)=>Emit("contact",
                string.Format(System.Globalization.CultureInfo.InvariantCulture,"{0:0.000},{1:0.000},{2},{3},{4:0}",face.x,face.y,(int)grade,super?1:0,TennisGame.LastLateness*1000));
            // The tutorial's steps for the phone, and its end; rallies for the stats; golf shots.
            TennisTutorial.StepChanged+=(i,n,text)=>Emit("tutorialStep",$"{i}|{n}|{text}");
            TennisTutorial.Finished+=()=>Emit("tutorialDone","");
            TennisGame.RallyEnded+=shots=>Emit("rally",shots.ToString());
            GolfGame.ShotStruck+=()=>Emit("shot","");
        }
        void OnEnable() { Camera.onPostRender+=Rendered; RenderPipelineManager.endCameraRendering+=RenderedSRP; }
        void OnDisable() { Camera.onPostRender-=Rendered; RenderPipelineManager.endCameraRendering-=RenderedSRP; }
        void Rendered(Camera camera) { if(camera==gameplayCamera) frameRendered=true; }
        void RenderedSRP(ScriptableRenderContext context,Camera camera)=>Rendered(camera);
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
                    case "pause": SetPaused(true,m.reason); break;
                    case "resume": SetPaused(false); break;
                    case "end": StopAllCoroutines(); loading=false; Ready=false; SetPaused(true); Emit("exit",tennis ? $"Rally hits: {tennis.Hits}" : "Golf session ended"); Active=false; Left=false; break;
                    case "refeed": if(tennis) tennis.Refeed(); break;
                    case "aim": if(tennis) tennis.AimInput=Mathf.Clamp(m.value,-1,1); if(golf) golf.NativeAim(m.value); break;
                    case "club": if(golf) golf.NativeClub(m.value<0?-1:1); break;
                    case "sound": AudioListener.volume=m.value>0?1:0; break;
                    case "haptics": Haptics.Enabled=m.value>0; Haptics.Release(); break;
                    case "display": if(!loading) StartCoroutine(Present(true,"displayReady")); break;
                    case "touch": Touch=true; touch=true; if(golf) golf.Swing.Detector.Reset(); break;
                    case "motion": Touch=false; touch=false; if(golf) golf.NativeReady(); break;
                    case "recalibrate": if(golf) golf.NativeReady(); break;
                    case "difficulty": if(tennis) tennis.OpponentDifficulty=Mathf.Clamp01(m.value); break;
                    case "coaching": TennisCoach.ResetTips(); break;
                    case "latency": if(tennis) tennis.DisplayLatency=m.value; break;
                    case "tutorialNext": if(tennis && tennis.PlayMode == TennisGame.Mode.Tutorial) tennis.GetComponent<TennisTutorial>()?.SkipStep(); break;
                    case "timingCheck": if(tennis) tennis.StartTimingCheck(); break;
                    // The controller serve: the toss meter's reading, the aim in the target box,
                    // and walking along the baseline before a serve (held buttons: -1, 0, 1).
                    case "toss": if(tennis) tennis.Toss(m.value); break;
                    case "serveAim": if(tennis) tennis.SetServeAim(m.value,m.value2); break;
                    case "nudge": if(tennis) tennis.ServeNudge=Mathf.Clamp(m.value,-1,1); break;
                    case "flash": if(!flashing) StartCoroutine(Flash(m.value)); break;
                }
            } catch(Exception e) { Emit("error",e.Message); }
        }
        IEnumerator Load(Message m) {
            loading=true; Ready=false; Active=true; session=m.session; token=m.token; Left=m.left;Touch=m.touch; lastSwing=0; lastSwingStart=0; lastSwingAbort=0; lastSample=-1; target=0;
            touch=m.touch;
            GolferStyle.Body=m.female?GolferStyle.BodyKind.Female:GolferStyle.BodyKind.Male;
            GolferStyle.SkinTone=m.skin; AudioListener.volume=m.sound?1:0; Haptics.Enabled=m.haptics;
            Time.timeScale=1;
            // 60 everywhere; 120 only when asked for and the panel can actually show it.
            // A TV runs at 60: rendering faster only adds frames AirPlay has to drop, and a
            // steady 60 is what keeps its delay steady.
            Application.targetFrameRate=m.external ? 60 : FrameRate.Target(m.fps,Screen.currentResolution.refreshRateRatio.value);
            QualitySettings.vSyncCount=0;
            TennisQuality.Apply();
            Screen.orientation=m.external ? ScreenOrientation.Portrait : ScreenOrientation.LandscapeLeft;
            // The phone's loading bar follows the scene load.
            var op=SceneManager.LoadSceneAsync(m.sport=="tennis"?"Tennis":"Golf");
            float nextProgress=0;
            while(!op.isDone) {
                if(Time.realtimeSinceStartup>=nextProgress) {
                    nextProgress=Time.realtimeSinceStartup+.1f;
                    Emit("loadProgress",Mathf.Clamp01(op.progress/.9f).ToString("0.00",System.Globalization.CultureInfo.InvariantCulture));
                }
                yield return null;
            }
            Emit("loadProgress","1");
            tennis=FindFirstObjectByType<TennisGame>(); golf=FindFirstObjectByType<GolfGame>();
            float deadline=Time.realtimeSinceStartup+15;
            while(m.sport=="tennis" && tennis && !tennis.Initialized && Time.realtimeSinceStartup<deadline) yield return null;
            if((m.sport=="tennis" && (!tennis || !tennis.Initialized)) || (m.sport=="golf" && !golf)) {
                loading=false; SetPaused(true); Emit("error","The sport did not initialize its gameplay scene."); yield break;
            }
            if(tennis) { tennis.NativeControlled=true; tennis.AutoPlay=m.bench; if(m.difficulty>=0) tennis.OpponentDifficulty=Mathf.Clamp01(m.difficulty); tennis.SelectCharacter(m.female);
                TennisCoach.TipsEnabled=m.tips;
                tennis.ApplyOutfit(TennisLook.Kit.From(m.shirt,m.shorts,m.accent,m.racket,m.skin));
                var mode=m.mode=="campaign" ? TennisGame.Mode.Campaign : m.mode=="training" ? TennisGame.Mode.Training
                    : m.mode=="tutorial" ? TennisGame.Mode.Tutorial : TennisGame.Mode.Exhibition;
                tennis.ConfigureMatch(mode,m.opponent,m.opponentName,m.round,m.sets,m.games,
                    string.IsNullOrEmpty(m.coach) ? null : m.coach.Split('|')); }
            if(m.bench && !GetComponent<FrameProbe>()) gameObject.AddComponent<FrameProbe>().Report=r=>Emit("perf",r);
            if(golf) golf.PrepareNativeAddress();
            gameplayCamera=tennis ? tennis.GameplayCamera : golf.GameplayCamera;
            if(golf && m.touch) golf.Swing.Armed=false;
            SetPaused(true);
            yield return Present(m.external,"ready");
            loading=false;
        }
        bool flashing;
        /// Delay probe for the phone: the whole TV goes black, then white, `count` times. The
        /// phone's camera, still aimed at the TV from setup, times when each white frame appears;
        /// "flash" reports when that frame's state was set, on the clock both sides share
        /// (SportsClock), so the difference is the TV's delay behind the game.
        IEnumerator Flash(float count) {
            flashing=true;
            var root=new GameObject("TV delay probe");
            var canvas=root.AddComponent<Canvas>();
            canvas.renderMode=RenderMode.ScreenSpaceOverlay; canvas.sortingOrder=32000; canvas.targetDisplay=outputDisplay;
            var panel=new GameObject("Probe").AddComponent<UnityEngine.UI.Image>();
            panel.transform.SetParent(root.transform,false); panel.raycastTarget=false;
            var rt=panel.rectTransform; rt.anchorMin=Vector2.zero; rt.anchorMax=Vector2.one; rt.offsetMin=rt.offsetMax=Vector2.zero;
            int flashes=Mathf.Clamp(Mathf.RoundToInt(count),1,8);
            try {
                for(int i=0;i<flashes;i++) {
                    panel.color=Color.black; yield return new WaitForSecondsRealtime(.35f);
                    panel.color=Color.white;
                    Emit("flash",SportsClock().ToString("R",System.Globalization.CultureInfo.InvariantCulture));
                    yield return new WaitForSecondsRealtime(.25f);
                }
            } finally { Destroy(root); flashing=false; }
        }

        IEnumerator Present(bool external,string eventType) {
            Ready=false; frameRendered=false;
            if(!ConfigureDisplay(external)) yield break;
            float deadline=Time.realtimeSinceStartup+8;
            while(!frameRendered && Time.realtimeSinceStartup<deadline) yield return null;
            if(!frameRendered) { Emit("error","Gameplay camera did not render a frame. Return to the menu and retry."); yield break; }
            Ready=true; Emit(eventType,"Gameplay camera rendered");
        }
        bool ConfigureDisplay(bool external) {
            int index=external?1:0;
            Debug.Log($"[SportsDisplay] Configure external={external} available={Display.displays.Length}");
            if(Display.displays.Length<=index) { Emit("error","This route does not expose an independent Unity display. Use a supported AirPlay receiver/wired display, or choose on-phone preview."); return false; }
            // On iOS Display.active reports screen availability even before a
            // render window exists. Always activate; native initialization is idempotent.
            if(external) Display.displays[index].Activate();
            if(external && !SportsPresentExternalDisplay()) {
                Emit("error","Unity could not attach its renderer to the external scene. Return to menu and reconnect the display.");
                return false;
            }
            if(!gameplayCamera) { Emit("error","Gameplay camera is missing."); return false; }
            foreach(var camera in FindObjectsByType<Camera>(FindObjectsSortMode.None)) {
                if(camera.targetTexture) continue; // Minimap/render-texture cameras are not TV cameras.
                camera.enabled=camera==gameplayCamera;
            }
            if(external) {
                // 1080p is what goes over AirPlay (the plugin caps the screen mode); render no
                // more than that either, whatever the TV reports.
                var d=Display.displays[index];
                float shrink=Mathf.Min(1f,Mathf.Min(1920f/Mathf.Max(1,d.systemWidth),1080f/Mathf.Max(1,d.systemHeight)));
                if(shrink<1f) d.SetRenderingResolution(Mathf.RoundToInt(d.systemWidth*shrink),Mathf.RoundToInt(d.systemHeight*shrink));
                Debug.Log($"[SportsDisplay] external system {d.systemWidth}x{d.systemHeight} rendering {d.renderingWidth}x{d.renderingHeight}");
            }
            gameplayCamera.targetDisplay=index;
            outputDisplay=index;
            gameplayCamera.rect=new Rect(0,0,1,1);
            gameplayCamera.aspect=(float)Display.displays[index].renderingWidth/Mathf.Max(1,Display.displays[index].renderingHeight);
            gameplayCamera.enabled=true;
            Debug.Log($"[SportsDisplay] camera={gameplayCamera.name} aspect={gameplayCamera.aspect} size={Display.displays[index].renderingWidth}x{Display.displays[index].renderingHeight}");
            foreach(var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None)) canvas.targetDisplay=index;
            Debug.Log($"[SportsDisplay] Gameplay cameras routed to display {index}, active={Display.displays[index].active}");
            return true;
        }
        void SetPaused(bool value,string reason=null) {
            paused=value; PauseReason=value ? (string.IsNullOrEmpty(reason) ? "PAUSED — tap Ready on your phone" : reason) : null;
            if(value) TrackingWarning="";
            if(!value) resumedAt=SportsClock();
            Time.timeScale=value?0:1; Haptics.Release(); if(value && golf) golf.Swing.Detector.Reset();
        }
        void Update() {
            if(!Active || loading) return;
            // Rotation settles after scene loading; do not freeze the initial portrait aspect.
            if(gameplayCamera && outputDisplay==0 && Screen.height>0)
                gameplayCamera.aspect=(float)Screen.width/Screen.height;
            for(int i=0;i<64;i++) {
                if(SportsPollSample(out var sample)==0) break;
                if(!AcceptSample(sample,token,lastSample,SportsClock())) continue;
                lastSample=sample.time;
                // The phone decides tracking quality; Unity only decides how to show it.
                TrackingWarning = sample.degraded ? DegradedWarning : "";
                if(paused || !sample.valid) continue;
                // A degraded sample still carries the last good court position, so the player
                // holds station through the blip instead of the game pausing.
                target=Mathf.Clamp(sample.target,-1,1);
                if(golf && !touch) golf.NativeMotion(sample);
                // Onset first: the character starts the stroke the moment the phone does, and
                // confirmation (or a write-off) arrives a few samples later. All three can land
                // in one sample when frames are slow, so the order here matters.
                if(sample.swingStart>lastSwingStart) {
                    lastSwingStart=sample.swingStart;
                    if(tennis) tennis.BeginSwing(sample.handSide,sample.lift,sample.strokeFacing);
                }
                if(sample.swing>lastSwing) {
                    lastSwing=sample.swing;
                    // Racket-face aim, measured on the phone at the moment the stroke confirmed.
                    // Touch play aims with its own control (the "aim" command) instead.
                    if(tennis && !touch) tennis.AimInput=Mathf.Clamp(sample.aim,-1,1);
                    if(tennis) tennis.RequestSwing(Mathf.Clamp01(sample.power),sample.handSide,sample.lift,sample.strokeFacing);
                    if(golf && touch) golf.NativeSwing(Mathf.Clamp01(sample.power));
                }
                if(sample.swingAbort>lastSwingAbort) {
                    lastSwingAbort=sample.swingAbort;
                    if(tennis) tennis.AbortSwing();
                }
            }
            if(!paused && SportsClock()-Math.Max(lastSample,resumedAt)>.5) {
                SetPaused(true,"INPUT PAUSED — tap Ready on your phone"); Emit("error","Motion input stopped. Tap Ready or select touch controls.");
            }
            if(!paused && tennis && tennis.Player) {
                // The assist moves the servo's reference, so steering cooperates with it
                // rather than fighting it back to the raw phone position.
                float delta=target*3.6f+tennis.AssistOffset-tennis.Player.transform.position.x;
                tennis.SetLateralInput(Mathf.Clamp(delta*1.25f,-1,1),Mathf.Abs(delta)>1.1f);
            }
            // The phone only shows this as status text; four updates a second is plenty and
            // keeps the per-frame string building out of the hot path.
            if(Time.unscaledTime>=nextFeedback) {
                nextFeedback=Time.unscaledTime+.25f;
                string message=tennis?tennis.Feedback:golf?golf.NativeFeedback():"";
                if(!ReferenceEquals(message,lastFeedback) || Time.unscaledTime>=nextHeartbeat) {
                    lastFeedback=message; nextHeartbeat=Time.unscaledTime+1;
                    Emit("feedback",tennis?$"Hits {tennis.Hits} · {message}":message,tennis?tennis.Stamina:1);
                }
            }
        }
        const string DegradedWarning="Tracking degraded — keep the lens clear";
        string lastFeedback; float nextHeartbeat;
        void Emit(string type,string message,float stamina=1)=>SportsEmit(JsonUtility.ToJson(new Event {session=session,type=type,message=message,stamina=stamina,frame=Time.frameCount,paused=paused,playerX=tennis && tennis.Player ? tennis.Player.transform.position.x : 0,inputAge=lastSample<0 ? -1 : SportsClock()-lastSample}));
        public static bool AcceptSample(in Sample s,int expected,double previous,double now) =>
            s.version==Sample.Version && s.session==expected && s.time>previous && s.time>=now-.25 && s.time<=now+.05 &&
            !float.IsNaN(s.target) && !float.IsInfinity(s.target) && !float.IsNaN(s.power) && !float.IsInfinity(s.power);
        void OnDestroy() { Active=false; Left=false; TrackingWarning=""; Time.timeScale=1; }
    }
}
