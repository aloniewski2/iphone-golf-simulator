using System;
using GolfArcade.Shot;
using UnityEngine;

namespace GolfArcade.Game
{
    /// The round's sounds, synthesised at start-up so the project ships no audio files: a
    /// strike per club (a driver's metallic tink, an iron's click, a putter's tock), the
    /// downswing's whoosh, the ball rattling into the cup, a splash, a chime when the phone has
    /// settled and the swing is armed, a flourish for a holed ball, ticks for the buttons — and
    /// the backswing's tension: a creaking wind-up that climbs in pitch and volume with the meter.
    public sealed class GolfSounds : MonoBehaviour
    {
        const int Rate = 44100;

        AudioSource source, tensionSource;
        AudioClip driver, iron, wedge, putter, whoosh, cup, splash, thud, tension, ready, fanfare, tick;
        float tensionTarget;

        public static GolfSounds Create(Transform parent)
        {
            var go = new GameObject("Sounds");
            go.transform.SetParent(parent, false);
            var s = go.AddComponent<GolfSounds>();
            s.source = go.AddComponent<AudioSource>();
            s.source.playOnAwake = false;
            s.source.spatialBlend = 0;
            s.tensionSource = go.AddComponent<AudioSource>();
            s.tensionSource.playOnAwake = false;
            s.tensionSource.spatialBlend = 0;
            s.tensionSource.loop = true;
            s.Synthesize();
            s.tensionSource.clip = s.tension;
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
        public void PlayThud(float strength) => source.PlayOneShot(thud, Mathf.Lerp(0.25f, 0.8f, Mathf.Clamp01(strength)));
        public void PlayReady() => source.PlayOneShot(ready, 0.5f);
        public void PlayFanfare() => source.PlayOneShot(fanfare, 0.7f);
        public void PlayTick() => source.PlayOneShot(tick, 0.5f);

        /// Backswing tension, 0–1 with the meter: the wind-up loop fades in and climbs a fifth
        /// in pitch by the top. Call with the load every frame it changes; Release() lets go.
        public void SetTension(double load)
        {
            tensionTarget = Mathf.Clamp01((float)load);
            if (!tensionSource.isPlaying && tensionTarget > 0) { tensionSource.volume = 0; tensionSource.Play(); }
        }

        public void Release() { tensionTarget = 0; }

        void Update()
        {
            if (!tensionSource.isPlaying) return;
            // Volume tracks the load quickly; pitch climbs with it, from a slack 0.8 to 1.5.
            float v = Mathf.MoveTowards(tensionSource.volume, tensionTarget * 0.55f, Time.unscaledDeltaTime * 4f);
            tensionSource.volume = v;
            tensionSource.pitch = 0.8f + 0.7f * tensionTarget;
            if (tensionTarget <= 0 && v <= 0.005f) tensionSource.Stop();
        }

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
            // Thud: a ball meeting turf — a low knock with a breath of grass.
            thud = Clip("Thud", 0.22f, (t, rng) =>
                Math.Sin(2 * Math.PI * 95 * t) * Math.Exp(-t / 0.045) * 0.9 + Noise(rng) * Math.Exp(-t / 0.03) * 0.35, lowPass: 0.25);
            splash = Clip("Splash", 0.7f, (t, rng) =>
            {
                double attack = Math.Min(1, t / 0.03);
                return Noise(rng) * attack * Math.Exp(-t / 0.18) * 0.9;
            }, lowPass: 0.08);
            // Tension: a one-second seamless loop — a low drone (whole cycles per second so the
            // loop joins), a ratchet of soft ticks like a creaking shaft, and a breath of noise.
            tension = Clip("Tension", 1.0f, (t, rng) =>
            {
                double drone = Math.Sin(2 * Math.PI * 88 * t) * 0.35 + Math.Sin(2 * Math.PI * 132 * t) * 0.15;
                double phase = (t * 14) % 1.0; // 14 ticks a second
                double ratchet = Math.Exp(-phase / 0.012) * Noise(rng) * 0.6;
                double breath = Noise(rng) * 0.08;
                return drone + ratchet + breath;
            }, lowPass: 0.25, fadeEnds: false);
            // Ready: two soft notes, a fifth apart, once the phone has settled.
            ready = Clip("Ready", 0.35f, (t, rng) =>
                Ring(t, 660, 0.09) * 0.6 + Ring(t - 0.12, 990, 0.12) * 0.6);
            // Fanfare for a holed ball: a rising arpeggio with a little shimmer on top.
            fanfare = Clip("Fanfare", 1.1f, (t, rng) =>
            {
                double v = 0;
                double[] notes = { 523.25, 659.25, 783.99, 1046.5 };
                for (int i = 0; i < notes.Length; i++)
                {
                    double dt = t - i * 0.13;
                    if (dt < 0) continue;
                    double tail = i == notes.Length - 1 ? 0.5 : 0.18;
                    v += (Ring(dt, notes[i], tail) + Ring(dt, notes[i] * 2, tail * 0.6) * 0.3) * 0.5;
                }
                return v;
            });
            tick = Clip("Tick", 0.03f, (t, rng) => Burst(t, 0.003, rng) * 0.6 + Ring(t, 2200, 0.006) * 0.6);
        }

        /// A stable seed per clip so the same noise renders on every run and platform.
        static int Seed(string name) { int h = 17; foreach (char c in name) h = h * 31 + c; return h; }
        static double Noise(System.Random rng) => rng.NextDouble() * 2 - 1;
        static double Burst(double t, double tau, System.Random rng) => t < 0 ? 0 : Noise(rng) * Math.Exp(-t / tau);
        static double Ring(double t, double hz, double tau) => t < 0 ? 0 : Math.Sin(2 * Math.PI * hz * t) * Math.Exp(-t / tau);

        /// Renders `sample(time, rng)` to a mono clip, optionally through a one-pole low-pass
        /// (`lowPass` is the filter coefficient, smaller = duller), normalised to full scale.
        static AudioClip Clip(string name, float seconds, Func<double, System.Random, double> sample, double lowPass = 1, bool fadeEnds = true)
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
            // A short fade-out so a one-shot never clicks off (a loop must stay seamless instead).
            int fade = fadeEnds ? Math.Min(n, Rate / 200) : 0;
            for (int i = 0; i < fade; i++) data[n - 1 - i] *= (float)i / fade;
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
