using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace GolfArcade.Course
{
    /// The world the Magma Open's holes sit in: the island floats on the molten lake of a crater, ringed by a
    /// wall of basalt strata with lava running down it, under a sky of ash and fire (HoleAtmosphere). It is
    /// Adnan's Volcano venue (Tennis/TennisVenueBuilder.cs on origin/newmapsandmenus: the lake, the cone, the
    /// streams, the boulders, the plume, the fountains, the embers) at the scale of a golf hole: the lake is
    /// as wide as the hole needs, and the wall, the strata, the noise and the particles scale with it.
    /// Textures are baked (Resources/Course/Lava, Editor/LavaTextureBaker.cs), the meshes are built here.
    /// The lava is at y = 0 like the sea the other holes sit in, so the hole models are unchanged.
    public static class LavaWorld
    {
        /// Where the sun is, as a direction toward it: over the crater wall behind the green's side.
        public static readonly Vector3 SunToward = new Vector3(0.55f, 0.52f, 0.66f).normalized;

        const float LakeY = -0.3f;          // a hand's breadth under the hole's own shore bands
        const float HeightScale = 0.62f;    // the wall's height against Adnan's (its crest was 0.87 of the lake's radius)

        // ----- textures and materials, made once
        static Material lake, rockMat, hotMat, plainMat, smokeMat, puffMat, glowMat, crust, shoreGlow;

        static Texture2D T(string name) => Resources.Load<Texture2D>("Course/Lava/" + name);

        static Material Make(string shader, Color color, Texture2D main = null)
        {
            var m = new Material(Shader.Find(shader)) { name = "Lava " + shader };
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            if (main) m.mainTexture = main;
            return m;
        }

        /// The lake's own material (the model's water bands round the island join it).
        public static Material LakeMaterial()
        {
            if (GolfCourseLook.Current && GolfCourseLook.Current.Hole.Theme == "magma")
                return GolfCourseLook.Current.Get(GolfCourseLook.Surface.Lava);
            if (lake) return lake;
            lake = Make("GolfArcade/LavaLake", Color.white);
            lake.SetTexture("_MainTex", T("lava_base")); lake.SetTexture("_GlowTex", T("lava_glow"));
            return lake;
        }

        /// The model's water, shallows and surf, made lava at the shore of an island: `name` is the Blender material.
        public static Material ShoreMaterial(string name)
        {
            if (name == "MAT_WATER") return LakeMaterial();
            if (name == "MAT_WATER_SHALLOW") return crust ? crust : crust = Unlit(new Color(0.78f, 0.24f, 0.07f));     // the cooling crust round the rock
            return shoreGlow ? shoreGlow : shoreGlow = Unlit(new Color(1f, 0.72f, 0.24f));                              // the bright rim
        }

        static Material Unlit(Color c)
        {
            var m = new Material(Shader.Find("Unlit/Color")) { name = "Lava glow" };
            m.color = c;
            return m;
        }

        static void Materials()
        {
            if (rockMat) return;
            var rock = T("crater_rock"); var relief = T("crater_relief");
            rockMat = Make("GolfArcade/CraterRock", new Color(0.92f, 0.86f, 0.82f), rock);
            rockMat.SetTexture("_BumpMap", relief); rockMat.SetFloat("_BumpScale", 1.6f);
            rockMat.SetTexture("_Glow", T("lava_streams")); rockMat.SetFloat("_GlowAmount", 1.55f);
            hotMat = new Material(rockMat) { name = "Lava hot rock" };
            hotMat.SetTexture("_Glow", T("lava_streams")); hotMat.SetFloat("_GlowAmount", 1.1f);
            plainMat = Make("GolfArcade/CraterRock", new Color(0.8f, 0.72f, 0.68f), T("ash_gravel"));
            plainMat.SetTexture("_BumpMap", relief); plainMat.SetFloat("_BumpScale", 0.6f); plainMat.SetFloat("_GlowAmount", 0f);
            var soft = T("soft"); var puff = T("puff");
            smokeMat = Make("GolfArcade/ParticleSoft", Color.white, puff);
            glowMat = Make("GolfArcade/ParticleAdd", Color.white, soft);
            puffMat = smokeMat;
        }

        // ----- noise (the same as the venue's)
        static float Sm(float e0, float e1, float x) { float t = Mathf.Clamp01((x - e0) / (e1 - e0)); return t * t * (3 - 2 * t); }
        static float Noise(float x, float y) => Mathf.PerlinNoise(x + 37.1f, y + 91.7f);
        static float Periodic(float th, float scale, float seed) => Noise(Mathf.Cos(th) * scale + seed, Mathf.Sin(th) * scale + seed * 1.7f);
        static float Rand(System.Random rng, float a, float b) => a + (float)rng.NextDouble() * (b - a);

        // ----- the crater's profile, in Adnan's own numbers (metres; the lake at -46), scaled by the caller
        const float AdnanLake = -46f, AdnanGround = -54f;
        static readonly float[][] Cone =
        {
            new[] { 0f, AdnanLake }, new[] { 120f, AdnanLake }, new[] { 133f, -30f }, new[] { 165f, -2f }, new[] { 215f, 24f }, new[] { 265f, 46f },
            new[] { 300f, 58f }, new[] { 350f, 42f }, new[] { 400f, 6f }, new[] { 450f, -36f }, new[] { 520f, AdnanGround },
        };

        static float ConeHeight(float r, float th)
        {
            float y = AdnanGround;
            for (int i = 0; i < Cone.Length - 1; i++)
                if (r <= Cone[i + 1][0])
                {
                    float t = Mathf.InverseLerp(Cone[i][0], Cone[i + 1][0], r); t = t * t * (3 - 2 * t);
                    y = Mathf.Lerp(Cone[i][1], Cone[i + 1][1], t); break;
                }
            if (r < 128) return y;
            if (r > 135 && r < 330)
            {
                // cliff strata: the wall steps up in ledges and risers instead of one smooth slope
                const float terr = 8f; float q = y / terr, f = q - Mathf.Floor(q);
                float stepped = (Mathf.Floor(q) + Sm(.5f, .9f, f)) * terr;
                y = Mathf.Lerp(y, stepped, .75f * Mathf.Clamp01((r - 135) / 20f) * Mathf.Clamp01((330 - r) / 30f));
            }
            float ridge = .55f + .9f * Periodic(th, 1.7f, 3f);
            if (y > 0) y *= ridge;
            float fade = Mathf.Clamp01((r - 128) / 30f);
            float wx = r * Mathf.Cos(th), wz = r * Mathf.Sin(th);
            y += (Noise(wx * .035f + 11, wz * .035f + 7) - .5f) * 2 * 7f * fade;
            y += (Noise(wx * .11f + 3, wz * .11f + 19) - .5f) * 2 * 3.2f * fade;
            y += (Periodic(th, 13f, 5f) - .5f) * 2 * 7f * fade;
            if (r > 235 && r < 340) y += (Periodic(th, 34f, 2f) - .5f) * 2 * 12f * Sm(235f, 290f, r);   // a toothed crest
            if (r > 300)
            {
                float g = Mathf.Abs(Mathf.Sin(th * 19f + Periodic(th, 3f, 9f) * 6f));
                y -= Mathf.Pow(1 - g, 3) * 16f * Mathf.Clamp01((r - 300) / 100f) * Mathf.Clamp01((520 - r) / 60f);
            }
            return y;
        }

        /// The crater's height above the lava at radius r (yards) and angle th, for a lake `k` times Adnan's.
        static float Crater(float r, float th, float k) => (ConeHeight(Mathf.Min(r / k, 520f), th) - AdnanLake) * k * HeightScale + LakeY;

        // ----- building

        /// The crater round a hole: `course` the bounds of the land, `along` down the hole, `seed` the hole's number.
        public static void Build(Transform parent, Bounds course, Vector3 along, int seed)
        {
            Materials();
            var root = new GameObject("Lava world").transform;
            root.SetParent(parent, false);
            var centre = course.center; centre.y = 0;
            float half = Mathf.Sqrt(course.extents.x * course.extents.x + course.extents.z * course.extents.z);
            float lakeR = Mathf.Max(240f, half + 110f);
            float k = lakeR / 123f;
            Lake(root, centre, lakeR);
            CraterWall(root, centre, lakeR, k);
            AshPlain(root, centre, lakeR, k);
            Boulders(root, centre, lakeR, k, seed);
            along.y = 0; along.Normalize();
            var right = Vector3.Cross(Vector3.up, along);
            Fire(root, centre, lakeR, k, along, right, course.extents, seed);
        }

        static void Lake(Transform root, Vector3 centre, float lakeR)
        {
            var radii = new List<float> { 0 };
            for (float r = 12; r < lakeR * 1.06f; r *= 1.16f) radii.Add(r);
            radii.Add(lakeR * 1.06f);
            const int segments = 96;
            var verts = new Vector3[1 + (radii.Count - 1) * segments];
            for (int m = 1; m < radii.Count; m++)
                for (int j = 0; j < segments; j++)
                {
                    float a = j * Mathf.PI * 2 / segments;
                    verts[1 + (m - 1) * segments + j] = new Vector3(Mathf.Cos(a) * radii[m], 0, Mathf.Sin(a) * radii[m]);
                }
            var tris = new List<int>();
            for (int j = 0; j < segments; j++) { tris.Add(0); tris.Add(1 + (j + 1) % segments); tris.Add(1 + j); }
            for (int m = 1; m < radii.Count - 1; m++)
                for (int j = 0; j < segments; j++)
                {
                    int a = 1 + (m - 1) * segments + j, b = 1 + (m - 1) * segments + (j + 1) % segments, c = a + segments, d = b + segments;
                    tris.Add(a); tris.Add(b); tris.Add(c); tris.Add(b); tris.Add(d); tris.Add(c);
                }
            var mesh = new Mesh { name = "Lava lake", vertices = verts, triangles = tris.ToArray() };
            var normals = new Vector3[verts.Length]; for (int i = 0; i < normals.Length; i++) normals[i] = Vector3.up;
            mesh.normals = normals; mesh.RecalculateBounds();
            var go = new GameObject("Lava lake"); go.transform.SetParent(root, false);
            go.transform.position = new Vector3(centre.x, LakeY + 0.3f, centre.z);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r0 = go.AddComponent<MeshRenderer>(); r0.sharedMaterial = LakeMaterial();
            r0.shadowCastingMode = ShadowCastingMode.Off; r0.receiveShadows = false;
        }

        /// A ring of terrain about the crater's centre between two radii (yards); `stepAt` the radial spacing at a radius.
        static Mesh Ring(string name, Vector3 centre, float r0, float r1, System.Func<float, float> stepAt, int around,
                         float uTiles, float vYards, System.Func<float, float, float> heightAt, float lift)
        {
            var radii = new List<float>();
            for (float r = r0; r < r1; r += stepAt(r)) radii.Add(r);
            radii.Add(r1);
            var verts = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
            // v runs along the slope (radius and height together), so a steep wall's texture is not stretched up it
            var meanH = new float[radii.Count];
            for (int m = 0; m < radii.Count; m++)
            {
                float sum = 0; const int samples = 24;
                for (int q = 0; q < samples; q++) sum += heightAt(radii[m], q * Mathf.PI * 2 / samples);
                meanH[m] = sum / samples;
            }
            float along = radii[0] / vYards;
            for (int m = 0; m < radii.Count; m++)
            {
                if (m > 0) along += Mathf.Sqrt((radii[m] - radii[m - 1]) * (radii[m] - radii[m - 1]) + (meanH[m] - meanH[m - 1]) * (meanH[m] - meanH[m - 1])) / vYards;
                float r = radii[m];
                for (int a = 0; a <= around; a++)
                {
                    float u = a / (float)around, th = u * Mathf.PI * 2;
                    verts.Add(new Vector3(centre.x + Mathf.Cos(th) * r, heightAt(r, th) + lift, centre.z + Mathf.Sin(th) * r));
                    uvs.Add(new Vector2(u * uTiles, along));
                }
            }
            for (int p = 0; p < radii.Count - 1; p++)
                for (int a = 0; a < around; a++)
                {
                    int i0 = p * (around + 1) + a, i1 = i0 + 1, i2 = i0 + around + 1, i3 = i2 + 1;
                    tris.AddRange(new[] { i0, i1, i2, i1, i3, i2 });   // clockwise seen from above (Unity is left-handed): faces up
                }
            var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(verts); mesh.SetUVs(0, uvs); mesh.SetTriangles(tris, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }

        static GameObject Spawn(Transform parent, string name, Mesh mesh, Material mat, bool shadows = false)
        {
            var standard = GolfCourseLook.Current;
            if (standard && standard.Hole.Theme == "magma")
            {
                if (mat == rockMat || mat == hotMat || mat == plainBoulderMat) mat = standard.Get(GolfCourseLook.Surface.Basalt);
                else if (mat == plainMat) mat = standard.Get(GolfCourseLook.Surface.Ash);
            }
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = mat;
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off; r.receiveShadows = shadows;
            return go;
        }

        static void CraterWall(Transform root, Vector3 centre, float lakeR, float k)
        {
            // the cone itself: basalt with lava streams, ringing the lake (radii in yards, Adnan's in metres times k)
            float foot = 118f * k, edge = 540f * k;
            var wall = Ring("Crater wall", centre, foot, edge,
                r => r < 130f * k ? 60f * k : (r < 340f * k ? 3.5f * k : 5.5f * k), 288, 20f * k * 0.9f, 90f * k, (r, th) => Crater(r, th, k), 0f);
            Spawn(root, "Crater wall", wall, rockMat);
        }

        static void AshPlain(Transform root, Vector3 centre, float lakeR, float k)
        {
            // the flat, grey-black plain the crater stands on, out into the haze
            float r0 = 500f * k, r1 = 2300f;
            float y = Crater(520f * k, 0.3f, k);
            var plain = Ring("Ash plain", centre, r0, Mathf.Max(r1, r0 + 400f), r => Mathf.Max(40f, r * 0.06f), 96, 60f, 40f * k, (r, th) => y, 0.05f);
            Spawn(root, "Ash plain", plain, plainMat);
        }

        // ----- rocks

        static Mesh RockMesh(System.Random rng)
        {
            const int around = 11, rings = 7;
            var v = new List<Vector3>(); var t = new List<int>(); var uv = new List<Vector2>();
            float seed = Rand(rng, 0, 100);
            v.Add(new Vector3(0, .5f * 1.05f, 0)); uv.Add(new Vector2(.5f, 1));
            for (int j = 1; j < rings; j++)
            {
                float ph = Mathf.PI * j / rings;
                for (int i = 0; i < around; i++)
                {
                    float th = 2 * Mathf.PI * i / around + (j % 2 == 0 ? .28f : 0f);
                    var d = new Vector3(Mathf.Sin(ph) * Mathf.Cos(th), Mathf.Cos(ph), Mathf.Sin(ph) * Mathf.Sin(th));
                    float n = Noise(d.x * 2.1f + seed, d.y * 2.1f + d.z * 1.7f + seed) * 1.1f + .55f;
                    v.Add(d * .5f * n * (1 - Mathf.Max(0, -d.y) * .35f));
                    uv.Add(new Vector2(i / (float)around, 1 - j / (float)rings));
                }
            }
            v.Add(new Vector3(0, -.5f * .8f, 0)); uv.Add(new Vector2(.5f, 0));
            for (int i = 0; i < around; i++) { t.Add(0); t.Add(1 + (i + 1) % around); t.Add(1 + i); }
            for (int j = 0; j < rings - 2; j++)
                for (int i = 0; i < around; i++)
                {
                    int a = 1 + j * around + i, b = 1 + j * around + (i + 1) % around, c = a + around, d = b + around;
                    t.Add(a); t.Add(b); t.Add(c); t.Add(b); t.Add(d); t.Add(c);
                }
            int last = v.Count - 1, first = 1 + (rings - 2) * around;
            for (int i = 0; i < around; i++) { t.Add(last); t.Add(first + i); t.Add(first + (i + 1) % around); }
            var mesh = new Mesh { name = "Crater rock" };
            mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetTriangles(t, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }

        /// Rock outcrops and boulders over the inner wall, the crest and the flank: relief you can see from the
        /// tee, so the wall is a rock face and not a wall. Batched into two meshes (plain and glowing).
        static void Boulders(Transform root, Vector3 centre, float lakeR, float k, int seed)
        {
            var rng = new System.Random(64 + seed);
            var shapes = new Mesh[8]; for (int i = 0; i < shapes.Length; i++) shapes[i] = RockMesh(rng);
            var plain = new List<CombineInstance>(); var hot = new List<CombineInstance>();
            int count = 150;
            for (int i = 0; i < count; i++)
            {
                float th = Rand(rng, 0, Mathf.PI * 2);
                float r = (i % 5 == 0 ? Rand(rng, 255, 335) : Rand(rng, 138, 300)) * k;   // every fifth one along the crest
                float size = (r > 250 * k ? Rand(rng, 5, 15) : Rand(rng, 2.5f, 11)) * k * 0.7f;
                var pos = new Vector3(centre.x + Mathf.Cos(th) * r, Crater(r, th, k) + size * .12f, centre.z + Mathf.Sin(th) * r);
                var rot = Quaternion.Euler(Rand(rng, -20, 20), Rand(rng, 0, 360), Rand(rng, -20, 20));
                var scale = new Vector3(size * Rand(rng, .8f, 1.5f), size * Rand(rng, .7f, 1.6f), size * Rand(rng, .8f, 1.5f));
                var ci = new CombineInstance { mesh = shapes[i % shapes.Length], transform = Matrix4x4.TRS(pos, rot, scale) };
                (i % 4 == 0 ? hot : plain).Add(ci);
            }
            foreach (var (list, mat, name) in new[] { (plain, plainBoulder(), "Boulders"), (hot, hotMat, "Hot boulders") })
            {
                var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
                mesh.CombineMeshes(list.ToArray(), true, true);
                mesh.RecalculateBounds();
                Spawn(root, name, mesh, mat);
            }
        }

        static Material plainBoulderMat;
        static Material plainBoulder()
        {
            if (plainBoulderMat) return plainBoulderMat;
            plainBoulderMat = new Material(rockMat) { name = "Lava plain rock" };
            plainBoulderMat.SetColor("_Color", new Color(0.85f, 0.80f, 0.76f)); plainBoulderMat.SetFloat("_GlowAmount", 0f);
            return plainBoulderMat;
        }

        // ----- fire, smoke and sparks

        static ParticleSystem NewSystem(Transform root, string name, Vector3 at, Vector3 euler)
        {
            var ps = new GameObject(name).AddComponent<ParticleSystem>();
            ps.transform.SetParent(root, false); ps.transform.position = at; ps.transform.rotation = Quaternion.Euler(euler);
            var main = ps.main; main.loop = true; main.prewarm = true; main.simulationSpace = ParticleSystemSimulationSpace.World;
            return ps;
        }

        static void Render(ParticleSystem ps, Material mat)
        {
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
        }

        /// A geyser of fire at `at` (a vent the hole model marks in its lava): a fountain of sparks and a breath of steam.
        public static void Geyser(Transform parent, Vector3 at)
        {
            Materials();
            var f = NewSystem(parent, "Geyser", at, new Vector3(-90, 0, 0));
            var fm = f.main; fm.startLifetime = 2.8f; fm.startSpeed = new ParticleSystem.MinMaxCurve(14, 26);
            fm.startSize = new ParticleSystem.MinMaxCurve(0.9f, 2.4f); fm.gravityModifier = 2.2f; fm.maxParticles = 90;
            var fe = f.emission; fe.rateOverTime = 26;
            var fs = f.shape; fs.shapeType = ParticleSystemShapeType.Cone; fs.angle = 11; fs.radius = 1.6f;
            var fc = f.colorOverLifetime; fc.enabled = true;
            var fg = new Gradient(); fg.SetKeys(new[] { new GradientColorKey(new Color(1f, .82f, .32f), 0), new GradientColorKey(new Color(1f, .3f, .08f), 1) }, new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) });
            fc.color = fg;
            Render(f, glowMat);
            var steam = NewSystem(parent, "Geyser steam", at + Vector3.up * 3f, new Vector3(-90, 0, 0));
            var m = steam.main; m.startLifetime = new ParticleSystem.MinMaxCurve(4, 7); m.startSpeed = new ParticleSystem.MinMaxCurve(3, 7);
            m.startSize = new ParticleSystem.MinMaxCurve(4, 9); m.maxParticles = 40; m.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
            var e = steam.emission; e.rateOverTime = 5;
            var sh = steam.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 10; sh.radius = 1.5f;
            var c = steam.colorOverLifetime; c.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(new Color(1f, .6f, .35f), 0), new GradientColorKey(new Color(.5f, .38f, .38f), 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(.4f, .2f), new GradientAlphaKey(0, 1) });
            c.color = g;
            var sz = steam.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(new Keyframe(0, .4f), new Keyframe(1, 1.8f)));
            Render(steam, puffMat);
        }

        static void Fire(Transform root, Vector3 centre, float lakeR, float k, Vector3 along, Vector3 right, Vector3 extents, int seed)
        {
            // steam curling off the inner wall
            for (int i = 0; i < 10; i++)
            {
                float th = i / 10f * Mathf.PI * 2 + .35f, r = (150 + (i % 4) * 16) * k;
                var at = new Vector3(centre.x + Mathf.Cos(th) * r, Crater(r, th, k), centre.z + Mathf.Sin(th) * r);
                var ps = NewSystem(root, "Steam vent", at, new Vector3(-90, 0, 0));
                var m = ps.main; m.startLifetime = new ParticleSystem.MinMaxCurve(6, 10); m.startSpeed = new ParticleSystem.MinMaxCurve(5 * k * .5f, 11 * k * .5f);
                m.startSize = new ParticleSystem.MinMaxCurve(8 * k * .5f, 18 * k * .5f); m.maxParticles = 50; m.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
                var e = ps.emission; e.rateOverTime = 6;
                var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 12; sh.radius = 3 * k * .5f;
                var c = ps.colorOverLifetime; c.enabled = true;
                var g = new Gradient();
                g.SetKeys(new[] { new GradientColorKey(new Color(1f, .6f, .35f), 0), new GradientColorKey(new Color(.5f, .38f, .38f), 1) },
                    new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(.45f, .2f), new GradientAlphaKey(0, 1) });
                c.color = g;
                var s2 = ps.sizeOverLifetime; s2.enabled = true; s2.size = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(new Keyframe(0, .4f), new Keyframe(1, 1.8f)));
                Render(ps, puffMat);
            }
            // the eruption behind the green: a column of ash and fire fountains, at the lake's far foot
            var vent = centre + along * (lakeR * 0.80f) + right * (lakeR * 0.34f); vent.y = LakeY + 2;
            var smoke = NewSystem(root, "Plume", vent, new Vector3(-90, 0, 0));
            var main = smoke.main; main.startLifetime = new ParticleSystem.MinMaxCurve(12, 20); main.startSpeed = new ParticleSystem.MinMaxCurve(22 * k * .55f, 36 * k * .55f);
            main.startSize = new ParticleSystem.MinMaxCurve(38 * k * .5f, 85 * k * .5f); main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
            main.maxParticles = 320; main.gravityModifier = -.02f;
            var em = smoke.emission; em.rateOverTime = 20;
            var shape = smoke.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 7; shape.radius = 12 * k * .5f;
            var col = smoke.colorOverLifetime; col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(new Color(1f, .62f, .24f), 0), new GradientColorKey(new Color(.86f, .40f, .26f), .2f), new GradientColorKey(new Color(.46f, .28f, .30f), .55f), new GradientColorKey(new Color(.26f, .21f, .27f), 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(.85f, .08f), new GradientAlphaKey(.75f, .7f), new GradientAlphaKey(0, 1) });
            col.color = grad;
            var szl = smoke.sizeOverLifetime; szl.enabled = true; szl.size = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(new Keyframe(0, .3f), new Keyframe(1, 1.7f)));
            var rot = smoke.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-.15f, .15f);
            Render(smoke, smokeMat);
            for (int i = 0; i < 3; i++)
            {
                var at = vent + new Vector3(Mathf.Sin(i * 1.9f) * 55 * k * .35f, 0, Mathf.Cos(i * 2.3f) * 55 * k * .35f);
                var f = NewSystem(root, "Lava fountain", at, new Vector3(-90, 0, 0));
                var fm = f.main; fm.startLifetime = 3.6f; fm.startSpeed = new ParticleSystem.MinMaxCurve(22 * k * .4f, 42 * k * .4f);
                fm.startSize = new ParticleSystem.MinMaxCurve(1.6f * k * .5f, 4.2f * k * .5f); fm.gravityModifier = 2.4f * (k * .4f); fm.maxParticles = 120;
                var fe = f.emission; fe.rateOverTime = 30;
                var fs = f.shape; fs.shapeType = ParticleSystemShapeType.Cone; fs.angle = 14; fs.radius = 3 * k * .5f;
                var fc = f.colorOverLifetime; fc.enabled = true;
                var fg = new Gradient(); fg.SetKeys(new[] { new GradientColorKey(new Color(1f, .8f, .3f), 0), new GradientColorKey(new Color(1f, .3f, .08f), 1) }, new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) });
                fc.color = fg;
                Render(f, glowMat);
            }
            // sparks and ash drifting up past the island
            var emb = NewSystem(root, "Embers", new Vector3(centre.x, LakeY + 2, centre.z), new Vector3(-90, 0, 0));
            var m2 = emb.main; m2.startLifetime = new ParticleSystem.MinMaxCurve(6, 11); m2.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 4.5f);
            m2.startSize = new ParticleSystem.MinMaxCurve(.18f, .5f); m2.maxParticles = 300;
            var e2 = emb.emission; e2.rateOverTime = 34;
            var s = emb.shape; s.shapeType = ParticleSystemShapeType.Box; s.scale = new Vector3(extents.x * 2 + 60, extents.z * 2 + 60, 6); s.rotation = new Vector3(0, 0, 0);
            var v = emb.velocityOverLifetime; v.enabled = true; v.x = new ParticleSystem.MinMaxCurve(-1.5f, 1.5f); v.y = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f); v.z = new ParticleSystem.MinMaxCurve(-1.5f, 1.5f);
            var c2 = emb.colorOverLifetime; c2.enabled = true;
            var g2 = new Gradient(); g2.SetKeys(new[] { new GradientColorKey(new Color(1f, .82f, .35f), 0), new GradientColorKey(new Color(1f, .3f, .08f), .6f), new GradientColorKey(new Color(.6f, .1f, .05f), 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .1f), new GradientAlphaKey(.7f, .7f), new GradientAlphaKey(0, 1) });
            c2.color = g2;
            Render(emb, glowMat);
        }
    }
}
