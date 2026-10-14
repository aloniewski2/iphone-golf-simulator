using System;
using GolfArcade.Shot;

namespace GolfArcade.Course
{
    /// Golf's own rules as the round's twists and the hole's weather bend them: how hard the ball
    /// falls, how it hops and runs, how quick the greens are, how wide the cup is. Every number is
    /// a multiplier on plain golf (1 changes nothing), so Standard is the game as it always was.
    /// The hole carries its rules (Hole.Rules) and CourseShot reads them there, so the shot, its
    /// forecast on the aim and every phone in a multiplayer round agree.
    public sealed class PlayRules
    {
        public static readonly PlayRules Standard = new();

        /// × gravity on the ball in the air, and on one dropping out of a tree or off a cliff.
        public double Gravity = 1;
        /// × how high the ball hops off the turf.
        public double Bounce = 1;
        /// × the grass's hold on a rolling ball off the green: over 1 is heavier (wet), under 1 runs on.
        public double FairwayDrag = 1;
        /// × the green's hold on a rolling ball: under 1 is quicker.
        public double GreenDrag = 1;
        /// × the curve a strike puts on the ball: a draw or a fade bends this much more.
        public double Curve = 1;
        /// × the cup's mouth.
        public double Cup = 1;
        /// How much harder trees, rocks and walls send the ball back: 1 as they always have, more
        /// for pinball (a trunk springs it back, a crown spits it out).
        public double ObstacleKick = 1;
        /// Greens hold every ball that comes down on them: it checks up where it lands.
        public bool StickyGreens;

        public bool IsStandard => Gravity == 1 && Bounce == 1 && FairwayDrag == 1 && GreenDrag == 1 && Curve == 1 && Cup == 1 && ObstacleKick == 1 && !StickyGreens;

        public PlayRules Copy() { var c = (PlayRules)MemberwiseClone(); c.carry = null; return c; }

        /// What the grass does to a rolling ball here, against plain golf's.
        public double DragOn(CourseLie lie) => lie switch
        {
            CourseLie.Green => GreenDrag,
            CourseLie.Fringe => (GreenDrag + FairwayDrag) / 2,
            CourseLie.Water or CourseLie.Ice or CourseLie.Bunker => 1,
            _ => FairwayDrag,
        };

        /// Gravity in the flight model's yards, under these rules.
        public double GravityYards => CourseShot.GravityYards * Gravity;

        /// Applied to a launch: the flight model's gravity and bounce.
        public void Apply(ref Shot.BallFlight.Launch launch)
        {
            launch.Gravity = Gravity;
            launch.Bounce = Bounce;
        }

        /// How far a full swing with `club` carries under these rules against plain golf's, from
        /// the flight model (cached: it is asked every frame by the aim's yardages and the club
        /// picker). 1 for the putter, whose roll is judged by the green's pace instead.
        public double CarryFactor(Shot.GolfClub club)
        {
            if (club == Shot.GolfClub.Putter) return 1 / Math.Max(0.2, GreenDrag);
            if (Gravity == 1 && Bounce == 1) return 1;
            int i = (int)club;
            carry ??= new double[GolfClubCount];
            if (i < 0 || i >= carry.Length) return 1;
            if (carry[i] > 0) return carry[i];
            var plain = club.Launch(1, 0, 0);
            var bent = plain; Apply(ref bent);
            double a = Shot.BallFlight.Simulate(plain).Carry, b = Shot.BallFlight.Simulate(bent).Carry;
            return carry[i] = a > 1 ? Math.Max(0.3, Math.Min(3, b / a)) : 1;
        }
        double[] carry;
        static readonly int GolfClubCount = Enum.GetValues(typeof(Shot.GolfClub)).Length;
    }
}
