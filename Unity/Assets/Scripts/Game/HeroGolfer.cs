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
        /// Colours of the shirt, the shorts (and the navy trim), the shoes and the cap or band; null keeps the kit as designed.
        public Color? Shirt, Shorts, Shoes, Hat;
    }

    /// Adnan's modular Hero (blender/scripts/hero_golf_retarget.py + hero_parts_export.py), assembled the
    /// way his handoff asks: ONE skeleton (hero_golf.fbx, the golf clips and the clubs) and every part
    /// (body, eyes, hair cuts, headwear, shirt, shorts, shoes) a skinned mesh in its own FBX, rebound to
    /// that skeleton by bone name. Any slot can change without touching another: the hair does not depend
    /// on the visor, the visor not on the hair.
    ///
    /// The colour atlas is re-coloured per region by the HeroKit shader (shirt, shorts and trim, skin);
    /// flat parts (hair, shoes, cap) take a colour; the girl is the "Female" blend shape on the body and the
    /// clothes over it.
    ///
    /// The head (blender/scripts/hero_head.py): one closed scalp under everything, skin below its hairline
    /// and hair above (bald: all skin; buzz and waves: the short-hair maps on it); each haircut seated on it;
    /// a hat sits over the hair and the hair is tucked in under it by a blend shape per hat ("Under_Visor"...),
    /// the hat eased out to sit on that cut's hair by one of its own ("OnHair_Hair_Bob"...). Nothing is cut away.
    public sealed class HeroGolfer
    {
        public enum Haircut { Swept, Ponytail, Bob, Long, Curly, Bald, Buzz, Waves }
        public enum Headwear { None, Visor, Cap, Sweatband }
        public static readonly string[] HaircutNames = { "Swept", "Ponytail", "Bob", "Long", "Curly", "Bald", "Buzz", "Waves" };
        public static readonly string[] HeadwearNames = { "None", "Visor", "Cap", "Sweatband" };
        /// The cuts the game offers (all of them, now the head is whole).
        public const int OfferedHaircuts = 8;

        public const string Path = "Hero/hero_golf";
        static readonly string[] Slots = { "body", "eyes", "hair", "visor", "cap", "sweatband", "shirt", "shorts", "shoes" };

        public GameObject Root { get; private set; }
        public readonly Dictionary<string, Transform> Bones = new();
        readonly List<SkinnedMeshRenderer> parts = new();
        readonly List<Material> owned = new();
        /// Each renderer's materials as the FBX gave them (the names decide what they become, every time a look is put on).
        readonly Dictionary<SkinnedMeshRenderer, Material[]> original = new();

        // materials, one set per figure
        Material atlas, foundation, underHair, hair, scalp, shoeWhite, shoeOrange, cap, iris, catchlight, plain;

        static Texture2D atlasTex, maskTex, irisTex, buzzTex, wavesTex, matCap;
        static Vector4 kitRef = new(0.85f, 0.19f, 0.3f, 0.67f);
        [System.Serializable] class Ref { public float shirt, shorts, hair, skin; }

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
                original[smr] = smr.sharedMaterials;
                parts.Add(smr);
            }
            Object.Destroy(inst);
        }

        // ----- Hair that follows the head: a damped spring on the head's acceleration, fed to the shader
        Vector3 swayX, swayV, lastHead, lastHeadVel;
        bool haveHead;
        float breeze;

        /// How far the hair is swung right now, in world units (for the tests).
        public float SwayMagnitude => swayX.magnitude;

        /// Call each frame after the pose: the hair lags the head, settles, and stirs a little.
        public void Sway(float dt)
        {
            if (!hair || !Bones.TryGetValue("Head", out var head) || dt <= 0f) return;
            dt = Mathf.Min(dt, 1f / 20f);
            var pos = head.position;
            if (!haveHead) { lastHead = pos; lastHeadVel = Vector3.zero; haveHead = true; return; }
            var vel = (pos - lastHead) / dt;
            var acc = Vector3.ClampMagnitude((vel - lastHeadVel) / dt, 60f);
            lastHead = pos; lastHeadVel = vel;
            breeze += dt;
            var stir = new Vector3(Mathf.Sin(breeze * 1.7f), 0f, Mathf.Sin(breeze * 1.3f + 1f)) * 0.6f;
            int steps = Mathf.CeilToInt(dt * 120f);
            float h = dt / steps;
            for (int i = 0; i < steps; i++)
            {
                var a = -260f * swayX - 14f * swayV - 0.8f * (acc + stir);
                swayV += a * h;
                swayX += swayV * h;
            }
            swayX = Vector3.ClampMagnitude(swayX, 0.09f);
            hair.SetVector("_HairSway", swayX);
        }

        static Material Make(string name, Color color, bool useAtlas, Texture map = null, Texture mask = null, float capStrength = 0.85f)
        {
            var shader = Shader.Find("GolfArcade/HeroKit");
            if (!shader) return null;
            var m = new Material(shader) { name = name, color = color };
            if (!matCap) matCap = Resources.Load<Texture2D>("Golfer/Look/clay_matcap");
            if (matCap) m.SetTexture("_MatCap", matCap);
            m.SetFloat("_UseAtlas", useAtlas ? 1 : 0);
            m.SetFloat("_MatCapStrength", capStrength);
            if (map) m.SetTexture("_MainTex", map);
            m.SetTexture("_Mask", mask ? mask : Texture2D.blackTexture);
            return m;
        }

        void MakeMaterials()
        {
            atlasTex ??= Resources.Load<Texture2D>("Hero/Look/hero_atlas");
            maskTex ??= Resources.Load<Texture2D>("Hero/Look/hero_mask");
            irisTex ??= Resources.Load<Texture2D>("Hero/Look/hero_iris");
            buzzTex ??= Resources.Load<Texture2D>("Hero/Look/hero_hair_buzz");
            wavesTex ??= Resources.Load<Texture2D>("Hero/Look/hero_hair_waves");
            var refJson = Resources.Load<TextAsset>("Hero/Look/hero_kitref");
            if (refJson) { var r = JsonUtility.FromJson<Ref>(refJson.text); kitRef = new Vector4(r.shirt, r.shorts, Mathf.Max(r.hair, 0.3f), r.skin); }
            atlas = Own(Make("Hero atlas", Color.white, true, atlasTex, maskTex, 0.6f));
            if (atlas) atlas.SetVector("_Ref", kitRef);
            foundation = Own(Make("Hero foundation", Color.gray, false));
            underHair = Own(Make("Hero under hair", Color.gray, false));
            hair = Own(Make("Hero hair", Color.white, false));
            if (hair) hair.SetFloat("_UseFlex", 1f);
            scalp = Own(Make("Hero scalp", Color.white, false, buzzTex, null, 0.6f));
            if (scalp) scalp.SetFloat("_UseScalp", 1f);
            shoeWhite = Own(Make("Hero shoe", new Color(0.89f, 0.89f, 0.87f), false));
            shoeOrange = Own(Make("Hero shoe accent", new Color(1f, 0.42f, 0.24f), false));
            cap = Own(Make("Hero cap", new Color(1f, 0.42f, 0.24f), false));
            iris = Own(Make("Hero eye", Color.white, true, irisTex, null, 0f));
            catchlight = Own(Make("Hero catchlight", Color.white, false, null, null, 0f));
            plain = Own(Make("Hero plain", Color.white, false));
        }

        Material Own(Material m) { if (m) owned.Add(m); return m; }

        /// Puts the look on: which parts show, their colours, the shape.
        public void Dress(in HeroLook look)
        {
            var skin = look.Skin; skin.a = 1f;
            var hairColor = look.HairColor; hairColor.a = 1f;
            if (atlas)
            {
                atlas.SetColor("_Skin", skin);
                SetTint(atlas, "_Shirt", look.Shirt);
                SetTint(atlas, "_Shorts", look.Shorts);
            }
            // the body's inner shell (under the clothes, and behind the odd tear in the face by the ears): skin
            if (foundation) foundation.color = new Color(skin.r * 0.85f, skin.g * 0.82f, skin.b * 0.8f);
            int cut = Mathf.Clamp(look.Haircut, 0, HaircutNames.Length - 1);
            bool bald = cut == (int)Haircut.Bald;
            // where hair meets skin on the body (the temples): the hair's colour, or the skin's when bald
            if (underHair) underHair.color = bald ? new Color(skin.r * 0.96f, skin.g * 0.94f, skin.b * 0.92f) : hairColor * 0.82f;
            if (hair) hair.color = hairColor;
            if (scalp)
            {
                scalp.SetColor("_ScalpSkin", new Color(skin.r * 0.97f, skin.g * 0.95f, skin.b * 0.94f));
                scalp.SetFloat("_HairAmount", bald ? 0f : 1f);
                scalp.color = DetailTint(hairColor);
                var map = cut == (int)Haircut.Waves ? wavesTex : buzzTex;
                if (map) scalp.SetTexture("_MainTex", map);
            }
            if (shoeWhite) shoeWhite.color = look.Shoes ?? new Color(0.89f, 0.89f, 0.87f);
            if (cap && look.Hat is Color hatColor) cap.color = hatColor;

            string want = HairRenderer(cut);
            if (want != null && !parts.Exists(r => r.name == want)) want = HairRenderer(0);
            int wear = Mathf.Clamp(look.Headwear, 0, HeadwearNames.Length - 1);
            string hat = wear == (int)Headwear.None ? null : "Hat_" + HeadwearNames[wear];
            string under = hat == null ? null : "Under_" + HeadwearNames[wear];
            // the hair held by the hat (it must not swing out through it)
            if (hair) hair.SetVector("_HatHold", new Vector4(wear == (int)Headwear.Visor ? 1 : 0, wear == (int)Headwear.Cap ? 1 : 0, wear == (int)Headwear.Sweatband ? 1 : 0, 0));
            foreach (var r in parts)
            {
                string n = r.name;
                if (n.StartsWith("Hair_")) r.enabled = n == want;
                else if (n.StartsWith("Hat_")) r.enabled = n == hat;
                var mesh = r.sharedMesh;
                if (mesh)
                    for (int i = 0; i < mesh.blendShapeCount; i++)
                    {
                        string shape = mesh.GetBlendShapeName(i);
                        float w = 0;
                        if (shape.EndsWith("Female")) w = look.Female ? 100 : 0;              // the girl: the body and the clothes over it
                        else if (shape.Contains("OnHair_")) w = want != null && shape.EndsWith("OnHair_" + want) ? 100 : 0;   // a hat sits on this cut's hair
                        else if (shape.Contains("Under_")) w = under != null && shape.EndsWith(under) ? 100 : 0;   // the hair tucked in under the hat
                        r.SetBlendShapeWeight(i, w);
                    }
                AssignMaterials(r);
            }
        }

        static void SetTint(Material m, string prop, Color? c) => m.SetColor(prop, c is Color v ? new Color(v.r, v.g, v.b, 1f) : new Color(1, 1, 1, 0));

        /// The haircut's mesh, or null for the cuts that are the scalp alone (bald, buzz, waves).
        static string HairRenderer(int cut) => cut switch
        {
            (int)Haircut.Bald or (int)Haircut.Buzz or (int)Haircut.Waves => null,
            <= 0 => "Hair_Default",
            _ => "Hair_" + HaircutNames[cut],
        };

        /// Buzz and waves carry a detail map multiplied by the tint: on near-black hair the pattern vanishes,
        /// so the tint keeps a little brightness for the ridges to read.
        static Color DetailTint(Color c)
        {
            float l = c.r * 0.2126f + c.g * 0.7152f + c.b * 0.0722f;
            return l >= 0.16f ? c : new Color(c.r + (0.16f - l), c.g + (0.16f - l), c.b + (0.16f - l), 1f);
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
                Material use = null;
                if (n.StartsWith("Hero_01_Scalp")) use = scalp;
                else if (n.StartsWith("skin_BlondHair")) use = underHair;
                else if (n.StartsWith("skin_CoveredFoundation")) use = foundation;
                else if (n.StartsWith("skin_") || n == "Hero_01_MatteCloth" || n == "Hero_01_WarmSkin" || n.StartsWith("Hero_01_Visor") || n.StartsWith("Hero_01_BlondHair")) use = atlas;
                else if (n.StartsWith("Hero_01_MatteCloth_TrimTeal")) use = atlas;
                else if (n.StartsWith("Hero_01_HairTuft")) use = hair;
                else if (n.StartsWith("Hero_01_ShoeCleanWhite")) use = shoeWhite;
                else if (n.StartsWith("Hero_01_ShoeOrange")) use = shoeOrange;
                else if (n.StartsWith("Hero_01_EyeCornea")) use = iris;
                else if (n.StartsWith("Hero_01_Catchlight")) use = catchlight;
                else if (n.StartsWith("Cos_")) use = cap;
                if (use) mats[i] = use;
            }
            r.sharedMaterials = mats;
        }

        /// Renderers of a part by name ("Hat_Visor", "Hair_Bob"...).
        public SkinnedMeshRenderer Part(string name) => parts.Find(r => r.name == name);
        public IReadOnlyList<SkinnedMeshRenderer> Parts => parts;

        public void Dispose()
        {
            foreach (var m in owned) if (m) Object.Destroy(m);
            owned.Clear();
            if (Root) Object.Destroy(Root);
        }
    }
}
