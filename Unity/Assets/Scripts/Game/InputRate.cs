using UnityEngine;
using UnityEngine.Rendering;

namespace GolfArcade.Game
{
    /// Swing input at 120 Hz while the TV keeps its steady 60. The game reads the phone's motion once per loop, so at 60 a
    /// swing could wait up to 16.7 ms (8 on average) before the game, the strike and its haptic knew of it; at 120 that
    /// halves. Only every other loop is drawn (`OnDemandRendering`), so the TV still gets exactly the 60 frames AirPlay
    /// keeps steady. It needs a ProMotion phone (the app asks only then, and not in Low Power Mode) and a drawn frame that
    /// fits in 8.3 ms: if TV frames start arriving late (a slower phone, heat, Low Power Mode switched on mid-match) the
    /// session drops back to the plain 60 Hz loop and stays there.
    public sealed class InputRate : MonoBehaviour
    {
        public const int FastLoop = 120, TvRate = 60;
        /// TV frames are judged over `Window` seconds after `Settle` seconds of grace (loading, the first hitches).
        public const float Window = 2f, Settle = 3f;
        /// A TV frame is late past `LateFactor` of its 1/60 s slot; `LatePercent` of late frames in a window ends the fast loop.
        public const float LateFactor = 1.25f;
        public const int LatePercent = 10;

        /// True while swing input runs at 120 Hz. `Generation` changes whenever the loop rate changes, so frame-time
        /// watchers can start their window again instead of judging frames from the other rate.
        public static bool Fast { get; private set; }
        public static int Generation { get; private set; }
        static InputRate running;
        float settle, elapsed, sinceDrawn;
        int drawn, late;

        /// Once the match is up (after the scene's own frame-rate settings): `requestedHz` is 120 when the player has it on
        /// and the phone can (decided by the app, which knows the phone's panel and power mode).
        public static void Begin(GameObject host, bool external, int requestedHz)
        {
            Stop();
            if (!external || requestedHz < FastLoop) return;
            Application.targetFrameRate = FastLoop;
            OnDemandRendering.renderFrameInterval = FastLoop / TvRate;
            running = host.AddComponent<InputRate>();
            running.settle = Settle;
            Fast = true; Generation++;
            Debug.Log("[InputRate] swing input at 120 Hz, TV drawn at 60");
        }

        /// Back to one drawn frame per loop at 60 (leaving a match, the plaza, or a fallback).
        public static void Stop()
        {
            if (running) { if (Application.isPlaying) Destroy(running); else DestroyImmediate(running); }
            running = null;
            if (!Fast) return;
            Fast = false; Generation++;
            OnDemandRendering.renderFrameInterval = 1;
            Application.targetFrameRate = TvRate;
        }

        /// Whole numbers: 10 % of 120 frames is exactly 12 (`120 * .1f` is not).
        public static bool ShouldFallBack(int drawn, int late) => drawn > 0 && late * 100 >= drawn * LatePercent;

        void Update()
        {
            sinceDrawn += Time.unscaledDeltaTime;
            if (!OnDemandRendering.willCurrentFrameRender) return;
            float gap = sinceDrawn; sinceDrawn = 0;
            if (settle > 0) { settle -= gap; return; }
            drawn++; if (gap > LateFactor / TvRate) late++;
            elapsed += gap;
            if (elapsed < Window) return;
            if (ShouldFallBack(drawn, late))
            {
                Debug.Log($"[InputRate] {late}/{drawn} TV frames late with 120 Hz input: back to 60 Hz for this session");
                Stop();
                return;
            }
            elapsed = 0; drawn = late = 0;
        }

        void OnDestroy() { if (running == this) running = null; }
    }
}
