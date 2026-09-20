using System;
using GolfArcade.Shot;
using UnityEngine;

namespace GolfArcade.Game
{
    /// The round's sounds, synthesised at start-up so the project ships no audio files: a
    /// strike per club (a driver's metallic tink, an iron's click, a putter's tock), the
    /// downswing's whoosh, the ball rattling into the cup, and a splash.
    public sealed class GolfSounds : MonoBehaviour
    {
        const int Rate = 44100;

        AudioSource source;
        AudioClip driver, iron, wedge, putter, whoosh, cup, splash;

        public static GolfSounds Create(Transform parent)
        {
            var go = new GameObject("Sounds");
            go.transform.SetParent(parent, false);
            var s = go.AddComponent<GolfSounds>();
            s.source = go.AddComponent<AudioSource>();
            s.source.playOnAwake = false;
            s.source.spatialBlend = 0;
            s.Synthesize();
            return s;
        }

        public void PlayStrike(GolfClub club, double power)
        {
            var clip = club switch { GolfClub.Driver => driver, GolfClub.Iron => iron, GolfClub.Wedge => wedge, _ => putter };
            source.PlayOneShot(clip, Mathf.Lerp(0.35f, 1f, Mathf.Clamp01((float)power)));
        }

        public void PlayWhoosh(double load) => source.PlayOneShot(whoosh, Mathf.Lerp(0.2f, 0.8f, Mathf.Clamp01((float)load)));
        public void PlayCup() => source.PlayOneShot(cup, 0.9f);
        public void PlaySplash() => source.PlayOneShot(splash, 0.8f);

        void Synthesize()
        {
            // Strikes: a burst of noise for the contact plus a ringing partial or two for the
            // clubhead's material; the decay times are what make a driver sound hollow and a
            // putter sound dead.
            driver = Clip("Driver", 0.16f, (t, rng) =>
                Burst(t, 0.006, rng) * 0.6 + Ring(t, 2400, 0.03) * 0.5 + Ring(t, 3700, 0.02) * 0.3);
            iron = Clip("Iron", 0.10f, (t, rng) =>
                Burst(t, 0.008, rng) * 0.9 + Ring(t, 1500, 0.012) * 0.5);
            wedge = Clip("Wedge", 0.10f, (t, rng) =>
                Burst(t, 0.012, rng) * 0.8 + Ring(t, 900, 0.015) * 0.4, lowPass: 0.35);
            putter = Clip("Putter", 0.08f, (t, rng) =>
                Burst(t, 0.004, rng) * 0.5 + Ring(t, 700, 0.012) * 0.8);
            // Whoosh: noise that swells and dies over a quarter of a second.
            whoosh = Clip("Whoosh", 0.30f, (t, rng) =>
            {
                double env = Math.Sin(Math.Min(1, t / 0.30) * Math.PI);
                return Noise(rng) * env * env * 0.7;
            }, lowPass: 0.12);
            // Cup: three knocks on the liner, each quieter, then the ball settling.
            cup = Clip("Cup", 0.55f, (t, rng) =>
            {
                double v = 0;
                foreach (var (at, gain) in new[] { (0.0, 1.0), (0.07, 0.7), (0.15, 0.5), (0.24, 0.3) })
                {
                    double dt = t - at;
                    if (dt < 0) continue;
                    v += (Burst(dt, 0.004, rng) * 0.6 + Ring(dt, 1150, 0.02) * 0.7) * gain;
                }
                if (t > 0.3) v += Noise(rng) * Math.Exp(-(t - 0.3) / 0.08) * 0.15;
                return v;
            });
            splash = Clip("Splash", 0.7f, (t, rng) =>
            {
                double attack = Math.Min(1, t / 0.03);
                return Noise(rng) * attack * Math.Exp(-t / 0.18) * 0.9;
            }, lowPass: 0.08);
        }

        /// A stable seed per clip so the same noise renders on every run and platform.
        static int Seed(string name) { int h = 17; foreach (char c in name) h = h * 31 + c; return h; }
        static double Noise(System.Random rng) => rng.NextDouble() * 2 - 1;
        static double Burst(double t, double tau, System.Random rng) => t < 0 ? 0 : Noise(rng) * Math.Exp(-t / tau);
        static double Ring(double t, double hz, double tau) => t < 0 ? 0 : Math.Sin(2 * Math.PI * hz * t) * Math.Exp(-t / tau);

        /// Renders `sample(time, rng)` to a mono clip, optionally through a one-pole low-pass
        /// (`lowPass` is the filter coefficient, smaller = duller), normalised to full scale.
        static AudioClip Clip(string name, float seconds, Func<double, System.Random, double> sample, double lowPass = 1)
        {
            int n = (int)(Rate * seconds);
            var data = new float[n];
            var rng = new System.Random(Seed(name));
            double filtered = 0, peak = 0;
            for (int i = 0; i < n; i++)
            {
                double v = sample((double)i / Rate, rng);
                filtered += lowPass * (v - filtered);
                data[i] = (float)filtered;
                peak = Math.Max(peak, Math.Abs(filtered));
            }
            if (peak > 0) for (int i = 0; i < n; i++) data[i] = (float)(data[i] / peak);
            // A short fade-out so the clip never clicks off.
            int fade = Math.Min(n, Rate / 200);
            for (int i = 0; i < fade; i++) data[n - 1 - i] *= (float)i / fade;
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
