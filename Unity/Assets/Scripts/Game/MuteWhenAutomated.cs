using UnityEngine;

namespace GolfArcade.Game
{
    /// Automated runs (batch-mode Unity: play-mode tests, captures, review recordings) are silent, so tooling
    /// never plays game audio through the developer's speakers. Builds and the interactive editor are unchanged.
    static class MuteWhenAutomated
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Mute()
        {
            if (Application.isBatchMode || System.Environment.GetEnvironmentVariable("GOLF_MUTE") == "1") AudioListener.volume = 0f;
        }
    }
}
