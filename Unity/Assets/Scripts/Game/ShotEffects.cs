using GolfArcade.Course;
using UnityEngine;

namespace GolfArcade.Game
{
    /// How a shot looks in the air and when it comes down. The ball trails a comet in the colour
    /// of the strike — green for a pure one, amber for a fair one, red for one off line — with
    /// a soft halo riding on the ball; every bounce throws up a puff of the ground it hit, and a
    /// ball in the sea splashes. All of it is built in code from one soft-disc texture.
    public sealed class ShotEffects : MonoBehaviour
    {
        public enum Quality { Pure, Fair, OffLine }
        public static readonly Color PureColor = new(0.42f, 0.95f, 0.45f);
        public static readonly Color FairColor = new(1f, 0.82f, 0.25f);
        public static readonly Color OffLineColor = new(1f, 0.32f, 0.28f);
        public static Color ColorOf(Quality q) => q == Quality.Pure ? PureColor : q == Quality.Fair ? FairColor : OffLineColor;

        static Texture2D soft;
        static Material particleMaterial;

        TrailRenderer trail;
        Transform halo;
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

        void BuildTrail(Transform ball)
        {
            if (!ball.gameObject.TryGetComponent(out trail)) trail = ball.gameObject.AddComponent<TrailRenderer>();
            trail.time = 2.6f;
            trail.minVertexDistance = 0.25f;
            // Newest at 0: a slim head at the ball, fullest just behind it, fading to a hair.
            trail.widthCurve = new AnimationCurve(new Keyframe(0, 0.5f), new Keyframe(0.15f, 1f), new Keyframe(0.5f, 0.45f), new Keyframe(1, 0.04f));
            trail.widthMultiplier = 0.12f;
            trail.numCornerVertices = 4; trail.numCapVertices = 4;
            trail.alignment = LineAlignment.View;
            trail.textureMode = LineTextureMode.Stretch;
            trail.material = GreenRead.Material();
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
            var c = ColorOf(q);
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.Lerp(c, Color.white, 0.35f), 0), new GradientColorKey(c, 0.25f), new GradientColorKey(c, 1) },
                      new[] { new GradientAlphaKey(0.95f, 0), new GradientAlphaKey(0.55f, 0.4f), new GradientAlphaKey(0, 1) });
            trail.colorGradient = g;
            var h = c; h.a = 0.55f;
            haloMaterial.color = h;
        }

        public void BeginFlight() { trail.Clear(); trail.emitting = true; halo.gameObject.SetActive(true); }
        public void EndFlight() { trail.emitting = false; halo.gameObject.SetActive(false); }

        void LateUpdate()
        {
            if (halo && halo.gameObject.activeSelf && view)
            {
                halo.rotation = view.transform.rotation;
                // a little bigger far away so it never shrinks to nothing on screen
                float d = Vector3.Distance(view.transform.position, halo.position);
                halo.localScale = Vector3.one * (4.5f + d * 0.05f);
            }
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
