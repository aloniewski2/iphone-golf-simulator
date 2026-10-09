using UnityEngine;

namespace GolfArcade.Tennis
{
    /// Holds the frame rate when a phone cannot. If frames run consistently late -- a slower
    /// phone, or one that has heated up and throttled -- it steps rendering quality down one
    /// notch at a time. It never steps back up within a session: oscillating between two
    /// quality levels would itself read as a hitch.
    public sealed class TennisFrameGovernor : MonoBehaviour
    {
        /// Seconds of frames judged at a time, and the grace period after loading and after
        /// each change, when frame times are not representative.
        public const float Window = 2f, Settle = 4f;
        /// A window is "late" when this share of its frames missed their slot.
        public const float LateShare = .1f;
        float elapsed, settle = Settle;
        int frames, late;

        void Update()
        {
            if (Application.isEditor || Time.timeScale == 0) return;
            float dt = Time.unscaledDeltaTime;
            if (settle > 0) { settle -= dt; return; }
            float slot = 1f / Mathf.Max(30, Application.targetFrameRate);
            frames++; if (dt > slot * 1.25f) late++;
            elapsed += dt;
            if (elapsed < Window) return;
            if (ShouldStepDown(frames, late) && TennisQuality.StepDown())
            {
                Debug.Log($"[TennisQuality] {late}/{frames} late frames — stepped down (reduction {TennisQuality.Reductions})");
                settle = Settle;
            }
            elapsed = 0; frames = 0; late = 0;
        }

        public static bool ShouldStepDown(int frames, int late) => frames > 0 && late >= frames * LateShare;
    }

    /// First use of a shader or particle material compiles its pipeline state, which on
    /// Metal is a visible hitch -- traditionally on the first serve or the first contact.
    /// This renders everything that will appear during play once, into an offscreen target
    /// with the same format and sample count as the real one, while the scene is loading.
    public static class TennisWarmup
    {
        public static void Run(Camera gameplay, TennisFx fx)
        {
            if (!gameplay) return;
            var go = new GameObject("Shader warm-up camera");
            var cam = go.AddComponent<Camera>();
            cam.CopyFrom(gameplay);
            cam.enabled = false;
            var target = new RenderTexture(64, 64, 24, gameplay.allowHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default)
            { antiAliasing = Mathf.Max(1, QualitySettings.antiAliasing), name = "Warm-up target" };
            cam.targetTexture = target;
            // Fire each effect once in front of the warm-up camera, then render.
            Vector3 at = cam.transform.position + cam.transform.forward * 3;
            if (fx)
            {
                fx.Contact(at, Timing.Perfect, true);
                fx.Bounce(at, Vector3.forward * 10, Color.gray);
            }
            cam.Render();
            if (fx) fx.Clear();
            cam.targetTexture = null;
            target.Release(); Object.Destroy(target); Object.Destroy(go);
        }
    }
}
