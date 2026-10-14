using System.Runtime.InteropServices;
using UnityEngine;

namespace GolfArcade {
    /// Keeps a PointClipAudioTap on the current AudioListener for as long as a TV session recording
    /// is running, so game audio is captured in every scene (the plaza and each match have their own
    /// listener). Does nothing while not recording and never removes a tap.
    /// Lives on the persistent "NativeSportsSession" object (DontDestroyOnLoad).
    public sealed class CaptureAudioTapKeeper : MonoBehaviour {
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] static extern int SportsCaptureActive();
#else
        static int SportsCaptureActive() { return 0; }
#endif
        const float Interval = 0.5f;
        float next;

        void Update() {
            if (Time.unscaledTime < next) return;
            next = Time.unscaledTime + Interval;
            if (SportsCaptureActive() != 1) return;
            var listener = FindFirstObjectByType<AudioListener>();
            if (listener == null || !listener.isActiveAndEnabled) return;
            // Never a second tap on a listener that has one: two taps would feed the recorder twice.
            var tap = listener.GetComponent<PointClipAudioTap>();
            if (tap == null) listener.gameObject.AddComponent<PointClipAudioTap>();
            else if (!tap.enabled) tap.enabled = true;
        }
    }
}
