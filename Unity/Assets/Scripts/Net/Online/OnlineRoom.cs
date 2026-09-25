using System;
using System.Collections.Generic;

namespace GolfArcade.Net.Online
{
    /// What this phone knows about its online room, rebuilt from the server's messages. Pure C#
    /// so the lobby and round logic is tested without a socket. The server is the authority:
    /// a `room` message replaces everything, `hole` fills in one score between them.
    public sealed class OnlineRoom
    {
        public string MyId = "";
        public string Code = "";
        public string HostId = "";
        public string CourseId = "cliffside";
        public int Seed;
        public bool Started, Finished, IsPublic;
        public readonly List<OnlinePlayer> Players = new();
        public string LastError = "";

        /// Anything about the room changed (players, host, a score).
        public Action Changed;
        /// The round is on: build a Match with this seed and course. Fires once per room, also
        /// for a phone that rejoins a round already under way.
        public Action<int, string> RoundStarted;
        /// Someone holed out: their id, the hole index, their strokes.
        public Action<string, int, int> HoleScored;
        public Action<string> Error;

        bool startAnnounced;

        public bool InRoom => Code.Length > 0;
        public bool IsHost => MyId.Length > 0 && MyId == HostId;
        public bool CanStart => IsHost && !Started && Players.Count >= 2;

        public OnlinePlayer Find(string id)
        {
            foreach (var p in Players) if (p.id == id) return p;
            return null;
        }

        public void Apply(OnlineMessage m)
        {
            if (m == null) return;
            switch (m.type)
            {
                case "welcome":
                    MyId = m.playerId ?? "";
                    break;

                case "room":
                    if (m.code != Code) startAnnounced = false;
                    Code = m.code ?? "";
                    HostId = m.host ?? "";
                    CourseId = string.IsNullOrEmpty(m.course) ? "cliffside" : m.course;
                    Seed = m.seed;
                    Started = m.started;
                    Finished = m.finished;
                    IsPublic = m.isPublic;
                    Players.Clear();
                    if (m.players != null) Players.AddRange(m.players);
                    LastError = "";
                    Changed?.Invoke();
                    if (Started) AnnounceStart();
                    break;

                case "start":
                    Started = true;
                    Seed = m.seed;
                    if (!string.IsNullOrEmpty(m.course)) CourseId = m.course;
                    AnnounceStart();
                    break;

                case "hole":
                    var player = Find(m.playerId);
                    if (player != null && m.hole >= 0 && m.hole < player.strokes.Length) player.strokes[m.hole] = m.strokes;
                    HoleScored?.Invoke(m.playerId, m.hole, m.strokes);
                    Changed?.Invoke();
                    break;

                case "error":
                    LastError = m.error ?? "";
                    Error?.Invoke(LastError);
                    Changed?.Invoke();
                    break;
            }
        }

        /// Forget the room (after leaving it, or when the connection is dropped on purpose).
        public void Reset()
        {
            Code = HostId = LastError = "";
            Started = Finished = IsPublic = false;
            Seed = 0;
            Players.Clear();
            startAnnounced = false;
            Changed?.Invoke();
        }

        void AnnounceStart()
        {
            if (startAnnounced) return;
            startAnnounced = true;
            RoundStarted?.Invoke(Seed, CourseId);
        }
    }
}
