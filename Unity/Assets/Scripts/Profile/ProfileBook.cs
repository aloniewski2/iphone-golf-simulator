using System;
using System.Collections.Generic;

namespace GolfArcade.Profile
{
    /// Every profile on this phone and which one is "me". Pure C#: ProfileStore saves it.
    [Serializable]
    public sealed class ProfileBook
    {
        public const int MaxProfiles = 8;

        public List<PlayerProfile> Profiles = new();
        public string ActiveId = "";

        /// The phone's owner: the solo player, player 1 at home, and the online identity.
        /// Creates the first profile on demand so there is always someone to play as.
        public PlayerProfile Active
        {
            get
            {
                var found = Find(ActiveId);
                if (found != null) return found;
                if (Profiles.Count == 0) Profiles.Add(new PlayerProfile { Name = "Player 1" });
                ActiveId = Profiles[0].Id;
                return Profiles[0];
            }
        }

        public PlayerProfile Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var p in Profiles) if (p.Id == id) return p;
            return null;
        }

        public bool CanAdd => Profiles.Count < MaxProfiles;

        /// A new profile with the next free "Player N" name and a look that differs from the
        /// others where it can, so two golfers on one phone are easy to tell apart.
        public PlayerProfile Add(string name = null)
        {
            if (!CanAdd) return null;
            var profile = new PlayerProfile { Name = PlayerProfile.CleanName(name) };
            if (profile.Name.Length == 0) profile.Name = NextDefaultName();
            var last = Profiles.Count > 0 ? Profiles[Profiles.Count - 1] : null;
            if (last != null)
            {
                profile.Body = 1 - last.Body;
                profile.Kit = (last.Kit + 1) % PlayerProfile.Colours;
            }
            Profiles.Add(profile);
            if (Find(ActiveId) == null) ActiveId = profile.Id;
            return profile;
        }

        public void SetActive(string id)
        {
            if (Find(id) != null) ActiveId = id;
        }

        /// Removes a profile; the last one stays so there is always a player.
        public bool Remove(string id)
        {
            if (Profiles.Count <= 1) return false;
            int n = Profiles.RemoveAll(p => p.Id == id);
            if (n > 0 && ActiveId == id) ActiveId = Profiles[0].Id;
            return n > 0;
        }

        /// A second player for a game at home: the first profile that is not `except`, or a
        /// new one when the phone only knows one player.
        public PlayerProfile Opponent(string except)
        {
            foreach (var p in Profiles) if (p.Id != except) return p;
            return Add();
        }

        string NextDefaultName()
        {
            for (int n = 1; ; n++)
            {
                string candidate = $"Player {n}";
                bool taken = false;
                foreach (var p in Profiles) if (p.Name == candidate) { taken = true; break; }
                if (!taken) return candidate;
            }
        }
    }
}
