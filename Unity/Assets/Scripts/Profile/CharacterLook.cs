using System;

namespace GolfArcade.Profile
{
    /// What a golfer looks like: one versioned record with stable ids (never positions in a list that
    /// might be reordered), saved on the profile and the phone, carried in the same shape by every
    /// sport that wears Adnan's Hero (docs/hero-golfer.md, after his character handoff's "appearance
    /// contract"). A colour is a "RRGGBB" hex string; an empty one keeps the kit as designed.
    /// Public fields and no nullables so Unity's JsonUtility saves it as it is.
    [Serializable]
    public sealed class CharacterLook
    {
        /// 0: not made yet (a profile saved before looks had their own record: see MigrateFrom). 2: Adnan's match heroes replaced the icon
        /// avatar kit, whose haircuts, hats, glasses, facial hair, tops and bottoms are no longer on the golfer (see Repair).
        public const int CurrentVersion = 2;

        public const int Boy = 0, Girl = 1;
        /// How many styles the golfer has of each (the lengths of HeroGolfer's lists, which a test keeps in step); saves are clamped to them.
        public const int Haircuts = 12, Headwears = 1, GlassesStyles = 1, FacialStyles = 1, Tops = 1, Bottoms = 1;
        /// The icon avatar kit's id for no hair (version 1 saves), which is Bald in the list now.
        const int KitBald = 5;

        public int Version = CurrentVersion;
        /// 0 boy, 1 girl (each has its own match hero; any haircut is open to either).
        public int Body;
        public string Skin = "";
        /// HeroGolfer.Haircut; -1 is the classic cut.
        public int Haircut = -1;
        public string Hair = "";
        /// HeroGolfer.Headwear (0 none).
        public int Headwear;
        /// HeroGolfer.GlassesNames (0 none), FacialNames (0 none), TopNames (0 polo), BottomNames (0 shorts).
        public int Glasses, Facial, Top, Bottom;
        /// The shirt, the shorts (and their trim), the shoes, and the cap or band.
        public string Shirt = "", Shorts = "", Shoes = "", Hat = "";

        /// A look nobody has made yet: MigrateFrom fills it.
        public static CharacterLook Unset() => new() { Version = 0 };

        public CharacterLook Clone() => (CharacterLook)MemberwiseClone();

        public bool Female => Body == Girl;

        /// The haircut shown: the one chosen, or the classic cut.
        public int HaircutId => Haircut >= 0 ? Haircut : 0;

        /// The quick-pick colours of the shorts (and trim) and the shirt, as hex: the first of each is the kit as designed.
        /// (Here, not in GolferStyle, so the profile code stays free of engine types and testable on its own.)
        public static readonly string[] KitHex = { "1E2B5A", "128A8C", "E25C4C", "BE242E", "22783E", "34373E" };
        public static readonly string[] ShirtHex = { "F6F7F9", "F4ECD6", "EAD2A4", "BAE0FA", "FAC6D4", "C4F0D6" };

        /// "RRGGBB" (with or without a #): six hex digits.
        public static bool IsHex(string hex)
        {
            if (string.IsNullOrEmpty(hex)) return false;
            hex = hex.TrimStart('#');
            if (hex.Length != 6) return false;
            foreach (var c in hex) if (!Uri.IsHexDigit(c)) return false;
            return true;
        }

        /// A profile saved before looks had their own record: its golfer (male or female) and the kit and shirt
        /// colours it picked from the palettes (0 was the kit as it came).
        public void MigrateFrom(int body, int kitIndex, int shirtIndex)
        {
            Version = CurrentVersion;
            Body = Math.Max(0, Math.Min(1, body));
            if (kitIndex > 0 && kitIndex < KitHex.Length) Shorts = KitHex[kitIndex];
            if (shirtIndex > 0 && shirtIndex < ShirtHex.Length) Shirt = ShirtHex[shirtIndex];
        }

        /// What an older or damaged save may have got wrong, put right.
        public void Repair()
        {
            if (Version == 1)
            {
                // saved with the icon avatar kit: its bald head stays bald, every other cut is the classic cut, and its hats, glasses, beards and clothes are gone
                Haircut = Haircut == KitBald ? 1 : (Haircut >= 0 ? 0 : -1);
                Headwear = Glasses = Facial = Top = Bottom = 0;
                Version = CurrentVersion;
            }
            Body = Math.Max(0, Math.Min(1, Body));
            Haircut = Math.Max(-1, Math.Min(Haircuts - 1, Haircut));
            Headwear = Math.Max(0, Math.Min(Headwears - 1, Headwear));
            Glasses = Math.Max(0, Math.Min(GlassesStyles - 1, Glasses));
            Facial = Math.Max(0, Math.Min(FacialStyles - 1, Facial));
            Top = Math.Max(0, Math.Min(Tops - 1, Top));
            Bottom = Math.Max(0, Math.Min(Bottoms - 1, Bottom));
            Skin = Clean(Skin); Hair = Clean(Hair); Shirt = Clean(Shirt); Shorts = Clean(Shorts); Shoes = Clean(Shoes); Hat = Clean(Hat);
            if (Version < CurrentVersion) Version = CurrentVersion;
        }

        /// A colour as "RRGGBB", or "" (as designed) for anything that isn't one.
        static string Clean(string hex) => IsHex(hex) ? hex.TrimStart('#').ToUpperInvariant() : "";

        public bool SameAs(CharacterLook o) =>
            o != null && Body == o.Body && Skin == o.Skin && Haircut == o.Haircut && Hair == o.Hair && Headwear == o.Headwear && Glasses == o.Glasses
            && Facial == o.Facial && Top == o.Top && Bottom == o.Bottom && Shirt == o.Shirt && Shorts == o.Shorts && Shoes == o.Shoes && Hat == o.Hat;
    }
}
