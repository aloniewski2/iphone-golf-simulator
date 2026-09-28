using GolfArcade.Game;
using UnityEngine;

namespace GolfArcade.Profile
{
    /// The phone's profiles, kept in PlayerPrefs as JSON. The first time it runs it makes one
    /// profile from the golfer the player had already picked (GolferStyle), so nobody loses
    /// their look.
    public static class ProfileStore
    {
        const string Key = "profiles.v1";
        static ProfileBook book;

        public static ProfileBook Book
        {
            get
            {
                if (book != null) return book;
                var json = PlayerPrefs.GetString(Key, "");
                if (json.Length > 0)
                {
                    try { book = JsonUtility.FromJson<ProfileBook>(json); }
                    catch (System.Exception e) { book = null; Debug.LogError($"The saved profiles couldn't be read ({e.Message}): kept as {Key}.unreadable, starting afresh"); }
                    // unreadable saved profiles are kept, not overwritten, so nobody's record is lost
                    if (book == null || book.Profiles == null) { PlayerPrefs.SetString(Key + ".unreadable", json); PlayerPrefs.Save(); }
                }
                if (book != null && book.Profiles != null) book.Profiles.RemoveAll(p => p == null);
                if (book == null || book.Profiles == null || book.Profiles.Count == 0)
                {
                    book = new ProfileBook();
                    var first = book.Add("Player 1");
                    first.Body = (int)GolferStyle.SavedBody;
                    first.Kit = GolferStyle.SavedKit;
                    first.Shirt = GolferStyle.SavedShirt;
                    Save();
                }
                foreach (var p in book.Profiles)
                {
                    // whatever an older save (or a damaged one) left out
                    p.Stats ??= new ProfileStats();
                    p.Stats.Courses ??= new System.Collections.Generic.List<CourseBest>();
                    p.Announced ??= new System.Collections.Generic.List<string>();
                    if (string.IsNullOrEmpty(p.Id)) p.Id = System.Guid.NewGuid().ToString("N");
                    if (string.IsNullOrWhiteSpace(p.Name)) p.Name = "Player";
                    p.Name = PlayerProfile.CleanName(p.Name).Length > 0 ? PlayerProfile.CleanName(p.Name) : "Player";
                    p.ServerId ??= ""; p.ServerToken ??= "";
                    p.Ball ??= "ball.white"; p.Trail ??= "trail.none"; p.Club ??= "club.classic";
                }
                return book;
            }
        }

        public static PlayerProfile Active => Book.Active;

        public static void Save()
        {
            if (book == null) return;
            try
            {
                PlayerPrefs.SetString(Key, JsonUtility.ToJson(book));
                PlayerPrefs.Save();
            }
            catch (System.Exception e) { Debug.LogError($"The profiles couldn't be saved: {e.Message}"); }
        }

        /// The server said this token is unknown: drop it so the profile signs up again.
        public static void ForgetServerToken(string token)
        {
            foreach (var p in Book.Profiles)
                if (p.ServerToken == token) { p.ServerId = ""; p.ServerToken = ""; }
            Save();
        }
    }
}
