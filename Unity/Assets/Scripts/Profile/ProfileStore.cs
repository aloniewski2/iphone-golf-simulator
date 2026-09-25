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
                    catch (System.ArgumentException) { book = null; }
                }
                if (book == null || book.Profiles == null || book.Profiles.Count == 0)
                {
                    book = new ProfileBook();
                    var first = book.Add("Player 1");
                    first.Body = (int)GolferStyle.SavedBody;
                    first.Kit = GolferStyle.SavedKit;
                    first.Shirt = GolferStyle.SavedShirt;
                    Save();
                }
                foreach (var p in book.Profiles) p.Stats ??= new ProfileStats();
                return book;
            }
        }

        public static PlayerProfile Active => Book.Active;

        public static void Save()
        {
            if (book == null) return;
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(book));
            PlayerPrefs.Save();
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
