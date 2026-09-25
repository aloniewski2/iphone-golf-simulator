using System.Collections.Generic;
using GolfArcade.Course;
using GolfArcade.Net.Online;
using GolfArcade.Profile;

namespace GolfArcade.Game
{
    public enum PlayMode { Solo, LocalVersus, Online, Tournament }

    /// What the menu chose: the mode, who plays (in tee order), and the round's seed, which sets
    /// every hole's wind. GolfGame turns it into a Match.
    public sealed class GameSetup
    {
        public PlayMode Mode;
        public string CourseId = "cliffside";
        public int Seed;
        public MatchFormat Format = MatchFormat.StrokePlay;
        readonly List<MatchPlayer> players = new();

        /// The holes, as the server names them: "cliffside" is the whole round, "cliffside-12"
        /// one hole of it (GolfGame's chosen holes, 0 for the round).
        public static string CourseIdFor(int holes) => holes == 0 ? "cliffside" : $"cliffside-{holes}";
        public static int HolesFor(string courseId) =>
            courseId != null && courseId.StartsWith("cliffside-") && int.TryParse(courseId.Substring(10), out var n) ? n : 0;

        /// A fresh copy of the players for a new round (Match gives each a new card).
        public List<MatchPlayer> Players()
        {
            var list = new List<MatchPlayer>();
            foreach (var p in players)
                list.Add(new MatchPlayer { Name = p.Name, Body = p.Body, Kit = p.Kit, Shirt = p.Shirt, ProfileId = p.ProfileId, RemoteId = p.RemoteId, IsLocal = p.IsLocal });
            return list;
        }

        public static GameSetup Solo(PlayerProfile profile, int holes = 0) => Local(PlayMode.Solo, new[] { profile }, holes);

        /// Two (or more) players taking turns on this phone.
        public static GameSetup LocalVersus(IEnumerable<PlayerProfile> profiles, int holes = 0, MatchFormat format = MatchFormat.StrokePlay)
        {
            var setup = Local(PlayMode.LocalVersus, profiles, holes);
            setup.Format = format;
            return setup;
        }

        /// The next round of the Open, for its players on this phone, with the round's winds.
        public static GameSetup Tournament(Championship open, IEnumerable<PlayerProfile> profiles)
        {
            var setup = Local(PlayMode.Tournament, profiles, 0);
            setup.CourseId = open.CourseId;
            setup.Seed = open.RoundSeed;
            return setup;
        }

        static GameSetup Local(PlayMode mode, IEnumerable<PlayerProfile> profiles, int holes)
        {
            var setup = new GameSetup { Mode = mode, Seed = System.Environment.TickCount, CourseId = CourseIdFor(holes) };
            foreach (var p in profiles) setup.players.Add(FromProfile(p));
            return setup;
        }

        /// Everyone in the online room, in seat order; this phone plays as `me`.
        public static GameSetup Online(OnlineRoom room, PlayerProfile me)
        {
            var setup = new GameSetup { Mode = PlayMode.Online, Seed = room.Seed, CourseId = room.CourseId };
            foreach (var p in room.Players)
            {
                bool local = p.id == room.MyId;
                var player = local ? FromProfile(me) : new MatchPlayer { Name = p.name, Body = p.body, Kit = p.kit, Shirt = p.shirt, IsLocal = false };
                player.RemoteId = p.id;
                setup.players.Add(player);
            }
            return setup;
        }

        static MatchPlayer FromProfile(PlayerProfile p) =>
            new() { Name = p.Name, Body = p.Body, Kit = p.Kit, Shirt = p.Shirt, ProfileId = p.Id, IsLocal = true };
    }
}
