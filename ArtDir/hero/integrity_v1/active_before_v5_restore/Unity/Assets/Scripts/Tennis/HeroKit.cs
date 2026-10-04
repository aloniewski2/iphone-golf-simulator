using System.Collections.Generic;
using UnityEngine;

namespace GolfArcade.Tennis
{
    /// The locker's choices applied to the locked Hero V4 (identity only; never stats). The same rules are
    /// ported to Swift for the locker mirror (CharacterModelPreview.swift, HeroLockerModel) so the phone
    /// shows exactly what the game plays:
    ///   * one shared atlas (Hero_01_BaseColor) re-tinted with the region mask HeroV4_KitMask
    ///     (R shirt body, G shorts + trim navy, B baked side hair, A skin; built by HeroLockerExport),
    ///     each region keeping its shading by scaling with luminance relative to the region's mean;
    ///   * flat materials: hair tuft = hair colour, clean-white shoe = shoe colour, racket frame = racket colour;
    ///   * headwear slot: none / visor (the locked default) / cap / sweatband.
    /// A colour with alpha 0 leaves that region as the locked V4 kit.
    public static class HeroKit
    {
        // Must match GolfArcade/Unity/SportProgress.swift (Outfit.skins) and CharacterOptions.hairHex.
        public static readonly string[] SkinHex = { "FFE0C4", "EEC4A0", "E2A06E", "C47C50", "965A38", "603C28" };
        public static readonly string[] HairHex = { "211C1A", "593722", "A54D2B", "D8B365", "BCC0C5", "365D99" };
        public const int DefaultSkin = 0, DefaultHair = 3, DefaultHeadwear = 1;
        public static readonly string[] HeadwearNames = { "None", "Visor", "Cap", "Sweatband" };
        /// Hero V5 haircuts (Tripo-sculpted): renderer Hair_Default (Swept) or Hair_<name>, each with its own band fill.
        public static readonly string[] HaircutNames = { "Swept", "Ponytail", "Bob", "Long", "Curly", "Bald", "Buzz", "Waves" };
        public static string HairRenderer(int cut) => cut <= 0 ? "Hair_Default" : "Hair_" + HaircutNames[Mathf.Min(cut, HaircutNames.Length - 1)];
        public static string BandFillRenderer(int cut) => cut <= 0 ? "Hair_BandFill" : "Hair_BandFill_" + HaircutNames[Mathf.Min(cut, HaircutNames.Length - 1)];

        public struct Style
        {
            public int Skin, HairColor, Headwear, Haircut; public bool Female; public float BodySize; public Color SkinTint, HairTint; public Color Shirt, Shorts, Shoes, Racket;
            public static Style Locked => new Style { Skin = DefaultSkin, HairColor = DefaultHair, Headwear = DefaultHeadwear, BodySize = .5f };
            public static Style From(int skin, int hairColor, int headwear, TennisLook.Kit kit) => new Style
            { Skin = Mathf.Clamp(skin, 0, 5), HairColor = Mathf.Clamp(hairColor, 0, 5), Headwear = Mathf.Clamp(headwear, 0, 3), BodySize = .5f, Shirt = kit.Shirt, Shorts = kit.Shorts, Shoes = kit.Accent, Racket = kit.Racket };
            public static Style From(int skin, int hairColor, int headwear, TennisLook.Kit kit, int haircut, bool female = false)
            { var s = From(skin, hairColor, headwear, kit); s.Haircut = Mathf.Clamp(haircut, 0, HaircutNames.Length - 1); s.Female = female; return s; }
        }

        /// Buzz / waves carry a detail map multiplied by the tint: on near-black hair the pattern vanished into a flat
        /// black blob, so the tint keeps a little brightness (luminance >= .16) for the ridges to read.
        public static Color DetailTint(Color hair)
        {
            float l = hair.r * .2126f + hair.g * .7152f + hair.b * .0722f;
            return l >= .16f ? hair : new Color(hair.r + (.16f - l), hair.g + (.16f - l), hair.b + (.16f - l), 1);
        }

        public static void SetFemale(SkinnedMeshRenderer r, bool female)
        {
            var m = r ? r.sharedMesh : null; if (!m) return;
            for (int i = 0; i < m.blendShapeCount; i++) if (m.GetBlendShapeName(i).EndsWith("Female")) r.SetBlendShapeWeight(i, female ? 100 : 0);
        }

        public static void SetBuild(SkinnedMeshRenderer r, float size)
        {
            var m = r ? r.sharedMesh : null; if (!m) return;
            float signed = (Mathf.Clamp01(size) - .5f) * 200;
            for (int i = 0; i < m.blendShapeCount; i++) {
                string name = m.GetBlendShapeName(i);
                if (name.EndsWith("BuildSlim")) r.SetBlendShapeWeight(i, Mathf.Max(0, -signed));
                else if (name.EndsWith("BuildBroad")) r.SetBlendShapeWeight(i, Mathf.Max(0, signed));
            }
        }

        public static Color Hex(string h) { ColorUtility.TryParseHtmlString("#" + h, out var c); return c; }

        [System.Serializable] public class Ref { public float shirt, shorts, hair, skin; }
        static Ref refs; static Texture2D mask, atlas; static Material recolor;
        const float SkinShading = .45f;

        public static void Apply(HeroTennisDriver hero, Style s)
        {
            if (!hero || !hero.look) return;
            var look = hero.look;
            refs ??= JsonUtility.FromJson<Ref>(Resources.Load<TextAsset>("Tennis/Hero/HeroV4_KitRef")?.text ?? "{}");
            mask ??= Resources.Load<Texture2D>("Tennis/Hero/HeroV4_KitMask");
            atlas ??= look.skinAtlas;
            if (!recolor) { var sh = Resources.Load<Shader>("Tennis/Shaders/KitRecolor"); if (sh) recolor = new Material(sh) { name = "Hero kit recolour" }; }
            var skin = s.SkinTint.a > 0 ? s.SkinTint : Hex(SkinHex[Mathf.Clamp(s.Skin, 0, 5)]);
            var hair = s.HairTint.a > 0 ? s.HairTint : Hex(HairHex[Mathf.Clamp(s.HairColor, 0, 5)]);
            // ---- atlas: shirt / shorts / baked hair / skin in one pass
            Texture tinted = null;
            if (atlas && mask && recolor && refs != null && refs.skin > 0)
            {
                recolor.SetTexture("_Mask", mask);
                recolor.SetVector("_Ref", new Vector4(refs.shirt, refs.shorts, refs.hair, refs.skin));
                recolor.SetColor("_Shirt", s.Shirt); recolor.SetColor("_Shorts", s.Shorts);
                recolor.SetColor("_Accent", new Color(hair.r, hair.g, hair.b, 1));
                recolor.SetColor("_Skin", new Color(skin.r, skin.g, skin.b, 1)); recolor.SetFloat("_SkinShading", SkinShading);
                var rt = new RenderTexture(atlas.width, atlas.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { name = "Hero kit atlas" };
                Graphics.Blit(atlas, rt, recolor); tinted = rt;
            }
            var own = new Dictionary<Material, Material>();
            Material Own(Material m) { if (!m) return m; if (own.TryGetValue(m, out var o)) return o; o = new Material(m) { name = m.name.Replace(" (Instance)", "") + " (kit)" }; own[m] = o; return o; }
            foreach (var r in look.GetComponentsInChildren<Renderer>(true))
            {
                if (r is TrailRenderer || r is LineRenderer) continue;
                var mats = r.sharedMaterials; bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i]; if (!m) continue; string n = m.name;
                    // Atlas-derived: the source atlas itself, or a runtime tint of it (ModularHeroLook's skin tint at
                    // startup, or an earlier HeroKit pass). Matching only the source atlas skipped the whole body
                    // (face / legs never took the skin tone) and made any second Apply a no-op.
                    var bm = m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : null;
                    bool fromAtlas = bm && (bm == atlas || bm is RenderTexture) && !n.Contains("CoveredFoundation");
                    if (tinted && fromAtlas) { mats[i] = Own(m); mats[i].SetTexture("_BaseMap", tinted); mats[i].mainTexture = tinted; changed = true; }
                    else if (n.StartsWith("Hero_01_HairTuft")) { mats[i] = Own(m); mats[i].SetColor("_BaseColor", n.StartsWith("Hero_01_HairTuft_") ? DetailTint(hair) : hair); changed = true; }
                    else if (n.StartsWith("Hero hair core")) { mats[i] = Own(m); mats[i].SetColor("_BaseColor", new Color(hair.r * HeroTennisDriver.HairCoreShade, hair.g * HeroTennisDriver.HairCoreShade, hair.b * HeroTennisDriver.HairCoreShade, 1)); changed = true; }
                    else if (n.StartsWith("Hero hair interior")) { mats[i] = Own(m); mats[i].SetColor("_BaseColor", new Color(hair.r * HeroTennisDriver.HairInteriorShade, hair.g * HeroTennisDriver.HairInteriorShade, hair.b * HeroTennisDriver.HairInteriorShade, 1)); changed = true; }
                    else if (n.StartsWith("Hero_01_ShoeCleanWhite") && s.Shoes.a > 0) { mats[i] = Own(m); mats[i].SetColor("_BaseColor", s.Shoes); changed = true; }
                    else if (n.StartsWith("Hero_Racket_Blue") && s.Racket.a > 0) { mats[i] = Own(m); mats[i].SetColor("_BaseColor", s.Racket); changed = true; }
                    else if (n.StartsWith("Hero neck skin")) { mats[i] = Own(m); mats[i].SetColor("_BaseColor", skin); changed = true; }
                    else if (n.StartsWith("Hero side hair")) { mats[i] = Own(m); mats[i].SetColor("_BaseColor", s.Haircut == 5 ? skin : hair); changed = true; }
                    else if (n.StartsWith("Hero lid skin")) { mats[i] = Own(m); mats[i].SetColor("_BaseColor", skin * new Color(.97f, .9f, .86f, 1)); changed = true; }
                    else if (n.Contains("CoveredFoundation")) { mats[i] = Own(m); mats[i].SetTexture("_BaseMap", null); mats[i].SetColor("_BaseColor", s.Haircut == 5 ? skin : hair * .8f); changed = true; }
                }
                if (changed) r.sharedMaterials = mats;
            }
            look.skinTone = skin; look.scalpTone = s.Haircut == 5 ? skin : hair * .8f;
            // ---- headwear
            var cos = hero.cosmetics;
            if (cos)
            {
                cos.shortHair = s.Haircut >= 5;   // bald / buzz / waves: hats sized to the bare skull
                if (s.Headwear == 2) cos.EquipHat(HeroCosmetics.Hat.Cap);
                else if (s.Headwear == 3) cos.EquipHat(HeroCosmetics.Hat.Sweatband);
                else cos.EquipHat(HeroCosmetics.Hat.Visor);
                foreach (Transform c in look.skeletonRoot) if (c.name == "Slot_Hat") foreach (var r in c.GetComponentsInChildren<Renderer>(true)) r.enabled = s.Headwear != 0;
                var head = look.animator ? look.animator.GetBoneTransform(HumanBodyBones.Head) : null; var liner = head ? head.Find("HatLiner") : null; if (liner) liner.gameObject.SetActive(s.Headwear != 0);
                // Hero V5: one haircut on; its natural (un-pressed) band strip only when no headwear presses it
                var hairs = look.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                // Solid sculpted cuts use opaque outward surfaces. Short scalp cuts retain two-sided edges.
                foreach (var r in hairs) {
                    r.quality = SkinQuality.Bone4;
                    if (!r.name.StartsWith("Hair_")) continue;
                    bool volume = !r.name.StartsWith("Hair_BandFill") && !r.name.StartsWith("Hair_Bald") && !r.name.StartsWith("Hair_Buzz") && !r.name.StartsWith("Hair_Waves");
                    foreach (var m in r.sharedMaterials) if (m) {
                        if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 0);
                        m.renderQueue = 2000; m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                        if (m.HasProperty("_Cull")) m.SetFloat("_Cull", volume ? 2 : 0);
                    }
                }
                // Hero V5 girl body: the 'Female' blend shape on the body and the clothes over it (one shared field)
                foreach (var r in hairs) { SetFemale(r, s.Female); SetBuild(r, s.BodySize); }
                // Hair_Bald is the scalp in skin: it wears exactly the body's (tinted) skin materials, matched by name
                var bodyR = System.Array.Find(hairs, r => r.name == "Body_Skin");
                foreach (var r in hairs)
                {
                    if (r.name != "Hair_Bald" || !bodyR) continue;
                    var bm = bodyR.sharedMaterials; var mine = r.sharedMaterials;
                    for (int i = 0; i < mine.Length; i++)
                    {
                        if (!mine[i]) continue; string baseName = mine[i].name.Replace(" (Instance)", "").Replace(" (kit)", "");
                        foreach (var b in bm) if (b && b.name.Replace(" (Instance)", "").Replace(" (kit)", "") == baseName) { mine[i] = b; break; }
                    }
                    r.sharedMaterials = mine;
                }
                string want = HairRenderer(s.Haircut); bool has = false;
                foreach (var r in hairs) if (r.name == want) has = true;
                int cut = has ? s.Haircut : 0; want = HairRenderer(cut); string fill = BandFillRenderer(cut), free = want + "_Free";
                // no headwear: the un-pressed sculpt (_Free) when it ships, else the pressed hair + its band fill
                bool hasFree = false; foreach (var r in hairs) if (r.name == free) hasFree = true;
                bool bare = s.Headwear == 0;
                foreach (var r in hairs)
                {
                    if (r.name.StartsWith("Hair_BandFill")) r.enabled = r.name == fill && bare && !hasFree;
                    else if (r.name.EndsWith("_Free")) r.enabled = r.name == free && bare;
                    else if (r.name.StartsWith("Hair_")) r.enabled = r.name == want && !(bare && hasFree);
                }
            }
        }
    }
}
