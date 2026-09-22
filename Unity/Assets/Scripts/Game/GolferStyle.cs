using UnityEngine;

namespace GolfArcade.Game
{
    /// Who the player looks like: the male or female golfer model and a skin tone. Remembered
    /// on the device between rounds.
    public static class GolferStyle
    {
        public enum BodyKind { Male, Female }

        /// From light to deep, the same scale as the character sheet's tan in the middle.
        public static readonly Color[] SkinTones =
        {
            Rgb(255, 224, 196), Rgb(238, 196, 160), Rgb(226, 160, 110),
            Rgb(196, 124, 80), Rgb(150, 90, 56), Rgb(96, 60, 40),
        };
        public static readonly string[] SkinToneNames = { "Fair", "Light", "Tan", "Olive", "Brown", "Deep" };

        const string BodyKey = "golfer.body", SkinKey = "golfer.skin";

        public static BodyKind Body
        {
            get => (BodyKind)PlayerPrefs.GetInt(BodyKey, 0);
            set { PlayerPrefs.SetInt(BodyKey, (int)value); PlayerPrefs.Save(); }
        }

        public static int SkinTone
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt(SkinKey, 2), 0, SkinTones.Length - 1);
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
            get => (HairKind)Mathf.Clamp(PlayerPrefs.GetInt(HairKey, 0), 0, HairNames.Length - 1);
            set { PlayerPrefs.SetInt(HairKey, (int)value); PlayerPrefs.Save(); }
        }

        public static int HairTone
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt(HairColorKey, 1), 0, HairColors.Length - 1);
            set { PlayerPrefs.SetInt(HairColorKey, Mathf.Clamp(value, 0, HairColors.Length - 1)); PlayerPrefs.Save(); }
        }

        public static Color HairColor => HairColors[HairTone];
        /// The mesh in the FBX for the chosen style, or null for none.
        public static string HairMesh => Hair == HairKind.None ? null : "HAIR_" + Hair.ToString().ToUpperInvariant();
        /// "Male · Tan · Bob, auburn": the golfer in a line, for the menu.
        public static string Summary => $"{(Body == BodyKind.Female ? "Female" : "Male")}   ·   {SkinName}   ·   {(Hair == HairKind.None ? "No hair" : $"{HairNames[(int)Hair]}, {HairColorNames[HairTone].ToLowerInvariant()}")}";
        /// Resources path of the model for the chosen body.
        public static string ModelPath => Body == BodyKind.Female ? "Golfer/golfer_f" : "Golfer/golfer_m";
        public static string BodyLabel => Body == BodyKind.Female ? "♀" : "♂";

        public static void CycleBody() => Body = Body == BodyKind.Male ? BodyKind.Female : BodyKind.Male;
        public static void CycleSkin() => SkinTone = (SkinTone + 1) % SkinTones.Length;

        static Color Rgb(int r, int g, int b) => new(r / 255f, g / 255f, b / 255f);
    }
}
