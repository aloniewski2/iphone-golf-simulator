using UnityEngine;
using System.Linq;

namespace GolfArcade.Tennis
{
    /// The look of one match hero (HERO_MAINSTAY): the grey-mannequin male or female body from work/match-anim-set, bald, with the
    /// painted face, and the classic racket on its Hand_Racket socket. Resources/Tennis/Customization/PlayerMale|PlayerFemale
    /// (TennisCustomization.HeroBase) are the only place these two meshes are loaded from.
    ///
    /// Identity only, never stats: the locker / roster skin tone tints the body material and a roster racket colour tints the racket
    /// frame. There is no hair and no headwear on these bodies (the old HeroKit hair, visors, caps and sweatbands were built for the old
    /// head). DRESS_MATCH_HEROES: they wear the White tennis kit (polo, shorts / skort, two socks, two shoes: six extra SkinnedMeshRenderers
    /// named Kit_*, skinned to this rig's own bones, built by MatchHeroKit) and SetKit recolours it per role. The rig is a Generic one whose bones are
    /// named after the humanoid bones; the baked FBX clips play on it as their own transform curves (nothing is retargeted).
    public sealed class MatchHeroLook : MonoBehaviour
    {
        public bool female;
        public bool golfKit;
        public const string RoleGolfHead = "Kit_GolfHead", RoleGolfGlove = "Kit_GolfGlove", RoleGolfHardware = "Kit_GolfHardware";
        public void SetGolfHand(bool leftHanded)
        {
            if (!golfKit || kit == null) return;
            foreach (var r in kit) if (r && (r.name == "Kit_Glove_L" || r.name == "Kit_Glove_R"))
                r.enabled = r.name == (leftHanded ? "Kit_Glove_R" : "Kit_Glove_L");
        }
        public Animator animator;
        /// Racket_Classic: its origin is the centre of the usable grip, +Y runs along the shaft towards the head.
        public Transform racketGrip;
        public SkinnedMeshRenderer body;
        public Renderer face;
        /// The racket frame renderer (the roster colour tints this; strings and grip keep their own colour).
        public Renderer racketFrame;
        /// The string bed measured on the racket mesh, in racketGrip-local metres (written by the prefab builder, MatchHeroBuild).
        public Vector3 stringCentreLocal = new Vector3(0, .403f, 0), stringRightLocal = Vector3.right, stringUpLocal = Vector3.up, stringNormalLocal = Vector3.forward;
        public Color skinTone = new Color(.886f, .627f, .384f, 1);
        /// The rig's bones by HumanBodyBones index (the rig is named after the humanoid bones: Hips, LeftUpperArm, RightIndexProximal ...). The clips play on this
        /// skeleton as Generic transform curves, so nothing is retargeted and no Humanoid Avatar is involved. Written by MatchHeroBuild.
        public Transform[] bones;
        public Transform Bone(HumanBodyBones b) { int i = (int)b; return bones != null && i >= 0 && i < bones.Length ? bones[i] : null; }

        /// The six kit SkinnedMeshRenderers (Kit_Top, Kit_Bottom, Kit_Sock_L/R, Kit_Shoe_L/R) written by MatchHeroKit; their materials are the seven roles below.
        public SkinnedMeshRenderer[] kit;

        // ---- kit colours (DRESS_MATCH_HEROES). Role materials, by exact name; the authored starter palette is the default.
        public const string RoleShirt = "Kit_Shirt", RoleShirtTrim = "Kit_ShirtTrim", RoleShorts = "Kit_Shorts", RoleShortsBand = "Kit_ShortsBand", RoleShoe = "Kit_Shoe", RoleSole = "Kit_Sole", RoleSock = "Kit_Sock";
        public static readonly string[] KitRoles = { RoleShirt, RoleShirtTrim, RoleShorts, RoleShortsBand, RoleShoe, RoleSole, RoleSock };
        /// sRGB albedo of the authored White kit (Std_White_Lit linear 0.84809) and of its black contrast band (Std_Black_Lit linear 0.00271 / 0.00271 / 0.0031), also the floor of a tint.
        public const float KitWhite = 0.9300f, KitBlackR = 0.0350f, KitBlackB = 0.0401f;
        /// A picked colour on a near-white kit material: multiply (a white pick is the authored white), never darker than the authored Black kit so folds and seams still read on a black pick.
        public static Color KitTint(Color pick) => new Color(Mathf.Max(KitWhite * pick.r, KitBlackR), Mathf.Max(KitWhite * pick.g, KitBlackR), Mathf.Max(KitWhite * pick.b, KitBlackB), 1);
        /// The trim / band colour that goes with a shirt / shorts pick: darkened 35 % when the pick is light (luminance >= .5), lightened 35 % toward white when it is dark.
        public static Color KitDerive(Color pick)
        {
            float l = pick.r * .2126f + pick.g * .7152f + pick.b * .0722f;
            return l >= .5f ? new Color(pick.r * .65f, pick.g * .65f, pick.b * .65f, 1) : new Color(pick.r + (1 - pick.r) * .35f, pick.g + (1 - pick.g) * .35f, pick.b + (1 - pick.b) * .35f, 1);
        }
        public static bool TryParseKitHex(string hex, out Color c)
        {
            c = default;
            if (string.IsNullOrEmpty(hex)) return false;
            return ColorUtility.TryParseHtmlString(hex.StartsWith("#") ? hex : "#" + hex, out c);
        }

        static readonly Color StarterNavy=new Color(.10f,.17f,.30f,1);
        static readonly Color StarterCream=new Color(.96f,.93f,.85f,1);
        /// Starter appearance only. Explicit saved kit picks still use KitTint and
        /// KitDerive exactly; no clothing ID or persistence schema changes.
        Color StarterShirt=>golfKit ? (female?new Color(.9373f,.5725f,.4980f,1):new Color(.1608f,.7176f,.7294f,1)) : (female?new Color(.7647f,.9020f,.3608f,1):new Color(.8824f,.2980f,.2471f,1));
        Color StarterLower=>golfKit ? StarterNavy : (female?new Color(.96f,.94f,.89f,1):new Color(.17f,.30f,.60f,1));
        Color StarterTrim(string role)=>role==RoleShirt||role==RoleShirtTrim ? StarterShirt*.82f:role==RoleShorts||role==RoleShortsBand ? StarterLower*.82f:role==RoleShoe ? (golfKit?StarterNavy:new Color(.74f,.77f,.80f,1)):StarterCream;
        bool StarterColour(string role,out Color colour) {
            colour=default;
            if(role==RoleShirt)colour=KitTint(StarterShirt);
            else if(role==RoleShirtTrim)colour=KitTint(StarterShirt*.82f);
            else if(role==RoleShorts)colour=KitTint(StarterLower);
            else if(role==RoleShortsBand)colour=KitTint(StarterLower*.82f);
            else if(role==RoleShoe||role==RoleSock||role==RoleGolfGlove)colour=KitTint(golfKit?StarterCream:new Color(.98f,.97f,.94f,1));
            else if(role==RoleSole)colour=KitTint(new Color(.86f,.87f,.82f,1));
            else return false;
            return true;
        }

        // ---- surface (character / cloth shaders). The prefab's imported materials are flat URP Lit; at Awake each hero's own copies move onto the
        // project's TennisCharacter (body, eyes, face decals, racket frame and strings) and TennisCloth (the kit and the racket grip). Roster tints still land in _BaseColor.
        // HERO_DETAIL: skin = smoothness .2, a very weak soft normal; cloth = a seam / collar / hem map + a tiled weave normal + thread break + a grazing sheen
        // (no highlight dot); the decals, grip and strings are off URP Lit. The maps are set HERE (the material assets stay flat Lit: MatchHeroKitTests checks them).
        public const float SkinSmoothness = .38f, SkinWrap = .25f, SkinRim = .075f, EyeSmoothness = .85f, FrameSmoothness = .72f, FrameRim = .075f;
        /// The skin's normal: a soft break in the light, not pores. Strength on the unpacked slope, and tiles per metre (one 256 px tile = 0.17 m).
        public const float SkinBump = 0f, SkinBumpTile = 6f;
        public const float ClothSmoothness = .10f, ClothBlackSmoothness = .25f, ClothWrap = .34f, ClothRim = .065f;
        public const float GripSmoothness = .05f, StringSmoothness = .15f, DecalWrap = .3f;
        /// The male shirt's UV islands are turned ~41 deg to world-up (measured from Male_Kit.fbx: area-weighted direction of up in UV space, -40..-44 deg); turning the weave by +41 puts its threads
        /// vertical / horizontal on the cloth. The female shirt measures -0.2 deg and the other pieces are axis aligned, so they keep 0.
        public const float MaleShirtWeaveAngle = 41f;
        public static readonly Color SkinSubsurface = new Color(.12f, .035f, .02f, 1);
        /// The shoe sole: darker than the white upper (sRGB), and the roughest thing on the kit.
        public static readonly Color KitSole = new Color(KitWhite, KitWhite, KitWhite, 1);
        static readonly string[] EyeParts = { "Sclera", "Iris", "IrisIn", "Pupil", "Limbal", "Catch" };
        static Shader characterShader, clothShader;
        static Shader CharacterShader => characterShader ? characterShader : characterShader = Resources.Load<Shader>("Tennis/Shaders/TennisCharacter");
        static Shader ClothShader => clothShader ? clothShader : clothShader = Resources.Load<Shader>("Tennis/Shaders/TennisCloth");
        static Texture2D weaveMap, skinBumpMap, fabricAtlas, golfFabricAtlas;
        static Texture2D GolfFabricAtlas => golfFabricAtlas ? golfFabricAtlas : golfFabricAtlas = Resources.Load<Texture2D>("Golf/HeroDetail/Golf_FabricAtlas");
        static Texture2D WeaveMap => weaveMap ? weaveMap : weaveMap = Resources.Load<Texture2D>("Tennis/HeroDetail/Cloth_Weave");
        static Texture2D FabricAtlas => fabricAtlas ? fabricAtlas : fabricAtlas = Resources.Load<Texture2D>("Tennis/HeroDetail/Cloth_FabricAtlas");
        static Texture2D SkinBumpMap => skinBumpMap ? skinBumpMap : skinBumpMap = Resources.Load<Texture2D>("Tennis/HeroDetail/Skin_Soft_N");

        /// How one kit role looks on TennisCloth. tileMetres = the size of one weave tile (8 threads); the smoothness only widens a faint broad gloss (cloth has no highlight dot).
        struct ClothLook { public float smooth, tileMetres, weaveNormal, thread, sheen; public ClothLook(float s, float t, float n, float th, float sh) { smooth = s; tileMetres = t; weaveNormal = n; thread = th; sheen = sh; } }
        static ClothLook ClothFor(string role)
        {
            switch (role)
            {
                case RoleShirt: return new ClothLook(ClothSmoothness, .026f, 1.0f, .55f, .55f);
                case RoleShirtTrim: return new ClothLook(ClothBlackSmoothness, .022f, .8f, .35f, .25f);   // the dark piping band, a little sleeker than the cloth
                case RoleShorts: return new ClothLook(ClothSmoothness, .029f, 1.0f, .55f, .50f);
                case RoleShortsBand: return new ClothLook(ClothBlackSmoothness, .022f, .8f, .35f, .20f);
                case RoleShoe: return new ClothLook(.12f, .024f, .8f, .45f, .35f);
                case RoleSole: return new ClothLook(.04f, .020f, .25f, 0f, 0f);                          // rubber: its own, rougher, no weave to speak of
                case RoleSock: return new ClothLook(.08f, .020f, .9f, .55f, .70f);
                default: return new ClothLook(ClothSmoothness, .026f, 1f, .5f, .5f);
            }
        }

        // Golf has coarser pique/twill construction; preserve distinct leather and rubber.
        static ClothLook GolfClothFor(string role)
        {
            switch (role)
            {
                case RoleShirt: return new ClothLook(.10f, .034f, 1.15f, .80f, .32f);
                case RoleShorts: return new ClothLook(.09f, .032f, .95f, .70f, .24f);
                case RoleShoe: return new ClothLook(.18f, .024f, .70f, .45f, .10f);
                case RoleGolfGlove: return new ClothLook(.18f, .022f, .45f, .35f, .10f);
                case RoleGolfHead: return new ClothLook(.08f, .028f, .80f, .55f, .18f);
                default: return ClothFor(role);
            }
        }

        /// A copy of an authored flat material on `shader`, keeping its colour (a missing shader leaves the flat copy, never a pink one).
        static Material OnShader(Material source, Shader shader, string name)
        {
            if (!shader) return new Material(source) { name = name };
            var m = new Material(shader) { name = name };
            m.SetColor("_BaseColor", source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor") : source.HasProperty("_Color") ? source.GetColor("_Color") : Color.white);
            return m;
        }
        static Material SkinSurface(Material m, bool bump)
        {
            if (m.HasProperty("_Smoothness") && m.shader == CharacterShader)
            {
                m.SetFloat("_Smoothness", SkinSmoothness); m.SetFloat("_Wrap", SkinWrap); m.SetFloat("_RimStrength", SkinRim);
                m.SetColor("_Subsurface", SkinSubsurface);
                m.SetFloat("_Saturation", .88f); m.SetFloat("_SpecularStrength", .95f); m.SetFloat("_EnvironmentStrength", .12f);
                if(System.Environment.GetEnvironmentVariable("VISUAL_HERO_FILMIC")=="1")m.SetFloat("_Saturation",1);
                if(System.Environment.GetEnvironmentVariable("VISUAL_HERO_COLOR")=="1"){
                    m.SetFloat("_Saturation",1.10f);m.SetFloat("_Wrap",.18f);
                    m.SetFloat("_Smoothness",.42f);m.SetFloat("_SpecularStrength",1.25f);
                    m.SetFloat("_RimStrength",.045f);m.SetColor("_Subsurface",new Color(.20f,.045f,.02f));
                }
                if (bump && SkinBump > 0 && SkinBumpMap)
                {
                    // the body has no UVs: the soft normal is projected from the bind-pose position in mesh channel 1 (WithBindPose)
                    m.SetTexture("_BumpMap", SkinBumpMap); m.SetFloat("_BumpScale", SkinBump); m.SetFloat("_BumpTriplanar", 1); m.SetFloat("_BumpTile", SkinBumpTile);
                    m.EnableKeyword("_NORMALMAP");
                }
                else { m.DisableKeyword("_NORMALMAP");m.SetFloat("_BumpScale",0);m.SetFloat("_BumpTriplanar",0); }
            }
            return m;
        }
        static Material EyeSurface(Material m)
        {
            if (m.shader == CharacterShader) {
                // Sclera/iris and existing catchlights keep their authored colours. A restrained
                // corneal highlight gives depth without bleaching the face paint.
                m.SetFloat("_Smoothness", EyeSmoothness); m.SetFloat("_Wrap", .18f);
                m.SetFloat("_RimStrength", 0); m.SetFloat("_Saturation", 1);
                m.SetFloat("_SpecularStrength", .55f); m.SetFloat("_EnvironmentStrength", .5f);
            }
            return m;
        }
        /// The painted brows, lids, lips, nostrils and the seam: stickers on the skin. They keep their own colour and their own (authored) smoothness, light like the skin
        /// light a little wrapped (DecalWrap, no subsurface) and take no weave and no skin normal.
        static Material DecalSurface(Material m, Material source)
        {
            if (m.shader == CharacterShader)
            {
                if (source.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", source.GetFloat("_Smoothness"));
                m.SetFloat("_Wrap", DecalWrap); m.SetFloat("_RimStrength", 0);
                m.SetFloat("_SpecularStrength", .25f); m.SetFloat("_EnvironmentStrength", 0);   // a sticker is lit nearly like the flat Lit paint it was: the skin's wrap + warm subsurface would wash the lips and brows pale
                m.SetFloat("_Saturation", 1);   // "keep their colours": the character shader's skin desaturation is not for paint
            }
            return m;
        }
        static Material FrameSurface(Material m)
        {
            if (m.shader == CharacterShader) { m.SetFloat("_Smoothness", FrameSmoothness); m.SetFloat("_RimStrength", FrameRim); m.SetFloat("_Wrap", .18f); m.SetFloat("_Saturation", 1); m.SetFloat("_SpecularStrength", 1.2f); m.SetFloat("_EnvironmentStrength", .65f); }
            return m;
        }
        /// Racket strings: the character shader, a touch smoother than the grip, no weave and no normal.
        static Material StringsSurface(Material m)
        {
            if (m.shader == CharacterShader) { m.SetFloat("_Smoothness", StringSmoothness); m.SetFloat("_Wrap", .2f); m.SetFloat("_RimStrength", 0); m.SetFloat("_SpecularStrength", .45f); m.SetFloat("_EnvironmentStrength", .1f); m.SetFloat("_Saturation", 1); }
            return m;
        }
        /// Racket grip: cloth, dark, rougher than the shirt. The racket meshes have no UVs, so no weave (it would read one texel).
        static Material GripSurface(Material m)
        {
            if (m.shader == ClothShader)
            {
                m.SetFloat("_Smoothness", GripSmoothness); m.SetFloat("_Wrap", ClothWrap); m.SetFloat("_RimStrength", 0);
                m.SetFloat("_WeaveNormal", 0); m.SetFloat("_WeaveThread", 0); m.SetFloat("_SheenStrength", .15f);
            }
            return m;
        }

        /// Cloth numbers + maps for one kit role on one kit piece. `tag` = Top, Bottom, ShoeL, ShoeR, SockL, SockR (the seam maps are per piece: left and right unwrap differently).
        Material ClothSurface(Material m, string role, string tag, Mesh mesh, int sub, float scale)
        {
            if (m.shader != ClothShader) return m;
            string clothSides = System.Environment.GetEnvironmentVariable("VISUAL_CLOTH_TWO_SIDED");
            if (tag == "Top" && (clothSides == "1" || (golfKit && clothSides != "0")))
                m.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
            var look = golfKit ? GolfClothFor(role) : ClothFor(role);
            m.SetFloat("_Smoothness", look.smooth); m.SetFloat("_Wrap", ClothWrap); m.SetFloat("_RimStrength", ClothRim);
            m.SetFloat("_WeaveNormal", look.weaveNormal); m.SetFloat("_WeaveThread", look.thread); m.SetFloat("_SheenStrength", look.sheen);
            if (WeaveMap) m.SetTexture("_WeaveMap", WeaveMap);
            // one weave tile = tileMetres on the cloth, whatever the unwrap's density: tiles per UV unit = metres per UV / tileMetres
            float mpu = MetresPerUv(mesh, sub, scale);
            m.SetFloat("_WeaveTile", mpu > 0 ? mpu / look.tileMetres : 60f);
            if (!female && tag == "Top" && role == RoleShirt) m.SetFloat("_WeaveAngle", MaleShirtWeaveAngle);
            string garment = golfKit ? $"Golf/HeroDetail/Golf_{(female ? "Female" : "Male")}_{tag}" : $"Tennis/HeroDetail/Kit_{(female ? "Female" : "Male")}_{tag}";
            var normal = Resources.Load<Texture2D>(garment + "_N");
            var mask = Resources.Load<Texture2D>(garment + "_M");
            if (normal && mask)
            {
                m.SetTexture("_NormalMap", normal); m.SetTexture("_MaskMap", mask);
                var atlas = golfKit ? GolfFabricAtlas : FabricAtlas;
                if (atlas) m.SetTexture("_WeaveMap", atlas);
                if (golfKit) m.SetFloat("_FabricVersion", 2);
                m.SetFloat("_UseGarmentMaps", 1); m.SetFloat("_NormalStrength", 1);
#if UNITY_EDITOR
                // Capture-only isolation of garment normal-map bake artifacts.
                string topNormalReview = System.Environment.GetEnvironmentVariable("VISUAL_TOP_NORMAL_STRENGTH");
                if (tag == "Top" && float.TryParse(topNormalReview, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float topNormalStrength))
                    m.SetFloat("_NormalStrength", Mathf.Clamp(topNormalStrength, 0, 2));
#endif
                m.SetColor("_TrimColor", new Color(KitBlackR, KitBlackR, KitBlackB, 1));
                m.SetFloat("_WeaveNormal", look.weaveNormal * .8f); m.SetFloat("_WeaveThread", look.thread * .85f);
                // New construction UVs use the fabric atlas at their authored orientation.
                // The legacy 41-degree male correction remains above for old garments.
                m.SetFloat("_WeaveAngle", 0);
                m.SetFloat("_Exposure", tag.StartsWith("Shoe") ? .78f : golfKit ? 1.05f : .84f);
            }
            else
            {
                var seams = Resources.Load<Texture2D>(garment);
                if (seams && m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", seams);
            }
            if (mesh && ((tag == "Top" && mesh.name.StartsWith("TailoredPolo")) || (tag == "Bottom" && mesh.name.StartsWith("TailoredSkirt"))))
            {
                // This source has a newly authored collar: legacy construction-map UVs
                // no longer describe that panel. Shape and neutral fine weave carry it.
                m.SetFloat("_UseGarmentMaps", 0);
                if (WeaveMap) m.SetTexture("_WeaveMap", WeaveMap);
                m.SetFloat("_WeaveTile", mpu > 0 ? mpu / .010f : 120f);
                m.SetFloat("_WeaveAngle", 0); m.SetFloat("_WeaveNormal", .075f);
                m.SetFloat("_WeaveThread", .035f); m.SetFloat("_SheenStrength", .22f);
                m.SetFloat("_Wrap", .26f); m.SetFloat("_RimStrength", .035f);
            }
            return m;
        }

        // metres per UV unit of a kit submesh = sqrt(3D area / UV area). The unwraps are uniform density (+-25 % across islands), so one number per piece does.
        static readonly System.Collections.Generic.Dictionary<long, float> uvDensity = new System.Collections.Generic.Dictionary<long, float>();
        static float MetresPerUv(Mesh mesh, int sub, float scale)
        {
            if (!mesh || !mesh.isReadable || mesh.subMeshCount == 0) return 0;
            sub = Mathf.Clamp(sub, 0, mesh.subMeshCount - 1);
            long key = ((long)mesh.GetInstanceID() << 4) ^ (uint)sub;
            if (!uvDensity.TryGetValue(key, out float d))
            {
                d = 0;
                var v = mesh.vertices; var uv = mesh.uv; var tri = mesh.GetTriangles(sub);
                if (uv.Length == v.Length)
                {
                    double a3 = 0, a2 = 0;
                    for (int t = 0; t + 2 < tri.Length; t += 3)
                    {
                        int i0 = tri[t], i1 = tri[t + 1], i2 = tri[t + 2];
                        a3 += Vector3.Cross(v[i1] - v[i0], v[i2] - v[i0]).magnitude * .5;
                        Vector2 e1 = uv[i1] - uv[i0], e2 = uv[i2] - uv[i0];
                        a2 += Mathf.Abs(e1.x * e2.y - e1.y * e2.x) * .5;
                    }
                    if (a2 > 1e-9) d = (float)System.Math.Sqrt(a3 / a2);
                }
                uvDensity[key] = d;
            }
            return d * scale;
        }

        // The body mesh has no UVs, so its soft normal needs a mapping that rides on the skin: a copy of the mesh with the BIND-POSE position in channel 1.
        // One copy per source mesh, shared by every hero of that sex. Null if the mesh cannot be read (the skin then keeps its own normal).
        static readonly System.Collections.Generic.Dictionary<Mesh, Mesh> bindPoseMeshes = new System.Collections.Generic.Dictionary<Mesh, Mesh>();
        static Mesh WithBindPose(Mesh src)
        {
            if (!src || !src.isReadable) return null;
            if (bindPoseMeshes.TryGetValue(src, out var copy) && copy) return copy;
            copy = Object.Instantiate(src); copy.name = src.name + " (bind pose)"; copy.hideFlags = HideFlags.DontSave;
            copy.SetUVs(1, new System.Collections.Generic.List<Vector3>(src.vertices));
            bindPoseMeshes[src] = copy; return copy;
        }

        static bool IsEye(Material m)
        {
            if (!m) return false;
            string n = m.name; int cut = n.LastIndexOf('_');
            return cut >= 0 && System.Array.IndexOf(EyeParts, n.Substring(cut + 1).Replace(" (hero)", "")) >= 0;
        }

        Material[] skinMaterials; Material[] faceMaterials; Color authoredSkin;
        bool referenceAnatomy, originalSeam;
        readonly System.Collections.Generic.Dictionary<Material, Color> anatomicalPaint = new();
        Material frameMaterial; Color frameDefault = Color.white;
        // role -> this hero's material(s) for it: one per kit piece that uses the role (the left and right shoe / sock carry their own seam maps), all recoloured together
        readonly System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<Material>> kitMaterials = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<Material>>();
        readonly System.Collections.Generic.Dictionary<string, Material> kitByPiece = new System.Collections.Generic.Dictionary<string, Material>();
        readonly System.Collections.Generic.Dictionary<string, Color> kitAuthored = new System.Collections.Generic.Dictionary<string, Color>();
        readonly System.Collections.Generic.List<UnityEngine.Object> owned = new System.Collections.Generic.List<UnityEngine.Object>();

        /// Kept so the driver and legacy callers read it like the old look (no hands are posed here: the grip is baked into the clips).
        public bool externalAnimation = true;
        public void ApplyHands(int rollStroke, float rollWeight, bool leftGrip) { }

        void Awake()
        {
            OwnMaterials();
            if (!GetComponent<HeroGripPolish>()) gameObject.AddComponent<HeroGripPolish>().look = this;
            if (face && !GetComponent<MatchHeroFacePerformance>()) gameObject.AddComponent<MatchHeroFacePerformance>().look = this;
            if (!golfKit && !GetComponent<HeroGarmentLOD>()) gameObject.AddComponent<HeroGarmentLOD>().look = this;
        }

        /// This hero's own copies of the shared skin and frame materials, so a tint never reaches the prefab asset or another hero.
        void OwnMaterials()
        {
            if (skinMaterials != null) return;
            originalSeam = HeroOriginalSeamAnatomy.Apply(this);
            referenceAnatomy = originalSeam || HeroReferenceAnatomy.Apply(this);
            if (!golfKit && kit != null) {
                bool tailoredPolo = System.Environment.GetEnvironmentVariable(female ? "VISUAL_TAILORED_FEMALE_POLO" : "VISUAL_TAILORED_POLO") != "0";
                var fitted=Resources.Load<GameObject>((tailoredPolo ? "Tennis/KitsTailored/" : "Tennis/KitsFitted/")+(female ? "Female":"Male"));
                var src=fitted ? fitted.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(r=>r.name=="Kit_Top"):null;
                var live=kit.FirstOrDefault(r=>r&&r.name=="Kit_Top");
                if(src&&live){var map=GetComponentsInChildren<Transform>(true).GroupBy(t=>t.name).ToDictionary(g=>g.Key,g=>g.First());live.sharedMesh=src.sharedMesh;live.bones=src.bones.Select(b=>map[b.name]).ToArray();if(tailoredPolo){live.sharedMaterials=src.sharedMaterials;live.sharedMesh=Object.Instantiate(src.sharedMesh);live.sharedMesh.name="TailoredPolo32 "+(female?"Female":"Male");owned.Add(live.sharedMesh);}}
            }
            if (!golfKit && female && kit != null && System.Environment.GetEnvironmentVariable("VISUAL_TAILORED_FEMALE_SKIRT") != "0") {
                var source = Resources.Load<GameObject>("Tennis/KitsTailored/FemaleSkirt");
                var src = source ? source.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(r => r.name == "Kit_Bottom") : null;
                var live = kit.FirstOrDefault(r => r && r.name == "Kit_Bottom");
                if (src && live) {
                    var bones = GetComponentsInChildren<Transform>(true).GroupBy(t => t.name).ToDictionary(g => g.Key, g => g.First());
                    live.sharedMesh = Object.Instantiate(src.sharedMesh); live.sharedMesh.name = "TailoredSkirt33 Female"; owned.Add(live.sharedMesh);
                    live.bones = src.bones.Select(b => bones[b.name]).ToArray(); live.sharedMaterials = src.sharedMaterials;
                }
            }
            if (body)
            {
                // the soft skin normal needs the bind-pose position in mesh channel 1 (the body has no UVs): a shared copy of the body mesh carries it
                body.sharedMesh=HeroHandWeights.Prepare(body,female);
                body.sharedMesh=HeroGarmentAxillaWeights.Prepare(body,female);
                if (!referenceAnatomy) {
                    body.sharedMesh=HeroHeadVolume.Prepare(this);
                    body.sharedMesh=HeroSkinNormals.Prepare(body,Bone(HumanBodyBones.Head));
                }
                else if(originalSeam && (string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("VISUAL_CHARACTER_NORMALS")) || System.Environment.GetEnvironmentVariable("VISUAL_CHARACTER_NORMALS")=="continuous" || System.Environment.GetEnvironmentVariable("VISUAL_CHARACTER_NORMALS")=="soft"))
                    body.sharedMesh=HeroSkinNormals.Prepare(body,Bone(HumanBodyBones.Head),true);
                body.sharedMesh=HeroBlinkNormalField.Prepare(body.sharedMesh);
                var bound = WithBindPose(body.sharedMesh); bool bump = bound != null;
                if (bump) body.sharedMesh = bound;
                var shared = body.sharedMaterials; skinMaterials = new Material[shared.Length];
                authoredSkin = shared.Length > 0 && shared[0] ? (shared[0].HasProperty("_BaseColor") ? shared[0].GetColor("_BaseColor") : shared[0].color) : skinTone;
                // OriginalSeam FBX retains Blender's linear Base Color values.
                // Unity Color properties and locker picks use sRGB authoring values.
                // Convert this imported default once; later SetSkin picks stay exact.
                if (originalSeam) { authoredSkin = authoredSkin.gamma; skinTone = authoredSkin; }
                for (int i = 0; i < shared.Length; i++)
                {
                    skinMaterials[i] = shared[i] ? SkinSurface(OnShader(shared[i], CharacterShader, shared[i].name + " (hero)"), bump) : null;
                    if (skinMaterials[i]) {
                        if(!referenceAnatomy || originalSeam)HeroHeadSurface.Configure(this,skinMaterials[i]);
                        if(originalSeam){
                            skinMaterials[i].SetColor("_BaseColor", skinMaterials[i].GetColor("_BaseColor").gamma);
                            HeroOriginalSeamAnatomy.ConfigurePigment(skinMaterials[i]);
                            skinMaterials[i].SetFloat("_SkinPigmentUV",System.Environment.GetEnvironmentVariable("VISUAL_CHARACTER_LIP_FINISH")=="0"?1:2);
                        }
                        if(System.Environment.GetEnvironmentVariable("VISUAL_CHARACTER_SKIN_FINISH")=="1" ||
                           (originalSeam && System.Environment.GetEnvironmentVariable("VISUAL_CHARACTER_SKIN_FINISH")!="0"))
                            skinMaterials[i].SetFloat("_SkinFinish",female?2:1);
                        owned.Add(skinMaterials[i]);
                    }
                }
                body.sharedMaterials = skinMaterials;
                if(!golfKit)HeroArmSkinning.Build(body);
            }
            else skinMaterials = new Material[0];
            if (face)
            {
                // The body supplies the complete head silhouette. Face decals
                // cast redundant layered shadows (47 draws measured in court A/B).
                face.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                // The eyes get the glossy eye surface; every other face material is a painted decal (brows, lids, lips, nostrils, seam) and moves onto the
                // character shader with its own colour, no weave and no skin smoothness.
                var fm = face.sharedMaterials; bool changed = false;
                for (int i = 0; i < fm.Length; i++)
                {
                    if (!fm[i]) continue;
                    var src = fm[i];
                    bool eye = referenceAnatomy ? new[]{"Sclera","Iris","Pupil","Limbal","Catch"}.Any(n=>src.name.Contains(n)) : IsEye(src);
                    fm[i] = eye ? EyeSurface(OnShader(src, CharacterShader, src.name + " (hero)")) : DecalSurface(OnShader(src, CharacterShader, src.name + " (hero)"), src);
                    if (!referenceAnatomy) ConfigureFaceRole(fm[i], src.name);
                    if(originalSeam)HeroOriginalSeamAnatomy.ConfigureFeature(fm[i],src.name);
                    if(src.name.Contains("Face_Lip")&&!src.name.Contains("LipUp")){fm[i].SetFloat("_Smoothness",.42f);fm[i].SetFloat("_SpecularStrength",.45f);}
                    if (FollowsSkin(src.name)) anatomicalPaint[fm[i]] = fm[i].GetColor("_BaseColor");
                    owned.Add(fm[i]); changed = true;
                }
                if (changed) face.sharedMaterials = fm;
                var filter = face.GetComponent<MeshFilter>();
                if (!referenceAnatomy && filter && filter.sharedMesh) {
                    filter.sharedMesh=HeroFaceCraft.Prepare(this,filter.sharedMesh,fm);
                    var rounded=HeroEyeRim.Append(this,filter.sharedMesh);
                    if(rounded){filter.sharedMesh=rounded;owned.Add(rounded);var rim=SkinSurface(OnShader(skinMaterials[0],CharacterShader,"Face_EyelidRim (hero)"),false);rim.SetFloat("_FaceRole",9);rim.SetFloat("_PaintLift",.00005f);owned.Add(rim);fm=fm.Concat(new[]{rim}).ToArray();face.sharedMaterials=fm;}
                    var fitted = HeroLidGeometry.Append(this,filter.sharedMesh,fm);
                    if (fitted) {
                        filter.sharedMesh=fitted;owned.Add(fitted);
                        var lid=SkinSurface(OnShader(skinMaterials[0],CharacterShader,"Face_Closure (hero)"),false);
                        lid.SetVector("_HeadOrigin",Vector4.zero);lid.SetFloat("_FaceRole",5);lid.SetFloat("_PaintLift",.0002f);owned.Add(lid);
                        fm=fm.Concat(new[]{lid}).ToArray();face.sharedMaterials=fm;
                    }
                }
                faceMaterials = fm;
                if (!referenceAnatomy) { ConfigureEyeGeometry(); HeroOriginalMaterial.ConfigureFace(this,faceMaterials); }
            }
            HeroGarmentHemWeights.Apply(this);
            HeroGarmentCoverage.Apply(this);
            if (kit != null)
                foreach (var r in kit)
                {
                    if (!r) continue;
                    string tag = r.name.StartsWith("Kit_") ? r.name.Substring(4).Replace("_", "") : r.name;   // Top, Bottom, ShoeL, ShoeR, SockL, SockR
                    float scale = r.transform.lossyScale.x;
                    var ms = r.sharedMaterials;
                    for (int i = 0; i < ms.Length; i++)
                    {
                        if (!ms[i]) continue;
                        string role = ms[i].name.Replace(" (hero)", "");
                        string key = role + "|" + tag;
                        if (!kitByPiece.TryGetValue(key, out var own))
                        {
                            own = ClothSurface(OnShader(ms[i], ClothShader, role + " (hero)"), role, tag, r.sharedMesh, i, scale);
                            if(StarterColour(role,out var starter)){if(own.HasProperty("_BaseColor"))own.SetColor("_BaseColor",starter);if(own.HasProperty("_Color"))own.SetColor("_Color",starter);}
                            if(own.HasProperty("_TrimColor"))own.SetColor("_TrimColor",KitTint(StarterTrim(role)));
                            kitByPiece[key] = own; owned.Add(own);
                            if (!kitMaterials.TryGetValue(role, out var list)) kitMaterials[role] = list = new System.Collections.Generic.List<Material>();
                            list.Add(own);
                            if (!kitAuthored.ContainsKey(role)) kitAuthored[role] = own.HasProperty("_BaseColor") ? own.GetColor("_BaseColor") : own.color;
                        }
                        ms[i] = own;
                    }
                    r.sharedMaterials = ms;
                }
            if (racketFrame && racketFrame.sharedMaterial)
            {
                frameMaterial = FrameSurface(OnShader(racketFrame.sharedMaterial, CharacterShader, racketFrame.sharedMaterial.name + " (hero)"));
                frameDefault = frameMaterial.HasProperty("_BaseColor") ? frameMaterial.GetColor("_BaseColor") : Color.white;
                racketFrame.sharedMaterial = frameMaterial; owned.Add(frameMaterial);
            }
            // The rest of the racket: the grip (and butt cap) is cloth, dark and rougher than the shirt; the strings are the character shader, a touch smoother, no weave.
            if (racketGrip)
            {
                Material grip = null, strings = null;
                foreach (var r in racketGrip.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (r == racketFrame || r.name == "Collider_Racket") continue;
                    if(r.name=="Racket_StringBed")r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                    var ms = r.sharedMaterials; bool changed = false;
                    for (int i = 0; i < ms.Length; i++)
                    {
                        if (!ms[i]) continue;
                        string n = ms[i].name.Replace(" (Instance)", "");
                        if (n.Contains("Grip")) { if (!grip) { grip = GripSurface(OnShader(ms[i], ClothShader, n + " (hero)")); owned.Add(grip); } ms[i] = grip; changed = true; }
                        else if (n.Contains("Strings")) { if (!strings) { strings = StringsSurface(OnShader(ms[i], CharacterShader, n + " (hero)")); owned.Add(strings); } ms[i] = strings; changed = true; }
                    }
                    if (changed) r.sharedMaterials = ms;
                }
            }
            // The hero rim light (HeroRimLight) sits on its own rendering layer so it lights the heroes and never the court.
            foreach (var r in GetComponentsInChildren<Renderer>(true)) r.renderingLayerMask |= HeroRimLight.Layer;
        }

        static bool FollowsSkin(string name) => new[] { "Face_Lip", "Face_LipUp", "Face_Seam", "Face_Nostril", "Face_LidCrease", "Face_LidLower", "Face_BrowSoft" }.Any(n => name.Contains(n));
        static void ConfigureFaceRole(Material m, string name)
        {
            float role = 0, lift = .00015f;
            if (name.Contains("Sclera")) { role = 1; lift = .00008f; }
            else if (new[] { "Iris", "Pupil", "Limbal", "Catch" }.Any(n => name.Contains(n))) { role = 2; lift = name.Contains("Catch") ? .0002f : name.Contains("Pupil") ? .00014f : .00012f; }
            else if ((name.Contains("LidLine") || name.Contains("LidLower") || name.Contains("LidCrease"))) { role = 3; lift = .00012f; }
            else if (name.Contains("Seam")) { role=6;lift=.00008f; }
            else if (name.Contains("LipUp")) { role=7;lift=0; }
            else if (name.Contains("BrowSoft")) { role=8;lift=.00008f; }
            else if (name.Contains("Brow")) { role = 4; lift = name.Contains("BrowSoft") ? .00008f : .00018f; }
            m.SetFloat("_FaceRole", role); m.SetFloat("_PaintLift", lift);
        }
        void ConfigureEyeGeometry()
        {
            var filter = face.GetComponent<MeshFilter>(); if (!filter || !filter.sharedMesh || !filter.sharedMesh.isReadable) return;
            var mesh = filter.sharedMesh; var vertices = mesh.vertices; var normals = mesh.normals; var eyeNormals = new Vector3[2]; var eyeCounts = new int[2];
            HeroFaceBasis.Get(this,out var right,out var up,out _);
            var bounds = new[] { new Bounds(), new Bounds() }; var found = new bool[2];
            var mid = mesh.bounds.center;
            for (int sub = 0; sub < faceMaterials.Length; sub++) if (faceMaterials[sub] && faceMaterials[sub].name.Contains("Sclera"))
                foreach (int i in mesh.GetTriangles(sub)) {
                    int side = Vector3.Dot(vertices[i] - mid, right) < 0 ? 0 : 1;
                    eyeNormals[side] += normals[i]; eyeCounts[side]++;
                    if (!found[side]) { bounds[side] = new Bounds(vertices[i], Vector3.zero); found[side] = true; } else bounds[side].Encapsulate(vertices[i]);
                }
            if (!found[0] || !found[1]) return;
            float Extent(Mesh source,int side,Vector3 axis){float min=float.MaxValue,max=float.MinValue;
                for(int sub=0;sub<faceMaterials.Length;sub++)if(faceMaterials[sub]&&faceMaterials[sub].name.Contains("Sclera"))foreach(int i in source.GetTriangles(sub))if((Vector3.Dot(vertices[i]-mid,right)<0 ? 0:1)==side){float v=Vector3.Dot(vertices[i],axis);min=Mathf.Min(min,v);max=Mathf.Max(max,v);}return max-min;}
            foreach (var m in faceMaterials) if (m) {
                var l = bounds[0].center; var r = bounds[1].center;
                m.SetVector("_EyeL", new Vector4(l.x,l.y,l.z,Extent(mesh,0,up) * .5f)); m.SetVector("_EyeR", new Vector4(r.x,r.y,r.z,Extent(mesh,1,up) * .5f));
                m.SetVector("_EyeNL", new Vector4(eyeNormals[0].normalized.x,eyeNormals[0].normalized.y,eyeNormals[0].normalized.z,Extent(mesh,0,right)*.5f));
                m.SetVector("_EyeNR", new Vector4(eyeNormals[1].normalized.x,eyeNormals[1].normalized.y,eyeNormals[1].normalized.z,Extent(mesh,1,right)*.5f));
                m.SetVector("_FaceUp", up); m.SetVector("_FaceRight", right);
            }
        }
        /// Bounded existing-face deformation shared with the native geometry modifier.
        public void SetFacePerformance(float blink, Vector2 gaze, float browLift = 0)
        {
            OwnMaterials(); if (faceMaterials == null) return;
            if (originalSeam) {
                ApplyOriginalSeamBlink(body, blink);
                if (face is SkinnedMeshRenderer seamFeatures) ApplyOriginalSeamBlink(seamFeatures, blink);
                return;
            }
            int bodyBlink=body&&body.sharedMesh ? body.sharedMesh.GetBlendShapeIndex("Hero_Blink"):-1;
            if(bodyBlink>=0)body.SetBlendShapeWeight(bodyBlink,Mathf.Clamp01(blink)*100);
            if(referenceAnatomy) {
                if(face is SkinnedMeshRenderer features && features.sharedMesh) {
                    int featureBlink=features.sharedMesh.GetBlendShapeIndex("Hero_Blink");
                    if(featureBlink>=0)features.SetBlendShapeWeight(featureBlink,Mathf.Clamp01(blink)*100);
                }
                return;
            }
            gaze = Vector2.ClampMagnitude(gaze, .0018f);
            foreach (var m in faceMaterials) if (m) {
                m.SetFloat("_Blink", Mathf.Clamp01(blink)); m.SetVector("_Gaze", new Vector4(gaze.x, gaze.y, 0, 0));
                m.SetFloat("_BrowLift", Mathf.Clamp(browLift, -.001f, .001f));
            }
        }

        // A physical half-closed target keeps the lid outside the curved eye
        // at mid-blink. Both targets are independent deltas from the open mesh.
        static void ApplyOriginalSeamBlink(SkinnedMeshRenderer skin, float blink)
        {
            if (!skin || !skin.sharedMesh) return;
            float b = Mathf.Clamp01(blink);
            int half = skin.sharedMesh.GetBlendShapeIndex("Hero_Blink_Half");
            int full = skin.sharedMesh.GetBlendShapeIndex("Hero_Blink");
            if (half >= 0) skin.SetBlendShapeWeight(half, Mathf.Max(0, 1 - Mathf.Abs(2 * b - 1)) * 100);
            if (full >= 0) skin.SetBlendShapeWeight(full, (half >= 0 ? Mathf.Max(0, 2 * b - 1) : b) * 100);
        }

        /// The skin tone on the whole body (the face features are painted on top and keep their own colours).
        public void SetSkin(Color tone)
        {
            OwnMaterials(); tone.a = 1; skinTone = tone;
            foreach (var pair in anatomicalPaint) if (pair.Key) {
                var c = pair.Value;
                var relative = new Color(Mathf.Clamp01(tone.r * c.r / Mathf.Max(.01f, authoredSkin.r)), Mathf.Clamp01(tone.g * c.g / Mathf.Max(.01f, authoredSkin.g)), Mathf.Clamp01(tone.b * c.b / Mathf.Max(.01f, authoredSkin.b)), 1);
                float pigment = pair.Key.name.Contains("Seam") ? .18f : pair.Key.name.Contains("LipUp") ? .2f : pair.Key.name.Contains("BrowSoft") ? .45f : 1;
                if(pair.Key.name.Contains("Face_LidLower")||pair.Key.name.Contains("Face_LidCrease"))relative=tone*.96f;
                else if(pair.Key.name.Contains("Face_Lip"))relative=new Color(tone.r*.99f,tone.g*.88f,tone.b*.87f,1);
                relative.a=1;pair.Key.SetColor("_BaseColor", Color.Lerp(tone, relative, pigment));
            }
            foreach (var m in faceMaterials ?? System.Array.Empty<Material>()) if (m) m.SetColor("_FaceSkin", tone);
            foreach (var m in skinMaterials) if (m) {
                Color pigment = referenceAnatomy && m.name.Contains("NaturalLip") ? new Color(tone.r*.99f,tone.g*.86f,tone.b*.84f,1) : tone;
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", pigment); if (m.HasProperty("_Color")) m.SetColor("_Color", pigment);
            }
        }

        /// A roster / locker racket colour on the frame; alpha 0 leaves the classic white frame.
        public void SetRacketColour(Color c)
        {
            OwnMaterials(); if (!frameMaterial) return;
            frameMaterial.SetColor("_BaseColor", c.a > 0 ? new Color(c.r, c.g, c.b, 1) : frameDefault);
        }

        /// Recolour the kit by role from the launch message / locker picks (hex strings without '#'; null or empty returns that group to the starter palette).
        ///   shirt  -> Kit_Shirt, Kit_ShirtTrim (trim derived: darkened 35 % if the pick is light, else lightened 35 %)
        ///   shorts -> Kit_Shorts, Kit_ShortsBand (band derived by the same rule)
        ///   shoes  -> Kit_Shoe   (Kit_Sole and Kit_Sock are never touched)
        /// Every call states the whole look: a group given nothing returns to its authored colour, so re-applying a look after a rebuild is idempotent.
        public void SetKit(string shirtHex, string shortsHex, string shoesHex)
        {
            Color c; var a = new Color(0, 0, 0, 0);
            SetKit(TryParseKitHex(shirtHex, out c) ? c : a, TryParseKitHex(shortsHex, out c) ? c : a, TryParseKitHex(shoesHex, out c) ? c : a);
        }
        /// Same, with colours (alpha 0 = no pick, the TennisLook.Kit / HeroKit.Style convention).
        public void SetKit(Color shirt, Color shorts, Color shoes)
        {
            OwnMaterials();
            void Put(string role, bool picked, Color c)
            {
                if (!kitMaterials.TryGetValue(role, out var list)) return;
                var col = picked ? c : kitAuthored[role]; col.a = 1;
                foreach (var m in list)
                {
                    if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", col);
                    if (m.HasProperty("_Color")) m.SetColor("_Color", col);
                }
            }
            bool hs = shirt.a > 0, hh = shorts.a > 0, ho = shoes.a > 0;
            Put(RoleShirt, hs, KitTint(shirt)); Put(RoleShirtTrim, hs, KitTint(KitDerive(shirt)));
            Put(RoleShorts, hh, KitTint(shorts)); Put(RoleShortsBand, hh, KitTint(KitDerive(shorts)));
            Put(RoleShoe, ho, KitTint(shoes));
            if (golfKit) { Put(RoleGolfHead, hs, KitTint(shirt)); Put(RoleGolfGlove, hs, KitTint(shirt)); Put(RoleGolfHardware, hh, KitTint(KitDerive(shorts))); }
            void Trim(string role, bool picked, Color pick)
            {
                if (!kitMaterials.TryGetValue(role, out var list)) return;
                var trim = picked ? KitTint(KitDerive(pick)) : KitTint(StarterTrim(role));
                foreach (var m in list) if (m.HasProperty("_TrimColor")) m.SetColor("_TrimColor", trim);
            }
            Trim(RoleShirt, hs, shirt); Trim(RoleShirtTrim, hs, shirt);
            Trim(RoleShorts, hh, shorts); Trim(RoleShortsBand, hh, shorts); Trim(RoleShoe, ho, shoes);
            if (golfKit) { Trim(RoleGolfHead, hs, shirt); Trim(RoleGolfGlove, hs, shirt); }
        }
        /// The colour a kit role currently has on this hero (tests and proofs).
        public bool TryGetKitColour(string role, out Color c)
        {
            OwnMaterials();
            if (kitMaterials.TryGetValue(role, out var list) && list.Count > 0) { var m = list[0]; c = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : m.color; return true; }
            c = default; return false;
        }

        void OnDestroy() { foreach (var o in owned) if (o) Destroy(o); owned.Clear(); }
    }
}
