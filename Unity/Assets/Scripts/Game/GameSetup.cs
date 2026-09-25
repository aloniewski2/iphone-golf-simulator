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

        /// The holes, as the server names them: a course's key for its whole round
        /// ("cliffside", "maplebay", "wildisles"), or key-number for one hole of it ("maplebay-17").
        public static string CourseIdFor(string courseKey, int hole) =>
            hole == 0 ? courseKey : $"{Course.Course.Containing(hole)?.Key ?? courseKey}-{hole}";

        /// The course and holes a course id names; Cliffside's round for anything unknown.
        public static Course.Course CourseFor(string courseId)
        {
            int hole = HolesFor(courseId);
            if (hole > 0 && Course.Course.Containing(hole) is Course.Course c)
                return new Course.Course { Name = c.Name, Key = c.Key, Holes = System.Array.FindAll(c.Holes, h => h.Number == hole) };
            string key = courseId != null && courseId.Contains("-") ? courseId.Substring(0, courseId.LastIndexOf('-')) : courseId;
            return Course.Course.ByKey(key) ?? Course.Course.Cliffside();
        }
        public int Seed;
        public MatchFormat Format = MatchFormat.StrokePlay;
        readonly List<MatchPlayer> players = new();

        /// The one hole a course id names, or 0 for a whole round.
        public static int HolesFor(string courseId) =>
            courseId != null && courseId.LastIndexOf('-') is int dash && dash > 0 && int.TryParse(courseId.Substring(dash + 1), out var n) ? n : 0;

        /// A fresh copy of the players for a new round (Match gives each a new card).
        public List<MatchPlayer> Players()
        {
            var list = new List<MatchPlayer>();
            foreach (var p in players)
                list.Add(new MatchPlayer { Name = p.Name, Body = p.Body, Kit = p.Kit, Shirt = p.Shirt, ProfileId = p.ProfileId, RemoteId = p.RemoteId, IsLocal = p.IsLocal });
            return list;
        }

        public static GameSetup Solo(PlayerProfile profile, string courseId = "cliffside") => Local(PlayMode.Solo, new[] { profile }, courseId);

        /// Two (or more) players taking turns on this phone.
        public static GameSetup LocalVersus(IEnumerable<PlayerProfile> profiles, string courseId = "cliffside", MatchFormat format = MatchFormat.StrokePlay)
        {
            var setup = Local(PlayMode.LocalVersus, profiles, courseId);
            setup.Format = format;
            return setup;
        }

        /// The next round of the Open, for its players on this phone, with the round's winds.
        public static GameSetup Tournament(Championship open, IEnumerable<PlayerProfile> profiles)
        {
            var setup = Local(PlayMode.Tournament, profiles, open.CourseId);
            setup.CourseId = open.CourseId;
            setup.Seed = open.RoundSeed;
            return setup;
        }

        static GameSetup Local(PlayMode mode, IEnumerable<PlayerProfile> profiles, string courseId)
        {
            var setup = new GameSetup { Mode = mode, Seed = System.Environment.TickCount, CourseId = courseId };
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
