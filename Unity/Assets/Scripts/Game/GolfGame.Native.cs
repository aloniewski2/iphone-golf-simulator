using UnityEngine;
using GolfArcade.Course;
using GolfArcade.Profile;
using GolfArcade.Swing;
namespace GolfArcade.Game {
    public sealed partial class GolfGame {
        public static event System.Action ShotStruck;
        public bool NativeControlled { get; private set; }
        public bool NativeHasNextHole => Current == State.RoundDone && holeIndex + 1 < course.Holes.Length;
        public static event System.Action NativeExitRequested;
        void RequestNativeExit() { enabled = false; NativeExitRequested?.Invoke(); }
        public Camera GameplayCamera => rig ? rig.Camera : null;
        public double TutorialHeading => heading;
        public GolfArcade.UI.Hud TutorialHud => hud;
        public int TutorialHole => holeIndex;
        public void PrepareNativeAddress(string courseKey = null) {
            NativeControlled = true; controllerMapHole = -1;
            hud.HideMenu(); home = null;
            hud.HideCourses(); courses = null;
            hud.HideLocker(); stage?.Hide();
            if (bigScreen) bigScreen.enabled = false;
            SetFog(0);
            var selected = GolfArcade.Course.Course.ByKey(courseKey ?? "cliffside") ?? GolfArcade.Course.Course.Cliffside();
            setup = GameSetup.Solo(ProfileStore.Active, selected.Key);
            StartRound();
            golfPresentation.Cancel(); pendingNativeIntro=true;
            BeginAim(false); hud.ShowPlayHud(true); RefreshControls();
            rig.SnapNext(); rig.ApplyFrame();
        }
        public void NativeContinue() {
            if(Current==State.Intro) { SkipPresentation(); return; }
            if (Current == State.Result) { ContinueShotResult(); return; }
            if (Current != State.RoundDone) return;
            if (NativeHasNextHole) NextHole(); else PlayAgain();

        }
        public void NativeAim(float value) { if(Current==State.Aim) Nudge(Mathf.Clamp(value,-1,1)*AimTapDegrees); }
        public void NativeClub(int value) { if(Current==State.Aim) CycleClub(value); }
        // The joystick uses the same axes as the phone's course map. Set the shot's
        // actual heading, so both the TV flight and the projected landing line agree.
        public void NativeAimDirection(float across,float forward) {
            if(Current!=State.Aim || !minimapCamera || float.IsNaN(across) || float.IsNaN(forward) ||
               float.IsInfinity(across) || float.IsInfinity(forward)) return;
            var direction=minimapCamera.transform.right*across+minimapCamera.transform.up*forward;
            direction.y=0;
            if(direction.sqrMagnitude<.01f) return;
            double desired=System.Math.Atan2(direction.x,direction.z)*180/System.Math.PI;
            if(GolfArcade.Multiplayer.SportsMultiplayer.Active) {
                var net=GolfArcade.Multiplayer.SportsMultiplayer.Instance;
                if(net.GolfState!=null) net.Submit(new GolfArcade.Multiplayer.NetworkInput {
                    action="golfAimHeading",value=(float)desired,actorSeat=net.GolfState.turn
                });
                return;
            }
            Nudge(Mathf.DeltaAngle((float)heading,(float)desired));
        }
        [System.Serializable] public sealed class ControllerPoint {
            public float x,y;
            public ControllerPoint(Vector3 viewport) { x=viewport.x; y=viewport.y; }
        }
        [System.Serializable] public sealed class PartyControllerPlayer {
            public int seat; public string name; public bool controlled,canEmote; public string[] emotes;
        }
        [System.Serializable] public sealed class PartyControllerReading {
            public int turn; public long shotID; public string player,outcome,phase;
            public bool myTurn,shared,putt; public double resultSeconds,carry,roll,apex,total;
            public PartyControllerPlayer[] players;
        }
        [System.Serializable] public sealed class ControllerReading {
            public PartyControllerReading party;
            public int hole,par,clubIndex;
            public string name,clubName,mapImage;
            public double yards,wind,windDegrees,clubYards,aimDegrees;
            public ControllerPoint ball,pin,landing,direction;
            public ControllerPoint[] path,reach;
        }
        int controllerMapHole=-1;
        Vector3 controllerMapPosition;
        float controllerMapSize;
        RenderTexture controllerMapTarget;
        // Metadata/marks are sent at 4 Hz. Reuse the already-rendered HUD map: no new camera
        // renders. Read back and JPEG-encode it only when the map's cached framing changes.
        public ControllerReading NativeControllerReading() {
            if(hole==null || !hud || !minimapCamera) return null;
            bool putting = hole.LieAt(ballAt).IsPuttingSurface();
            var reading=new ControllerReading {
                hole=hole.Number,name=hole.Name,par=hole.Par,yards=ballAt.DistanceTo(hole.Pin),
                wind=Wind.SpeedMPH,windDegrees=Wind.RelativeTo(heading),
                clubIndex=System.Array.IndexOf(GolfArcade.Shot.GolfClubs.All,club),
                clubName=GolfArcade.Shot.GolfClubs.DisplayName(club),clubYards=GolfArcade.Shot.GolfClubs.ReferenceDistanceYards(club),
                aimDegrees=Mathf.DeltaAngle((float)ballAt.HeadingTo(putting ? hole.Pin : hole.RecommendedTarget(ballAt)),(float)heading),
                ball=new ControllerPoint(minimapCamera.WorldToViewportPoint(hud.Map.Ball)),
                pin=new ControllerPoint(minimapCamera.WorldToViewportPoint(hud.Map.Pin)),
                landing=new ControllerPoint(minimapCamera.WorldToViewportPoint(hud.Map.Landing)),
                path=ControllerPoints(hud.Map.Path),reach=ControllerPoints(hud.Map.Reach),
                direction=new ControllerPoint(minimapCamera.transform.InverseTransformDirection(AimDirection()))
            };
            if(minimapTexture && renderedMapHole==hole.Number &&
               (controllerMapHole!=hole.Number || controllerMapTarget!=minimapTexture ||
                controllerMapPosition!=minimapCamera.transform.position || controllerMapSize!=minimapCamera.orthographicSize)) {
                var previous=RenderTexture.active;
                var rt=RenderTexture.GetTemporary(256,Mathf.RoundToInt(256f/minimapCamera.aspect),0,RenderTextureFormat.ARGB32);
                Texture2D cpu=null;
                try {
                    Graphics.Blit(minimapTexture,rt);RenderTexture.active=rt;
                    cpu=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);
                    cpu.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);cpu.Apply();
                    reading.mapImage=System.Convert.ToBase64String(cpu.EncodeToJPG(82));
                    controllerMapHole=hole.Number;controllerMapTarget=minimapTexture;
                    controllerMapPosition=minimapCamera.transform.position;controllerMapSize=minimapCamera.orthographicSize;
                } finally { RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);if(cpu) Destroy(cpu); }
            }
            if(networkConfigured)FillPartyReading(reading);
            return reading;
        }
        ControllerPoint[] ControllerPoints(System.Collections.Generic.List<Vector3> points) {
            int count=Mathf.Min(24,points.Count);var output=new ControllerPoint[count];
            for(int i=0;i<count;i++) {
                int at=count<2?0:Mathf.RoundToInt(i*(points.Count-1f)/(count-1));
                output[i]=new ControllerPoint(minimapCamera.WorldToViewportPoint(points[at]));
            }
            return output;
        }
        public string NativeFeedback() {
            if(Current==State.RoundDone) return $"Round complete · {Card.Total} strokes · {Card.ToPar:+0;-0;0} to par";
            if(Current==State.Result) return ShotResultTitle + " · " + ShotStatistics;
            if(LastShot!=null && Current==State.HoleDone) return $"Carry {LastShot.Carry:F0} yd · Total {LastShot.Total:F0} yd · {LastShot.Lie}";
            return $"{Current} · {Swing.Phase} · Load {Swing.Detector.Load:P0}";
        }
        public void NativeSwing(float power) {
            if(Current!=State.Aim || !NativeShotReady) return;
            golfer.ShowLoad(power);
            OnImpact(new SwingImpact { Power=power, Backswing=power, PeakSpeed=power*16, Commit=1.2, DownswingSeconds=.25, TempoSeconds=.7 });
        }
        public bool NativeShotReady { get; private set; }
        public void RequireNativeReady() {
            NativeShotReady = false; needsReadyPose = false;
            Swing.Detector.Reset();
        }
        bool needsReadyPose;
        int nativeLoadFrame = -1;
        public void NativeReady(System.Numerics.Quaternion? grip = null) {
            if (Current != State.Aim) return;
            NativeShotReady = true; Swing.Detector.UseReadyPose=true; needsReadyPose=true; Swing.Detector.Reset();
            if (grip.HasValue) CaptureNativeGrip(grip.Value);
        }
        void CaptureNativeGrip(System.Numerics.Quaternion q) {
            lastReadyGrip=q; Swing.Detector.SetReadyPose(q); needsReadyPose=false;
        }
        public void NativeMotion(in NativeSportsSession.Sample sample) {
            if(Current==State.Intro) {
                float speed=sample.rx*sample.rx+sample.ry*sample.ry+sample.rz*sample.rz;
                if(!float.IsNaN(speed) && !float.IsInfinity(speed) && speed>20) { presentationMotionConsumed=true; SkipPresentation(); }
                return;
            }
            if (Current != State.Aim || !NativeShotReady) return; // Follow-through and result-screen motions cannot arm a shot.
            var q=new System.Numerics.Quaternion(sample.qx,sample.qy,sample.qz,sample.qw);
            var rate = new System.Numerics.Vector3(sample.rx,sample.ry,sample.rz);
            var gravity = new System.Numerics.Vector3(sample.gx,sample.gy,sample.gz);
            if(float.IsNaN(q.LengthSquared()) || float.IsInfinity(q.LengthSquared()) || q.LengthSquared()<.5f ||
               float.IsNaN(rate.LengthSquared()) || float.IsInfinity(rate.LengthSquared())) return;
            if(needsReadyPose) {
                if (rate.Length() > 1.2f) return;
                CaptureNativeGrip(q);
                Debug.Log("[SportsMotion] Ready pose captured");
            }
            var detector = Swing.Detector;
            var before = detector.Phase;
            var e = detector.Ingest(sample.time,q,rate,gravity);
            if(e==null) return;
            switch(e.Value.Kind) {
                case SwingEventKind.Load:
                    if (detector.Phase == SwingPhase.Downswing && before != SwingPhase.Downswing) golfer.Strike();
                    else if (detector.Phase == SwingPhase.Backswing && nativeLoadFrame != Time.frameCount) {
                        nativeLoadFrame = Time.frameCount;
                        OnLoad(e.Value.Load);
                    }
                    break;
                case SwingEventKind.Cancel: OnCancel(); break;
                case SwingEventKind.Impact:
                    var impact=e.Value.Impact;
                    Debug.Log($"[SportsMotion] golf impact speed={impact.PeakSpeed:F2} power={impact.Power:F2}");
                    if(NativeSportsSession.Left) { impact.FaceDegrees*=-1; impact.CurveDegrees*=-1; impact.StartLineDegrees*=-1; }
                    OnImpact(impact); break;
            }
        }

    }
}
