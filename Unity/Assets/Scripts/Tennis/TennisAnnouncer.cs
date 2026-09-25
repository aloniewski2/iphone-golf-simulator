using System.Collections.Generic;
using UnityEngine;

namespace GolfArcade.Tennis
{
    /// The match announcer: umpire score calls and the commentator's reactions, recorded
    /// voice lines in Resources/Tennis/Announcer (made by tools/make_announcer.py). Lines queue
    /// so calls never talk over each other; stale calls (a score overtaken by the next point)
    /// are dropped rather than read late. A line with numbered takes ("winner", "winner_2" ...)
    /// picks one at random, never the same one twice running.
    public sealed class TennisAnnouncer : MonoBehaviour
    {
        /// Every line the game asks for, with its script (the text the voice reads).
        public static readonly (string key, string text)[] Script =
        {
            ("fifteen_love", "Fifteen, love."), ("thirty_love", "Thirty, love."), ("forty_love", "Forty, love."),
            ("love_fifteen", "Love, fifteen."), ("love_thirty", "Love, thirty."), ("love_forty", "Love, forty."),
            ("fifteen_all", "Fifteen all."), ("thirty_all", "Thirty all."),
            ("thirty_fifteen", "Thirty, fifteen."), ("fifteen_thirty", "Fifteen, thirty."),
            ("forty_fifteen", "Forty, fifteen."), ("fifteen_forty", "Fifteen, forty."),
            ("forty_thirty", "Forty, thirty."), ("thirty_forty", "Thirty, forty."),
            ("deuce", "Deuce."), ("advantage_server", "Advantage, server."), ("advantage_receiver", "Advantage, receiver."),
            ("game", "Game."), ("game_point", "Game point."), ("break_point", "Break point!"), ("match_point", "Match point!"),
            ("game_set_match", "Game, set, and match!"),
            ("fault", "Fault."), ("double_fault", "Double fault."), ("out", "Out!"),
            ("quiet_please", "Quiet, please."), ("play", "Players ready. Play!"),
            ("welcome", "Welcome to the island, everyone! What a day for tennis."),
            ("ace", "Ace!"), ("ace_2", "What an ace! Didn't even see it!"),
            ("winner", "Winner!"), ("winner_2", "Oh, what a shot!"), ("winner_3", "Absolutely brilliant!"), ("winner_4", "Right on the line!"),
            ("smash", "Smashed it!"),
            ("rally", "What a rally!"), ("rally_2", "Incredible rally, this!"), ("rally_3", "They just won't miss!"),
            ("close", "Oh, so close!"), ("close_2", "Ooh, unlucky!"),
            ("comeback", "Right back in it!"),
            ("you_win", "What a performance! Game, set, and match!"), ("you_lose", "Hard luck. Better luck next time!"),
        };

        AudioSource voice;
        readonly Dictionary<string, List<AudioClip>> lines = new Dictionary<string, List<AudioClip>>();
        readonly Dictionary<string, AudioClip> last = new Dictionary<string, AudioClip>();
        readonly List<(string key, float at, float expires)> queue = new List<(string, float, float)>();
        float busyUntil;

        public bool Speaking => voice && voice.isPlaying;
        public float Volume = 1;

        public static TennisAnnouncer Create(Transform parent)
        {
            var go = new GameObject("Tennis announcer");
            go.transform.SetParent(parent, false);
            var a = go.AddComponent<TennisAnnouncer>();
            a.voice = go.AddComponent<AudioSource>(); a.voice.playOnAwake = false; a.voice.spatialBlend = 0; a.voice.priority = 8;
            foreach (var clip in Resources.LoadAll<AudioClip>("Tennis/Announcer"))
            {
                string key = clip.name;
                int us = key.LastIndexOf('_');
                if (us > 0 && int.TryParse(key.Substring(us + 1), out _)) key = key.Substring(0, us);
                if (!a.lines.TryGetValue(key, out var takes)) a.lines[key] = takes = new List<AudioClip>();
                takes.Add(clip);
            }
            return a;
        }

        public bool Has(string key) => lines.ContainsKey(key);

        /// Queue a line after `delay` seconds. It is dropped if it cannot start within
        /// `patience` seconds of that (the moment has passed).
        public void Say(string key, float delay = 0, float patience = 2.5f)
        {
            if (key == null || !lines.ContainsKey(key)) return;
            float at = Time.unscaledTime + delay;
            queue.Add((key, at, at + patience));
        }

        /// Forget everything queued (a new point is starting).
        public void Clear() => queue.Clear();

        void Update()
        {
            if (queue.Count == 0 || Time.unscaledTime < busyUntil || Speaking) return;
            float now = Time.unscaledTime;
            for (int i = 0; i < queue.Count; i++)
            {
                var (key, at, expires) = queue[i];
                if (now > expires) { queue.RemoveAt(i); i--; continue; }
                if (now < at) continue;
                queue.RemoveAt(i);
                var clip = Pick(key);
                voice.PlayOneShot(clip, Volume);
                busyUntil = now + clip.length + .12f;
                return;
            }
        }

        AudioClip Pick(string key)
        {
            var takes = lines[key];
            AudioClip clip = takes[Random.Range(0, takes.Count)];
            if (takes.Count > 1 && last.TryGetValue(key, out var previous) && previous == clip)
                clip = takes[(takes.IndexOf(clip) + 1) % takes.Count];
            last[key] = clip;
            return clip;
        }

        /// The umpire's key for the score, server first ("thirty_fifteen", "deuce", ...), or
        /// null when the game has just been won.
        public static string ScoreKey(TennisMatch m)
        {
            int server = m.PlayerServes ? m.PlayerPoints : m.OpponentPoints;
            int receiver = m.PlayerServes ? m.OpponentPoints : m.PlayerPoints;
            if (server == 0 && receiver == 0) return null;
            if (server >= 3 && receiver >= 3)
                return server == receiver ? "deuce" : server > receiver ? "advantage_server" : "advantage_receiver";
            string[] words = { "love", "fifteen", "thirty", "forty" };
            return server == receiver ? words[server] + "_all" : words[server] + "_" + words[receiver];
        }
    }
}
