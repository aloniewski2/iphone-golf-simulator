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

        public OpponentProfile Profile => OpponentProfile.Rival(Style, Rung);

        public static readonly TennisRoster[] All =
        {
            new() { Key = "Milo",   Female = false, Skin = new Color(.86f, .58f, .38f), Style = "wildcard",   Rung = .05f },
            new() { Key = "Tama",   Female = false, Skin = new Color(.70f, .46f, .30f), Style = "moonballer", Rung = .20f },
            new() { Key = "Suki",   Female = true,  Skin = new Color(.97f, .78f, .64f), Style = "needle",     Rung = .33f },
            new() { Key = "Dex",    Female = false, Skin = new Color(.36f, .22f, .14f), Style = "server",     Rung = .45f },
            new() { Key = "Lina",   Female = true,  Skin = new Color(.95f, .80f, .68f), Style = "counter",    Rung = .55f },
            new() { Key = "Bruno",  Female = false, Skin = new Color(.42f, .26f, .16f), Style = "hammer",     Rung = .65f },
            new() { Key = "Rosa",   Female = true,  Skin = new Color(.84f, .60f, .42f), Style = "magician",   Rung = .74f },
            new() { Key = "Jax",    Female = false, Skin = new Color(.92f, .74f, .60f), Style = "sniper",     Rung = .83f },
            new() { Key = "Nadia",  Female = true,  Skin = new Color(.96f, .84f, .74f), Style = "icequeen",   Rung = .92f },
            new() { Key = "Viktor", Female = false, Skin = new Color(.93f, .80f, .70f), Style = "boss",       Rung = 1f, Boss = true },
        };

        public static TennisRoster Find(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            foreach (var r in All) if (r.Key == key) return r;
            return null;
        }
    }
}
