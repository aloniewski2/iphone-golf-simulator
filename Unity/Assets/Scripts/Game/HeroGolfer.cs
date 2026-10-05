using System.Collections.Generic;
using UnityEngine;

namespace GolfArcade.Game
{
    /// Who the Hero looks like. Stable ids, never positions in a list that might be reordered
    /// (docs/hero-golfer.md); the game's saved looks map to these.
    public struct HeroLook
    {
        public bool Female;
        /// Skin tone, hair colour: any colour (the pickers offer palettes).
        public Color Skin, HairColor;
        public int Haircut;      // HeroGolfer.Haircut
        public int Headwear;     // HeroGolfer.Headwear
        public int Glasses;      // HeroGolfer.GlassesNames (0 none)
        public int Facial;       // HeroGolfer.FacialNames (0 none)
        public int Top, Bottom;  // HeroGolfer.TopNames, BottomNames
        /// Colours of the shirt, the shorts (and the trim), the shoes and the hat; null keeps the kit as designed.
        public Color? Shirt, Shorts, Shoes, Hat;
    }

    /// The golfer: Adnan's match hero (HERO_MAINSTAY from his newmapsandmenus branch), the grey-bodied, painted-faced
    /// boy or girl in the white tennis kit, swinging the game's golf clips. One FBX per sex (blender/scripts/matchhero_golf.py:
    /// "Hero/golfer_m" and "Hero/golfer_f") carries the 53-bone rig, the body, the face, the kit (polo, shorts or skort, socks,
    /// shoes), his default hair, the four clubs and the nine golf takes, so there is nothing to assemble: this class makes a
    /// HeroKit material for every material name the FBX uses and dresses the look on them.
    ///
    /// The kit is the extension point the earlier icon avatar had (docs/avatar-kit.md): more haircuts, hats, glasses, facial
    /// hair, tops and bottoms are rigid or skinned parts named Hair_*, Hat_*, Glasses_*, Face_*, Top_*, Bottom_* in the FBX
    /// (or a part FBX next to it), listed in the arrays below at the END; `Dress` shows the one that is picked. His head is not
    /// the icon head, so until those parts are fitted to it the lists hold what the FBX has: his hair, or none.
    public sealed class HeroGolfer
    {
        // The styles, in id order: the position is the id saved on the profile, so a new style goes at the END of its list and
        // nothing is ever reordered. A name is the part the FBX carries (Hair_<Name>...), "Classic" being his own hair (with a scalp cap under it).
        public enum Haircut { Classic, Bald, Short, Curly, Long, Tail }
        public enum Headwear { None }
        public static readonly string[] HaircutNames = { "Classic", "Bald", "Short", "Curly", "Long", "Tail" };
        public static readonly string[] HeadwearNames = { "None" };
        public static readonly string[] GlassesNames = { "None" };
        public static readonly string[] FacialNames = { "None" };
        public static readonly string[] TopNames = { "Polo" };
        public static readonly string[] BottomNames = { "Shorts" };
        /// The cuts the game offers (all of them).
        public const int OfferedHaircuts = 6;

        /// A style's name as a person reads it ("Flat cap", "Cat-eye", "Top hat").
        public static string Pretty(string name) => name switch
        {
            "Flatcap" => "Flat cap", "Tophat" => "Top hat", "Cateye" => "Cat-eye", "Mustache" => "Moustache", "Wrap" => "Sport",
            _ => name,
        };

        /// The golfer's FBX (rig, parts, clubs, clips) and, beside it, "<path>_clips" (the clips' landmarks).
        public static string PathFor(bool female) => female ? "Hero/golfer_f" : "Hero/golfer_m";

        public GameObject Root { get; private set; }
        public readonly Dictionary<string, Transform> Bones = new();
        readonly List<SkinnedMeshRenderer> parts = new();
        readonly List<Material> owned = new();
        /// One material per name the FBX gave (Kit_Shirt, Face_Iris, Hair_M ...).
        readonly Dictionary<string, Material> roles = new();
        /// Each renderer's materials as the FBX gave them (the names decide what they become, every time a look is put on).
        readonly Dictionary<SkinnedMeshRenderer, Material[]> original = new();
        bool female;

        static Texture2D matCap;

        // ---- the colours, as Adnan authored them (sRGB; his material files hold them linear)
        const string AuthoredSkinM = "F2D0AF", AuthoredSkinF = "F3DAC9";
        /// The painted face: each layer's colour for the boy and the girl, whether it follows the skin tone (a seam, a lip, a nostril is
        /// the skin a shade deeper: its ratio to his authored skin is kept on any tone), and how far it sits in front of the skin (depth offset).
        static readonly (string name, string boy, string girl, bool skin, int front)[] Decals =
        {
            ("Face_Seam", "AD7659", "B88676", true, 1), ("Face_LidCrease", "9E7C69", "957365", true, 1), ("Face_LidLower", "957365", "957365", true, 1),
            ("Face_Nostril", "8B6555", "9B6C5D", true, 1), ("Face_Lip", "F8D1B8", "F1CABA", true, 1), ("Face_LipUp", "E2BA9C", "EAC0AD", true, 2),
            ("Face_Sclera", "E7E3DD", "EFEDEC", false, 1), ("Face_BrowSoft", "817469", "9B8473", false, 1), ("Face_Brow", "73695F", "7C6757", false, 2),
            ("Face_Iris", "957352", "957352", false, 2), ("Face_IrisIn", "A47E5B", "A47E5B", false, 3), ("Face_Limbal", "553C2C", "614530", false, 3),
            ("Face_Pupil", "2C241D", "27211D", false, 4), ("Face_LidLine", "5F4B3F", "4B3F38", false, 4), ("Face_Catch", "FFFFFF", "FFFFFF", false, 5),
        };
        static readonly string[] SkinMaterials = { "Blockout_Grey", "Base_Grey_F", "Skin_F" };
        static readonly string[] HairMaterials = { "Hair_M", "Hair_F", "Hair_Dark" };

        public static bool Available => Resources.Load<GameObject>(PathFor(false)) != null;

        /// The figure, or null when the golfer isn't in the build.
        public static HeroGolfer Build(Transform parent, in HeroLook look)
        {
            var prefab = Resources.Load<GameObject>(PathFor(look.Female));
            if (!prefab) return null;
            // the bodies are skinned by up to four bones a vertex (shoulders, wrists, the fingers); the phone's default quality blends two
            if (QualitySettings.skinWeights != SkinWeights.FourBones && QualitySettings.skinWeights != SkinWeights.Unlimited) QualitySettings.skinWeights = SkinWeights.FourBones;
            var g = new HeroGolfer { female = look.Female };
            g.Root = Object.Instantiate(prefab, parent);
            g.Root.name = "Golfer model";
            foreach (var t in g.Root.GetComponentsInChildren<Transform>(true)) g.Bones[t.name] = t;
            foreach (var smr in g.Root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (smr.name.StartsWith("CLUB_")) continue;
                smr.updateWhenOffscreen = true;
                // smooth skin and the painted face speckle with self-shadow; the face is a thin shell that casts nothing worth having
                bool skin = smr.name is "Body" or "Face";
                smr.receiveShadows = !skin;
                if (smr.name == "Face") smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                g.original[smr] = smr.sharedMaterials;
                g.parts.Add(smr);
            }
            g.MakeMaterials();
            g.Dress(look);
            return g;
        }

        static Color Hex(string hex) => ColorUtility.TryParseHtmlString("#" + hex, out var c) ? c : Color.magenta;

        Material Make(string name, Color color, float capStrength, int front = 0)
        {
            var shader = Shader.Find("GolfArcade/HeroKit");
            if (!shader) return null;
            var m = new Material(shader) { name = name, color = color };
            matCap ??= Resources.Load<Texture2D>("Golfer/Look/clay_matcap");
            if (matCap) m.SetTexture("_MatCap", matCap);
            m.SetFloat("_UseAtlas", 0);
            m.SetFloat("_MatCapStrength", capStrength);
            m.SetFloat("_Wrap", 0.6f);                      // a wide wrap: soft shadows, no black undersides
            m.SetTexture("_Mask", Texture2D.blackTexture);
            // the layers of the painted face sit a hair in front of the skin: a depth offset, deeper for each layer on top
            m.SetFloat("_OffsetFactor", -front); m.SetFloat("_OffsetUnits", -front);
            owned.Add(m);
            return m;
        }

        void MakeMaterials()
        {
            foreach (var n in SkinMaterials) roles[n] = Make(n, Hex(female ? AuthoredSkinF : AuthoredSkinM), 0.5f);
            foreach (var n in HairMaterials) roles[n] = Make(n, Hex("3C3732"), 0.5f);
            foreach (var (name, boy, girl, skin, front) in Decals) roles[name] = Make(name, Hex(female ? girl : boy), skin ? 0.5f : 0.15f, front);
            foreach (var n in new[] { "Kit_Shirt", "Kit_Shorts", "Kit_Shoe", "Kit_Sock" }) roles[n] = Make(n, Hex("F7F7F7"), 0.5f);
            foreach (var n in new[] { "Kit_ShirtTrim", "Kit_ShortsBand" }) roles[n] = Make(n, Hex("353538"), 0.5f);
            roles["Kit_Sole"] = Make("Kit_Sole", Hex("D4D3D0"), 0.5f);
        }

        void Tint(string role, Color c) { if (roles.TryGetValue(role, out var m) && m) { c.a = 1f; m.color = c; } }

        static Color Mix(Color a, Color b, float t) => new(a.r + (b.r - a.r) * t, a.g + (b.g - a.g) * t, a.b + (b.b - a.b) * t, 1f);

        /// Puts the look on: which parts show, their colours.
        public void Dress(in HeroLook look)
        {
            var skin = look.Skin; skin.a = 1f;
            var hair = look.HairColor; hair.a = 1f;
            foreach (var n in SkinMaterials) Tint(n, skin);
            foreach (var n in HairMaterials) Tint(n, n == "Hair_Dark" ? hair * 0.78f : hair);
            // a seam, a lip, a nostril is the skin a shade deeper: his ratio to his own skin, on any tone
            var authored = Hex(female ? AuthoredSkinF : AuthoredSkinM);
            foreach (var (name, boy, girl, follows, _) in Decals)
            {
                if (!follows) continue;
                var d = Hex(female ? girl : boy);
                Tint(name, new Color(Mathf.Clamp01(skin.r * d.r / authored.r), Mathf.Clamp01(skin.g * d.g / authored.g), Mathf.Clamp01(skin.b * d.b / authored.b), 1f));
            }
            // the brows take the hair's colour, a shade deeper (a soft brow is that colour fading into the skin)
            var brow = Mix(hair, Color.black, 0.45f);
            Tint("Face_Brow", brow); Tint("Face_BrowSoft", Mix(brow, skin, 0.5f));
            // the kit as designed: a white polo with dark trim, navy shorts with a dark band, white shoes and socks; a picked colour replaces its own
            var shirt = look.Shirt ?? Hex("F7F7F7");
            var shorts = look.Shorts ?? Hex("1E2B5A");
            Tint("Kit_Shirt", shirt); Tint("Kit_ShirtTrim", TrimOf(shirt));
            Tint("Kit_Shorts", shorts); Tint("Kit_ShortsBand", TrimOf(shorts));
            Tint("Kit_Shoe", look.Shoes ?? Hex("F7F7F7"));

            int cut = Mathf.Clamp(look.Haircut, 0, HaircutNames.Length - 1);
            string hairName = cut == (int)Haircut.Bald ? null : "Hair_" + HaircutNames[cut];
            foreach (var r in parts)
            {
                if (r.name.StartsWith("Hair_")) r.enabled = r.name == hairName;
                AssignMaterials(r);
            }
        }

        /// A hat's colour as designed (there are no hats yet: the kit's place for them).
        public static Color HatDefault(int wear) => new Color(0.95f, 0.95f, 0.93f);

        /// The colour of a garment's trim or band: a deep shade of a light colour, a lighter one of a dark colour.
        public static Color TrimOf(Color c)
        {
            float l = c.r * 0.2126f + c.g * 0.7152f + c.b * 0.0722f;
            return l > 0.55f ? new Color(c.r * 0.22f, c.g * 0.22f, c.b * 0.24f, 1f) : Mix(c, Color.white, 0.4f);
        }

        /// The renderer's materials, by the names the FBX gave them.
        void AssignMaterials(SkinnedMeshRenderer r)
        {
            var mats = original.TryGetValue(r, out var o) ? (Material[])o.Clone() : r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                var m = mats[i];
                if (!m) continue;
                string n = m.name.Replace(" (Instance)", "");
                int dot = n.IndexOf('.'); if (dot > 0) n = n.Substring(0, dot);
                if (roles.TryGetValue(n, out var use) && use) mats[i] = use;
            }
            r.sharedMaterials = mats;
        }

        /// Renderers of a part by name ("Body", "Face", "Kit_Top", "Hair_Classic"...).
        public SkinnedMeshRenderer Part(string name) => parts.Find(r => r.name == name);
        public IReadOnlyList<SkinnedMeshRenderer> Parts => parts;

        public void Dispose()
        {
            foreach (var m in owned) if (m) Object.Destroy(m);
            owned.Clear(); roles.Clear();
            if (Root) Object.Destroy(Root);
        }
    }
}
