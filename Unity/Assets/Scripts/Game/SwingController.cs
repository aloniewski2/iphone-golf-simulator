using System;
using GolfArcade.Net;
using GolfArcade.Shot;
using GolfArcade.Swing;
using UnityEngine;

namespace GolfArcade.Game
{
    /// Feeds whatever motion source is live into the detector every frame and hands the game
    /// its events. Three sources, in order of preference: a phone on the network holding the
    /// game as its club (Wii style, this machine is the screen), this device's own gyro, and
    /// the synthetic swing driven by the space bar or the hold-to-swing button. The network
    /// club can arrive or drop at any time; the detector is reset whenever the source changes.
    public sealed class SwingController
    {
        public readonly MotionSwingDetector Detector = new();
        public PhoneMotionSource Phone { get; }
        public SyntheticMotionSource Synthetic { get; }
        public NetworkMotionSource Network { get; }
        public IMotionSource Source { get; private set; }
        /// True when a real gyro (this phone's or a networked one) is the club.
        public bool UsingPhone => Source == Phone || Source == Network;
        public bool UsingNetwork => Source == Network;
        public Action<double> OnLoad;
        public Action OnCancel;
        public Action<SwingImpact> OnImpact;
        public Action<IMotionSource> OnSourceChanged;
        public bool Armed;
        GolfClub club;

        public SwingController(bool forceSynthetic = false, bool listenForController = true)
        {
            Phone = new PhoneMotionSource();
            Synthetic = new SyntheticMotionSource();
            if (listenForController)
            {
                try { Network = new NetworkMotionSource(() => Time.unscaledTimeAsDouble); }
                catch (Exception e) { Debug.LogWarning($"Controller listener unavailable: {e.Message}"); }
            }
            Source = !forceSynthetic && Phone.IsAvailable ? Phone : Synthetic;
        }

        public void Start()
        {
            Phone.Start(); Synthetic.Start(); Network?.Start();
        }

        public void Stop()
        {
            Phone.Stop(); Synthetic.Stop(); Network?.Stop();
        }

        public void SetClub(GolfClub club) { this.club = club; Detector.Configure(club); }

        public SwingPhase Phase => Detector.Phase;

        public void Update()
        {
            ChooseSource();
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

        /// A connected controller takes over from whatever was driving; when it goes quiet the
        /// previous source comes back. Either way the detector starts clean.
        void ChooseSource()
        {
            IMotionSource want;
            if (Network != null && Network.IsConnected) want = Network;
            else if (Source == Network) want = Phone.IsAvailable ? Phone : Synthetic;
            else want = Source;
            if (want == Source) return;
            Source = want;
            Detector.Configure(club);
            OnSourceChanged?.Invoke(Source);
        }
    }
}
