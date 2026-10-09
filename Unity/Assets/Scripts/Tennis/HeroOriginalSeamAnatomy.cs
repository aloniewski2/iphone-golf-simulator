using System;
using System.Linq;
using UnityEngine;

namespace GolfArcade.Tennis
{
    /// Original-model surface and eyelid finish. Explicit review flags retain prior controls.
    public static class HeroOriginalSeamAnatomy
    {
        public static bool Enabled => Environment.GetEnvironmentVariable("VISUAL_ORIGINAL_SEAM") == "1" ||
            (Environment.GetEnvironmentVariable("VISUAL_ORIGINAL_SEAM") != "0" && Environment.GetEnvironmentVariable("VISUAL_REFERENCE_HEAD") != "1");
        // Accepted eye13 treatment is the default only inside this optional
        // OriginalSeam candidate. Explicit 0 retains the matched control.
        public static bool EyeMaterialEnabled => Environment.GetEnvironmentVariable("VISUAL_ORIGINAL_EYE_MATERIAL") != "0";
        static Texture2D pigmentLookup;
        static Texture2D irisLookup, scleraLookup;
        static readonly System.Collections.Generic.Dictionary<Mesh, Mesh> blinkMeshes = new System.Collections.Generic.Dictionary<Mesh, Mesh>();

        // Unity calculates shape normals against the imported base, before our
        // continuous skin normal field. Unmoved vertices must have zero deltas
        // or a blink changes the lighting of the nose, mouth, jaw and brows.
        static Mesh PrepareBlinkMesh(Mesh source, bool features)
        {
            if (!source || source.blendShapeCount == 0) return source;
            if (blinkMeshes.TryGetValue(source, out var cached)) return cached;
            var frames = new System.Collections.Generic.List<(string name, float weight, Vector3[] positions, Vector3[] normals, Vector3[] tangents)>();
            int cleared = 0;
            for (int shape = 0; shape < source.blendShapeCount; shape++) {
                string name = source.GetBlendShapeName(shape);
                bool blink = name == "Hero_Blink" || name == "Hero_Blink_Half";
                for (int frame = 0; frame < source.GetBlendShapeFrameCount(shape); frame++) {
                    var dp = new Vector3[source.vertexCount]; var dn = new Vector3[source.vertexCount]; var dt = new Vector3[source.vertexCount];
                    source.GetBlendShapeFrameVertices(shape, frame, dp, dn, dt);
                    if (blink) for (int i = 0; i < source.vertexCount; i++) {
                        if (features || dp[i].sqrMagnitude < 1e-12f || Environment.GetEnvironmentVariable("VISUAL_BLINK_FLAT_NORMALS")=="1") { if (dn[i].sqrMagnitude > 1e-12f) cleared++; dn[i] = Vector3.zero; dt[i] = Vector3.zero; }
                        // Eye parts only translate backwards; deeper final
                        // recession prevents their peripheral sheets poking
                        // through the skinned closed boundary in Ready poses.
                        if (features && name == "Hero_Blink") dp[i] *= 3;
                    }
                    frames.Add((name, source.GetBlendShapeFrameWeight(shape, frame), dp, dn, dt));
                }
            }
            var mesh = UnityEngine.Object.Instantiate(source); mesh.name = source.name + " (local blink normals)"; mesh.ClearBlendShapes();
            foreach (var frame in frames) mesh.AddBlendShapeFrame(frame.name, frame.weight, frame.positions, frame.normals, frame.tangents);
            blinkMeshes[source] = mesh;
            Debug.Log("[HeroOriginalSeam] blink normal isolation " + (features ? "features" : "body") + ": cleared=" + cleared + "; base geometry/normals unchanged");
            return mesh;
        }

        public static bool Apply(MatchHeroLook hero)
        {
            if (!Enabled || !hero.body) return false;
            string sourceFolder = Environment.GetEnvironmentVariable("VISUAL_ORIGINAL_BLINK") != "0"
                ? "Tennis/OriginalSeamBlinkFinish/"
                : EyeMaterialEnabled ? "Tennis/OriginalSeamEyeMaterial/" : "Tennis/OriginalSeam/";
            var source = Resources.Load<GameObject>(sourceFolder + (hero.female ? "Female" : "Male"));
            if (!source) { Debug.LogError("[HeroOriginalSeam] missing optional resource"); return false; }
            var renderers = source.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var skin = renderers.FirstOrDefault(r => r.name == "Body");
            var features = renderers.FirstOrDefault(r => r.name == "ReferenceFeatures");
            if (!skin || !features || !skin.sharedMesh || !features.sharedMesh) {
                Debug.LogError("[HeroOriginalSeam] Body/ReferenceFeatures contract missing"); return false;
            }
            float ratio = skin.sharedMesh.bounds.size.magnitude / Mathf.Max(.001f, hero.body.sharedMesh.bounds.size.magnitude);
            if (ratio < .5f || ratio > 2f) { Debug.LogError("[HeroOriginalSeam] canonical units mismatch"); return false; }
            var map = hero.GetComponentsInChildren<Transform>(true).GroupBy(t => t.name).ToDictionary(g => g.Key, g => g.First());
            bool CanMap(SkinnedMeshRenderer r) => r.bones.All(b => b && map.ContainsKey(b.name));
            if (!CanMap(skin) || !CanMap(features)) { Debug.LogError("[HeroOriginalSeam] canonical bone contract mismatch"); return false; }
            var oldFace = hero.face;
            var headName = hero.Bone(HumanBodyBones.Head).name;
            var fittedBody = HeroFaceReferenceFit.Prepare(skin, skin, features, headName, hero.female);
            var fittedFeatures = HeroFaceReferenceFit.Prepare(features, skin, features, headName, hero.female);
            hero.body.sharedMesh = PrepareBlinkMesh(fittedBody, false); hero.body.sharedMaterials = skin.sharedMaterials;
            hero.body.bones = skin.bones.Select(b => map[b.name]).ToArray();
            if (skin.rootBone && map.TryGetValue(skin.rootBone.name, out var root)) hero.body.rootBone = root;
            hero.body.localBounds = skin.localBounds;
            var node = new GameObject("OriginalSeamFeatures"); node.transform.SetParent(hero.body.transform, false);
            var relative = skin.transform.worldToLocalMatrix * features.transform.localToWorldMatrix;
            node.transform.localPosition = relative.GetColumn(3); node.transform.localRotation = relative.rotation; node.transform.localScale = relative.lossyScale;
            var live = node.AddComponent<SkinnedMeshRenderer>(); live.sharedMesh = PrepareBlinkMesh(fittedFeatures, true); live.sharedMaterials = features.sharedMaterials;
            live.bones = features.bones.Select(b => map[b.name]).ToArray();
            if (features.rootBone && map.TryGetValue(features.rootBone.name, out var featureRoot)) live.rootBone = featureRoot;
            live.localBounds = features.localBounds; live.updateWhenOffscreen = true;
            if (oldFace) oldFace.enabled = false;
            hero.face = live;
            Debug.Log("[HeroOriginalSeam] installed " + (hero.female ? "Female" : "Male") + " body=" + skin.sharedMesh.vertexCount + " features=" + features.sharedMesh.vertexCount + "; original rig and eye proportions retained; legacy face layers bypassed");
            return true;
        }

        /// UV0 stores continuous lip and seam/nose mask amounts, not a skin hue.
        /// Multiplication by BaseColor preserves every caller's chosen skin tone.
        public static void ConfigurePigment(Material material)
        {
            if (!material || !material.HasProperty("_BaseMap")) return;
            if (!pigmentLookup)
            {
                const int size = 64;
                pigmentLookup = new Texture2D(size, size, TextureFormat.RGBA32, false, true) {
                    name = "OriginalSeam_RelativeSkinPigment", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear
                };
                var pixels = new Color[size * size];
                for (int y = 0; y < size; y++) for (int x = 0; x < size; x++) {
                    float lip = x / (float)(size - 1), dark = y / (float)(size - 1);
                    var c = Vector3.Lerp(Vector3.one, new Vector3(.93f, .66f, .68f), lip);
                    c = Vector3.Lerp(c, new Vector3(.64f, .49f, .48f), dark);
                    pixels[y * size + x] = new Color(c.x, c.y, c.z, 1);
                }
                // Tiny readable LUT also remains exportable for native parity.
                pigmentLookup.SetPixels(pixels); pigmentLookup.Apply(false, false);
            }
            material.SetTexture("_BaseMap", pigmentLookup); material.SetTextureScale("_BaseMap", Vector2.one); material.SetTextureOffset("_BaseMap", Vector2.zero);
        }

        /// Source Blender albedos were linear; imported diffuse values are used
        /// as Unity colour properties. Explicit sRGB eye colours preserve the
        /// warm brown source appearance instead of decoding those values twice.
        public static void ConfigureFeature(Material material, string sourceName)
        {
            if (!material || !material.HasProperty("_BaseColor")) return;
            Color c;
            if (sourceName.Contains("Catch")) c = new Color(.98f, .98f, .96f, 1);
            else if (sourceName.Contains("Sclera")) c = new Color(.94f, .945f, .915f, 1);
            else if (sourceName.Contains("Pupil")) c = new Color(.028f, .023f, .020f, 1);
            else if (sourceName.Contains("Limbal")) c = new Color(.16f, .10f, .055f, 1);
            else if (sourceName.Contains("Iris")) c = EyeMaterialEnabled ? new Color(.48f, .29f, .13f, 1) : new Color(.48f, .305f, .14f, 1);
            else if (sourceName.Contains("Brow")) c = new Color(.18f, .135f, .105f, 1);
            else return;
            material.SetColor("_BaseColor", c);
            if (material.HasProperty("_Color")) material.SetColor("_Color", c);
            if (!EyeMaterialEnabled) return;
            if (sourceName.Contains("Iris") || sourceName.Contains("Sclera")) {
                bool iris = sourceName.Contains("Iris");
                material.SetTexture("_BaseMap", EyeLookup(iris));
                material.SetTextureScale("_BaseMap", Vector2.one); material.SetTextureOffset("_BaseMap", Vector2.zero);
                material.SetFloat("_Smoothness", iris ? .90f : .82f);
                bool polish = Environment.GetEnvironmentVariable("VISUAL_CHARACTER_SKIN_POLISH") == "1";
                material.SetFloat("_SpecularStrength", polish ? (iris ? .85f : .52f) : (iris ? .70f : .45f));
                material.SetFloat("_EnvironmentStrength", iris ? .5f : .32f);
            }
            else if (sourceName.Contains("Brow")) {
                material.SetFloat("_Smoothness", .12f); material.SetFloat("_SpecularStrength", .10f);
                material.SetFloat("_EnvironmentStrength", 0);
            }
        }

        // Relative albedo, sampled in eye-local UVs authored on the unchanged
        // feature mesh. A broad iris gradient gives depth without starburst or
        // extra eye geometry; the white keeps a softer upper/outer transition.
        static Texture2D EyeLookup(bool iris)
        {
            var existing = iris ? irisLookup : scleraLookup; if (existing) return existing;
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true) {
                name = iris ? "OriginalEye_RelativeIris" : "OriginalEye_RelativeSclera",
                wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear
            };
            var colors = new Color[size * size];
            float Smooth(float lo, float hi, float value) { float t = Mathf.Clamp01((value - lo) / (hi - lo)); return t * t * (3 - 2 * t); }
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++) {
                float u = (x + .5f) / size, v = (y + .5f) / size;
                Color color;
                if (iris) {
                    float r = new Vector2(2 * u - 1, 2 * v - 1).magnitude;
                    float middle = Mathf.Exp(-Mathf.Pow((r - .71f) / .22f, 2));
                    float a = .68f + .32f * middle - .16f * Smooth(.86f, 1, r);
                    color = new Color(a, a * (.91f + .09f * middle), a * (.82f + .18f * middle), 1);
                }
                else {
                    float edge = Smooth(.62f, 1, Mathf.Abs(2 * u - 1)), upper = Smooth(.40f, 1, v);
                    color = new Color(1 - .12f * edge - .12f * upper, 1 - .15f * edge - .13f * upper, 1 - .18f * edge - .14f * upper, 1);
                    float upperEdge=Smooth(.50f,.66f,v);
                    color=Color.Lerp(color,new Color(.17f,.105f,.07f,1),upperEdge*.94f);
                }
                colors[y * size + x] = color;
            }
            texture.SetPixels(colors); texture.Apply(false, false);
            if (iris) irisLookup = texture; else scleraLookup = texture;
            return texture;
        }
    }
}
