using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace GolfArcade.Tennis
{
    /// Everything about how the tennis scene is lit and shaded, in one place.
    public static class TennisLook
    {
        static Shader character, blob, face;
        static Texture2D faceAtlas;
        static Texture2D falloff;

        static Shader Character => character ? character : character = Resources.Load<Shader>("Tennis/Shaders/TennisCharacter");
        static Shader Blob => blob ? blob : blob = Resources.Load<Shader>("Tennis/Shaders/TennisShadowBlob");

        /// Surface response per kind of material, picked from the authored material name.
        /// Skin wraps light softly and has a modest sheen; cloth is matte; shoes and hair
        /// carry a little more gloss.
        /// The island terrain: its Higgsfield colour and normal maps on URP Lit.
        public static void StyleIsland(GameObject island)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Tropical island" };
            var color = Resources.Load<Texture2D>("Tennis/Island/Island_Color");
            var normal = Resources.Load<Texture2D>("Tennis/Island/Island_Normal");
            if (color) mat.SetTexture("_BaseMap", color);
            if (normal) { mat.SetTexture("_BumpMap", normal); mat.EnableKeyword("_NORMALMAP"); }
            mat.SetFloat("_Smoothness", .12f);
            foreach (var r in island.GetComponentsInChildren<Renderer>())
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                r.sharedMaterials = mats;
            }
        }

        static Material invisible;
        /// A material that draws nothing in any pass: hides a submesh of a combined mesh.
        static Material Invisible
        {
            get
            {
                if (invisible) return invisible;
                invisible = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "Hidden placeholder" };
                foreach (var pass in new[] { "UniversalForward", "UniversalForwardOnly", "SRPDefaultUnlit", "ShadowCaster", "DepthOnly",
                                             "DepthNormals", "DepthNormalsOnly", "MotionVectors", "Universal2D", "Meta", "UniversalGBuffer" })
                    invisible.SetShaderPassEnabled(pass, false);
                return invisible;
            }
        }

        public struct Surface { public float Smoothness, Wrap, Rim; public Color Subsurface; }
        public static Surface SurfaceFor(string materialName)
        {
            string n = materialName.ToLowerInvariant();
            if (n.Contains("skin") || n.Contains("hand") || n.Contains("face")) return new Surface { Smoothness = .32f, Wrap = .55f, Rim = .24f, Subsurface = new Color(.55f, .12f, .05f) };
            if (n.Contains("hair") || n.Contains("brow")) return new Surface { Smoothness = .55f, Wrap = .35f, Rim = .38f, Subsurface = new Color(.25f, .12f, .02f) };
            if (n.Contains("shoe") || n.Contains("sole") || n.Contains("sneaker")) return new Surface { Smoothness = .42f, Wrap = .15f, Rim = .18f };
            if (n.Contains("eye")) return new Surface { Smoothness = .85f, Wrap = .1f, Rim = 0 };
            if (n.Contains("racket") || n.Contains("frame")) return new Surface { Smoothness = .65f, Wrap = .1f, Rim = .2f };
            return new Surface { Smoothness = .14f, Wrap = .32f, Rim = .2f };
        }

        /// Re-shade a character. This used to rebuild every material as a flat Standard
        /// material from its colour alone -- throwing away any texture and giving skin, cloth
        /// and shoes the same 10%-gloss plastic finish. Materials are now per character, so
        /// recolouring one crowd member no longer recolours everyone sharing that colour.
        public static void PrepareCharacter(GameObject obj, Color? skin = null)
        {
            var shader = Character;
            var cache = new Dictionary<Material, Material>();
            foreach (var renderer in obj.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    var original = materials[i]; if (!original) continue;
                    if (!cache.TryGetValue(original, out var converted) && original.name.StartsWith("V4 face decal"))
                    {
                        // The painted face: an expression atlas cell (see TennisActor.Expression).
                        face ??= Resources.Load<Shader>("Tennis/Shaders/TennisFace");
                        if (!faceAtlas) faceAtlas = Resources.Load<Texture2D>("Tennis/Characters/FaceAtlas");
                        converted = new Material(face) { name = "Face (tennis)", mainTexture = faceAtlas };
                        cache[original] = converted;
                    }
                    if (!cache.TryGetValue(original, out converted) && original.name.StartsWith("V4 Higgs "))
                    {
                        // Higgsfield models (players, umpire, chair), fitted in Blender: colour and
                        // normal maps baked alongside, named by the material's last word.
                        string n = original.name;
                        string who = n.Substring(n.LastIndexOf(' ') + 1).Split('.')[0];
                        converted = new Material(shader) { name = "Higgs " + who + " (tennis)", color = Color.white };
                        converted.mainTexture = Resources.Load<Texture2D>("Tennis/Characters/Higgs" + who + "_Color");
                        var normal = Resources.Load<Texture2D>("Tennis/Characters/Higgs" + who + "_Normal");
                        if (normal) { converted.SetTexture("_BumpMap", normal); converted.EnableKeyword("_NORMALMAP"); }
                        // Skin and cloth share one map: a middle ground between the two surfaces.
                        converted.SetFloat("_Smoothness", .26f);
                        converted.SetFloat("_Wrap", .45f);
                        converted.SetFloat("_RimStrength", .26f);
                        converted.SetColor("_Subsurface", new Color(.18f, .06f, .03f));
                        cache[original] = converted;
                    }
                    if (!cache.TryGetValue(original, out converted))
                    {
                        Color color = original.HasProperty("_Color") ? original.color : Color.white;
                        if (skin.HasValue && original.name.StartsWith("V4 skin")) color = skin.Value;
                        if (shader)
                        {
                            var surface = SurfaceFor(original.name);
                            converted = new Material(shader) { name = original.name + " (tennis)", color = color };
                            if (original.HasProperty("_MainTex") && original.mainTexture) converted.mainTexture = original.mainTexture;
                            converted.SetFloat("_Smoothness", surface.Smoothness);
                            converted.SetFloat("_Wrap", surface.Wrap);
                            converted.SetFloat("_RimStrength", surface.Rim);
                            converted.SetColor("_Subsurface", surface.Subsurface);
                        }
                        else converted = GolfArcade.Course.HoleView.Mat(color);
                        cache[original] = converted;
                    }
                    materials[i] = converted;
                }
                renderer.sharedMaterials = materials;
                if (renderer is SkinnedMeshRenderer skinned) skinned.updateWhenOffscreen = true;
                // Faces and hair are thin shells on the head; they must not cast their own
                // shadow onto it.
                if (renderer.name.StartsWith("V4 face decal")) renderer.shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        static Shader lit;
        /// URP Lit, for runtime materials (the ball, props).
        public static Material Lit(Color color, Texture texture = null, float smoothness = .2f)
        {
            if (!lit) lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var m = new Material(lit) { color = color };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (texture) { m.mainTexture = texture; if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", texture); }
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            return m;
        }

        /// Post-processing: the profile authored by UrpSetup (ACES, bloom, warm grade,
        /// vignette; depth of field for replays). Off on the lowest tier.
        public static Volume SetupPost(Camera camera)
        {
            var data = camera.GetUniversalAdditionalCameraData();
            bool on = TennisQuality.Current != TennisQuality.Tier.Low;
            data.renderPostProcessing = on;
            data.antialiasing = AntialiasingMode.None; // MSAA comes from the pipeline asset
            data.renderShadows = true;
            var profile = Resources.Load<VolumeProfile>("Tennis/Rendering/TennisPost");
            if (!profile) return null;
            var go = new GameObject("Tennis post-processing");
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true; volume.priority = 10;
            // A private copy, so replay depth of field never edits the shared asset.
            volume.profile = Object.Instantiate(profile);
            return volume;
        }

        /// Depth of field for slow-motion replays, focused on the ball.
        public static void ReplayFocus(Volume volume, bool on, float distance)
        {
            if (!volume || !volume.profile.TryGet(out DepthOfField dof)) return;
            dof.active = on;
            dof.gaussianStart.Override(Mathf.Max(1, distance + 2)); dof.gaussianEnd.Override(distance + 14);
        }

        /// Where the sun sits: low over the left of the far court, late afternoon, so shadows
        /// are long and the players are rim-lit toward the camera (art target A).
        public static readonly Vector3 SunDirection = new Vector3(-.62f, .42f, .66f).normalized;

        /// Light the court for URP in linear colour: a warm golden-hour sun, a sky/horizon/
        /// ground ambient, a light distance haze, and the painted sky.
        public static void LightScene(Light sun)
        {
            sun.transform.rotation = Quaternion.LookRotation(-SunDirection);
            sun.color = new Color(1f, .87f, .70f);
            sun.intensity = 2.3f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = .82f;
            sun.shadowBias = .05f; sun.shadowNormalBias = .4f;
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.42f, .58f, .86f);
            RenderSettings.ambientEquatorColor = new Color(.66f, .58f, .50f);
            RenderSettings.ambientGroundColor = new Color(.24f, .26f, .22f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(.80f, .84f, .90f);
            RenderSettings.fogStartDistance = 140; RenderSettings.fogEndDistance = 900;
            var skyShader = Resources.Load<Shader>("Tennis/Shaders/TennisSky");
            var panorama = Resources.Load<Texture2D>("Tennis/Environment/SkyPanorama");
            if (skyShader)
            {
                var sky = new Material(skyShader) { name = "Painted sky" };
                if (panorama) sky.SetTexture("_Panorama", panorama);
                sky.SetVector("_SunDir", SunDirection);
                RenderSettings.skybox = sky;
            }
        }

        /// Re-surface the imported arena by what each material is (the Blender names travel
        /// with the export in materials.json): animated sea, a richer court, turquoise runoff.
        public static void StyleArena(GameObject arena)
        {
            var water = Resources.Load<Shader>("Tennis/Shaders/TennisWater");
            Material sea = water ? new Material(water) { name = "Resort sea" } : null;
            var cache = new Dictionary<Material, Material>();
            foreach (var r in arena.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i]; if (!m) continue;
                    if (cache.TryGetValue(m, out var done)) { mats[i] = done; changed = true; continue; }
                    Material result = null;
                    switch (m.name.Replace(" (Instance)", ""))
                    {
                        case "TropicalV3_001": result = sea; break;                                     // turquoise water
                        case "TropicalV3_002": result = Tinted(m, new Color(.015f, .19f, .62f), .1f); break; // court
                        case "TropicalV3_011": result = Tinted(m, new Color(.02f, .34f, .34f), .22f); break;  // runoff
                        case "TropicalV3_003": result = Tinted(m, new Color(.95f, .96f, .93f), .25f); break;  // lines
                        case "TropicalV3_010": result = Tinted(m, new Color(.78f, .70f, .56f), .15f); break;  // limestone
                        case "TropicalV3_009": result = Tinted(m, new Color(.12f, .36f, .07f), .1f); break;   // turf
                        // Placeholder blob spectators: the real seated crowd sits there instead
                        // (TennisStandsCrowd).
                        // ...and the flat rectangular sand slab off the sea side, which read as an
                        // unfinished box from the air: the terrace meets the sea at its seawall.
                        case "TropicalV3_000":
                        case "TropicalV3_004": case "TropicalV3_005": case "TropicalV3_006": case "TropicalV3_007":
                            result = Invisible; break;
                    }
                    if (!result) continue;
                    cache[m] = result; mats[i] = result; changed = true;
                }
                if (changed) r.sharedMaterials = mats;
            }
        }

        static Material Tinted(Material source, Color color, float smoothness)
        {
            var m = new Material(source) { name = source.name + " (styled)" };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color); else m.color = color;
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            return m;
        }

        /// Radial falloff shared by every contact shadow.
        public static Texture2D Falloff
        {
            get
            {
                if (falloff) return falloff;
                const int size = 64;
                falloff = new Texture2D(size, size, TextureFormat.Alpha8, false) { wrapMode = TextureWrapMode.Clamp, name = "Contact shadow falloff" };
                var pixels = new Color32[size * size];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float dx = (x + .5f) / size * 2 - 1, dy = (y + .5f) / size * 2 - 1;
                        float r = Mathf.Sqrt(dx * dx + dy * dy);
                        float a = Mathf.Clamp01(1 - r); a = a * a * (3 - 2 * a);
                        pixels[y * size + x] = new Color32(0, 0, 0, (byte)(a * 255));
                    }
                falloff.SetPixels32(pixels); falloff.Apply(false, true);
                return falloff;
            }
        }

        public static ContactShadow AddContactShadow(Transform follow, float radius, float strength)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Contact shadow — " + follow.name;
            Object.Destroy(quad.GetComponent<Collider>());
            quad.transform.rotation = Quaternion.Euler(90, 0, 0);
            var renderer = quad.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            var material = new Material(Blob ? Blob : Shader.Find("Sprites/Default")) { mainTexture = Falloff };
            if (material.HasProperty("_Strength")) material.SetFloat("_Strength", strength);
            renderer.sharedMaterial = material;
            var shadow = quad.AddComponent<ContactShadow>();
            shadow.Follow = follow; shadow.Radius = radius; shadow.Strength = strength; shadow.Material = material;
            return shadow;
        }
    }

    /// A soft occlusion disc on the court under a player or the ball. It shrinks and fades as
    /// the thing above it rises, which is also the best cue for how high the ball is.
    public sealed class ContactShadow : MonoBehaviour
    {
        public Transform Follow;
        public float Radius = .5f, Strength = .5f, Ground = .012f, FadeHeight = 3f;
        public Material Material;
        public float HeightOverride = -1;

        void LateUpdate()
        {
            if (!Follow) { gameObject.SetActive(false); return; }
            Vector3 p = Follow.position;
            float height = Mathf.Max(0, HeightOverride >= 0 ? HeightOverride : p.y);
            float fade = Mathf.Clamp01(1 - height / FadeHeight);
            transform.position = new Vector3(p.x, Ground, p.z);
            transform.localScale = Vector3.one * (Radius * 2 * Mathf.Lerp(.6f, 1f, fade));
            if (Material && Material.HasProperty("_Strength")) Material.SetFloat("_Strength", Strength * fade);
        }

        void OnDestroy() { if (Material) Destroy(Material); }
    }

}
