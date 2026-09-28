using UnityEngine;

namespace GolfArcade.Tennis
{
    /// The campaign's opponents, as the game needs them: which rig their animations come from,
    /// their skin tone, whether they are the boss, and how they play (a style on a rung of the
    /// difficulty ladder, see OpponentProfile.Rival). Names, rounds and the story belong to the
    /// native menu, which sends the key with the match.
    ///
    /// Each body is built by blender/scripts/build_video_character.py (VARIANTS) and exported
    /// to Resources/Tennis/Opponents/<Key>.
    public sealed class TennisRoster
    {
        public string Key; public bool Female; public Color Skin; public bool Boss;
        /// Playing style (see OpponentProfile.Rival) and rung on the ladder, 0 club .. 1 pro.
        public string Style; public float Rung;
        /// Their look on the one character standard (Hero V4 body + Hero_* clips): skin / hair index into
        /// HeroKit's palettes, headwear (0 none, 1 visor, 2 cap, 3 sweatband) and kit colours. Identity only.
        public int HeroSkin, HeroHair, Headwear; public string Shirt, Shorts, Shoes, Racket;
        /// Haircut (HeroKit.HaircutNames): female rivals wear the female cuts.
        public int Haircut => Key switch { "Suki" => 2, "Lina" => 1, "Rosa" => 3, "Nadia" => 1, "Tama" => 4, "Dex" => 4, _ => 0 };   // Bald / Buzz / Waves return after the head rebuild
        public HeroKit.Style HeroLook => HeroKit.Style.From(HeroSkin, HeroHair, Headwear, TennisLook.Kit.From(Shirt, Shorts, Shoes, Racket, HeroSkin), Haircut, Female);

        public OpponentProfile Profile => OpponentProfile.Rival(Style, Rung);

        public static readonly TennisRoster[] All =
        {
            new() { Key = "Milo",   Female = false, Skin = new Color(.86f, .58f, .38f), Style = "wildcard",   Rung = .05f, HeroSkin = 2, HeroHair = 1, Headwear = 2, Shirt = "FF8A3D", Shorts = "2B2F6B", Shoes = "FFFFFF", Racket = "FF8A3D" },
            new() { Key = "Tama",   Female = false, Skin = new Color(.70f, .46f, .30f), Style = "moonballer", Rung = .20f, HeroSkin = 3, HeroHair = 0, Headwear = 3, Shirt = "1FB5A5", Shorts = "F4F1E8", Shoes = "1FB5A5", Racket = "1B6B63" },
            new() { Key = "Suki",   Female = true,  Skin = new Color(.97f, .78f, .64f), Style = "needle",     Rung = .33f, HeroSkin = 0, HeroHair = 0, Headwear = 1, Shirt = "FF6FA8", Shorts = "3A2C6B", Shoes = "FFFFFF", Racket = "FF6FA8" },
            new() { Key = "Dex",    Female = false, Skin = new Color(.36f, .22f, .14f), Style = "server",     Rung = .45f, HeroSkin = 5, HeroHair = 0, Headwear = 0, Shirt = "D8342C", Shorts = "1B1B1B", Shoes = "D8342C", Racket = "1B1B1B" },
            new() { Key = "Lina",   Female = true,  Skin = new Color(.95f, .80f, .68f), Style = "counter",    Rung = .55f, HeroSkin = 0, HeroHair = 3, Headwear = 1, Shirt = "9C7BFF", Shorts = "F4F1E8", Shoes = "9C7BFF", Racket = "5B3FD1" },
            new() { Key = "Bruno",  Female = false, Skin = new Color(.42f, .26f, .16f), Style = "hammer",     Rung = .65f, HeroSkin = 4, HeroHair = 1, Headwear = 3, Shirt = "2E7D32", Shorts = "1B1B1B", Shoes = "2E7D32", Racket = "1B1B1B" },
            new() { Key = "Rosa",   Female = true,  Skin = new Color(.84f, .60f, .42f), Style = "magician",   Rung = .74f, HeroSkin = 2, HeroHair = 2, Headwear = 2, Shirt = "FF5E5B", Shorts = "2B2F6B", Shoes = "FFFFFF", Racket = "FFC53D" },
            new() { Key = "Jax",    Female = false, Skin = new Color(.92f, .74f, .60f), Style = "sniper",     Rung = .83f, HeroSkin = 1, HeroHair = 4, Headwear = 2, Shirt = "222222", Shorts = "D8342C", Shoes = "222222", Racket = "D8342C" },
            new() { Key = "Nadia",  Female = true,  Skin = new Color(.96f, .84f, .74f), Style = "icequeen",   Rung = .92f, HeroSkin = 0, HeroHair = 4, Headwear = 1, Shirt = "9FD8FF", Shorts = "F4F1E8", Shoes = "9FD8FF", Racket = "3A7BD5" },
            new() { Key = "Viktor", Female = false, Skin = new Color(.93f, .80f, .70f), Style = "boss",       Rung = 1f, Boss = true, HeroSkin = 1, HeroHair = 0, Headwear = 0, Shirt = "14213D", Shorts = "14213D", Shoes = "E8B931", Racket = "E8B931" },
        };

        public static TennisRoster Find(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            foreach (var r in All) if (r.Key == key) return r;
            return null;
        }
    }
}
