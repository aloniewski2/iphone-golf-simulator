using System;
using System.Threading.Tasks;
using UnityEngine;
using static GolfArcade.Tennis.TennisSynth;

namespace GolfArcade.Tennis
{
    /// The world around the court: surf and breeze with the odd gull, the murmur of the
    /// stands, and the crowd's voice -- an "ooh" at a near miss, a groan, a roar for a winner --
    /// plus the players' shoes on the hard court. Everything is synthesised off the main
    /// thread at start-up; until a sound is ready its call is simply skipped.
    public sealed class TennisAmbience : MonoBehaviour
    {
        const int Rate = 32000;
        AudioSource beach, murmur, crowd, feet;
        float[][] rendered;
        AudioClip ooh, groan, cheer, roar, squeakA, squeakB, squeakC, land;
        Task<float[][]> task;
        float murmurWant = .22f, beachWant = .3f, hush;

        public static TennisAmbience Create(Transform parent)
        {
            var go = new GameObject("Tennis ambience");
            go.transform.SetParent(parent, false);
            var a = go.AddComponent<TennisAmbience>();
            AudioSource Source(bool loop, int priority)
            {
                var s = go.AddComponent<AudioSource>(); s.playOnAwake = false; s.loop = loop; s.spatialBlend = 0; s.priority = priority;
                return s;
            }
            a.beach = Source(true, 64); a.murmur = Source(true, 64); a.crowd = Source(false, 40); a.feet = Source(false, 96);
            a.task = Task.Run(Render);
            return a;
        }

        /// Quiet the stands (a serve at match point); 0 = normal, 1 = silent.
        public void Hush(float amount) => hush = Mathf.Clamp01(amount);

        /// A near miss or a big get: rising and falling "ooh".
        public void Ooh(float strength = 1) { if (ooh) crowd.PlayOneShot(ooh, Mathf.Lerp(.35f, .8f, strength)); }
        /// The home crowd's disappointment.
        public void Groan(float strength = 1) { if (groan) crowd.PlayOneShot(groan, Mathf.Lerp(.3f, .7f, strength)); }
        /// Cheering: a cheer for a good point, a roar for a winner or the match.
        public void Cheer(float strength)
        {
            var clip = strength > .7f ? roar : cheer;
            if (clip) crowd.PlayOneShot(clip, Mathf.Lerp(.35f, .9f, strength));
        }

        /// A planted foot. Hard courts squeak under a sharp change of direction; plain steps
        /// only thud softly.
        public void Step(float speed, bool nearPlayer)
        {
            if (!squeakA) return;
            float gain = nearPlayer ? 1 : .45f;
            float s = Mathf.Abs(speed);
            if (s > 4.5f && UnityEngine.Random.value < .45f)
            {
                var clip = UnityEngine.Random.value < .33f ? squeakA : UnityEngine.Random.value < .5f ? squeakB : squeakC;
                feet.pitch = UnityEngine.Random.Range(.92f, 1.1f);
                feet.PlayOneShot(clip, Mathf.Lerp(.12f, .35f, (s - 4.5f) / 3) * gain);
            }
            else if (s > 1.5f) feet.PlayOneShot(land, Mathf.Lerp(.05f, .14f, s / 6) * gain);
        }

        /// Landing from a jump (serve, smash).
        public void Land(float weight = 1) { if (land) feet.PlayOneShot(land, .35f * weight); }

        void Update()
        {
            if (task != null && task.IsCompleted)
            {
                if (task.Status == TaskStatus.RanToCompletion)
                {
                    var r = task.Result;
                    beach.clip = Clip("Beach", r[0]); murmur.clip = Clip("Stands murmur", r[1]);
                    ooh = Clip("Crowd ooh", r[2]); groan = Clip("Crowd groan", r[3]); cheer = Clip("Crowd cheer", r[4]); roar = Clip("Crowd roar", r[5]);
                    squeakA = Clip("Squeak A", r[6]); squeakB = Clip("Squeak B", r[7]); squeakC = Clip("Squeak C", r[8]); land = Clip("Land", r[9]);
                    beach.volume = 0; murmur.volume = 0; beach.Play(); murmur.Play();
                }
                task = null;
            }
            if (!beach.isPlaying) return;
            float dt = Time.unscaledDeltaTime;
            beach.volume = Mathf.MoveTowards(beach.volume, beachWant, dt * .3f);
            murmur.volume = Mathf.MoveTowards(murmur.volume, murmurWant * (1 - hush), dt * (hush > 0 ? .8f : .25f));
        }

        static AudioClip Clip(string name, float[] data)
        {
            var c = AudioClip.Create(name, data.Length, 1, Rate, false);
            c.SetData(data, 0);
            return c;
        }

        static float[][] Render() => new[]
        {
            Beach(), Murmur(), Crowd(1.5, Oo, Oo, 1.0, 1.25, .9, 26, 11), Crowd(1.7, Aw, Oo, 1.0, .74, .8, 26, 12),
            Cheer(2.6, false), Cheer(3.6, true), Squeak(2350, 3300, 1), Squeak(2100, 2900, 2), Squeak(2600, 3600, 3), Land(),
        };

        /// Surf on a beach behind the stands, a breeze in the palms, and a gull now and then.
        static float[] Beach()
        {
            double seconds = 18;
            int n = (int)(seconds * Rate), fade = Rate;
            var d = new float[n + fade];
            var r = new System.Random(5);
            var surf = Biquad.LowPass(520, .7, Rate); var hiss = Biquad.HighPass(2500, .7, Rate); var breeze = Biquad.BandPass(420, .6, Rate);
            double brown = 0;
            for (int i = 0; i < d.Length; i++)
            {
                double t = (double)i / Rate;
                brown = brown * .995 + Noise(r) * .1;
                // Two overlapping sets of waves; the crest adds a white hiss as the wave breaks.
                double swell = .55 + .45 * Math.Pow(.5 + .5 * Math.Sin(2 * Math.PI * t / 7.3), 2) * (.7 + .3 * Math.Sin(2 * Math.PI * t / 4.1 + 1));
                double crest = Math.Pow(Math.Max(0, Math.Sin(2 * Math.PI * t / 7.3 - .6)), 6);
                double gust = .5 + .5 * Math.Sin(2 * Math.PI * t / 5.7) * Math.Sin(2 * Math.PI * t / 2.3 + .4);
                d[i] = (float)(surf.Process(brown) * swell * 1.4 + hiss.Process(Noise(r)) * crest * .12 + breeze.Process(Noise(r)) * gust * .05);
            }
            // Gulls: "kyow" calls, a couple of cries each.
            foreach (var (at, cries) in new[] { (3.2, 3), (11.8, 2) })
                for (int c = 0; c < cries; c++)
                {
                    int start = (int)((at + c * .42) * Rate);
                    Mix(d, start, (int)(.34 * Rate), i =>
                    {
                        double t = (double)i / Rate, u = t / .34;
                        double f = 1250 + 520 * Math.Sin(Math.PI * Math.Min(1, u * 1.6)) - 300 * u + 40 * Math.Sin(2 * Math.PI * 32 * t);
                        double env = Math.Sin(Math.PI * u);
                        return Math.Tanh(2.2 * Math.Sin(2 * Math.PI * f * t)) * env * env;
                    }, .03);
                }
            return Normalize(Loopable(d, fade), .8f);
        }

        /// The stands between points: many voices chatting, each a string of syllables.
        static float[] Murmur()
        {
            double seconds = 14;
            int n = (int)(seconds * Rate), fade = Rate;
            var d = new float[n + fade];
            var vowels = new[] { Ah, Eh, Oo, Aw, Ee };
            for (int v = 0; v < 22; v++)
            {
                var voice = new Voice(100 + v, Rate); var r = new System.Random(200 + v);
                double f0 = 105 + r.NextDouble() * 140, next = 0, sylLen = .2, syl = 0;
                bool talking = true; double switchAt = r.NextDouble() * 3;
                var lp = Biquad.LowPass(2400, .7, Rate);
                for (int i = 0; i < d.Length; i++)
                {
                    double t = (double)i / Rate;
                    if (t >= switchAt) { talking = !talking; switchAt = t + (talking ? 1.5 + r.NextDouble() * 3 : .6 + r.NextDouble() * 2); }
                    if (t >= next)
                    {
                        var vw = vowels[r.Next(vowels.Length)]; voice.Vowel(vw[0] * (.9 + r.NextDouble() * .2), vw[1] * (.9 + r.NextDouble() * .2));
                        sylLen = .12 + r.NextDouble() * .16; syl = t; next = t + sylLen;
                    }
                    double env = talking ? Math.Sin(Math.PI * Math.Min(1, (t - syl) / sylLen)) : 0;
                    double pitch = f0 * (1 + .08 * Math.Sin(2 * Math.PI * .7 * t + v));
                    d[i] += (float)(lp.Process(voice.Next(pitch, .35)) * env * .1);
                }
            }
            return Normalize(Loopable(d, fade), .7f);
        }

        /// A crowd sound: many voices on a vowel gliding from `from` to `to`, the pitch moving
        /// by `bend` (1.25 = up a third; an "ooh" rises then falls, a groan sinks).
        static float[] Crowd(double seconds, double[] from, double[] to, double startBend, double bend, double breath, int voices, int seed)
        {
            int n = (int)(seconds * Rate);
            var d = new float[n];
            bool rise = bend > 1;
            for (int v = 0; v < voices; v++)
            {
                var voice = new Voice(seed * 100 + v, Rate); var r = new System.Random(seed * 1000 + v);
                double f0 = 120 + r.NextDouble() * 170, onset = r.NextDouble() * .18, len = seconds * (.75 + r.NextDouble() * .25) - onset;
                double spread = .92 + r.NextDouble() * .16;
                for (int i = 0; i < n; i++)
                {
                    double t = (double)i / Rate - onset;
                    if (t < 0 || t > len) continue;
                    double u = t / len;
                    if (i % 64 == 0) voice.Vowel((from[0] + (to[0] - from[0]) * u) * spread, (from[1] + (to[1] - from[1]) * u) * spread);
                    double contour = rise ? 1 + (bend - 1) * Math.Sin(Math.PI * Math.Min(1, u * 1.3)) : 1 + (bend - 1) * u;
                    double env = Math.Min(1, t / .12) * Math.Pow(1 - u, .6);
                    d[i] += (float)(voice.Next(f0 * startBend * contour, breath * .4) * env * .08);
                }
            }
            return Normalize(d, .85f);
        }

        /// Cheering: voices shouting "yeah" and "ahh", whistles, and a bed of applause.
        static float[] Cheer(double seconds, bool roar)
        {
            int n = (int)(seconds * Rate);
            var d = new float[n];
            int voices = roar ? 40 : 24;
            for (int v = 0; v < voices; v++)
            {
                var voice = new Voice(700 + v, Rate); var r = new System.Random(900 + v);
                double f0 = 150 + r.NextDouble() * 230, onset = r.NextDouble() * .35, len = seconds * (.45 + r.NextDouble() * .5);
                bool yeah = r.Next(2) == 0;
                for (int i = 0; i < n; i++)
                {
                    double t = (double)i / Rate - onset;
                    if (t < 0 || t > len) continue;
                    double u = t / len;
                    if (i % 64 == 0)
                    {
                        var a = yeah && u < .25 ? Eh : Ah;
                        voice.Vowel(a[0], a[1]);
                    }
                    double pitch = f0 * (1 + .15 * Math.Sin(Math.PI * Math.Min(1, u * 2))) * (1 + .03 * Math.Sin(2 * Math.PI * 6 * t + v));
                    double env = Math.Min(1, t / .06) * Math.Pow(1 - u, .8);
                    d[i] += (float)(voice.Next(pitch, .45) * env * .07);
                }
            }
            // Whistles.
            var wr = new System.Random(77);
            for (int w = 0; w < (roar ? 4 : 2); w++)
            {
                double at = .2 + wr.NextDouble() * seconds * .4, len = .5 + wr.NextDouble() * .7, f = 2100 + wr.NextDouble() * 900;
                bool wolf = wr.Next(2) == 0;
                Mix(d, (int)(at * Rate), (int)(len * Rate), i =>
                {
                    double t = (double)i / Rate, u = t / len;
                    double hz = wolf ? f * (1 + .35 * Math.Sin(Math.PI * u)) : f * (1 + .02 * Math.Sin(2 * Math.PI * 7 * t));
                    return Math.Sin(2 * Math.PI * hz * t) * Math.Sin(Math.PI * u);
                }, .05);
            }
            // Applause: individual claps, dense and irregular.
            var cr = new System.Random(roar ? 31 : 32);
            int claps = (int)(seconds * (roar ? 420 : 260));
            for (int c = 0; c < claps; c++)
            {
                double at = cr.NextDouble() * seconds * .95;
                double fade = Math.Pow(1 - at / seconds, 1.3);
                var bp = Biquad.BandPass(900 + cr.NextDouble() * 1800, 1.4, Rate); var nr = new System.Random(c);
                double tau = .006 + cr.NextDouble() * .006;
                Mix(d, (int)(at * Rate), (int)(.04 * Rate), i => bp.Process(Noise(nr)) * Math.Exp(-(double)i / Rate / tau), .22 * fade);
            }
            return Normalize(d, .9f);
        }

        /// A court-shoe squeak: a rubber chirp, rising.
        static float[] Squeak(double f0, double f1, int seed)
        {
            double seconds = .11;
            int n = (int)(seconds * Rate);
            var d = new float[n];
            var r = new System.Random(seed);
            double phase = 0;
            for (int i = 0; i < n; i++)
            {
                double t = (double)i / Rate, u = t / seconds;
                double f = f0 + (f1 - f0) * u + 60 * Math.Sin(2 * Math.PI * 90 * t);
                phase += f / Rate;
                double env = Math.Sin(Math.PI * Math.Min(1, u * 1.2)) * (1 - u * .5);
                d[i] = (float)((Math.Sin(2 * Math.PI * phase) * .8 + Math.Sin(4 * Math.PI * phase) * .25 + Noise(r) * .08) * env);
            }
            return Normalize(d, .8f);
        }

        /// A soft landing: body weight onto court shoes.
        static float[] Land()
        {
            int n = (int)(.25 * Rate);
            var d = new float[n];
            var r = new System.Random(9); var lp = Biquad.LowPass(900, .7, Rate);
            for (int i = 0; i < n; i++)
            {
                double t = (double)i / Rate;
                d[i] = (float)(Math.Sin(2 * Math.PI * (70 + 40 * Math.Exp(-t / .02)) * t) * Math.Exp(-t / .05) + lp.Process(Noise(r)) * Math.Exp(-t / .02) * .6);
            }
            return Normalize(d, .8f);
        }
    }
}
