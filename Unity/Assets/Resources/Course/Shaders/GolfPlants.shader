// Golf postcard plants (LK_PLANTS): URP Lit look + a gentle vertex sway driven by the hole's wind.
// v2 2026-10-05 (area U). THE ONLY SHADER THAT READS TIME: lava, surf, fall, sky, ground, rocks and the sea stay static (the sea keeps the tennis shader's own swell).
//
// What it does: every vertex is displaced in WORLD space, along the wind direction (plus a small crosswise part), by
//     amplitude x weight x (a 0..1 downwind lean that breathes with two slow sines and a travelling gust envelope)  [the user's formula: weight x amplitude x the waves; the cantilever shape of a plant is in how the weights rise along it].
// weight = the mesh's vertex colour R (0 at the root / pinned point, rising to the free tip); G = a per-vertex phase 0..1; B = 0 marks "this mesh carries sway data".
// A mesh WITHOUT vertex colours reaches the shader as white (B = 1): weight 0, it never moves. The per-instance phase is hashed from the object's pivot (world XZ),
// so no two clumps move together. Nothing is stretched: weight 0 = no displacement, so the root stays pinned.
//
// Inputs: the global float4 _GolfWind set every frame by GolfArcade.Course.GolfWindSway: xyz = world wind direction x amplitude in yards (y = 0), w = time in seconds, wrapped at 600
// (every frequency below is a whole number of cycles per 600 s loop, so the wrap is seamless). All zero (never set) = no sway, i.e. exactly the old static plants.
// GolfWindSway.Displacement() is the C# mirror of GolfPlantsSwayOS: keep the two in step (the editor check GolfPlantSwayCheck compares them on the GPU).
//
// Shading: the same as URP Lit for what LK_PLANTS used (palette atlas _BaseMap x _BaseColor, smoothness, metallic 0, no normal map, no emission, opaque): LitForwardPass.hlsl's own
// vertex / fragment functions run on the displaced position, so lights, additional lights, shadows, SH ambient and fog are URP's. ShadowCaster, DepthOnly and DepthNormals apply the SAME
// sway to the same vertices (a shadow that stood still under a moving plant would show). The UnityPerMaterial layout is LitInput.hlsl's (SRP Batcher compatible); GPU instancing is on.
// Forward renderer only (both URP assets are Forward): there is no GBuffer pass. Only multi_compile keywords (nothing a build could strip from a material made at runtime by GolfLook).
// Lives under Resources so a player build always carries it (Resources.Load<Shader>("Course/Shaders/GolfPlants")).
Shader "GolfArcade/GolfPlants"
{
    Properties
    {
        [MainTexture] _BaseMap ("Palette atlas (LK_PLANTS)", 2D) = "white" {}
        [MainColor] _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        _Smoothness ("Smoothness", Range(0.0, 1.0)) = 0.1
        _Metallic ("Metallic", Range(0.0, 1.0)) = 0.0
        [HideInInspector][Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Lit"
            "IgnoreProjector" = "True"
        }
        LOD 300

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        // xyz = wind direction (world) x amplitude (yd), w = time (s, 0..600). Set by GolfWindSway; zero = static.
        float4 _GolfWind;

        // Dave Hoskins' hash without sine: stable on every GPU (no big-argument sin), 0..1.
        float GolfPlantsHash(float2 p)
        {
            float3 p3 = frac(float3(p.xyx) * 0.1031);
            p3 += dot(p3, p3.yzx + 33.33);
            return frac((p3.x + p3.y) * p3.z);
        }

        // World-space sway of one vertex, returned as an OBJECT-space offset (the stock vertex functions then run unchanged on the displaced position).
        // vcol: R = weight, G = phase, B = 0 for a mesh with sway data (white = no data = static).
        float3 GolfPlantsSwayOS(float3 positionOS, float4 vcol)
        {
            float amp = length(_GolfWind.xz);
            float w = (amp > 1e-5 && vcol.b < 0.5) ? saturate(vcol.r) : 0.0;
            float2 dir = _GolfWind.xz / max(amp, 1e-5);
            float2 origin = GetObjectToWorldMatrix()._m03_m23;               // the instance pivot (world XZ, yd)
            float2 cell = round(origin * 100.0);                             // 1 cm cells
            float h  = GolfPlantsHash(cell);                                 // per instance, one independent hash per oscillator
            float h2 = GolfPlantsHash(cell + 31.7);
            float h3 = GolfPlantsHash(cell + 71.3);
            float u = _GolfWind.w * (1.0 / 600.0);                           // 0..1 over the 600 s loop
            float travel = dot(origin, dir) * 0.025;                         // cycles: a gust front crossing the field (40 yd per cycle)
            // each clump gets its own whole-cycle frequency (0.45 .. 0.49 Hz and 0.83 .. 0.87 Hz: 270 + 6k and 498 + 6k cycles per 600 s loop, k = 0..4), so neighbours drift apart instead of staying in step
            float s1 = sin(6.2831853 * ((270.0 + 6.0 * floor(h2 * 4.999)) * u + frac(h  + vcol.g)));       // ~0.45 Hz
            float s2 = sin(6.2831853 * ((498.0 + 6.0 * floor(h3 * 4.999)) * u + frac(h2 + vcol.g * 0.5))); // ~0.83 Hz
            float sc = sin(6.2831853 * (360.0 * u + h3));                    // 0.60 Hz, crosswise
            float gust = 0.8 + 0.2 * sin(6.2831853 * (30.0 * u - travel));   // 0.6 .. 1.0, 0.05 Hz, travels with the wind
            float lean = 0.5 + 0.5 * gust * (0.62 * s1 + 0.38 * s2);         // 0 .. 1, downwind only
            float side = 0.2 * gust * sc;                                    // +-0.2 crosswise
            float2 d2 = (dir * lean + float2(-dir.y, dir.x) * side) * (amp * w);
            return TransformWorldToObjectDir(float3(d2.x, 0.0, d2.y), false);
        }
        ENDHLSL

        // ------------------------------------------------------------------
        //  Forward pass (single pass: main + additional lights, shadows, SH, fog)
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex PlantPassVertex
            #pragma fragment LitPassFragment

            // Universal Pipeline keywords (the same list as URP Lit's forward pass, all multi_compile: nothing is stripped from a runtime material)
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _SCREEN_SPACE_IRRADIANCE
            #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            // Unity defined keywords
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile_fragment _ LIGHTMAP_BICUBIC_SAMPLING
            #pragma multi_compile_fragment _ REFLECTION_PROBE_ROTATION
            #pragma multi_compile _ DYNAMICLIGHTMAP_ON
            #pragma multi_compile _ USE_LEGACY_LIGHTMAPS
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_fragment _ DEBUG_DISPLAY
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ProbeVolumeVariants.hlsl"

            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer

            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl"

            struct PlantAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 texcoord : TEXCOORD0;
                float2 staticLightmapUV : TEXCOORD1;
                float2 dynamicLightmapUV : TEXCOORD2;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings PlantPassVertex(PlantAttributes p)
            {
                UNITY_SETUP_INSTANCE_ID(p);
                Attributes a = (Attributes)0;
                a.positionOS = float4(p.positionOS.xyz + GolfPlantsSwayOS(p.positionOS.xyz, p.color), p.positionOS.w);
                a.normalOS = p.normalOS;
                a.tangentOS = p.tangentOS;
                a.texcoord = p.texcoord;
                a.staticLightmapUV = p.staticLightmapUV;
                a.dynamicLightmapUV = p.dynamicLightmapUV;
                UNITY_TRANSFER_INSTANCE_ID(p, a);
                return LitPassVertex(a);
            }
            ENDHLSL
        }

        // ------------------------------------------------------------------
        //  Shadow caster: the same sway, so a shadow moves with its plant
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex PlantShadowVertex
            #pragma fragment ShadowPassFragment

            #pragma multi_compile_instancing
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            // directional vs punctual shadow normal bias
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"

            struct PlantAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 texcoord : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings PlantShadowVertex(PlantAttributes p)
            {
                UNITY_SETUP_INSTANCE_ID(p);
                Attributes a = (Attributes)0;
                a.positionOS = float4(p.positionOS.xyz + GolfPlantsSwayOS(p.positionOS.xyz, p.color), p.positionOS.w);
                a.normalOS = p.normalOS;
                a.texcoord = p.texcoord;
                UNITY_TRANSFER_INSTANCE_ID(p, a);
                return ShadowPassVertex(a);
            }
            ENDHLSL
        }

        // ------------------------------------------------------------------
        //  Depth only (the camera depth texture the soft smoke cards read, depth priming)
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex PlantDepthVertex
            #pragma fragment DepthOnlyFragment

            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"

            struct PlantAttributes
            {
                float4 position : POSITION;
                float2 texcoord : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings PlantDepthVertex(PlantAttributes p)
            {
                UNITY_SETUP_INSTANCE_ID(p);
                Attributes a = (Attributes)0;
                a.position = float4(p.position.xyz + GolfPlantsSwayOS(p.position.xyz, p.color), p.position.w);
                a.texcoord = p.texcoord;
                UNITY_TRANSFER_INSTANCE_ID(p, a);
                return DepthOnlyVertex(a);
            }
            ENDHLSL
        }

        // ------------------------------------------------------------------
        //  Depth + normals (SSAO / depth-normals consumers; none are on today, the pass keeps the shader complete)
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex PlantDepthNormalsVertex
            #pragma fragment DepthNormalsFragment

            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitDepthNormalsPass.hlsl"

            struct PlantAttributes
            {
                float4 positionOS : POSITION;
                float4 tangentOS : TANGENT;
                float2 texcoord : TEXCOORD0;
                float3 normal : NORMAL;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings PlantDepthNormalsVertex(PlantAttributes p)
            {
                UNITY_SETUP_INSTANCE_ID(p);
                Attributes a = (Attributes)0;
                a.positionOS = float4(p.positionOS.xyz + GolfPlantsSwayOS(p.positionOS.xyz, p.color), p.positionOS.w);
                a.tangentOS = p.tangentOS;
                a.texcoord = p.texcoord;
                a.normal = p.normal;
                UNITY_TRANSFER_INSTANCE_ID(p, a);
                return DepthNormalsVertex(a);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
