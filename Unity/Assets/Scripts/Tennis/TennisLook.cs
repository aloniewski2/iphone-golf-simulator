using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace GolfArcade.Tennis
{
    /// Everything about how the tennis scene is lit and shaded, in one place.
    public static class TennisLook
    {
        static Shader character, blob;
        static Texture2D falloff;

        static Shader Character => character ? character : character = Resources.Load<Shader>("Tennis/Shaders/TennisCharacter");
        static Shader Blob => blob ? blob : blob = Resources.Load<Shader>("Tennis/Shaders/TennisShadowBlob");

        /// Surface response per kind of material, picked from the authored material name.
        /// Skin wraps light softly and has a modest sheen; cloth is matte; shoes and hair
        /// carry a little more gloss.
        public struct Surface { public float Smoothness, Wrap, Rim; }
        public static Surface SurfaceFor(string materialName)
        {
            string n = materialName.ToLowerInvariant();
            if (n.Contains("skin") || n.Contains("hand") || n.Contains("face")) return new Surface { Smoothness = .38f, Wrap = .5f, Rim = .26f };
            if (n.Contains("hair") || n.Contains("brow")) return new Surface { Smoothness = .5f, Wrap = .3f, Rim = .3f };
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
                    if (!cache.TryGetValue(original, out var converted))
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
                        }
                        else converted = GolfArcade.Course.HoleView.Mat(color);
                        cache[original] = converted;
                    }
                    materials[i] = converted;
                }
                renderer.sharedMaterials = materials;
                if (renderer is SkinnedMeshRenderer skinned) skinned.updateWhenOffscreen = true;
            }
        }

        /// Light the court. One sun and a flat grey ambient left the players looking pasted
        /// on; a sky/horizon/ground ambient and a cool fill from the opposite side give them
        /// shape. The fill is a vertex light, so it costs no extra per-pixel pass.
        public static void LightScene(Light sun)
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.62f, .74f, .86f);
            RenderSettings.ambientEquatorColor = new Color(.66f, .66f, .60f);
            RenderSettings.ambientGroundColor = new Color(.40f, .44f, .34f);
            sun.color = new Color(1f, .95f, .86f);
            sun.intensity = 1.2f;
            sun.shadowStrength = .78f;
            sun.shadowBias = .03f; sun.shadowNormalBias = .25f;
            var fill = new GameObject("Resort fill light").AddComponent<Light>();
            fill.type = LightType.Directional; fill.renderMode = LightRenderMode.ForceVertex;
            fill.color = new Color(.62f, .74f, .95f); fill.intensity = .32f; fill.shadows = LightShadows.None;
            fill.transform.rotation = Quaternion.Euler(24, 150, 0);
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

    /// The camera's final grade. Skipped on the lowest tier, where every millisecond goes to
    /// holding the frame rate.
    [RequireComponent(typeof(Camera))]
    public sealed class TennisGrade : MonoBehaviour
    {
        Material material;
        public float Exposure = 1f, Saturation = 1.14f, Contrast = 1.1f, Vignette = .5f, Shoulder = 1f;

        void OnEnable()
        {
            var shader = Resources.Load<Shader>("Tennis/Shaders/TennisGrade");
            if (!shader || !shader.isSupported || TennisQuality.Current == TennisQuality.Tier.Low) { enabled = false; return; }
            material = new Material(shader);
        }

        void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (!material) { Graphics.Blit(source, destination); return; }
            material.SetFloat("_Exposure", Exposure);
            material.SetFloat("_Saturation", Saturation);
            material.SetFloat("_Contrast", Contrast);
            material.SetFloat("_Vignette", Vignette);
            material.SetFloat("_Shoulder", Shoulder);
            material.SetVector("_Lift", new Vector4(.004f, 0, -.006f, 0));
            material.SetVector("_Gain", new Vector4(1.02f, 1.0f, .96f, 1));
            Graphics.Blit(source, destination, material);
        }

        void OnDisable() { if (material) Destroy(material); }
    }
}
