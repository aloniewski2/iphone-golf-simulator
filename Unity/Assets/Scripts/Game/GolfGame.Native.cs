using UnityEngine;
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
            NativeControlled = true;
            hud.HideMenu(); home = null;
            hud.HideCourses(); courses = null;
            hud.HideLocker(); stage?.Hide();
            if (bigScreen) bigScreen.enabled = false;
            SetFog(0);
            var selected = GolfArcade.Course.Course.ByKey(courseKey ?? "cliffside") ?? GolfArcade.Course.Course.Cliffside();
            setup = GameSetup.Solo(ProfileStore.Active, selected.Key);
            StartRound();
            BeginAim(false); hud.ShowPlayHud(true); RefreshControls();
            rig.SnapNext(); rig.ApplyFrame();
        }
        public void NativeContinue() {
            if (Current == State.Result) { ContinueShotResult(); return; }
            if (Current != State.RoundDone) return;
            if (NativeHasNextHole) NextHole(); else PlayAgain();
            if (Current == State.Intro) BeginAim(false);
        }
        public void NativeAim(float value) { if(Current==State.Aim) Nudge(Mathf.Clamp(value,-1,1)*AimTapDegrees); }
        public void NativeClub(int value) { if(Current==State.Aim) CycleClub(value); }
        public string NativeFeedback() {
            if(Current==State.RoundDone) return $"Round complete · {Card.Total} strokes · {Card.ToPar:+0;-0;0} to par";
            if(Current==State.Result) return ShotResultTitle + " · " + ShotStatistics;
            if(LastShot!=null && Current==State.HoleDone) return $"Carry {LastShot.Carry:F0} yd · Total {LastShot.Total:F0} yd · {LastShot.Lie}";
            return $"{Current} · {Swing.Phase} · Load {Swing.Detector.Load:P0}";
        }
        public void NativeSwing(float power) {
            if(Current!=State.Aim || !NativeShotReady || NativeCalibrating) return;
            golfer.ShowLoad(power);
            OnImpact(new SwingImpact { Power=power, Backswing=power, PeakSpeed=power*16, Commit=1.2, DownswingSeconds=.25, TempoSeconds=.7 });
        }
        public bool NativeShotReady { get; private set; }
        public void RequireNativeReady() {
            NativeShotReady = false; needsReadyPose = false;
            Swing.Detector.Reset();
        }
        bool needsReadyPose;
        GolfSwingCalibration golfCalibration;
        bool calibrationRequested;
        int nativeLoadFrame = -1;
        public bool NativeCalibrating => calibrationRequested;
        public int NativeCalibrationCount => golfCalibration?.Count ?? 0;
        public void StartGolfCalibration(System.Numerics.Quaternion? grip = null) {
            if (Current != State.Aim) return;
            golfCalibration = null; calibrationRequested = true; OnCancel(); NativeReady(grip);
        }
        public void FinishGolfCalibration(bool apply = true) {
            if (apply && golfCalibration != null && golfCalibration.Complete) golfCalibration.Apply(Swing.Detector);
            calibrationRequested = false; golfCalibration = null; RequireNativeReady(); OnCancel();
        }
        public void NativeReady(System.Numerics.Quaternion? grip = null) {
            if (Current != State.Aim) return;
            NativeShotReady = true; Swing.Detector.UseReadyPose=true; needsReadyPose=true; Swing.Detector.Reset();
            if (grip.HasValue) CaptureNativeGrip(grip.Value);
        }
        void CaptureNativeGrip(System.Numerics.Quaternion q) {
            Swing.Detector.SetReadyPose(q); needsReadyPose=false;
            if (calibrationRequested) {
                if (golfCalibration == null) golfCalibration = new GolfSwingCalibration(q);
                else golfCalibration.Detector.SetReadyPose(q);
            }
        }
        public void NativeMotion(in NativeSportsSession.Sample sample) {
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
            var detector = calibrationRequested ? golfCalibration.Detector : Swing.Detector;
            var before = detector.Phase;
            var e = calibrationRequested ? golfCalibration.Ingest(sample.time,q,rate,gravity) : detector.Ingest(sample.time,q,rate,gravity);
            if(e==null) return;
            switch(e.Value.Kind) {
                case SwingEventKind.Load:
                    if (detector.Phase == SwingPhase.Downswing && before != SwingPhase.Downswing) golfer.Strike();
                    else if (detector.Phase == SwingPhase.Backswing && nativeLoadFrame != Time.frameCount) {
                        nativeLoadFrame = Time.frameCount;
                        if (calibrationRequested) golfer.ShowLoad((float)e.Value.Load);
                        else OnLoad(e.Value.Load);
                    }
                    break;
                case SwingEventKind.Cancel: OnCancel(); break;
                case SwingEventKind.Impact:
                    if (calibrationRequested) {
                        RequireNativeReady();
                        golfer.Strike(atImpact:true);
                        hud.SetStatus($"Practice swings {NativeCalibrationCount}/3 · tap Ready for the next practice swing");
                        break;
                    }
                    var impact=e.Value.Impact;
                    Debug.Log($"[SportsMotion] golf impact speed={impact.PeakSpeed:F2} power={impact.Power:F2}");
                    if(NativeSportsSession.Left) { impact.FaceDegrees*=-1; impact.CurveDegrees*=-1; impact.StartLineDegrees*=-1; }
                    OnImpact(impact); break;
            }
        }

    }
}
