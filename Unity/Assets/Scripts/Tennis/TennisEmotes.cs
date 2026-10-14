using System;
using System.Linq;
namespace GolfArcade.Tennis {
    /// Stable ids shared with the native locker. Commands select one of three equipped slots.
    public static class TennisEmotes {
        public static readonly string[] IDs = { "scuba", "thrust", "spike", "wave", "bringIt", "pushups", "jumpJoy", "fistPump", "stomp", "slump", "shock", "victory", "defeat" };
        public static readonly string[] Defaults = { "wave", "scuba", "spike" };
        static readonly HeroTennisDriver.Clip[] Clips = {
            HeroTennisDriver.Clip.EmoteScuba, HeroTennisDriver.Clip.EmoteThrust, HeroTennisDriver.Clip.EmoteSpike,
            HeroTennisDriver.Clip.IntroWave, HeroTennisDriver.Clip.IntroBringIt, HeroTennisDriver.Clip.IntroPushups,
            HeroTennisDriver.Clip.EmoteJumpJoy, HeroTennisDriver.Clip.EmoteFistPump, HeroTennisDriver.Clip.EmoteStomp, HeroTennisDriver.Clip.EmoteSlump,
            HeroTennisDriver.Clip.EmoteShock, HeroTennisDriver.Clip.EmoteVictory, HeroTennisDriver.Clip.EmoteDefeat
        };
        // Golf exports these same authored match clips on its rig, without the tennis racket.
        static readonly string[] ExportNames = { "Emote_Scuba", "Emote_Thrust", "Emote_Spike", "Intro_Wave", "Intro_BringIt", "Intro_Pushups",
            "Emote_JumpJoy", "Emote_FistPump", "Emote_Stomp", "Emote_Slump", "Emote_Shock", "Emote_Victory", "Emote_Defeat" };
        static readonly string[] Names = { "Scuba", "Thrust", "Spike", "Wave", "Bring It", "Push-ups", "Jump for Joy", "Fist Pump", "Stomp", "Slump", "Shock", "Victory", "Defeat" };
        public static string ExportName(string id) { int i = Array.IndexOf(IDs, id); return i >= 0 ? ExportNames[i] : null; }
        public static string Name(string id) { int i = Array.IndexOf(IDs, id); return i >= 0 ? Names[i] : "Emote"; }
        static readonly float[] Durations = { 5.2f, 4f, 3.7f, 2.5f, 3.5f, 3.5f, 2.1f, 1.6f, 2.2f, 2.6f, 2.2f, 3.2f, 3.3f };
        public static string[] Normalize(string[] saved) => (saved ?? Defaults).Concat(Defaults).Concat(IDs).Where(id => Array.IndexOf(IDs,id)>=0).Distinct().Take(3).ToArray();
        // JsonUtility writes null arrays as empty arrays; both represent a legacy loadout.
        public static bool Valid(string[] saved) => saved == null || saved.Length == 0 || saved.Length == 3 && saved.Distinct().Count() == 3 && saved.All(id => Array.IndexOf(IDs,id)>=0);
        public static bool TryClip(string id, out HeroTennisDriver.Clip clip) { int i=Array.IndexOf(IDs,id); clip=i>=0?Clips[i]:default; return i>=0; }
        public static float Duration(string id) { int i=Array.IndexOf(IDs,id); return i>=0?Durations[i]:0; }
    }
}
