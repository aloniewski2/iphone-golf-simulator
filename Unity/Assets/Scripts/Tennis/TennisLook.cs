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
        static Material resortSky;

        static Shader Character => character ? character : character = Resources.Load<Shader>("Tennis/Shaders/TennisCharacter");
        static Shader Blob => blob ? blob : blob = Resources.Load<Shader>("Tennis/Shaders/TennisShadowBlob");

        /// Surface response per kind of material, picked from the authored material name.
        /// Skin wraps light softly and has a modest sheen; cloth is matte; shoes and hair
        /// carry a little more gloss.
        /// The island terrain: its Higgsfield colour and normal maps on URP Lit.
        public static void StyleIsland(GameObject island)
        {
            // The replacement coast owns its own material roles; avoid loading the
            // obsolete scan atlas / normal map for an invisible fallback island.
            if (Resources.Load<GameObject>("Tennis/Premium/TennisCoast")) return;
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
            if (n.Contains("skin") || n.Contains("hand") || n.Contains("face")) return new Surface { Smoothness = .28f, Wrap = .32f, Rim = .075f, Subsurface = new Color(.12f, .035f, .02f) };
            if (n.Contains("hair") || n.Contains("brow")) return new Surface { Smoothness = .32f, Wrap = .25f, Rim = .055f, Subsurface = new Color(.25f, .12f, .02f) };
            if (n.Contains("shoe") || n.Contains("sole") || n.Contains("sneaker")) return new Surface { Smoothness = .28f, Wrap = .22f, Rim = .045f };
            if (n.Contains("eye")) return new Surface { Smoothness = .85f, Wrap = .1f, Rim = 0 };
            if (n.Contains("racket") || n.Contains("frame")) return new Surface { Smoothness = .72f, Wrap = .18f, Rim = .075f };
            return new Surface { Smoothness = .18f, Wrap = .30f, Rim = .055f };
        }

        /// Re-shade a character. This used to rebuild every material as a flat Standard
        /// material from its colour alone -- throwing away any texture and giving skin, cloth
        /// and shoes the same 10%-gloss plastic finish. Materials are now per character, so
        /// recolouring one crowd member no longer recolours everyone sharing that colour.
        /// The player's outfit colours from the character screen. A colour with alpha 0 keeps
        /// the kit's own; Skin is a GolferStyle skin-tone index (2, the kit's own tan, is left as is).
        public struct Kit
        {
            public Color Shirt, Shorts, Accent, Racket; public int Skin; public bool HasSkin;
            public bool Any => Shirt.a > 0 || Shorts.a > 0 || Accent.a > 0 || Racket.a > 0 || HasSkin || Skin != 2;
            static Color Parse(string hex) =>
                !string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString("#" + hex, out var c) ? c : new Color(1, 1, 1, 0);
            public static Kit From(string shirt, string shorts, string accent, string racket, int skin) => new Kit
            { Shirt = Parse(shirt), Shorts = Parse(shorts), Accent = Parse(accent), Racket = Parse(racket), Skin = skin, HasSkin = true };
        }

        static Material recolor;

        /// Re-tint a player body's kit texture (Higgs<who>_Color) with the outfit colours, using
        /// its region mask (Higgs<who>_Mask, from blender/scripts/bake_kit_mask.py). Returns the
        /// new texture, or null when there is nothing to change or no mask for this body.
        public static Texture RecolorKit(string who, Kit kit)
        {
            if (!kit.Any) return null;
            var source = Resources.Load<Texture2D>("Tennis/Characters/Higgs" + who + "_Color");
            var mask = Resources.Load<Texture2D>("Tennis/Characters/Higgs" + who + "_Mask");
            if (!source || !mask) return null;
            if (!recolor)
            {
                var shader = Resources.Load<Shader>("Tennis/Shaders/KitRecolor");
                if (!shader || !shader.isSupported) return null;
                recolor = new Material(shader) { name = "Kit recolour" };
            }
            var reference = new Vector4(.08f, .08f, .28f, .27f);
            var json = Resources.Load<TextAsset>("Tennis/Characters/Higgs" + who + "_Mask");
            if (json)
            {
                var r = JsonUtility.FromJson<MaskReference>(json.text);
                reference = new Vector4(r.shirt, r.shorts, r.accent, r.skin);
            }
            recolor.SetTexture("_Mask", mask);
            recolor.SetVector("_Ref", reference);
            recolor.SetColor("_Shirt", kit.Shirt);
            recolor.SetColor("_Shorts", kit.Shorts);
            recolor.SetColor("_Accent", kit.Accent);
            var skin = (kit.HasSkin || kit.Skin != 2) ? GolfArcade.Game.GolferStyle.SkinTones[Mathf.Clamp(kit.Skin, 0, GolfArcade.Game.GolferStyle.SkinTones.Length - 1)] : new Color(1, 1, 1, 0);
            skin.a = (kit.HasSkin || kit.Skin != 2) ? 1 : 0;
            recolor.SetColor("_Skin", skin);
            var target = new RenderTexture(source.width, source.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
            { name = "Kit " + who + " (recoloured)", useMipMap = true, autoGenerateMips = true, anisoLevel = 2, wrapMode = source.wrapMode };
            Graphics.Blit(source, target, recolor);
            return target;
        }

        [System.Serializable] struct MaskReference { public float shirt, shorts, accent, skin; }

        static readonly Dictionary<string,Texture> crowdPalettes=new();
        /// Six coherent existing-kit palettes. Authored atlas shading remains intact and
        /// the masks deliberately exclude all skin/hair pixels, so identity is preserved.
        public static void ApplyCrowdPalette(GameObject fan,bool female,int index)
        {
            string who=female ? "Female":"Male";int pick=Mathf.Abs(index)%6;string key=who+pick;
            if(!crowdPalettes.TryGetValue(key,out var texture)){
                string[] shirts={"EEEAE0","A8C7C4","E6BF8F","B6BAD5","D5AEB6","D1D8AB"};
                string[] shorts={"334B61","385E59","46545B","3A4262","674657","4D5D43"};
                string[] accents={"E8BC5A","EFE4CF","C69C69","D3C0A0","E8C7BC","F3E3AA"};
                texture=RecolorKit(who,Kit.From(shirts[pick],shorts[pick],accents[pick],null,2));
                if(!texture)return;crowdPalettes[key]=texture;
            }
            foreach(var renderer in fan.GetComponentsInChildren<Renderer>(true))foreach(var material in renderer.sharedMaterials)
                if(material&&material.name.StartsWith("Higgs "+who+" "))material.mainTexture=texture;
        }

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
                        // A character may carry its own atlas ("V4 face decal Avatar" ->
                        // FaceAtlas_Avatar); everyone else shares the default one.
                        if (!faceAtlas) faceAtlas = Resources.Load<Texture2D>("Tennis/Characters/FaceAtlas");
                        string suffix = original.name.Length > "V4 face decal".Length ? original.name.Substring("V4 face decal".Length).Trim().Split('.')[0] : "";
                        var own = suffix.Length > 0 ? Resources.Load<Texture2D>("Tennis/Characters/FaceAtlas_" + suffix) : null;
                        converted = new Material(face) { name = "Face (tennis)", mainTexture = own ? own : faceAtlas };
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
                        // The base avatars are matched to the concept video's smooth, toy-like
                        // finish: keep a hint of form from the scan's normal map, not its wrinkles.
                        converted.SetFloat("_BumpScale", who.StartsWith("Avatar") ? .18f : .10f);
                        // Skin and cloth share one map: a middle ground between the two surfaces.
                        converted.SetFloat("_Smoothness", .18f);
                        converted.SetFloat("_Wrap", .30f);
                        converted.SetFloat("_RimStrength", .055f);
                        converted.SetFloat("_SpecularStrength", .35f); converted.SetFloat("_EnvironmentStrength", .035f);
                        converted.SetColor("_Subsurface", new Color(.08f, .022f, .012f));
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
                            converted.SetFloat("_SpecularStrength", original.name.ToLowerInvariant().Contains("eye") ? .55f : .6f);converted.SetFloat("_EnvironmentStrength", .08f);
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

        /// ACES, restrained bloom and neutral grading. Low tier retains tone mapping.
        public static Volume SetupPost(Camera camera)
        {
            var existing = camera.GetComponent<SportsPostProcessing>();
            if (existing) return existing.Volume;
            var data = camera.GetUniversalAdditionalCameraData();
            camera.allowHDR = true;
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.None; // MSAA comes from the pipeline asset
            data.renderShadows = true;
            var profile = Resources.Load<VolumeProfile>("Tennis/Rendering/TennisPost");
            if (!profile) return null;
            var go = new GameObject("Sports post-processing");
            go.transform.SetParent(camera.transform, false);
            data.volumeLayerMask = 1 << go.layer;
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true; volume.priority = 10;
            // A private copy, so replay depth of field never edits the shared asset.
            volume.sharedProfile = profile; // Volume.profile deep-clones the components on first access.
            if (volume.profile.TryGet(out Bloom bloom))
            {
                bloom.threshold.Override(1.05f); bloom.intensity.Override(.28f); bloom.tint.Override(Color.white);
                bloom.maxIterations.Override(4); // local glow with a bounded mobile blur chain
            }
            if (volume.profile.TryGet(out WhiteBalance balance))
            { balance.temperature.Override(0); balance.tint.Override(0); }
            if (volume.profile.TryGet(out ColorAdjustments grade))
            { grade.postExposure.Override(0); grade.contrast.Override(5); grade.saturation.Override(8); grade.colorFilter.Override(Color.white); }
            if (volume.profile.TryGet(out Vignette vignette)) vignette.intensity.Override(.12f);
            if (volume.profile.TryGet(out LiftGammaGain lift)) lift.lift.Override(new Vector4(1,1,1,0));
            camera.gameObject.AddComponent<SportsPostProcessing>().Initialize(volume);
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
        /// Hero V5 light recipe (ArtDir/hero/v5_proof/LIGHTING.md): key at ~40 deg elevation (art bible 35-45).
        public static readonly Vector3 SunDirection = new Vector3(-.62f, .76f, .66f).normalized;
        /// Camera-side fill (no shadows): cool, ~35% of the key, so the rival's face (turned to camera, away from
        /// the sun) and the player's back never go gray.
        public static readonly Vector3 FillDirection = new Vector3(.30f, .55f, -.78f).normalized;
        public const float SunIntensity = 1.65f, FillIntensity = .18f;
        public static readonly Color SunColor = new Color(1f, .94f, .85f), FillColor = new Color(.94f, .97f, 1f);

        /// Light the court for URP in linear colour: a warm golden-hour sun, a sky/horizon/
        /// ground ambient, a light distance haze, and the painted sky.
        public static void LightScene(Light sun)
        {
            sun.transform.rotation = Quaternion.LookRotation(-SunDirection);
            sun.color = SunColor;
            sun.intensity = SunIntensity;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = .72f;   // soft; the cool trilight ambient tints what is left
            var fillGo = GameObject.Find("Hero fill"); if (!fillGo) fillGo = new GameObject("Hero fill");
            var fill = fillGo.GetComponent<Light>(); if (!fill) fill = fillGo.AddComponent<Light>();
            fill.type = LightType.Directional; fill.shadows = LightShadows.None;
            fill.transform.rotation = Quaternion.LookRotation(-FillDirection);
            fill.color = FillColor; fill.intensity = FillIntensity;
            HeroRimLight.Ensure();   // the one warm rim light on the heroes, back toward the camera (own rendering layer, never on the court)
            sun.shadowBias = .025f; sun.shadowNormalBias = .18f;
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.36f, .50f, .70f);
            RenderSettings.ambientEquatorColor = new Color(.28f, .35f, .39f);
            RenderSettings.ambientGroundColor = new Color(.26f, .30f, .24f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(.53f, .70f, .80f);
            RenderSettings.fogStartDistance = 180; RenderSettings.fogEndDistance = 900;
            var skyShader = Resources.Load<Shader>("Tennis/Shaders/TennisCoastalSky");
            var panorama = Resources.Load<Texture2D>("Course/Resort/SkyCoastalSmall");
            if (skyShader && panorama)
            {
                if (!resortSky) resortSky = new Material(skyShader) { name = "Resort coastal sky" };
                resortSky.SetTexture("_Panorama", panorama);
                resortSky.SetFloat("_Rotation", -45); resortSky.SetFloat("_Exposure", 1);
                resortSky.SetFloat("_AirColorShare",.24f);resortSky.SetFloat("_DayHaze",.04f);
                if (resortSky.HasProperty("_CloudCompression")) resortSky.SetFloat("_CloudCompression", 1.05f);
                RenderSettings.skybox = resortSky;
            }
        }

        /// Re-surface the imported arena by what each material is (the Blender names travel
        /// with the export in materials.json): animated sea, a richer court, turquoise runoff.
        public static void StyleArena(GameObject arena)
        {
            TennisVenue.CacheRenderedCourtHeight(arena);
            var water = Resources.Load<Shader>("Tennis/Shaders/TennisWater");
            Material sea = water ? new Material(water) { name = "Resort sea" } : null;
            if (sea)
            {
                sea.SetColor("_Shallow", new Color(.035f, .51f, .56f));
                sea.SetColor("_Deep", new Color(.015f, .245f, .46f));
                sea.SetColor("_Sky", new Color(.19f, .53f, .74f));
                sea.SetFloat("_Sparkle", 1.0f);
                var shore = Resources.Load<Texture2D>("Tennis/Premium/CoastShallows");
                if (shore && sea.HasProperty("_ShoreMap"))
                {
                    sea.SetTexture("_ShoreMap",shore); sea.SetFloat("_UseShoreMap",1);
                    sea.SetVector("_ShoreBounds",new Vector4(-242.5f,-187.5f,477.5f,632.5f));
                }
                TennisResortMaterials.Sea(sea);
            }
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
                        case "TropicalV3_002": result = Tinted(m, TennisVenue.CourtColor, .12f); result.SetFloat("_CourtFinish",1); result.SetFloat("_Grain",.010f);
                            result.SetTexture("_MicroMap",Resources.Load<Texture2D>("Tennis/Premium/AcrylicAggregate"));result.SetFloat("_MicroStrength",.07f);result.SetFloat("_MicroScale",2); break; // fine acrylic court
                        case "TropicalV3_011": result = Tinted(m, TennisVenue.RunoffColor, .14f); result.SetFloat("_CourtFinish",1);
                            result.SetTexture("_MicroMap",Resources.Load<Texture2D>("Tennis/Premium/AcrylicAggregate"));result.SetFloat("_MicroStrength",.06f);result.SetFloat("_MicroScale",2);break;  // runoff
                        case "TropicalV3_003": result = Tinted(m, TennisVenue.LineColor, .25f); break;  // lines
                        case "TropicalV3_010": result = Tinted(m, new Color(.79f, .74f, .64f), .27f); result.SetFloat("_Paving",1);
                            result.SetTexture("_MicroMap",Resources.Load<Texture2D>("Tennis/Premium/AcrylicAggregate"));result.SetFloat("_MicroStrength",.10f);result.SetFloat("_MicroScale",.5f);break;  // limestone
                        case "TropicalV3_009": result = Tinted(m, new Color(.13f, .30f, .10f), .1f); break;   // turf
                        case "TropicalV3_008": result = Tinted(m, new Color(.035f, .11f, .105f), .48f); break; // enamel posts
                        case "TropicalV3_012": result = Tinted(m, new Color(.022f, .033f, .031f), .08f); break; // braided net
                        case "TropicalV3_027": result = Tinted(m, new Color(.87f, .83f, .73f), .17f); break; // ivory retaining stone
                        case "TropicalV3_016": result = Tinted(m, new Color(.81f, .83f, .78f), .35f); break; // lighter crafted railing
                        case "TropicalV3_019": result = Tinted(m, new Color(.88f, .83f, .73f), .18f); break; // terrace stairs
                        case "TropicalV3_021": result = Tinted(m, new Color(.38f, .59f, .48f), .10f); break; // lush distant headlands
                        // Placeholder blob spectators: the real seated crowd sits there instead
                        // (TennisStandsCrowd).
                        // ...and the flat rectangular sand slab off the sea side, which read as an
                        // unfinished box from the air: the terrace meets the sea at its seawall.
                        case "TropicalV3_000":
                        case "TropicalV3_004": case "TropicalV3_005": case "TropicalV3_006": case "TropicalV3_007":
                        case "TropicalV3_013": // The same baked stand-in spectators' exposed skin / heads.
                            result = Invisible; break;
                    }
                    if (!result) continue;
                    cache[m] = result; mats[i] = result; changed = true;
                }
                if (changed)
                {
                    r.sharedMaterials = mats;
                    if (System.Array.TrueForAll(mats, material => !material || material.name == "Hidden placeholder")) r.enabled = false;
                }
            }
        }

        static Material Tinted(Material source, Color color, float smoothness)
        {
            Texture map = source.HasProperty("_BaseMap") ? source.GetTexture("_BaseMap") : source.mainTexture;
            return TennisVenueArt.Surface(source.name + " (styled) " + ColorUtility.ToHtmlStringRGB(color), color,
                smoothness, 0, source.name.Contains("_002") || source.name.Contains("_011") ? .028f : .045f, .009f, map);
        }

        /// Radial falloff shared by every contact shadow.
        public static Texture2D Falloff
        {
            get
            {
                if (falloff) return falloff;
                const int size = 64;
                falloff = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Contact shadow falloff" };
                var pixels = new Color32[size * size];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float dx = (x + .5f) / size * 2 - 1, dy = (y + .5f) / size * 2 - 1;
                        float r = Mathf.Sqrt(dx * dx + dy * dy);
                        float a = Mathf.Clamp01(1 - r); a = a * a * (3 - 2 * a);
                        pixels[y * size + x] = new Color32((byte)(a * 255), (byte)(a * 255), (byte)(a * 255), (byte)(a * 255));
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
            if (follow.GetComponent<TennisActor>()) quad.AddComponent<TennisShoeOcclusion>().shadow = shadow;
            return shadow;
        }
    }

    /// A soft occlusion disc on the court under a player or the ball. It shrinks and fades as
    /// the thing above it rises, which is also the best cue for how high the ball is.
    public sealed class ContactShadow : MonoBehaviour
    {
        public Transform Follow;
        public float Radius = .5f, Strength = .5f, Ground = .004f, FadeHeight = 3f;
        public Material Material;
        public float HeightOverride = -1;
        /// Legacy fallback for scenes without the venue court kit. Live matches use the
        /// cached rendered court plane, not this actor root, for the occlusion disc.
        public Transform Surface;
        /// Sky and crater courts: no shadow where there is no floor (a ball falling off the deck).
        public bool OnlyOverDeck;
        public bool GolfGround;

        void LateUpdate()
        {
            if (!Follow) { gameObject.SetActive(false); return; }
            Vector3 p = Follow.position;
            var rend = GetComponent<Renderer>(); if (rend) rend.enabled = !OnlyOverDeck || TennisVenue.OverDeck(p);
            float court = GolfGround ? (float)GolfArcade.Course.HoleView.GroundHeight(new GolfArcade.Course.CoursePoint(p.x,p.z)) : TennisVenue.HasRenderedCourtHeight ? TennisVenue.RenderedCourtHeight : Surface ? Surface.position.y : 0;
            float height = Mathf.Max(0, HeightOverride >= 0 ? HeightOverride : p.y - court);
            float fade = Mathf.Clamp01(1 - height / FadeHeight);
            transform.position = new Vector3(p.x, court + Ground, p.z);
            transform.localScale = Vector3.one * (Radius * 2 * Mathf.Lerp(.6f, 1f, fade));
            if (Material && Material.HasProperty("_Strength")) Material.SetFloat("_Strength", Strength * fade);
        }

        void OnDestroy() { if (Material) Destroy(Material); }
    }

}
