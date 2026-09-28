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
                    catch (System.Exception e) { current = null; Debug.LogError($"The saved Open couldn't be read ({e.Message}): kept as {Key}.unreadable"); }
                }
                if (current != null && (current.Field == null || current.Field.Count == 0 || current.Pars == null || current.Pars.Length == 0)) current = null;
                // an event that can't be played on is kept aside, not silently dropped
                if (current == null && json.Length > 0) { PlayerPrefs.SetString(Key + ".unreadable", json); PlayerPrefs.DeleteKey(Key); PlayerPrefs.Save(); }
                return current;
            }
            set { current = value; loaded = true; Save(); }
        }

        public static void Save()
        {
            try
            {
                if (current == null) PlayerPrefs.DeleteKey(Key);
                else PlayerPrefs.SetString(Key, JsonUtility.ToJson(current));
                PlayerPrefs.Save();
            }
            catch (System.Exception e) { Debug.LogError($"The Open couldn't be saved: {e.Message}"); }
        }
    }
}
