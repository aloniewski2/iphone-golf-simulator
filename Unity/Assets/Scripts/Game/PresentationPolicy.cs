using System;
using UnityEngine;
namespace GolfArcade.Game {
    /// Content revision scopes first-view flags; skipping never writes a flag.
    public static class PresentationPolicy {
        public const int Revision = 1;
        public static PresentationCut Preference = PresentationCut.Full;
        public static bool Constrained, BigMoments = true, Multiplayer, HoleFlyover=true;
        public static event Action<string> Trace;
        public static void Configure(string preference, bool constrained, bool bigMoments, bool multiplayer) {
            Preference = preference == "off" ? PresentationCut.Off : preference == "short" ? PresentationCut.Short : PresentationCut.Full;
            Constrained = constrained; BigMoments = bigMoments; Multiplayer = multiplayer;
        }
        static string Key(string beat) => "presentation.v" + Revision + "." + beat;
        public static PresentationCut Cut(string beat) => PresentationDirector.Select(Preference, PlayerPrefs.GetInt(Key(beat), 0) == 1, Multiplayer, Constrained);
        public static void Seen(string beat) { PlayerPrefs.SetInt(Key(beat), 1); PlayerPrefs.Save(); }
        public static void Event(string beat, string state, int sequence, string detail = "") {
            var line = $"{Revision}|{sequence}|{beat}|{state}|{Time.realtimeSinceStartupAsDouble:F3}|{detail}";
            Debug.Log("[Presentation] " + line); Trace?.Invoke(line);
        }
    }
}
