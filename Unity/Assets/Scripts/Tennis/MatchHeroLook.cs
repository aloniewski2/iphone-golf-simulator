using UnityEngine;

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

        // ---- kit colours (DRESS_MATCH_HEROES). Role materials, by exact name; the authored White kit is the default.
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

        // ---- surface (character / cloth shaders). The prefab's imported materials are flat URP Lit; at Awake each hero's own copies move onto the
        // project's TennisCharacter (body, eyes, face decals, racket frame and strings) and TennisCloth (the kit and the racket grip). Roster tints still land in _BaseColor.
        // HERO_DETAIL: skin = smoothness .2, a very weak soft normal; cloth = a seam / collar / hem map + a tiled weave normal + thread break + a grazing sheen
        // (no highlight dot); the decals, grip and strings are off URP Lit. The maps are set HERE (the material assets stay flat Lit: MatchHeroKitTests checks them).
        public const float SkinSmoothness = .2f, SkinWrap = .55f, SkinRim = .35f, EyeSmoothness = .85f, FrameSmoothness = .65f, FrameRim = .2f;
        /// The skin's normal: a soft break in the light, not pores. Strength on the unpacked slope, and tiles per metre (one 256 px tile = 0.17 m).
        public const float SkinBump = .18f, SkinBumpTile = 6f;
        public const float ClothSmoothness = .10f, ClothBlackSmoothness = .25f, ClothWrap = .5f, ClothRim = .2f;
        public const float GripSmoothness = .05f, StringSmoothness = .15f, DecalWrap = .3f;
        /// The male shirt's UV islands are turned ~41 deg to world-up (measured from Male_Kit.fbx: area-weighted direction of up in UV space, -40..-44 deg); turning the weave by +41 puts its threads
        /// vertical / horizontal on the cloth. The female shirt measures -0.2 deg and the other pieces are axis aligned, so they keep 0.
        public const float MaleShirtWeaveAngle = 41f;
        public static readonly Color SkinSubsurface = new Color(.18f, .06f, .03f, 1);
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
                if (bump && SkinBumpMap)
                {
                    // the body has no UVs: the soft normal is projected from the bind-pose position in mesh channel 1 (WithBindPose)
                    m.SetTexture("_BumpMap", SkinBumpMap); m.SetFloat("_BumpScale", SkinBump); m.SetFloat("_BumpTriplanar", 1); m.SetFloat("_BumpTile", SkinBumpTile);
                    m.EnableKeyword("_NORMALMAP");
                }
            }
            return m;
        }
        static Material EyeSurface(Material m)
        {
            if (m.shader == CharacterShader) m.SetFloat("_Smoothness", EyeSmoothness);
            return m;
        }
        /// The painted brows, lids, lips, nostrils and the seam: stickers on the skin. They keep their own colour and their own (authored) smoothness, light like the skin
        /// light a little wrapped (DecalWrap, no subsurface) and take no weave and no skin normal.
        static Material DecalSurface(Material m, Material source)
        {
            if (m.shader == CharacterShader)
            {
                if (source.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", source.GetFloat("_Smoothness"));
                m.SetFloat("_Wrap", DecalWrap); m.SetFloat("_RimStrength", .1f);   // a sticker is lit nearly like the flat Lit paint it was: the skin's wrap + warm subsurface would wash the lips and brows pale
                m.SetFloat("_Saturation", 1);   // "keep their colours": the character shader's skin desaturation is not for paint
            }
            return m;
        }
        static Material FrameSurface(Material m)
        {
            if (m.shader == CharacterShader) { m.SetFloat("_Smoothness", FrameSmoothness); m.SetFloat("_RimStrength", FrameRim); }
            return m;
        }
        /// Racket strings: the character shader, a touch smoother than the grip, no weave and no normal.
        static Material StringsSurface(Material m)
        {
            if (m.shader == CharacterShader) { m.SetFloat("_Smoothness", StringSmoothness); m.SetFloat("_Wrap", .2f); m.SetFloat("_RimStrength", .1f); m.SetFloat("_Saturation", 1); }
            return m;
        }
        /// Racket grip: cloth, dark, rougher than the shirt. The racket meshes have no UVs, so no weave (it would read one texel).
        static Material GripSurface(Material m)
        {
            if (m.shader == ClothShader)
            {
                m.SetFloat("_Smoothness", GripSmoothness); m.SetFloat("_Wrap", ClothWrap); m.SetFloat("_RimStrength", .1f);
                m.SetFloat("_WeaveNormal", 0); m.SetFloat("_WeaveThread", 0); m.SetFloat("_SheenStrength", .15f);
            }
            return m;
        }

        /// Cloth numbers + maps for one kit role on one kit piece. `tag` = Top, Bottom, ShoeL, ShoeR, SockL, SockR (the seam maps are per piece: left and right unwrap differently).
        Material ClothSurface(Material m, string role, string tag, Mesh mesh, int sub, float scale)
        {
            if (m.shader != ClothShader) return m;
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
                m.SetColor("_TrimColor", new Color(KitBlackR, KitBlackR, KitBlackB, 1));
                m.SetFloat("_WeaveNormal", look.weaveNormal * .8f); m.SetFloat("_WeaveThread", look.thread * .85f);
                // New construction UVs use the fabric atlas at their authored orientation.
                // The legacy 41-degree male correction remains above for old garments.
                m.SetFloat("_WeaveAngle", 0);
                m.SetFloat("_Exposure", tag.StartsWith("Shoe") ? .78f : 1.05f);
            }
            else
            {
                var seams = Resources.Load<Texture2D>(garment);
                if (seams && m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", seams);
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

        Material[] skinMaterials; Material frameMaterial; Color frameDefault = Color.white;
        // role -> this hero's material(s) for it: one per kit piece that uses the role (the left and right shoe / sock carry their own seam maps), all recoloured together
        readonly System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<Material>> kitMaterials = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<Material>>();
        readonly System.Collections.Generic.Dictionary<string, Material> kitByPiece = new System.Collections.Generic.Dictionary<string, Material>();
        readonly System.Collections.Generic.Dictionary<string, Color> kitAuthored = new System.Collections.Generic.Dictionary<string, Color>();
        readonly System.Collections.Generic.List<UnityEngine.Object> owned = new System.Collections.Generic.List<UnityEngine.Object>();

        /// Kept so the driver and legacy callers read it like the old look (no hands are posed here: the grip is baked into the clips).
        public bool externalAnimation = true;
        public void ApplyHands(int rollStroke, float rollWeight, bool leftGrip) { }

        void Awake() { OwnMaterials(); }

        /// This hero's own copies of the shared skin and frame materials, so a tint never reaches the prefab asset or another hero.
        void OwnMaterials()
        {
            if (skinMaterials != null) return;
            if (body)
            {
                // the soft skin normal needs the bind-pose position in mesh channel 1 (the body has no UVs): a shared copy of the body mesh carries it
                var bound = WithBindPose(body.sharedMesh); bool bump = bound != null;
                if (bump) body.sharedMesh = bound;
                var shared = body.sharedMaterials; skinMaterials = new Material[shared.Length];
                for (int i = 0; i < shared.Length; i++)
                {
                    skinMaterials[i] = shared[i] ? SkinSurface(OnShader(shared[i], CharacterShader, shared[i].name + " (hero)"), bump) : null;
                    if (skinMaterials[i]) owned.Add(skinMaterials[i]);
                }
                body.sharedMaterials = skinMaterials;
            }
            else skinMaterials = new Material[0];
            if (face)
            {
                // The eyes get the glossy eye surface; every other face material is a painted decal (brows, lids, lips, nostrils, seam) and moves onto the
                // character shader with its own colour, no weave and no skin smoothness.
                var fm = face.sharedMaterials; bool changed = false;
                for (int i = 0; i < fm.Length; i++)
                {
                    if (!fm[i]) continue;
                    var src = fm[i];
                    fm[i] = IsEye(src) ? EyeSurface(OnShader(src, CharacterShader, src.name + " (hero)")) : DecalSurface(OnShader(src, CharacterShader, src.name + " (hero)"), src);
                    owned.Add(fm[i]); changed = true;
                }
                if (changed) face.sharedMaterials = fm;
            }
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

        /// The skin tone on the whole body (the face features are painted on top and keep their own colours).
        public void SetSkin(Color tone)
        {
            OwnMaterials(); tone.a = 1; skinTone = tone;
            foreach (var m in skinMaterials) if (m) { if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", tone); if (m.HasProperty("_Color")) m.SetColor("_Color", tone); }
        }

        /// A roster / locker racket colour on the frame; alpha 0 leaves the classic white frame.
        public void SetRacketColour(Color c)
        {
            OwnMaterials(); if (!frameMaterial) return;
            frameMaterial.SetColor("_BaseColor", c.a > 0 ? new Color(c.r, c.g, c.b, 1) : frameDefault);
        }

        /// Recolour the kit by role from the launch message / locker picks (hex strings without '#'; null or empty leaves that group at the authored White kit).
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
                var trim = picked ? KitTint(KitDerive(pick)) : new Color(KitBlackR, KitBlackR, KitBlackB, 1);
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
