using System;
using GolfArcade.Course;
using GolfArcade.Shot;
using GolfArcade.Swing;
using NUnit.Framework;

namespace GolfArcade.Tests
{
    /// The ball against what stands on the course: into a tree's branches and down short of it,
    /// back off a wall, a putt off a trunk, caught in a bush, and stopped by soft sand.
    public class ObstacleTests
    {
        /// A long straight flat hole down +D, a bunker where one is wanted.
        static Hole Straight(params Obstacle[] obstacles) => new()
        {
            Number = 99, Par = 4,
            Centerline = new[] { new CoursePoint(0, 0), new CoursePoint(0, 400) },
            FairwayWidth = 60, GreenRadius = 12, RoughWidth = 30,
            Obstacles = obstacles,
        };

        static SwingImpact Full(double power = 1) => new() { Power = power };

        [Test]
        public void IntoTheBranchesAndDownShortOfTheTree()
        {
            var clear = new CourseShot(GolfClub.Iron, Full(), 0, new CoursePoint(0, 0), Straight());
            // a tall pine 30 yards out, on the line, the ball still climbing through it
            var pine = Obstacle.Tree(0, 30, 0, 22, 4, 3);
            var blocked = new CourseShot(GolfClub.Iron, Full(), 0, new CoursePoint(0, 0), Straight(pine));
            Assert.Greater(clear.Rest.D, 140, "unblocked it goes its distance");
            Assert.Less(blocked.Rest.D, 60, "the branches take its pace");
            Assert.Less(blocked.Rest.DistanceTo(new CoursePoint(0, 30)), 25, "and it drops near the tree");
            Assert.Less(blocked.Carry, 45);
            Assert.GreaterOrEqual(blocked.Knocks.Count, 1, "the hit is on record for the picture and the sound");
            Assert.IsFalse(blocked.Knocks[0].Hard, "into the leaves");
            Assert.AreEqual(0, clear.Knocks.Count);
        }

        [Test]
        public void AClippedCrownKeepsMostOfItsPace()
        {
            var clear = new CourseShot(GolfClub.Iron, Full(), 0, new CoursePoint(0, 0), Straight());
            // the ball just grazes the edge of a round crown
            var at = clear.PositionAt(0.6); double y = at.h;
            var edge = Obstacle.Tree(4.0 + 0.2, at.d, 0, y + 5, 4.2, y - 5, 0.2, false);
            var clipped = new CourseShot(GolfClub.Iron, Full(), 0, new CoursePoint(0, 0), Straight(edge));
            Assert.Less(clipped.Rest.D, clear.Rest.D - 5, "it lost something");
            Assert.Greater(clipped.Rest.D, 60, "but not the shot");
        }

        [Test]
        public void BackOffAWall()
        {
            // a low punch into a wall 25 yards out comes back off it
            var wall = Obstacle.Round(ObstacleKind.Wall, 0, 25, 0, 40, 3);
            var shot = new CourseShot(GolfClub.Iron, Full(0.8), 0, new CoursePoint(0, 0), Straight(wall));
            Assert.Less(shot.Rest.D, 22, "it stays this side of the wall");
            Assert.Greater(shot.Rest.D, -30);
        }

        [Test]
        public void APuttComesBackOffATrunk()
        {
            var tree = Obstacle.Tree(0, 8, 0, 10, 3, 3, 0.3);
            var shot = new CourseShot(GolfClub.Putter, Full(1), 0, new CoursePoint(0, 0), Straight(tree));
            Assert.Less(shot.Rest.D, 7.7, "it never gets past the trunk");
            Assert.IsTrue(shot.Knocks.Count >= 1 && shot.Knocks[0].Hard && double.IsNaN(shot.Knocks[0].Y), "a knock on the ground");
            var clear = new CourseShot(GolfClub.Putter, Full(1), 0, new CoursePoint(0, 0), Straight());
            Assert.Greater(clear.Rest.D, 9, "(without it, it would have)");
        }

        [Test]
        public void ABushCatchesARollingBall()
        {
            var bush = Obstacle.Round(ObstacleKind.Bush, 0, 6, 0, 1.2, 1.0);
            var shot = new CourseShot(GolfClub.Putter, Full(0.8), 0, new CoursePoint(0, 0), Straight(bush));
            Assert.Less(shot.Rest.D, 6.2, "it stops at the bush");
            Assert.Greater(shot.Rest.D, 3.5);
        }

        [Test]
        public void SandStopsARollingBallInAYardOrTwo()
        {
            var hole = Straight();
            hole.Hazards = new[] { new CourseHazard(HazardKind.Bunker, 0, 40, 30, 10) };   // 35 to 45 down the hole
            var shot = new CourseShot(GolfClub.Putter, Full(1), 0, new CoursePoint(0, 28), hole);
            Assert.AreEqual(CourseLie.Bunker, shot.Lie, "it ran into the sand");
            Assert.Less(shot.Rest.D, 37.5, "and stopped just inside it");
            Assert.Greater(CourseShot.RollingDeceleration(CourseLie.Bunker), 5 * CourseShot.RollingDeceleration(CourseLie.Fairway));
        }

        [Test]
        public void ClearOfEverythingTheShotIsUnchanged()
        {
            var off = Obstacle.Tree(40, 60, 0, 12, 3, 3);   // well wide of the line
            var clear = new CourseShot(GolfClub.Driver, Full(), 0, new CoursePoint(0, 0), Straight());
            var shot = new CourseShot(GolfClub.Driver, Full(), 0, new CoursePoint(0, 0), Straight(off));
            Assert.AreEqual(clear.Rest.D, shot.Rest.D, 1e-9);
            Assert.AreEqual(clear.Rest.X, shot.Rest.X, 1e-9);
        }

        [Test]
        public void ShapesAreTheirOwn()
        {
            var pine = Obstacle.Tree(0, 0, 0, 10, 3, 2, 0.2, cone: true);
            Assert.IsTrue(pine.Touches(0.1, 1, 0, 0.05, out bool trunk, out _)); Assert.IsTrue(trunk, "low down it's the trunk");
            Assert.IsTrue(pine.Touches(2.5, 2.5, 0, 0.05, out trunk, out _)); Assert.IsFalse(trunk, "wide low branches");
            Assert.IsFalse(pine.Touches(2.5, 9, 0, 0.05, out _, out _), "the pine narrows to its tip");
            var round = Obstacle.Tree(0, 0, 0, 10, 3, 2, 0.2, cone: false);
            Assert.IsTrue(round.Touches(2.5, 6, 0, 0.05, out _, out _), "a round crown is widest in the middle");
            var rock = Obstacle.Round(ObstacleKind.Rock, 0, 0, 0, 2, 1.5);
            Assert.IsTrue(rock.Touches(0, 1, 1.2, 0.05, out _, out _));
            Assert.IsFalse(rock.Touches(0, 2.5, 0, 0.05, out _, out _));
        }
    }
}
