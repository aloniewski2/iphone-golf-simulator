using System;
using GolfArcade.Profile;
using UnityEngine;

namespace GolfArcade.Game
{
    /// Who the player looks like: Adnan's Hero (HeroGolfer) dressed by a CharacterLook — boy or girl, skin,
    /// haircut and hair colour, headwear, and the colours of the shirt, shorts, shoes and cap. The device's
    /// own look is kept in PlayerPrefs; with more than one golfer on the phone the round `Wear`s the look of
    /// whoever is up (their profile's), and the locker edits whichever is on. The older V4 figure (the
    /// crowd's, and the stand-in when the Hero isn't in the build) still takes a skin tone, hair colour and
    /// shirt colour from the same look.
    public static class GolferStyle
    {
        public enum BodyKind { Male, Female }

        // ---- Palettes: the quick picks in the locker (any colour can be mixed too).
        /// From light to deep: Adnan's own skin tones, warmed a touch for the course's sun.
        public static readonly Color[] SkinTones =
        {
            Rgb(250, 214, 186), Rgb(232, 190, 146), Rgb(226, 160, 110),
            Rgb(196, 124, 80), Rgb(150, 90, 56), Rgb(96, 60, 40),
        };
        public static readonly string[] SkinToneNames = { "Fair", "Cream", "Tan", "Olive", "Brown", "Deep" };
        public const int DefaultSkin = 1;

        public static readonly Color[] HairColors =
        {
            Rgb(28, 24, 22), Rgb(62, 40, 26), Rgb(118, 78, 48), Rgb(152, 62, 34), Rgb(222, 190, 122), Rgb(204, 206, 212),
        };
        public static readonly string[] HairColorNames = { "Black", "Dark brown", "Brown", "Auburn", "Blonde", "Silver" };
        /// Blonde, as the Hero is designed.
        public const int DefaultHairTone = 4;

        /// The shorts (and their trim) and the shirt: the quick picks; the first of each is the kit as designed.
        public static readonly string[] KitNames = { "Navy", "Teal", "Coral", "Crimson", "Forest", "Charcoal" };
        public static readonly Color[] KitColors = Palette(CharacterLook.KitHex);
        public static readonly string[] ShirtNames = { "White", "Ivory", "Sand", "Sky", "Blush", "Mint" };
        public static readonly Color[] ShirtColors = Palette(CharacterLook.ShirtHex);
        /// Shoes take the shirt palette, and the cap or band the kit's.
        public static readonly Color ShoesAsDesigned = Rgb(227, 226, 222);
        public static readonly Color HatAsDesigned = Rgb(255, 107, 61);

        // ---- The look on screen
        const string LookKey = "golfer.look.v1";
        static CharacterLook device, worn;

        /// The phone's own look (the active profile's); kept in PlayerPrefs.
        public static CharacterLook Device
        {
            get
            {
                if (device != null) return device;
                var json = PlayerPrefs.GetString(LookKey, "");
                if (json.Length > 0)
                {
                    try { device = JsonUtility.FromJson<CharacterLook>(json); }
                    catch (Exception) { device = null; }
                }
                if (device == null || device.Version == 0)
                {
                    // a phone that picked its golfer before looks had their own record: the same golfer, kit and shirt
                    device = CharacterLook.Unset();
                    device.MigrateFrom(PlayerPrefs.GetInt("golfer.body", 0), PlayerPrefs.GetInt("golfer.kit", 0), PlayerPrefs.GetInt("golfer.shirt", 0));
                }
                device.Repair();
                return device;
            }
        }

        public static void SaveDevice(CharacterLook look)
        {
            device = look.Clone();
            PlayerPrefs.SetString(LookKey, JsonUtility.ToJson(device));
            PlayerPrefs.Save();
        }

        public static bool Worn => worn != null;

        /// Another golfer's look on the figure (the round's, the locker's for a profile); the phone's own again after Unwear.
        public static void Wear(CharacterLook look) { worn = look.Clone(); worn.Repair(); }
        public static void Unwear() => worn = null;

        /// The look on screen: the worn one, or the phone's own.
        public static CharacterLook Current => worn ?? Device;

        /// Changes the look on screen (the phone's own is remembered; a worn one is edited in place).
        public static void Edit(Action<CharacterLook> change)
        {
            var look = Current;
            change(look);
            look.Repair();
            if (worn == null) SaveDevice(look);
        }

        /// A look from what the server (or an older save) knows: body, kit and shirt colours.
        public static CharacterLook LookFromLegacy(int body, int kit, int shirt)
        {
            var look = CharacterLook.Unset();
            look.MigrateFrom(body, kit, shirt);
            return look;
        }

        /// Keeps the profile's older fields in step with its look (the server still stores body, kit and shirt).
        public static void SyncLegacy(PlayerProfile p)
        {
            p.Body = p.Look.Body;
            p.Kit = Mathf.Max(0, IndexOf(KitColors, p.Look.Shorts));
            p.Shirt = Mathf.Max(0, IndexOf(ShirtColors, p.Look.Shirt));
        }

        // ---- The pieces of the look as the rest of the game asks for them
        public static BodyKind Body
        {
            get => Current.Body == CharacterLook.Girl ? BodyKind.Female : BodyKind.Male;
            set => Edit(l => l.Body = value == BodyKind.Female ? CharacterLook.Girl : CharacterLook.Boy);
        }

        public static void CycleBody() => Body = Body == BodyKind.Male ? BodyKind.Female : BodyKind.Male;

        public static Color SkinColor => ColorOf(Current.Skin) ?? SkinTones[DefaultSkin];
        public static Color HairColor => ColorOf(Current.Hair) ?? HairColors[DefaultHairTone];

        /// The palette entry a colour is (by hex), or -1 for a mixed colour; an empty hex is `whenEmpty`.
        public static int IndexOf(Color[] palette, string hex, int whenEmpty = 0)
        {
            if (!TryColor(hex, out var c)) return whenEmpty;
            for (int i = 0; i < palette.Length; i++) if (HexOf(palette[i]) == HexOf(c)) return i;
            return -1;
        }

        public static int SkinTone
        {
            get => Mathf.Max(0, IndexOf(SkinTones, Current.Skin, DefaultSkin));
            set => Edit(l => l.Skin = HexOf(SkinTones[Mathf.Clamp(value, 0, SkinTones.Length - 1)]));
        }

        public static void CycleSkin() => SkinTone = (SkinTone + 1) % SkinTones.Length;

        public static int HairTone
        {
            get => Mathf.Max(0, IndexOf(HairColors, Current.Hair, DefaultHairTone));
            set => Edit(l => l.Hair = HexOf(HairColors[Mathf.Clamp(value, 0, HairColors.Length - 1)]));
        }

        public static int Haircut
        {
            get => Current.HaircutId;
            set => Edit(l => l.Haircut = Mathf.Clamp(value, 0, HeroGolfer.HaircutNames.Length - 1));
        }

        public static int Headwear
        {
            get => Current.Headwear;
            set => Edit(l => l.Headwear = Mathf.Clamp(value, 0, HeroGolfer.HeadwearNames.Length - 1));
        }

        /// The kit (shorts) and shirt as palette entries; 0 is the kit as designed (also what a mixed colour reads as).
        public static int Kit
        {
            get => Mathf.Max(0, IndexOf(KitColors, Current.Shorts));
            set => Edit(l => l.Shorts = value <= 0 ? "" : HexOf(KitColors[Mathf.Clamp(value, 0, KitColors.Length - 1)]));
        }

        public static int Shirt
        {
            get => Mathf.Max(0, IndexOf(ShirtColors, Current.Shirt));
            set => Edit(l => l.Shirt = value <= 0 ? "" : HexOf(ShirtColors[Mathf.Clamp(value, 0, ShirtColors.Length - 1)]));
        }

        // ---- Adnan's Hero
        static bool? heroAvailable;
        /// Tests can put the older V4 figure back (false) to compare; null is as the build has it.
        public static bool? HeroOverride;
        public static bool UseHero => HeroOverride ?? (heroAvailable ??= HeroGolfer.Available);

        /// The look on screen as the Hero's own record.
        public static HeroLook Hero => HeroOf(Current);

        public static HeroLook HeroOf(CharacterLook l) => new()
        {
            Female = l.Female,
            Skin = ColorOf(l.Skin) ?? SkinTones[DefaultSkin],
            HairColor = ColorOf(l.Hair) ?? HairColors[DefaultHairTone],
            Haircut = l.HaircutId, Headwear = l.Headwear,
            Shirt = ColorOf(l.Shirt), Shorts = ColorOf(l.Shorts),
            Shoes = ColorOf(l.Shoes), Hat = ColorOf(l.Hat),
        };

        // ---- The older V4 figure (crowd, stand-in): the same skin, hair and shirt
        public enum HairKind { Short, Long, Curly, None }
        public static readonly string[] HairNames = { "Short", "Bob", "Curls", "None" };
        /// The V4 figure's hair mesh for a Hero haircut: none for a bare head, a cut of its own otherwise.
        public static string HairMesh => Current.HaircutId switch { 5 or 6 or 7 => null, 2 or 3 => "HAIR_LONG", 4 => "HAIR_CURLY", _ => "HAIR_SHORT" };
        public static Color? ShirtColor => ColorOf(Current.Shirt) ?? Rgb(62, 62, 66);
        public static Color? TrousersColor => ColorOf(Current.Shorts) ?? Rgb(56, 56, 60);
        /// Resources path of the V4 model for the body.
        public static string ModelPath => Body == BodyKind.Female ? "Golfer/golfer_f" : "Golfer/golfer_m";

        /// A random look (the locker's SHUFFLE): skin, haircut, hair colour, headwear and, where earned, outfit colours.
        public static void Shuffle(CharacterLook l, Func<int, bool> outfitOpen, bool mixerOpen)
        {
            var rng = new System.Random();
            l.Skin = HexOf(Color.Lerp(SkinTones[rng.Next(SkinTones.Length)], SkinTones[rng.Next(SkinTones.Length)], (float)rng.NextDouble()));
            l.Haircut = rng.Next(HeroGolfer.OfferedHaircuts);
            l.Hair = HexOf(HairColors[rng.Next(HairColors.Length)]);
            l.Headwear = rng.Next(HeroGolfer.HeadwearNames.Length);
            int Pick() { for (int tries = 0; tries < 12; tries++) { int i = rng.Next(KitColors.Length); if (outfitOpen(i)) return i; } return 0; }
            l.Shirt = HexOf(ShirtColors[Pick()]); l.Shorts = HexOf(KitColors[Pick()]);
            if (mixerOpen) l.Hat = HexOf(Color.HSVToRGB((float)rng.NextDouble(), 0.75f, 0.95f));
        }

        /// "Girl · Ponytail · Visor": the golfer in a line, for the menus.
        public static string Describe(CharacterLook l) =>
            $"{(l.Female ? "GIRL" : "BOY")}  ·  {HeroGolfer.HaircutNames[Mathf.Clamp(l.HaircutId, 0, HeroGolfer.HaircutNames.Length - 1)].ToUpperInvariant()}  ·  {HeroGolfer.HeadwearNames[Mathf.Clamp(l.Headwear, 0, HeroGolfer.HeadwearNames.Length - 1)].ToUpperInvariant()}";

        public static string Summary => Describe(Current);
        public static string BodyLabel => Body == BodyKind.Female ? "♀" : "♂";

        static Color Rgb(int r, int g, int b) => new(r / 255f, g / 255f, b / 255f);

        // ---- Colours as the look stores them: "RRGGBB"
        public static string HexOf(Color c) =>
            $"{Mathf.Clamp(Mathf.RoundToInt(c.r * 255f), 0, 255):X2}{Mathf.Clamp(Mathf.RoundToInt(c.g * 255f), 0, 255):X2}{Mathf.Clamp(Mathf.RoundToInt(c.b * 255f), 0, 255):X2}";

        public static bool TryColor(string hex, out Color c)
        {
            c = default;
            if (!CharacterLook.IsHex(hex)) return false;
            int v = Convert.ToInt32(hex.TrimStart('#'), 16);
            c = new Color(((v >> 16) & 255) / 255f, ((v >> 8) & 255) / 255f, (v & 255) / 255f, 1f);
            return true;
        }

        /// The colour a look's hex names, or null for "as designed".
        public static Color? ColorOf(string hex) => TryColor(hex, out var c) ? c : null;

        static Color[] Palette(string[] hex)
        {
            var colors = new Color[hex.Length];
            for (int i = 0; i < hex.Length; i++) TryColor(hex[i], out colors[i]);
            return colors;
        }
    }
}
