using GolfArcade.Course;
using UnityEngine;

namespace GolfArcade.Game
{
    /// How a shot looks in the air and when it comes down. The ball trails Codex's tube — a
    /// translucent taper, fat and bright at the ball, a hair at the tail, in the colour of the
    /// swing's rating: green for a clean one, yellow for an imperfect one, red for a mishit — with
    /// a soft halo riding on the ball; the trail narrows away after the landing. Every bounce
    /// throws up a puff of the ground it hit, and a ball in the sea splashes. The trail's recipe
    /// is Resources/Course/swing_trail.json, read off Hole_12_Cinematic.blend by
    /// blender/scripts/hole12_cinematic_export.py.
    public sealed class ShotEffects : MonoBehaviour
    {
        public enum Quality { Pure, Fair, OffLine }

        [System.Serializable] class RatingBand { public string quality; public float min, max; }
        [System.Serializable] class Rgb { public float[] green, yellow, red; }
        [System.Serializable]
        class Recipe
        {
            public Rgb colors;
            public float headAlpha = 0.42f, tailAlpha = 0.04f, headRadiusBalls = 0.68f, tailRadiusBalls = 0.056f;
            public float lengthSeconds = 0.333f, fadeSeconds = 0.533f;
            public RatingBand[] ratingBands;
        }
        static Recipe recipe;
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

        TrailRenderer trail;
        Transform halo;
        bool landedOnce;
        Material haloMaterial;
        ParticleSystem puffs, splash;
        Camera view;

        public static ShotEffects Create(Transform parent, Transform ball, Camera view)
        {
            var go = new GameObject("Shot effects");
            go.transform.SetParent(parent, false);
            var fx = go.AddComponent<ShotEffects>();
            fx.view = view;
            fx.BuildTrail(ball);
            fx.BuildHalo(ball);
            fx.SetQuality(Quality.Fair);
            fx.puffs = fx.BuildParticles("Puffs", 0.7f, 1.1f, 0.22f, 0.5f, -2.2f);
            fx.splash = fx.BuildParticles("Splash", 0.9f, 1.4f, 0.28f, 0.9f, -3.5f);
            return fx;
        }

        static Texture2D band;
        /// A strip that is solid down its middle and fades to nothing at both edges — laid along
        /// the trail so it reads as a soft ribbon of light rather than a painted stripe.
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

        /// A disc that fades to nothing at its edge, for particles and the halo.
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

        /// Codex's tube as a ribbon on the ball: widest at the ball, tapering as its radius did
        /// (0.025 + 0.28·t^1.2 over 10 frames), alpha falling from the head's to the tail's, the
        /// band texture feathering both edges so it reads as light.
        void BuildTrail(Transform ball)
        {
            var spec = Spec();
            if (!ball.gameObject.TryGetComponent(out trail)) trail = ball.gameObject.AddComponent<TrailRenderer>();
            trail.time = spec.lengthSeconds;
            trail.minVertexDistance = 0.15f;
            float tail = spec.tailRadiusBalls / spec.headRadiusBalls, mid = (0.025f + 0.28f * Mathf.Pow(0.5f, 1.2f)) / 0.305f;
            trail.widthCurve = new AnimationCurve(new Keyframe(0, 1f), new Keyframe(0.5f, mid), new Keyframe(1, tail));
            trail.widthMultiplier = 0.4f;
            trail.numCornerVertices = 4; trail.numCapVertices = 6;
            trail.alignment = LineAlignment.View;
            trail.textureMode = LineTextureMode.Stretch;
            trail.material = new Material(ParticleMaterial()) { mainTexture = Band() };
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.emitting = false;
        }

        void BuildHalo(Transform ball)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "Halo";
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(ball, false);
            go.transform.localScale = Vector3.one * 4.5f; // the ball is 0.12 yd across; the halo about 0.55
            haloMaterial = new Material(ParticleMaterial());
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = haloMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            halo = go.transform;
            halo.gameObject.SetActive(false);
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

        public void SetQuality(Quality q)
        {
            var spec = Spec();
            var c = ColorOf(q);
            var g = new Gradient();
            float midAlpha = 0.04f + 0.38f * 0.25f;   // Codex's 0.04 + 0.38·t² halfway down the tube
            g.SetKeys(new[] { new GradientColorKey(Color.Lerp(c, Color.white, 0.25f), 0), new GradientColorKey(c, 0.3f), new GradientColorKey(c, 1) },
                      new[] { new GradientAlphaKey(spec.headAlpha, 0), new GradientAlphaKey(midAlpha, 0.5f), new GradientAlphaKey(spec.tailAlpha, 1) });
            trail.colorGradient = g;
            var h = c; h.a = 0.3f;
            haloMaterial.color = h;
        }

        public void BeginFlight() { trail.Clear(); trail.emitting = true; halo.gameObject.SetActive(true); landedOnce = false; }
        public void EndFlight() { trail.emitting = false; halo.gameObject.SetActive(false); }

        /// The comet keeps its presence on screen whatever the lens is doing: the ribbons and
        /// the halo are sized against the height of the view where the ball is (distance and
        /// field of view together), never smaller than their close-up size.
        void LateUpdate()
        {
            if (!view || !halo.gameObject.activeSelf) return;
            float d = Vector3.Distance(view.transform.position, halo.position);
            float viewHeight = 2f * d * Mathf.Tan(view.fieldOfView * Mathf.Deg2Rad / 2f);   // yards top to bottom at the ball
            trail.widthMultiplier = Mathf.Max(0.16f, 0.045f * viewHeight);   // the tube's head, about a twentieth of the frame
            halo.rotation = view.transform.rotation;
            halo.localScale = Vector3.one * (Mathf.Max(0.54f, 0.04f * viewHeight) / 0.12f);   // the ball is 0.12 yd across
        }

        // ---- coming down

        /// A bounce or the landing: the ground it hit decides the puff.
        public void Touchdown(Vector3 at, CourseLie lie, float strength)
        {
            strength = Mathf.Clamp(strength, 0.3f, 1.5f);
            // the tube narrows away after the landing: nothing new is laid, the rest drains out
            if (!landedOnce) { landedOnce = true; trail.emitting = false; }
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
