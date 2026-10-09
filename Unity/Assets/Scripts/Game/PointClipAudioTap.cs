using System.Runtime.InteropServices;
using UnityEngine;

namespace GolfArcade {
    /// Listener tap: mixed game sound only. Never opens the microphone.
    public sealed class PointClipAudioTap : MonoBehaviour {
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] static extern double SportsClock();
        [DllImport("__Internal")] static extern void SportsRecorderAudio([In] float[] samples,int count,int channels,int rate,double timestamp);
#endif
        int sampleRate;
        double clockOffset;
        void OnEnable() {
            sampleRate=AudioSettings.outputSampleRate;
#if UNITY_IOS && !UNITY_EDITOR
            clockOffset=SportsClock()-AudioSettings.dspTime;
#endif
        }
        void OnApplicationPause(bool paused) { if(!paused) OnEnable(); }
        void OnAudioFilterRead(float[] data,int channels) {
#if UNITY_IOS && !UNITY_EDITOR
            SportsRecorderAudio(data,data.Length,channels,sampleRate,AudioSettings.dspTime+clockOffset);
#endif
        }
    }
}
