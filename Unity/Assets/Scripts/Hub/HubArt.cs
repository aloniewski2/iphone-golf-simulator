using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace GolfArcade.Hub
{
    /// The Plaza's look (PLAN_MenuHub_WalkableWorld §7, concept 03 "simple evening plaza", made sport-neutral): a painted sunset
    /// sky with the sun low over the sea, hazy islands, soft rounded cream pavilions with royal-blue sign bands and lime trim,
    /// grass roofs with low-poly trees, warm glowing doorways, a pale stone plaza with the lavender ring and the blue emote stage,
    /// planters, lamp globes, the club crest sculpture, party pads at the PLAY door. Colours are picked from the concept.
    /// Lighting: a warm low sun with soft shadows, a pink/peach/purple trilight ambient, sky reflections, then neutral tone
    /// mapping, a warm white balance, purple shadows, gentle bloom on everything that glows, and a soft far-background blur.
    public static class HubArt
    {
        // ---- palette (sampled from proof/menu-hub/concepts/round5_simple/03_simple_evening_plaza.png)
        public static readonly Color Cream = C("F7EDE1"), CreamShade = C("EBDCCB"), Royal = C("3F62CC"), RoyalDeep = C("324FA8"),
            Lime = C("B9D84B"), Grass = C("86B65A"), GrassDeep = C("6E9E45"), Leaf = C("6FA043"), LeafDark = C("557F35"), Trunk = C("8A5A3C"),
            Stone = C("F4DED2"), RingLilac = C("CDBFDC"), Stage = C("3F5FC4"), StageRim = C("8EA2EA"), Bronze = C("4A3F45"),
            Taupe = C("8E7C72"), Gold = C("F1C24E"), SeaNear = C("A6A2D6"), SeaFar = C("F2B9A8"), Haze = C("C99BB4");
        public static readonly Color DoorGlow = new Color(2.4f, 1.6f, .75f), LampGlow = new Color(4.2f, 3.1f, 1.6f), SunGlow = new Color(4.5f, 3.9f, 2.6f);
        public static readonly Vector3 SunSkyDir = Dir(21, 8.5f), SunLightDir = Dir(152, 31);
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
        static Material GlowTex(Texture2D tex, float hdr) { var m = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "Hub glow " + tex.name }; m.SetTexture("_BaseMap", tex); m.SetColor("_BaseColor", new Color(hdr, hdr, hdr, 1)); return m; }
        static Texture2D doorGradient;
        /// A lit room seen through a doorway: warm white high up, amber lower down, darker at the edges.
        static Texture2D DoorGradient()
        {
            if (doorGradient) return doorGradient;
            const int w = 64, h = 128; var tex = new Texture2D(w, h, TextureFormat.RGBA32, true) { name = "door light", wrapMode = TextureWrapMode.Clamp };
            var px = new Color[w * h];
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
            {
                float u = x / (w - 1f), v = y / (h - 1f);
                var c = Color.Lerp(C("E9862F"), C("FFE9B8"), Mathf.SmoothStep(0, 1, v * 1.2f - .1f));
                float edge = Mathf.SmoothStep(0, 1, Mathf.Min(u, 1 - u) * 6) * Mathf.SmoothStep(0, 1, (1 - v) * 10);
                c *= Mathf.Lerp(.55f, 1f, edge); c.a = 1; px[y * w + x] = c;
            }
            tex.SetPixels(px); tex.Apply(true, true); return doorGradient = tex;
        }
        static Material Painted(Texture2D tex) { var m = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "Hub painted " + tex.name }; m.SetTexture("_BaseMap", tex); m.SetColor("_BaseColor", Color.white); return m; }

        static GameObject Put(string name, Transform parent, Mesh mesh, Material mat, Vector3 pos, Quaternion? rot = null, Vector3? scale = null, bool shadows = true)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, rot ?? Quaternion.identity); if (scale.HasValue) go.transform.localScale = scale.Value;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = mat; if (!shadows) r.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }
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
        /// The sky's colour in a direction (also the reflection cubemap and the ambient).
        public static Color SkyColor(Vector3 d)
        {
            float e = d.y;
            Color c;
            if (e < 0) c = Color.Lerp(C("FBC39D"), C("F3B096"), Mathf.Clamp01(-e * 6));
            else
            {
                var stops = new[] { (0f, C("FFB771")), (.05f, C("FFA67A")), (.13f, C("FC9888")), (.26f, C("F293A0")), (.42f, C("D896B8")), (.66f, C("A991CB")), (1f, C("8088C4")) };
                c = stops[0].Item2;
                for (int k = 1; k < stops.Length; k++) if (e >= stops[k - 1].Item1) c = Color.Lerp(stops[k - 1].Item2, stops[k].Item2, Mathf.InverseLerp(stops[k - 1].Item1, stops[k].Item1, e));
            }
            float sun = Mathf.Max(0, Vector3.Dot(d.normalized, SunSkyDir));
            c += C("FFC97A") * (Mathf.Pow(sun, 6) * .28f + Mathf.Pow(sun, 40) * .4f + Mathf.Pow(sun, 400) * .6f);
            c += C("FFD69E") * Mathf.Exp(-Mathf.Abs(e) * 18) * .22f;
            float flat = Mathf.Max(0, Vector3.Dot(new Vector3(d.x, 0, d.z).normalized, new Vector3(SunSkyDir.x, 0, SunSkyDir.z).normalized));
            c += C("FFB46A") * Mathf.Pow(flat, 8) * Mathf.Exp(-Mathf.Abs(e) * 7) * .35f;
            // soft streaky clouds
            float az = Mathf.Atan2(d.x, d.z);
            foreach (var (caz, cel, w, h, a) in new[] { (-.55f, .16f, .32f, .018f, .35f), (.15f, .22f, .45f, .02f, .25f), (.9f, .12f, .3f, .015f, .3f), (-1.4f, .26f, .4f, .02f, .2f), (1.6f, .2f, .35f, .018f, .25f) })
            {
                float dx = Mathf.DeltaAngle(az * Mathf.Rad2Deg, caz * Mathf.Rad2Deg) * Mathf.Deg2Rad / w, dy = (e - cel) / h;
                float k = Mathf.Exp(-(dx * dx + dy * dy)) * a * (.8f + .2f * Mathf.Sin(az * 37 + e * 90));
                c = Color.Lerp(c, C("FFC6AE"), Mathf.Clamp01(k));
            }
            c.a = 1; return c;
        }

        public static void Lighting(Transform parent, Camera camera)
        {
            var sun = new GameObject("Evening sun").AddComponent<Light>(); sun.transform.SetParent(parent, false);
            sun.type = LightType.Directional; sun.color = C("FFC58F"); sun.intensity = 2.05f; sun.shadows = LightShadows.Soft; sun.shadowStrength = .82f;
            sun.shadowBias = .04f; sun.shadowNormalBias = .3f;
            sun.transform.rotation = Quaternion.LookRotation(-SunLightDir, Vector3.up);
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = C("C9A3C9") * .78f; RenderSettings.ambientEquatorColor = C("F2A58C") * .72f; RenderSettings.ambientGroundColor = C("7D6A92") * .7f;
            RenderSettings.fog = false; RenderSettings.skybox = null;
            // sky reflections on every smooth surface (the plastic sheen of the concept)
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
                    var c = SkyColor(d.normalized); if (d.y < 0) c = Color.Lerp(c, C("C4A79C"), Mathf.Clamp01(-d.normalized.y * 3));
                    px[y * size + x] = c;
                }
                cube.SetPixels(px, face);
            }
            cube.Apply(false, true);
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom; RenderSettings.customReflectionTexture = cube; RenderSettings.reflectionIntensity = .85f;
            // camera + post
            camera.allowHDR = true; camera.fieldOfView = 42; camera.nearClipPlane = .3f;
            var data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true; data.renderShadows = true; data.antialiasing = AntialiasingMode.None;
            var vol = new GameObject("Hub post-processing"); vol.transform.SetParent(camera.transform, false);
            data.volumeLayerMask = 1 << vol.layer;
            var volume = vol.AddComponent<Volume>(); volume.isGlobal = true; volume.priority = 20;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>(); profile.name = "Hub evening";
            var tm = profile.Add<Tonemapping>(true); tm.mode.Override(TonemappingMode.Neutral);
            var ca = profile.Add<ColorAdjustments>(true); ca.postExposure.Override(.1f); ca.contrast.Override(22); ca.saturation.Override(26); ca.colorFilter.Override(Color.white);
            var wb = profile.Add<WhiteBalance>(true); wb.temperature.Override(3); wb.tint.Override(6);
            var smh = profile.Add<ShadowsMidtonesHighlights>(true);
            smh.shadows.Override(new Vector4(.94f, .88f, 1.1f, -.02f)); smh.midtones.Override(new Vector4(1.02f, .99f, .98f, 0)); smh.highlights.Override(new Vector4(1.04f, 1.0f, .94f, 0));
            var bloom = profile.Add<Bloom>(true); bloom.threshold.Override(1.1f); bloom.intensity.Override(.8f); bloom.scatter.Override(.62f); bloom.tint.Override(C("FFE3C4")); bloom.maxIterations.Override(5);
            var vig = profile.Add<Vignette>(true); vig.intensity.Override(.2f); vig.smoothness.Override(.45f); vig.color.Override(C("3B2A4A"));
            var dof = profile.Add<DepthOfField>(true); dof.mode.Override(DepthOfFieldMode.Gaussian); dof.gaussianStart.Override(55); dof.gaussianEnd.Override(220); dof.gaussianMaxRadius.Override(1.1f); dof.highQualitySampling.Override(true);
            volume.sharedProfile = profile;
        }

        // ================================================================== the plaza
        public static void BuildPlaza(Transform root)
        {
            Sky(root); Sea(root); Islands(root);
            Ground(root);
            foreach (var pv in HubLayout.Pavilions)
            {
                var facing = HubLayout.Facing(pv.centre, pv.face);
                if (pv.id == "play") PlayTower(root, pv.centre, pv.radius, facing);
                else Pavilion(root, pv.id, pv.label, pv.centre, pv.radius, pv.id == "locker" ? "hanger" : "crown", facing);
            }
            Dressing(root);
        }

        static void Sky(Transform root)
        {
            const int w = 1024, h = 512;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true) { name = "Hub sky", wrapModeU = TextureWrapMode.Repeat, wrapModeV = TextureWrapMode.Clamp };
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                float e = Mathf.Asin(Mathf.Clamp(y / (h - 1f) * 2 - 1, -1, 1));
                for (int x = 0; x < w; x++)
                {
                    float az = x / (float)w * Mathf.PI * 2;
                    px[y * w + x] = SkyColor(new Vector3(Mathf.Sin(az) * Mathf.Cos(e), Mathf.Sin(e), Mathf.Cos(az) * Mathf.Cos(e)));
                }
            }
            tex.SetPixels(px); tex.Apply(true, true);
            var dome = Put("Sky", root, HubKit.SkyDome(64, 32), Painted(tex), Vector3.zero, null, Vector3.one * 760, false);
            dome.GetComponent<MeshRenderer>().receiveShadows = false;
            // the sun: a bright disc low over the sea (HDR, it blooms)
            var sun = Put("Sun", root, HubShapes.Disc(48), Glow(SunGlow), SunSkyDir * 640, Quaternion.FromToRotation(Vector3.up, -SunSkyDir), Vector3.one * 22, false);
            sun.GetComponent<MeshRenderer>().receiveShadows = false;
        }

        static void Sea(Transform root)
        {
            const int s = 1024; const float R = 900;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, true) { name = "Hub sea", wrapMode = TextureWrapMode.Clamp };
            var px = new Color[s * s]; var sunFlat = new Vector2(SunSkyDir.x, SunSkyDir.z).normalized;
            for (int y = 0; y < s; y++) for (int x = 0; x < s; x++)
            {
                var p = new Vector2((x + .5f) / s * 2 - 1, (y + .5f) / s * 2 - 1); float r = p.magnitude;
                float far = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.04f, .55f, r));
                var c = Color.Lerp(SeaNear, SeaFar, far);
                // a soft band of light under the sun, brighter toward the horizon, with ripple breaks
                var dir = r > 1e-4f ? p / r : Vector2.up;
                float along = Vector2.Dot(dir, sunFlat), across = Mathf.Abs(dir.x * sunFlat.y - dir.y * sunFlat.x) * r * R;
                float band = along > 0 ? Mathf.Exp(-across * across / (2 * Mathf.Pow(8 + r * 70, 2))) * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.03f, .6f, r)) : 0;
                float ripple = .65f + .35f * Mathf.Sin(r * R * .9f + Mathf.Sin(across * .2f) * 2);
                c = Color.Lerp(c, C("FFE6B8"), Mathf.Clamp01(band * ripple * .85f));
                c.a = 1; px[y * s + x] = c;
            }
            tex.SetPixels(px); tex.Apply(true, true);
            Put("Sea", root, HubShapes.Disc(96), Painted(tex), new Vector3(0, -2.3f, 0), null, new Vector3(R, 1, R), false).GetComponent<MeshRenderer>().receiveShadows = false;
        }

        static void Islands(Transform root)
        {
            // far ranges in haze (lighter and pinker with distance), the near headlands greener
            var ranges = new (float az, float dist, float w, float h, Color c, int seed)[]
            {
                (-62, 330, 130, 34, C("B98FB0"), 11), (-38, 380, 120, 46, C("BE94B2"), 12), (-12, 420, 150, 30, C("C59BB5"), 13),
                (14, 400, 110, 40, C("C29AB4"), 14), (55, 360, 140, 28, C("C6A0B6"), 15), (82, 300, 120, 38, C("BC95B0"), 16),
                (-80, 210, 90, 22, C("A88AA2"), 17), (70, 230, 80, 18, C("AE8EA6"), 18), (-28, 260, 70, 16, C("B090A8"), 19),
            };
            foreach (var (az, dist, w, h, c, seed) in ranges)
            {
                var at = Dir(az, 0) * dist; at.y = -h * .32f - 1.6f;
                var hill = Put("Island", root, HubShapes.Crown(seed, 4, .18f), Lit(c, .05f), at, Quaternion.Euler(0, seed * 37, 0), new Vector3(w, h, w * .55f), false);
                hill.GetComponent<MeshRenderer>().receiveShadows = false;
            }
        }

        static void Ground(Transform root)
        {
            // plaza stone with a soft rounded kerb, the lavender ring, the emote stage with its figure
            float R0 = HubLayout.PlazaRadius + .45f, R1 = R0 + .7f; var st = HubLayout.Stage;
            var floor = Lathe("plaza floor", new[] { new Vector2(0, -.4f), new Vector2(R0, -.4f), new Vector2(R0, 0), new Vector2(0, 0) }, .12f, 96);
            Put("Plaza floor", root, floor, FloorMaterial(R0), Vector3.zero);
            Collider(root, HubKit.Drum(R0, .4f, 64), new Vector3(0, -.4f, 0));
            Put("Plaza ring", root, HubKit.Ring(6.9f, 8.4f, 96), Lit(RingLilac, .25f), st + new Vector3(0, .006f, 0), null, null, false);
            var stage = Lathe("emote stage", new[] { new Vector2(3.45f, 0), new Vector2(3.35f, .2f), new Vector2(0, .2f) }, .1f, 96);
            Put("Emote stage", root, stage, Lit(Stage, .45f), st);
            Put("Emote stage rim", root, HubKit.Ring(3.0f, 3.18f, 96), Lit(StageRim, .4f), st + new Vector3(0, .207f, 0), null, null, false);
            Collider(root, HubKit.Drum(3.4f, .2f, 48), st);
            FlatIcon(root, "figure", st + new Vector3(0, .212f, .1f), 2.6f, new Color(1, 1, 1, .9f), 0);
            // party pads at the PLAY door (friends gather here; party colours)
            var pads = new[] { C("5B8CFF"), C("7BDA4A"), C("FF63B8"), C("A970FF") };
            for (int i = 0; i < 4; i++)
            {
                var at = new Vector3(-3.45f + i * 2.3f, 0, 9.3f);
                Put("Party pad", root, Lathe("party pad", new[] { new Vector2(.86f, 0), new Vector2(.8f, .1f), new Vector2(0, .1f) }, .04f, 48), Lit(C("2D3150"), .6f), at);
                Put("Party pad glow", root, HubKit.Ring(.5f, .8f, 48), Glow(pads[i] * 2.8f), at + Vector3.up * .105f, null, null, false);
                Put("Party pad light", root, HubShapes.Disc(32), Glow(pads[i] * .8f), at + Vector3.up * .102f, null, new Vector3(.51f, 1, .51f), false);
            }
            // low wall around the plaza and the green slope down to the sea
            var wall = Lathe("plaza wall", new[] { new Vector2(R1, -.1f), new Vector2(R1, .5f), new Vector2(R0, .5f), new Vector2(R0, -.1f) }, .14f, 120);
            Put("Plaza wall", root, wall, Lit(Cream, .3f), Vector3.zero);
            Collider(root, HubKit.RingWall(R0, R1, .55f, 64), Vector3.zero);
            var slope = Lathe("grass slope", new[] { new Vector2(28.5f, -2.6f), new Vector2(26.5f, -1.15f), new Vector2(24.5f, -.55f), new Vector2(R1, -.08f) }, 1.2f, 120);
            Put("Grass", root, slope, Lit(Grass, .12f), Vector3.zero);
            var shore = Lathe("shore", new[] { new Vector2(37f, -4f), new Vector2(30f, -2.9f), new Vector2(28.2f, -2.45f), new Vector2(26.9f, -1.25f) }, .6f, 120);
            Put("Shore", root, shore, Lit(C("E8C9A8"), .2f), Vector3.zero);
            for (int i = 0; i < 26; i++)
            {
                float a = i * .2417f * Mathf.PI * 2 + .3f, r = 27.2f + (i * 13 % 7) * .25f;
                Put("Rock", root, HubShapes.Crown(300 + i, 2, .2f), Lit(Color.Lerp(C("C9A9A0"), C("A98C95"), (i % 5) / 4f), .1f),
                    new Vector3(Mathf.Sin(a) * r, -1.9f, Mathf.Cos(a) * r), Quaternion.Euler(0, i * 41, 0), new Vector3(1.6f, .9f, 1.2f) * (.8f + (i % 3) * .3f));
            }
        }

        static Material FloorMaterial(float R0)
        {
            // the floor lathe's UV: u = around, v = distance along the profile from the centre (0 .. R0 on the top face)
            const int w = 8, h = 256; var tex = new Texture2D(w, h, TextureFormat.RGBA32, true) { name = "plaza stone", wrapModeU = TextureWrapMode.Repeat, wrapModeV = TextureWrapMode.Clamp };
            var px = new Color[w * h]; float total = R0 + .4f + R0;   // bottom, side, top lengths in profile order (metres along the lathe profile)
            for (int y = 0; y < h; y++)
            {
                float along = y / (h - 1f) * total; float fromCentre = Mathf.Max(0, total - along);
                float k = Mathf.Clamp01(fromCentre / R0);
                var c = Color.Lerp(C("FAE8DD"), Stone, Mathf.SmoothStep(0, 1, k * 1.1f));
                c = Color.Lerp(c, C("E9CBBE"), Mathf.SmoothStep(.86f, 1f, k) * .7f);
                for (int x = 0; x < w; x++) px[y * w + x] = c;
            }
            tex.SetPixels(px); tex.Apply(true, true);
            var m = new Material(Lit(Stone, .22f)) { name = "Hub plaza stone" }; m.SetTexture("_BaseMap", tex); m.SetColor("_BaseColor", Color.white); m.SetTextureScale("_BaseMap", new Vector2(1, 1 / total));
            return m;
        }

        /// A word laid around a cylinder (signs on the round bands), letters upright, centred on `facing`.
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
            float cursor = -span / 2;
            // reading left to right from outside means increasing angle clockwise seen from above: angle grows to the reader's left in
            // Unity's left-handed frame, so walk the letters with a negative step
            float sign = -1; cursor = -span / 2;
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
                    var sh = t.gameObject.AddComponent<Shadow>(); sh.effectColor = new Color(.12f, .14f, .3f, .55f); sh.effectDistance = new Vector2(2.5f, -3f);
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

        /// A white icon on a surface (flat on the floor, or on a wall when `upright`).
        public static void FlatIcon(Transform parent, string icon, Vector3 pos, float size, Color color, float yaw, bool upright = false)
        {
            var go = new GameObject("Icon " + icon); go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, upright ? Quaternion.Euler(0, yaw, 0) : Quaternion.Euler(90, yaw, 0));
            var canvas = go.AddComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
            var rt = (RectTransform)go.transform; rt.sizeDelta = new Vector2(size * 100, size * 100); rt.localScale = Vector3.one / 100;
            var img = new GameObject("Image").AddComponent<RawImage>(); img.transform.SetParent(go.transform, false); img.texture = HubIcons.Get(icon); img.color = color; img.raycastTarget = false;
            var irt = img.rectTransform; irt.anchorMin = Vector2.zero; irt.anchorMax = Vector2.one; irt.offsetMin = irt.offsetMax = Vector2.zero;
        }

        // ------------------------------------------------------------------ pavilions
        static void Pavilion(Transform root, string id, string label, Vector3 centre, float radius, string icon, Vector3 toCentre)
        {
            var t = new GameObject(label + " pavilion").transform; t.SetParent(root, false);
            float R = radius;
            Put("Plinth", t, Lathe("pav plinth " + R, new[] { new Vector2(0, 0), new Vector2(R + .18f, 0), new Vector2(R + .18f, .24f), new Vector2(0, .24f) }, .06f), Lit(CreamShade, .25f), centre);
            Put("Wall", t, Lathe("pav wall " + R, new[] { new Vector2(0, .2f), new Vector2(R, .2f), new Vector2(R, 3.45f), new Vector2(0, 3.45f) }, .05f), Lit(Cream, .3f), centre);
            Put("Band", t, Lathe("pav band " + R, new[] { new Vector2(R - .2f, 3.35f), new Vector2(R + .32f, 3.35f), new Vector2(R + .32f, 4.65f), new Vector2(R - .2f, 4.65f) }, .16f), Lit(Royal, .38f), centre);
            Put("Lime lip", t, Lathe("pav lip " + R, new[] { new Vector2(R - .2f, 4.62f), new Vector2(R + .42f, 4.62f), new Vector2(R + .42f, 4.86f), new Vector2(R - .2f, 4.86f) }, .1f), Lit(Lime, .35f), centre);
            Put("Roof", t, Lathe("pav roof " + R, new[] { new Vector2(R + .3f, 4.84f), new Vector2(R * .7f, 5.25f), new Vector2(0, 5.4f) }, .6f), Lit(Grass, .12f), centre);
            Collider(t, HubKit.Drum(R + .1f, 4.9f, 48), centre);
            CurvedText(t, label, centre, R + .34f, toCentre, 4.0f, .62f, Color.white, icon);
            // roof garden: a tree and bushes
            var side = Vector3.Cross(Vector3.up, toCentre);
            Tree(t, centre - toCentre * 1.6f - side * Mathf.Sign(centre.x) * 1.2f + Vector3.up * 5.2f, 1.25f, id.Length * 31);
            for (int i = 0; i < 5; i++)
            {
                float a = Mathf.Atan2(toCentre.x, toCentre.z) + (i - 2) * .55f;
                var d = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a));
                Bush(t, centre + d * (R - .5f) + Vector3.up * 5.0f, .62f + (i % 2) * .15f, i + (id.Length * 7));
            }
            Doorway(t, centre, R, toCentre, 2.4f, 2.95f, icon, false);
            // planters flanking the door
            Planter(t, centre + toCentre * (R + 1.6f) + side * 2.6f, .95f, id.Length * 3);
        }

        /// A warm, glowing doorway set into a drum: rounded pillars and header, a lit interior, the room's panel with its icon.
        static void Doorway(Transform t, Vector3 centre, float R, Vector3 outward, float width, float height, string icon, bool royalFrame)
        {
            // Everything stands proud of the drum: a deep portal (pillars, header) whose inside is a warm lit recess, so the doorway
            // reads as an opening into a bright room without cutting the wall.
            var rot = Quaternion.LookRotation(outward, Vector3.up); var side = Vector3.Cross(Vector3.up, outward);
            var face = centre + outward * R;
            var frame = royalFrame ? Lit(Royal, .38f) : Lit(Cream, .3f);
            float pw = .5f, deep = royalFrame ? 1.1f : .85f;
            foreach (int s in new[] { -1, 1 })
                Put("Pillar", t, HubShapes.RoundedBox(new Vector3(pw, height + .35f, deep + .3f), .1f), frame, face + side * s * (width / 2 + pw / 2) + Vector3.up * (height + .35f) / 2 + outward * (deep / 2 - .15f), rot);
            Put("Header", t, HubShapes.RoundedBox(new Vector3(width + pw * 2 + .1f, .55f, deep + .35f), .12f), frame, face + Vector3.up * (height + .3f) + outward * (deep / 2 - .13f), rot);
            Put("Threshold", t, HubShapes.RoundedBox(new Vector3(width + pw * 2, .1f, deep + .9f), .04f), Lit(CreamShade, .3f), face + Vector3.up * .05f + outward * (deep / 2 + .25f), rot);
            var inner = Lit(C("FFD3A0"), .2f);
            foreach (int s in new[] { -1, 1 })
                Put("Reveal", t, HubShapes.RoundedBox(new Vector3(.1f, height, deep + .25f), .03f), inner, face + side * s * (width / 2 - .05f) + Vector3.up * height / 2 + outward * (deep / 2 - .12f), rot);
            Put("Soffit", t, HubShapes.RoundedBox(new Vector3(width, .1f, deep + .25f), .03f), inner, face + Vector3.up * (height - .05f) + outward * (deep / 2 - .12f), rot);
            Put("Interior floor", t, HubShapes.RoundedBox(new Vector3(width, .06f, deep + .25f), .02f), Lit(C("E8B98C"), .3f), face + Vector3.up * .11f + outward * (deep / 2 - .12f), rot);
            Put("Interior glow", t, HubShapes.Quad(new Vector2(width - .1f, height - .1f)), GlowTex(DoorGradient(), 2.1f), face + Vector3.up * (height / 2) + outward * .06f, Quaternion.LookRotation(-outward, Vector3.up), null, false);
            if (icon != null)
            {
                var panelAt = face + outward * .16f;
                Put("Door panel", t, HubShapes.RoundedBox(new Vector3(width * .72f, height * .76f, .08f), .05f), Lit(Taupe, .3f), panelAt + Vector3.up * (height * .4f), rot);
                FlatIcon(t, icon, panelAt + Vector3.up * (height * .46f) + outward * .05f, width * .42f, new Color(1, 1, 1, .92f), Mathf.Atan2(-outward.x, -outward.z) * Mathf.Rad2Deg, true);
            }
            // light spilling out onto the plaza in front (kept off the wall so it never burns it white)
            var spill = new GameObject("Door light").AddComponent<Light>(); spill.transform.SetParent(t, false);
            spill.type = LightType.Point; spill.color = C("FFC07A"); spill.intensity = 1.3f; spill.range = 4.5f; spill.shadows = LightShadows.None;
            spill.transform.position = face + outward * (deep + 1.4f) + Vector3.up * .9f;
        }

        // ------------------------------------------------------------------ PLAY tower
        static void PlayTower(Transform root, Vector3 centre, float R, Vector3 front)
        {
            var t = new GameObject("PLAY tower").transform; t.SetParent(root, false);
            Put("Plinth", t, Lathe("play plinth", new[] { new Vector2(0, 0), new Vector2(R + .25f, 0), new Vector2(R + .25f, .3f), new Vector2(0, .3f) }, .08f), Lit(CreamShade, .25f), centre);
            Put("Wall", t, Lathe("play wall", new[] { new Vector2(0, .25f), new Vector2(R, .25f), new Vector2(R, 5.8f), new Vector2(0, 5.8f) }, .05f), Lit(Cream, .3f), centre);
            Put("Band", t, Lathe("play band", new[] { new Vector2(R - .2f, 5.7f), new Vector2(R + .4f, 5.7f), new Vector2(R + .4f, 7.75f), new Vector2(R - .2f, 7.75f) }, .2f), Lit(Royal, .38f), centre);
            Put("Lime lip", t, Lathe("play lip", new[] { new Vector2(R - .2f, 7.7f), new Vector2(R + .5f, 7.7f), new Vector2(R + .5f, 7.95f), new Vector2(R - .2f, 7.95f) }, .1f), Lit(Lime, .35f), centre);
            float R2 = R - 1.0f;
            Put("Upper wall", t, Lathe("play upper", new[] { new Vector2(0, 7.9f), new Vector2(R2, 7.9f), new Vector2(R2, 11.1f), new Vector2(0, 11.1f) }, .05f), Lit(Cream, .3f), centre);
            Put("Upper band", t, Lathe("play upper band", new[] { new Vector2(R2 - .2f, 11.0f), new Vector2(R2 + .3f, 11.0f), new Vector2(R2 + .3f, 11.7f), new Vector2(R2 - .2f, 11.7f) }, .15f), Lit(Royal, .38f), centre);
            Put("Crown rim", t, Lathe("play crown", new[] { new Vector2(R2 - .2f, 11.65f), new Vector2(R2 + .2f, 11.65f), new Vector2(R2 + .2f, 12.0f), new Vector2(R2 - .6f, 12.0f), new Vector2(R2 - .6f, 11.85f), new Vector2(0, 11.85f) }, .1f), Lit(Cream, .3f), centre);
            Put("Crown garden", t, Lathe("play garden", new[] { new Vector2(R2 - .55f, 11.84f), new Vector2(R2 * .5f, 12.15f), new Vector2(0, 12.25f) }, .4f), Lit(Grass, .12f), centre);
            for (int i = 0; i < 9; i++)
            {
                float a = i * Mathf.PI * 2 / 9 + .2f; var d = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a));
                Bush(t, centre + d * (R2 - .45f) + Vector3.up * 12.0f, .55f + (i % 3) * .12f, 40 + i);
            }
            Collider(t, HubKit.Drum(R + .1f, 8f, 64), centre);
            CurvedText(t, "PLAY", centre, R + .42f, front, 6.72f, 1.25f, Color.white);
            // the crest medallion on the upper tier
            var medal = centre + front * (R2 + .05f) + Vector3.up * 9.5f;
            Put("Medallion ring", t, Lathe("medallion", new[] { new Vector2(0, -.18f), new Vector2(1.25f, -.18f), new Vector2(1.25f, .18f), new Vector2(0, .18f) }, .12f, 64), Lit(Royal, .4f), medal, Quaternion.FromToRotation(Vector3.up, front));
            Put("Medallion face", t, HubShapes.Disc(64), Lit(Gold, .55f), medal + front * .19f, Quaternion.FromToRotation(Vector3.up, front), new Vector3(1.05f, 1, 1.05f));
            FlatIcon(t, "crest", medal + front * .21f, 1.9f, C("3A4A96"), Mathf.Atan2(-front.x, -front.z) * Mathf.Rad2Deg, true);
            // banners either side of the door
            var side = Vector3.Cross(Vector3.up, front);
            foreach (int s in new[] { -1, 1 })
            {
                float a = Mathf.Atan2(front.x, front.z) + s * .5f; var d = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a));
                var at = centre + d * (R + .12f) + Vector3.up * 3.9f;
                Put("Banner", t, HubShapes.RoundedBox(new Vector3(1.0f, 3.1f, .14f), .07f), Lit(Royal, .38f), at, Quaternion.LookRotation(d));
                Put("Banner tip", t, HubShapes.RoundedBox(new Vector3(1.0f, .14f, .2f), .05f), Lit(Lime, .35f), at + Vector3.up * 1.58f, Quaternion.LookRotation(d));
                FlatIcon(t, "crest", at + d * .09f + Vector3.up * .55f, .78f, Color.white, Mathf.Atan2(-d.x, -d.z) * Mathf.Rad2Deg, true);
            }
            Doorway(t, centre, R, front, 3.0f, 3.7f, null, true);
            // greenery at the foot of the tower
            foreach (int s in new[] { -1, 1 })
            {
                Planter(t, centre + front * (R + 1.0f) + side * s * 4.2f, .95f, 60 + s);
                Tree(t, centre + front * (R - 3.2f) + side * s * 8.8f + Vector3.down * .2f, 1.6f, 70 + s);
            }
        }

        // ------------------------------------------------------------------ dressing
        static void Dressing(Transform root)
        {
            var t = new GameObject("Dressing").transform; t.SetParent(root, false);
            // planters with bushes on the plaza (front corners, the sides) — kept off the walking lines to the doors
            foreach (var (x, z, s, seed) in new[] { (-10.4f, -6.6f, 1.25f, 1), (10.6f, -7.4f, 1.1f, 2), (-6.4f, 7.9f, .9f, 3), (6.4f, 7.9f, .9f, 4) })
                Planter(t, new Vector3(x, 0, z), s, seed);
            // the club crest sculpture (the photo spot; sport neutral)
            var crestAt = new Vector3(9.6f, 0, -5.4f);
            Planter(t, crestAt, 1.75f, 9, false);
            var stand = crestAt + Vector3.up * .9f; var faceDir = (new Vector3(0, 0, -9) - crestAt); faceDir.y = 0; faceDir.Normalize();
            var medal = stand + Vector3.up * 1.75f;
            var rot = Quaternion.LookRotation(faceDir);
            Put("Crest disc", t, Lathe("crest disc", new[] { new Vector2(0, -.32f), new Vector2(1.65f, -.32f), new Vector2(1.65f, .32f), new Vector2(0, .32f) }, .22f, 72), Lit(Gold, .62f), medal, Quaternion.FromToRotation(Vector3.up, faceDir));
            Put("Crest face", t, HubShapes.Disc(72), Lit(Royal, .45f), medal + faceDir * .325f, Quaternion.FromToRotation(Vector3.up, faceDir), new Vector3(1.38f, 1, 1.38f));
            FlatIcon(t, "crest", medal + faceDir * .33f, 2.5f, C("FFF4DA"), Mathf.Atan2(-faceDir.x, -faceDir.z) * Mathf.Rad2Deg, true);
            Put("Crest post", t, Lathe("crest post", new[] { new Vector2(0, 0), new Vector2(.28f, 0), new Vector2(.22f, 1.0f), new Vector2(0, 1.0f) }, .05f, 24), Lit(Royal, .4f), stand - Vector3.up * .1f);
            Collider(t, HubKit.Drum(1.85f, 1f, 32), crestAt);
            // lamp globes along the back wall and the front
            for (int i = 0; i < 12; i++)
            {
                float a = (i + .5f) * Mathf.PI * 2 / 12; var d = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a));
                if (Mathf.Abs(Mathf.DeltaAngle(a * Mathf.Rad2Deg, 0)) < 16) continue;
                Lamp(t, d * (HubLayout.PlazaRadius - .25f));
            }
            // trees around the outside of the plaza
            foreach (var (az, r, s, seed) in new[] { (-140f, 18f, 1.5f, 21), (-108f, 21.5f, 1.45f, 22), (-58f, 21f, 1.7f, 23), (-30f, 22.5f, 1.55f, 25),
                                                       (30f, 22.5f, 1.55f, 26), (58f, 21f, 1.7f, 28), (108f, 21.5f, 1.45f, 29), (140f, 18f, 1.5f, 30), (178f, 18.5f, 1.3f, 33) })
            {
                var d = Dir(az, 0); var at = d * r; at.y = Mathf.Lerp(-.08f, -.55f, Mathf.InverseLerp(14.75f, 24.5f, r));
                Tree(t, at, s, seed);
            }
            for (int i = 0; i < 22; i++)
            {
                float a = i * 2.39996f; float r = 15.6f + (i * 7 % 8);
                var at = new Vector3(Mathf.Sin(a) * r, 0, Mathf.Cos(a) * r); at.y = Mathf.Lerp(-.08f, -.55f, Mathf.InverseLerp(14.75f, 24.5f, r)) + .1f;
                Bush(t, at, .7f + (i % 4) * .15f, 100 + i);
            }
        }

        static void Planter(Transform t, Vector3 at, float s, int seed, bool bushes = true)
        {
            var mesh = Lathe("planter", new[] { new Vector2(0, 0), new Vector2(.95f, 0), new Vector2(1.0f, .55f), new Vector2(1.1f, .6f), new Vector2(1.1f, .72f), new Vector2(.92f, .72f), new Vector2(.92f, .62f), new Vector2(0, .62f) }, .06f, 48);
            Put("Planter", t, mesh, Lit(Cream, .3f), at, null, Vector3.one * s);
            Put("Soil", t, HubShapes.Disc(32), Lit(C("6B5446"), .05f), at + Vector3.up * .63f * s, null, new Vector3(.92f * s, 1, .92f * s), false);
            Collider(t, HubKit.Drum(1.1f * s, .72f * s, 24), at);
            if (!bushes) return;
            Bush(t, at + Vector3.up * .8f * s, .85f * s, seed);
            Bush(t, at + Vector3.up * .75f * s + new Vector3(.45f, 0, .2f) * s, .55f * s, seed + 50);
            Bush(t, at + Vector3.up * .75f * s + new Vector3(-.4f, 0, -.25f) * s, .5f * s, seed + 90);
        }

        static void Bush(Transform t, Vector3 at, float s, int seed)
        {
            var tint = Color.Lerp(Leaf, LeafDark, (seed * 37 % 100) / 260f);
            Put("Bush", t, HubShapes.Crown(seed, 3, .14f), Lit(tint, .18f), at, Quaternion.Euler(0, seed * 47, 0), new Vector3(s, s * .72f, s));
        }

        static void Tree(Transform t, Vector3 at, float s, int seed)
        {
            Put("Trunk", t, Lathe("trunk", new[] { new Vector2(0, 0), new Vector2(.2f, 0), new Vector2(.13f, 2.6f), new Vector2(0, 2.6f) }, .05f, 12), Lit(Trunk, .15f), at, Quaternion.Euler(0, seed * 13, 0), Vector3.one * s);
            var tint = Color.Lerp(Leaf, LeafDark, (seed * 53 % 100) / 200f);
            Put("Crown", t, HubShapes.Crown(seed + 7, 4, .16f), Lit(tint, .16f), at + Vector3.up * 3.1f * s, Quaternion.Euler(0, seed * 31, 0), new Vector3(1.35f, 1.15f, 1.35f) * s);
        }

        static void Lamp(Transform t, Vector3 at)
        {
            Put("Lamp post", t, Lathe("lamp post", new[] { new Vector2(0, 0), new Vector2(.16f, 0), new Vector2(.16f, .12f), new Vector2(.07f, .2f), new Vector2(.06f, 1.25f), new Vector2(.11f, 1.32f), new Vector2(0, 1.36f) }, .03f, 16), Lit(Bronze, .5f), at);
            Put("Lamp globe", t, HubKit.Ball(2), Glow(LampGlow), at + Vector3.up * 1.56f, null, Vector3.one * .25f, false);
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
    }
}
