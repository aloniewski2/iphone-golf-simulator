using System.Collections.Generic;
using GolfArcade.Course;
using UnityEngine;

namespace GolfArcade.Game
{
    /// The things moving about a hole, drawn: a striped hot-air balloon with its basket, ropes and
    /// burner; a golf cart with its canopy, its driver and a bag on the back, wheels turning as it
    /// goes; gulls with beating wings. Toy-like and bright, in the game's soft plastic. Each is
    /// posed every frame from its path at the movers' clock (CourseMovers.Now), so what the player
    /// sees is where the ball will meet it. A hit wobbles the balloon, sends a gull tumbling and
    /// makes the cart honk (React). Lives under the hole, and goes with it.
    public sealed class MoverView : MonoBehaviour
    {
        CourseMovers movers;
        readonly List<Transform> bodies = new();
        readonly List<(Transform left, Transform right)> wings = new();
        readonly List<Transform[]> wheels = new();
        readonly List<Transform> flames = new();
        readonly Dictionary<int, float> hitAt = new();
        double lastClock;
        bool paused;

        public static MoverView Build(Transform parent, CourseMovers movers)
        {
            var go = new GameObject("Movers");
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<MoverView>();
            view.movers = movers;
            for (int i = 0; i < movers.All.Length; i++)
            {
                var m = movers.All[i];
                var root = new GameObject($"{m.Kind} {i}").transform;
                root.SetParent(go.transform, false);
                view.bodies.Add(root);
                view.wings.Add((null, null));
                view.wheels.Add(null);
                view.flames.Add(null);
                switch (m.Kind)
                {
                    case MoverKind.Balloon: view.BuildBalloon(root, i, m.Variant); break;
                    case MoverKind.Cart: view.BuildCart(root, i); break;
                    case MoverKind.Twister: view.BuildTwister(root, i); break;
                    default: view.BuildGull(root, i); break;
                }
                // (the minimap draws only the course; nothing on it is solid)
                foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = BallLook.OverlayLayer;
            }
            view.Pose();
            return view;
        }

        /// Freeze them (the game's pause).
        public void SetPaused(bool on) => paused = on;

        void Update()
        {
            if (movers == null) return;
            if (!paused) movers.Advance(Time.deltaTime);
            Pose();
        }

        /// The ball met mover `index`: the balloon wobbles, a gull tumbles away and comes back.
        public void React(int index) { if (index >= 0 && index < bodies.Count) hitAt[index] = Time.time; }

        void Pose()
        {
            double t = movers.Now;
            double step = t - lastClock; lastClock = t;
            for (int i = 0; i < movers.All.Length; i++)
            {
                var m = movers.All[i]; var root = bodies[i];
                if (!root) continue;
                var (x, y, d) = m.At(t);
                float heading = (float)m.HeadingAt(t);
                float since = hitAt.TryGetValue(i, out var at) ? Time.time - at : 99f;
                switch (m.Kind)
                {
                    case MoverKind.Balloon:
                    {
                        root.position = new Vector3((float)x, (float)y, (float)d);
                        // it turns slowly as it drifts; struck, the envelope wobbles like a jelly and settles
                        root.rotation = Quaternion.Euler(0, (float)(t * 5 % 360), 0);
                        float wob = since < 1.6f ? Mathf.Sin(since * 18f) * Mathf.Exp(-since * 2.6f) * 0.12f : 0f;
                        root.GetChild(0).localScale = new Vector3(1 + wob, 1 - wob, 1 + wob);
                        var flame = flames[i];
                        if (flame) { float f = 0.7f + 0.3f * Mathf.PerlinNoise(Time.time * 6f, i); flame.localScale = new Vector3(0.45f, 0.8f * f, 0.45f); }
                        break;
                    }
                    case MoverKind.Cart:
                    {
                        // level with the ground under it, front to back
                        var ahead = m.At(t + 0.12); var behind = m.At(t - 0.12);
                        float run = Mathf.Max(0.01f, new Vector2((float)(ahead.x - behind.x), (float)(ahead.d - behind.d)).magnitude);
                        float pitch = -Mathf.Atan2((float)(ahead.y - behind.y), run) * Mathf.Rad2Deg;
                        root.position = new Vector3((float)x, (float)y, (float)d);
                        float bump = since < 0.5f ? Mathf.Sin(since * 30f) * Mathf.Exp(-since * 8f) * 3f : 0f;
                        root.rotation = Quaternion.Euler(pitch, heading, bump);
                        var spin = wheels[i];
                        if (spin != null)
                        {
                            var before = m.At(t - step);
                            float travelled = new Vector2((float)(x - before.x), (float)(d - before.d)).magnitude;
                            float roll = travelled / 0.28f * Mathf.Rad2Deg * Mathf.Sign((float)step);   // over the wheel's radius
                            foreach (var w in spin) if (w) w.Rotate(Vector3.right, roll, Space.Self);
                        }
                        break;
                    }
                    case MoverKind.Twister:
                    {
                        // it leans and sways, its walls spinning at their own speeds, faster near the ground
                        root.SetPositionAndRotation(new Vector3((float)x, (float)y, (float)d),
                            Quaternion.Euler(4f * Mathf.Sin((float)t * 0.6f + i), 0, 5f * Mathf.Sin((float)t * 0.45f + 2 * i)));
                        if (spinners.TryGetValue(i, out var layers))
                            for (int k = 0; k < layers.Length; k++)
                                if (layers[k]) layers[k].localRotation = Quaternion.Euler(0, Time.time * (40f + 25f * k) % 360f, 0);
                        break;
                    }
                    default:
                    {
                        var p = new Vector3((float)x, (float)y, (float)d);
                        // banked into the turn it is circling
                        float bank = -Mathf.Clamp(Mathf.DeltaAngle(heading, (float)m.HeadingAt(t + 0.1)) * 5f, -28f, 28f);
                        var rot = Quaternion.Euler(0, heading, bank);
                        if (since < 1.2f)
                        {
                            // knocked tumbling, up out of the ball's way, and back to its circle
                            float k = Mathf.Sin(Mathf.Clamp01(since / 1.2f) * Mathf.PI);
                            p += Vector3.up * k * 2.2f;
                            rot *= Quaternion.Euler(since * 720f * (1 - since / 1.2f), 0, since * 540f * (1 - since / 1.2f));
                        }
                        root.SetPositionAndRotation(p, rot);
                        var (l, r) = wings[i];
                        // a few beats, then a glide
                        float cycle = Mathf.Repeat((float)t * 0.7f + i * 0.37f, 1f);
                        float beat = cycle < 0.55f || since < 1.2f ? Mathf.Sin((float)t * 11f + i) * 38f : 6f;
                        if (l) l.localRotation = Quaternion.Euler(0, 0, beat);
                        if (r) r.localRotation = Quaternion.Euler(0, 0, -beat);
                        break;
                    }
                }
            }
        }

        // ----- The models -----

        static readonly Dictionary<Color, Material> toys = new();
        /// The game's soft plastic: a lit colour with a little sheen.
        public static Material Toy(Color c)
        {
            if (toys.TryGetValue(c, out var m) && m) return m;
            m = new Material(HoleView.Mat(c)) { name = "Toy " + ColorUtility.ToHtmlStringRGB(c) };
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.38f);
            toys[c] = m;
            return m;
        }

        static GameObject Part(PrimitiveType type, string name, Color c, Transform parent, Vector3 pos, Vector3 scale, Vector3 euler = default)
        {
            var go = HoleView.Primitive(type, name, c, parent);
            go.GetComponent<Renderer>().sharedMaterial = Toy(c);
            go.transform.localPosition = pos; go.transform.localScale = scale; go.transform.localRotation = Quaternion.Euler(euler);
            return go;
        }

        static readonly Color[][] BalloonPalettes =
        {
            new[] { UiColor("F4513A"), UiColor("FFF6E8"), UiColor("F7C531") },
            new[] { UiColor("3E8EF0"), UiColor("FFF6E8"), UiColor("FF8A3D") },
            new[] { UiColor("9B5DE5"), UiColor("F15BB5"), UiColor("FEE440") },
            new[] { UiColor("00BBF9"), UiColor("00C49A"), UiColor("FEE440") },
        };
        static Color UiColor(string hex) => ColorUtility.TryParseHtmlString("#" + hex, out var c) ? c : Color.white;

        void BuildBalloon(Transform root, int index, int variant)
        {
            var palette = BalloonPalettes[Mathf.Abs(variant) % BalloonPalettes.Length];
            var envelope = new GameObject("Envelope").transform;
            envelope.SetParent(root, false);
            var mf = envelope.gameObject.AddComponent<MeshFilter>();
            var mr = envelope.gameObject.AddComponent<MeshRenderer>();
            mf.sharedMesh = EnvelopeMesh((float)Mover.Envelope, palette.Length);
            var mats = new Material[palette.Length];
            for (int i = 0; i < mats.Length; i++) mats[i] = Toy(palette[i]);
            mr.sharedMaterials = mats;
            float mouthY = -(float)Mover.Envelope * 1.28f;
            Part(PrimitiveType.Cylinder, "Skirt", UiColor("3A2F3A"), root, new Vector3(0, mouthY - 0.15f, 0), new Vector3(1.25f, 0.18f, 1.25f));
            // the basket: wicker, a darker rim, and the ropes up to the mouth
            float basketY = -(float)Mover.BasketDrop;
            Part(PrimitiveType.Cube, "Basket", UiColor("B07A45"), root, new Vector3(0, basketY, 0), new Vector3(1.55f, 1.15f, 1.55f));
            Part(PrimitiveType.Cube, "Rim", UiColor("7A4E2A"), root, new Vector3(0, basketY + 0.6f, 0), new Vector3(1.68f, 0.16f, 1.68f));
            var top = new Vector3(0, mouthY - 0.2f, 0);
            foreach (var corner in new[] { new Vector3(0.7f, 0, 0.7f), new Vector3(-0.7f, 0, 0.7f), new Vector3(0.7f, 0, -0.7f), new Vector3(-0.7f, 0, -0.7f) })
            {
                var from = new Vector3(corner.x, basketY + 0.65f, corner.z);
                var to = top + corner * 0.75f;
                var rope = Part(PrimitiveType.Cylinder, "Rope", UiColor("4A3A30"), root, (from + to) / 2, new Vector3(0.05f, (to - from).magnitude / 2, 0.05f));
                rope.transform.localRotation = Quaternion.FromToRotation(Vector3.up, to - from);
                rope.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            var flame = HoleView.Primitive(PrimitiveType.Sphere, "Burner flame", Color.white, root);
            flame.GetComponent<Renderer>().sharedMaterial = HoleView.UnlitMat(UiColor("FFB23F"));
            flame.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            flame.transform.localPosition = new Vector3(0, mouthY - 0.6f, 0);
            flames[index] = flame.transform;
        }

        /// The envelope: a sphere over a tapering cone down to the mouth, in `colours` alternating
        /// gores (one mesh, a submesh a colour, so the shading runs smoothly over the seams).
        static Mesh EnvelopeMesh(float radius, int colours)
        {
            const int gores = 12, perGore = 3, rings = 22;
            int segments = gores * perGore;
            var verts = new List<Vector3>(); var normals = new List<Vector3>();
            // the profile, top to mouth: (height, radius)
            var profile = new Vector2[rings + 1];
            for (int r = 0; r <= rings; r++)
            {
                float u = r / (float)rings;
                if (u <= 0.5f) { float phi = u / 0.5f * Mathf.PI / 2; profile[r] = new Vector2(radius * Mathf.Cos(phi), radius * Mathf.Sin(phi)); }
                else
                {
                    float v = (u - 0.5f) / 0.5f;
                    float h = -radius * 1.28f * v;
                    float w = radius * (0.24f + 0.76f * Mathf.Pow(Mathf.Max(0f, Mathf.Cos(v * Mathf.PI / 2)), 0.85f));   // (cos 90° comes out a hair below 0)
                    profile[r] = new Vector2(h, w);
                }
            }
            for (int r = 0; r <= rings; r++)
                for (int s = 0; s <= segments; s++)
                {
                    float a = s / (float)segments * Mathf.PI * 2;
                    verts.Add(new Vector3(Mathf.Cos(a) * profile[r].y, profile[r].x, Mathf.Sin(a) * profile[r].y));
                }
            var tris = new List<int>[colours];
            for (int c = 0; c < colours; c++) tris[c] = new List<int>();
            for (int r = 0; r < rings; r++)
                for (int s = 0; s < segments; s++)
                {
                    int i0 = r * (segments + 1) + s, i1 = i0 + 1, i2 = i0 + segments + 1, i3 = i2 + 1;
                    var list = tris[(s / perGore) % colours];
                    list.Add(i0); list.Add(i1); list.Add(i2);
                    list.Add(i1); list.Add(i3); list.Add(i2);
                }
            var mesh = new Mesh { name = "Balloon envelope" };
            mesh.SetVertices(verts);
            mesh.subMeshCount = colours;
            for (int c = 0; c < colours; c++) mesh.SetTriangles(tris[c], c);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        void BuildCart(Transform root, int index)
        {
            var white = UiColor("F7F5EF"); var canopy = UiColor("2FA36B"); var tyre = UiColor("26282C"); var seat = UiColor("C9A27A");
            var body = new GameObject("Body").transform; body.SetParent(root, false);
            Part(PrimitiveType.Cube, "Chassis", white, body, new Vector3(0, 0.5f, 0), new Vector3(1.25f, 0.42f, 2.3f));
            Part(PrimitiveType.Cube, "Cowl", white, body, new Vector3(0, 0.82f, 0.82f), new Vector3(1.18f, 0.36f, 0.62f), new Vector3(-14, 0, 0));
            Part(PrimitiveType.Cube, "Bumper", UiColor("3B3F46"), body, new Vector3(0, 0.38f, 1.18f), new Vector3(1.3f, 0.16f, 0.12f));
            Part(PrimitiveType.Cube, "Seat", seat, body, new Vector3(0, 0.82f, -0.12f), new Vector3(1.05f, 0.18f, 0.6f));
            Part(PrimitiveType.Cube, "Back rest", seat, body, new Vector3(0, 1.1f, -0.44f), new Vector3(1.05f, 0.5f, 0.12f));
            foreach (float sx in new[] { -0.55f, 0.55f })
                foreach (float sz in new[] { 0.62f, -0.5f })
                    Part(PrimitiveType.Cylinder, "Post", UiColor("D7D9DC"), body, new Vector3(sx, 1.45f, sz), new Vector3(0.06f, 0.55f, 0.06f));
            Part(PrimitiveType.Cube, "Canopy", canopy, body, new Vector3(0, 2.02f, 0.05f), new Vector3(1.38f, 0.1f, 1.62f));
            Part(PrimitiveType.Cube, "Canopy trim", UiColor("F7F5EF"), body, new Vector3(0, 1.95f, 0.05f), new Vector3(1.42f, 0.05f, 1.66f));
            Part(PrimitiveType.Cylinder, "Steering column", UiColor("3B3F46"), body, new Vector3(-0.28f, 1.08f, 0.42f), new Vector3(0.05f, 0.26f, 0.05f), new Vector3(-30, 0, 0));
            Part(PrimitiveType.Cylinder, "Wheel", UiColor("3B3F46"), body, new Vector3(-0.28f, 1.3f, 0.3f), new Vector3(0.32f, 0.03f, 0.32f), new Vector3(-55, 0, 0));
            // the driver, waving a little as the cart goes round
            Part(PrimitiveType.Capsule, "Driver", UiColor("F25F5C"), body, new Vector3(-0.28f, 1.22f, -0.12f), new Vector3(0.42f, 0.36f, 0.34f));
            Part(PrimitiveType.Sphere, "Head", UiColor("E8B48F"), body, new Vector3(-0.28f, 1.72f, -0.1f), new Vector3(0.3f, 0.32f, 0.3f));
            Part(PrimitiveType.Cylinder, "Cap", UiColor("1F4E8C"), body, new Vector3(-0.28f, 1.86f, -0.08f), new Vector3(0.31f, 0.05f, 0.31f));
            Part(PrimitiveType.Cube, "Peak", UiColor("1F4E8C"), body, new Vector3(-0.28f, 1.84f, 0.1f), new Vector3(0.24f, 0.03f, 0.16f));
            // a bag on the back, club heads out of the top
            Part(PrimitiveType.Cylinder, "Bag", UiColor("1F4E8C"), body, new Vector3(0, 1.0f, -1.02f), new Vector3(0.36f, 0.42f, 0.36f), new Vector3(12, 0, 0));
            for (int k = 0; k < 3; k++)
                Part(PrimitiveType.Cube, "Club head", UiColor("C7CCD3"), body, new Vector3(-0.1f + 0.1f * k, 1.5f, -1.12f + 0.04f * k), new Vector3(0.07f, 0.1f, 0.16f));
            var spin = new List<Transform>();
            foreach (float sx in new[] { -0.6f, 0.6f })
                foreach (float sz in new[] { 0.74f, -0.74f })
                {
                    var hub = new GameObject("Wheel hub").transform; hub.SetParent(body, false);
                    hub.localPosition = new Vector3(sx, 0.28f, sz);
                    Part(PrimitiveType.Cylinder, "Tyre", tyre, hub, Vector3.zero, new Vector3(0.56f, 0.1f, 0.56f), new Vector3(0, 0, 90));
                    Part(PrimitiveType.Cube, "Hub cap", UiColor("D7D9DC"), hub, new Vector3(sx > 0 ? 0.11f : -0.11f, 0, 0), new Vector3(0.02f, 0.26f, 0.08f));
                    spin.Add(hub);
                }
            wheels[index] = spin.ToArray();
        }

        readonly Dictionary<int, Transform[]> spinners = new();

        /// A twister: three walls of swirling cloud round its middle, wide at the top and pinched
        /// to a point at the ground, each streaked and spun at its own speed; a skirt of dust where
        /// it touches down, and debris whirling up it.
        void BuildTwister(Transform root, int index)
        {
            // the funnel's walls: GolfArcade/Twister winds and spins its streaks; the dirt round its foot is plain soft particles
            var mat = new Material(ShotEffects.ParticleMaterial()) { name = "Twister dust", mainTexture = Texture2D.whiteTexture };
            var funnelShader = Resources.Load<Shader>("Golf/Twister");
            var walls = funnelShader ? new Material(funnelShader) { name = "Twister" } : mat;
            var layers = new Transform[2];
            for (int k = 0; k < layers.Length; k++)
            {
                var go = new GameObject("Funnel " + k);
                go.transform.SetParent(root, false);
                go.AddComponent<MeshFilter>().sharedMesh = FunnelMesh(1f - 0.22f * k, k);
                var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = walls;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
                // the inner wall is drawn first, the outer over it
                mr.sortingOrder = -k;
                layers[k] = go.transform;
            }
            spinners[index] = layers;
            // where it touches down: a low skirt of dust and spray
            var skirt = new GameObject("Dust skirt");
            skirt.transform.SetParent(root, false);
            skirt.AddComponent<MeshFilter>().sharedMesh = SkirtMesh();
            var sr = skirt.AddComponent<MeshRenderer>(); sr.sharedMaterial = mat;
            sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // debris whirling up and round it
            var debris = new GameObject("Debris").AddComponent<ParticleSystem>();
            debris.transform.SetParent(root, false);
            debris.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = debris.main;
            main.loop = true; main.playOnAwake = true; main.maxParticles = 400; main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.2f, 3.4f); main.startSpeed = 0; main.gravityModifier = 0;
            main.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.55f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.32f, 0.27f, 0.2f, 0.95f), new Color(0.26f, 0.38f, 0.2f, 0.95f));
            var shape = debris.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Circle; shape.radius = 3.2f; shape.rotation = new Vector3(-90, 0, 0);
            var vel = debris.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.Local;
            // (every axis in the same mode, a range: Unity refuses to mix a constant with a range)
            vel.x = new ParticleSystem.MinMaxCurve(0f, 0f); vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            vel.y = new ParticleSystem.MinMaxCurve(5f, 9f);
            vel.orbitalX = new ParticleSystem.MinMaxCurve(0f, 0f); vel.orbitalZ = new ParticleSystem.MinMaxCurve(0f, 0f);
            vel.orbitalY = new ParticleSystem.MinMaxCurve(4f, 6f);
            vel.radial = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
            var emission = debris.emission; emission.rateOverTime = 110;
            var pr = debris.GetComponent<ParticleSystemRenderer>();
            pr.sharedMaterial = ShotEffects.ParticleMaterial();
            pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            debris.Play();
            // and the cloud of dirt it churns up round its foot, billowing round and out
            var dust = new GameObject("Dust cloud").AddComponent<ParticleSystem>();
            dust.transform.SetParent(root, false);
            dust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var dm = dust.main;
            dm.loop = true; dm.playOnAwake = true; dm.maxParticles = 120; dm.simulationSpace = ParticleSystemSimulationSpace.Local;
            dm.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 4f); dm.startSpeed = 0; dm.gravityModifier = 0;
            dm.startSize = new ParticleSystem.MinMaxCurve(3.5f, 7f);
            dm.startColor = new ParticleSystem.MinMaxGradient(new Color(0.45f, 0.39f, 0.31f, 0.38f), new Color(0.38f, 0.36f, 0.33f, 0.3f));
            var ds = dust.shape; ds.enabled = true; ds.shapeType = ParticleSystemShapeType.Circle; ds.radius = 3.5f; ds.rotation = new Vector3(-90, 0, 0);
            var dv = dust.velocityOverLifetime; dv.enabled = true; dv.space = ParticleSystemSimulationSpace.Local;
            dv.x = new ParticleSystem.MinMaxCurve(0f, 0f); dv.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            dv.orbitalX = new ParticleSystem.MinMaxCurve(0f, 0f); dv.orbitalZ = new ParticleSystem.MinMaxCurve(0f, 0f);
            dv.y = new ParticleSystem.MinMaxCurve(0.4f, 1.6f); dv.orbitalY = new ParticleSystem.MinMaxCurve(1.5f, 2.5f); dv.radial = new ParticleSystem.MinMaxCurve(1.2f, 2.4f);
            var dc = dust.colorOverLifetime; dc.enabled = true;
            var dg = new Gradient(); dg.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.25f), new GradientAlphaKey(0, 1) });
            dc.color = dg;
            var de = dust.emission; de.rateOverTime = 30;
            var dr = dust.GetComponent<ParticleSystemRenderer>();
            dr.sharedMaterial = ShotEffects.ParticleMaterial(); dr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            dust.Play();
        }

        /// One wall of the funnel: a flared lathe up to 32 yards, streaked by its vertex alpha, softest at the top and the ground.
        static Mesh FunnelMesh(float scale, int layer)
        {
            const int rings = 30, around = 32; const float height = 40f;
            var verts = new List<Vector3>(); var colours = new List<Color>(); var uvs = new List<Vector2>(); var tris = new List<int>();
            for (int j = 0; j <= rings; j++)
            {
                float v = j / (float)rings, h = v * height;
                float r = (1.0f + 10f * Mathf.Pow(v, 1.6f)) * scale;
                for (int i = 0; i <= around; i++)
                {
                    float a = i * Mathf.PI * 2 / around + v * (2.2f + layer);   // the wall twists as it climbs
                    verts.Add(new Vector3(Mathf.Cos(a) * r, h, Mathf.Sin(a) * r));
                    float fade = Mathf.Clamp01(v * 8f) * Mathf.Clamp01((1 - v) * 3f);
                    // dark storm cloud, browner low down where it is full of dirt
                    float g = Mathf.Lerp(0.2f, 0.4f, v) + 0.04f * layer;
                    // (the streaks are the shader's; the vertex alpha fades the funnel's top into the cloud and its foot into the dust)
                    colours.Add(new Color(g * Mathf.Lerp(1.12f, 1f, v), g * 1.02f, g * Mathf.Lerp(0.92f, 1.1f, v), fade * (layer == 0 ? 1f : 0.85f)));
                    // u round the funnel, v up it: the twister's shader winds its streaks by them
                    uvs.Add(new Vector2(i / (float)around, v));
                }
            }
            for (int j = 0; j < rings; j++)
                for (int i = 0; i < around; i++)
                {
                    int a0 = j * (around + 1) + i, a1 = a0 + 1, b0 = a0 + around + 1, b1 = b0 + 1;
                    tris.AddRange(new[] { a0, b0, a1, b0, b1, a1 });
                }
            var mesh = new Mesh { name = "Twister funnel " + layer };
            mesh.SetVertices(verts); mesh.SetColors(colours); mesh.SetUVs(0, uvs); mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }

        static Mesh SkirtMesh()
        {
            const int around = 32; var verts = new List<Vector3> { new(0, 0.3f, 0) }; var colours = new List<Color> { new(0.42f, 0.37f, 0.3f, 0.45f) };
            var uvs = new List<Vector2> { new(0.5f, 0.5f) }; var tris = new List<int>();
            for (int i = 0; i <= around; i++)
            {
                float a = i * Mathf.PI * 2 / around;
                verts.Add(new Vector3(Mathf.Cos(a) * 9f, 2.2f + 0.7f * Mathf.Sin(a * 5), Mathf.Sin(a) * 9f));
                colours.Add(new Color(0.5f, 0.45f, 0.38f, 0f)); uvs.Add(new Vector2(0.5f, 0.5f));
                if (i > 0) tris.AddRange(new[] { 0, i + 1, i });
            }
            var mesh = new Mesh { name = "Twister skirt" };
            mesh.SetVertices(verts); mesh.SetColors(colours); mesh.SetUVs(0, uvs); mesh.SetTriangles(tris, 0); mesh.RecalculateBounds();
            return mesh;
        }

        void BuildGull(Transform root, int index)
        {
            var white = UiColor("FAFAF7"); var grey = UiColor("B8C0C8"); var tip = UiColor("2E3236");
            // a little bigger than life, the way the ball is: a speck on the sky reads as a gull
            var body = new GameObject("Body").transform; body.SetParent(root, false); body.localScale = Vector3.one * 1.5f;
            Part(PrimitiveType.Capsule, "Torso", white, body, Vector3.zero, new Vector3(0.24f, 0.32f, 0.24f), new Vector3(90, 0, 0));
            Part(PrimitiveType.Sphere, "Head", white, body, new Vector3(0, 0.08f, 0.3f), new Vector3(0.2f, 0.19f, 0.22f));
            Part(PrimitiveType.Cube, "Beak", UiColor("F6B53C"), body, new Vector3(0, 0.06f, 0.45f), new Vector3(0.05f, 0.05f, 0.13f));
            Part(PrimitiveType.Cube, "Tail", grey, body, new Vector3(0, 0.02f, -0.36f), new Vector3(0.16f, 0.03f, 0.16f));
            Transform Wing(float side)
            {
                var shoulder = new GameObject(side < 0 ? "Left wing" : "Right wing").transform;
                shoulder.SetParent(body, false); shoulder.localPosition = new Vector3(0.1f * side, 0.06f, 0.04f);
                Part(PrimitiveType.Cube, "Wing", grey, shoulder, new Vector3(0.32f * side, 0, 0), new Vector3(0.62f, 0.03f, 0.26f));
                Part(PrimitiveType.Cube, "Tip", tip, shoulder, new Vector3(0.7f * side, 0, -0.03f), new Vector3(0.18f, 0.025f, 0.18f));
                return shoulder;
            }
            wings[index] = (Wing(-1), Wing(1));
            foreach (var r in root.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }
}
