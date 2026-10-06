using UnityEngine;
using GolfArcade.Swing;
namespace GolfArcade.Game {
    public sealed partial class GolfGame {
        public static event System.Action ShotStruck;
        public Camera GameplayCamera => rig ? rig.Camera : null;
        public double TutorialHeading => heading;
        public GolfArcade.UI.Hud TutorialHud => hud;
        public int TutorialHole => holeIndex;
        public void PrepareNativeAddress() {
            if (Match == null) StartRound();
            BeginAim(false); rig.SnapNext(); rig.ApplyFrame();
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
            OnImpact(new SwingImpact { Power=power, Backswing=power, PeakSpeed=power*16, TempoSeconds=.7 });
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
