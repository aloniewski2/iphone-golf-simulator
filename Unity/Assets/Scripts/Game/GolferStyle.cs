using UnityEngine;

namespace GolfArcade.Game
{
    /// Who the player looks like: the male or female golfer model and a skin tone. Remembered
    /// on the device between rounds.
    public static class GolferStyle
    {
        public enum BodyKind { Male, Female }
        public static int Hair=1,HairColor=1,Face,Height=2;
        public static float Size=.5f;
        public static GolfArcade.Tennis.TennisLook.Kit Outfit;

        /// From light to deep, the same scale as the character sheet's tan in the middle.
        /// Chroma retune (skin liveliness, 2026-10-02): hue and value ladder kept, colour saturation (HSV S) lifted where the tone rendered pale/grey in the
        /// neutral studio (HeroBase studio, URP Lit, smoothness 0.40; rendered S Fair .224→.278, Light .292→.361, Olive .547→.564, Brown .549→.565, Deep .448→.474).
        /// Colour S: Fair .231→.317 (value 1.00→.95 so the highlights stop clipping to chalk), Light .328→.399, Olive .592→.612, Brown .627→.647, Deep .583→.625.
        /// Tan is held: it already renders at the plate's skin chroma (S≈.48 vs plate ≈.46), raising it would overshoot.
        /// Old → new RGB: Fair 255,224,196→243,201,166 · Light 238,196,160→238,187,143 · Tan 226,160,110 (unchanged)
        /// · Olive 196,124,80→196,122,76 · Brown 150,90,56→150,88,53 · Deep 96,60,40→96,57,36.
        /// HeroBase materials take their BaseColor from these values (neutral albedo), so a tone change never needs a new texture.
        public static readonly Color[] SkinTones =
        {
            Rgb(243, 201, 166), Rgb(238, 187, 143), Rgb(226, 160, 110),
            Rgb(196, 122, 76), Rgb(150, 88, 53), Rgb(96, 57, 36),
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
