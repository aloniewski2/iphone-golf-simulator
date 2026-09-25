using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using static GolfArcade.Tennis.TennisSynth;

namespace GolfArcade.Tennis
{
    /// The resort's soundtrack: a sunny calypso in F -- steel pan melody, marimba off-beats, a
    /// plucked bass and hand percussion -- composed and rendered at start-up (no audio files
    /// ship). It is three synchronised stems so the mix can follow the match: everything
    /// between points, the lead dropping out for the serve, and only a low groove under a
    /// rally, where the ball and the crowd carry the sound. Short stingers mark points, games
    /// and the match.
    public sealed class TennisMusic : MonoBehaviour
    {
        public enum Sting { Point, Winner, Game, MatchWon, MatchLost }
        const int Rate = 32000;
        const double Bpm = 112, Beat = 60 / Bpm;
        const int Bars = 16;

        // Chord per bar: MIDI root and whether it is minor. F C Dm Bb | F C Bb C | Dm Bb F C | Dm Bb Gm C
        static readonly (int root, bool minor)[] Chords =
        {
            (53, false), (48, false), (50, true), (46, false), (53, false), (48, false), (46, false), (48, false),
            (50, true), (46, false), (53, false), (48, false), (50, true), (46, false), (43, true), (48, false),
        };
        static readonly int[] Scale = { 0, 2, 4, 5, 7, 9, 11 };             // F major, from F

        AudioSource beat, keys, lead, stings;
        readonly Dictionary<Sting, AudioClip> stingClips = new Dictionary<Sting, AudioClip>();
        Task<float[][]> rendering;
        bool started;
        float wantBeat, wantKeys, wantLead, duck = 1, master = .55f;

        public bool Ready => started;
        /// Overall music level (0..1), e.g. from a settings slider.
        public float Master { get => master; set => master = Mathf.Clamp01(value); }

        public static TennisMusic Create(Transform parent)
        {
            var go = new GameObject("Tennis music");
            go.transform.SetParent(parent, false);
            var m = go.AddComponent<TennisMusic>();
            AudioSource Source(bool loop)
            {
                var s = go.AddComponent<AudioSource>(); s.playOnAwake = false; s.loop = loop; s.spatialBlend = 0; s.volume = 0; s.priority = 32;
                return s;
            }
            m.beat = Source(true); m.keys = Source(true); m.lead = Source(true); m.stings = Source(false); m.stings.volume = 1;
            m.rendering = Task.Run(RenderStems);
            return m;
        }

        /// Stem levels (0..1) the mix eases toward.
        public void SetMix(float beatLevel, float keysLevel, float leadLevel) { wantBeat = beatLevel; wantKeys = keysLevel; wantLead = leadLevel; }
        /// Temporary attenuation (e.g. while the announcer speaks).
        public void Duck(float level) => duck = Mathf.Clamp01(level);

        public void Play(Sting sting, float volume = .8f)
        {
            if (!stingClips.TryGetValue(sting, out var clip))
            {
                clip = MakeClip("Sting " + sting, RenderSting(sting));
                stingClips[sting] = clip;
            }
            stings.PlayOneShot(clip, volume * master * 1.3f);
        }

        void Update()
        {
            if (!started && rendering != null && rendering.IsCompleted)
            {
                if (rendering.Status == TaskStatus.RanToCompletion)
                {
                    var stems = rendering.Result;
                    beat.clip = MakeClip("Music beat", stems[0]); keys.clip = MakeClip("Music keys", stems[1]); lead.clip = MakeClip("Music lead", stems[2]);
                    // Start all three on the same audio-clock sample so they stay locked.
                    double at = AudioSettings.dspTime + .1;
                    beat.PlayScheduled(at); keys.PlayScheduled(at); lead.PlayScheduled(at);
                    started = true;
                }
                rendering = null;
            }
            if (!started) return;
            float dt = Time.unscaledDeltaTime, rate = dt * 1.4f;
            beat.volume = Mathf.MoveTowards(beat.volume, wantBeat * duck * master, rate);
            keys.volume = Mathf.MoveTowards(keys.volume, wantKeys * duck * master, rate);
            lead.volume = Mathf.MoveTowards(lead.volume, wantLead * duck * master, rate);
        }

        static AudioClip MakeClip(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static int At(double beats) => (int)(beats * Beat * Rate);
        static double Hz(int midi) => Midi(midi);

        static int[] Triad(int bar)
        {
            var (root, minor) = Chords[bar];
            return new[] { root, root + (minor ? 3 : 4), root + 7 };
        }

        /// Three stems: [beat + bass, keys (marimba + pad), lead (steel pan)].
        static float[][] RenderStems()
        {
            int length = At(Bars * 4);
            var beatStem = new float[length + Rate]; var keysStem = new float[length + Rate]; var leadStem = new float[length + Rate];
            var rng = new System.Random(1717);
            for (int bar = 0; bar < Bars; bar++)
            {
                double b0 = bar * 4;
                var (root, _) = Chords[bar];
                var triad = Triad(bar);
                // Percussion: soft kick on 1 and 3, rim on 2 and 4, shaker sixteenths.
                foreach (double k in new[] { 0.0, 2.0 }) Mix(beatStem, At(b0 + k), Rate / 2, i => Kick((double)i / Rate), .75);
                foreach (double k in new[] { 1.0, 3.0 })
                {
                    var bp = Biquad.BandPass(2200, 1.2, Rate); var r = new System.Random(bar * 13 + (int)k);
                    Mix(beatStem, At(b0 + k), Rate / 8, i => bp.Process(Noise(r)) * Math.Exp(-(double)i / Rate / .035), .45);
                }
                for (int s = 0; s < 16; s++)
                {
                    var hp = Biquad.HighPass(6500, .7, Rate); var r = new System.Random(bar * 97 + s);
                    double v = s % 4 == 2 ? .34 : s % 2 == 1 ? .16 : .22;
                    Mix(beatStem, At(b0 + s * .25), Rate / 16, i => hp.Process(Noise(r)) * Math.Exp(-(double)i / Rate / .028), v);
                }
                // Son clave, 3-2 over two bars.
                int[] clave = bar % 2 == 0 ? new[] { 0, 6, 12 } : new[] { 4, 8 };
                foreach (int s in clave) Mix(beatStem, At(b0 + s * .25), Rate / 5, i => Clave((double)i / Rate), .22);
                // Calypso bass: root, fifth on the "and" of 2, root on 3, octave on the "and" of 4.
                (double at, int note, double len)[] bassline = { (0, root - 12, .9), (1.5, root - 5, .45), (2, root - 12, .9), (3.5, root, .4) };
                foreach (var (at, note, len) in bassline)
                {
                    int nt = note; double l = len * Beat;
                    Mix(beatStem, At(b0 + at), (int)((l + .2) * Rate), i => Bass((double)i / Rate, Hz(nt), l), .5);
                }
                // Keys: marimba stabs on the off-beats, a pad under the whole bar.
                for (int e = 0; e < 4; e++)
                    foreach (int n in triad)
                    {
                        int nt = n + 12;
                        Mix(keysStem, At(b0 + e + .5), Rate, i => Marimba((double)i / Rate, Hz(nt)), .16);
                    }
                foreach (int n in triad)
                {
                    int nt = n; double l = 4 * Beat;
                    Mix(keysStem, At(b0), (int)((l + .6) * Rate), i => Pad((double)i / Rate, Hz(nt), l), .07);
                }
            }
            // Lead: a steel-pan tune, phrased in four-bar sentences (A A' B B').
            foreach (var (at, note, len) in Melody())
            {
                int nt = note;
                Mix(leadStem, At(at), (int)((Math.Max(len * Beat, .6) + .3) * Rate), i => SteelPan((double)i / Rate, Hz(nt)), .42);
                // A soft echo a dotted eighth later, the way pans ring in the open air.
                Mix(leadStem, At(at + .75), Rate, i => SteelPan((double)i / Rate, Hz(nt)), .12);
            }
            int fold = At(4) / 8;
            var stems = new[] { beatStem, keysStem, leadStem };
            for (int s = 0; s < 3; s++)
            {
                // Fold the tail (ringing notes past the loop point) back onto the start.
                var st = stems[s];
                for (int i = 0; i < Rate; i++) st[i] += st[length + i];
                Array.Resize(ref st, length);
                stems[s] = Normalize(st, s == 0 ? .8f : .7f);
            }
            return stems;
        }

        /// The tune: note onsets in beats, MIDI notes, lengths in beats. Rhythms come from a
        /// small book of calypso cells; pitches favour chord tones on strong beats and step
        /// through the scale between them.
        static List<(double at, int note, double len)> Melody()
        {
            var cells = new[]
            {
                new[] { (0.0, 1.0), (1.0, .5), (1.5, 1.0), (2.5, .5), (3.0, 1.0) },
                new[] { (0.0, .5), (.5, .5), (1.0, 1.5), (2.5, .5), (3.0, .5), (3.5, .5) },
                new[] { (0.0, 1.5), (1.5, 1.5), (3.0, 1.0) },
                new[] { (.5, .5), (1.0, .5), (1.5, 1.0), (2.5, 1.5) },
            };
            int[] plan = { 0, 1, 0, 2, 0, 1, 3, 2, 1, 1, 0, 2, 1, 3, 0, 2 };
            var notes = new List<(double, int, double)>();
            var rng = new System.Random(4242);
            int current = 72;                                                    // C5
            for (int bar = 0; bar < Bars; bar++)
            {
                // The second phrase of each pair answers the first: the same rhythm, and pitches
                // resolving toward the tonic in the last bar.
                var cell = cells[plan[bar]];
                var triad = Triad(bar);
                for (int k = 0; k < cell.Length; k++)
                {
                    var (at, len) = cell[k];
                    bool strong = Math.Abs(at % 1) < .01;
                    int target;
                    if (bar == Bars - 1 && k == cell.Length - 1) target = 77;       // F5 to close
                    else if (strong) target = Nearest(triad, current, rng);
                    else target = StepInScale(current, rng.Next(2) == 0 ? -1 : 1);
                    target = Math.Max(67, Math.Min(86, target));
                    notes.Add((bar * 4 + at, target, len));
                    current = target;
                }
            }
            return notes;
        }

        static int Nearest(int[] triad, int from, System.Random rng)
        {
            int best = from, bestDist = 99;
            foreach (int t in triad)
                for (int o = 60; o <= 96; o += 12)
                {
                    int n = t % 12 + o; int d = Math.Abs(n - from) + (n == from ? 3 : 0);
                    if (d < bestDist || (d == bestDist && rng.Next(2) == 0)) { best = n; bestDist = d; }
                }
            return best;
        }

        static int StepInScale(int note, int dir)
        {
            for (int n = note + dir; ; n += dir)
                if (Array.IndexOf(Scale, ((n - 53) % 12 + 12) % 12) >= 0) return n;
        }

        static float[] RenderSting(Sting sting)
        {
            (double at, int note, double len, bool pan)[] line = sting switch
            {
                Sting.Point => new[] { (0.0, 77, .3, true), (.09, 81, .3, true), (.18, 84, .6, true) },
                Sting.Winner => new[] { (0.0, 77, .2, true), (.07, 81, .2, true), (.14, 84, .2, true), (.21, 89, .8, true), (.21, 65, .8, false), (.21, 69, .8, false), (.21, 72, .8, false) },
                Sting.Game => new[] { (0.0, 72, .25, true), (.12, 77, .25, true), (.24, 84, .25, true), (.36, 89, 1.0, true), (.36, 53, 1.0, false), (.36, 57, 1.0, false), (.36, 60, 1.0, false) },
                Sting.MatchWon => new[]
                {
                    (0.0, 70, .2, true), (.14, 72, .2, true), (.28, 74, .2, true), (.42, 77, .4, true),
                    (.7, 81, .2, true), (.84, 79, .2, true), (.98, 81, .2, true), (1.12, 84, .4, true), (1.4, 89, 1.2, true),
                    (0.0, 46, 1.4, false), (0.0, 50, 1.4, false), (.7, 48, .7, false), (.7, 52, .7, false),
                    (1.4, 53, 1.6, false), (1.4, 57, 1.6, false), (1.4, 60, 1.6, false),
                },
                _ => new[] { (0.0, 81, .35, true), (.3, 77, .35, true), (.6, 74, 1.0, true), (.6, 50, 1.2, false), (.6, 53, 1.2, false), (.6, 57, 1.2, false) },
            };
            double end = 0;
            foreach (var n in line) end = Math.Max(end, n.at + n.len + .8);
            var data = new float[(int)(end * Rate)];
            foreach (var (at, note, len, pan) in line)
            {
                int nt = note; double l = len;
                if (pan) Mix(data, (int)(at * Rate), (int)((l + .7) * Rate), i => SteelPan((double)i / Rate, Hz(nt)), .5);
                else Mix(data, (int)(at * Rate), (int)((l + .6) * Rate), i => Pad((double)i / Rate, Hz(nt), l) * .6 + Marimba((double)i / Rate, Hz(nt + 12)) * .3, .35);
            }
            if (sting == Sting.Winner || sting == Sting.MatchWon)
            {
                // A shaker roll into the last chord.
                var hp = Biquad.HighPass(5500, .7, Rate); var r = new System.Random(3);
                double peak = sting == Sting.Winner ? .21 : 1.4;
                Mix(data, 0, (int)((peak + .5) * Rate), i =>
                {
                    double t = (double)i / Rate;
                    double env = t < peak ? t / peak : Math.Exp(-(t - peak) / .12);
                    return hp.Process(Noise(r)) * env * (.6 + .4 * Math.Sin(2 * Math.PI * 18 * t));
                }, .3);
            }
            return Normalize(data, .85f);
        }
    }
}
