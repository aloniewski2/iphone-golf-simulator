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
        /// Resources path of the model for the chosen body.
        public static string ModelPath => Body == BodyKind.Female ? "StandardCharacters/standard_female_golf" : "StandardCharacters/standard_male_golf";
        public static string BodyLabel => Body == BodyKind.Female ? "♀" : "♂";

        public static void CycleBody() => Body = Body == BodyKind.Male ? BodyKind.Female : BodyKind.Male;
        public static void CycleSkin() => SkinTone = (SkinTone + 1) % SkinTones.Length;

        static Color Rgb(int r, int g, int b) => new(r / 255f, g / 255f, b / 255f);
    }
}
