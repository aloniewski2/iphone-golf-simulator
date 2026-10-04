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

    /// The Hero, built from the avatar kit (blender/scripts/avatar_*.py, docs/avatar-kit.md): ONE skeleton
    /// (hero_golf.fbx, the golf clips and the clubs) and every part a skinned mesh in its own FBX, rebound to that
    /// skeleton by bone name: the body (neck, torso, arms, hands, legs), the head and the face on it, the shirt, the
    /// shorts, the shoes, the haircuts, the hats, the glasses. Any slot changes without touching another.
    ///
    /// Nothing is textured. A part's colours are its material roles (Hero_Skin, Hero_Hair, Hero_Top, Hero_HatA ...):
    /// the FBX names them, this class makes one HeroKit material per role and tints it from the look, so one mesh is
    /// every colour. Each haircut ships twice, as it is and with its crown pressed down ("Hair_Bob_Hat"): the second
    /// is shown when a hat is worn, so a hat never meets hair standing up through it.
    public sealed class HeroGolfer
    {
        // The styles, in id order: the position is the id saved on the profile, so a new style goes at the END of its
        // list and nothing is ever reordered. They are the kit's catalog (blender/scripts/avatar_catalog.py), by the names
        // the FBX parts carry (Hair_<Name>, Hat_<Name>, Glasses_<Name>, Face_<Name>, Top_<Name>, Bottom_<Name>).
        public enum Haircut { Swept, Ponytail, Bob, Long, Curly, Bald, Crop, Spikes, Afro, Bun, Pigtails, Mohawk, Quiff, Buzz, Braids }
        public enum Headwear { None, Visor, Cap, Bucket, Beanie, Straw, Headband, Beret, Flatcap, Fedora, Cowboy, Headphones, Bandana, Tophat, Crown }
        public static readonly string[] HaircutNames = { "Swept", "Ponytail", "Bob", "Long", "Curly", "Bald", "Crop", "Spikes", "Afro", "Bun", "Pigtails", "Mohawk", "Quiff", "Buzz", "Braids" };
        public static readonly string[] HeadwearNames = { "None", "Visor", "Cap", "Bucket", "Beanie", "Straw", "Headband", "Beret", "Flatcap", "Fedora", "Cowboy", "Headphones", "Bandana", "Tophat", "Crown" };
        public static readonly string[] GlassesNames = { "None", "Round", "Square", "Shades", "Aviator", "Cateye", "Wrap" };
        public static readonly string[] FacialNames = { "None", "Beard", "Mustache", "Goatee", "Handlebar", "Stubble" };
        public static readonly string[] TopNames = { "Polo", "Tee", "Hoodie", "Vest" };
        public static readonly string[] BottomNames = { "Shorts", "Trousers", "Skirt" };
        /// The cuts the game offers (all of them).
        public const int OfferedHaircuts = 15;

        /// A style's name as a person reads it ("Flat cap", "Cat-eye", "Top hat").
        public static string Pretty(string name) => name switch
        {
            "Flatcap" => "Flat cap", "Tophat" => "Top hat", "Cateye" => "Cat-eye", "Mustache" => "Moustache", "Wrap" => "Sport",
            _ => name,
        };

        public const string Path = "Hero/hero_golf";
        static readonly string[] Slots = { "body", "head", "shirt", "shorts", "shoes", "hair", "hats", "glasses" };

        public GameObject Root { get; private set; }
        public readonly Dictionary<string, Transform> Bones = new();
        readonly List<SkinnedMeshRenderer> parts = new();
        readonly List<Material> owned = new();
        /// One material per colour role (the names Blender gave the materials).
        readonly Dictionary<string, Material> roles = new();
        /// Each renderer's materials as the FBX gave them (the names decide what they become, every time a look is put on).
        readonly Dictionary<SkinnedMeshRenderer, Material[]> original = new();

        static Texture2D matCap;

        // role, the colour as designed (sRGB), how much the MatCap shades it (the clay look)
        static readonly (string role, string hex, float cap)[] Designed =
        {
            ("Hero_Skin", "E8A074", 0.60f), ("Hero_Ear", "D9826A", 0.60f), ("Hero_Blush", "F0806E", 0.60f),
            ("Hero_Hair", "E0A43A", 0.60f), ("Hero_HairB", "C07F22", 0.60f),
            ("Hero_Eye", "1C1412", 0.25f), ("Hero_EyeGlint", "FFFFFF", 0f), ("Hero_MouthIn", "7A1F22", 0.30f),
            ("Hero_Tongue", "E0606A", 0.30f), ("Hero_Teeth", "F7F2EA", 0.25f),
            ("Hero_Top", "F2F3F5", 0.60f), ("Hero_TopTrim", "1B2F6B", 0.60f), ("Hero_Accent", "F0501A", 0.60f),
            ("Hero_Bottom", "1B2F6B", 0.60f), ("Hero_Sock", "F2F3F5", 0.60f), ("Hero_Shoe", "F4F4F6", 0.55f),
            ("Hero_ShoeSole", "A9B0C4", 0.55f), ("Hero_Lace", "C9CFE2", 0.55f),
            ("Hero_HatA", "1B2F6B", 0.60f), ("Hero_HatB", "F0501A", 0.60f),
            ("Hero_Frame", "23262E", 0.45f), ("Hero_Lens", "9FD3FF", 0.45f), ("Hero_LensDark", "1B2230", 0.30f), ("Hero_Metal", "D8B45A", 0.45f),
        };

        public static bool Available => Resources.Load<GameObject>(Path) != null;

        /// The figure, or null when the Hero isn't in the build.
        public static HeroGolfer Build(Transform parent, in HeroLook look)
        {
            var prefab = Resources.Load<GameObject>(Path);
            if (!prefab) return null;
            var g = new HeroGolfer();
            g.Root = Object.Instantiate(prefab, parent);
            g.Root.name = "Golfer model";
            foreach (var t in g.Root.GetComponentsInChildren<Transform>(true)) g.Bones[t.name] = t;
            foreach (var slot in Slots) g.AddPart(slot);
            g.MakeMaterials();
            g.Dress(look);
            return g;
        }

        void AddPart(string slot)
        {
            var prefab = Resources.Load<GameObject>("Hero/hero_" + slot);
            if (!prefab) { Debug.LogWarning($"Hero part hero_{slot} is missing"); return; }
            var inst = Object.Instantiate(prefab);
            foreach (var smr in inst.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                // Rebind to the shared skeleton by NAME, in this mesh's own bone order (each mesh lists its
                // bones its own way; copying one renderer's array to another deforms badly).
                var own = smr.bones;
                var mapped = new Transform[own.Length];
                for (int i = 0; i < own.Length; i++)
                    mapped[i] = own[i] && Bones.TryGetValue(own[i].name, out var b) ? b : null;
                smr.transform.SetParent(Root.transform, false);
                smr.bones = mapped;
                smr.rootBone = Bones.TryGetValue("Hips", out var hips) ? hips : null;
                smr.updateWhenOffscreen = true;
                // smooth low-poly skin and shoes speckle with self-shadow (on the face, where the shorts and the socks meet them)
                smr.receiveShadows = slot != "body" && slot != "head" && slot != "shoes";
                original[smr] = smr.sharedMaterials;
                parts.Add(smr);
            }
            Object.Destroy(inst);
        }

        static Color Hex(string hex) => ColorUtility.TryParseHtmlString("#" + hex, out var c) ? c : Color.magenta;

        Material Make(string name, Color color, float capStrength)
        {
            var shader = Shader.Find("GolfArcade/HeroKit");
            if (!shader) return null;
            var m = new Material(shader) { name = name, color = color };
            matCap ??= Resources.Load<Texture2D>("Golfer/Look/clay_matcap");
            if (matCap) m.SetTexture("_MatCap", matCap);
            m.SetFloat("_UseAtlas", 0);
            m.SetFloat("_MatCapStrength", capStrength);
            m.SetFloat("_Wrap", 0.6f);                      // a wide wrap: soft shadows, so a hat's underside is shaded, not black
            m.SetTexture("_Mask", Texture2D.blackTexture);
            owned.Add(m);
            return m;
        }

        void MakeMaterials()
        {
            foreach (var (role, hex, cap) in Designed) roles[role] = Make(role, Hex(hex), cap);
            // the blush is skin that goes pink toward the middle of its patch (the vertex colour's R is how pink): the scalp mode of the shader
            if (roles.TryGetValue("Hero_Blush", out var blush) && blush) { blush.SetFloat("_UseScalp", 1); blush.SetFloat("_HairAmount", 1); }
            // clear glasses have a frame and no lens (a lens would hide the eyes); the shades' lens is solid
            if (roles.TryGetValue("Hero_Lens", out var lens) && lens) lens.SetFloat("_Clear", 1);
        }

        void Tint(string role, Color c) { if (roles.TryGetValue(role, out var m) && m) { c.a = 1f; m.color = c; } }

        static Color Mix(Color a, Color b, float t) => new(a.r + (b.r - a.r) * t, a.g + (b.g - a.g) * t, a.b + (b.b - a.b) * t, 1f);

        /// Puts the look on: which parts show, their colours.
        public void Dress(in HeroLook look)
        {
            var skin = look.Skin; skin.a = 1f;
            var hair = look.HairColor; hair.a = 1f;
            Tint("Hero_Skin", skin);
            Tint("Hero_Ear", Mix(skin, new Color(0.75f, 0.29f, 0.29f), 0.30f));
            var blushColor = Mix(skin, new Color(0.93f, 0.31f, 0.38f), 0.45f);
            Tint("Hero_Blush", blushColor);
            if (roles.TryGetValue("Hero_Blush", out var blush) && blush) blush.SetColor("_ScalpSkin", skin);
            Tint("Hero_Hair", hair);
            Tint("Hero_HairB", hair * 0.78f);
            // the kit as designed: a white polo, navy shorts and trim, white shoes; a picked colour replaces its own
            Tint("Hero_Top", look.Shirt ?? Hex("F2F3F5"));
            var navy = look.Shorts ?? Hex("1B2F6B");
            Tint("Hero_Bottom", navy); Tint("Hero_TopTrim", navy);
            Tint("Hero_Shoe", look.Shoes ?? Hex("F4F4F6"));

            int cut = Mathf.Clamp(look.Haircut, 0, HaircutNames.Length - 1);
            int wear = Mathf.Clamp(look.Headwear, 0, HeadwearNames.Length - 1);
            int glasses = Mathf.Clamp(look.Glasses, 0, GlassesNames.Length - 1);
            int facial = Mathf.Clamp(look.Facial, 0, FacialNames.Length - 1);
            int top = Mathf.Clamp(look.Top, 0, TopNames.Length - 1);
            int bottom = Mathf.Clamp(look.Bottom, 0, BottomNames.Length - 1);
            // the hat's colour (its own as designed until one is picked) and its band, bill and button a shade to match
            var hatColor = look.Hat ?? HatDefault(wear);
            Tint("Hero_HatA", hatColor); Tint("Hero_HatB", TrimOf(hatColor));

            bool hatOn = wear != (int)Headwear.None;
            string hairName = cut == (int)Haircut.Bald ? null : "Hair_" + HaircutNames[cut] + (hatOn ? "_Hat" : "");
            string hatName = hatOn ? "Hat_" + HeadwearNames[wear] : null;
            string glassesName = glasses > 0 ? "Glasses_" + GlassesNames[glasses] : null;
            string facialName = facial > 0 ? "Face_" + FacialNames[facial] : null;
            string topName = "Top_" + TopNames[top], bottomName = "Bottom_" + BottomNames[bottom];
            foreach (var r in parts)
            {
                string n = r.name;
                if (n.StartsWith("Hair_")) r.enabled = n == hairName;
                else if (n.StartsWith("Hat_")) r.enabled = n == hatName;
                else if (n.StartsWith("Glasses_")) r.enabled = n == glassesName;
                else if (n.StartsWith("Face_")) r.enabled = n == facialName;
                else if (n.StartsWith("Top_")) r.enabled = n == topName;
                else if (n.StartsWith("Bottom_")) r.enabled = n == bottomName;
                AssignMaterials(r);
            }
        }

        /// A hat's colour as designed.
        public static Color HatDefault(int wear) => wear switch
        {
            (int)Headwear.Cap => new Color(1f, 0.42f, 0.24f),
            (int)Headwear.Bucket => new Color(0.20f, 0.50f, 0.32f),
            (int)Headwear.Beanie => new Color(0.13f, 0.20f, 0.42f),
            (int)Headwear.Straw => new Color(0.95f, 0.82f, 0.45f),
            (int)Headwear.Headband => new Color(0.94f, 0.31f, 0.10f),
            (int)Headwear.Beret => new Color(0.60f, 0.12f, 0.22f),
            (int)Headwear.Flatcap => new Color(0.42f, 0.36f, 0.30f),
            (int)Headwear.Fedora => new Color(0.36f, 0.27f, 0.20f),
            (int)Headwear.Cowboy => new Color(0.65f, 0.45f, 0.27f),
            (int)Headwear.Headphones => new Color(0.17f, 0.18f, 0.22f),
            (int)Headwear.Bandana => new Color(0.80f, 0.15f, 0.16f),
            (int)Headwear.Tophat => new Color(0.12f, 0.12f, 0.15f),
            (int)Headwear.Crown => new Color(0.96f, 0.76f, 0.20f),
            _ => new Color(0.95f, 0.95f, 0.93f),
        };

        /// The colour of a hat's band, bill and button: navy on a light hat, a deeper shade of the hat's own on a dark one.
        public static Color TrimOf(Color hat)
        {
            float l = hat.r * 0.2126f + hat.g * 0.7152f + hat.b * 0.0722f;
            return l > 0.72f ? new Color(0.13f, 0.2f, 0.42f) : new Color(hat.r * 0.66f, hat.g * 0.66f, hat.b * 0.66f, 1f);
        }

        /// The renderer's materials, by the role names the FBX gave them.
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

        /// Renderers of a part by name ("Hat_Visor", "Hair_Bob"...).
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
