using System;
using GolfArcade.Shot;
using GolfArcade.Swing;
using UnityEngine;

namespace GolfArcade.Game
{
    /// Feeds whatever motion source is live into the detector every frame and hands the game
    /// its events. Two sources: this device's own gyro, and the synthetic swing driven by the space
    /// bar or the hold-to-swing button. (In the app the native bridge feeds the detector instead.)
    public sealed class SwingController
    {
        public readonly MotionSwingDetector Detector = new();
        public PhoneMotionSource Phone { get; }
        public SyntheticMotionSource Synthetic { get; }
        public IMotionSource Source { get; private set; }
        /// True when this phone's gyro is the club.
        public bool UsingPhone => Source == Phone;
        public Action<double> OnLoad;
        public Action OnCancel;
        public Action<SwingImpact> OnImpact;
        public Action<IMotionSource> OnSourceChanged;
        public bool Armed;
        GolfClub club;

        public SwingController(bool forceSynthetic = false)
        {
            Phone = new PhoneMotionSource();
            Synthetic = new SyntheticMotionSource();
            Source = !forceSynthetic && Phone.IsAvailable ? Phone : Synthetic;
        }

        public void Start()
        {
            if(!NativeSportsSession.Active) Phone.Start();
            Synthetic.Start();
        }

        public void Stop()
        {
            Phone.Stop(); Synthetic.Stop();
        }

        public void SetClub(GolfClub club) { this.club = club; Detector.Configure(club); }

        public SwingPhase Phase => Detector.Phase;

        /// Drive the detector from `source` instead (demos and tests: a scripted phone stroke).
        public void UseSource(IMotionSource source)
        {
            Source = source;
            source.Start();
            Detector.Configure(club);
            OnSourceChanged?.Invoke(Source);
        }

        public void Update()
        {
            if(NativeSportsSession.Active) return; // Native bridge owns samples; no second sensor/detector loop.
            while (Source.TryRead(out var sample))
            {
                var e = Detector.Ingest(sample.Time, sample.Attitude, sample.RotationRate, sample.Gravity);
                if (!Armed || e == null) continue;
                switch (e.Value.Kind)
                {
                    case SwingEventKind.Load: OnLoad?.Invoke(e.Value.Load); break;
                    case SwingEventKind.Cancel: OnCancel?.Invoke(); break;
                    case SwingEventKind.Impact: OnImpact?.Invoke(e.Value.Impact); break;
                }
            }
        }

    }
}
