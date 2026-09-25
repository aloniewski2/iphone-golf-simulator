using GolfArcade.Course;
using UnityEngine;

namespace GolfArcade.Profile
{
    /// The Open in progress, kept on the device between sessions (PlayerPrefs, as JSON), so a
    /// four-round event can be played a round at a time.
    public static class ChampionshipStore
    {
        const string Key = "championship.v1";
        static Championship current;
        static bool loaded;

        /// The event under way (or just finished), or null.
        public static Championship Current
        {
            get
            {
                if (loaded) return current;
                loaded = true;
                var json = PlayerPrefs.GetString(Key, "");
                if (json.Length > 0)
                {
                    try { current = JsonUtility.FromJson<Championship>(json); }
                    catch (System.ArgumentException) { current = null; }
                }
                if (current != null && (current.Field == null || current.Field.Count == 0 || current.Pars == null || current.Pars.Length == 0)) current = null;
                return current;
            }
            set { current = value; loaded = true; Save(); }
        }

        public static void Save()
        {
            if (current == null) PlayerPrefs.DeleteKey(Key);
            else PlayerPrefs.SetString(Key, JsonUtility.ToJson(current));
            PlayerPrefs.Save();
        }
    }
}
