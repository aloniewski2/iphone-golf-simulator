using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace GolfArcade.EditorTools
{
    /// One-time move from the built-in renderer to URP, and the tennis post-processing look.
    ///
    ///   Unity -batchmode -executeMethod GolfArcade.EditorTools.UrpSetup.Run -quit
    ///
    /// Idempotent: re-running updates the same assets in place.
    public static class UrpSetup
    {
        const string Folder = "Assets/Resources/Tennis/Rendering";
        const string PipelinePath = Folder + "/TennisURP.asset";
        const string RendererPath = Folder + "/TennisURP_Renderer.asset";
        public const string PostPath = Folder + "/TennisPost.asset";

        [MenuItem("Golf Arcade/Rendering/Set Up URP")]
        public static void Run()
        {
            Directory.CreateDirectory(Folder);
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (!renderer)
            {
                renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, RendererPath);
            }
            AddSsao(renderer);

            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (!pipeline)
            {
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(pipeline, PipelinePath);
            }
            ConfigurePipeline(pipeline);

            GraphicsSettings.defaultRenderPipeline = pipeline;
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = pipeline;
            }
            QualitySettings.SetQualityLevel(2, false);
            // Correct lighting maths: soft light falloff, bloom and tonemapping all assume it.
            PlayerSettings.colorSpace = ColorSpace.Linear;

            BuildPostProfile();
            ConvertMaterials();
            EnsureShadersShip();
            AssetDatabase.SaveAssets();
            Debug.Log("[URP] setup complete: " + PipelinePath);
        }

        static void ConfigurePipeline(UniversalRenderPipelineAsset asset)
        {
            asset.msaaSampleCount = 4;
            asset.supportsHDR = true;
            asset.renderScale = 1f;
            asset.shadowDistance = 42;
            asset.shadowCascadeCount = 2;
            asset.mainLightShadowmapResolution = 2048;
            var so = new SerializedObject(asset);
            void Set(string name, System.Action<SerializedProperty> apply) { var p = so.FindProperty(name); if (p != null) apply(p); else Debug.LogWarning("[URP] no field " + name); }
            Set("m_SoftShadowsSupported", p => p.boolValue = true);
            Set("m_MainLightShadowsSupported", p => p.boolValue = true);
            Set("m_RequireDepthTexture", p => p.boolValue = true);
            Set("m_SupportsCameraDepthTexture", p => p.boolValue = true);
            Set("m_ColorGradingMode", p => p.intValue = 1);          // HDR grading
            Set("m_ColorGradingLutSize", p => p.intValue = 32);
            Set("m_AdditionalLightsRenderingMode", p => p.intValue = 1); // per pixel
            Set("m_AdditionalLightsPerObjectLimit", p => p.intValue = 2);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
        }

        static void AddSsao(UniversalRendererData renderer)
        {
            if (renderer.rendererFeatures.Any(f => f is ScreenSpaceAmbientOcclusion)) return;
            var ssao = ScriptableObject.CreateInstance<ScreenSpaceAmbientOcclusion>();
            ssao.name = "Contact AO";
            AssetDatabase.AddObjectToAsset(ssao, renderer);
            var so = new SerializedObject(ssao);
            var settings = so.FindProperty("m_Settings");
            void Set(string name, System.Action<SerializedProperty> apply) { var p = settings?.FindPropertyRelative(name); if (p != null) apply(p); }
            Set("Intensity", p => p.floatValue = 1.4f);
            Set("Radius", p => p.floatValue = .3f);
            Set("DirectLightingStrength", p => p.floatValue = .25f);
            Set("Downsample", p => p.boolValue = true);
            so.ApplyModifiedPropertiesWithoutUndo();
            renderer.rendererFeatures.Add(ssao);
            var rso = new SerializedObject(renderer);
            var map = rso.FindProperty("m_RendererFeatureMap");
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(ssao, out _, out long id);
            map.InsertArrayElementAtIndex(map.arraySize);
            map.GetArrayElementAtIndex(map.arraySize - 1).longValue = id;
            rso.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(renderer);
        }

        /// Warm, clean, slightly punchy: the late-afternoon resort look from the art targets.
        static void BuildPostProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(PostPath);
            if (!profile) { profile = ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(profile, PostPath); }
            T Get<T>() where T : VolumeComponent
            {
                if (!profile.TryGet(out T c)) { c = profile.Add<T>(true); c.name = typeof(T).Name; AssetDatabase.AddObjectToAsset(c, profile); }
                c.active = true;
                return c;
            }
            var tone = Get<Tonemapping>(); tone.mode.Override(TonemappingMode.ACES);
            var bloom = Get<Bloom>(); bloom.threshold.Override(1.05f); bloom.intensity.Override(.55f); bloom.scatter.Override(.62f);
            bloom.tint.Override(new Color(1f, .93f, .82f));
            var colour = Get<ColorAdjustments>(); colour.postExposure.Override(.2f); colour.contrast.Override(18f);
            colour.saturation.Override(20f); colour.colorFilter.Override(new Color(1f, .975f, .94f));
            var balance = Get<WhiteBalance>(); balance.temperature.Override(8f);
            var lgg = Get<LiftGammaGain>(); lgg.lift.Override(new Vector4(1f, .99f, 1.02f, 0f));
            var vignette = Get<Vignette>(); vignette.intensity.Override(.22f); vignette.smoothness.Override(.45f);
            var dof = Get<DepthOfField>(); dof.mode.Override(DepthOfFieldMode.Gaussian); dof.gaussianStart.Override(8f); dof.gaussianEnd.Override(30f);
            dof.active = false; // replays switch it on
            EditorUtility.SetDirty(profile);
        }

        /// Built-in Standard materials become URP Lit with the same colour, texture, normal map
        /// and surface values. Model importers already build URP materials once URP is active.
        public static void ConvertMaterials()
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            int converted = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (!m || m.shader == null || m.shader.name != "Standard") continue;
                Color color = m.HasProperty("_Color") ? m.color : Color.white;
                var tex = m.HasProperty("_MainTex") ? m.GetTexture("_MainTex") : null;
                var bump = m.HasProperty("_BumpMap") ? m.GetTexture("_BumpMap") : null;
                float gloss = m.HasProperty("_Glossiness") ? m.GetFloat("_Glossiness") : .3f;
                float metal = m.HasProperty("_Metallic") ? m.GetFloat("_Metallic") : 0f;
                var emission = m.HasProperty("_EmissionColor") ? m.GetColor("_EmissionColor") : Color.black;
                float mode = m.HasProperty("_Mode") ? m.GetFloat("_Mode") : 0;
                m.shader = lit;
                m.SetColor("_BaseColor", color);
                if (tex) m.SetTexture("_BaseMap", tex);
                if (bump) { m.SetTexture("_BumpMap", bump); m.EnableKeyword("_NORMALMAP"); }
                m.SetFloat("_Smoothness", gloss); m.SetFloat("_Metallic", metal);
                if (emission.maxColorComponent > .01f) { m.SetColor("_EmissionColor", emission); m.EnableKeyword("_EMISSION"); }
                if (mode >= 1) // cutout / fade / transparent
                {
                    if (mode == 1) { m.SetFloat("_AlphaClip", 1); m.EnableKeyword("_ALPHATEST_ON"); }
                    else { m.SetFloat("_Surface", 1); m.renderQueue = (int)RenderQueue.Transparent; m.SetOverrideTag("RenderType", "Transparent"); m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); }
                }
                EditorUtility.SetDirty(m); converted++;
            }
            Debug.Log($"[URP] converted {converted} materials to URP Lit");
        }

        static void EnsureShadersShip()
        {
            var graphics = new SerializedObject(GraphicsSettings.GetGraphicsSettings());
            var list = graphics.FindProperty("m_AlwaysIncludedShaders");
            foreach (var name in new[] { "Universal Render Pipeline/Lit", "Universal Render Pipeline/Unlit", "Universal Render Pipeline/Simple Lit", "Universal Render Pipeline/Particles/Unlit" })
            {
                var shader = Shader.Find(name); if (!shader) { Debug.LogError("[URP] missing " + name); continue; }
                bool present = false;
                for (int i = 0; i < list.arraySize; i++) if (list.GetArrayElementAtIndex(i).objectReferenceValue == shader) present = true;
                if (present) continue;
                list.InsertArrayElementAtIndex(list.arraySize);
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = shader;
            }
            graphics.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
