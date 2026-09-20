using System;
using GolfArcade.Shot;
using GolfArcade.Swing;
using UnityEngine;

namespace GolfArcade.Game
{
    /// Feeds whatever motion source is live into the detector once a frame and hands the
    /// game its events. On the phone that is the gyro; in the editor it is the synthetic
    /// swing driven by the space bar or the hold-to-swing button.
    public sealed class SwingController
    {
        public readonly MotionSwingDetector Detector = new();
        public IMotionSource Source { get; }
        public SyntheticMotionSource Synthetic { get; }
        public bool UsingPhone { get; }
        public Action<double> OnLoad;
        public Action OnCancel;
        public Action<SwingImpact> OnImpact;
        public bool Armed;

        public SwingController(bool forceSynthetic = false)
        {
            var phone = new PhoneMotionSource();
            UsingPhone = phone.IsAvailable && !forceSynthetic;
            if (UsingPhone) Source = phone;
            else { Synthetic = new SyntheticMotionSource(); Source = Synthetic; }
        }

        public void Start() => Source.Start();
        public void Stop() => Source.Stop();

        public void SetClub(GolfClub club) => Detector.Configure(club);

        public SwingPhase Phase => Detector.Phase;

        public void Update()
        {
            if (!Source.TryRead(out var sample)) return;
            var e = Detector.Ingest(sample.Time, sample.Attitude, sample.RotationRate);
            if (!Armed || e == null) return;
            switch (e.Value.Kind)
            {
                case SwingEventKind.Load: OnLoad?.Invoke(e.Value.Load); break;
                case SwingEventKind.Cancel: OnCancel?.Invoke(); break;
                case SwingEventKind.Impact: OnImpact?.Invoke(e.Value.Impact); break;
            }
        }
    }
}
