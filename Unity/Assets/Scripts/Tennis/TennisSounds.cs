using System;
using UnityEngine;

namespace GolfArcade.Tennis
{
    /// Tennis sound, synthesised at start-up like the golf set so nothing ships as audio
    /// files: the pop of strings on felt (sharper for a cleaner hit), the bounce, the net,
    /// the racket's whoosh, crowd applause and a gasp, and short umpire chimes for calls.
    public sealed class TennisSounds : MonoBehaviour
    {
        const int Rate = 44100;
        AudioSource source, crowd;
        AudioClip pop, popSweet, bounce, net, whoosh, applause, gasp, chimeGood, chimeBad;
        float crowdTarget;

        public static TennisSounds Create(Transform parent)
        {
            var go = new GameObject("Tennis sounds");
            go.transform.SetParent(parent, false);
            var s = go.AddComponent<TennisSounds>();
            s.source = go.AddComponent<AudioSource>(); s.source.playOnAwake = false; s.source.spatialBlend = 0;
            s.crowd = go.AddComponent<AudioSource>(); s.crowd.playOnAwake = false; s.crowd.spatialBlend = 0; s.crowd.loop = true;
            s.Synthesize();
            s.crowd.clip = s.applause;
            return s;
        }

        public void Hit(Timing grade, float power)
        {
            float v = Mathf.Lerp(.45f, 1f, Mathf.Clamp01(power));
            source.pitch = Mathf.Lerp(.94f, 1.08f, Mathf.Clamp01(power));
            source.PlayOneShot(grade >= Timing.Great ? popSweet : pop, v);
            source.pitch = 1;
        }
        public void Bounce(float pace) => source.PlayOneShot(bounce, Mathf.Lerp(.25f, .7f, Mathf.Clamp01(pace)));
        public void Net() => source.PlayOneShot(net, .8f);
        public void Whoosh(float power) => source.PlayOneShot(whoosh, Mathf.Lerp(.15f, .5f, Mathf.Clamp01(power)));
        public void Call(bool good) => source.PlayOneShot(good ? chimeGood : chimeBad, .55f);
        public void Gasp() => source.PlayOneShot(gasp, .45f);

        /// Applause that swells and dies away; stronger for longer rallies.
        public void Applaud(float strength)
        {
            crowdTarget = Mathf.Lerp(.25f, .8f, Mathf.Clamp01(strength));
            if (!crowd.isPlaying) { crowd.volume = 0; crowd.Play(); }
        }

        void Update()
        {
            if (!crowd.isPlaying) return;
            crowd.volume = Mathf.MoveTowards(crowd.volume, crowdTarget, Time.unscaledDeltaTime * (crowdTarget > crowd.volume ? 3f : .5f));
            crowdTarget = Mathf.MoveTowards(crowdTarget, 0, Time.unscaledDeltaTime * .35f);
            if (crowdTarget <= 0 && crowd.volume <= .005f) crowd.Stop();
        }

        void Synthesize()
        {
            // Strings on felt: a very short broadband click plus the ball's hollow resonance.
            pop = Clip("Pop", .12f, (t, r) => Burst(t, .0035, r) * .8 + Ring(t, 520, .018) * .55 + Ring(t, 1180, .009) * .3, .6);
            popSweet = Clip("Pop sweet", .14f, (t, r) => Burst(t, .0025, r) * .7 + Ring(t, 610, .024) * .7 + Ring(t, 1420, .012) * .35, .8);
            bounce = Clip("Bounce", .1f, (t, r) => Burst(t, .004, r) * .5 + Ring(t, 190, .02) * .8, .3);
            net = Clip("Net", .35f, (t, r) => Noise(r) * Math.Exp(-t / .07) * .7 + Ring(t, 95, .08) * .6, .12);
            whoosh = Clip("Swing whoosh", .26f, (t, r) => { double e = Math.Sin(Math.Min(1, t / .26) * Math.PI); return Noise(r) * e * e * .8; }, .18);
            // Applause: many soft claps at random moments, loopable over two seconds.
            applause = Clip("Applause", 2f, (t, r) =>
            {
                double v = Noise(r) * .08;
                double phase = (t * 37) % 1.0, phase2 = (t * 53 + .37) % 1.0, phase3 = (t * 29 + .71) % 1.0;
                v += Math.Exp(-phase / .02) * Noise(r) * .5 + Math.Exp(-phase2 / .015) * Noise(r) * .4 + Math.Exp(-phase3 / .025) * Noise(r) * .45;
                return v;
            }, .35, fadeEnds: false);
            gasp = Clip("Gasp", .7f, (t, r) =>
            {
                double env = Math.Sin(Math.Min(1, t / .7) * Math.PI);
                return (Noise(r) * .6 + Math.Sin(2 * Math.PI * 240 * t) * .1) * env;
            }, .06);
            chimeGood = Clip("Call good", .5f, (t, r) => Ring(t, 880, .14) * .6 + Ring(t - .09, 1318.5, .2) * .6);
            chimeBad = Clip("Call out", .5f, (t, r) => Ring(t, 392, .16) * .7 + Ring(t - .1, 330, .22) * .6);
        }

        static int Seed(string name) { int h = 23; foreach (char c in name) h = h * 31 + c; return h; }
        static double Noise(System.Random rng) => rng.NextDouble() * 2 - 1;
        static double Burst(double t, double tau, System.Random rng) => t < 0 ? 0 : Noise(rng) * Math.Exp(-t / tau);
        static double Ring(double t, double hz, double tau) => t < 0 ? 0 : Math.Sin(2 * Math.PI * hz * t) * Math.Exp(-t / tau);

        static AudioClip Clip(string name, float seconds, Func<double, System.Random, double> sample, double lowPass = 1, bool fadeEnds = true)
        {
            int n = (int)(Rate * seconds);
            var data = new float[n];
            var rng = new System.Random(Seed(name));
            double filtered = 0, peak = 0;
            for (int i = 0; i < n; i++)
            {
                filtered += lowPass * (sample((double)i / Rate, rng) - filtered);
                data[i] = (float)filtered; peak = Math.Max(peak, Math.Abs(filtered));
            }
            if (peak > 0) for (int i = 0; i < n; i++) data[i] = (float)(data[i] / peak);
            int fade = fadeEnds ? Math.Min(n, Rate / 200) : 0;
            for (int i = 0; i < fade; i++) data[n - 1 - i] *= (float)i / fade;
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
