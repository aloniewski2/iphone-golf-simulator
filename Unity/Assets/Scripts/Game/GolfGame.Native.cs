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
            if (Current != State.RoundDone) return;
            if (NativeHasNextHole) NextHole(); else PlayAgain();
            if (Current == State.Intro) BeginAim(false);
        }
        public void NativeAim(float value) { if(Current==State.Aim) Nudge(Mathf.Clamp(value,-1,1)*AimTapDegrees); }
        public void NativeClub(int value) { if(Current==State.Aim) CycleClub(value); }
        public string NativeFeedback() {
            if(Current==State.RoundDone) return $"Round complete · {Card.Total} strokes · {Card.ToPar:+0;-0;0} to par";
            if(LastShot!=null && (Current==State.Result || Current==State.HoleDone)) return $"Carry {LastShot.Carry:F0} yd · Total {LastShot.Total:F0} yd · {LastShot.Lie}";
            return $"{Current} · {Swing.Phase} · Load {Swing.Detector.Load:P0}";
        }
        public void NativeSwing(float power) {
            if(Current!=State.Aim) return;
            golfer.ShowLoad(power);
            OnImpact(new SwingImpact { Power=power, Backswing=power, PeakSpeed=power*16, Commit=1.2, DownswingSeconds=.25, TempoSeconds=.7 });
        }
        bool needsReadyPose;
        public void NativeReady() { Swing.Detector.UseReadyPose=true; needsReadyPose=true; Swing.Detector.Reset(); }
        public void NativeMotion(in NativeSportsSession.Sample sample) {
            var q=new System.Numerics.Quaternion(sample.qx,sample.qy,sample.qz,sample.qw);
            if(q.LengthSquared()<.5f) return;
            if(needsReadyPose) { Swing.Detector.SetReadyPose(q); needsReadyPose=false; Debug.Log("[SportsMotion] Ready pose captured"); }
            var e=Swing.Detector.Ingest(sample.time,q,new System.Numerics.Vector3(sample.rx,sample.ry,sample.rz),new System.Numerics.Vector3(sample.gx,sample.gy,sample.gz));
            if(e==null || Current!=State.Aim) return;
            switch(e.Value.Kind) {
                case SwingEventKind.Load: OnLoad(e.Value.Load); break;
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
