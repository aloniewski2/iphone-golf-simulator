using GolfArcade.Course;
using UnityEngine;

namespace GolfArcade.Game
{
    /// How a shot looks in the air and when it comes down. The ball draws a tracer (Tracer): a
    /// thin luminous line along its whole flight that stays over the hole, in the colour of the
    /// swing's rating — green for a clean one, yellow for an imperfect one, red for a mishit.
    /// Every bounce throws up a puff of the ground it hit, and a ball in the sea splashes. The
    /// strike itself flashes: a starburst at the ball, a shockwave ring across the ground and the
    /// turf it cut flying forward (Resources/Effects: Higgsfield sprites — flash, ring, grass,
    /// dust, splash). The
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
            // cartoon dust for the puffs; the splash keeps its soft droplets
            var dust = SpriteMaterial("dust");
            if (dust) fx.puffs.GetComponent<ParticleSystemRenderer>().sharedMaterial = dust;
            fx.clippings = fx.BuildParticles("Clippings", 0.28f, 0.5f, 0.55f, 0.95f, -9f);
            var grass = SpriteMaterial("grass");
            if (grass)
            {
                fx.clippings.GetComponent<ParticleSystemRenderer>().sharedMaterial = grass;
                var sheet = fx.clippings.textureSheetAnimation;
                sheet.enabled = true; sheet.numTilesX = 2; sheet.numTilesY = 2;
                sheet.animation = ParticleSystemAnimationType.WholeSheet;
                sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f);
                sheet.startFrame = new ParticleSystem.MinMaxCurve(0f, 3.999f / 4f);   // one clump each, at random
                var main = fx.clippings.main;
                main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
                var spin = fx.clippings.rotationOverLifetime; spin.enabled = true;
                spin.z = new ParticleSystem.MinMaxCurve(-6f, 6f);
                var size = fx.clippings.sizeOverLifetime; size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1, 1, 0.8f));
            }
            fx.flash = fx.Burst("Strike flash", "flash", false);
            fx.ring = fx.Burst("Strike ring", "ring", true);
            fx.crown = fx.Burst("Splash crown", "splash", false);
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

        /// A particle material wearing one of the Higgsfield sprites in Resources/Effects.
        static Material SpriteMaterial(string name)
        {
            var tex = Resources.Load<Texture2D>("Effects/" + name);
            if (!tex) return null;
            tex.wrapMode = TextureWrapMode.Clamp;
            return new Material(ParticleMaterial()) { mainTexture = tex, name = "Effect " + name };
        }

        /// A one-shot sprite that grows and fades: a quad facing the camera, or lying flat on the ground.
        sealed class Flare
        {
            public Transform quad; public Material mat; public bool flat;
            public float age = -1, seconds, from, to; public Color tint;
        }
        Flare flash, ring, crown;
        ParticleSystem clippings;

        Flare Burst(string name, string sprite, bool flat)
        {
            var mat = SpriteMaterial(sprite);
            if (!mat) return null;
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            go.layer = BallLook.OverlayLayer;   // the minimap leaves it out
            go.SetActive(false);
            return new Flare { quad = go.transform, mat = mat, flat = flat };
        }

        void Fire(Flare f, Vector3 at, float seconds, float from, float to, Color tint)
        {
            if (f == null) return;
            f.quad.position = at; f.age = 0; f.seconds = seconds; f.from = from; f.to = to; f.tint = tint;
            f.quad.localRotation = f.flat ? Quaternion.Euler(90, 0, 0) : Quaternion.identity;
            f.quad.gameObject.SetActive(true);
            Step(f, 0);
        }

        void Step(Flare f, float dt)
        {
            if (f == null || f.age < 0) return;
            f.age += dt;
            float t = f.age / f.seconds;
            if (t >= 1) { f.age = -1; f.quad.gameObject.SetActive(false); return; }
            float grow = 1 - (1 - t) * (1 - t) * (1 - t);   // out fast, then settle
            f.quad.localScale = Vector3.one * Mathf.Lerp(f.from, f.to, grow);
            var c = f.tint; c.a *= t < 0.25f ? 1 : 1 - (t - 0.25f) / 0.75f; f.mat.color = c;
            if (!f.flat && view)
            {
                // face the camera, with a quick spin on the starburst
                f.quad.rotation = Quaternion.LookRotation(f.quad.position - view.transform.position, view.transform.up) * Quaternion.Euler(0, 0, f.age * 90f);
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            Step(flash, dt); Step(ring, dt); Step(crown, dt);
        }

        /// The club meets the ball at `at`, sending it along `forward`, off `lie`, at `power`
        /// (0–1): a starburst, a ring across the ground, and what the club cut — turf clippings
        /// off grass, a spray of sand from a bunker. A putt only rings, softly.
        public void Strike(Vector3 at, Vector3 forward, CourseLie lie, float power, bool putt)
        {
            power = Mathf.Clamp01(power);
            var ground = at; ground.y += 0.03f;
            if (putt) { Fire(ring, ground, 0.4f, 0.2f, 0.9f, new Color(1, 1, 1, 0.55f)); return; }
            Fire(flash, at + Vector3.up * 0.12f, 0.22f, 0.25f, 0.9f + 1.1f * power, Color.white);
            Fire(ring, ground, 0.45f, 0.3f, 2.2f + 2.5f * power, new Color(1, 1, 1, 0.75f));
            forward.y = 0; forward.Normalize();
            var side = Vector3.Cross(Vector3.up, forward);
            if (lie == CourseLie.Bunker) { Touchdown(at, CourseLie.Bunker, 1.2f + power); return; }
            if (lie == CourseLie.Water || !clippings) return;
            int count = Mathf.RoundToInt(8 + 22 * power) * (lie == CourseLie.Rough ? 2 : 1);
            for (int i = 0; i < count; i++)
            {
                var v = forward * Random.Range(1.5f, 6f) * (0.5f + power) + side * Random.Range(-1.6f, 1.6f) + Vector3.up * Random.Range(1.8f, 4.5f) * (0.6f + 0.6f * power);
                clippings.Emit(new ParticleSystem.EmitParams
                {
                    position = at + side * Random.Range(-0.08f, 0.08f) + Vector3.up * 0.04f,
                    velocity = v,
                    startColor = Color.Lerp(Color.white, new Color(0.8f, 0.95f, 0.7f), Random.value),
                    startSize = Random.Range(0.28f, 0.5f),
                    startLifetime = Random.Range(0.55f, 0.95f),
                }, 1);
            }
        }

        /// The ball meeting something standing on the course: a shower of leaves out of a crown or
        /// a bush, chips and dust off a trunk, a rock or a wall.
        public void Knock(Vector3 at, bool leaves, bool stone)
        {
            if (leaves && clippings)
            {
                for (int i = 0; i < 44; i++)
                {
                    var v = Random.insideUnitSphere * 3.6f + Vector3.up * 1.6f;
                    clippings.Emit(new ParticleSystem.EmitParams
                    {
                        position = at + Random.insideUnitSphere * 0.8f,
                        velocity = v,
                        startColor = Color.Lerp(new Color(0.35f, 0.62f, 0.3f), new Color(0.62f, 0.85f, 0.4f), Random.value),
                        startSize = Random.Range(0.5f, 0.95f),
                        startLifetime = Random.Range(1.0f, 1.7f),
                    }, 1);
                }
                return;
            }
            Color a = stone ? new Color(0.78f, 0.8f, 0.84f) : new Color(0.55f, 0.4f, 0.28f), b = stone ? new Color(0.6f, 0.62f, 0.68f) : new Color(0.72f, 0.6f, 0.45f);
            for (int i = 0; i < 12; i++)
            {
                var dir = Random.insideUnitSphere;
                puffs.Emit(new ParticleSystem.EmitParams
                {
                    position = at + dir * 0.1f,
                    velocity = dir * 2.2f + Vector3.up * 0.6f,
                    startColor = Color.Lerp(a, b, Random.value),
                    startSize = Random.Range(0.25f, 0.5f),
                    startLifetime = Random.Range(0.3f, 0.55f),
                }, 1);
            }
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
            Fire(crown, at + Vector3.up * 0.6f, 0.7f, 0.6f, 2.6f, Color.white);
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
