using System;
using System.Collections.Generic;

namespace GolfArcade.Profile
{
    public enum RewardKind { Course, Outfit, Ball, Trail, Club }

    /// Something a player earns by playing: a course, an outfit colour, a ball, a trail behind it,
    /// a club finish. Each says how it is earned, and is earned by the player's record — so a
    /// reward, once earned, stays earned, and nothing about it needs saving but its announcement.
    public sealed class Reward
    {
        public readonly string Id, Name, How;
        public readonly RewardKind Kind;
        readonly Func<ProfileStats, bool> earned;

        public Reward(string id, RewardKind kind, string name, string how, Func<ProfileStats, bool> earned = null)
        {
            Id = id; Kind = kind; Name = name; How = how; this.earned = earned;
        }

        /// Everyone has it from the start.
        public bool Free => earned == null;
        public bool EarnedBy(ProfileStats stats) => earned == null || (stats != null && earned(stats));
    }

    public static class Unlocks
    {
        public const string WildIsles = "course.wildisles";
        /// The first outfit colours are everyone's; the rest are earned (outfit.3 to outfit.5).
        public const int FreeOutfitColours = 3;

        static int Birdies(ProfileStats s) => s.Birdies + s.Eagles + s.HolesInOne;

        public static readonly Reward[] All =
        {
            new(WildIsles, RewardKind.Course, "Wild Isles", "Beat par on Cliffside", s => s.BestOn("cliffside") <= 0),

            new("outfit.mixer", RewardKind.Outfit, "Colour mixer", "Finish 2 rounds", s => s.RoundsPlayed >= 2),
            new("outfit.3", RewardKind.Outfit, "Crimson", "Finish 5 rounds", s => s.RoundsPlayed >= 5),
            new("outfit.4", RewardKind.Outfit, "Forest", "Beat par on Cliffside", s => s.BestOn("cliffside") <= 0),
            new("outfit.5", RewardKind.Outfit, "Charcoal", "Win 3 matches", s => s.MatchesWon >= 3),

            new("ball.white", RewardKind.Ball, "Classic", ""),
            new("ball.yellow", RewardKind.Ball, "Sunshine", "Make a birdie", s => Birdies(s) >= 1),
            new("ball.orange", RewardKind.Ball, "Tangerine", "Finish 3 rounds", s => s.RoundsPlayed >= 3),
            new("ball.pink", RewardKind.Ball, "Bubblegum", "Make 10 pars", s => s.Pars >= 10),
            new("ball.gold", RewardKind.Ball, "Gold", "Make an eagle", s => s.Eagles + s.HolesInOne >= 1),

            new("trail.none", RewardKind.Trail, "Plain", ""),
            new("trail.sparkle", RewardKind.Trail, "Sparkles", "Finish a round on Wild Isles", s => s.RoundsOn("wildisles") >= 1),
            new("trail.fire", RewardKind.Trail, "Fire", "Hit a 250-yard drive", s => s.LongestDrive >= 250),
            new("trail.rainbow", RewardKind.Trail, "Rainbow", "Beat par on Wild Isles", s => s.BestOn("wildisles") <= 0),

            new("club.classic", RewardKind.Club, "Classic", ""),
            new("club.midnight", RewardKind.Club, "Midnight", "Win a match", s => s.MatchesWon >= 1),
            new("club.neon", RewardKind.Club, "Neon", "Make 5 birdies", s => Birdies(s) >= 5),
            new("club.gold", RewardKind.Club, "Gold", "Make a hole-in-one", s => s.HolesInOne >= 1),
        };

        /// Everything open, whatever the record (the tests, and the course review).
        public static bool Everything;

        public static Reward Find(string id) => Array.Find(All, r => r.Id == id);

        public static List<Reward> Of(RewardKind kind) => new(Array.FindAll(All, r => r.Kind == kind));

        public static bool Has(PlayerProfile p, string id)
        {
            var r = Find(id);
            return r == null || Everything || (p != null && p.AllUnlocked) || r.EarnedBy(p?.Stats);
        }

        /// A course is open to the player: Wild Isles once they have beaten par on Cliffside.
        public static bool CourseOpen(PlayerProfile p, string courseKey) => courseKey != "wildisles" || Has(p, WildIsles);

        /// An outfit colour (a kit or a shirt colour by its index) is open to the player.
        public static bool OutfitOpen(PlayerProfile p, int colour) => colour < FreeOutfitColours || Has(p, $"outfit.{colour}");

        /// The locker's colour sliders for the outfit (any colour, not only the quick picks) are earned.
        public static bool MixerOpen(PlayerProfile p) => Has(p, "outfit.mixer");

        /// The player's pick of a kind, if it is open to them; the free one otherwise.
        public static string Chosen(PlayerProfile p, RewardKind kind)
        {
            string id = kind switch { RewardKind.Ball => p?.Ball, RewardKind.Trail => p?.Trail, RewardKind.Club => p?.Club, _ => null };
            if (!string.IsNullOrEmpty(id) && Find(id)?.Kind == kind && Has(p, id)) return id;
            return Array.Find(All, r => r.Kind == kind && r.Free)?.Id;
        }

        /// Opens everything to the player, their record as it is; nothing is announced for it.
        public static void GrantAll(PlayerProfile p)
        {
            if (p == null) return;
            p.AllUnlocked = true;
            foreach (var r in All) if (!r.Free && !p.Announced.Contains(r.Id)) p.Announced.Add(r.Id);
        }

        /// Rewards the player has earned and not yet been told about, in the catalogue's order;
        /// they are marked as told. Free ones are never news.
        public static List<Reward> Announce(PlayerProfile p)
        {
            var news = new List<Reward>();
            if (p == null) return news;
            foreach (var r in All)
            {
                if (r.Free || !r.EarnedBy(p.Stats) || p.Announced.Contains(r.Id)) continue;
                news.Add(r);
                p.Announced.Add(r.Id);
            }
            return news;
        }
    }
}
