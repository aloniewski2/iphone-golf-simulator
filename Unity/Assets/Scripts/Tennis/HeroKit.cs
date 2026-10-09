using System.Collections.Generic;
using UnityEngine;

namespace GolfArcade.Tennis
{
    /// The locker's choices (identity only; never stats) and the palettes / style record they travel in. The record keeps every field the
    /// native session and the roster send (skin, hair, headwear, kit colours, haircut), but the match heroes wear only two of them: the skin
    /// tone and the racket colour (see Apply). The Swift locker mirror (CharacterModelPreview.swift) applies the same two.
    public static class HeroKit
    {
        // Must match GolfArcade/Unity/SportProgress.swift (Outfit.skins) and CharacterOptions.hairHex.
        public static readonly string[] SkinHex = { "F3C9A6", "EEBB8F", "E2A06E", "C47A4C", "965835", "603924" }; // = GolferStyle.SkinTones (liveliness retune 2026-10-02)
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

        /// Apply the locker / roster look to a hero.
        ///
        /// HERO_MAINSTAY: the heroes on court are the two match bodies (MatchHeroLook): bald grey-mannequin bodies, painted face, the classic
        /// racket. The look is identity only and has exactly two inputs:
        ///   * skin    -> the tone on the body material (the locker's exact SkinTint, else the SkinHex palette entry);
        ///   * racket  -> the colour of the classic racket's frame (alpha 0 = the classic white frame; the strings and grip are never tinted).
        /// DRESS_MATCH_HEROES: the kit colours (Style.Shirt / Shorts / Shoes, alpha 0 = the authored White kit) are applied to the worn kit by role.
        /// Everything else in the Style (hair colour, haircut, headwear) is not applied: the old hair, visors, caps and
        /// sweatbands were built for the old head. The Style keeps those fields because the roster, the native session and the locker still carry them.
        public static void Apply(HeroTennisDriver hero, Style s)
        {
            if (!hero || !hero.matchLook) return;
            var skin = s.SkinTint.a > 0 ? s.SkinTint : Hex(SkinHex[Mathf.Clamp(s.Skin, 0, 5)]);
            hero.matchLook.SetSkin(skin);
            hero.matchLook.SetRacketColour(s.Racket);
            // DRESS_MATCH_HEROES: the heroes wear the White tennis kit; a shirt / shorts / shoes pick (alpha 0 = kit colour) recolours it by role (MatchHeroLook.SetKit)
            hero.matchLook.SetKit(s.Shirt, s.Shorts, s.Shoes);
        }
    }
}
