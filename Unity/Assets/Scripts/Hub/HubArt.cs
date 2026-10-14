using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace GolfArcade.Hub
{
    /// The Plaza's look: a classic country club at golden hour (ArtDir/ui/hub-targets-2026-10-12/country-club, the AFTER sheets).
    /// Same island, same buildings in the same places with the same doors, same walking space; restyled as cream stone houses
    /// with navy slate roofs and gold-on-navy signs, a gravel plaza with a cobbled circle round a navy-and-gold star medallion,
    /// clipped hedges, roses, lamp posts, a fountain, and outside the hedge lawns on cliffs above a calm sea with a grass court,
    /// a putting green and a pond with a footbridge. Lighting: a low warm sun with long soft shadows, a blue-sky/warm-horizon
    /// trilight ambient, sky reflections, then gentle warm grading, bloom on what glows and a soft far-background blur.
    /// The kit (materials, textures, props) is HubClub; what moves (wind, gulls, flags) is HubLife.
    public static class HubArt
    {
        // ---- palette (names kept from the evening plaza: the rooms and the party markers use them)
        public static readonly Color Cream = C("F4E9D3"), CreamShade = C("E2D0AF"), Royal = C("24336B"), RoyalDeep = C("1B2757"),
            Lime = C("E2B54E"), Grass = C("5C9A37"), GrassDeep = C("4A842E"), Leaf = C("4C8A33"), LeafDark = C("2E6125"), Trunk = C("6E4A30"),
            Stone = C("E3D2B2"), RingLilac = C("D6C7AA"), Stage = C("1F2C5C"), StageRim = C("E6B94E"), Bronze = C("2A2C33"),
            Taupe = C("8C5A33"), Gold = C("E6B94E"), SeaNear = C("3E9BB5"), SeaFar = C("7FB2D3"), Haze = C("C9D6E2");
        public static readonly Color DoorGlow = new Color(2.4f, 1.6f, .75f), LampGlow = new Color(4.2f, 3.1f, 1.6f), SunGlow = new Color(5f, 4.3f, 3f);
        /// The sun as seen in the sky (low over the sea behind PLAY), and the direction the light comes from (behind the camera,
        /// so the facades facing the plaza are lit and shadows run long across the paving).
        public static readonly Vector3 SunSkyDir = Dir(21, 7.5f), SunLightDir = Dir(176, 25);
        static Color C(string hex) { ColorUtility.TryParseHtmlString("#" + hex, out var c); return c; }
        static Vector3 Dir(float azimuth, float elevation)
        {
            float a = azimuth * Mathf.Deg2Rad, e = elevation * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(a) * Mathf.Cos(e), Mathf.Sin(e), Mathf.Cos(a) * Mathf.Cos(e));
        }

        // ---- materials
        static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
        public static Material Lit(Color c, float smooth = .32f)
        {
            string key = "lit" + c + smooth;
            if (mats.TryGetValue(key, out var m) && m) return m;
            m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Hub " + ColorUtility.ToHtmlStringRGB(c), enableInstancing = true };
            m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", smooth); m.SetFloat("_Metallic", 0); m.SetFloat("_EnvironmentReflections", 1);
            return mats[key] = m;
        }
        public static Material Glow(Color hdr)
        {
            string key = "glow" + hdr;
            if (mats.TryGetValue(key, out var m) && m) return m;
            m = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "Hub glow", enableInstancing = true };
            m.SetColor("_BaseColor", hdr); return mats[key] = m;
        }
        static Material Painted(Texture2D tex) { var m = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "Hub painted " + tex.name }; m.SetTexture("_BaseMap", tex); m.SetColor("_BaseColor", Color.white); return m; }

        static GameObject Put(string name, Transform parent, Mesh mesh, Material mat, Vector3 pos, Quaternion? rot = null, Vector3? scale = null, bool shadows = true)
            => HubClub.Put(name, parent, mesh, mat, pos, rot, scale, shadows);
        static void Collider(Transform parent, Mesh mesh, Vector3 pos, Quaternion? rot = null)
        {
            var go = new GameObject("Collider"); go.transform.SetParent(parent, false); go.transform.SetPositionAndRotation(pos, rot ?? Quaternion.identity);
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
        }
        static Mesh Lathe(string key, Vector2[] profile, float fillet, int segments = 72)
        {
            var m = HubShapes.Lathe(key, HubShapes.Fillet(profile, fillet), segments); HubShapes.FixWinding(m); return m;
        }

        // ================================================================== sky, sea, light, post
        /// The golden-hour sky's colour in a direction (also the reflection cubemap and the rooms' sky): warm gold at the horizon
        /// under the sun, peach and soft lilac above, clear blue overhead, a few lit cloud banks.
        public static Color SkyColor(Vector3 d)
        {
            d.Normalize(); float e = d.y;
            Color c;
            if (e < 0) c = Color.Lerp(C("F5D2A6"), C("C9B8A8"), Mathf.Clamp01(-e * 5));
            else
            {
                var stops = new[] { (0f, C("FFCF8E")), (.035f, C("FBC08E")), (.09f, C("F1BC9E")), (.16f, C("D6C0C8")), (.25f, C("AEC0DF")), (.4f, C("8BABDC")), (.68f, C("6E96D5")), (1f, C("5A84CC")) };
                c = stops[0].Item2;
                for (int k = 1; k < stops.Length; k++) if (e >= stops[k - 1].Item1) c = Color.Lerp(stops[k - 1].Item2, stops[k].Item2, Mathf.InverseLerp(stops[k - 1].Item1, stops[k].Item1, e));
            }
            float sun = Mathf.Max(0, Vector3.Dot(d, SunSkyDir));
            c += C("FFD08A") * (Mathf.Pow(sun, 5) * .32f + Mathf.Pow(sun, 40) * .5f + Mathf.Pow(sun, 500) * .9f);
            c += C("FFDDAE") * Mathf.Exp(-Mathf.Abs(e) * 16) * .16f;
            float flat = Mathf.Max(0, Vector3.Dot(new Vector3(d.x, 0, d.z).normalized, new Vector3(SunSkyDir.x, 0, SunSkyDir.z).normalized));
            c += C("FFB86E") * Mathf.Pow(flat, 6) * Mathf.Exp(-Mathf.Abs(e) * 6) * .3f;
            // cloud banks: soft lumps low in the sky, lit warm on the sun side
            if (e > .02f && e < .42f)
            {
                float az = Mathf.Atan2(d.x, d.z);
                float band = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.03f, .09f, e)) * Mathf.SmoothStep(1, 0, Mathf.InverseLerp(.2f, .42f, e));
                float n = Mathf.PerlinNoise(az * 2.2f + 10, e * 9) * .65f + Mathf.PerlinNoise(az * 6.1f + 3, e * 22) * .35f;
                float cloud = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.5f, .72f, n)) * band;
                var lit = Color.Lerp(C("FFE7CF"), C("FFD3A8"), Mathf.Pow(flat, 3));
                var shade = Color.Lerp(C("E4B9B3"), C("C8B2C8"), Mathf.InverseLerp(.05f, .35f, e));
                c = Color.Lerp(c, Color.Lerp(shade, lit, Mathf.InverseLerp(.5f, .8f, n)), cloud * .85f);
            }
            c.a = 1; return c;
        }

        static Texture2D skyTex;
        /// The sky painted once (equirectangular, 1024 x 512), shared by the plaza's dome and the outdoor sport grounds' domes.
        public static Texture2D SkyTexture
        {
            get
            {
                if (skyTex) return skyTex;
                const int w = 1024, h = 512;
                skyTex = new Texture2D(w, h, TextureFormat.RGBA32, true) { name = "Hub sky", wrapModeU = TextureWrapMode.Repeat, wrapModeV = TextureWrapMode.Clamp };
                var px = new Color32[w * h];
                for (int y = 0; y < h; y++)
                {
                    float e = Mathf.Asin(Mathf.Clamp(y / (h - 1f) * 2 - 1, -1, 1));
                    for (int x = 0; x < w; x++)
                    {
                        float az = x / (float)w * Mathf.PI * 2;
                        px[y * w + x] = SkyColor(new Vector3(Mathf.Sin(az) * Mathf.Cos(e), Mathf.Sin(e), Mathf.Cos(az) * Mathf.Cos(e)));
                    }
                }
                skyTex.SetPixels32(px); skyTex.Apply(true, true); return skyTex;
            }
        }
        /// A sky dome of `radius` round `centre` (with the sun disc): the plaza's, and each outdoor sport ground's own.
        public static void SkyDome(Transform root, Vector3 centre, float radius)
        {
            var dome = Put("Sky", root, HubKit.SkyDome(64, 32), Painted(SkyTexture), centre, null, Vector3.one * radius, false);
            dome.GetComponent<MeshRenderer>().receiveShadows = false;
            var sun = Put("Sun", root, HubShapes.Disc(48), Glow(SunGlow), centre + SunSkyDir * radius * .84f, Quaternion.FromToRotation(Vector3.up, -SunSkyDir), Vector3.one * radius * .026f, false);
            sun.GetComponent<MeshRenderer>().receiveShadows = false;
        }

        public static void Lighting(Transform parent, Camera camera)
        {
            var sun = new GameObject("Golden-hour sun").AddComponent<Light>(); sun.transform.SetParent(parent, false);
            sun.type = LightType.Directional; sun.color = C("FFD39C"); sun.intensity = 1.42f; sun.shadows = LightShadows.Soft; sun.shadowStrength = .82f;
            sun.shadowBias = .04f; sun.shadowNormalBias = .3f;
            sun.transform.rotation = Quaternion.LookRotation(-SunLightDir, Vector3.up);
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = C("A9C1E6") * .74f; RenderSettings.ambientEquatorColor = C("EBCCA6") * .66f; RenderSettings.ambientGroundColor = C("66734A") * .58f;
            RenderSettings.fog = false; RenderSettings.skybox = null;
            // sky reflections on every smooth surface (the slate, the gold, the glass)
            const int size = 64;
            var cube = new Cubemap(size, TextureFormat.RGBA32, false) { name = "Hub sky reflection" };
            for (int f = 0; f < 6; f++)
            {
                var face = (CubemapFace)f; var px = new Color[size * size];
                for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
                {
                    float u = (x + .5f) / size * 2 - 1, v = (y + .5f) / size * 2 - 1;
                    Vector3 d = face switch
                    {
                        CubemapFace.PositiveX => new Vector3(1, -v, -u), CubemapFace.NegativeX => new Vector3(-1, -v, u),
                        CubemapFace.PositiveY => new Vector3(u, 1, v), CubemapFace.NegativeY => new Vector3(u, -1, -v),
                        CubemapFace.PositiveZ => new Vector3(u, -v, 1), _ => new Vector3(-u, -v, -1)
                    };
                    var c = SkyColor(d.normalized); if (d.y < 0) c = Color.Lerp(c, C("8C9A6A"), Mathf.Clamp01(-d.normalized.y * 3));
                    px[y * size + x] = c;
                }
                cube.SetPixels(px, face);
            }
            cube.Apply(false, true);
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom; RenderSettings.customReflectionTexture = cube; RenderSettings.reflectionIntensity = .8f;
            // camera + post
            camera.allowHDR = true; camera.fieldOfView = 42; camera.nearClipPlane = .3f;
            var data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true; data.renderShadows = true; data.antialiasing = AntialiasingMode.None;
            var vol = new GameObject("Hub post-processing"); vol.transform.SetParent(camera.transform, false);
            data.volumeLayerMask = 1 << vol.layer;
            var volume = vol.AddComponent<Volume>(); volume.isGlobal = true; volume.priority = 20;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>(); profile.name = "Hub golden hour";
            var tm = profile.Add<Tonemapping>(true); tm.mode.Override(TonemappingMode.Neutral);
            var ca = profile.Add<ColorAdjustments>(true); ca.postExposure.Override(-.06f); ca.contrast.Override(26); ca.saturation.Override(18); ca.colorFilter.Override(Color.white);
            var wb = profile.Add<WhiteBalance>(true); wb.temperature.Override(4); wb.tint.Override(2);
            var smh = profile.Add<ShadowsMidtonesHighlights>(true);
            smh.shadows.Override(new Vector4(.95f, .97f, 1.08f, -.03f)); smh.midtones.Override(new Vector4(1.01f, 1f, .98f, 0)); smh.highlights.Override(new Vector4(1.03f, 1.0f, .95f, -.02f));
            var bloom = profile.Add<Bloom>(true); bloom.threshold.Override(1.05f); bloom.intensity.Override(.7f); bloom.scatter.Override(.62f); bloom.tint.Override(C("FFE2B8")); bloom.maxIterations.Override(5);
            var vig = profile.Add<Vignette>(true); vig.intensity.Override(.2f); vig.smoothness.Override(.45f); vig.color.Override(C("2A2236"));
            var dof = profile.Add<DepthOfField>(true); dof.mode.Override(DepthOfFieldMode.Gaussian); dof.gaussianStart.Override(55); dof.gaussianEnd.Override(220); dof.gaussianMaxRadius.Override(1.1f); dof.highQualitySampling.Override(true);
            volume.sharedProfile = profile;
        }

        // ================================================================== the plaza
        public const float IslandRadius = 31.5f;
        public static void BuildPlaza(Transform root)
        {
            RegisterFootprints();
            SkyDome(root, Vector3.zero, 760); Sea(root); Islands(root);
            Ground(root);
            foreach (var pv in HubLayout.Pavilions)
            {
                var facing = HubLayout.Facing(pv.centre, pv.face);
                if (pv.id == "play") PlayHall(root, pv.centre, pv.radius, facing);
                else Cottage(root, pv.id, pv.label, pv.centre, pv.radius, pv.id == "locker" ? "hanger" : "crown", facing);
            }
            Dressing(root);
            Grounds(root);
            var life = HubLife.Of(root);
            life.Flock(new Vector3(0, 0, 10), 5, 40, 15, 1);
            life.Flock(new Vector3(-30, 0, 45), 3, 16, 12, 2);
        }

        static void Sea(Transform root)
        {
            // a polar disc whose UV v runs out from the island on a log scale (fine detail at the cliffs, a long gradient to the
            // horizon); u is the angle, so the sun-side warmth is painted along it
            const int seg = 128, rings = 28; const float r0 = 30f, r1 = 900f;
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            float V(float r) => Mathf.Log(1 + (r - r0) / 12f) / Mathf.Log(1 + (r1 - r0) / 12f);
            for (int i = 0; i <= rings; i++)
            {
                float r = r0 + (r1 - r0) * (Mathf.Exp(i / (float)rings * Mathf.Log(1 + (r1 - r0) / 12f)) - 1) * 12f / (r1 - r0);
                for (int s = 0; s <= seg; s++) { float a = s * Mathf.PI * 2 / seg; v.Add(new Vector3(Mathf.Sin(a) * r, 0, Mathf.Cos(a) * r)); uv.Add(new Vector2(s / (float)seg, V(r))); }
            }
            for (int i = 0; i < rings; i++) for (int s = 0; s < seg; s++) { int a = i * (seg + 1) + s, b = a + seg + 1; t.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 }); }
            var mesh = new Mesh { name = "Hub sea" }; mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetTriangles(t, 0);
            var n = new Vector3[v.Count]; for (int i = 0; i < n.Length; i++) n[i] = Vector3.up; mesh.normals = n; HubShapes.FixWinding(mesh); mesh.RecalculateBounds();
            var sunAz = Mathf.Atan2(SunSkyDir.x, SunSkyDir.z);
            var tex = HubClub.Paint("sea", 128, 256, (u, vv) =>
            {
                float r = r0 + (Mathf.Exp(vv * Mathf.Log(1 + (r1 - r0) / 12f)) - 1) * 12f;
                var c = Color.Lerp(C("3FA9B6"), C("27689D"), Mathf.SmoothStep(0, 1, Mathf.InverseLerp(33, 75, r)));
                c = Color.Lerp(c, C("3E79AE"), Mathf.SmoothStep(0, 1, Mathf.InverseLerp(120, 420, r)));
                c = Color.Lerp(c, C("8DB3CF"), Mathf.SmoothStep(0, 1, Mathf.InverseLerp(380, 900, r)));
                float toSun = Mathf.Cos(Mathf.DeltaAngle(u * 360, sunAz * Mathf.Rad2Deg) * Mathf.Deg2Rad);
                c = Color.Lerp(c, C("F3D2A2"), Mathf.Pow(Mathf.Max(0, toSun), 6) * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(150, 900, r)) * .75f);
                c = Color.Lerp(c, C("F4F8F6"), Mathf.SmoothStep(1, 0, Mathf.Abs(r - 34.2f) / 1.4f) * .8f);   // surf at the cliff foot
                c.a = 1; return c;
            }, true);
            var mat = HubClub.Water("sea", Color.white, C("B9CDE2"), tex, .2f, .35f, .55f, 6f, .5f);
            if (mat.HasProperty("_SunDir")) mat.SetVector("_SunDir", SunSkyDir);
            Put("Sea", root, mesh, mat, new Vector3(0, -6.3f, 0), null, null, false).GetComponent<MeshRenderer>().receiveShadows = false;
        }

        static void Islands(Transform root)
        {
            // far headlands in blue haze, the nearer ones greener
            var ranges = new (float az, float dist, float w, float h, Color c, int seed)[]
            {
                (-62, 330, 130, 34, C("B4C3D0"), 11), (-38, 380, 120, 46, C("BAC7D2"), 12), (-12, 420, 150, 30, C("C2CDD6"), 13),
                (14, 400, 110, 40, C("C6CDD2"), 14), (55, 360, 140, 28, C("BCC8D2"), 15), (82, 300, 120, 38, C("AFBFCC"), 16),
                (-80, 210, 90, 22, C("98AFB4"), 17), (70, 230, 80, 18, C("9DB2B6"), 18), (-28, 260, 70, 16, C("A2B6BA"), 19),
            };
            // flat silhouettes in the haze (unlit: far land has no facets, only the colour of the air in front of it)
            foreach (var (az, dist, w, h, c, seed) in ranges)
            {
                var at = Dir(az, 0) * dist; at.y = -h * .32f - 6.0f;
                var hill = Put("Island", root, HubShapes.Crown(seed, 4, .18f), Glow(c), at, Quaternion.Euler(0, seed * 37, 0), new Vector3(w, h, w * .55f), false);
                hill.GetComponent<MeshRenderer>().receiveShadows = false;
            }
        }

        static void Ground(Transform root)
        {
            float R0 = HubLayout.PlazaRadius + .45f, R1 = R0 + .7f; var st = HubLayout.Stage;
            var b = new HubClub.Batch();
            // the plaza: pale gravel (same floor and collider as before)
            var floor = Lathe("plaza floor", new[] { new Vector2(0, -.4f), new Vector2(R0, -.4f), new Vector2(R0, 0), new Vector2(0, 0) }, .12f, 96);
            Put("Plaza floor", root, floor, HubClub.Surface("gravel", HubClub.Gravel, HubClub.GravelTex, 1, new Vector2(1.5f, 1.5f), .3f, .35f, .3f, .04f), Vector3.zero);
            Collider(root, HubKit.Drum(R0, .4f, 64), new Vector3(0, -.4f, 0));
            // a cobbled circle round the medallion (where the lilac ring was), edged in pale stone
            Put("Cobbles", root, HubKit.Ring(3.3f, 7.6f, 96), HubClub.Surface("cobbles", C("DCCDB0"), HubClub.CobbleTex, 2, new Vector2(1.2f, .6f), .12f, .6f, .3f, .1f, centre: st), st + new Vector3(0, .008f, 0), null, null, false);
            Put("Cobble kerb", root, HubKit.Ring(7.6f, 7.95f, 96), Lit(HubClub.StoneLight, .3f), st + new Vector3(0, .012f, 0), null, null, false);
            // flagstone paths from the circle to each door and to the front
            var paths = new List<(Vector3 from, Vector3 to)>();
            foreach (var d in HubLayout.Doors) if (d.place == HubLayout.Plaza) { var dir = d.position - st; dir.y = 0; paths.Add((st + dir.normalized * 7.8f, d.position + d.inward * .4f)); }
            paths.Add((st + Vector3.back * 7.8f, new Vector3(0, 0, -R0 + .2f)));
            var flag = HubClub.Surface("flagstones", C("E7D9BD"), HubClub.FlagTex, 1, new Vector2(2.4f, 2.4f), .12f, .5f, .3f, .08f);
            foreach (var (a, c) in paths)
            {
                var dir = c - a; dir.y = 0; float len = dir.magnitude; var f = new HubClub.Frame((a + c) / 2, Vector3.Cross(dir / len, Vector3.up));
                b[flag].Quad(f.P(-len / 2, .006f, -1.4f), f.P(-len / 2, .006f, 1.4f), f.P(len / 2, .006f, 1.4f), f.P(len / 2, .006f, -1.4f), Vector3.up);
                foreach (int s in new[] { -1, 1 }) b[Lit(HubClub.StoneShade, .3f)].Box(f, new Vector3(0, .008f, s * 1.47f), new Vector3(len, .016f, .14f));
            }
            b.noShadow.Add(flag); b.noShadow.Add(Lit(HubClub.StoneShade, .3f));
            // the navy-and-gold star medallion (the emote stage: same size, same collider)
            var stage = Lathe("emote stage", new[] { new Vector2(3.45f, 0), new Vector2(3.35f, .2f), new Vector2(0, .2f) }, .1f, 96);
            Put("Emote stage", root, stage, Lit(HubClub.StoneLight, .35f), st);
            Put("Medallion", root, HubShapes.Disc(96), Lit(HubClub.Navy, .55f), st + new Vector3(0, .203f, 0), null, new Vector3(3.0f, 1, 3.0f), false);
            Put("Medallion rim", root, HubKit.Ring(2.95f, 3.13f, 96), Lit(HubClub.Gold, .72f), st + new Vector3(0, .207f, 0), null, null, false);
            Put("Medallion inner rim", root, HubKit.Ring(2.42f, 2.5f, 96), Lit(HubClub.Gold, .72f), st + new Vector3(0, .207f, 0), null, null, false);
            Put("Medallion star", root, StarMesh(5, 1.95f, .78f, .035f), Lit(HubClub.Gold, .75f), st + new Vector3(0, .205f, 0), null, null, false);
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4 + Mathf.PI / 8;
                Put("Medallion stud", root, StarMesh(4, .17f, .06f, .02f), Lit(HubClub.Gold, .75f), st + new Vector3(Mathf.Sin(a) * 2.72f, .206f, Mathf.Cos(a) * 2.72f), null, null, false);
            }
            Collider(root, HubKit.Drum(3.4f, .2f, 48), st);
            // party pads at the PLAY door (friends gather here; the party colours glow in navy pads with brass rims)
            var pads = new[] { C("5B8CFF"), C("7BDA4A"), C("FF63B8"), C("A970FF") };
            for (int i = 0; i < 4; i++)
            {
                var at = new Vector3(-3.45f + i * 2.3f, 0, 9.3f);
                Put("Party pad", root, Lathe("party pad", new[] { new Vector2(.86f, 0), new Vector2(.8f, .1f), new Vector2(0, .1f) }, .04f, 48), Lit(HubClub.Navy, .55f), at);
                Put("Party pad rim", root, HubKit.Ring(.78f, .88f, 48), Lit(HubClub.Brass, .7f), at + Vector3.up * .104f, null, null, false);
                Put("Party pad glow", root, HubKit.Ring(.46f, .66f, 48), Glow(pads[i] * 2.4f), at + Vector3.up * .105f, null, null, false);
                Put("Party pad light", root, HubShapes.Disc(32), Glow(pads[i] * .7f), at + Vector3.up * .102f, null, new Vector3(.47f, 1, .47f), false);
            }
            // the plaza's edge: a clipped hedge on the old wall (same collider) with roses tucked in, a pale kerb inside it
            var ring = b[HubClub.HedgeMat]; ring.colour = new Color(HubClub.HedgeGreen.r, HubClub.HedgeGreen.g, HubClub.HedgeGreen.b, 0);
            ring.Lathe(Vector3.zero, new[] { new Vector2(R1 + .05f, -.1f), new Vector2(R1 + .1f, .55f), new Vector2(R1 - .04f, .84f), new Vector2(R0 + .06f, .84f), new Vector2(R0 - .1f, .55f), new Vector2(R0 - .08f, -.02f) }, 120);
            b[Lit(HubClub.StoneLight, .3f)].Lathe(Vector3.zero, new[] { new Vector2(R0 - .08f, 0), new Vector2(R0 - .1f, .12f), new Vector2(R0 - .34f, .12f), new Vector2(R0 - .36f, .005f) }, 120);
            Collider(root, HubKit.RingWall(R0, R1, .55f, 64), Vector3.zero);
            for (int i = 0; i < 30; i++)
            {
                float a = (i + .5f) * Mathf.PI * 2 / 30; var d = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a));
                if (InsideBuilding(d * R0, .8f)) continue;
                HubClub.RoseBush(b, d * (R0 + .3f) + Vector3.up * .42f, .62f, i * 7 + 1);
            }
            b.Flush(root, "Plaza ground");
            Island(root);
        }

        /// The island round the plaza: a lawn on a cliff top, the rock face down to the sea, boulders and surf at its foot.
        static void Island(Transform root)
        {
            float R = IslandRadius; var b = new HubClub.Batch();
            Put("Lawn", root, Lathe("island lawn", new[] { new Vector2(R - .2f, -.1f), new Vector2(0, -.1f) }, 0, 128),
                HubClub.Surface("lawn", HubClub.Lawn, HubClub.GrassTex, 1, new Vector2(2.2f, 2.2f), .5f, .16f, .35f, .05f), Vector3.zero, null, null, false);
            // the lip of the lawn rolling over the cliff edge
            b[HubClub.Surface("lawn lip", HubClub.Lawn * .92f, null, 0, default, .5f, 1.2f, .4f, .03f)].Lathe(Vector3.zero, new[] { new Vector2(R + .35f, -.55f), new Vector2(R + .45f, -.25f), new Vector2(R + .1f, -.06f), new Vector2(R - .4f, -.08f) }, 128);
            var cliff = HubClub.Surface("cliff", HubClub.Cliff, HubClub.CliffTex, 0, new Vector2(1 / 66f, 3f), .35f, .5f, .3f, .05f);
            b[cliff].Lathe(Vector3.zero, new[] { new Vector2(R + 3.6f, -6.6f), new Vector2(R + 2.4f, -5.2f), new Vector2(R + 1.2f, -3.6f), new Vector2(R + .9f, -2.0f), new Vector2(R + .35f, -.5f) }, 128);
            for (int i = 0; i < 46; i++)
            {
                float a = i * .2417f * Mathf.PI * 2 + .3f, r = R + 1.2f + (i * 13 % 7) * .35f, y = -5.6f + (i * 7 % 5) * .9f;
                var col = Color.Lerp(HubClub.Cliff, HubClub.CliffDark, (i % 5) / 4f);
                var m = HubShapes.Crown(300 + i, 2, .22f);
                b[HubClub.Surface("boulder" + (i % 5), col, null, 0, default, .4f, 1.5f, .3f, .05f)].Mesh(m, Matrix4x4.TRS(new Vector3(Mathf.Sin(a) * r, y, Mathf.Cos(a) * r), Quaternion.Euler(i * 17, i * 41, 0), new Vector3(2.4f, 1.6f, 1.9f) * (.8f + (i % 3) * .35f)));
            }
            b.Flush(root, "Island");
        }

        /// A flat star (n points, outer radius r0, inner r1, `h` thick) lying on y = 0.
        public static Mesh StarMesh(int n, float r0, float r1, float h)
        {
            var mb = new HubClub.MB(); var top = new List<Vector3>();
            for (int i = 0; i < n * 2; i++) { float a = i * Mathf.PI / n; float r = i % 2 == 0 ? r0 : r1; top.Add(new Vector3(Mathf.Sin(a) * r, h, Mathf.Cos(a) * r)); }
            var c = new Vector3(0, h, 0);
            for (int i = 0; i < top.Count; i++)
            {
                var a = top[i]; var bb = top[(i + 1) % top.Count];
                // a gentle ridge from the centre to each point (the star catches the light like a pressed brass inlay)
                var cc = c + Vector3.up * h;
                mb.Tri(cc, a, bb, Vector3.Cross(a - cc, bb - cc).normalized * (Vector3.Cross(a - cc, bb - cc).y < 0 ? -1 : 1), Vector2.zero, Vector2.zero, Vector2.zero);
                var e = (bb - a); var nn = Vector3.Cross(Vector3.up, e).normalized; if (Vector3.Dot(nn, (a + bb) / 2 - c) < 0) nn = -nn;
                mb.Quad(a - Vector3.up * h, a, bb, bb - Vector3.up * h, nn);
            }
            return mb.Build("star " + n + " " + r0);
        }

        // ------------------------------------------------------------------ building footprints (for placing dressing)
        static readonly List<(Vector3 c, Vector3 o, float hx, float z0, float z1)> footprints = new List<(Vector3, Vector3, float, float, float)>();
        static bool InsideBuilding(Vector3 p, float margin)
        {
            foreach (var (c, o, hx, z0, z1) in footprints)
            {
                var r = Vector3.Cross(o, Vector3.up); var d = p - c; float x = Vector3.Dot(d, r), z = Vector3.Dot(d, o);
                if (Mathf.Abs(x) < hx + margin && z > z0 - margin && z < z1 + margin) return true;
            }
            return false;
        }
        static void Footprint(Vector3 c, Vector3 o, float hx, float z0, float z1)
        {
            foreach (var f in footprints) if (f.c == c && f.hx == hx && f.z0 == z0) return;
            footprints.Add((c, o, hx, z0, z1));
        }
        /// The three buildings' footprints (roof overhang included), known before anything is placed round them.
        static void RegisterFootprints()
        {
            foreach (var pv in HubLayout.Pavilions)
            {
                var o = HubLayout.Facing(pv.centre, pv.face);
                if (pv.id == "play") Footprint(pv.centre, o, 7.8f + .5f, -5.2f - .5f, pv.radius + .5f);
                else Footprint(pv.centre, o, 3.8f + .4f, -2.4f - .4f, pv.radius + .4f);
            }
        }
        static HubLayout.Door PlazaDoor(string id) => HubLayout.DoorById("door-plaza-" + id);

        /// A word laid around a cylinder (letters upright, centred on `facing`): the club's name on the crest's plinth.
        public static void CurvedText(Transform parent, string text, Vector3 centre, float radius, Vector3 facing, float y, float capHeight, Color color, string icon = null)
        {
            var font = HubKit.Font; int size = 100;
            font.RequestCharactersInTexture(text, size, FontStyle.Normal);
            float scale = capHeight / (size * .7f);   // Rubik caps are about 0.7 em
            var advances = new List<float>(); float total = 0;
            foreach (var ch in text) { float adv = font.GetCharacterInfo(ch, out var info, size) ? info.advance * scale : capHeight * .6f; advances.Add(adv); total += adv; }
            float iconW = icon != null ? capHeight * 1.5f : 0, gap = icon != null ? capHeight * .45f : 0;
            float span = total + iconW + gap;
            float baseAngle = Mathf.Atan2(facing.x, facing.z);
            // reading left to right from outside means the angle shrinks (Unity's left-handed frame, seen from above)
            float sign = -1, cursor = -span / 2;
            var letters = new List<(string, float, System.Action<RectTransform>)>();
            if (icon != null) letters.Add(("Icon", iconW, rt =>
            {
                var img = new GameObject("Icon").AddComponent<RawImage>(); img.transform.SetParent(rt, false); img.texture = HubIcons.Get(icon); img.color = color; img.raycastTarget = false;
                var irt = img.rectTransform; irt.anchorMin = irt.anchorMax = new Vector2(.5f, .5f); irt.sizeDelta = new Vector2(iconW * 100, iconW * 100);
            }));
            if (icon != null) letters.Add(("Gap", gap, _ => { }));
            for (int i = 0; i < text.Length; i++)
            {
                string ch = text[i].ToString();
                letters.Add(("Letter " + ch, advances[i], rt =>
                {
                    var t = new GameObject("Text").AddComponent<Text>(); t.transform.SetParent(rt, false);
                    t.font = font; t.text = ch; t.fontSize = size; t.color = color; t.alignment = TextAnchor.MiddleCenter; t.raycastTarget = false;
                    t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
                    var trt = t.rectTransform; trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = trt.offsetMax = Vector2.zero;
                    trt.localScale = Vector3.one * (scale * 100);
                }));
            }
            foreach (var (name, width, fill) in letters)
            {
                float mid = cursor + width / 2; float ang = baseAngle + sign * mid / radius;
                var dir = new Vector3(Mathf.Sin(ang), 0, Mathf.Cos(ang));
                var go = new GameObject(name); go.transform.SetParent(parent, false);
                go.transform.SetPositionAndRotation(centre + dir * radius + Vector3.up * y, Quaternion.LookRotation(-dir, Vector3.up));
                var canvas = go.AddComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
                var rt = (RectTransform)go.transform; rt.sizeDelta = new Vector2(width * 100, capHeight * 1.8f * 100); rt.localScale = Vector3.one / 100;
                fill(rt); cursor += width;
            }
        }

        /// An icon on a surface (flat on the floor, or on a wall when `upright`).
        public static void FlatIcon(Transform parent, string icon, Vector3 pos, float size, Color color, float yaw, bool upright = false)
        {
            var go = new GameObject("Icon " + icon); go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, upright ? Quaternion.Euler(0, yaw, 0) : Quaternion.Euler(90, yaw, 0));
            var canvas = go.AddComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
            var rt = (RectTransform)go.transform; rt.sizeDelta = new Vector2(size * 100, size * 100); rt.localScale = Vector3.one / 100;
            var img = new GameObject("Image").AddComponent<RawImage>(); img.transform.SetParent(go.transform, false); img.texture = HubIcons.Get(icon); img.color = color; img.raycastTarget = false;
            var irt = img.rectTransform; irt.anchorMin = Vector2.zero; irt.anchorMax = Vector2.one; irt.offsetMin = irt.offsetMax = Vector2.zero;
        }

        // ------------------------------------------------------------------ name tag
        public static Transform NameTag(Transform parent, string text, Color color)
        {
            var go = new GameObject("Name tag " + text); go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
            var rt = (RectTransform)go.transform; rt.sizeDelta = new Vector2(100, 40); rt.localScale = Vector3.one * .0062f;
            var plate = new GameObject("Plate").AddComponent<Image>(); plate.transform.SetParent(go.transform, false); plate.color = color; plate.raycastTarget = false;
            var prt = plate.rectTransform; prt.anchorMin = Vector2.zero; prt.anchorMax = Vector2.one; prt.offsetMin = prt.offsetMax = Vector2.zero;
            var label = new GameObject("Text").AddComponent<Text>(); label.transform.SetParent(go.transform, false);
            label.font = HubKit.Font; label.text = text; label.fontSize = 26; label.color = Color.white; label.alignment = TextAnchor.MiddleCenter; label.raycastTarget = false;
            var lrt = label.rectTransform; lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one; lrt.offsetMin = lrt.offsetMax = Vector2.zero;
            return go.transform;
        }

        // ------------------------------------------------------------------ LOCKER and CLUBHOUSE: cream stone houses
        /// One of the side buildings: its facade stands where the drum's door was (the doorway, the walk-through and the arrival
        /// point are unchanged); the house is 7.6 m wide, 7 m deep, with a navy hip roof, two dormers, a chimney. LOCKER is a shop
        /// front (striped awning, glass doors onto racks of clothes, a polo in the window); CLUBHOUSE has wooden doors under a
        /// fanlight, arched windows onto the trophy lounge, a front gable with its sign, and a terrace.
        static void Cottage(Transform root, string id, string label, Vector3 centre, float R, string icon, Vector3 toCentre)
        {
            var t = new GameObject(label + " house").transform; t.SetParent(root, false);
            var b = new HubClub.Batch();
            var f = new HubClub.Frame(centre, toCentre);
            const float hx = 3.8f, back = -2.4f, eave = 4.0f;
            var stone = b[HubClub.LitTex(HubClub.StoneTex, HubClub.Stone, .22f, 1.2f)];
            var light = b[Lit(HubClub.StoneLight, .3f)];
            stone.Box(f, new Vector3(0, eave / 2, (back + R) / 2), new Vector3(hx * 2, eave, R - back));
            b[HubClub.LitTex(HubClub.StoneTex, HubClub.StoneShade, .2f, 1.2f)].Box(f, new Vector3(0, .22f, (back + R) / 2), new Vector3(hx * 2 + .14f, .44f, R - back + .14f));
            // quoins up the front corners, a cornice under the eaves
            foreach (int s in new[] { -1, 1 })
                for (int k = 0; k < 8; k++)
                {
                    bool longOnFront = k % 2 == 0;
                    light.Box(f, new Vector3(s * (hx - (longOnFront ? .24f : .15f) + .025f), .66f + k * .44f, R - (longOnFront ? .15f : .24f) + .025f), new Vector3(longOnFront ? .52f : .34f, .4f, longOnFront ? .34f : .52f));
                }
            light.Box(f, new Vector3(0, eave - .08f, (back + R) / 2), new Vector3(hx * 2 + .3f, .2f, R - back + .3f));
            HubClub.HipRoof(b, f, -hx - .42f, hx + .42f, back - .42f, R + .42f, eave + .02f, 2.75f);
            // dormers on the front slope (the clubhouse's middle is its sign gable: its dormers sit on the side slopes)
            if (id == "locker") foreach (int s in new[] { -1, 1 }) HubClub.Dormer(b, f, s * 1.75f, R - .7f, eave + .62f, 1.12f, .98f);
            else foreach (int s in new[] { -1, 1 }) HubClub.Dormer(b, new HubClub.Frame(f.P(s * hx, 0, (back + R) / 2), f.R * s), 0, -.7f, eave + .62f, 1.05f, .95f);
            stone.Box(f, new Vector3(-2.3f, eave + 2.5f, -.6f), new Vector3(.8f, 3.2f, .8f));
            light.Box(f, new Vector3(-2.3f, eave + 4.15f, -.6f), new Vector3(.98f, .14f, .98f));
            b[Lit(HubClub.Terracotta, .3f)].Lathe(f.P(-2.15f, eave + 4.2f, -.6f), new[] { new Vector2(.11f, 0), new Vector2(.1f, .32f), new Vector2(0, .32f) }, 8);
            // arched windows on both side walls
            foreach (int s in new[] { -1, 1 })
                foreach (float z in new[] { .2f, 2.7f })
                    HubClub.Window(b, new HubClub.Frame(f.P(s * hx, 0, z), f.R * s), 1.15f, .95f, 1.75f, true, Glow(HubClub.WarmGlow * .85f), .13f);
            // box colliders: the house (its front where the drum's door was), and the dressing hugging the facade
            HubClub.BoxCollider(t, f.P(0, 2.5f, (back + R) / 2), new Vector3(hx * 2, 5f, R - back), f.Facing);
            if (id == "locker") ShopFront(t, b, f, R, label, icon); else ClubFront(t, b, f, R, label, icon);
            // the warm light spilling out of the doorway onto the gravel
            var spill = new GameObject("Door light").AddComponent<Light>(); spill.transform.SetParent(t, false);
            spill.type = LightType.Point; spill.color = C("FFC98A"); spill.intensity = 1.2f; spill.range = 4.5f; spill.shadows = LightShadows.None;
            spill.transform.position = f.P(0, .9f, R + 1.6f);
            b.Flush(t, label);
        }

        static void ShopFront(Transform t, HubClub.Batch b, HubClub.Frame f, float R, string label, string icon)
        {
            var light = b[Lit(HubClub.StoneLight, .3f)]; var navy = b[Lit(HubClub.Navy, .45f)];
            var fac = new HubClub.Frame(f.P(0, 0, R), f.O);
            // pilasters, the entablature that carries the sign, a little cornice
            foreach (int s in new[] { -1, 1 }) { light.Box(f, new Vector3(s * 2.78f, 1.75f, R + .1f), new Vector3(.5f, 3.5f, .22f)); light.Box(f, new Vector3(s * 2.78f, .12f, R + .14f), new Vector3(.62f, .24f, .3f)); }
            light.Box(f, new Vector3(0, 3.28f, R + .12f), new Vector3(6.1f, .66f, .26f));
            light.Box(f, new Vector3(0, 3.66f, R + .2f), new Vector3(6.3f, .12f, .4f));
            HubClub.SignBoard(t, label, icon, f.P(0, 3.27f, R + .26f), f.O, .34f, 4.0f, 1.55f);
            // the glazed front: the lit shop behind, navy frames, two glass doors in the middle, a window either side
            var inside = HubClub.PaintedFit(HubClub.LockerInterior, 1.25f, 4.9f, 2.55f);
            b[inside].Quad(f.P(-2.45f, .2f, R + .012f), f.P(-2.45f, 2.75f, R + .012f), f.P(2.45f, 2.75f, R + .012f), f.P(2.45f, .2f, R + .012f), f.O);
            foreach (float x in new[] { -2.45f, -1.08f, -.03f, .03f, 1.08f, 2.45f }) navy.Box(f, new Vector3(x, 1.47f, R + .02f), new Vector3(Mathf.Abs(x) < .1f ? .05f : .1f, 2.6f, .08f));
            navy.Box(f, new Vector3(0, 2.7f, R + .02f), new Vector3(5f, .12f, .08f));
            navy.Box(f, new Vector3(0, 2.28f, R + .02f), new Vector3(5f, .07f, .08f));
            foreach (int s in new[] { -1, 1 }) { navy.Box(f, new Vector3(s * 1.77f, .4f, R + .02f), new Vector3(1.38f, .44f, .1f)); navy.Box(f, new Vector3(s * .55f, .24f, R + .02f), new Vector3(1.05f, .12f, .09f)); }
            foreach (int s in new[] { -1, 1 }) b[Lit(HubClub.Brass, .8f)].Box(f, new Vector3(s * .16f, 1.25f, R + .1f), new Vector3(.035f, .5f, .04f));
            // the polo on a mannequin in the right-hand window
            var m = f.P(1.75f, .62f, R + .1f);
            b[Lit(HubClub.Iron, .5f)].Lathe(m, new[] { new Vector2(.14f, 0), new Vector2(.02f, .05f), new Vector2(.02f, .55f), new Vector2(0, .56f) }, 8);
            // (the torso is flattened front to back so it stands inside the window)
            var flat = new Vector3(1, 1, .6f);
            b[Lit(HubClub.White, .3f)].Mesh(HubShapes.Lathe("mannequin polo", new[] { new Vector2(.2f, 0), new Vector2(.23f, .3f), new Vector2(.27f, .55f), new Vector2(.16f, .68f), new Vector2(0, .7f) }, 14), Matrix4x4.TRS(m + Vector3.up * .5f, f.Facing, flat));
            b[Lit(HubClub.Navy, .4f)].Mesh(HubShapes.Lathe("mannequin collar", new[] { new Vector2(.1f, 0), new Vector2(.12f, .06f), new Vector2(.05f, .08f) }, 12), Matrix4x4.TRS(m + Vector3.up * 1.15f, f.Facing, flat));
            b[Lit(HubClub.StoneLight, .4f)].Lathe(m + Vector3.up * 1.2f, new[] { new Vector2(.05f, 0), new Vector2(.05f, .12f), new Vector2(0, .14f) }, 8);
            // the striped awning and its scalloped valance
            var stripes = b[HubClub.LitTex(HubClub.AwningTex, Color.white, .2f, 1.2f)];
            Vector3 A(float x, float z) => f.P(x, 2.88f - (z / 1.05f) * .52f, R + .06f + z);
            stripes.Quad(A(-2.55f, 0), A(-2.55f, 1.05f), A(2.55f, 1.05f), A(2.55f, 0), Vector3.Cross(A(2.55f, 0) - A(-2.55f, 0), A(-2.55f, 1.05f) - A(-2.55f, 0)).normalized);
            for (int i = 0; i < 17; i++)
            {
                float x0 = -2.55f + i * .3f, x1 = x0 + .3f; var mat = b[Lit(i % 2 == 0 ? HubClub.Navy : HubClub.C("F3EAD6"), .2f)];
                var pts = new List<Vector2> { new Vector2(x0, 0), new Vector2(x1, 0) };
                for (int k = 0; k <= 6; k++) { float a = k * Mathf.PI / 6; pts.Add(new Vector2((x0 + x1) / 2 + Mathf.Cos(a) * .15f, -Mathf.Sin(a) * .13f - .1f)); }
                mat.Extrude(new HubClub.Frame(f.P(0, 2.36f, R + 1.11f), f.O), pts, -.01f, .01f, true);
            }
            foreach (int s in new[] { -1, 1 }) b[Lit(HubClub.Iron, .5f)].Box(f, new Vector3(s * 2.55f, 2.62f, R + .58f), new Vector3(.03f, .03f, 1.05f));
            // lanterns either side, a stone threshold, roses in planter boxes against the facade, a topiary by the door
            foreach (int s in new[] { -1, 1 }) HubClub.WallLantern(b, fac, s * 3.32f, 2.05f);
            light.Box(f, new Vector3(0, .03f, R + .3f), new Vector3(2.6f, .06f, .6f));
            foreach (int s in new[] { -1, 1 })
            {
                b[HubClub.LitTex(HubClub.PlankTex, HubClub.C("8C5E3A"), .3f, 1f)].Box(f, new Vector3(s * 3.3f, .3f, R + .32f), new Vector3(.9f, .6f, .55f));
                HubClub.RoseBush(b, f.P(s * 3.3f, .45f, R + .32f), .75f, s > 0 ? 3 : 7);
                HubClub.BoxCollider(t, f.P(s * 3.3f, .4f, R + .32f), new Vector3(.9f, .8f, .55f), f.Facing);
            }
        }

        static void ClubFront(Transform t, HubClub.Batch b, HubClub.Frame f, float R, string label, string icon)
        {
            var light = b[Lit(HubClub.StoneLight, .3f)];
            // pilasters
            foreach (float x in new[] { -3.55f, -1.45f, 1.45f, 3.55f }) { light.Box(f, new Vector3(x, 1.85f, R + .09f), new Vector3(.42f, 3.7f, .2f)); light.Box(f, new Vector3(x, .14f, R + .12f), new Vector3(.54f, .28f, .26f)); light.Box(f, new Vector3(x, 3.62f, R + .12f), new Vector3(.54f, .16f, .26f)); }
            // the doorway: a stone arch, a glowing fanlight, wooden double doors with brass handles
            light.Extrude(new HubClub.Frame(f.P(0, 0, R), f.O), HubClub.ArchOutline(1.22f, 0, 2.55f, 1.08f), 0f, .07f);
            b[HubClub.PaintedFit(HubClub.LoungeInterior, 1.35f, 1.92f, 1.0f, -.96f, 2.5f)].Extrude(new HubClub.Frame(f.P(0, 0, R), f.O), HubClub.ArchOutline(.96f, 2.5f, 2.6f, .9f), 0f, .09f, false, false);
            var wood = b[HubClub.LitTex(HubClub.PlankTex, HubClub.WoodWarm, .45f, .8f)];
            foreach (int s in new[] { -1, 1 })
            {
                wood.Box(f, new Vector3(s * .48f, 1.25f, R + .12f), new Vector3(.93f, 2.5f, .1f));
                b[Lit(HubClub.WoodDark, .45f)].Box(f, new Vector3(s * .48f, 1.75f, R + .18f), new Vector3(.62f, .9f, .03f));
                b[Lit(HubClub.WoodDark, .45f)].Box(f, new Vector3(s * .48f, .65f, R + .18f), new Vector3(.62f, .7f, .03f));
                b[Lit(HubClub.Brass, .8f)].Box(f, new Vector3(s * .1f, 1.2f, R + .21f), new Vector3(.04f, .34f, .05f));
            }
            light.Box(f, new Vector3(0, 2.52f, R + .12f), new Vector3(2.0f, .08f, .06f));
            light.Box(f, new Vector3(0, 3.6f, R + .1f), new Vector3(.3f, .34f, .12f));   // keystone
            // arched windows onto the trophy lounge
            var lounge = HubClub.PaintedFit(HubClub.LoungeInterior, 1.3f, 1.2f, 2.25f, -.6f, .8f);
            foreach (int s in new[] { -1, 1 }) HubClub.Window(b, new HubClub.Frame(f.P(s * 2.5f, 0, R + .01f), f.O), .8f, 1.2f, 2.25f, true, lounge, .15f);
            var pilasterFace = new HubClub.Frame(f.P(0, 0, R + .19f), f.O);
            foreach (int s in new[] { -1, 1 }) HubClub.WallLantern(b, pilasterFace, s * 1.45f, 2.1f);
            // the front gable carrying the sign
            var stone = b[HubClub.LitTex(HubClub.StoneTex, HubClub.Stone, .22f, 1.2f)];
            stone.Extrude(new HubClub.Frame(f.P(0, 0, R), f.O), new List<Vector2> { new Vector2(-2.7f, 3.9f), new Vector2(2.7f, 3.9f), new Vector2(0, 6.35f) }, -2.6f, .06f);
            var slate = b[HubClub.LitTex(HubClub.SlateTex, HubClub.SlateTint, .25f, 1.6f)];
            foreach (int s in new[] { -1, 1 })
            {
                var p0 = f.P(s * 3.0f, 3.7f, R + .35f); var p1 = f.P(0, 6.6f, R + .35f); var p2 = f.P(0, 6.6f, R - 3.2f); var p3 = f.P(s * 3.0f, 3.7f, R - 3.2f);
                var nn = Vector3.Cross(p1 - p0, p3 - p0).normalized; if (nn.y < 0) nn = -nn; slate.Quad(p0, p1, p2, p3, nn);
            }
            light.Extrude(new HubClub.Frame(f.P(0, 0, R + .06f), f.O), new List<Vector2> { new Vector2(-2.9f, 3.62f), new Vector2(2.9f, 3.62f), new Vector2(2.9f, 3.72f), new Vector2(0, 6.48f), new Vector2(-2.9f, 3.72f) }, -.05f, .05f, false);
            HubClub.SignBoard(t, label, icon, f.P(0, 4.32f, R + .14f), f.O, .3f, 3.2f, 1.75f, true);
            light.Box(f, new Vector3(0, .03f, R + .35f), new Vector3(2.8f, .06f, .7f));
            foreach (int s in new[] { -1, 1 }) HubClub.Topiary(b, f.P(s * 1.62f, 0, R + .45f), .7f, true);
            foreach (int s in new[] { -1, 1 }) HubClub.BoxCollider(t, f.P(s * 1.62f, .6f, R + .45f), new Vector3(.6f, 1.2f, .6f), f.Facing);
        }

        // ------------------------------------------------------------------ PLAY: the club hall with its clock tower
        /// The PLAY hall: a stone portico (its front where the tower's door was) with columns, trophy niches, the club's name on
        /// the entablature and grand wooden doors standing open onto a glimpse of lawn court and green; a long hall behind with a
        /// navy roof and dormers; a clock tower rising from the middle with PLAY down its face, the clock, a slate spire, the flag.
        static void PlayHall(Transform root, Vector3 centre, float R, Vector3 front)
        {
            var t = new GameObject("PLAY hall").transform; t.SetParent(root, false);
            var b = new HubClub.Batch(); var f = new HubClub.Frame(centre, front);
            const float px = 4.4f, hall = 7.8f, hallFront = 3.6f, hallBack = -5.2f, eave = 4.6f;
            var stone = b[HubClub.LitTex(HubClub.StoneTex, HubClub.Stone, .22f, 1.2f)];
            var light = b[Lit(HubClub.StoneLight, .3f)];
            var shade = b[HubClub.LitTex(HubClub.StoneTex, HubClub.StoneShade, .2f, 1.2f)];
            // the hall and its roof, dormers along the front slope
            stone.Box(f, new Vector3(0, eave / 2, (hallBack + hallFront) / 2), new Vector3(hall * 2, eave, hallFront - hallBack));
            shade.Box(f, new Vector3(0, .25f, (hallBack + hallFront) / 2), new Vector3(hall * 2 + .16f, .5f, hallFront - hallBack + .16f));
            light.Box(f, new Vector3(0, eave - .08f, (hallBack + hallFront) / 2), new Vector3(hall * 2 + .3f, .2f, hallFront - hallBack + .3f));
            HubClub.HipRoof(b, f, -hall - .45f, hall + .45f, hallBack - .45f, hallFront + .45f, eave + .02f, 3.1f);
            foreach (int s in new[] { -1, 1 }) HubClub.Dormer(b, f, s * 5.8f, hallFront - .5f, eave + .5f, 1.25f, 1.05f);
            foreach (int s in new[] { -1, 1 }) foreach (float x in new[] { 5.6f, 7.0f }) HubClub.Window(b, new HubClub.Frame(f.P(s * (x - .1f), 0, hallFront + .01f), f.O), 1.05f, .9f, 2.0f, true, Glow(HubClub.WarmGlow * .85f), .13f);
            // the portico
            stone.Box(f, new Vector3(0, 2.8f, (hallFront - 1f + R) / 2), new Vector3(px * 2, 5.6f, R - hallFront + 1f));
            shade.Box(f, new Vector3(0, .25f, (hallFront - 1f + R) / 2), new Vector3(px * 2 + .16f, .5f, R - hallFront + 1.16f));
            light.Box(f, new Vector3(0, 5.42f, R + .32f), new Vector3(px * 2 + .5f, .56f, .9f));      // entablature over the columns
            light.Box(f, new Vector3(0, 5.76f, R + .36f), new Vector3(px * 2 + .8f, .14f, 1.05f));
            HubClub.HipRoof(b, f, -px - .4f, px + .4f, hallFront - 1.2f, R + .85f, 5.84f, 1.0f);
            // the clock tower: PLAY down its face (as the brief's island view), the clock above, a slate spire, a gold finial, the flag
            const float tz = 1.3f, th = 12.0f;
            stone.Box(f, new Vector3(0, (5.4f + th) / 2, tz), new Vector3(3.1f, th - 5.4f, 3.1f));
            light.Box(f, new Vector3(0, th, tz), new Vector3(3.4f, .26f, 3.4f));
            for (int s = 0; s < 4; s++)   // quoins
            {
                float sx = s % 2 == 0 ? -1 : 1, sz = s < 2 ? -1 : 1;
                for (int k = 0; k < 11; k++) light.Box(f, new Vector3(sx * 1.5f, 6.1f + k * .52f, tz + sz * 1.5f), new Vector3(k % 2 == 0 ? .5f : .3f, .46f, k % 2 == 0 ? .3f : .5f));
            }
            b[Lit(HubClub.StoneLight, .3f)].Box(f, new Vector3(0, th + .2f, tz), new Vector3(3.0f, .16f, 3.0f));
            b[HubClub.LitTex(HubClub.SlateTex, HubClub.SlateTint, .25f, 1.6f)].Lathe(f.P(0, th + .28f, tz), new[] { new Vector2(1.95f, 0), new Vector2(1.2f, .9f), new Vector2(.3f, 2.2f), new Vector2(0, 2.4f) }, 8);
            b[Lit(HubClub.Gold, .8f)].Lathe(f.P(0, th + 2.6f, tz), new[] { new Vector2(.03f, 0), new Vector2(.03f, 1.6f), new Vector2(0, 1.62f) }, 6);
            b[Lit(HubClub.Gold, .8f)].Ball(f.P(0, th + 2.75f, tz), Vector3.one * .2f, 1);
            HubLife.Of(root).Flag(f.P(0, th + 4.15f, tz), 1.5f, .9f, HubClub.Navy);
            var clockAt = f.P(0, 11.0f, tz + 1.58f);
            Put("Clock face", t, HubShapes.Disc(64), HubClub.Painted(HubClub.ClockFace, 1.05f), clockAt, Quaternion.LookRotation(Vector3.up, f.O), new Vector3(.85f, 1, .85f), false);
            Put("Clock rim", t, Lathe("clock rim small", new[] { new Vector2(.83f, -.04f), new Vector2(.98f, -.04f), new Vector2(.98f, .06f), new Vector2(.83f, .09f) }, .03f, 48), Lit(HubClub.Gold, .8f), clockAt, Quaternion.FromToRotation(Vector3.up, f.O));
            HubClub.SignBoardVertical(t, "PLAY", f.P(0, 8.62f, tz + 1.6f), f.O, .5f, 1.15f);
            // the front: a stone landing, two columns a side, trophy niches between, the arch with its doors open
            light.Box(f, new Vector3(0, .03f, R + .5f), new Vector3(px * 2 + .4f, .06f, 1.0f));
            foreach (int s in new[] { -1, 1 })
                foreach (float x in new[] { 2.3f, 3.95f })
                    light.Lathe(f.P(s * x, 0, R + .42f), new[] { new Vector2(.42f, 0), new Vector2(.42f, .22f), new Vector2(.34f, .32f), new Vector2(.29f, .45f), new Vector2(.25f, 4.7f), new Vector2(.36f, 4.86f), new Vector2(.4f, 5.14f), new Vector2(0, 5.14f) }, 16);
            light.Extrude(new HubClub.Frame(f.P(0, 0, R), f.O), HubClub.ArchOutline(1.88f, 0, 2.7f, 1.6f), 0f, .14f);
            b[HubClub.PaintedFit(HubClub.PlayGlimpse, 1.3f, 3.0f, 4.2f, -1.5f, 0)].Extrude(new HubClub.Frame(f.P(0, 0, R), f.O), HubClub.ArchOutline(1.5f, 0, 2.7f, 1.5f), 0f, .15f, false, false);
            light.Box(f, new Vector3(0, 4.3f, R + .22f), new Vector3(.36f, .42f, .14f));   // keystone
            var door = b[HubClub.LitTex(HubClub.PlankTex, HubClub.WoodWarm, .45f, .8f)];
            foreach (int s in new[] { -1, 1 })
            {
                // each leaf swung open toward the plaza, hinged on the arch's side
                var hinge = f.P(s * 1.5f, 0, R + .16f); var dir = (f.R * s * .26f + f.O * .97f).normalized;
                var lf = new HubClub.Frame(hinge + dir * .72f, Vector3.Cross(Vector3.up, dir) * s);
                door.Box(lf, new Vector3(0, 1.85f, 0), new Vector3(1.44f, 3.7f, .1f));
                foreach (float y in new[] { .9f, 2.45f }) b[Lit(HubClub.WoodDark, .45f)].Box(lf, new Vector3(0, y, .06f), new Vector3(1.0f, 1.1f, .03f));
                b[Lit(HubClub.Brass, .8f)].Lathe(lf.P(-s * .5f, 1.55f, .08f) , new[] { new Vector2(.06f, 0), new Vector2(.06f, .03f), new Vector2(0, .03f) }, 10);
            }
            foreach (int s in new[] { -1, 1 })
            {
                var nf = new HubClub.Frame(f.P(s * 3.08f, 0, R + .01f), f.O);
                b[Lit(HubClub.C("C8B48E"), .3f)].Extrude(nf, HubClub.ArchOutline(.52f, 1.35f, 2.75f, .52f), 0f, .1f);
                b[Lit(HubClub.NavyDeep, .35f)].Extrude(nf, HubClub.ArchOutline(.4f, 1.45f, 2.75f, .4f), 0f, .11f, false, false);
                light.Box(nf, new Vector3(0, 1.38f, .2f), new Vector3(1.0f, .08f, .3f));
                HubClub.Trophy(b, nf.P(0, 1.5f, .22f), .85f);
            }
            // the club's name on the entablature, gold stars either side (PLAY is on the tower)
            HubClub.SignBoard(t, "MOTION CLUB", null, f.P(0, 5.38f, R + .98f), f.O, .4f, 4.0f, 1.9f);
            foreach (int s in new[] { -1, 1 }) Put("Sign star", t, StarMesh(4, .26f, .09f, .03f), Lit(HubClub.Gold, .75f), f.P(s * 2.42f, 5.38f, R + .99f), Quaternion.FromToRotation(Vector3.up, f.O), null, false);
            // the navy runner with gold edges down to the plaza, brass lamps, banners, topiaries in urns
            b[Lit(HubClub.Navy, .25f)].Box(f, new Vector3(0, .07f, R + .6f), new Vector3(1.7f, .03f, 1.4f));
            foreach (int s in new[] { -1, 1 }) b[Lit(HubClub.Gold, .5f)].Box(f, new Vector3(s * .68f, .087f, R + .6f), new Vector3(.07f, .006f, 1.4f));
            foreach (int s in new[] { -1, 1 }) { HubClub.LampPost(b, f.P(s * 2.3f, 0, R + 1.25f), 2.6f); HubClub.BoxCollider(t, f.P(s * 2.3f, 1f, R + 1.25f), new Vector3(.4f, 2f, .4f), f.Facing); }
            HubClub.Banner(b, t, f.P(-5.25f, 0, R + .25f), f.O, "rackets", 2.3f);
            HubClub.Banner(b, t, f.P(5.25f, 0, R + .25f), f.O, "shield", 2.3f);
            foreach (int s in new[] { -1, 1 }) HubClub.BoxCollider(t, f.P(s * 5.25f, 1.5f, R + .25f), new Vector3(.3f, 3f, .3f), f.Facing);
            foreach (int s in new[] { -1, 1 })
            {
                HubClub.Topiary(b, f.P(s * 4.2f, 0, R + 1f), 1.05f, false);
                HubClub.RoseBush(b, f.P(s * 4.75f, 0, R + 1.55f), .9f, s > 0 ? 71 : 73);
                HubClub.RoseBush(b, f.P(s * 3.55f, 0, R + 1.6f), .75f, s > 0 ? 75 : 77);
                HubClub.RoseBush(b, f.P(s * 6.1f, 0, R + .35f), 1.0f, s > 0 ? 79 : 81);
            }
            foreach (int s in new[] { -1, 1 })
            {
                HubClub.Hedge(b, f.P(s * 4.8f, 0, hallFront + .6f), f.P(s * (hall - .2f), 0, hallFront + .6f), .75f, .7f, 9, s > 0 ? 5 : 9);
            }
            // colliders: the hall, the portico, the urns where the planters were
            HubClub.BoxCollider(t, f.P(0, 4f, (hallBack + hallFront) / 2), new Vector3(hall * 2, 8f, hallFront - hallBack), f.Facing);
            HubClub.BoxCollider(t, f.P(0, 4f, (hallFront - 1f + R) / 2), new Vector3(px * 2, 8f, R - hallFront + 1f), f.Facing);
            foreach (int s in new[] { -1, 1 }) Collider(t, HubKit.Drum(1.1f * .95f, .72f * .95f, 24), f.P(s * 4.2f, 0, R + 1f));
            var spill = new GameObject("Door light").AddComponent<Light>(); spill.transform.SetParent(t, false);
            spill.type = LightType.Point; spill.color = C("FFC98A"); spill.intensity = 1.4f; spill.range = 5f; spill.shadows = LightShadows.None;
            spill.transform.position = f.P(0, 1f, R + 2.3f);
            b.Flush(t, "PLAY");
        }

        // ------------------------------------------------------------------ the plaza's dressing
        static void Dressing(Transform root)
        {
            var t = new GameObject("Dressing").transform; t.SetParent(root, false);
            var b = new HubClub.Batch();
            // the four planters became rose beds and a fountain (same places, same colliders)
            foreach (var (x, z, s, seed) in new[] { (-10.4f, -6.6f, 1.25f, 1), (10.6f, -7.4f, 1.1f, 2), (-6.4f, 7.9f, .9f, 3), (6.4f, 7.9f, .9f, 4) })
            {
                var at = new Vector3(x, 0, z);
                if (seed == 4) HubClub.Fountain(b, at, 1.1f * s); else HubClub.RoseBed(b, at, 1.1f * s, seed * 13);
                Collider(t, HubKit.Drum(1.1f * s, .72f * s, 24), at);
            }
            // the side houses' planters: a topiary with roses by LOCKER, the terrace by CLUBHOUSE
            foreach (var pv in HubLayout.Pavilions)
            {
                if (pv.id == "play") continue;
                var toCentre = HubLayout.Facing(pv.centre, pv.face); var side = Vector3.Cross(Vector3.up, toCentre);
                var at = pv.centre + toCentre * (pv.radius + 1.6f) + side * 2.6f;
                if (pv.id == "locker") HubClub.RoseBed(b, at, 1.05f, 21);
                else
                {
                    HubClub.Parasol(b, at, toCentre, 3);
                    var f = new HubClub.Frame(pv.centre, toCentre);
                    var second = f.P(3.45f, 0, pv.radius + 1.45f);
                    HubClub.Parasol(b, second, -side, 2);
                    Collider(t, HubKit.Drum(.95f, 1f, 16), second);
                }
                Collider(t, HubKit.Drum(1.1f * .95f, .72f * .95f, 24), at);
            }
            // the club crest (the photo spot): a stone plinth, the navy-and-gold medal on a post, the club's name round the plinth
            var crestAt = new Vector3(9.6f, 0, -5.4f);
            var faceDir = new Vector3(0, 0, -9) - crestAt; faceDir.y = 0; faceDir.Normalize();
            b[HubClub.LitTex(HubClub.StoneTex, HubClub.StoneLight, .25f, 1.2f)].Lathe(crestAt, new[] { new Vector2(1.85f, 0), new Vector2(1.85f, .62f), new Vector2(1.95f, .72f), new Vector2(1.95f, .85f), new Vector2(1.7f, .9f), new Vector2(0, .9f) }, 12);
            var medal = crestAt + Vector3.up * 2.65f;
            Put("Crest disc", t, Lathe("crest disc", new[] { new Vector2(0, -.32f), new Vector2(1.65f, -.32f), new Vector2(1.65f, .32f), new Vector2(0, .32f) }, .22f, 72), Lit(HubClub.Gold, .7f), medal, Quaternion.FromToRotation(Vector3.up, faceDir));
            Put("Crest face", t, HubShapes.Disc(72), Lit(HubClub.Navy, .5f), medal + faceDir * .325f, Quaternion.FromToRotation(Vector3.up, faceDir), new Vector3(1.38f, 1, 1.38f));
            FlatIcon(t, "crest", medal + faceDir * .33f, 2.5f, HubClub.GoldText, Mathf.Atan2(-faceDir.x, -faceDir.z) * Mathf.Rad2Deg, true);
            CurvedText(t, "MOTION CLUB", crestAt, 1.97f, faceDir, .44f, .3f, HubClub.Navy);
            b[Lit(HubClub.Navy, .45f)].Lathe(crestAt + Vector3.up * .85f, new[] { new Vector2(.3f, 0), new Vector2(.22f, .95f), new Vector2(0, .95f) }, 16);
            Collider(t, HubKit.Drum(1.85f, 1f, 32), crestAt);
            // lamp posts round the edge (none inside a house)
            for (int i = 0; i < 12; i++)
            {
                float a = (i + .5f) * Mathf.PI * 2 / 12; var d = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a));
                if (Mathf.Abs(Mathf.DeltaAngle(a * Mathf.Rad2Deg, 0)) < 16) continue;
                var at = d * (HubLayout.PlazaRadius - .25f);
                if (InsideBuilding(at, .2f)) continue;
                HubClub.LampPost(b, at);
            }
            // garden borders inside the hedge on the front half: a white picket fence with roses behind it (the border is not walked)
            foreach (var (a0, a1) in new[] { (100f, 152f), (-152f, -100f) })
            {
                const float rf = 13.15f; int n = 7;
                for (int i = 0; i < n; i++)
                {
                    float u0 = Mathf.Lerp(a0, a1, i / (float)n), u1 = Mathf.Lerp(a0, a1, (i + 1) / (float)n);
                    var p0 = Dir(u0, 0) * rf; var p1 = Dir(u1, 0) * rf;
                    if (InsideBuilding(p0, .3f) || InsideBuilding(p1, .3f)) continue;
                    bool clash = false;
                    foreach (var (c, r) in new[] { (new Vector3(-10.4f, 0, -6.6f), 1.375f), (new Vector3(10.6f, 0, -7.4f), 1.21f), (new Vector3(9.6f, 0, -5.4f), 1.95f) })
                        if (Vector3.Distance((p0 + p1) / 2, c) < r + 1.1f) clash = true;
                    if (clash) continue;
                    HubClub.PicketFence(b, p0, p1, .72f);
                    var mid = (p0 + p1) / 2; var face = -mid.normalized;
                    HubClub.BoxCollider(t, mid + Vector3.up * .4f + mid.normalized * .45f, new Vector3((p1 - p0).magnitude, .8f, .95f), Quaternion.LookRotation(face));
                    HubClub.RoseBush(b, Dir((u0 + u1) / 2, 0) * (rf + .5f), .78f, 60 + i + (a0 > 0 ? 0 : 20));
                }
            }
            // two benches by the front rose beds, facing the medallion
            foreach (int s in new[] { -1, 1 })
            {
                var at = new Vector3(s * 8.4f, 0, -4.2f); var face = HubLayout.Stage - at; face.y = 0; face.Normalize();
                HubClub.Bench(b, at, face, 1.9f);
                HubClub.BoxCollider(t, at + Vector3.up * .45f - face * .05f, new Vector3(1.9f, .9f, .7f), Quaternion.LookRotation(face));
            }
            b.Flush(t, "Dressing");
        }

        /// Outside the hedge: trees, rose beds and fences, the grass court, the putting green with bunkers, the pond and its bridge,
        /// gravel paths between them.
        static void Grounds(Transform root)
        {
            var t = new GameObject("Grounds").transform; t.SetParent(root, false);
            var b = new HubClub.Batch(); var life = HubLife.Of(root);
            // the grass court (left lawn), white fence round it
            float courtAz = -122 * Mathf.Deg2Rad; var cc = new Vector3(Mathf.Sin(courtAz), 0, Mathf.Cos(courtAz)) * 23f; cc.y = -.08f;
            var along = new Vector3(Mathf.Cos(courtAz), 0, -Mathf.Sin(courtAz)); var cf = new HubClub.Frame(cc, along);
            var turf = b[HubClub.Surface("court turf", HubClub.C("5E9E3D"), HubClub.StripeTexZ, 0, new Vector2(1, 2.6f), .15f, .4f, .35f, .05f)];
            turf.Quad(cf.P(-5f, .02f, -10.5f), cf.P(-5f, .02f, 10.5f), cf.P(5f, .02f, 10.5f), cf.P(5f, .02f, -10.5f), Vector3.up);
            HubClub.CourtLines(b, cc + Vector3.up * .025f, along, .72f);
            HubClub.Net(b, cc + Vector3.up * .02f, along, 4.6f);
            var fence = new[] { cf.P(-5.3f, 0, -10.8f), cf.P(5.3f, 0, -10.8f), cf.P(5.3f, 0, 10.8f), cf.P(-5.3f, 0, 10.8f) };
            for (int i = 0; i < 4; i++) HubClub.PicketFence(b, fence[i] + Vector3.down * .08f, fence[(i + 1) % 4] + Vector3.down * .08f, .85f);
            // the putting green (right lawn) with three bunkers and a flag
            float greenAz = 122 * Mathf.Deg2Rad; var gc = new Vector3(Mathf.Sin(greenAz), 0, Mathf.Cos(greenAz)) * 22.5f; gc.y = -.08f;
            var gf = new HubClub.Frame(gc, new Vector3(Mathf.Sin(greenAz), 0, Mathf.Cos(greenAz)));
            b[HubClub.Surface("green collar", HubClub.C("4F9134"), null, 0, default, .2f, .5f, .35f, .05f)].Lathe(gc + Vector3.up * .015f, new[] { new Vector2(4.6f, 0), new Vector2(0, 0) }, 40, new Vector3(1.3f, 1, .95f));
            b[HubClub.Surface("putting green", HubClub.C("74B44A"), HubClub.StripeTexZ, 1, new Vector2(1.6f, 1.6f), .1f, .5f, .35f, .06f)].Lathe(gc + Vector3.up * .03f, new[] { new Vector2(4.0f, 0), new Vector2(0, 0) }, 40, new Vector3(1.3f, 1, .95f));
            var sand = b[HubClub.Surface("bunker", HubClub.Sand, HubClub.GravelTex, 1, new Vector2(1.2f, 1.2f), .15f, .8f, .4f, .03f)];
            foreach (var (x, z, sx, sz) in new[] { (-5.6f, -1.2f, 1.5f, 1.0f), (5.4f, .8f, 1.3f, .9f), (.5f, -4.4f, 1.8f, .8f) })
                sand.Lathe(gf.P(x, .04f, z), new[] { new Vector2(1f, 0), new Vector2(0, -.01f) }, 24, new Vector3(sx, 1, sz));
            var pin = gf.P(1.2f, .03f, .6f); HubClub.Pin(b, pin, 2.1f); life.Flag(pin + Vector3.up * 2.1f, .75f, .5f, HubClub.C("E8433A"));
            // the pond (front lawn) with a wooden bridge on the path
            float pondAz = 165 * Mathf.Deg2Rad; var pc = new Vector3(Mathf.Sin(pondAz), 0, Mathf.Cos(pondAz)) * 20f; pc.y = -.25f;
            var pondAlong = new Vector3(Mathf.Cos(pondAz), 0, -Mathf.Sin(pondAz));
            var pf = new HubClub.Frame(pc, pondAlong);
            b[HubClub.Water("pond", HubClub.C("3F8BA6"), HubClub.C("C8DCE6"), null, .35f, 1.2f, 1.6f, 5f, .6f)].Lathe(pc, new[] { new Vector2(1f, 0), new Vector2(0, 0) }, 40, new Vector3(4.2f, 1, 2.8f));
            b[Lit(HubClub.StoneShade, .2f)].Lathe(pc + Vector3.down * .05f, new[] { new Vector2(1.08f, .02f), new Vector2(1.0f, .24f), new Vector2(.97f, .06f) }, 40, new Vector3(4.2f, 1, 2.8f));
            for (int i = 0; i < 14; i++)
            {
                float a = i * Mathf.PI * 2 / 14 + .2f; var p = pc + new Vector3(Mathf.Cos(a) * 4.5f, .1f, Mathf.Sin(a) * 3.05f);
                if (i % 3 == 0) HubClub.RoseBush(b, p, .7f, 40 + i); else { var m = b[HubClub.Foliage]; m.colour = new Color(HubClub.Leaf.r, HubClub.Leaf.g * 1.05f, HubClub.Leaf.b, .3f); m.Ball(p + Vector3.up * .25f, new Vector3(.45f, .35f, .45f), 1, .15f, i); }
            }
            var lily = b[HubClub.Surface("lily pads", HubClub.C("4E8E3A"), null, 0, default, .2f, 2f)];
            for (int i = 0; i < 7; i++) lily.Lathe(pc + new Vector3((i * 1.37f % 5) - 2.5f, .02f, (i * .91f % 3) - 1.5f), new[] { new Vector2(.32f, 0), new Vector2(0, 0) }, 10);
            HubClub.Bridge(b, pc - pondAlong * 4.3f + Vector3.up * .15f, pc + pondAlong * 4.3f + Vector3.up * .15f, 1.7f, .6f);
            // gravel paths round the front lawn (court gate - pond bridge - green) and spurs to them
            var gravel = b[HubClub.Surface("garden path", HubClub.C("D9C7A2"), HubClub.GravelTex, 1, new Vector2(1.5f, 1.5f), .25f, .4f, .3f, .03f)];
            b.noShadow.Add(HubClub.Surface("garden path", HubClub.C("D9C7A2"), HubClub.GravelTex, 1, new Vector2(1.5f, 1.5f), .25f, .4f, .3f, .03f));
            const float pathR = 19.6f; int steps = 40; float a0 = 136 * Mathf.Deg2Rad, a1 = 222 * Mathf.Deg2Rad;
            for (int i = 0; i < steps; i++)
            {
                float u0 = Mathf.Lerp(a0, a1, i / (float)steps), u1 = Mathf.Lerp(a0, a1, (i + 1) / (float)steps);
                Vector3 P(float a, float r) => new Vector3(Mathf.Sin(a) * r, -.07f, Mathf.Cos(a) * r);
                var mid = P((u0 + u1) / 2, pathR);
                if (Vector3.Distance(new Vector3(mid.x, 0, mid.z), new Vector3(pc.x, 0, pc.z)) < 4.6f) continue;
                gravel.Quad(P(u0, pathR - .85f), P(u0, pathR + .85f), P(u1, pathR + .85f), P(u1, pathR - .85f), Vector3.up);
            }
            // trees round the island (clear of the court, the green and the houses)
            foreach (var (az, r, s, seed) in new[] {
                (-62f, 25f, 1.5f, 21), (-44f, 27f, 1.7f, 22), (-26f, 26.5f, 1.55f, 23), (-11f, 28.5f, 1.4f, 24), (0f, 27.2f, 1.3f, 25), (11f, 28.5f, 1.4f, 26),
                (26f, 26.5f, 1.55f, 27), (44f, 27f, 1.7f, 28), (62f, 25f, 1.5f, 29), (-38f, 19.6f, 1.25f, 30), (38f, 19.6f, 1.25f, 31), (-86f, 26f, 1.5f, 32),
                (86f, 25.5f, 1.45f, 33), (-158f, 28f, 1.35f, 34), (150f, 27f, 1.35f, 35), (-174f, 26.5f, 1.3f, 36), (178f, 27.5f, 1.4f, 37), (100f, 28.5f, 1.25f, 39) })
            {
                var at = Dir(az, 0) * r; at.y = -.1f;
                HubClub.Tree(b, at, s, seed);
            }
            // rose beds and short white fences on the lawn between the houses, hedges along the back
            foreach (var (az, r, rr, seed) in new[] { (-52f, 18.5f, 1.3f, 50), (52f, 18.5f, 1.3f, 51), (-28f, 25f, 1.2f, 52), (28f, 25f, 1.2f, 53), (-140f, 16.8f, 1.1f, 54), (140f, 16.8f, 1.1f, 55) })
            {
                var at = Dir(az, 0) * r + Vector3.down * .1f;
                if (!InsideBuilding(at, rr)) HubClub.RoseBed(b, at, rr, seed);
            }
            foreach (var (fa, fb, r) in new[] { (-70f, -42f, 15.6f), (42f, 70f, 15.6f), (-160f, -132f, 15.6f), (132f, 160f, 15.6f) })
                for (float a = fa; a < fb - .1f; a += 7f)
                {
                    var p0 = Dir(a, 0) * r + Vector3.down * .1f; var p1 = Dir(Mathf.Min(a + 7f, fb), 0) * r + Vector3.down * .1f;
                    if (InsideBuilding((p0 + p1) / 2, .3f) || InsideBuilding(p0, .1f) || InsideBuilding(p1, .1f)) continue;
                    HubClub.PicketFence(b, p0, p1, .8f);
                }
            b.Flush(t, "Grounds");
        }
    }
}
