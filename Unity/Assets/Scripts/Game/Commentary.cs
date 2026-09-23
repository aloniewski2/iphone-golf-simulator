using System.Collections.Generic;
using UnityEngine;

namespace GolfArcade.Game
{
    /// The commentator in the booth: short lines recorded with Higgsfield (Resources/Audio/
    /// Commentary, one clip per line, named <moment>_<n>), picked at random for the moment and
    /// never the same line twice running. Keeps quiet when a line is still playing or one was
    /// said a moment ago, so the booth comments on the round rather than narrating every frame.
    public sealed class Commentary : MonoBehaviour
    {
        readonly Dictionary<string, List<AudioClip>> lines = new();
        readonly Dictionary<string, AudioClip> lastSaid = new();
        AudioSource voice;
        float quietUntil;
        public bool Muted;

        public static Commentary Create(Transform parent)
        {
            var go = new GameObject("Commentary");
            go.transform.SetParent(parent, false);
            var c = go.AddComponent<Commentary>();
            c.voice = go.AddComponent<AudioSource>();
            c.voice.playOnAwake = false; c.voice.spatialBlend = 0; c.voice.volume = 0.9f;
            foreach (var clip in Resources.LoadAll<AudioClip>("Audio/Commentary"))
            {
                int cut = clip.name.LastIndexOf('_');
                string key = cut > 0 ? clip.name.Substring(0, cut) : clip.name;
                if (!c.lines.TryGetValue(key, out var list)) c.lines[key] = list = new List<AudioClip>();
                list.Add(clip);
            }
            return c;
        }

        public bool Has(string moment) => lines.ContainsKey(moment);
        public bool Speaking => voice.isPlaying;

        /// Say something for `moment` (e.g. "perfect", "water"), `chance` of the time, after
        /// `delay` seconds. `interrupt` cuts off whatever is being said (a holed ball matters more).
        public bool Say(string moment, float chance = 1f, float delay = 0f, bool interrupt = false)
        {
            if (Muted || !lines.TryGetValue(moment, out var list) || list.Count == 0) return false;
            if (!interrupt && (voice.isPlaying || Time.unscaledTime < quietUntil)) return false;
            if (Random.value > chance) return false;
            lastSaid.TryGetValue(moment, out var last);
            AudioClip pick;
            do pick = list[Random.Range(0, list.Count)]; while (list.Count > 1 && pick == last);
            lastSaid[moment] = pick;
            voice.Stop();
            voice.clip = pick;
            voice.PlayDelayed(delay);
            quietUntil = Time.unscaledTime + delay + pick.length + 0.6f;
            return true;
        }

        public void Hush() { voice.Stop(); quietUntil = 0; }
    }
}
