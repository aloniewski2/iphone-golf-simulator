using UnityEngine;

namespace GolfArcade.Game
{
    /// Explicit -benchGolf device launches use the same public touch-swing API
    /// as a player. Course simulation, cameras and character animation run live.
    public sealed class GolfVisualBenchmark : MonoBehaviour
    {
        GolfGame game;
        GolfGame.State previous;
        float stateTime, elapsed;
        void Awake() { game=GetComponent<GolfGame>(); }
        void Update()
        {
            if (!game || Application.isEditor || Time.timeScale == 0 || game.Swing == null || elapsed >= 125) return;
            elapsed += Time.unscaledDeltaTime;
            if (game.Current != previous) { previous=game.Current; stateTime=0; }
            stateTime += Time.deltaTime;
            if (game.Current == GolfGame.State.Aim)
            {
                if (stateTime < 1.5f) game.ShowBackswing(Mathf.Clamp01((stateTime-.65f)/.85f)*.78f);
                else { game.NativeReady(); game.NativeSwing(.78f); }
            }
            else if ((game.Current == GolfGame.State.Result || game.Current == GolfGame.State.RoundDone) && stateTime > 2)
                game.NativeContinue();
        }
    }
}
