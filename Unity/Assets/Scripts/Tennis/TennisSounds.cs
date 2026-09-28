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
        AudioClip pop, popSweet, bounce, net, whoosh, applause, gasp, chimeGood, chimeBad, thump, perfectSting, smashCrack, whiffSwish, whiffSlide, rivalPop;
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

        public void Hit(Timing grade, float power) => Hit(grade, power, false);
        /// Contact only (called when the ball leaves the strings). Every hit gets a body thump under
        /// the string pop so routine hits land solid; a perfect adds a bright sting, a smash a crack.
        public void Hit(Timing grade, float power, bool smash)
        {
            float v = Mathf.Lerp(.5f, 1f, Mathf.Clamp01(power));
            source.pitch = Mathf.Lerp(.94f, 1.08f, Mathf.Clamp01(power));
            source.PlayOneShot(grade >= Timing.Great ? popSweet : pop, v);
            source.pitch = 1;
            source.PlayOneShot(thump, Mathf.Lerp(.35f, .7f, Mathf.Clamp01(power)));
            if (smash) source.PlayOneShot(smashCrack, .95f);
            else if (grade >= Timing.Perfect) source.PlayOneShot(perfectSting, .7f);
        }
        /// The opponent's contact: the same honest pop and thump, a notch quieter and duller.
        public void RivalHit(float power) { source.PlayOneShot(rivalPop, Mathf.Lerp(.45f, .75f, power)); source.PlayOneShot(thump, .3f); }
        /// A confirmed whiff: louder and sillier than any routine hit — a big air swish and a
        /// falling slide whistle.
        public void Whiff() { source.PlayOneShot(whiffSwish, .8f); source.PlayOneShot(whiffSlide, .6f); }
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
            // Plan 2 contact weight and stingers.
            thump = Clip("Body thump", .16f, (t, r) => Math.Sin(2 * Math.PI * (70 + 90 * Math.Exp(-t / .02)) * t) * Math.Exp(-t / .045) + Burst(t, .002, r) * .2, .5);
            rivalPop = Clip("Rival pop", .12f, (t, r) => Burst(t, .004, r) * .7 + Ring(t, 470, .016) * .55 + Ring(t, 1050, .008) * .25, .45);
            perfectSting = Clip("Perfect sting", .6f, (t, r) => Ring(t, 1567.98, .16) * .5 + Ring(t - .05, 2093, .2) * .45 + Ring(t - .1, 2637, .22) * .35
                + Math.Sin(2 * Math.PI * (55 + 60 * Math.Exp(-t / .05)) * t) * Math.Exp(-t / .12) * .8);
            smashCrack = Clip("Smash crack", .7f, (t, r) => Burst(t, .012, r) * 1.0 + Math.Sin(2 * Math.PI * (45 + 120 * Math.Exp(-t / .04)) * t) * Math.Exp(-t / .2) * 1.1
                + Noise(r) * Math.Exp(-t / .25) * .25, .7);
            whiffSwish = Clip("Whiff swish", .42f, (t, r) => { double e = Math.Sin(Math.Min(1, t / .42) * Math.PI); return Noise(r) * e * e * 1.0; }, .25);
            whiffSlide = Clip("Whiff slide", .7f, (t, r) =>
            {
                // slide whistle: pitch falls from ~1.3 kHz to ~300 Hz with a little wobble
                double f = 300 + 1000 * Math.Exp(-t / .25), ph = 2 * Math.PI * (300 * t + 250 * (1 - Math.Exp(-t / .25))) + .8 * Math.Sin(2 * Math.PI * 7 * t);
                double env = Math.Min(1, t / .03) * Math.Exp(-t / .45);
                return (Math.Sin(ph) + .25 * Math.Sin(2 * ph)) * env * (f > 0 ? 1 : 0);
            });
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
