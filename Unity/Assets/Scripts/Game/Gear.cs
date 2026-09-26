using GolfArcade.Profile;
using UnityEngine;

namespace GolfArcade.Game
{
    /// What the rewards (Profile/Unlocks.cs) look like: a ball's colour, the trail it leaves in
    /// the air, a club's finish, and the swatch each shows on the golfer screen.
    public static class Gear
    {
        public enum TrailStyle { None, Sparkle, Fire, Rainbow }

        public static Color BallColor(string id) => id switch
        {
            "ball.yellow" => new Color(1f, 0.9f, 0.22f),
            "ball.orange" => new Color(1f, 0.56f, 0.14f),
            "ball.pink" => new Color(1f, 0.52f, 0.76f),
            "ball.gold" => new Color(1f, 0.78f, 0.26f),
            _ => new Color(0.97f, 0.97f, 0.96f),
        };

        public static TrailStyle Trail(string id) => id switch
        {
            "trail.sparkle" => TrailStyle.Sparkle,
            "trail.fire" => TrailStyle.Fire,
            "trail.rainbow" => TrailStyle.Rainbow,
            _ => TrailStyle.None,
        };

        /// The colour a club's head and shaft take on; null leaves the steel as it is.
        public static Color? ClubFinish(string id) => id switch
        {
            "club.midnight" => new Color(0.13f, 0.14f, 0.2f),
            "club.neon" => new Color(0.38f, 1f, 0.3f),
            "club.gold" => new Color(1f, 0.78f, 0.26f),
            _ => null,
        };

        /// The swatch on the golfer screen's GEAR page.
        public static Color Swatch(Reward r) => r.Kind switch
        {
            RewardKind.Ball => BallColor(r.Id),
            RewardKind.Club => ClubFinish(r.Id) ?? new Color(0.78f, 0.8f, 0.84f),
            RewardKind.Trail => Trail(r.Id) switch
            {
                TrailStyle.Sparkle => new Color(1f, 0.95f, 0.6f),
                TrailStyle.Fire => new Color(1f, 0.42f, 0.1f),
                TrailStyle.Rainbow => new Color(0.75f, 0.35f, 1f),
                _ => new Color(0.85f, 0.88f, 0.92f),
            },
            _ => Color.white,
        };

        /// A reward by name, as it is announced: "Gold ball", "Fire trail", "Wild Isles".
        public static string Title(Reward r) => r.Kind switch
        {
            RewardKind.Ball => $"{r.Name} ball",
            RewardKind.Trail => $"{r.Name} trail",
            RewardKind.Club => $"{r.Name} clubs",
            RewardKind.Outfit => $"{r.Name} outfit",
            _ => r.Name,
        };

        /// The icon a reward is announced with.
        public static string Icon(Reward r) => r.Kind switch
        {
            RewardKind.Course => "flag",
            RewardKind.Outfit => "shirt",
            RewardKind.Ball => "ball",
            RewardKind.Trail => "sparkle",
            _ => "iron",
        };
    }
}
