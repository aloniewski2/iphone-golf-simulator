using UnityEngine;

namespace GolfArcade.Game
{
    /// Who the player looks like: the male or female golfer model, a skin tone, hair and an
    /// outfit. Remembered on the device between rounds. Out of the box it is Adnan's standard
    /// identity as his reference sheet shows it: cream skin, no hair, black tee and joggers.
    public static class GolferStyle
    {
        public enum BodyKind { Male, Female }

        /// From light to deep; the second is the cream of Adnan's identity references (sampled
        /// off SportsLibrary/Characters/IdentityReferences, warmed so it renders that way under the
        /// course's blue sky light) and the default.
        public static readonly Color[] SkinTones =
        {
            Rgb(255, 228, 204), Rgb(232, 190, 146), Rgb(226, 160, 110),
            Rgb(196, 124, 80), Rgb(150, 90, 56), Rgb(96, 60, 40),
        };
        public static readonly string[] SkinToneNames = { "Fair", "Cream", "Tan", "Olive", "Brown", "Deep" };
        public const int DefaultSkin = 1;

        const string BodyKey = "golfer.body", SkinKey = "golfer.skin", LookKey = "golfer.look";
        /// Bumped when the default look changes enough that saved looks should start over.
        const int LookVersion = 2;

        /// Once per LookVersion: back to Adnan's standard look (a device that saved the old
        /// tan, brown-haired, teal-polo golfer comes up as his character).
        static GolferStyle()
        {
            if (PlayerPrefs.GetInt(LookKey, 0) >= LookVersion) return;
            PlayerPrefs.SetInt(SkinKey, DefaultSkin);
            PlayerPrefs.SetInt(HairKey, (int)HairKind.None);
            PlayerPrefs.SetInt(OutfitKey, (int)OutfitKind.Standard);
            PlayerPrefs.SetInt(LookKey, LookVersion);
            PlayerPrefs.Save();
        }

        public static BodyKind Body
        {
            get => (BodyKind)PlayerPrefs.GetInt(BodyKey, 0);
            set { PlayerPrefs.SetInt(BodyKey, (int)value); PlayerPrefs.Save(); }
        }

        public static int SkinTone
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt(SkinKey, DefaultSkin), 0, SkinTones.Length - 1);
            set { PlayerPrefs.SetInt(SkinKey, Mathf.Clamp(value, 0, SkinTones.Length - 1)); PlayerPrefs.Save(); }
        }

        public static Color SkinColor => SkinTones[SkinTone];
        public static string SkinName => SkinToneNames[SkinTone];

        // ---- Hair: three styles modelled on Adnan's head (HAIR_SHORT / HAIR_LONG / HAIR_CURLY in
        // the golfer FBX) or none, in one of six colours.
        public enum HairKind { Short, Long, Curly, None }
        public static readonly string[] HairNames = { "Short", "Bob", "Curls", "None" };
        public static readonly Color[] HairColors =
        {
            Rgb(28, 24, 22), Rgb(62, 40, 26), Rgb(118, 78, 48), Rgb(152, 62, 34), Rgb(222, 190, 122), Rgb(204, 206, 212),
        };
        public static readonly string[] HairColorNames = { "Black", "Dark brown", "Brown", "Auburn", "Blonde", "Silver" };
        const string HairKey = "golfer.hair", HairColorKey = "golfer.haircolor";

        public static HairKind Hair
        {
            get => (HairKind)Mathf.Clamp(PlayerPrefs.GetInt(HairKey, (int)HairKind.None), 0, HairNames.Length - 1);
            set { PlayerPrefs.SetInt(HairKey, (int)value); PlayerPrefs.Save(); }
        }

        public static int HairTone
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt(HairColorKey, 1), 0, HairColors.Length - 1);
            set { PlayerPrefs.SetInt(HairColorKey, Mathf.Clamp(value, 0, HairColors.Length - 1)); PlayerPrefs.Save(); }
        }

        public static Color HairColor => HairColors[HairTone];
        // ---- Outfit: Adnan's standard tee and joggers (his identity sheet and golf gameplay
        // concept), or the teal polo and sand trousers of his V4 golf kit as modelled.
        public enum OutfitKind { Standard, GolfKit }
        public static readonly string[] OutfitNames = { "Tee & joggers", "Golf polo" };
        const string OutfitKey = "golfer.outfit";

        public static OutfitKind Outfit
        {
            get => (OutfitKind)Mathf.Clamp(PlayerPrefs.GetInt(OutfitKey, 0), 0, OutfitNames.Length - 1);
            set { PlayerPrefs.SetInt(OutfitKey, (int)value); PlayerPrefs.Save(); }
        }

        /// The shirt and trousers over the kit's teal and sand, or null to keep the kit.
        public static Color? ShirtColor => Outfit == OutfitKind.Standard ? Rgb(62, 62, 66) : null;
        public static Color? TrousersColor => Outfit == OutfitKind.Standard ? Rgb(56, 56, 60) : null;

        /// The mesh in the FBX for the chosen style, or null for none.
        public static string HairMesh => Hair == HairKind.None ? null : "HAIR_" + Hair.ToString().ToUpperInvariant();
        /// The player is a Higgsfield golfer (blender/characters/golf) who comes dressed, with their
        /// own hair and face: only the body is a choice. Skin, outfit and hair still apply to the
        /// crowd and to a V4-model player.
        public const bool Customizable = false;
        // ---- The Higgsfield golfer's kit: GolferClay recolours the colour map's navy (shorts or
        // skirt, collar, trims, cap brim) and its white (shirt, cap, socks). The first of each is
        // the kit as it comes; the palette starts from the standard characters' (navy, teal,
        // ivory, sand, coral).
        public static readonly string[] KitNames = { "Navy", "Teal", "Coral", "Crimson", "Forest", "Charcoal" };
        public static readonly Color[] KitColors = { Rgb(30, 43, 90), Rgb(18, 138, 140), Rgb(226, 92, 76), Rgb(190, 36, 46), Rgb(34, 120, 62), Rgb(52, 55, 62) };
        public static readonly string[] ShirtNames = { "White", "Ivory", "Sand", "Sky", "Blush", "Mint" };
        public static readonly Color[] ShirtColors = { Rgb(246, 247, 249), Rgb(244, 236, 214), Rgb(234, 210, 164), Rgb(186, 224, 250), Rgb(250, 198, 212), Rgb(196, 240, 214) };
        const string KitKey = "golfer.kit", ShirtKey = "golfer.shirt";

        public static int Kit
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt(KitKey, 0), 0, KitColors.Length - 1);
            set { PlayerPrefs.SetInt(KitKey, Mathf.Clamp(value, 0, KitColors.Length - 1)); PlayerPrefs.Save(); }
        }

        public static int Shirt
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt(ShirtKey, 0), 0, ShirtColors.Length - 1);
            set { PlayerPrefs.SetInt(ShirtKey, Mathf.Clamp(value, 0, ShirtColors.Length - 1)); PlayerPrefs.Save(); }
        }

        /// The kit on a GolferClay material with the Higgsfield colour map.
        public static void Dress(Material m)
        {
            if (!m) return;
            m.SetFloat("_KitOn", Kit > 0 ? 1 : 0); m.SetColor("_KitColor", KitColors[Kit]);
            m.SetFloat("_ShirtOn", Shirt > 0 ? 1 : 0); m.SetColor("_ShirtColor", ShirtColors[Shirt]);
        }

        /// "Male · Tan · Bob, auburn": the golfer in a line, for the menu.
        public static string Summary => !Customizable ? $"{(Body == BodyKind.Female ? "Female" : "Male")} golfer   ·   {KitNames[Kit]}{(Shirt > 0 ? $" & {ShirtNames[Shirt].ToLowerInvariant()}" : "")} kit" : $"{(Body == BodyKind.Female ? "Female" : "Male")}   ·   {SkinName}   ·   {(Hair == HairKind.None ? "No hair" : $"{HairNames[(int)Hair]}, {HairColorNames[HairTone].ToLowerInvariant()}")}";
        /// Resources path of the model for the chosen body.
        public static string ModelPath => Body == BodyKind.Female ? "Golfer/golfer_f" : "Golfer/golfer_m";
        public static string BodyLabel => Body == BodyKind.Female ? "♀" : "♂";

        public static void CycleBody() => Body = Body == BodyKind.Male ? BodyKind.Female : BodyKind.Male;
        public static void CycleSkin() => SkinTone = (SkinTone + 1) % SkinTones.Length;

        static Color Rgb(int r, int g, int b) => new(r / 255f, g / 255f, b / 255f);
    }
}
