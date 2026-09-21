using System;
using System.Collections;
using System.Runtime.InteropServices;
using System.Text;
using GolfArcade.Tennis;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;

namespace GolfArcade.Game
{
    public sealed class NativeSportsSession : MonoBehaviour
    {
        [Serializable] public class Message {
            public int version; public string session, action, sport, playerID, playerName,reason;
            public bool female,left,sound=true,haptics=true,touch,external; public int skin=2; public float value;
        }
        [Serializable] public class Sample {
            public int version,swing; public string session; public double time; public float target,power,aim; public bool valid;
            public string tracking,warning;
            public float qx,qy,qz,qw,rx,ry,rz,gx,gy,gz,handSide,lift,strokeFacing;
        }
        [Serializable] class Event {
            public int version=1; public string session,type,message; public float stamina=1,playerX; public int frame; public bool paused; public double inputAge;
        }
        public static bool Active { get; private set; }
        public static bool Left { get; private set; }
        public static string PauseReason { get; private set; }
        /// Non-empty while world tracking is degraded. The rally keeps running; this is the
        /// on-screen explanation for why the player briefly stopped responding to steps.
        public static string TrackingWarning { get; private set; }
        string session; bool loading,paused=true,touch; int lastSwing;
        double lastSample=-1,resumedAt; float target, nextFeedback;
        TennisGame tennis; GolfGame golf;
        const int InputCapacity=8192;
        readonly IntPtr inputBuffer=Marshal.AllocHGlobal(InputCapacity);
        readonly byte[] inputBytes=new byte[InputCapacity];
        Camera gameplayCamera;
        bool frameRendered;
        int outputDisplay;
        public bool Ready { get; private set; }
        public Camera GameplayCamera => gameplayCamera;
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] static extern int SportsPollInput(IntPtr value,int capacity);
        [DllImport("__Internal")] static extern void SportsEmit(string value);
        [DllImport("__Internal")] static extern double SportsClock();
        [DllImport("__Internal")][return: MarshalAs(UnmanagedType.I1)] static extern bool SportsPresentExternalDisplay();
#else
        static int SportsPollInput(IntPtr value,int capacity)=>0;
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
        void Start()=>Emit("boot","");
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
                    case "touch": touch=true; if(golf) golf.Swing.Detector.Reset(); break;
                    case "motion": touch=false; if(golf) golf.NativeReady(); break;
                    case "recalibrate": if(golf) golf.NativeReady(); break;
                }
            } catch(Exception e) { Emit("error",e.Message); }
        }
        IEnumerator Load(Message m) {
            loading=true; Ready=false; Active=true; session=m.session; Left=m.left; lastSwing=0; lastSample=-1; target=0;
            touch=m.touch;
            GolferStyle.Body=m.female?GolferStyle.BodyKind.Female:GolferStyle.BodyKind.Male;
            GolferStyle.SkinTone=m.skin; AudioListener.volume=m.sound?1:0; Haptics.Enabled=m.haptics;
            Time.timeScale=1; Application.targetFrameRate=60;
            Screen.orientation=m.external ? ScreenOrientation.Portrait : ScreenOrientation.LandscapeLeft;
            yield return SceneManager.LoadSceneAsync(m.sport=="tennis"?"Tennis":"Golf");
            tennis=FindFirstObjectByType<TennisGame>(); golf=FindFirstObjectByType<GolfGame>();
            float deadline=Time.realtimeSinceStartup+15;
            while(m.sport=="tennis" && tennis && !tennis.Initialized && Time.realtimeSinceStartup<deadline) yield return null;
            if((m.sport=="tennis" && (!tennis || !tennis.Initialized)) || (m.sport=="golf" && !golf)) {
                loading=false; SetPaused(true); Emit("error","The sport did not initialize its gameplay scene."); yield break;
            }
            if(tennis) { tennis.NativeControlled=true; tennis.SelectCharacter(m.female); }
            if(golf) golf.PrepareNativeAddress();
            gameplayCamera=tennis ? tennis.GameplayCamera : golf.GameplayCamera;
            if(golf && m.touch) golf.Swing.Armed=false;
            SetPaused(true);
            yield return Present(m.external,"ready");
            loading=false;
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
                int count=SportsPollInput(inputBuffer,InputCapacity);
                if(count<=0) break;
                if(count>=InputCapacity) continue;
                Marshal.Copy(inputBuffer,inputBytes,0,count);
                Sample sample;
                try { sample=JsonUtility.FromJson<Sample>(Encoding.UTF8.GetString(inputBytes,0,count)); }
                catch(ArgumentException) { continue; }
                if(!AcceptSample(sample,session,lastSample,SportsClock())) continue;
                lastSample=sample.time;
                // The phone decides tracking quality; Unity only decides how to show it.
                TrackingWarning = sample.tracking == "degraded"
                    ? (string.IsNullOrEmpty(sample.warning) ? "Tracking degraded" : sample.warning) : "";
                if(paused || !sample.valid) continue;
                // A degraded sample still carries the last good court position, so the player
                // holds station through the blip instead of the game pausing.
                target=Mathf.Clamp(sample.target,-1,1);
                if(golf && !touch) golf.NativeMotion(sample);
                if(sample.swing>lastSwing) {
                    lastSwing=sample.swing;
                    Debug.Log($"[SportsInput] accepted swing {lastSwing} power={sample.power}");
                    if(tennis) tennis.RequestSwing(Mathf.Clamp01(sample.power),sample.handSide,sample.lift,sample.strokeFacing);
                    if(golf && touch) golf.NativeSwing(Mathf.Clamp01(sample.power));
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
            if(Time.unscaledTime>=nextFeedback) {
                nextFeedback=Time.unscaledTime+.1f;
                Emit("feedback",tennis?$"Hits {tennis.Hits} · {tennis.Feedback}":golf?golf.NativeFeedback():"",tennis?tennis.Stamina:1);
            }
        }
        void Emit(string type,string message,float stamina=1)=>SportsEmit(JsonUtility.ToJson(new Event {session=session,type=type,message=message,stamina=stamina,frame=Time.frameCount,paused=paused,playerX=tennis && tennis.Player ? tennis.Player.transform.position.x : 0,inputAge=lastSample<0 ? -1 : SportsClock()-lastSample}));
        public static bool AcceptSample(Sample s,string expected,double previous,double now) =>
            s!=null && s.version==1 && s.session==expected && s.time>previous && s.time>=now-.25 && s.time<=now+.05 &&
            !float.IsNaN(s.target) && !float.IsInfinity(s.target) && !float.IsNaN(s.power) && !float.IsInfinity(s.power);
        void OnDestroy() { Marshal.FreeHGlobal(inputBuffer); Active=false; Left=false; TrackingWarning=""; Time.timeScale=1; }
    }
}
