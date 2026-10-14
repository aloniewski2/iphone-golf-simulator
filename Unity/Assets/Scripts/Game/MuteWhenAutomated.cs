using UnityEngine;

namespace GolfArcade.Game
{
    /// Automated runs (batch-mode Unity: play-mode tests, captures, review recordings) are silent, so tooling
    /// never plays game audio through the developer's speakers. Builds and the interactive editor are unchanged.
    public static class MuteWhenAutomated
    {
        /// True for batch-mode runs and any run started with GOLF_MUTE=1.
        public static readonly bool Muted = Application.isBatchMode || System.Environment.GetEnvironmentVariable("GOLF_MUTE") == "1";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Mute()
        {
            if (Muted) AudioListener.volume = 0f;
        }
        /// The volume a session asks for, kept at 0 on a muted run (a launch's "sound on" must not undo the mute).
        public static float Volume(bool on) => on && !Muted ? 1f : 0f;
    }
}
