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
        AudioClip driver, iron, wedge, putter, whoosh, cup, splash, thud, tension, ready, fanfare, tick, applause, gasp, groan, leaves, knockWood, knockStone, ovation, whistles;
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
            var clip = club.Family() switch { ClubFamily.Wood => driver, ClubFamily.Iron => iron, ClubFamily.Wedge => wedge, _ => putter };
            source.PlayOneShot(clip, Mathf.Lerp(0.35f, 1f, Mathf.Clamp01((float)power)));
        }

        public void PlayWhoosh(double load) => source.PlayOneShot(whoosh, Mathf.Lerp(0.2f, 0.8f, Mathf.Clamp01((float)load)));
        public void PlayCup() => source.PlayOneShot(cup, 0.9f);
        public void PlaySplash() => source.PlayOneShot(splash, 0.8f);
        public void PlayThud(float strength) => source.PlayOneShot(thud, Mathf.Lerp(0.25f, 0.8f, Mathf.Clamp01(strength)));
        /// The ball into a tree's branches or a bush: a rustle and a snap of twigs.
        public void PlayLeaves() => source.PlayOneShot(leaves, 0.75f);
        /// Off a trunk (a hollow knock) or a rock or a wall (a hard clack).
        public void PlayKnock(bool stone) => source.PlayOneShot(stone ? knockStone : knockWood, 0.8f);
        public void PlayReady() => source.PlayOneShot(ready, 0.5f);
        public void PlayFanfare() => source.PlayOneShot(fanfare, 0.7f);
        public void PlayTick() => source.PlayOneShot(tick, 0.5f);
        /// The gallery's applause, for the introductions on the tee and a good shot.
        public void PlayApplause(float volume = 0.35f) => source.PlayOneShot(applause, volume);

        /// The gallery's reactions, sized to the shot: an "ooh" as one flies at the flag or
        /// finds the water, an "aww" for a lip-out or a ball in the sand.
        public void PlayGasp(float volume = 0.6f) => source.PlayOneShot(gasp, volume);
        public void PlayGroan(float volume = 0.55f) => source.PlayOneShot(groan, volume);

        /// The ball in the hole, the gallery's answer sized to the score: for a birdie or better
        /// a long round of applause with whistles in it; a par, a warm round; over par, polite.
        public void PlayHoleOut(int strokes, int par)
        {
            int toPar = strokes - par;
            if (strokes == 1 || toPar < 0)
            {
                float big = strokes == 1 || toPar <= -2 ? 1f : 0.85f;
                source.PlayOneShot(ovation, 0.5f * big);
                source.PlayOneShot(whistles, 0.15f * big);   // (pure tones: they carry)
            }
            else if (toPar == 0) source.PlayOneShot(ovation, 0.42f);
            else source.PlayOneShot(ovation, 0.3f);
        }

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
            applause = Applause(3.4f, 16);
            // A hole finished: more of the gallery clapping, for longer, and a few putting
            // fingers to their lips. (No roar: the synthesised crowd's voices through breath
            // noise came over as a loud muffled static.)
            ovation = Applause(6f, 28, "Ovation");
            whistles = Whistles();
            // The gallery's voices: a couple of dozen people, men and women, each a buzz of
            // harmonics shaped into a vowel, coming in a beat apart and sliding in pitch.
            gasp = Crowd("Gasp", 1.6f, 18, (300, 870), (330, 900), u => 1 + 0.35 * Math.Sin(Math.PI * Math.Min(1, u * 1.3)), 0.25);
            groan = Crowd("Groan", 1.7f, 18, (660, 1190), (560, 840), u => 1.3 - 0.45 * u, 0.3);
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
            leaves = Clip("Leaves", 0.55f, (t, rng) =>
            {
                // a rustle that swells and dies, with twigs snapping in it
                double rustle = Noise(rng) * Math.Min(1, t / 0.02) * Math.Exp(-t / 0.16) * 0.7;
                double snap = 0;
                foreach (double at in new[] { 0.012, 0.05, 0.11, 0.2 })
                    if (t >= at) snap += Noise(rng) * Math.Exp(-(t - at) / 0.006) * 0.8;
                return rustle + snap;
            }, lowPass: 0.35);
            knockWood = Clip("Knock", 0.2f, (t, rng) =>
                Math.Sin(2 * Math.PI * 420 * t) * Math.Exp(-t / 0.03) * 0.8 + Math.Sin(2 * Math.PI * 180 * t) * Math.Exp(-t / 0.05) * 0.5 + Noise(rng) * Math.Exp(-t / 0.004) * 0.6, lowPass: 0.4);
            knockStone = Clip("Clack", 0.16f, (t, rng) =>
                Math.Sin(2 * Math.PI * 1250 * t) * Math.Exp(-t / 0.018) * 0.6 + Math.Sin(2 * Math.PI * 2100 * t) * Math.Exp(-t / 0.01) * 0.35 + Noise(rng) * Math.Exp(-t / 0.003) * 0.8, lowPass: 0.7);
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

        /// A gallery clapping: `people` of them, each at their own steady pace (three and a half
        /// to five claps a second), joining in over the first half second and dropping out one by
        /// one. Each clap is a click ringing through the cupped hands' resonance (0.85–2.35 kHz),
        /// and a small room gathers them, with the fizz above taken off — so it's clapping, not
        /// the hiss that scattering bursts of white noise made (the user heard static).
        static AudioClip Applause(float seconds, int people = 16, string name = "Applause")
        {
            int n = (int)(Rate * seconds), ring = (int)(0.03 * Rate), click = (int)(0.0015 * Rate);
            var mix = new double[n];
            var rng = new System.Random(Seed(name));
            for (int p = 0; p < people; p++)
            {
                double pace = 3.4 + 1.8 * rng.NextDouble();
                double start = 0.03 + 0.45 * Math.Pow(rng.NextDouble(), 2), stop = seconds * (0.4 + 0.5 * rng.NextDouble());
                double f = 850 + 1500 * rng.NextDouble(), q = 4 + 5 * rng.NextDouble(), loud = 0.45 + 0.55 * rng.NextDouble();
                double r = Math.Exp(-Math.PI * f / q / Rate), c = 2 * r * Math.Cos(2 * Math.PI * f / Rate);
                for (double t = start; t < stop; t += (1 + 0.07 * Noise(rng)) / pace)
                {
                    double amp = loud * (0.7 + 0.3 * rng.NextDouble()) * Math.Min(1, (t - start) / 0.25 + 0.4) * Math.Min(1, (stop - t) / 0.6);
                    int i0 = (int)(t * Rate);
                    double y1 = 0, y2 = 0;
                    for (int i = 0; i < ring && i0 + i < n; i++)
                    {
                        double x = i < click ? Noise(rng) * Math.Exp(-i / (0.0004 * Rate)) : 0;
                        double y = (1 - r) * x + c * y1 - r * r * y2; y2 = y1; y1 = y;
                        mix[i0 + i] += amp * (3 * y + 0.08 * x);
                    }
                }
            }
            // a small room (three early reflections), then the fizz taken off
            var room = (double[])mix.Clone();
            foreach (var (delay, gain) in new[] { (0.019, 0.3), (0.031, 0.22), (0.047, 0.14) })
            {
                int k = (int)(delay * Rate);
                for (int i = k; i < n; i++) room[i] += gain * mix[i - k];
            }
            double lp = 0, peak = 1e-9;
            for (int i = 0; i < n; i++) { lp += 0.4 * (room[i] - lp); room[i] = lp; peak = Math.Max(peak, Math.Abs(lp)); }
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = (float)(room[i] / peak * 0.9);
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// A few of the gallery whistling through their fingers, one after another: each a bright
        /// tone that swoops up into its note, holds with a waver, and drops off, with the breath
        /// in it.
        static AudioClip Whistles()
        {
            // (start, length, from, note, end) in seconds and hertz
            var calls = new[] { (0.25, 0.75, 1700.0, 2900.0, 2300.0), (0.9, 0.55, 2100.0, 3300.0, 2500.0), (1.55, 0.9, 1600.0, 2700.0, 2000.0), (2.5, 0.5, 2000.0, 3100.0, 2600.0) };
            return Clip("Whistles", 3.3f, (t, rng) =>
            {
                double v = 0;
                foreach (var (at, len, from, note, end) in calls)
                {
                    double u = t - at;
                    if (u < 0 || u > len) continue;
                    // the pitch: a swoop up over the first 0.12 s, a hold, a fall over the last
                    // 0.15 s (the phase integrated piecewise so the glides don't chirp)
                    double rise = 0.12, fall = 0.15, hold = len - rise - fall;
                    double phase;
                    if (u < rise) phase = from * u + (note - from) * u * u / (2 * rise);
                    else if (u < rise + hold) phase = (from + note) / 2 * rise + note * (u - rise);
                    else { double w = u - rise - hold; phase = (from + note) / 2 * rise + note * hold + note * w + (end - note) * w * w / (2 * fall); }
                    phase += 0.004 * Math.Sin(2 * Math.PI * 6.5 * u);   // the waver
                    double env = Math.Min(1, u / 0.03) * Math.Min(1, (len - u) / 0.06);
                    v += (Math.Sin(2 * Math.PI * phase) + 0.12 * Math.Sin(4 * Math.PI * phase)) * env * 0.5 + Noise(rng) * env * 0.05;
                }
                return v;
            });
        }

        /// A crowd voicing one vowel together: `voices` people, each a harmonic buzz at their own
        /// pitch (bent over time by `pitch`, 0–1 through the clip) run through the vowel's two
        /// formants, sliding from `from` to `to`; `breath` mixes in the same vowel whispered,
        /// which is most of what a crowd sounds like from a distance. Rendered at 22 kHz.
        static AudioClip Crowd(string name, float seconds, int voices, (double f1, double f2) from, (double f1, double f2) to, Func<double, double> pitch, double breath)
        {
            const int rate = 22050, block = 128;
            int n = (int)(rate * seconds);
            var mix = new double[n];
            var rng = new System.Random(Seed(name));
            double Formant(double f, double f1, double f2) => 1 / (1 + Math.Pow((f - f1) / 110, 2)) + 0.6 / (1 + Math.Pow((f - f2) / 150, 2));
            for (int v = 0; v < voices; v++)
            {
                bool high = rng.NextDouble() < 0.4;
                double f0 = high ? 190 + 70 * rng.NextDouble() : 95 + 50 * rng.NextDouble();
                double onset = 0.02 + 0.2 * rng.NextDouble() * rng.NextDouble();
                double length = seconds * (0.55 + 0.4 * rng.NextDouble());
                double gain = 0.5 + 0.5 * rng.NextDouble();
                double vibRate = 4 + 2 * rng.NextDouble(), vibPhase = rng.NextDouble() * 6.28;
                int harmonics = (int)Math.Min(12, 2600 / f0);
                var phase = new double[harmonics + 1];
                var amp = new double[harmonics + 1];
                for (int i0 = 0; i0 < n; i0 += block)
                {
                    double t = (double)i0 / rate, u = Math.Min(1, t / seconds);
                    double f1 = from.f1 + (to.f1 - from.f1) * u, f2 = from.f2 + (to.f2 - from.f2) * u;
                    double hz = f0 * pitch(u) * (1 + 0.015 * Math.Sin(vibRate * t + vibPhase));
                    for (int k = 1; k <= harmonics; k++) amp[k] = Formant(k * hz, f1, f2) / k;
                    for (int i = i0; i < Math.Min(n, i0 + block); i++)
                    {
                        double ti = (double)i / rate - onset;
                        if (ti < 0) continue;
                        double env = Math.Min(1, ti / 0.07) * (ti < length ? 1 : Math.Exp(-(ti - length) / 0.18));
                        if (env < 1e-4) break;
                        double sum = 0;
                        for (int k = 1; k <= harmonics; k++)
                        {
                            phase[k] += 2 * Math.PI * k * hz / rate;
                            sum += Math.Sin(phase[k]) * amp[k];
                        }
                        mix[i] += sum * env * gain;
                    }
                }
            }
            // the whisper: noise through two resonators at the formants
            double y1a = 0, y2a = 0, y1b = 0, y2b = 0, peakVoices = 1e-9;
            for (int i = 0; i < n; i++) peakVoices = Math.Max(peakVoices, Math.Abs(mix[i]));
            var data = new float[n];
            double peak = 1e-9;
            for (int i = 0; i < n; i++)
            {
                double t = (double)i / rate, u = Math.Min(1, t / seconds);
                double f1 = from.f1 + (to.f1 - from.f1) * u, f2 = from.f2 + (to.f2 - from.f2) * u;
                double x = Noise(rng);
                double Res(double f, double bw, ref double y1, ref double y2)
                {
                    double r = Math.Exp(-Math.PI * bw / rate), c = 2 * r * Math.Cos(2 * Math.PI * f / rate);
                    double y = (1 - r) * x + c * y1 - r * r * y2; y2 = y1; y1 = y; return y;
                }
                double env = Math.Min(1, t / 0.12) * Math.Exp(-Math.Max(0, t - seconds * 0.6) / (seconds * 0.2));
                double whisper = (Res(f1, 120, ref y1a, ref y2a) + 0.6 * Res(f2, 160, ref y1b, ref y2b)) * env * 6;
                double v = mix[i] / peakVoices * (1 - breath) + whisper * breath;
                data[i] = (float)v; peak = Math.Max(peak, Math.Abs(v));
            }
            for (int i = 0; i < n; i++) data[i] = (float)(data[i] / peak * 0.9);
            int fade = Math.Min(n, rate / 50);
            for (int i = 0; i < fade; i++) data[n - 1 - i] *= (float)i / fade;
            var clip = AudioClip.Create(name, n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
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
