using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace GolfArcade.Course
{
    /// The postcard holes' (8 Needle, 9 Split, 10 Crater) surface look at runtime.
    /// The Blender FBX carries material NAMES only (LOOK_CONTRACT.md section 3); every name that starts
    /// with "LK_" is rebuilt here as a real textured material from Resources/Course/Look/<Name>_C|_N|_E:
    /// URP Lit with albedo + normal (+ emission for basalt cracks and lava), GolfArcade/TennisWater for the
    /// sea, GolfArcade/GolfSurf for whitewater, the waterfall sheet and smoke. Hole 7 never comes here.
    /// RUNTIME.md (work/postcard-look) lists every tunable.
    public static class GolfLook
    {
        public const string Prefix = "LK_";
        // One-line rollback. No pipeline or scoring changes.
        public static bool UseSurfaceShaders = true;
        static Material BuildSurface(string name, Spec spec)
        {
            bool rock=name.Contains("CLIFF")||name.Contains("ROCK")||name.Contains("BASALT")||name.Contains("MASONRY");
            var shader=Resources.Load<Shader>("Course/Shaders/"+(rock?"GolfRock":"GolfGround"));
            if(!shader||!shader.isSupported)throw new System.InvalidOperationException("Golf surface shader unavailable: "+name);
            var m=new Material(shader){name=name,enableInstancing=true};
            var albedo=rock?Resources.Load<Texture2D>("Course/Surface/"+spec.Tex+"_CE"):Load(spec.Tex,"_C",true);var normal=spec.Normal?Load(spec.Tex,"_N",true):null;
            m.SetColor("_BaseColor",albedo?spec.Tint:spec.Fallback);if(albedo)m.SetTexture("_BaseMap",albedo);
            if(normal)m.SetTexture("_BumpMap",normal);m.SetFloat("_NormalEnabled",normal?1:0);
            m.SetFloat("_BumpScale",spec.BumpScale);m.SetFloat("_Smoothness",spec.Smoothness);
            m.SetFloat("_SheenFromAlpha",spec.SmoothnessFromAlbedoAlpha?1:0);
            m.SetFloat("_Rock",rock?1:0);m.SetFloat("_Basalt",name.Contains("BASALT")?1:0);
            m.SetFloat("_Cap",name.Contains("WET")||name.Contains("BASALT")||name.Contains("MASONRY")?0:.3f);
            float surface=name=="LK_FAIRWAY"?1:name=="LK_GREEN"?2:name=="LK_SAND"?5:0;
            if(surface==5)m.EnableKeyword("_GOLF_SAND");
            m.SetFloat("_Surface",surface);m.SetFloat("_Bands",surface==1?.025f:surface==2?.018f:0);
            m.SetFloat("_StripeWidth",surface==1?5:2.2f);
            if(spec.Emissive) {var e=Load(spec.Tex,"_E",true);if(e)m.SetTexture("_EmissionMap",e);
                m.SetFloat("_EmissionEnabled",e?1:0);m.SetVector("_EmissionColor",(Vector4)spec.Emission);}
            return m;
        }
        static void SurfaceDirection(Material m,int hole)
        {
            if(!m||!m.HasProperty("_StripeDirection"))return;
            var view=HoleView.Current;Vector3 along=Vector3.forward;
            if(view&&view.Hole.Number==hole){along=new Vector3((float)(view.Hole.Pin.X-view.Hole.Tee.X),0,(float)(view.Hole.Pin.D-view.Hole.Tee.D)).normalized;}
            m.SetVector("_StripeDirection",new Vector4(along.z,0,-along.x,0));
        }
        public static void DressLegacy(GameObject model,int hole)
        {
            if(!UseSurfaceShaders)return;
            var cache=new Dictionary<string,Material>();
            foreach(var r in model.GetComponentsInChildren<MeshRenderer>(true))
            {
                var mats=r.sharedMaterials;
                for(int i=0;i<mats.Length;i++)
                {
                    var src=mats[i];if(!src)continue;
                    string n=r.name;string key=n.StartsWith("GREEN")?"LK_GREEN":n.StartsWith("FAIRWAY")?"LK_FAIRWAY":n.StartsWith("TEE_BOX")?"LK_GREEN":n.StartsWith("BUNKER")?"LK_SAND":n.StartsWith("TERRAIN")?"LK_ROUGH":null;
                    // Hole 7's frozen island mesh has rough in slot 0 and the
                    // upper/lower cliff bands in slots 1/2 (phase2_5_course.py).
                    // HoleView replaces imported material names with flat Lit
                    // materials, so use the authored slots to retain its palette.
                    if(hole==7 && n=="TERRAIN_ISLAND" && (i==1 || i==2))key="LK_ROCK";
                    if(key==null)continue;
                    string cacheKey=key+src.color.ToString();
                    if(!cache.TryGetValue(cacheKey,out var m)){
                        m=BuildSurface(key,Table[key]);m.SetTexture("_BaseMap",Texture2D.whiteTexture);m.SetFloat("_NormalEnabled",0);m.SetColor("_BaseColor",src.color);SurfaceDirection(m,hole);cache.Add(cacheKey,m);
                    }
                    mats[i]=m;
                }
                r.sharedMaterials=mats;
            }
            GolfSurfaceEdges.Apply(model);
            GolfSurfaceBatching.Apply(model);
        }
        const string LookFolder = "Course/Look/";

        public enum Kind { Lit, Water, Surf, Lava, Plants }

        /// One row of the material table. Colours are sRGB (as authored); `Emission` is a linear HDR
        /// multiplier on the _E map, per channel (warm-weighted: see LK_BASALT / LK_LAVA).
        public sealed class Spec
        {
            public Kind Kind = Kind.Lit;
            public string Tex;                // texture base name: <Tex>_C, <Tex>_N, <Tex>_E
            public bool Normal = true, Emissive;
            public float Smoothness = .12f, BumpScale = 1f;
            /// v9 2026-10-05 (grass liveliness, mow-band sheen): the albedo's ALPHA channel is the smoothness (URP Lit "Smoothness Source = Albedo Alpha": keyword
            /// _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A, smoothness = alpha x _Smoothness, so Smoothness is the SCALE of the channel, 1 = the alpha is absolute). Fairway_C / Green_C carry per-band sheen there.
            /// `SmoothnessNoAlpha` is the flat smoothness used when the loaded albedo has no alpha channel (an old PNG): the material never goes mirror-smooth.
            public bool SmoothnessFromAlbedoAlpha; public float SmoothnessNoAlpha = .10f;
            public Color Tint = new(.94f, .94f, .94f);        // _BaseColor over the albedo (EnvV4 "a notch under the characters")
            public Color Fallback = new(.6f, .6f, .6f);       // flat colour when the albedo is missing (never pink)
            public Color Emission = Color.black;              // HDR, multiplies the _E map
            public bool NoSpecular;                           // matte: no specular highlight, no environment reflection (lava: the sky reflection washes the orange to pink)
            // water (GolfArcade/TennisWater; colours sRGB)
            public Color Shallow, Deep, Sky; public float DeepDistance = 60, WaveScale = .35f, Sparkle = 6;
            // lava (GolfArcade/GolfLava): the textures say how hot a texel is, a 3-stop ramp (sRGB, all in the orange band) says what colour that is
            public Color RampDeep = new(.58f, .13f, .04f), RampCrust = new(.73f, .17f, .05f), RampFlow = new(.88f, .31f, .065f), RampHot = new(1f, .50f, .11f);
            public float HeatAlbedo = 1.5f, HeatGlow = 1.5f, HeatBias = -.16f, HeatGain = 1.4f, Relief = .15f;   // calibration round 2 (review K, "orange paint"): HeatBias -.15 -> -.16, RampDeep (150,33,10) -> (24,6,3), RampCrust (186,43,13) -> (210,52,16): near-black crust ribbons (Game view V<.4 on 3.5 % of the pool, was 0.0 %; in band 92.7 %, was 92.6 %), T's model gates all pass
            public bool WorldUV = true;                       // the lava maps are projected from the world (one tile per LavaTilePerYard), never from the mesh's UVs
            // surf (GolfArcade/GolfSurf)
            public float VertexAlpha, AlphaPower = 1, AlphaGain = 1, Lit = 1, SunShare = .45f, SelfLight = .15f, Wrap = .5f, EdgeFade, FogShare = 1, SoftFade;   // SoftFade: world units (yd) over which a card fades out in front of an opaque surface behind it (GolfSurf _SoftFade, 0 = off)
            public Vector2 Tiling = Vector2.one;
        }

        static Color Rgb(int r, int g, int b) => new(r / 255f, g / 255f, b / 255f);

        /// One Lava tile is 24 m = 26.25 yd: the world projection of GolfLava (and the UV density a mesh-UV lava must have: tile units = metres / 24).
        public const float LavaTilePerYard = .9144f / 24f;

        /// The material table (LOOK_CONTRACT.md section 3). UVs are authored in tile units, so tiling stays (1,1).
        public static readonly Dictionary<string, Spec> Table = new()
        {
            // v9 2026-10-05: smoothness comes from the albedo alpha (per mow band: glossy light bands, matte dark bands; tile mean ~.15), Smoothness = 1 = the scale of that channel (the generator's GRASS_SHEEN_ALPHA gate reads this number); flat .10 / .12 when the PNG has no alpha
            ["LK_FAIRWAY"]       = new Spec { Tex = "Fairway",   Smoothness = 1f, SmoothnessFromAlbedoAlpha = true, SmoothnessNoAlpha = .10f, Fallback = Rgb(118, 208, 56), BumpScale = .75f },
            ["LK_GREEN"]         = new Spec { Tex = "Green",     Smoothness = 1f, SmoothnessFromAlbedoAlpha = true, SmoothnessNoAlpha = .12f, Fallback = Rgb(156, 228, 72), BumpScale = .6f },
            ["LK_ROUGH"]         = new Spec { Tex = "Rough",     Smoothness = .08f, Fallback = Rgb(58, 148, 38) },
            ["LK_SCRUB"]         = new Spec { Tex = "Scrub",     Smoothness = .08f, Fallback = Rgb(132, 128, 62) },
            ["LK_SAND"]          = new Spec { Tex = "Sand",      Smoothness = .12f, Fallback = Rgb(240, 218, 160), Tint = new Color(.80f, .80f, .80f) },   // bright albedo: .94 clipped to white under Needle's light
            ["LK_CLIFF"]         = new Spec { Tex = "Cliff",     Smoothness = .12f, Fallback = Rgb(92, 90, 86), BumpScale = 1.2f },
            ["LK_CLIFF_DARK"]    = new Spec { Tex = "CliffDark", Smoothness = .35f, Fallback = Rgb(62, 66, 64), BumpScale = 1.2f },
            ["LK_ROCK"]          = new Spec { Tex = "Rock",      Smoothness = .12f, Fallback = Rgb(128, 124, 116), BumpScale = 1.2f },
            ["LK_ROCK_WET"]      = new Spec { Tex = "Rock",      Smoothness = .35f, Fallback = Rgb(84, 86, 84), BumpScale = 1.2f, Tint = new Color(.68f, .70f, .70f) },
            ["LK_PATH"]          = new Spec { Tex = "Path",      Smoothness = .10f, Fallback = Rgb(206, 200, 184) },
            ["LK_MASONRY"]       = new Spec { Tex = "Masonry",   Smoothness = .10f, Fallback = Rgb(170, 162, 148), BumpScale = 1.2f },
            // BASALT: emission multiplier (LINEAR, per channel, on the _E map) warm-weighted and about 1.0, not the first build's neutral 1.8:
            // post-processing renders nowhere in this project (no PostProcessData on either renderer, RUNTIME.md section 7), so there is no bloom to carry
            // a hot core and no ACES to roll the highlights off; a neutral multiplier clipped G with R and turned the orange cracks cream.
            // LAVA is its own self-lit shader (GolfArcade/GolfLava, RUNTIME.md v2.3): textures give the heat, a ramp inside the orange band gives the colour.
            // URP Lit + albedo + emission could not hold the band: the swirl texture's hue spread (red crust .. yellow veins) is wider than hue 8..28, and
            // the best Lit tint x multiplier measured 76-78 % of the lava pixels in band (needs 85 %). Gates: GolfLookBoards LAVA_ORANGE_NO_BLOOM, BASALT_GLOW_ORANGE.
            // v2 review fix: the lit basalt read as brown planks ((69,52,43), S .37, median luminance 52; crater.jpg basalt is charcoal (26,25,29) .. (39,32,34), median 30..34, S .2..28).
            // Why: Basalt_C is already near-neutral dark (39,36,38), but the dusk key / ambient are orange and a smoothness .15 sky reflection puts a floor of ~30 sRGB levels
            // under any tint (dim LINEAR values are big in sRGB). So the basalt is MATTE (no specular, no environment reflection) and carries a cool, dark tint (.52,.80,.92)
            // that cancels the orange light: key-lit wall (emission off) mean (36,31,28) median 29 S .21; beside the lava lights (51,35,25) like crater.jpg's lava-lit rock
            // (52,27,23). The darker rock answers the lava lights less, so LavaLightIntensity is 600 (was 200). Glow = emission only (2.2 % of the wall, joints).
            ["LK_BASALT"]        = new Spec { Tex = "Basalt",    Smoothness = .15f, Fallback = Rgb(38, 36, 40), BumpScale = 1.4f, Emissive = true, Emission = new Color(1.0f, .80f, .50f), Tint = new Color(.56f, .74f, .90f), NoSpecular = true },   // v2 repair round 3 2026-10-05 (review, high: Game-view basalt S .54-.59 maroon, crater.jpg (26,25,29)): the table tint stays cool and dark, only R .52 -> .56 (key-lit wall brightest tenth (49,53,54) S .09 -> (53,54,54) S .02, BASALT_NOT_GREEN); the red was in the LIGHT, not the tint: the walls in the proof stills are shadow side (ambient + fog + lava lights only), and Crater's fog (.42,.19,.14) and ember equator (.55,.30,.22) are now neutral (GolfAtmosphere hole 10); mean S .54 / .58 / .53 -> .13 / .16 / .12 (tee / approach / lava rim, v2/unity_r3/v11). calibration round 2 (review K): (.52,.80,.92) -> (.52,.74,.90): the brightest tenth of the lit wall read green-grey (55,60,55) hue 121; now (55,55,54) neutral, wall mean (36,29,29) S .18, median 28
            ["LK_LAVA"]          = new Spec { Kind = Kind.Lava, Tex = "Lava", FogShare = .35f,   // v2 repair round 3 2026-10-05: the neutral Crater fog (basalt) greyed the 430 yd cone lava (tee still: 65 % in band, limit 85); the lava takes 35 % of the fog: 94.3 % in band (fog share .5: 88.4 %, .7: 80.8 %, 1: 65.1 %; v2/unity_r3/v12), approach / lava rim unchanged (94.8 / 92.6 %)
                                                 Fallback = Rgb(255, 110, 24), BumpScale = 1f, Emissive = true, Emission = new Color(1.0f, .65f, .50f), Tint = new Color(.75f, .75f, .75f), NoSpecular = true, Smoothness = .10f,
                                                RampDeep = Rgb(84, 22, 2), RampCrust = Rgb(210, 52, 16), RampFlow = Rgb(224, 79, 17), RampHot = Rgb(255, 150, 60) },   // v6 2026-10-04 (area T, review 'black ink swirls / no hot cores'): _Deep (24,6,3) near-black -> (84,22,2) dark RED crust (hue 14.6, S .98, V .33: still < .4 so LAVA_HAS_CRUST counts it; B 2 keeps |dR|+|dG|+|dB| = 34 > 30 from the dusk sea colour (77,31,20), which the board's lava mask needs: (84,22,8) measured 28 and dropped the crust to 0.5 %), _Hot (255,128,28) -> (255,150,60) (hue 27.7, S .76: the top of the band, a paler yellow-orange vein)
            ["LK_PLANTS"]        = new Spec { Kind = Kind.Plants, Tex = "Plants", Smoothness = .10f, Normal = false, Fallback = Rgb(70, 140, 52) },   // v2 2026-10-05 (area U): GolfArcade/GolfPlants = URP Lit look (same palette atlas, tint, smoothness) + the gentle wind sway (GolfWindSway); falls back to URP Lit when the shader is missing
            // The sea. TennisWater's shallow->deep gradient runs from the WORLD ORIGIN (= the tee), not from the shore,
            // so the open ocean keeps both ends deep and saturated (a slight lift near the tee only), and the shelf mesh
            // (WATER_SHELF) is turquoise at both ends. Colours are matched to needle.jpg / split.jpg.
            // _Sky is what the fresnel blends toward: the phone camera sees the sea at a grazing angle (fresnel ~.9), so a pale
            // _Sky turns the whole ocean grey-lavender (measured (203,218,225) with the tennis-like pale value). Keep it a saturated blue.
            ["LK_WATER"]         = new Spec { Kind = Kind.Water, Shallow = Rgb(12, 86, 144), Deep = Rgb(6, 54, 104), Sky = Rgb(28, 108, 156), DeepDistance = 260, WaveScale = .22f, Sparkle = 5 },
            ["LK_WATER_SHALLOW"] = new Spec { Kind = Kind.Water, Shallow = Rgb(24, 93, 106), Deep = Rgb(19, 81, 102), Sky = Rgb(35, 92, 119), DeepDistance = 420, WaveScale = .30f, Sparkle = 4 },   // v2 calibration: x .88 of (27,106,120) / (22,92,116) / (40,105,135): the thin edge lit lum 77 / 90 (needle / split) -> the stills' teal (lum 66 / 64) x <= 1.3
            // golf-owned GolfSurf: static alpha-blended cards
            ["LK_SURF"]          = new Spec { Kind = Kind.Surf, Tex = "Surf",  Normal = false, Tint = Color.white, Fallback = new Color(1, 1, 1, .55f), VertexAlpha = 1, AlphaPower = 1, Lit = 1, SunShare = .50f, SelfLight = .35f, Wrap = .2f },   // .18 self light read lavender on the blue sea
            ["LK_FALL"]          = new Spec { Kind = Kind.Surf, Tex = "Fall",  Normal = false, Tint = new Color(.96f, .99f, 1f), Fallback = new Color(.92f, .97f, 1f, .7f), VertexAlpha = 0, AlphaPower = 1, Lit = 1, SunShare = .40f, SelfLight = .22f, Wrap = .8f },
            // v2 review fix: the first soft smoke was invisible (peak alpha .49, plume moved the frame by median 4.6 / p99 14.5 levels). crater.jpg's plumes: top-10 % (183,154,150) over a ~100-luminance sky.
            // Then: tint (1,.95,.92), opacity gain 1.35 on the painted alpha (peak .70, feathered edge kept: _EdgeFade .18, border alpha 0), self light .4, lit .8: plume median 8 / p99 77 levels, brightest tenth ~ (190,150,140).
            // v2 repair round 1 (review: "no readable smoke plume in any of the three stills", a ghostly streak in the aerial): the board card stood 130 yd out (1 % fog) but the real plumes stand 390+ m (430 yd) from
            // the tee, where Crater's dark red-brown fog (.42,.19,.14, 120..1000 yd) took ~35 % of their colour and the warm ambient tinted the rest: the plume came out (178,110,95), DARKER than the bright dusk sky
            // behind it (median |delta luminance| 9.9 / p99 42 at 430 yd vs 14.8 / 61 at 130). Now: FogShare .3 (the plume keeps 70 % of its own colour: smoke lit from the lava below is not hidden by haze that is
            // darker than the sky it sits against), tint white, Lit .6 / SelfLight .40 (the ambient no longer paints it brown; own lit colour (189,164,163) = crater.jpg's (183,154,150) +-14 in the texture tool's model gate): at 430 yd
            // median 15.4 / p99 62.7 levels (was 9.9 / 42.5), brightest tenth over the dusk sky (186,143,136), crater.jpg's plume (183,154,150). Peak alpha unchanged (.67), border 0, edge step p99 2.5 levels/px. What this cannot do is make a plume bigger or put it in the frame: that is the hole's
            // card size / placement (SMOKE_VISIBLE_STILLS reads the share of every Crater still the plume really covers).
            // v2 repair round 3 2026-10-05 (review, medium, hole 10: SMOKE_SOFT FAIL on tee, edge step p99 18.0 > 6, and approach, |dLum| max 132 > 110): (1) tee: the straight diagonal edge down the cone flank is where a smoke card passes THROUGH the cone and the depth test cuts
            // it (hiding the cone: edge step 1.3; the card clipped by it: 17.0): SoftFade 40 yd (GolfSurf _SoftFade, soft intersection on the depth texture) fades a card out over 40 yd in front of any surface behind it, no line left; (2) approach: three cards stack over the near-black wall, so the
            // plume's core reaches alpha ~.9 and moves the frame by 129 levels: per-card gain 1.35 -> 1.10 and contrast power 1 -> 1.5 (thin edges thinner, peak per card .58 -> .32, stacked core ~.7): max 102.6 (limit 110), p95 89.9, tee edge 3.0, readable share 1.9 / 3.2 / 3.9 %
            // (>= 1.5 %). Colour (Lit .6, SelfLight .40, FogShare .3) unchanged. v2/unity_r3/v4 + v5 sweeps (14 combinations).
            ["LK_SMOKE"]         = new Spec { Kind = Kind.Surf, Tex = "Smoke", Normal = false, Tint = new Color(1f, 1f, 1f, 1f), Fallback = new Color(.5f, .45f, .45f, .35f), VertexAlpha = 0, AlphaPower = 1.5f, AlphaGain = 1.1f, Lit = .6f, SunShare = .30f, SelfLight = .40f, Wrap = 1f, EdgeFade = .18f, FogShare = .3f, SoftFade = 40f },
        };

        /// Lava point lights spawned on LAVA_LIGHT_nn empties (hole 10). A renderer only gets the few lights nearest ITS bounds: the
        /// per-object additional-lights limit is 2 in TennisURP and 4 in HeroBaseStudioURP (Crater has no additional directional: its fill is folded
        /// into the ambient, GolfAtmosphere.FoldFillIntoAmbient), so a big basalt / lava mesh sees one or two lights chosen by ITS centre. The hole
        /// builder must therefore split the walls and skins beside the lava into chunks of about 20 yd (RUNTIME.md). Measured (GolfLookBoards,
        /// both assets alike; v2 review fix: intensity 200 -> 600 because the basalt is now a dark matte charcoal that answers the lights less): the first build's 200 / 52 yd / lift 4 lights the basalt within ~15 yd of a light by >= 8 luminance levels on ~40 % of its pixels and
        /// never flares the lava (0 % of the pool pixels brightened by >= 40 levels); the old 28 / 48 / 0 lit 0.2 %.
        public static Color LavaLightColor = new(1f, .45f, .12f);
        public static float LavaLightRange = 52f, LavaLightIntensity = 600f;   // world units are yards; URP point lights fall off 1/d^2
        /// The light sits this far (yd) above its LAVA_LIGHT_nn empty (the empties are ~2.4 yd over the lava): higher = a wider,
        /// flatter pool, so the lava under the light does not flare while the walls 10-20 yd away still warm up.
        public static float LavaLightLift = 4f;

        public static bool IsPostcard(int holeNumber) => holeNumber >= 8 && holeNumber <= 10;
        public static bool Handles(string materialName) => !string.IsNullOrEmpty(materialName) && Clean(materialName).StartsWith(Prefix);

        static string Clean(string name)
        {
            name = name.Replace(" (Instance)", "");
            int dot = name.IndexOf('.');                  // Blender duplicates: LK_ROCK.001 -> LK_ROCK
            if (dot > 0) name = name.Substring(0, dot);
            int at = name.IndexOf('@');                   // per-hole variants: LK_FAIRWAY@10 -> LK_FAIRWAY
            return at > 0 ? name.Substring(0, at) : name;
        }

        /// Per-hole _BaseColor over a material (replaces the table Tint on that hole only), sRGB as authored (SetColor converts: .94 -> .87 linear).
        /// v2 2026-10-04: the grass albedos were re-hued to the stills (Fairway_C hue 73.6, Green_C 72.4, Rough_C 68.1, Scrub_C 49.9: 20 deg less blue-green than
        /// before), so Needle needs no hole tint (table .94) and Crater keeps its lime-keeping tint; Split's old tint (.76,.84,.72) kept G high and pushed the new albedo
        /// to hue 80, so it gets the neutral darkening the texture agent solved with work/postcard-look/v2/tools/lit_predict.py (v2/tex/recommended_tints.json).
        /// Measured under the real hole lights on the board (GolfLookBoards.Grass, GRASS_HUE_LIT): RUNTIME.md v2.5.
        public static readonly Dictionary<(int, string), Color> HoleTint = new()
        {
            // v2 calibration 2026-10-04 (Game view, board under the real hole lights, RUNTIME.md "v2 2026-10-04 calibration"): Needle's table tint .94 left the lit fairway / green at V .78 / .77
            // (needle.jpg (172,186,66) / (171,181,64) = V .73): neutral .87 / .88 lands on (172,185,67) / (173,184,72). Split's green sat at S .548 (< the stills' .55..66; split.jpg (154,173,75) S .57):
            // B .74 -> .70 gives (154,172,74) S .57, hue unchanged.
            // v5 2026-10-04 (area T, grass flatness): the new mottle drifts the albedo hue by +-3 deg; the Game-view per-pixel hue of the Needle tee was p5/p50/p95 62/65/69 (band 64..73), the Split tee 67/70/74:
            // R -1.7 % on Needle (hue +~1.1 deg) and +1.5 % on Split's fairway (hue -~.7 deg) centre both inside 64..73 (V, S unchanged within .01).
            [(8, "LK_FAIRWAY")] = new Color(.82f, .835f, .835f),
            [(8, "LK_GREEN")] = new Color(.752f, .762f, .762f),
            // Needle's warm key (1,.86,.66) x 2.3 clipped 4.9 % of the flat path pixels (a channel >= 250; limit 3 %) and 2.1 % of the sand: the stills' flagstones are (160..178,144..157,82..100) V .63..70,
            // their bunker sand (188,175,142) V .74. Path .94 -> .86 gives V ~.68 and 0 % clipped; sand (.80) -> (.72,.74,.76) takes V .92 -> ~.82 and its orange cast (hue 32, stills 43..47) a little toward beige.
            [(8, "LK_PATH")] = new Color(.86f, .86f, .86f),
            [(8, "LK_SAND")] = new Color(.72f, .74f, .76f),
            [(9, "LK_FAIRWAY")] = new Color(.70f, .66f, .66f),
            [(9, "LK_GREEN")] = new Color(.677f, .658f, .658f),
            [(9, "LK_ROUGH")] = new Color(.77f, .76f, .77f),
            [(9, "LK_SCRUB")] = new Color(.82f, .82f, .86f),
            [(9, "LK_SAND")] = new Color(.70f, .70f, .68f),
            // v9 2026-10-05 (grass liveliness, area T): the mow bands are +-12 % luminance now (Fairway_C / Green_C) and the GolfLookSmoke grass board (central lower third of a flat quad: it sits on the LIGHT band / stripe) read V .75 (Needle fairway), .76 (green), .74 (Split green;
            // HeroBaseStudioURP +.02) on the new maps with the v8 tints (V band .55..75). Needle fairway x.96 -> (.82,.835,.835) (board V .72), green x.94 -> (.752,.762,.762) (V .71), Split green x.96 -> (.677,.658,.658) (V .71): the dark stripes then read V ~.57-.58, the means ~.64-.66.
            // Crater's light band clipped G >= 250 on 3-4 % of the stills' grass (old maps 0-3 %, round-1 tee 1.4 %): fairway / green x.89 (B x.89, S up) = (.85,1.01,.55): clipped 0.0-0.3 % on the Game-view stills, hue 64.5..70, board V .57 / S .74 (v2/tex/v9 variants H..J, tune_t10*.txt).
            // v2 repair round 3 2026-10-05: the neutral Crater ambient / fog (GolfAtmosphere hole 10: the equator and ground bounce no longer add an ember red to the grass' shaded faces) moved the lit grass hue +3 (fairway
            // (165,189,43) h70 -> (156,188,42) h73 = the top of the band, GRASS_HUE_GAMEVIEW hole10 FAIL): R .90 -> .95 (rough .93), G 1.15 -> 1.13 (rough 1.09) puts it back: fairway (164,185,42) h69, green (165,182,44) h68,
            // rough h60-63 (crater.jpg fairway h71, pillar green h67); v2/unity_r3 tune_grass.sh (3 specs).
            [(10, "LK_FAIRWAY")] = new Color(.85f, 1.01f, .55f),
            [(10, "LK_GREEN")] = new Color(.85f, 1.01f, .55f),
            [(10, "LK_ROUGH")] = new Color(.93f, 1.09f, .62f),
        };

        /// The material for an LK_ name on one hole: Get(name), or its per-hole tinted copy (named "LK_X@hole").
        public static Material GetForHole(string materialName, int holeNumber)
        {
            string name = Clean(materialName);
            var shared = Get(name);
            SurfaceDirection(shared,holeNumber);
            if (!HoleTint.TryGetValue((holeNumber, name), out var tint)) return shared;
            string key = name + "@" + holeNumber;
            if (cache.TryGetValue(key, out var m) && m) return m;
            m = new Material(shared) { name = key, enableInstancing = true };
            if (shared.GetTexture("_BaseMap")) { tint.a = 1; m.SetColor("_BaseColor", tint); }   // the flat fallback keeps its palette colour
            SurfaceDirection(m,holeNumber);
            cache[key] = m;
            return m;
        }

        static readonly Dictionary<string, Material> cache = new();
        static readonly HashSet<string> warned = new();

        /// The runtime material for an LK_ name (cached; the same Material for every renderer, so instancing /
        /// the SRP batcher can share it). Unknown LK_ names fall back to the generic rule
        /// LK_FOO_BAR -> textures FooBar_C / FooBar_N on URP Lit (smoothness .12).
        public static Material Get(string materialName)
        {
            string name = Clean(materialName);
            if (cache.TryGetValue(name, out var m) && m) return m;
            if (!Table.TryGetValue(name, out var spec))
            {
                spec = new Spec { Tex = Pascal(name.Substring(Mathf.Min(Prefix.Length, name.Length))) };
                Warn(name, $"[GolfLook] {name} is not in the material table: using the generic rule (textures {spec.Tex}_C/_N, URP Lit)");
            }
            m = spec.Kind switch { Kind.Water => BuildWater(name, spec), Kind.Surf => BuildSurf(name, spec), Kind.Lava => BuildLava(name, spec), Kind.Plants => BuildPlants(name, spec), _ => BuildLit(name, spec) };
            m.name = name;
            cache[name] = m;
            return m;
        }

        static string Pascal(string snake)
        {
            var parts = snake.ToLowerInvariant().Split('_');
            var s = new System.Text.StringBuilder();
            foreach (var p in parts) if (p.Length > 0) s.Append(char.ToUpperInvariant(p[0])).Append(p.Substring(1));
            return s.ToString();
        }

        static void Warn(string key, string message) { if (warned.Add(key)) Debug.LogWarning(message); }

        /// Anisotropic filtering level of every tiling look texture (ground, rock, basalt, lava), set here at load time instead of in the importer
        /// (the importer's 4 is the texture area's and a re-import resets it; this survives both). v2 repair round 2 (review, low, hole 10): the
        /// ground seen at a grazing angle (the lava-rim shot's far fairway, 10-25 deg off the horizon: footprint ratio 6..16) was soft and streaky
        /// because a GPU takes the mip level from major axis / min(ratio, level): at level 4 and a ratio of 12 it reads a mip 1.6 levels too blurry
        /// (Laplacian detail of that window +14 % at 16; the hard wedge inside it is a UV smear of the hole 10 mesh, which no level removes: verifier gate GROUND_UV_LOCAL_SMEAR).
        /// It does nothing for the ground right under the camera (that is texel magnification: RUNTIME.md "v2 repair round 2").
        /// Only applies where the quality level allows per-texture aniso (QualitySettings.anisotropicTextures = 1 on Medium and up; Very Low / Low ignore it).
        public static int Aniso = 16;
        static bool TilesAndNeedsAniso(string name) => !(name.StartsWith("Sky_") || name.StartsWith("Plants_") || name.StartsWith("Surf_") || name.StartsWith("Fall_") || name.StartsWith("Smoke_"));

        static Texture2D Load(string name, string suffix, bool required)
        {
            if (string.IsNullOrEmpty(name)) return null;
            var t = Resources.Load<Texture2D>(LookFolder + name + suffix);
            if (!t && required) Warn(name + suffix, $"[GolfLook] missing Resources/{LookFolder}{name}{suffix}: falling back to the flat palette colour");
            if (t && TilesAndNeedsAniso(name)) t.anisoLevel = Mathf.Max(t.anisoLevel, AnisoOverride(Aniso));
            return t;
        }

        static int AnisoOverride(int level)
        {
#if UNITY_EDITOR
            // experiments only (editor): GOLF_LOOK_ANISO=<1..16> replaces the level for the run; never read in a player
            if (int.TryParse(System.Environment.GetEnvironmentVariable("GOLF_LOOK_ANISO"), out var v)) return Mathf.Clamp(v, 1, 16);
#endif
            return level;
        }

        static Shader lit, water, surf, lavaShader, plantsShader;
        static Shader LitShader => lit ? lit : lit = Shader.Find("Universal Render Pipeline/Lit");
        static Shader WaterShader => water ? water : water = Resources.Load<Shader>("Tennis/Shaders/TennisWater");
        static Shader SurfShader => surf ? surf : surf = Resources.Load<Shader>("Course/Shaders/GolfSurf");
        static Shader LavaShader => lavaShader ? lavaShader : lavaShader = Resources.Load<Shader>("Course/Shaders/GolfLava");
        static Shader PlantsShader => plantsShader ? plantsShader : plantsShader = Resources.Load<Shader>("Course/Shaders/GolfPlants");

        /// Does the albedo PNG carry an alpha channel (the per-band smoothness)? The GPU format cannot tell (BC7 / ASTC always have an alpha channel: an RGB PNG would read alpha 1 = smoothness 1, a mirror), so the editor asks the importer
        /// (DoesSourceTextureHaveAlpha); a player trusts the PNGs the editor-side gates verified (generator colour type 6 + GolfLookSmoke / GolfLookGrassCheck GRASS_SHEEN_WIRED).
        static bool AlbedoHasAlpha(string file)
        {
#if UNITY_EDITOR
            var imp = UnityEditor.AssetImporter.GetAtPath("Assets/Resources/" + LookFolder + file + ".png") as UnityEditor.TextureImporter;
            return imp != null && imp.DoesSourceTextureHaveAlpha();
#else
            return true;
#endif
        }

        static Material BuildLit(string name, Spec spec)
        {
            if (UseSurfaceShaders) return BuildSurface(name,spec);
            var shader = LitShader;
            if (!shader) { Debug.LogError("[GolfLook] URP Lit missing from the build (run Golf Arcade -> Set Up Project)"); shader = Shader.Find("Standard"); }
            var m = new Material(shader) { enableInstancing = true };
            var albedo = Load(spec.Tex, "_C", true);
            var color = albedo ? spec.Tint : spec.Fallback;
            color.a = 1;
            m.SetColor("_BaseColor", color); m.color = color;
            if (albedo) { m.SetTexture("_BaseMap", albedo); m.mainTexture = albedo; }
            if (spec.Normal)
            {
                var normal = Load(spec.Tex, "_N", true);
                if (normal) { m.SetTexture("_BumpMap", normal); m.SetFloat("_BumpScale", spec.BumpScale); m.EnableKeyword("_NORMALMAP"); }
            }
            bool sheen = spec.SmoothnessFromAlbedoAlpha && albedo && AlbedoHasAlpha(spec.Tex + "_C");
            if (spec.SmoothnessFromAlbedoAlpha && !sheen) Warn(name + "#alpha", $"[GolfLook] {name}: the albedo has no alpha channel: flat smoothness {spec.SmoothnessNoAlpha} (no per-band sheen)");
            m.SetFloat("_Smoothness", spec.SmoothnessFromAlbedoAlpha && !sheen ? spec.SmoothnessNoAlpha : spec.Smoothness);
            if (sheen) { m.SetFloat("_SmoothnessTextureChannel", 1); m.EnableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A"); }
            m.SetFloat("_Metallic", 0);
            m.SetFloat("_EnvironmentReflections", spec.NoSpecular ? 0 : 1);
            if (spec.NoSpecular)
            {
                m.SetFloat("_SpecularHighlights", 0);
                m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF"); m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            }
            if (spec.Emissive)
            {
                var emission = Load(spec.Tex, "_E", true);
                if (emission) m.SetTexture("_EmissionMap", emission);
                // no map: the whole surface glows dimly in the fallback colour (lava stays lava, basalt stays dark)
                var glow = emission ? spec.Emission : (Color)(spec.Fallback.linear * (name == "LK_LAVA" ? 1.3f : 0f));
                // SetVector, not SetColor: the multiplier is LINEAR as written (SetColor would gamma-convert it, x^2.2 per channel)
                m.SetVector("_EmissionColor", new Vector4(glow.r, glow.g, glow.b, 1));
                if (glow.maxColorComponent > 0) m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            return m;
        }

        /// LK_PLANTS: GolfArcade/GolfPlants = the URP Lit look the plants always had (palette atlas x tint, smoothness, metallic 0, no normal map) plus the vertex sway of
        /// RUNTIME.md "v2 2026-10-05 area U". If the shader is missing or unsupported the plants fall back to the plain (static) URP Lit material, never to pink.
        static Material BuildPlants(string name, Spec spec)
        {
            var shader = PlantsShader;
            if (!shader || !shader.isSupported)
            {
                Debug.LogError("[GolfLook] GolfArcade/GolfPlants not found/supported: static URP Lit for " + name);
                return BuildLit(name, spec);
            }
            var m = new Material(shader) { enableInstancing = true };
            var albedo = Load(spec.Tex, "_C", true);
            var color = albedo ? spec.Tint : spec.Fallback;
            color.a = 1;
            m.SetColor("_BaseColor", color); m.color = color;
            if (albedo) { m.SetTexture("_BaseMap", albedo); m.mainTexture = albedo; }
            m.SetFloat("_Smoothness", spec.Smoothness);
            m.SetFloat("_Metallic", 0);
            return m;
        }

        static Material BuildWater(string name, Spec spec)
        {
            var shader = WaterShader;
            if (!shader || !shader.isSupported)
            {
                Debug.LogError("[GolfLook] GolfArcade/TennisWater not found/supported: flat deep blue instead");
                var flat = new Material(LitShader) { enableInstancing = true };
                flat.SetColor("_BaseColor", spec.Deep); flat.SetFloat("_Smoothness", .6f);
                return flat;
            }
            var m = new Material(shader);
            // the shader works in linear space (half4 colour properties are converted on SetColor in a linear project)
            m.SetColor("_Shallow", spec.Shallow); m.SetColor("_Deep", spec.Deep); m.SetColor("_Sky", spec.Sky);
            m.SetFloat("_DeepDistance", spec.DeepDistance); m.SetFloat("_WaveScale", spec.WaveScale); m.SetFloat("_Sparkle", spec.Sparkle);
            return m;
        }

        /// LK_LAVA: GolfArcade/GolfLava (self-lit ramp, static). If the shader is missing the lava falls back to URP Lit + emission (the old look).
        static Material BuildLava(string name, Spec spec)
        {
            var shader = LavaShader;
            if (!shader || !shader.isSupported)
            {
                Debug.LogError("[GolfLook] GolfArcade/GolfLava not found/supported: URP Lit + emission fallback for " + name);
                return BuildLit(name, spec);
            }
            var m = new Material(shader) { enableInstancing = false };
            var albedo = Load(spec.Tex, "_C", true); var glow = Load(spec.Tex, "_E", true); var normal = Load(spec.Tex, "_N", true);
            if (albedo) m.SetTexture("_BaseMap", albedo);
            if (glow) m.SetTexture("_EmissionMap", glow);
            if (normal) { m.SetTexture("_BumpMap", normal); m.SetFloat("_BumpScale", spec.BumpScale); }
            if (!albedo && !glow) { foreach (var stop in new[] { "_Deep", "_Crust", "_Flow", "_Hot" }) m.SetColor(stop, spec.Fallback); return m; }   // no textures: one flat orange, never pink
            m.SetColor("_Deep", spec.RampDeep); m.SetColor("_Crust", spec.RampCrust); m.SetColor("_Flow", spec.RampFlow); m.SetColor("_Hot", spec.RampHot);
            m.SetFloat("_WorldUV", spec.WorldUV ? 1 : 0); m.SetFloat("_WorldTile", LavaTilePerYard);
            m.SetFloat("_FogShare", spec.FogShare);
            m.SetFloat("_HeatAlbedo", spec.HeatAlbedo); m.SetFloat("_HeatGlow", spec.HeatGlow); m.SetFloat("_HeatBias", spec.HeatBias); m.SetFloat("_HeatGain", spec.HeatGain); m.SetFloat("_Relief", spec.Relief);
            return m;
        }

        static Material BuildSurf(string name, Spec spec)
        {
            var shader = SurfShader;
            if (!shader || !shader.isSupported)
            {
                Debug.LogError("[GolfLook] GolfArcade/GolfSurf not found/supported: using URP Unlit (opaque) for " + name);
                return new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = spec.Fallback };
            }
            var m = new Material(shader) { enableInstancing = true };
            var tex = Load(spec.Tex, "_C", true);
            if (tex) m.SetTexture("_MainTex", tex);
            var tint = tex ? spec.Tint : spec.Fallback;
            m.SetColor("_Color", tint);
            m.SetTextureScale("_MainTex", spec.Tiling);
            m.SetFloat("_VertexAlpha", spec.VertexAlpha);
            m.SetFloat("_AlphaPower", spec.AlphaPower);
            m.SetFloat("_AlphaGain", spec.AlphaGain);
            m.SetFloat("_Lit", spec.Lit);
            m.SetFloat("_SunShare", spec.SunShare);
            m.SetFloat("_Emission", spec.SelfLight);
            m.SetFloat("_Wrap", spec.Wrap);
            m.SetFloat("_EdgeFade", spec.EdgeFade);
            m.SetFloat("_FogShare", spec.FogShare);
            m.SetFloat("_SoftFade", spec.SoftFade);
            return m;
        }

        /// Forget the cached materials (the smoke test and a texture re-import use it).
        public static void ClearCache()
        {
            foreach (var m in cache.Values) if (m) Object.DestroyImmediate(m);
            cache.Clear(); warned.Clear();
        }

        static bool IsSurf(Material m) => m && m.shader && m.shader.name == "GolfArcade/GolfSurf";

        /// Dress an instantiated postcard model: LK_ materials swapped (idempotent with HoleView's own swap),
        /// renderer settings (shadows, receive shadows), GPU instancing on every LK_ material, and one static
        /// orange point light per LAVA_LIGHT_nn empty. Never adds a collider. Not for hole 7.
        public static void DressModel(GameObject model, int holeNumber)
        {
            if (!model || !IsPostcard(holeNumber)) return;
            if(UseSurfaceShaders)GolfSurfaceEdges.Apply(model);
            int swapped = 0, lights = 0, densified = 0;
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                bool changed = false, transparent = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (!mats[i]) continue;
                    if (Handles(mats[i].name))
                    {
                        var want = GetForHole(mats[i].name, holeNumber);
                        if (mats[i] != want) { mats[i] = want; changed = true; swapped++; }
                    }
                    if (IsSurf(mats[i])) transparent = true;
                }
                if (changed) r.sharedMaterials = mats;
                string n = r.name;
                if (n.StartsWith("WATER") && r is MeshRenderer && IsTennisWater(mats)) densified += Densify(r.GetComponent<MeshFilter>()) ? 1 : 0;
                bool noShadow = transparent || n.StartsWith("WATER") || n.StartsWith("LAVA") || n.StartsWith("DRESS_SMOKE") || n.StartsWith("DRESS_CONE");
                r.shadowCastingMode = noShadow ? ShadowCastingMode.Off : ShadowCastingMode.On;
                r.receiveShadows = !transparent && !n.StartsWith("DRESS_CONE");
            }
            foreach (var t in model.GetComponentsInChildren<Transform>(true))
            {
                if (!t.name.StartsWith("LAVA_LIGHT")) continue;
                if (t.GetComponentInChildren<Light>(true)) continue;   // already dressed (the light child also starts with LAVA_LIGHT)
                var go = new GameObject(t.name + " light");
                go.transform.SetParent(t, false);
                go.transform.position = t.position + Vector3.up * LavaLightLift;   // world up: the empty's own axes come from the FBX
                var light = go.AddComponent<Light>();              // lights ignore transform scale: range/intensity are world (yards)
                light.type = LightType.Point;
                light.color = LavaLightColor;
                light.range = LavaLightRange;
                light.intensity = LavaLightIntensity;
                light.shadows = LightShadows.None;
                light.renderMode = LightRenderMode.ForcePixel;
#if UNITY_EDITOR
                light.lightmapBakeType = LightmapBakeType.Realtime;   // editor-only API; runtime-created lights are already realtime
#endif
                lights++;
            }
            if (holeNumber == 10 && lights == 0 && FindLavaRenderer(model) != null)
                Debug.Log("[GolfLook] hole 10 has no LAVA_LIGHT_nn empties: the lava lights only by emission + the orange trilight ground");
            if(UseSurfaceShaders){GolfSurfaceBatching.Apply(model);GolfPlantInstances.Apply(model);}
            if (swapped > 0 || lights > 0 || densified > 0) Debug.Log($"[GolfLook] hole {holeNumber}: {swapped} LK_ material slot(s) dressed, {lights} lava light(s), {densified} low-poly water mesh(es) densified");
        }

        static bool IsTennisWater(Material[] mats)
        {
            foreach (var m in mats) if (m && m.shader && m.shader.name == "GolfArcade/TennisWater") return true;
            return false;
        }

        /// TennisWater (frozen) computes linear fog PER VERTEX and saturates it there: on a few huge triangles (the old
        /// 4-vertex WATER_OCEAN) every vertex sits past the fog end and the whole sea comes out ~50 % fog colour (measured:
        /// (136,161,192) with fog, (25,85,147) without, 60-130 yd from the camera). postcard_look_lib builds WATER_OCEAN as a
        /// graded grid; this is the runtime safety for a low-poly water mesh only (<= 64 triangles): each triangle becomes an
        /// n x n barycentric grid with ONE n for the whole mesh (shared edges get the same points: no T-junctions),
        /// cells <= ~60 yd, <= 60k triangles. Water never collides, so swapping its mesh is safe.
        public static float WaterCellYards = 60f;
        static readonly Dictionary<Mesh, Mesh> densifiedMeshes = new();

        static bool Densify(MeshFilter mf)
        {
            if (!mf || !mf.sharedMesh) return false;
            var src = mf.sharedMesh;
            if (densifiedMeshes.TryGetValue(src, out var done) && done) { mf.sharedMesh = done; return true; }
            if (!src.isReadable) return false;
            var tris = src.triangles;
            int count = tris.Length / 3;
            if (count == 0 || count > 64) return false;
            var v = src.vertices; var nrm = src.normals; var uv = src.uv;
            bool hasN = nrm != null && nrm.Length == v.Length, hasUV = uv != null && uv.Length == v.Length;
            float longest = 0;
            for (int t = 0; t < tris.Length; t += 3)
                for (int e = 0; e < 3; e++)
                    longest = Mathf.Max(longest, mf.transform.TransformVector(v[tris[t + (e + 1) % 3]] - v[tris[t + e]]).magnitude);
            int n = Mathf.Clamp(Mathf.CeilToInt(longest / WaterCellYards), 1, Mathf.Max(1, Mathf.FloorToInt(Mathf.Sqrt(60000f / count))));
            if (n <= 1) return false;
            var pos = new List<Vector3>(); var nor = new List<Vector3>(); var uvs = new List<Vector2>(); var idx = new List<int>();
            for (int t = 0; t < tris.Length; t += 3)
            {
                int a = tris[t], b = tris[t + 1], c = tris[t + 2];
                int start = pos.Count;
                for (int i = 0; i <= n; i++)          // row i: weight of b = i/n
                    for (int j = 0; j <= n - i; j++)  // weight of c = j/n
                    {
                        float wb = i / (float)n, wc = j / (float)n, wa = (n - i - j) / (float)n;
                        pos.Add(v[a] * wa + v[b] * wb + v[c] * wc);
                        if (hasN) nor.Add((nrm[a] * wa + nrm[b] * wb + nrm[c] * wc).normalized);
                        if (hasUV) uvs.Add(uv[a] * wa + uv[b] * wb + uv[c] * wc);
                    }
                int Row(int i) => start + i * (n + 1) - i * (i - 1) / 2;   // first vertex of row i
                for (int i = 0; i < n; i++)
                    for (int j = 0; j < n - i; j++)
                    {
                        int p0 = Row(i) + j, p1 = Row(i + 1) + j, p2 = p0 + 1;
                        idx.Add(p0); idx.Add(p1); idx.Add(p2);
                        if (j < n - i - 1) { int p3 = Row(i + 1) + j + 1; idx.Add(p2); idx.Add(p1); idx.Add(p3); }
                    }
            }
            var mesh = new Mesh { name = src.name + " (densified)", indexFormat = pos.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
            mesh.SetVertices(pos);
            if (hasN) mesh.SetNormals(nor);
            if (hasUV) mesh.SetUVs(0, uvs);
            mesh.SetTriangles(idx, 0);
            if (!hasN) mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            densifiedMeshes[src] = mesh;
            mf.sharedMesh = mesh;
            return true;
        }

        static Renderer FindLavaRenderer(GameObject model)
        {
            foreach (var r in model.GetComponentsInChildren<Renderer>(true)) if (r.name.StartsWith("WATER_LAVA")) return r;
            return null;
        }
    }
}
