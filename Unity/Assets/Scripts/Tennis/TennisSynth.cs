using System;

namespace GolfArcade.Tennis
{
    /// Small sound-synthesis kit for the tennis audio: filters, voices and envelopes that the
    /// music, crowd and court sounds are built from at start-up. Pure C#, no Unity calls, so the
    /// heavy rendering can run off the main thread.
    public static class TennisSynth
    {
        /// RBJ biquad (the "audio EQ cookbook" filters).
        public struct Biquad
        {
            double b0, b1, b2, a1, a2, x1, x2, y1, y2;

            public static Biquad BandPass(double hz, double q, int rate) => Make(hz, q, rate, 0);
            public static Biquad LowPass(double hz, double q, int rate) => Make(hz, q, rate, 1);
            public static Biquad HighPass(double hz, double q, int rate) => Make(hz, q, rate, 2);

            static Biquad Make(double hz, double q, int rate, int kind)
            {
                double w = 2 * Math.PI * Math.Min(hz, rate * .45) / rate, cos = Math.Cos(w), alpha = Math.Sin(w) / (2 * q);
                double a0 = 1 + alpha;
                var f = new Biquad();
                switch (kind)
                {
                    case 0: f.b0 = alpha; f.b1 = 0; f.b2 = -alpha; break;
                    case 1: f.b0 = (1 - cos) / 2; f.b1 = 1 - cos; f.b2 = (1 - cos) / 2; break;
                    default: f.b0 = (1 + cos) / 2; f.b1 = -(1 + cos); f.b2 = (1 + cos) / 2; break;
                }
                f.a1 = -2 * cos; f.a2 = 1 - alpha;
                f.b0 /= a0; f.b1 /= a0; f.b2 /= a0; f.a1 /= a0; f.a2 /= a0;
                return f;
            }

            /// Retune without clearing the filter's memory (for sweeping formants).
            public void Retune(double hz, double q, int rate, int kind = 0)
            {
                var n = Make(hz, q, rate, kind);
                b0 = n.b0; b1 = n.b1; b2 = n.b2; a1 = n.a1; a2 = n.a2;
            }

            public double Process(double x)
            {
                double y = b0 * x + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;
                x2 = x1; x1 = x; y2 = y1; y1 = y;
                return y;
            }
        }

        public static double Midi(double note) => 440 * Math.Pow(2, (note - 69) / 12);
        public static double Noise(Random r) => r.NextDouble() * 2 - 1;

        /// Scale to a peak and return the buffer.
        public static float[] Normalize(float[] data, float peak = .9f)
        {
            float m = 0;
            foreach (var v in data) m = Math.Max(m, Math.Abs(v));
            if (m > 0) for (int i = 0; i < data.Length; i++) data[i] *= peak / m;
            return data;
        }

        /// Make a buffer loop seamlessly: the last `fade` samples are cross-faded into the
        /// start and the buffer is shortened by that much.
        public static float[] Loopable(float[] data, int fade)
        {
            int n = data.Length - fade;
            var o = new float[n];
            Array.Copy(data, o, n);
            for (int i = 0; i < fade; i++)
            {
                double k = (double)i / fade;
                o[i] = (float)(data[n + i] * Math.Cos(k * Math.PI / 2) + o[i] * Math.Sin(k * Math.PI / 2));
            }
            return o;
        }

        /// Add a sound into a buffer at a sample offset.
        public static void Mix(float[] into, int at, int length, Func<int, double> sample, double gain = 1)
        {
            for (int i = 0; i < length; i++)
            {
                int k = at + i;
                if (k < 0) continue;
                if (k >= into.Length) break;
                into[k] += (float)(sample(i) * gain);
            }
        }

        // ---- Instruments (t in seconds since the note started) ----

        /// Steel pan: a bright fundamental with its octave and twelfth, each decaying at its
        /// own rate, and a hint of detune for the shimmer of a hand-hammered drum.
        public static double SteelPan(double t, double hz)
        {
            if (t < 0) return 0;
            double attack = Math.Min(1, t / .003);
            return attack * (Math.Sin(2 * Math.PI * hz * t) * Math.Exp(-t / .55)
                           + .55 * Math.Sin(2 * Math.PI * hz * 2.005 * t) * Math.Exp(-t / .22)
                           + .28 * Math.Sin(2 * Math.PI * hz * 3.01 * t) * Math.Exp(-t / .1)
                           + .12 * Math.Sin(2 * Math.PI * hz * 4.2 * t) * Math.Exp(-t / .04));
        }

        /// Marimba: a woody fundamental with the bar's fourth-harmonic ring on the strike.
        public static double Marimba(double t, double hz)
        {
            if (t < 0) return 0;
            double attack = Math.Min(1, t / .002);
            return attack * (Math.Sin(2 * Math.PI * hz * t) * Math.Exp(-t / .38)
                           + .4 * Math.Sin(2 * Math.PI * hz * 3.93 * t) * Math.Exp(-t / .05));
        }

        /// Plucked bass: round, with a little second harmonic so it reads on small speakers.
        public static double Bass(double t, double hz, double length)
        {
            if (t < 0) return 0;
            double env = Math.Min(1, t / .004) * Math.Exp(-t / .3) * (t > length ? Math.Exp(-(t - length) / .03) : 1);
            return env * (Math.Sin(2 * Math.PI * hz * t) + .35 * Math.Sin(4 * Math.PI * hz * t) + .12 * Math.Sin(6 * Math.PI * hz * t));
        }

        /// Warm pad tone (for sustained chords): soft attack and release.
        public static double Pad(double t, double hz, double length)
        {
            if (t < 0) return 0;
            double env = Math.Min(1, t / .35) * (t > length ? Math.Max(0, 1 - (t - length) / .5) : 1);
            double wobble = 1 + .004 * Math.Sin(2 * Math.PI * 5.2 * t);
            return env * (Math.Sin(2 * Math.PI * hz * wobble * t) + .25 * Math.Sin(2 * Math.PI * hz * 3 * t) + .1 * Math.Sin(2 * Math.PI * hz * 5 * t));
        }

        /// Soft kick: a sine dropping in pitch.
        public static double Kick(double t)
        {
            if (t < 0 || t > .4) return 0;
            double f = 48 + 60 * Math.Exp(-t / .03);
            return Math.Sin(2 * Math.PI * f * t) * Math.Exp(-t / .16);
        }

        /// Woodblock / clave.
        public static double Clave(double t)
        {
            if (t < 0 || t > .2) return 0;
            return (Math.Sin(2 * Math.PI * 1850 * t) + .5 * Math.Sin(2 * Math.PI * 2750 * t)) * Math.Exp(-t / .028);
        }

        /// A voiced crowd member: a glottal buzz shaped by two vowel formants.
        public sealed class Voice
        {
            Biquad f1, f2; double phase; readonly Random rng; readonly int rate;
            public Voice(int seed, int rate) { rng = new Random(seed); this.rate = rate; f1 = Biquad.BandPass(600, 5, rate); f2 = Biquad.BandPass(1100, 6, rate); }
            public void Vowel(double first, double second) { f1.Retune(first, 5, rate); f2.Retune(second, 6, rate); }
            /// One sample at pitch `hz` with `breath` (0..1) of aspiration mixed into the buzz.
            public double Next(double hz, double breath)
            {
                phase += hz / rate; if (phase >= 1) phase -= 1;
                double buzz = 1 - 2 * phase;                              // sawtooth
                double src = buzz * (1 - breath) + Noise(rng) * breath * 1.6;
                return f1.Process(src) * 1.0 + f2.Process(src) * .6;
            }
        }

        // Vowel formants (F1, F2), roughly an adult average.
        public static readonly double[] Ah = { 730, 1090 }, Eh = { 530, 1840 }, Oo = { 320, 870 }, Aw = { 570, 840 }, Ee = { 300, 2200 };
    }
}
