using System;
using System.Collections.Generic;
using System.Numerics;
using GolfArcade.Shot;

namespace GolfArcade.Swing
{
    /// Three deliberate practice strokes, using the same address/return detector as play.
    /// No balls or score are touched. The median resists one unusually hard or short swing.
    public sealed class GolfSwingCalibration
    {
        public readonly MotionSwingDetector Detector = new();
        readonly List<double> arcs = new(), rates = new();
        public int Count => arcs.Count;
        public bool Complete => Count == 3;
        public GolfSwingCalibration(Quaternion grip) {
            Detector.Configure(GolfClub.Driver);
            Detector.SetReadyPose(grip);
        }
        public SwingEvent? Ingest(double time, Quaternion attitude, Vector3 rate, Vector3 gravity) {
            if (Complete) return null;
            var e = Detector.Ingest(time, attitude, rate, gravity);
            if (e?.Kind == SwingEventKind.Impact) {
                var hit = e.Value.Impact;
                double arc = hit.Backswing * Detector.FullBackswing;
                if (arc >= .45 && hit.PeakSpeed >= 1.5 && hit.DownswingSeconds < 1.2) {
                    arcs.Add(arc); rates.Add(hit.PeakSpeed);
                }
            }
            return e;
        }
        public void Apply(MotionSwingDetector target) {
            if (!Complete) return;
            var a = arcs.ToArray(); var r = rates.ToArray(); Array.Sort(a); Array.Sort(r);
            target.CalibrateFullSwing(a[1], r[1]);
        }
    }
}
