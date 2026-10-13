using System;
using UnityEngine;

namespace GolfArcade.Game
{
    /// The round's events, heard: rain on the course and the wind in loops that swell and fade
    /// with the weather, thunder rolling in after the flash, a gust, the balloon's rubbery boing,
    /// a gull's squawk, the cart's horn, and the twists' reveal (a roulette ratchet and a stamp).
    /// Synthesised, like GolfSounds, so they need no files — each the first time it is wanted
    /// (Prepare makes a storm's ahead, while its hole is being built), so a round starts as
    /// quickly as it always did.
    public sealed class EventSounds : MonoBehaviour
    {
        const int Rate = 44100;
        AudioSource once, rain, wind;
        AudioClip rainLoop, windLoop, thunderNear, thunderFar, gust, boing, squawk, horn, ratchet, stamp, rumble, chime, bigChime, jingle, foghorn;
        float rainTarget, windTarget;
        AudioClip RainLoop => rainLoop ??= MakeRain();
        AudioClip WindLoop => windLoop ??= MakeWind();
        AudioClip ThunderNear => thunderNear ??= MakeThunderNear();
        AudioClip ThunderFar => thunderFar ??= MakeThunderFar();
        AudioClip Rumbling => rumble ??= MakeRumble();
        AudioClip GustClip => gust ??= MakeGust();
        AudioClip BoingClip => boing ??= MakeBoing();
        AudioClip SquawkClip => squawk ??= MakeSquawk();
        AudioClip HornClip => horn ??= MakeHorn();
        AudioClip RatchetClip => ratchet ??= MakeRatchet();
        AudioClip StampClip => stamp ??= MakeStamp();
        AudioClip ChimeClip => chime ??= MakeChime("Ring chime", new[] { 1318.5, 1975.5 });
        AudioClip BigChimeClip => bigChime ??= MakeChime("Big ring chime", new[] { 1046.5, 1318.5, 1568.0, 2093.0 });
        AudioClip JingleClip => jingle ??= MakeJingle();
        AudioClip FoghornClip => foghorn ??= MakeFoghorn();

        public static EventSounds Create(Transform parent)
        {
            var go = new GameObject("Event sounds");
            go.transform.SetParent(parent, false);
            var s = go.AddComponent<EventSounds>();
            AudioSource Source(bool loop) { var a = go.AddComponent<AudioSource>(); a.playOnAwake = false; a.spatialBlend = 0; a.loop = loop; a.volume = 0; return a; }
            s.once = Source(false); s.once.volume = 1;
            s.rain = Source(true); s.wind = Source(true);
            return s;
        }

        /// The sounds a hole's weather will want, made now (while the hole is built) rather than
        /// mid-shot: the rain, and a storm's thunder or an eruption's rumble.
        public void Prepare(Course.Weather weather, Course.Sky sky)
        {
            if (weather is Course.Weather.Rain or Course.Weather.Storm && sky != Course.Sky.Snow) _ = RainLoop;
            if (weather == Course.Weather.Storm) { if (sky == Course.Sky.Ash) _ = Rumbling; else if (sky == Course.Sky.Rain) { _ = ThunderNear; _ = ThunderFar; } }
        }

        /// How loud the rain is (0 none, 1 a downpour) and the wind (0 still, 1 a gale): the loops ease toward them.
        public void SetAmbience(float rainLevel, float windLevel) { rainTarget = Mathf.Clamp01(rainLevel); windTarget = Mathf.Clamp01(windLevel); }

        void Update()
        {
            Ease(rain, rainTarget * 0.42f);
            Ease(wind, windTarget * 0.32f);
        }

        void Ease(AudioSource a, float target)
        {
            if (!a.clip) { if (target <= 0) return; a.clip = a == rain ? RainLoop : WindLoop; }
            a.volume = Mathf.MoveTowards(a.volume, target, Time.unscaledDeltaTime * 0.35f);
            if (a.volume > 0.001f && !a.isPlaying) a.Play();
            else if (a.volume <= 0.001f && a.isPlaying && target <= 0) a.Stop();
        }

        /// Thunder `distance` (0 overhead, 1 far off): a crack and a long roll, or only the roll.
        public void Thunder(float distance) => once.PlayOneShot(distance < 0.4f ? ThunderNear : ThunderFar, Mathf.Lerp(1f, 0.55f, distance));
        /// The volcano's rumble, for an eruption's flash.
        public void Rumble() => once.PlayOneShot(Rumbling, 0.9f);
        public void Gust() => once.PlayOneShot(GustClip, 0.75f);
        public void Boing() => once.PlayOneShot(BoingClip, 0.9f);
        public void Squawk() => once.PlayOneShot(SquawkClip, 0.8f);
        public void Horn() => once.PlayOneShot(HornClip, 0.7f);
        public void Ratchet() => once.PlayOneShot(RatchetClip, 0.35f);
        public void Stamp() => once.PlayOneShot(StampClip, 0.85f);
        /// A ring flown through: a bright two-note chime, a run up four for the big ones.
        public void Chime(bool big) => once.PlayOneShot(big ? BigChimeClip : ChimeClip, 0.7f);
        /// Coins paid out (a bounty done): a jingle of them landing.
        public void Jingle() => once.PlayOneShot(JingleClip, 0.8f);
        /// Far off in the fog, a foghorn.
        public void Foghorn() => once.PlayOneShot(FoghornClip, 0.5f);

        // Rain: a bed of soft hiss with countless tiny ticks of drops in it, looped seamlessly.
        static AudioClip MakeRain() => Loop("Rain", 3f, (t, rng) =>
            {
                double hiss = Noise(rng) * 0.35;
                double drop = rng.NextDouble() < 0.0025 ? (rng.NextDouble() - 0.5) * 2.2 : 0;
                return hiss + drop;
            }, 0.25);
        // Wind: low noise breathing slowly in and out.
        static AudioClip MakeWind() => Loop("Wind", 4f, (t, rng) => Noise(rng) * (0.55 + 0.45 * Math.Sin(2 * Math.PI * t / 4.0) * Math.Sin(2 * Math.PI * t / 2.0 + 1)), 0.035);
        // Thunder: a sharp crack splitting, then a long rolling rumble that swells and dies.
        static AudioClip MakeThunderNear() => Clip("Thunder near", 4.5f, (t, rng) =>
            {
                double crack = t < 0.25 ? Noise(rng) * Math.Exp(-t / 0.05) * 1.4 : 0;
                double roll = Noise(rng) * Math.Exp(-t / 1.4) * (0.6 + 0.4 * Math.Sin(t * 9 + Math.Sin(t * 3) * 2)) * Math.Min(1, t / 0.15);
                return crack + roll;
            }, 0.06);
        static AudioClip MakeThunderFar() => Clip("Thunder far", 4.5f, (t, rng) =>
                Noise(rng) * Math.Min(1, t / 0.6) * Math.Exp(-t / 1.6) * (0.65 + 0.35 * Math.Sin(t * 5 + Math.Sin(t * 2) * 3)), 0.025);
        static AudioClip MakeRumble() => Clip("Rumble", 3.5f, (t, rng) =>
                Noise(rng) * Math.Min(1, t / 0.4) * Math.Exp(-t / 1.3) + Math.Sin(2 * Math.PI * 38 * t) * Math.Exp(-t / 1.0) * 0.5, 0.03);
        // A gust: noise swelling and falling over a second and a half.
        static AudioClip MakeGust() => Clip("Gust", 1.6f, (t, rng) =>
            {
                double env = Math.Sin(Math.Min(1, t / 1.6) * Math.PI);
                return Noise(rng) * env * env;
            }, 0.08);
        // The balloon: a rubbery boing, a pitch that wobbles down.
        static AudioClip MakeBoing() => Clip("Boing", 0.7f, (t, rng) =>
            {
                double f = 180 + 140 * Math.Exp(-t / 0.12) + 18 * Math.Sin(2 * Math.PI * 9 * t) * Math.Exp(-t / 0.3);
                return Math.Sin(2 * Math.PI * f * t) * Math.Exp(-t / 0.22) + Noise(rng) * Math.Exp(-t / 0.01) * 0.3;
            });
        // A gull: two harsh rising-falling calls.
        static AudioClip MakeSquawk() => Clip("Squawk", 0.75f, (t, rng) =>
            {
                double v = 0;
                foreach (double at in new[] { 0.0, 0.33 })
                {
                    double u = t - at;
                    if (u < 0 || u > 0.28) continue;
                    double f = 900 + 700 * Math.Sin(Math.PI * u / 0.28);
                    double env = Math.Sin(Math.PI * u / 0.28);
                    v += (Saw(f * u) * 0.6 + Noise(rng) * 0.25) * env;
                }
                return v;
            }, 0.5);
        // The cart: two short toots of a little horn.
        static AudioClip MakeHorn() => Clip("Horn", 0.6f, (t, rng) =>
            {
                double v = 0;
                foreach (double at in new[] { 0.0, 0.26 })
                {
                    double u = t - at;
                    if (u < 0 || u > 0.18) continue;
                    double env = Math.Min(1, u / 0.01) * Math.Min(1, (0.18 - u) / 0.02);
                    v += (Saw(420 * u) + Saw(530 * u)) * 0.5 * env;
                }
                return v;
            }, 0.35);
        // The reveal: a ratchet click for each face the card flips past, and the stamp it lands with.
        static AudioClip MakeRatchet() => Clip("Ratchet", 0.05f, (t, rng) => Noise(rng) * Math.Exp(-t / 0.006) + Math.Sin(2 * Math.PI * 2400 * t) * Math.Exp(-t / 0.01) * 0.5);
        static AudioClip MakeStamp() => Clip("Stamp", 0.5f, (t, rng) =>
                Math.Sin(2 * Math.PI * (120 + 200 * Math.Exp(-t / 0.03)) * t) * Math.Exp(-t / 0.12) + Noise(rng) * Math.Exp(-t / 0.015) * 0.6
                + Math.Sin(2 * Math.PI * 1320 * t) * Math.Exp(-t / 0.15) * 0.25, 0.6);

        static double Noise(System.Random rng) => rng.NextDouble() * 2 - 1;
        static double Saw(double phase) => 2 * (phase - Math.Floor(phase + 0.5));

        // Bells: each note a struck sine with a little octave shimmer, ringing out, one after another.
        static AudioClip MakeChime(string name, double[] notes) => Clip(name, 0.5f + notes.Length * 0.07f, (t, rng) =>
        {
            double v = 0;
            for (int i = 0; i < notes.Length; i++)
            {
                double s = t - i * 0.07;
                if (s < 0) continue;
                v += (Math.Sin(2 * Math.PI * notes[i] * s) + 0.3 * Math.Sin(2 * Math.PI * notes[i] * 2.01 * s)) * Math.Exp(-s / 0.18) * Math.Min(1, s / 0.003);
            }
            return v * 0.42;
        });
        // Coins: a dozen bright little clinks, each pitched apart, landing close together.
        static AudioClip MakeJingle() => Clip("Jingle", 0.9f, (t, rng) =>
        {
            double v = 0;
            for (int i = 0; i < 12; i++)
            {
                double s = t - i * 0.045 - 0.012 * Math.Sin(i * 7.3);
                if (s < 0) continue;
                double f = 2600 + 900 * Math.Sin(i * 2.1) + 400 * i % 700;
                v += (Math.Sin(2 * Math.PI * f * s) + 0.5 * Math.Sin(2 * Math.PI * f * 2.76 * s)) * Math.Exp(-s / 0.05);
            }
            return v * 0.16;
        });
        // A foghorn across the water: a low reedy note, swelling and dying away.
        static AudioClip MakeFoghorn() => Clip("Foghorn", 3.2f, (t, rng) =>
        {
            double env = Math.Min(1, t / 0.5) * Math.Exp(-Math.Max(0, t - 1.8) / 0.5);
            double f = 110 + 1.5 * Math.Sin(t * 3);
            double reed = Math.Sin(2 * Math.PI * f * t) + 0.5 * Math.Sin(4 * Math.PI * f * t) + 0.25 * Math.Sin(6 * Math.PI * f * t);
            return reed * env * 0.3;
        }, 0.08);

        static AudioClip Clip(string name, float seconds, Func<double, System.Random, double> sample, double lowPass = 1) => Render(name, seconds, sample, lowPass, false);
        static AudioClip Loop(string name, float seconds, Func<double, System.Random, double> sample, double lowPass) => Render(name, seconds, sample, lowPass, true);

        /// Renders `sample(time, rng)` to a mono clip through a one-pole low-pass, normalised;
        /// a loop is crossfaded end into start so it never clicks.
        static AudioClip Render(string name, float seconds, Func<double, System.Random, double> sample, double lowPass, bool loop)
        {
            int n = (int)(Rate * seconds);
            int tail = loop ? Rate / 4 : 0;
            var raw = new float[n + tail];
            var rng = new System.Random(name.GetHashCode());
            double filtered = 0;
            for (int i = 0; i < raw.Length; i++)
            {
                filtered += lowPass * (sample((double)(i % n) / Rate, rng) - filtered);
                raw[i] = (float)filtered;
            }
            var data = new float[n];
            Array.Copy(raw, data, n);
            // (a loop: its own extra quarter second faded over its start)
            for (int i = 0; i < tail; i++) { float w = (float)i / tail; data[i] = data[i] * w + raw[n + i] * (1 - w); }
            float peak = 0;
            foreach (var v in data) peak = Mathf.Max(peak, Mathf.Abs(v));
            if (peak > 0) for (int i = 0; i < n; i++) data[i] /= peak;
            if (!loop) { int fade = Math.Min(n, Rate / 200); for (int i = 0; i < fade; i++) data[n - 1 - i] *= (float)i / fade; }
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
