using GolfArcade.Course;
using UnityEngine;

namespace GolfArcade.Game
{
    /// How a shot looks in the air and when it comes down. The ball draws a tracer (Tracer): a
    /// thin luminous line along its whole flight that stays over the hole, in the colour of the
    /// swing's rating — green for a clean one, yellow for an imperfect one, red for a mishit.
    /// Every bounce throws up a puff of the ground it hit, and a ball in the sea splashes. The
    /// rating bands and colours are Resources/Course/swing_trail.json, read off Codex's
    /// Hole_12_Cinematic.blend by blender/scripts/hole12_cinematic_export.py.
    public sealed class ShotEffects : MonoBehaviour
    {
        public enum Quality { Pure, Fair, OffLine }

        [System.Serializable] public class RatingBand { public string quality; public float min, max; }
        [System.Serializable] public class Rgb { public float[] green, yellow, red; }
        [System.Serializable]
        public class Recipe
        {
            public Rgb colors;
            public float headAlpha = 0.42f, tailAlpha = 0.04f, headRadiusBalls = 0.68f, tailRadiusBalls = 0.056f;
            public float lengthSeconds = 0.333f, fadeSeconds = 0.533f;
            public RatingBand[] ratingBands;
        }
        static Recipe recipe;
        public static Recipe TrailRecipe() => Spec();
        static Recipe Spec()
        {
            if (recipe != null) return recipe;
            var text = Resources.Load<TextAsset>("Course/swing_trail");
            recipe = text ? JsonUtility.FromJson<Recipe>(text.text) : new Recipe();
            recipe.colors ??= new Rgb();
            recipe.colors.green ??= new[] { 0.035f, 0.95f, 0.15f };
            recipe.colors.yellow ??= new[] { 1f, 0.65f, 0.025f };
            recipe.colors.red ??= new[] { 1f, 0.035f, 0.025f };
            if (recipe.ratingBands == null || recipe.ratingBands.Length == 0)
                recipe.ratingBands = new[] { new RatingBand { quality = "red", min = 0, max = 50 }, new RatingBand { quality = "yellow", min = 50, max = 80 }, new RatingBand { quality = "green", min = 80, max = 101 } };
            return recipe;
        }
        static Color Of(float[] c) => new(c[0], c[1], c[2]);
        public static Color PureColor => Of(Spec().colors.green);
        public static Color FairColor => Of(Spec().colors.yellow);
        public static Color OffLineColor => Of(Spec().colors.red);
        public static Color ColorOf(Quality q) => q == Quality.Pure ? PureColor : q == Quality.Fair ? FairColor : OffLineColor;

        /// The band a 0–100 rating falls in, by Codex's thresholds.
        public static Quality QualityOf(float rating)
        {
            foreach (var b in Spec().ratingBands)
                if (rating >= b.min && rating < b.max)
                    return b.quality == "green" ? Quality.Pure : b.quality == "yellow" ? Quality.Fair : Quality.OffLine;
            return rating >= 80 ? Quality.Pure : rating >= 50 ? Quality.Fair : Quality.OffLine;
        }

        static Texture2D soft;
        static Material particleMaterial;

        Tracer tracer;
        CometTail comet;
        bool cometShot;
        Transform ball;
        ParticleSystem puffs, splash;
        Camera view;

        public static ShotEffects Create(Transform parent, Transform ball, Camera view, BallLook look)
        {
            var go = new GameObject("Shot effects");
            go.transform.SetParent(parent, false);
            var fx = go.AddComponent<ShotEffects>();
            fx.view = view;
            fx.ball = ball;
            fx.tracer = Tracer.Create(go.transform, view);
            fx.comet = CometTail.Create(go.transform, look);
            fx.SetQuality(Quality.Fair);
            fx.puffs = fx.BuildParticles("Puffs", 0.7f, 1.1f, 0.22f, 0.5f, -2.2f);
            fx.splash = fx.BuildParticles("Splash", 0.9f, 1.4f, 0.28f, 0.9f, -3.5f);
            return fx;
        }

        static Texture2D band;
        /// A strip solid down its middle, fading to nothing at both edges — across a line so it
        /// reads as light rather than a painted stripe.
        public static Texture2D Band()
        {
            if (band) return band;
            const int n = 32;
            band = new Texture2D(4, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[4 * n];
            for (int y = 0; y < n; y++)
            {
                float d = Mathf.Abs((y + 0.5f) / n - 0.5f) * 2f;
                float a = Mathf.Clamp01(1 - d); a = a * a * (3 - 2 * a);
                for (int x = 0; x < 4; x++) px[y * 4 + x] = new Color32(255, 255, 255, (byte)(255 * a));
            }
            band.SetPixels32(px); band.Apply();
            return band;
        }

        /// A disc that fades to nothing at its edge, for particles.
        public static Texture2D Soft()
        {
            if (soft) return soft;
            const int n = 64;
            soft = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - n / 2f) * (x + 0.5f - n / 2f) + (y + 0.5f - n / 2f) * (y + 0.5f - n / 2f)) / (n / 2f);
                    float a = Mathf.Clamp01(1 - d); a = a * a * (3 - 2 * a);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(255 * a));
                }
            soft.SetPixels32(px); soft.Apply();
            return soft;
        }

        public static Material ParticleMaterial()
        {
            if (particleMaterial) return particleMaterial;
            var shader = Shader.Find("GolfArcade/ParticleSoft");
            if (!shader) Debug.LogError("GolfArcade/ParticleSoft missing from the build — run Golf Arcade → Set Up Project");
            particleMaterial = new Material(shader ?? Shader.Find("Unlit/Color")) { mainTexture = Soft() };
            return particleMaterial;
        }

        ParticleSystem BuildParticles(string name, float sizeMin, float sizeMax, float lifeMin, float lifeMax, float gravity)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = false; main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, lifeMax);
            main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
            main.startSpeed = 0; main.gravityModifier = 0; main.maxParticles = 200;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission; emission.enabled = false;
            var shape = ps.shape; shape.enabled = false;
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                      new[] { new GradientAlphaKey(0.9f, 0), new GradientAlphaKey(0.6f, 0.5f), new GradientAlphaKey(0, 1) });
            col.color = g;
            var size = ps.sizeOverLifetime; size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, 0.5f), new Keyframe(0.3f, 1f), new Keyframe(1, 1.3f)));
            var force = ps.forceOverLifetime; force.enabled = true; force.y = gravity;
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = ParticleMaterial();
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            return ps;
        }

        // ---- in flight

        Quality quality = Quality.Fair;
        public void SetQuality(Quality q) => quality = q;

        /// Which ball the tracer follows: the game's, or Codex's during his shot.
        public void Follow(Transform t) => ball = t;

        bool flying;
        /// The ball is away: the tracer line — or, for the ball POV, Codex's short tube behind it.
        public void BeginFlight(bool pov = false)
        {
            cometShot = pov;
            if (pov) comet.Begin(ColorOf(quality)); else tracer.Begin(ColorOf(quality));
            flying = true;
        }
        /// The ball is down: the line ends where it landed, the way TopTracer draws it — the
        /// bounces and the roll are the ball's own, not the line's; the POV's tube narrows away.
        public void Land()
        {
            if (cometShot) comet.Land();
            else if (flying && ball) tracer.Push(ball.position);
            flying = false;
        }
        /// The shot is over: the line stays over the hole a while, then goes.
        public void EndFlight() { flying = false; if (!cometShot) tracer.FadeOut(2.5f); }
        /// Setting up the next shot: the line goes now.
        public void ClearTracer() { flying = false; tracer.Clear(); comet.Clear(); }

        void LateUpdate()
        {
            if (flying && ball && !cometShot) tracer.Push(ball.position);
        }

        // ---- coming down

        /// A bounce or the landing: the ground it hit decides the puff.
        public void Touchdown(Vector3 at, CourseLie lie, float strength)
        {
            strength = Mathf.Clamp(strength, 0.3f, 1.5f);
            if (lie == CourseLie.Water) { Splash(at); return; }
            Color a, b;
            switch (lie)
            {
                case CourseLie.Bunker: a = new Color(0.94f, 0.85f, 0.62f); b = new Color(0.86f, 0.74f, 0.5f); break;
                case CourseLie.Green: a = new Color(0.75f, 0.95f, 0.5f); b = new Color(0.55f, 0.85f, 0.4f); break;
                case CourseLie.Rough: case CourseLie.OutOfBounds: a = new Color(0.4f, 0.65f, 0.28f); b = new Color(0.55f, 0.45f, 0.3f); break;
                default: a = new Color(0.62f, 0.9f, 0.4f); b = new Color(0.45f, 0.75f, 0.3f); break;
            }
            int count = Mathf.RoundToInt(10 + 14 * strength);
            for (int i = 0; i < count; i++)
            {
                var dir = Random.insideUnitCircle * 1.6f;
                var p = new ParticleSystem.EmitParams
                {
                    position = at + new Vector3(dir.x * 0.1f, 0.05f, dir.y * 0.1f),
                    velocity = new Vector3(dir.x, Random.Range(1.2f, 3.2f) * strength, dir.y) * strength,
                    startColor = Color.Lerp(a, b, Random.value),
                    startSize = Random.Range(0.35f, 0.75f) * (0.7f + 0.5f * strength),
                    startLifetime = Random.Range(0.3f, 0.6f),
                };
                puffs.Emit(p, 1);
            }
        }

        public void Splash(Vector3 at)
        {
            at.y = 0.02f;
            for (int i = 0; i < 46; i++)
            {
                var dir = Random.insideUnitCircle;
                float up = Random.Range(2.5f, 6.5f);
                var p = new ParticleSystem.EmitParams
                {
                    position = at + new Vector3(dir.x * 0.25f, 0, dir.y * 0.25f),
                    velocity = new Vector3(dir.x * Random.Range(0.6f, 2.2f), up, dir.y * Random.Range(0.6f, 2.2f)),
                    startColor = Color.Lerp(new Color(0.85f, 0.95f, 1f), new Color(0.55f, 0.8f, 1f), Random.value),
                    startSize = Random.Range(0.3f, 0.8f),
                    startLifetime = Random.Range(0.5f, 1.0f),
                };
                splash.Emit(p, 1);
            }
            // the ring on the water
            for (int i = 0; i < 24; i++)
            {
                float a = i * Mathf.PI * 2 / 24;
                var p = new ParticleSystem.EmitParams
                {
                    position = at + new Vector3(Mathf.Cos(a) * 0.4f, 0.01f, Mathf.Sin(a) * 0.4f),
                    velocity = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 2.4f,
                    startColor = new Color(0.9f, 0.97f, 1f, 0.8f),
                    startSize = 0.45f, startLifetime = 0.9f,
                };
                puffs.Emit(p, 1);
            }
        }
    }
}
