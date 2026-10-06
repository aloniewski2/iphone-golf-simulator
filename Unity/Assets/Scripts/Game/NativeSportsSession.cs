using System;
using System.Collections;
using System.Runtime.InteropServices;
using GolfArcade.Tennis;
using GolfArcade.Multiplayer;
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
            public string mode,opponent,opponentName,round; public int ultimate;
            // The match format (sets to win, games per set) and the coach's changeover lines, "|"-separated.
            public int sets=1,games=3; public string coach;
            public int tutorialStart;
            // The player's kit colours (hex, "" = the kit's own), coaching tips, TV edge margin.
            public int hairStyle=1,hairColor=1,faceShape,heightChoice=2,buildChoice=2,haircut=-1;
            public float bodySize=-1;
            public string shirt,shorts,accent,racket,skinHex,hairHex; public bool tips=true; public float overscan;
            // The tennis court: "" / "resort", "skyscraper" or "volcano"; and a court colour (hex, "" = the venue's own).
            public string venue,course,courtHex,network; public int aimSequence;
            public string[] emotes;
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
            public int version=1; public string session,type,message,finalScore,golfState; public bool matchComplete,matchWon,golfHasNextHole; public float stamina=1,playerX; public int frame; public bool paused; public double inputAge;
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
        string session; int token; bool loading,paused=true,touch,multiplayerSession; int lastSwing,lastSwingStart,lastSwingAbort;
        double lastSample=-1,resumedAt; float target, nextFeedback;
        TennisGame tennis; GolfGame golf;
        Camera gameplayCamera;
        bool frameRendered;
        int outputDisplay;
        public bool Ready { get; private set; }
        public Camera GameplayCamera => gameplayCamera;
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void SportsRecorderConfigure(string session,int display);
        [DllImport("__Internal")] static extern void SportsRecorderEnable([MarshalAs(UnmanagedType.I1)] bool enabled);
        [DllImport("__Internal")] static extern void SportsRecorderPause([MarshalAs(UnmanagedType.I1)] bool paused);
        [DllImport("__Internal")] static extern void SportsRecorderBeginPoint();
        [DllImport("__Internal")] static extern void SportsRecorderEndPoint();
        [DllImport("__Internal")] static extern void SportsRecorderSave();
        [DllImport("__Internal")] static extern void SportsRecorderStop();
        [DllImport("__Internal")] static extern int SportsPollSample(out Sample sample);
        [DllImport("__Internal")] static extern void SportsEmit(string value);
        [DllImport("__Internal")] static extern double SportsClock();
        [DllImport("__Internal")] static extern int SportsPrepareExternalDisplay();
#else
        static void SportsRecorderConfigure(string session,int display) {}
        static void SportsRecorderEnable(bool enabled) {}
        static void SportsRecorderPause(bool paused) {}
        static void SportsRecorderBeginPoint() {}
        static void SportsRecorderEndPoint() {}
        static void SportsRecorderSave() {}
        static void SportsRecorderStop() {}
        static int SportsPollSample(out Sample sample) { sample=default; return 0; }
        static void SportsEmit(string value) {}
        static double SportsClock()=>Time.realtimeSinceStartupAsDouble;
        static int SportsPrepareExternalDisplay()=>1;
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
            TennisGame.RecordingPointStarted+=SportsRecorderBeginPoint;
            TennisGame.RecordingPointEnded+=SportsRecorderEndPoint;
            // What the phone's controller and campaign screens need from a tennis match.
            TennisGame.MatchFinished+=(won,score)=>Emit("matchOver",(won?"won|":"lost|")+score);
            TennisGame.MatchStatsReady+=line=>Emit("matchStats",line);
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
            GolfGame.NativeExitRequested+=GolfExit;

            GolfTutorial.StepChanged+=(i,n,text)=>Emit("tutorialStep",$"{i}|{n}|{text}");
            GolfTutorial.StepResolved+=(i,misses,skip)=>Emit(skip ? "tutorialSkip" : "tutorialSuccess",$"{i}|{misses}");
            GolfTutorial.HoleDone+=(strokes,capped)=>Emit("golfHoleDone",$"{strokes}|{(capped ? "true" : "false")}");
            GolfTutorial.Finished+=()=>Emit("tutorialDone","");
            TennisGame.Landed += AimLanding;
            TennisGame.DrillPoint += AimMiss;
        }
        void AimLanding(bool player, bool legal, Vector3 at, bool serve) {
            if (tennis && tennis.AimPractice && player)
                Emit("aimLanding", string.Format(System.Globalization.CultureInfo.InvariantCulture,"{0}|{1:0.000}|{2:0.000}|{3}",tennis.AimPracticeSequence,at.x,at.z,legal?1:0));
        }
        void AimMiss(bool player, string why) {
            if (tennis && tennis.AimPractice && !player) Emit("aimMiss",tennis.AimPracticeSequence+"|"+why);
        }
        void OnDestroy() {
            TennisGame.RecordingPointStarted-=SportsRecorderBeginPoint;
            TennisGame.RecordingPointEnded-=SportsRecorderEndPoint;
            TennisGame.Landed -= AimLanding; TennisGame.DrillPoint -= AimMiss;
            GolfGame.NativeExitRequested -= GolfExit;
            SportsRecorderStop();
            Active=false; Left=false; TrackingWarning=""; Time.timeScale=1;
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
                if (SportsMultiplayer.Active && (m.action=="pause" || m.action=="resume")) SetPaused(m.action=="pause",m.reason);
                if (SportsMultiplayer.Command(m)) return;
                switch(m.action) {
                    case "pause": SetPaused(true,m.reason); break;
                    case "resume": SetPaused(false); break;
                    case "end": SportsMultiplayer.Shutdown(); SportsRecorderStop(); StopAllCoroutines(); loading=false; Ready=false; SetPaused(true); Emit("exit",tennis ? $"Rally hits: {tennis.Hits}" : "Golf session ended"); Active=false; Left=false; break;
                    case "recordPoints": if(tennis) SportsRecorderEnable(m.value>0); break;
                    case "savePoint": if(tennis) SportsRecorderSave(); break;
                    case "ultimateSelect": break; // Ignore commands from older controllers.
                    case "ultimate": break;
                    case "dive": if(tennis && !paused) tennis.RequestDive(); break;
                    case "emote":
                        bool played = tennis && !paused && m.value == Mathf.Floor(m.value) && tennis.RequestEquippedEmote((int)m.value);
                        Emit("emoteResult", played ? "Emote selected" : "Emotes are available for your intro or after you score.");
                        break;
                    case "refeed": if(tennis) tennis.Refeed(); break;
                    case "rallyAim": if(tennis) tennis.SetShotAim(m.value,m.value2); break;
                    case "aimPractice": if(tennis) tennis.SetAimPractice(m.value>0); break;
                    case "aimFeed": if(tennis) tennis.FeedAimPractice(m.value,m.value2<0,m.aimSequence); break;
                    case "aim": if(tennis) tennis.AimInput=Mathf.Clamp(m.value,-1,1); if(golf) golf.NativeAim(m.value); break;
                    case "golfContinue": if(golf && !paused) golf.NativeContinue(); break;
                    case "club": if(golf) golf.NativeClub(m.value<0?-1:1); break;
                    case "sound": AudioListener.volume=m.value>0 && !Application.isBatchMode?1:0; break;
                    case "haptics": Haptics.Enabled=m.value>0; Haptics.Release(); break;
                    case "display": if(!loading) StartCoroutine(Present(true,"displayReady")); break;
                    case "touch": Touch=true; touch=true; if(tennis) { tennis.CancelTimingCheck(); tennis.SetAimPractice(false); } if(golf) golf.Swing.Detector.Reset(); break;
                    case "motion": Touch=false; touch=false; if(golf) golf.NativeReady(); break;
                    case "recalibrate": if(golf) golf.NativeReady(); break;
                    case "difficulty": if(tennis) tennis.OpponentDifficulty=Mathf.Clamp01(m.value); break;
                    case "coaching": TennisCoach.ResetTips(); break;
                    case "latency": if(tennis) tennis.DisplayLatency=m.value; break;
                    case "tutorialNext":
                        if(tennis && tennis.PlayMode == TennisGame.Mode.Tutorial) tennis.GetComponent<TennisTutorial>()?.SkipStep();
                        break;
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
            loading=true; Ready=false; Active=true; multiplayerSession=!string.IsNullOrEmpty(m.network); session=m.session; token=m.token; Left=m.left;Touch=m.touch; lastSwing=0; lastSwingStart=0; lastSwingAbort=0; lastSample=-1; target=0;
            touch=m.touch;
            GolferStyle.Body=m.female?GolferStyle.BodyKind.Female:GolferStyle.BodyKind.Male;
            GolferStyle.SkinTone=m.skin;
            GolferStyle.Edit(look => {
                look.Haircut = m.hairStyle;
                look.Hair = GolferStyle.HexOf(GolferStyle.HairColors[Mathf.Clamp(m.hairColor, 0, GolferStyle.HairColors.Length - 1)]);
                look.Shirt = m.shirt; look.Shorts = m.shorts; look.Shoes = m.accent;
            });
            AudioListener.volume=m.sound && !Application.isBatchMode?1:0; Haptics.Enabled=m.haptics;   // automated runs stay silent
            Time.timeScale=1;
            // 60 everywhere; 120 only when asked for and the panel can actually show it.
            // A TV runs at 60: rendering faster only adds frames AirPlay has to drop, and a
            // steady 60 is what keeps its delay steady.
            Application.targetFrameRate=m.external ? 60 : FrameRate.Target(m.fps,Screen.currentResolution.refreshRateRatio.value);
            QualitySettings.vSyncCount=0;
            TennisQuality.Apply();
            Screen.orientation=m.external ? ScreenOrientation.Portrait : ScreenOrientation.LandscapeLeft;
            // The phone's loading bar follows the scene load.
            TennisVenue.Selected=TennisVenue.Parse(m.venue);
            TennisVenue.CourtColorOverride=string.IsNullOrEmpty(m.courtHex) ? (Color?)null : HeroKit.Hex(m.courtHex);
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
                tennis.Player.Customize(m.skin,m.hairStyle,m.hairColor,m.faceShape,m.heightChoice,m.buildChoice,m.bodySize,TennisLook.Kit.From(m.shirt,m.shorts,m.accent,m.racket,m.skin));
                // The locker's look on the visible Hero V4 (the gameplay rig above is hidden).
                { var look=HeroKit.Style.From(m.skin,m.hairColor,m.hairStyle,TennisLook.Kit.From(m.shirt,m.shorts,m.accent,m.racket,m.skin),m.haircut>=0?m.haircut:(m.female?1:0),m.female);
                  // locker colour ranges: the exact skin / hair picked (else the preset index)
                  if(!string.IsNullOrEmpty(m.skinHex)) look.SkinTint=HeroKit.Hex(m.skinHex);
                  if(!string.IsNullOrEmpty(m.hairHex)) look.HairTint=HeroKit.Hex(m.hairHex);
                  look.BodySize=Mathf.Clamp01(m.bodySize<0?m.buildChoice/4f:m.bodySize);
                  tennis.SetPlayerLook(look); }   // kept and re-applied on every rebuild
                var mode=m.mode=="campaign" ? TennisGame.Mode.Campaign : m.mode=="training" ? TennisGame.Mode.Training
                    : TennisGame.Mode.Exhibition;
                TennisTutorial.StartIndex = mode == TennisGame.Mode.Tutorial ? m.tutorialStart : 0;
                tennis.ConfigureMatch(mode,m.opponent,m.opponentName,m.round,m.sets,m.games,
                    string.IsNullOrEmpty(m.coach) ? null : m.coach.Split('|')); }
            if(tennis) tennis.EquipEmotes(m.emotes);
            if(m.bench && !GetComponent<FrameProbe>()) gameObject.AddComponent<FrameProbe>().Report=r=>Emit("perf",r);
            if(golf) golf.PrepareNativeAddress(m.course);
            if(!string.IsNullOrEmpty(m.network)) SportsMultiplayer.Configure(m.network);
            gameplayCamera=tennis ? tennis.GameplayCamera : golf.GameplayCamera;
            if(golf && m.touch) golf.Swing.Armed=false;
            SetPaused(true);
            yield return Present(m.external,"ready");
            loading=false;
        }
        void GolfExit() {
            SetPaused(true); Emit("exit", "Golf session ended");
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
            float attachDeadline=Time.realtimeSinceStartup+8;
            while(!ConfigureDisplay(external)) {
                if(!external || Time.realtimeSinceStartup>=attachDeadline) {
                    Emit("error", external ? "The external display could not start. Check AirPlay and choose a map to retry." : "Gameplay camera is missing.");
                    yield break;
                }
                yield return null;
            }
            float deadline=Time.realtimeSinceStartup+8;
            while(!frameRendered && Time.realtimeSinceStartup<deadline) yield return null;
            if(!frameRendered) { Emit("error","Gameplay camera did not render a frame. Return to the menu and retry."); yield break; }
            Ready=true; Emit(eventType,"Gameplay camera rendered");
        }
        bool ConfigureDisplay(bool external) {
            // The scene's UIScreen can appear after Unity's initial display cache was built.
            // Native code registers and activates that exact screen and returns its cache index.
            int index=external ? SportsPrepareExternalDisplay() : 0;
            if(index<0 || Display.displays.Length<=index || !gameplayCamera) return false;
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
            foreach(var listener in FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
                if(listener.enabled && !listener.GetComponent<PointClipAudioTap>()) listener.gameObject.AddComponent<PointClipAudioTap>();
            SportsRecorderConfigure(session,index);
            SportsRecorderPause(paused);
            gameplayCamera.rect=new Rect(0,0,1,1);
            gameplayCamera.aspect=(float)Display.displays[index].renderingWidth/Mathf.Max(1,Display.displays[index].renderingHeight);
            gameplayCamera.enabled=true;
            Debug.Log($"[SportsDisplay] camera={gameplayCamera.name} aspect={gameplayCamera.aspect} size={Display.displays[index].renderingWidth}x{Display.displays[index].renderingHeight}");
            foreach(var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None)) canvas.targetDisplay=index;
            Debug.Log($"[SportsDisplay] Gameplay cameras routed to display {index}, active={Display.displays[index].active}");
            return true;
        }
        void SetPaused(bool value,string reason=null) {
            paused=value; SportsRecorderPause(value); PauseReason=value ? (string.IsNullOrEmpty(reason) ? "PAUSED — tap Ready on your phone" : reason) : null;
            if(value) TrackingWarning="";
            if(!value) { resumedAt=SportsClock(); if(tennis) tennis.LockLoadout(); }
            Time.timeScale=SportsMultiplayer.Active?1:value?0:1; Haptics.Release(); if(value && golf) golf.Swing.Detector.Reset();
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
                if (SportsMultiplayer.Active) { if(golf && !touch) golf.NativeMotion(sample); else SportsMultiplayer.Sample(sample,touch); continue; }
                // A degraded sample still carries the last good court position, so the player
                // holds station through the blip instead of the game pausing.
                target=Mathf.Clamp(sample.target,-1,1);
                float inputAge = Mathf.Clamp((float)(SportsClock() - sample.time), 0, .25f);
                if (tennis && !touch) tennis.AimInput = Mathf.Clamp(sample.aim, -1, 1);
                if(golf && !touch) golf.NativeMotion(sample);
                // Onset first: the character starts the stroke the moment the phone does, and
                // confirmation (or a write-off) arrives a few samples later. All three can land
                // in one sample when frames are slow, so the order here matters.
                if(sample.swingStart>lastSwingStart) {
                    lastSwingStart=sample.swingStart;
                    if(tennis) tennis.BeginSwing(sample.handSide,sample.lift,sample.strokeFacing,inputAge);
                }
                if(sample.swing>lastSwing) {
                    lastSwing=sample.swing;
                    // Racket-face aim, measured on the phone at the moment the stroke confirmed.
                    // Touch play aims with its own control (the "aim" command) instead.
                    if(tennis && !touch) tennis.AimInput=Mathf.Clamp(sample.aim,-1,1);
                    if(tennis) tennis.RequestSwing(Mathf.Clamp01(sample.power),sample.handSide,sample.lift,sample.strokeFacing,inputAge);
                    if(golf && touch) golf.NativeSwing(Mathf.Clamp01(sample.power));
                }
                if(sample.swingAbort>lastSwingAbort) {
                    lastSwingAbort=sample.swingAbort;
                    if(tennis) tennis.AbortSwing();
                }
            }
            if(!SportsMultiplayer.Active && !paused && SportsClock()-Math.Max(lastSample,resumedAt)>.5) {
                SetPaused(true,"INPUT PAUSED — tap Ready on your phone"); Emit("error","Motion input stopped. Tap Ready or select touch controls.");
            }
            if(!SportsMultiplayer.Active && !paused && tennis && tennis.Player) {
                // The assist moves the servo's reference, so steering cooperates with it
                // rather than fighting it back to the raw phone position.
                float delta=target*3.6f+tennis.AssistOffset-tennis.Player.transform.position.x;
                tennis.SetLateralInput(Mathf.Clamp(delta*1.25f,-1,1),Mathf.Abs(delta)>1.1f);
            }
            // The phone only shows this as status text; four updates a second is plenty and
            // keeps the per-frame string building out of the hot path.
            if(Time.unscaledTime>=nextFeedback) {
                nextFeedback=Time.unscaledTime+.25f;
                Emit("emoteState", !paused && tennis ? tennis.EmoteWindow : "");
                if(tennis) Emit("abilities", string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:0.000}|{1}|{2:0.00}|{3}|{4}", tennis.PlayerUltimate, tennis.UltimateArmed?1:0, tennis.DiveCooldownLeft, !paused && tennis.CanDive?1:0, !paused && tennis.CanArmUltimate?1:0));
                // Results are durable state, not a one-shot event that can be lost
                // while the phone changes scenes or drains the bounded event queue.
                if(!multiplayerSession && tennis && tennis.Match.Complete)
                    Emit("matchOver",(tennis.Match.PlayerWonMatch?"won|":"lost|")+tennis.Match.FinalScore);
                string message=tennis?tennis.Feedback:golf?golf.NativeFeedback():"";
                if(!ReferenceEquals(message,lastFeedback) || Time.unscaledTime>=nextHeartbeat) {
                    lastFeedback=message; nextHeartbeat=Time.unscaledTime+1;
                    Emit("feedback",tennis?$"Hits {tennis.Hits} · {message}":message,tennis?tennis.Stamina:1);
                }
            }
        }
        const string DegradedWarning="Tracking degraded — keep the lens clear";
        string lastFeedback; float nextHeartbeat;
        void Emit(string type,string message,float stamina=1)=>SportsEmit(JsonUtility.ToJson(new Event {session=session,type=type,message=message,golfState=golf ? golf.Current.ToString() : "",golfHasNextHole=golf && golf.NativeHasNextHole,matchComplete=!multiplayerSession && tennis && tennis.Match.Complete,matchWon=!multiplayerSession && tennis && tennis.Match.PlayerWonMatch,finalScore=!multiplayerSession && tennis && tennis.Match.Complete ? tennis.Match.FinalScore : "",stamina=stamina,frame=Time.frameCount,paused=paused,playerX=tennis && tennis.Player ? tennis.Player.transform.position.x : 0,inputAge=lastSample<0 ? -1 : SportsClock()-lastSample}));
        public static bool AcceptSample(in Sample s,int expected,double previous,double now) =>
            s.version==Sample.Version && s.session==expected && s.time>previous && s.time>=now-.25 && s.time<=now+.05 &&
            !float.IsNaN(s.target) && !float.IsInfinity(s.target) && !float.IsNaN(s.power) && !float.IsInfinity(s.power) &&
            !float.IsNaN(s.aim) && !float.IsInfinity(s.aim) && !float.IsNaN(s.strokeFacing) && !float.IsInfinity(s.strokeFacing);
    }
}
