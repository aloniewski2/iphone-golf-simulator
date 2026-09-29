using GolfArcade.Course;
using GolfArcade.Shot;
using GolfArcade.Swing;
using NUnit.Framework;

namespace GolfArcade.Tests
{
    /// A full swing is the whole club, but not the same number every time: a pure strike comes
    /// off the face faster, a fast swing faster still, and each swing scatters a little (Strikes.Pure).
    public class StrikeDistanceTests
    {
        static readonly Hole Flat = new()
        {
            Number = 99, Par = 4, Centerline = new[] { new CoursePoint(0, 0), new CoursePoint(0, 500) },
            FairwayWidth = 80, GreenRadius = 15, RoughWidth = 80,
        };

        static SwingImpact Swing(double commit, double tempoSeconds = 1.0, double curve = 0) =>
            new() { Power = 1, Commit = commit, TempoSeconds = tempoSeconds, DownswingSeconds = 0.25, CurveDegrees = curve };

        static CourseShot Drive(SwingImpact impact) => new(GolfClub.Driver, impact, 0, new CoursePoint(0, 0), Flat, 1, Wind.Calm);

        [Test]
        public void APureFastStrikeFliesPastTheClubsNumber()
        {
            var planned = Drive(new SwingImpact { Power = 1 });
            Assert.AreEqual(GolfClub.Driver.ReferenceDistanceYards(), planned.Carry, 3, "a planned full swing is the club's number");
            var perfect = Swing(1.2);
            Assert.AreEqual(StrikeGrade.Perfect, Strikes.Judge(perfect).Grade);
            var pure = Drive(Strikes.Pure(perfect, Strikes.Judge(perfect), 0.5, 0.5));
            Assert.Greater(pure.Carry, planned.Carry * 1.04, $"a perfect strike carries on: {pure.Carry:F0} against {planned.Carry:F0}");
            var lashed = Swing(2.2);
            var fastest = Drive(Strikes.Pure(lashed, Strikes.Judge(lashed), 0.5, 0.5));
            Assert.Greater(fastest.Carry, pure.Carry + 3, "and a faster swing further still");
            var sliced = Swing(1.0, 1.0, 12);
            Assert.AreNotEqual(StrikeGrade.Perfect, Strikes.Judge(sliced).Grade);
            Assert.AreEqual(0, Strikes.Pure(sliced, Strikes.Judge(sliced), 0.5, 0.5).SpeedBonus, 1e-9, "a slice off the toe gets nothing extra");
        }

        [Test]
        public void NoTwoSwingsGoTheSameWay()
        {
            var swing = Swing(1.2);
            var report = Strikes.Judge(swing);
            var shortest = Drive(Strikes.Pure(swing, report, 0, 0));
            var longest = Drive(Strikes.Pure(swing, report, 1, 1));
            Assert.Greater(longest.Total - shortest.Total, 6, $"the same swing lands between {shortest.Total:F0} and {longest.Total:F0} yd");
            Assert.Less(longest.Total - shortest.Total, 40, "but never wildly");
            var bonus = Strikes.Pure(swing, report, 1, 1).SpeedBonus;
            Assert.AreEqual(Strikes.PerfectBonus + Strikes.FastBonus * 0.2 + Strikes.SpeedScatter, bonus, 1e-9);
        }
    }
}
